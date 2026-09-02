/*
 * Copyright (c) 2020 Darío Kondratiuk
 * Copyright (c) 2020 Meir Blachman
 * Modifications copyright (c) Microsoft Corporation. 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Represents an isolated browser context in Chromium (analogous to an incognito window).
    /// Each context has its own cookies, local storage, and set of pages, and can be
    /// closed independently without affecting other contexts.
    /// </summary>
    internal class CRBrowserContext : IAsyncDisposable
    {
        private readonly CRBrowser _browser;
        private readonly string _browserContextId;
        private readonly List<CRPage> _pages = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<CRPage>> _pendingPageCreations = new();
        private readonly ConcurrentDictionary<string, CRPage> _earlyPages = new();
        private readonly ConcurrentDictionary<string, CRWorker> _serviceWorkers = new();
        private readonly List<CRRouteEntry> _routes = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="CRBrowserContext"/> class.
        /// </summary>
        /// <param name="browser">The owning <see cref="CRBrowser"/> instance.</param>
        /// <param name="browserContextId">The CDP browser context ID returned by <c>Target.createBrowserContext</c>.</param>
        public CRBrowserContext(CRBrowser browser, string browserContextId)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _browserContextId = browserContextId;
        }

        /// <summary>
        /// Raised when a Chromium service-worker target attaches to this context.
        /// </summary>
        internal event EventHandler<CRWorker> ServiceWorkerCreated;

        /// <summary>
        /// Gets the owning <see cref="CRBrowser"/> instance.
        /// </summary>
        internal CRBrowser Browser => _browser;

        /// <summary>
        /// Gets the CDP browser context ID.
        /// </summary>
        internal string BrowserContextId => _browserContextId;

        /// <summary>
        /// Official persistent context from <c>launchPersistentContext</c> uses a
        /// null CDP browserContextId (the default profile). Isolated contexts do not.
        /// </summary>
        internal bool IsPersistent => _browser.DefaultContext == this;

        /// <summary>
        /// Public instance that owns context init scripts and page wrappers.
        /// </summary>
        internal ChromiumBrowserContext PublicContext { get; set; }

        /// <summary>
        /// Gets the list of pages currently open in this context.
        /// </summary>
        internal IReadOnlyList<CRPage> Pages
        {
            get
            {
                lock (_routes)
                {
                    return new List<CRPage>(_pages);
                }
            }
        }

        /// <summary>
        /// Context geolocation override applied to every page, including popups.
        /// </summary>
        internal Geolocation Geolocation { get; set; }

        /// <summary>
        /// Service workers currently attached to this context.
        /// </summary>
        internal IReadOnlyCollection<CRWorker> ServiceWorkers
        {
            get
            {
                List<CRWorker> workers = new List<CRWorker>();
                foreach (CRWorker worker in _serviceWorkers.Values)
                {
                    workers.Add(worker);
                }

                return workers;
            }
        }

        /// <summary>
        /// Gets or sets the context proxy. Used to answer <c>Fetch.authRequired</c>.
        /// </summary>
        internal Proxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets HTTP credentials. Used to answer server <c>Fetch.authRequired</c>.
        /// </summary>
        internal IReadOnlyList<HttpCredentials> HttpCredentials { get; set; } = Array.Empty<HttpCredentials>();

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a route handler for every page in this context, current and future.
        /// Pages created after this call (via <see cref="NewPageAsync"/> or external
        /// navigation) inherit the registered routes at attach time through
        /// <see cref="AddPage(string, CRPage)"/>.
        /// </summary>
        /// <param name="entry">The route registration.</param>
        /// <returns>A task that completes when Fetch interception has been enabled on existing pages.</returns>
        internal async Task RouteAsync(CRRouteEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            // Snapshot the existing pages under the same lock that AddPage uses to
            // add to _routes, guaranteeing every (page, route) pair is covered by
            // exactly one path: either this RouteAsync sees the page in _pages, or
            // AddPage sees the route in _routes.
            List<CRPage> existingPages;
            lock (_routes)
            {
                entry.OnExpired = () => RemoveExpired(entry);
                _routes.Add(entry);
                existingPages = new List<CRPage>(_pages);
            }

            foreach (CRPage page in existingPages)
            {
                page.NetworkManager.AddRoute(entry);
                await page.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }

            if (PublicContext != null)
            {
                await PublicContext.UpdateServiceWorkerInterceptionAsync().ConfigureAwait(false);
            }
        }

        internal IReadOnlyList<CRRouteEntry> SnapshotRoutes()
        {
            lock (_routes)
            {
                return new List<CRRouteEntry>(_routes);
            }
        }

        /// <summary>
        /// Drops an expired timed context route from this context and every page.
        /// </summary>
        /// <param name="entry">The route that reached its <c>times</c> limit.</param>
        internal void RemoveExpired(CRRouteEntry entry)
        {
            List<CRPage> existingPages;
            lock (_routes)
            {
                _routes.Remove(entry);
                existingPages = new List<CRPage>(_pages);
            }

            foreach (CRPage page in existingPages)
            {
                page.NetworkManager.RemoveEntry(entry);
            }
        }

        /// <summary>
        /// Registers a glob-pattern context route (Chromium-layer convenience used by tests).
        /// </summary>
        /// <param name="pattern">A glob-style URL pattern (e.g. <c>**/api/**</c>).</param>
        /// <param name="handler">The async handler invoked on matching requests.</param>
        /// <returns>A task that completes when Fetch interception has been enabled on existing pages.</returns>
        internal Task RouteAsync(string pattern, Func<CRRoute, Task> handler)
            => RouteAsync(new CRRouteEntry(pattern, null, null, handler, handler, isContextRoute: true));

        /// <summary>
        /// Removes context-level routes matching the given matcher and optional handler
        /// from this context and every current page.
        /// </summary>
        /// <param name="urlString">Glob used at registration, or <see langword="null"/>.</param>
        /// <param name="urlRegex">Regex used at registration, or <see langword="null"/>.</param>
        /// <param name="urlFunc">Predicate used at registration, or <see langword="null"/>.</param>
        /// <param name="handlerIdentity">Handler to remove, or <see langword="null"/> for all matching matchers.</param>
        /// <param name="behavior">How to treat in-flight handlers.</param>
        /// <returns>A task that completes when Fetch interception has been updated on existing pages.</returns>
        internal async Task UnrouteAsync(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            object handlerIdentity,
            UnrouteBehavior behavior = default)
        {
            List<CRPage> existingPages;
            List<CRRouteEntry> removed = new();
            lock (_routes)
            {
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    if (_routes[i].MatchesRegistration(urlString, urlRegex, urlFunc, handlerIdentity))
                    {
                        removed.Add(_routes[i]);
                        _routes.RemoveAt(i);
                    }
                }

                existingPages = new List<CRPage>(_pages);
            }

            foreach (CRPage page in existingPages)
            {
                page.NetworkManager.RemoveRoute(urlString, urlRegex, urlFunc, handlerIdentity, contextRoute: true);
            }

            List<RouteHandlerLifetime> lifetimes = new();
            for (int i = 0; i < removed.Count; i++)
            {
                lifetimes.Add(removed[i].Lifetime);
            }

            await RouteHandlerLifetime.StopAllAsync(lifetimes, behavior).ConfigureAwait(false);

            foreach (CRPage page in existingPages)
            {
                await page.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }

            if (PublicContext != null)
            {
                await PublicContext.UpdateServiceWorkerInterceptionAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Removes every context-level route from this context and every current page.
        /// </summary>
        /// <param name="behavior">How to treat in-flight handlers.</param>
        /// <returns>A task that completes when Fetch interception has been updated.</returns>
        internal async Task UnrouteAllAsync(UnrouteBehavior behavior = default)
        {
            List<CRPage> existingPages;
            List<CRRouteEntry> removed;
            lock (_routes)
            {
                removed = new List<CRRouteEntry>(_routes);
                _routes.Clear();
                existingPages = new List<CRPage>(_pages);
            }

            foreach (CRPage page in existingPages)
            {
                page.NetworkManager.ClearRoutes(contextRoute: true);
            }

            List<RouteHandlerLifetime> lifetimes = new();
            for (int i = 0; i < removed.Count; i++)
            {
                lifetimes.Add(removed[i].Lifetime);
            }

            await RouteHandlerLifetime.StopAllAsync(lifetimes, behavior).ConfigureAwait(false);

            foreach (CRPage page in existingPages)
            {
                await page.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }

            if (PublicContext != null)
            {
                await PublicContext.UpdateServiceWorkerInterceptionAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a new page in this browser context by sending <c>Target.createTarget</c>
        /// and waiting for the corresponding <c>Target.attachedToTarget</c> event to deliver
        /// the fully initialized <see cref="CRPage"/>.
        /// </summary>
        /// <returns>A task that resolves to the newly created <see cref="CRPage"/>.</returns>
        internal async Task<CRPage> NewPageAsync()
        {
            // We create the TCS now but can't register it in _pendingPageCreations yet —
            // we don't know the targetId until the Target.createTarget response arrives.
            // The actual registration happens below, after we have the targetId.
            var tcs = new TaskCompletionSource<CRPage>(TaskCreationOptions.RunContinuationsAsynchronously);

            JsonElement? response = string.IsNullOrEmpty(_browserContextId)
                ? await _browser.Connection.RootSession
                    .SendAsync("Target.createTarget", new
                    {
                        url = "about:blank",
                    }).ConfigureAwait(false)
                : await _browser.Connection.RootSession
                    .SendAsync("Target.createTarget", new
                    {
                        url = "about:blank",
                        browserContextId = _browserContextId,
                    }).ConfigureAwait(false);

            string targetId = string.Empty;

            if (response.HasValue)
            {
                JsonElement responseElement = response.Value;

                if (responseElement.TryGetProperty("targetId", out JsonElement targetIdElement))
                {
                    targetId = targetIdElement.GetString() ?? string.Empty;
                }
            }

            if (string.IsNullOrEmpty(targetId))
            {
                throw new PlaywrightNativeException("Target.createTarget did not return a targetId.");
            }

            // Register TCS BEFORE checking earlyPages to avoid a race where AddPage
            // runs between the TryRemove check and TryAdd, leaving the TCS unresolved.
            // Race window without this ordering:
            //   1. _earlyPages.TryRemove = false  (page not yet added)
            //   2. [AddPage fires: _pendingPageCreations.TryRemove = false, _earlyPages.TryAdd]
            //   3. _pendingPageCreations.TryAdd    (TCS registered too late - AddPage already ran)
            //   4. await tcs.Task                  (hangs - nobody will set it)
            _pendingPageCreations.TryAdd(targetId, tcs);

            // Double-check: page may have arrived in the window between createTarget
            // response and our TCS registration above.
            if (_earlyPages.TryRemove(targetId, out CRPage earlyPage))
            {
                _pendingPageCreations.TryRemove(targetId, out _);
                return earlyPage;
            }

            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            catch
            {
                _pendingPageCreations.TryRemove(targetId, out _);
                throw;
            }
        }

        /// <summary>
        /// Adds cookies via CDP <c>Storage.setCookies</c>.
        /// </summary>
        /// <param name="cookies">Cookies to install in this context.</param>
        /// <returns>A task that completes when the cookies have been set.</returns>
        internal Task AddCookiesAsync(IEnumerable<Cookie> cookies)
        {
            object[] protocolCookies = Helpers.ContextCookies.ToProtocol(cookies);
            if (string.IsNullOrEmpty(_browserContextId))
            {
                return _browser.Connection.RootSession.SendAsync("Storage.setCookies", new
                {
                    cookies = protocolCookies,
                });
            }

            return _browser.Connection.RootSession.SendAsync("Storage.setCookies", new
            {
                cookies = protocolCookies,
                browserContextId = _browserContextId,
            });
        }

        /// <summary>
        /// Returns cookies via CDP <c>Storage.getCookies</c>.
        /// </summary>
        /// <param name="urls">Optional URL filter.</param>
        /// <returns>Cookies stored in this context.</returns>
        internal async Task<IReadOnlyList<BrowserContextCookiesResult>> GetCookiesAsync(IEnumerable<string> urls)
        {
            JsonElement? result = string.IsNullOrEmpty(_browserContextId)
                ? await _browser.Connection.RootSession.SendAsync("Storage.getCookies").ConfigureAwait(false)
                : await _browser.Connection.RootSession.SendAsync("Storage.getCookies", new
                {
                    browserContextId = _browserContextId,
                }).ConfigureAwait(false);

            return Helpers.ContextCookies.FilterByUrls(
                Helpers.ContextCookies.FromProtocol(result),
                urls);
        }

        /// <summary>
        /// Clears cookies via CDP <c>Storage.clearCookies</c>.
        /// </summary>
        /// <returns>A task that completes when the store has been cleared.</returns>
        internal Task ClearCookiesAsync()
        {
            if (string.IsNullOrEmpty(_browserContextId))
            {
                return _browser.Connection.RootSession.SendAsync("Storage.clearCookies");
            }

            return _browser.Connection.RootSession.SendAsync("Storage.clearCookies", new
            {
                browserContextId = _browserContextId,
            });
        }

        /// <summary>
        /// Grants <paramref name="permissions"/> via CDP <c>Browser.grantPermissions</c>.
        /// </summary>
        /// <param name="permissions">Playwright permission names.</param>
        /// <param name="origin">Optional origin to grant for. When omitted, every origin is granted.</param>
        /// <returns>A task that completes when the grant has been applied.</returns>
        internal async Task GrantPermissionsAsync(IEnumerable<string> permissions, string origin = null)
        {
            try
            {
                await SendGrantPermissionsAsync(
                    Helpers.ContextPermissionMapper.ToChromium(permissions),
                    origin).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException) when (ContainsLocalNetworkAccess(permissions))
            {
                await SendGrantPermissionsAsync(
                    Helpers.ContextPermissionMapper.ToChromium(permissions, localNetworkFallback: true),
                    origin).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Clears permission overrides via CDP <c>Browser.resetPermissions</c>.
        /// </summary>
        /// <returns>A task that completes when permissions have been reset.</returns>
        internal Task ResetPermissionsAsync()
        {
            if (string.IsNullOrEmpty(_browserContextId))
            {
                return _browser.Connection.RootSession.SendAsync("Browser.resetPermissions");
            }

            return _browser.Connection.RootSession.SendAsync("Browser.resetPermissions", new
            {
                browserContextId = _browserContextId,
            });
        }

        /// <summary>
        /// Closes this browser context by sending <c>Target.disposeBrowserContext</c>
        /// and removing it from the owning browser.
        /// </summary>
        /// <returns>A task representing the asynchronous close operation.</returns>
        internal async Task CloseAsync()
        {
            if (string.IsNullOrEmpty(_browserContextId))
            {
                await _browser.CloseAsync().ConfigureAwait(false);
                return;
            }

            await _browser.Connection.RootSession
                .SendAsync("Target.disposeBrowserContext", new
                {
                    browserContextId = _browserContextId,
                }).ConfigureAwait(false);
            _browser.RemoveContext(_browserContextId);
        }

        /// <summary>
        /// Adds a page to this context. Called by <see cref="CRBrowser"/> when a
        /// <c>Target.attachedToTarget</c> event indicates a new page belongs to this context.
        /// If a pending <see cref="NewPageAsync"/> call is waiting for this target, the
        /// corresponding <see cref="TaskCompletionSource{TResult}"/> is completed.
        /// </summary>
        /// <param name="targetId">The CDP target ID of the newly attached page.</param>
        /// <param name="page">The <see cref="CRPage"/> instance to add.</param>
        internal void AddPage(string targetId, CRPage page)
        {
            // Snapshot registered context routes and add the page under the same
            // lock that RouteAsync uses. This guarantees serialization with a
            // concurrent RouteAsync call: either RouteAsync sees this page in
            // _pages and applies the route directly, or AddPage sees the route
            // in _routes and applies it here. No (page, route) pair is missed.
            List<CRRouteEntry> routesSnapshot;
            lock (_routes)
            {
                _pages.Add(page);
                routesSnapshot = _routes.Count > 0
                    ? new List<CRRouteEntry>(_routes)
                    : null;
            }

            page.NetworkManager.SetProxy(Proxy);
            page.NetworkManager.SetHttpCredentials(HttpCredentials);

            if (routesSnapshot != null
                || ProxySettings.HasCredentials(Proxy)
                || HttpBasicAuth.HasCredentials(HttpCredentials))
            {
                if (routesSnapshot != null)
                {
                    foreach (CRRouteEntry entry in routesSnapshot)
                    {
                        page.NetworkManager.AddRoute(entry);
                    }
                }

                // AddPage runs on the CDP dispatcher thread (via CRBrowser.OnAttachedToTarget)
                // and must not block on async CDP work. Fire-and-forget UpdateInterceptionAsync
                // with a try/catch so Fetch.enable failures surface instead of being silently
                // dropped. Route handler failures are still wrapped by CRNetworkManager itself.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await page.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
                    }
                    catch (TargetClosedException)
                    {
                        // Expected on teardown — the session is closing while a new page
                        // is mid-attach. CRTestBase tears down on every test; without this
                        // filter, stderr would get a warning line per test.
                    }
                    catch (Exception ex)
                    {
                        LogUpdateInterceptionError(ex);
                    }
                });
            }

            if (_pendingPageCreations.TryRemove(targetId, out TaskCompletionSource<CRPage> tcs))
            {
                tcs.TrySetResult(page);
            }
            else
            {
                // The page arrived before NewPageAsync registered its TCS.
                // Store it for later pickup.
                _earlyPages.TryAdd(targetId, page);
            }
        }

        /// <summary>
        /// Removes a page from this context. Called by <see cref="CRBrowser"/> when a page
        /// is closed or its target is detached. Takes the <c>_routes</c> lock to serialize
        /// with <see cref="AddPage"/> and <see cref="RouteAsync(CRRouteEntry)"/>, which also mutate / snapshot
        /// <c>_pages</c> under the same lock.
        /// </summary>
        /// <param name="page">The <see cref="CRPage"/> instance to remove.</param>
        internal void RemovePage(CRPage page)
        {
            lock (_routes)
            {
                _pages.Remove(page);
            }
        }

        /// <summary>
        /// Adds a service worker created from <c>Target.attachedToTarget</c>.
        /// </summary>
        /// <param name="targetId">The CDP target ID.</param>
        /// <param name="worker">The attached worker.</param>
        internal void AddServiceWorker(string targetId, CRWorker worker)
        {
            if (string.IsNullOrEmpty(targetId) || worker == null)
            {
                return;
            }

            if (_serviceWorkers.TryAdd(targetId, worker))
            {
                ServiceWorkerCreated?.Invoke(this, worker);
            }
        }

        /// <summary>
        /// Removes a service worker after its target detaches.
        /// </summary>
        /// <param name="targetId">The CDP target ID.</param>
        internal void RemoveServiceWorker(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            _serviceWorkers.TryRemove(targetId, out _);
        }

        private static bool ContainsLocalNetworkAccess(IEnumerable<string> permissions)
        {
            if (permissions == null)
            {
                return false;
            }

            foreach (string permission in permissions)
            {
                if (string.Equals(permission, ContextPermissions.LocalNetworkAccess, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void LogUpdateInterceptionError(Exception ex)
        {
            // Wrapper around Console.Error.WriteLine so VSTHRD103 does not flag the
            // synchronous call inside the async Task.Run continuation in AddPage.
            System.Console.Error.WriteLine(
                $"[CRBrowserContext] UpdateInterception after AddPage failed: {ex.Message}");
        }

        private Task SendGrantPermissionsAsync(string[] mapped, string origin)
        {
            if (string.IsNullOrEmpty(_browserContextId))
            {
                return string.IsNullOrEmpty(origin)
                    ? _browser.Connection.RootSession.SendAsync("Browser.grantPermissions", new { permissions = mapped })
                    : _browser.Connection.RootSession.SendAsync("Browser.grantPermissions", new { origin, permissions = mapped });
            }

            if (string.IsNullOrEmpty(origin))
            {
                return _browser.Connection.RootSession.SendAsync("Browser.grantPermissions", new
                {
                    browserContextId = _browserContextId,
                    permissions = mapped,
                });
            }

            return _browser.Connection.RootSession.SendAsync("Browser.grantPermissions", new
            {
                browserContextId = _browserContextId,
                origin,
                permissions = mapped,
            });
        }
    }
}
