/*
 * Copyright (c) 2020 Dario Kondratiuk
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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Helpers;
using PlaywrightNative.Transport;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Represents a connected Chromium browser instance. Wraps a <see cref="CRConnection"/>
    /// and manages the browser lifecycle including graceful shutdown.
    /// </summary>
    internal class CRBrowser : IAsyncDisposable
    {
        private readonly CRConnection _connection;
        private readonly IConnectionTransport _transport;
        private readonly BrowserProcessManager _processManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, CRBrowserContext> _contexts = new();
        private readonly List<string> _contextOrder = new();
        private readonly object _contextOrderLock = new();
        private readonly ConcurrentDictionary<string, CRPage> _crPages = new();
        private readonly ConcurrentDictionary<string, CRWorker> _serviceWorkers = new();
        private readonly List<PendingOopif> _pendingOopifs = new();
        private CRBrowserContext _defaultContext;
        private bool _closed;
        private bool _noDefaults;
        private bool _adoptingExistingTargets;

        private CRBrowser(CRConnection connection, IConnectionTransport transport, string version, string userAgent, BrowserProcessManager processManager, ILoggerFactory loggerFactory)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Version = version;
            UserAgent = userAgent ?? string.Empty;
            _processManager = processManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<CRBrowser>();

            _connection.Disconnected += OnDisconnected;

            // Listen for target attach/detach events on the root session.
            _connection.RootSession.MessageReceived += OnRootSessionMessage;
        }

        /// <summary>
        /// Raised when the CDP connection drops (browser closed, crashed, or disposed).
        /// </summary>
        internal event EventHandler Disconnected;

        /// <summary>
        /// Gets the browser version string reported by the browser.
        /// </summary>
        internal string Version { get; }

        /// <summary>
        /// Default User-Agent from <c>Browser.getVersion</c>.
        /// </summary>
        internal string UserAgent { get; }

        /// <summary>
        /// Gets a value indicating whether the browser is still connected.
        /// </summary>
        internal bool IsConnected => !_closed && !_connection.IsClosed;

        /// <summary>
        /// Gets the underlying CDP connection.
        /// </summary>
        internal CRConnection Connection => _connection;

        /// <summary>
        /// Gets the contexts currently open in this browser.
        /// </summary>
        internal IReadOnlyCollection<CRBrowserContext> Contexts
        {
            get
            {
                List<CRBrowserContext> contexts = new List<CRBrowserContext>();

                // Official connectOverCDP: expose Target.getBrowserContexts
                // (Playwright newContext / newPage) and only fall back to the
                // default chrome context when no created context exists. That
                // keeps leftover launch about:blank off contexts()[0].
                if (_processManager == null)
                {
                    lock (_contextOrderLock)
                    {
                        foreach (string id in _contextOrder)
                        {
                            if (_contexts.TryGetValue(id, out CRBrowserContext created))
                            {
                                contexts.Add(created);
                            }
                        }
                    }

                    if (contexts.Count == 0 && _defaultContext != null)
                    {
                        contexts.Add(_defaultContext);
                    }

                    return contexts;
                }

                if (_defaultContext != null)
                {
                    contexts.Add(_defaultContext);
                }

                lock (_contextOrderLock)
                {
                    foreach (string id in _contextOrder)
                    {
                        if (_contexts.TryGetValue(id, out CRBrowserContext context))
                        {
                            contexts.Add(context);
                        }
                    }
                }

                return contexts;
            }
        }

        /// <summary>
        /// Default context used by <c>LaunchPersistentContextAsync</c>.
        /// </summary>
        internal CRBrowserContext DefaultContext => _defaultContext;

        /// <summary>
        /// Pages currently attached to this browser.
        /// </summary>
        internal IReadOnlyCollection<CRPage> AttachedPages
        {
            get
            {
                List<CRPage> pages = new List<CRPage>();
                foreach (CRPage page in _crPages.Values)
                {
                    pages.Add(page);
                }

                return pages;
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Connects to a Chromium browser over an existing <see cref="CRConnection"/>,
        /// retrieves the browser version, and configures automatic target attachment.
        /// </summary>
        /// <param name="connection">The CDP connection to use.</param>
        /// <param name="transport">The underlying transport for raw protocol messages (e.g., Browser.close).</param>
        /// <param name="processManager">
        /// Optional process manager that owns the browser process. When provided,
        /// the browser will kill the process on disposal.
        /// </param>
        /// <param name="loggerFactory">Optional logger factory for diagnostic output.</param>
        /// <param name="persistent">When <see langword="true"/>, create the default context before auto-attach.</param>
        /// <param name="noDefaults">
        /// Official <c>connectOverCDP({ noDefaults })</c>. Skip default
        /// overrides on targets that already exist when connecting.
        /// </param>
        /// <param name="headless">
        /// When <see langword="false"/>, keep the automatic launch page so headed
        /// Chromium retains a window; when <see langword="true"/>, close it.
        /// </param>
        /// <returns>A fully initialized <see cref="CRBrowser"/> instance.</returns>
        internal static async Task<CRBrowser> ConnectAsync(
            CRConnection connection,
            IConnectionTransport transport,
            BrowserProcessManager processManager = null,
            ILoggerFactory loggerFactory = null,
            bool persistent = false,
            bool noDefaults = false,
            bool headless = true)
        {
            // Retrieve browser version information.
            JsonElement? versionResponse = await connection.RootSession
                .SendAsync("Browser.getVersion").ConfigureAwait(false);

            string version = string.Empty;
            string userAgent = string.Empty;

            if (versionResponse.HasValue)
            {
                JsonElement versionElement = versionResponse.Value;

                if (versionElement.TryGetProperty("product", out JsonElement productElement))
                {
                    string product = productElement.GetString() ?? string.Empty;
                    int slash = product.IndexOf('/');
                    version = slash >= 0 && slash + 1 < product.Length
                        ? product.Substring(slash + 1)
                        : product;
                }

                if (versionElement.TryGetProperty("userAgent", out JsonElement userAgentElement))
                {
                    userAgent = userAgentElement.GetString() ?? string.Empty;
                }
            }

            CRBrowser browser = new(connection, transport, version, userAgent, processManager, loggerFactory);
            browser._noDefaults = noDefaults;
            browser._adoptingExistingTargets = noDefaults && processManager == null;

            // Official connectOverCDP passes persistent so unknown targets land
            // in the default context. Playwright-created contexts are adopted
            // from Target.getBrowserContexts before auto-attach.
            if (persistent || processManager == null)
            {
                browser._defaultContext = new CRBrowserContext(browser, browserContextId: null);
            }

            if (processManager == null)
            {
                await browser.AdoptExistingBrowserContextsAsync().ConfigureAwait(false);
            }

            // Enable auto-attach so the browser sends Target.attachedToTarget events
            // for every new target (page, worker, service worker, etc.).
            await connection.RootSession
                .SendAsync("Target.setAutoAttach", new
                {
                    autoAttach = true,
                    waitForDebuggerOnStart = true,
                    flatten = true,
                }).ConfigureAwait(false);

            browser.FlushPendingOopifs();
            if (processManager == null)
            {
                await browser.AdoptExistingOopifsAsync().ConfigureAwait(false);
            }

            browser._adoptingExistingTargets = false;
            if (!persistent && processManager != null && headless)
            {
                // Official launch() uses --no-startup-window. Websocket launch
                // still needs leftover about:blank to start; close those pages
                // so Target.setDiscoverTargets sees none. Headed Chrome must keep
                // at least one window or Target.createTarget fails with
                // "Failed to open a new tab".
                await browser.CloseAutomaticLaunchPagesAsync().ConfigureAwait(false);
            }

            return browser;
        }

        /// <summary>
        /// Official <c>browserType.launch()</c> does not create pages
        /// (<c>--no-startup-window</c>). Websocket launch still needs
        /// leftover <c>about:blank</c> to start; close those pages so
        /// <c>Target.setDiscoverTargets</c> sees none.
        /// </summary>
        /// <returns>A <see cref="Task"/> that completes when leftover pages are closed.</returns>
        internal async Task CloseAutomaticLaunchPagesAsync()
        {
            List<CRPage> pages = new List<CRPage>();
            foreach (CRPage page in _crPages.Values)
            {
                pages.Add(page);
            }

            foreach (CRPage page in pages)
            {
                try
                {
                    await page.ClosePageAsync(runBeforeUnload: false).ConfigureAwait(false);
                    await Task.WhenAny(page.ClosedTask, Task.Delay(2000)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Closing leftover launch page failed");
                }
            }

            // Official launch() has no contexts until newContext(). Leftover
            // about:blank adopted Chrome's default profile; drop the empty
            // tracking entry (it cannot Target.disposeBrowserContext).
            lock (_contextOrderLock)
            {
                List<string> leftover = new List<string>(_contextOrder);
                foreach (string id in leftover)
                {
                    if (_contexts.TryGetValue(id, out CRBrowserContext context)
                        && context.Pages.Count == 0)
                    {
                        _contexts.TryRemove(id, out _);
                        _contextOrder.Remove(id);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new isolated browser context.
        /// </summary>
        /// <returns>The newly created <see cref="CRBrowserContext"/>.</returns>
        internal async Task<CRBrowserContext> NewContextAsync(Proxy proxy = null)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["disposeOnDetach"] = true,
            };
            string proxyServer = ProxySettings.FormatServer(proxy, includeCredentials: false);
            if (!string.IsNullOrEmpty(proxyServer))
            {
                parameters["proxyServer"] = proxyServer;
                parameters["proxyBypassList"] = ProxySettings.FormatBypassList(proxy);
            }

            JsonElement? response = await _connection.RootSession
                .SendAsync("Target.createBrowserContext", parameters).ConfigureAwait(false);

            string browserContextId = string.Empty;

            if (response.HasValue &&
                response.Value.TryGetProperty("browserContextId", out JsonElement idElement))
            {
                browserContextId = idElement.GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(browserContextId))
            {
                throw new PlaywrightNativeException("Target.createBrowserContext did not return a browserContextId.");
            }

            CRBrowserContext context = new(this, browserContextId);
            context.Proxy = proxy;
            if (_contexts.TryAdd(browserContextId, context))
            {
                lock (_contextOrderLock)
                {
                    _contextOrder.Add(browserContextId);
                }
            }

            return context;
        }

        /// <summary>
        /// Gracefully closes the browser by sending a <c>Browser.close</c> CDP command
        /// and waiting for the process to exit.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous close operation.</returns>
        internal async Task CloseAsync()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;

            if (_processManager != null)
            {
                try
                {
                    // Send Browser.close directly on the transport with the special ID
                    // so CRConnection ignores the response. Bound the wait so a
                    // stuck websocket cannot leak the process past EnsureExit.
                    Task close = ChromiumBrowserType.AttemptToGracefullyCloseBrowserAsync(_transport);
                    await Task.WhenAny(close, Task.Delay(2000)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The browser may have already disconnected.
                    _logger?.LogDebug(ex, "Browser.close failed, browser may have already disconnected");
                }

                // Wait for the process to exit (Browser.close should cause it to exit).
                // If it doesn't exit within a reasonable time, kill it.
                await _processManager.EnsureExitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            else
            {
                // Official connectOverCDP close only disconnects the websocket.
                try
                {
                    await _transport.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "connectOverCDP transport close failed");
                }
            }

            CloseRemainingPages();

            try
            {
                _connection.Disconnected -= OnDisconnected;
            }
#pragma warning disable RCS1075
            catch (Exception)
            {
            }
#pragma warning restore RCS1075

            try
            {
                _connection.Dispose();
            }
#pragma warning disable RCS1075
            catch (Exception)
            {
            }
#pragma warning restore RCS1075

            if (_processManager != null)
            {
                try
                {
                    _processManager.Dispose();
                }
#pragma warning disable RCS1075
                catch (Exception)
                {
                }
#pragma warning restore RCS1075
            }

            RaiseDisconnected();
        }

        /// <summary>
        /// Removes a context from the browser's tracking when it is closed.
        /// Called by <see cref="CRBrowserContext.CloseAsync"/>.
        /// </summary>
        /// <param name="browserContextId">The context ID to remove.</param>
        internal void RemoveContext(string browserContextId)
        {
            _contexts.TryRemove(browserContextId, out _);
            lock (_contextOrderLock)
            {
                _contextOrder.Remove(browserContextId);
            }
        }

        /// <summary>
        /// Official connectOverCDP: adopt a context from <c>targetInfo.browserContextId</c>.
        /// </summary>
        /// <param name="browserContextId">CDP context id, or empty for the default context.</param>
        /// <returns>The existing or newly created context.</returns>
        internal CRBrowserContext GetOrCreateContext(string browserContextId)
        {
            if (string.IsNullOrEmpty(browserContextId))
            {
                return _defaultContext ??= new CRBrowserContext(this, browserContextId: null);
            }

            if (_contexts.TryGetValue(browserContextId, out CRBrowserContext existing))
            {
                return existing;
            }

            // Official CRBrowser: unknown browserContextId belongs to the
            // default chrome profile (persistent launch and connectOverCDP).
            // Extension service workers attach with the profile id, which is
            // not in Target.createBrowserContext / _contexts.
            if (_defaultContext != null)
            {
                return _defaultContext;
            }

            CRBrowserContext created = new CRBrowserContext(this, browserContextId);
            if (_contexts.TryAdd(browserContextId, created))
            {
                lock (_contextOrderLock)
                {
                    _contextOrder.Add(browserContextId);
                }

                return created;
            }

            return _contexts.TryGetValue(browserContextId, out CRBrowserContext raced)
                ? raced
                : created;
        }

        /// <summary>
        /// Attaches a flattened CDP session to an existing target (page).
        /// </summary>
        /// <param name="targetId">The CDP target identifier.</param>
        /// <returns>The child session for raw CDP commands.</returns>
        internal async Task<CRSession> AttachToTargetAsync(string targetId)
        {
            JsonElement? response = await _connection.RootSession.SendAsync("Target.attachToTarget", new
            {
                targetId,
                flatten = true,
            }).ConfigureAwait(false);

            return SessionFromAttachResponse(response, "Target.attachToTarget");
        }

        /// <summary>
        /// Attaches a flattened CDP session to the browser target.
        /// </summary>
        /// <returns>The child session for browser-level CDP commands.</returns>
        internal async Task<CRSession> AttachToBrowserTargetAsync()
        {
            JsonElement? response = await _connection.RootSession.SendAsync("Target.attachToBrowserTarget", new
            {
                flatten = true,
            }).ConfigureAwait(false);

            return SessionFromAttachResponse(response, "Target.attachToBrowserTarget");
        }

        private CRSession SessionFromAttachResponse(JsonElement? response, string method)
        {
            string sessionId = string.Empty;
            if (response.HasValue && response.Value.TryGetProperty("sessionId", out JsonElement idElement))
            {
                sessionId = idElement.GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(sessionId))
            {
                throw new PlaywrightNativeException($"{method} did not return a sessionId.");
            }

            if (_connection.Sessions.TryGetValue(sessionId, out CRSession existing))
            {
                return existing;
            }

            return _connection.RootSession.CreateChildSession(sessionId);
        }

        private void OnRootSessionMessage(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Target.attachedToTarget":
                    OnAttachedToTarget(parameters);
                    break;
                case "Target.detachedFromTarget":
                    OnDetachedFromTarget(parameters);
                    break;
                case "Browser.downloadWillBegin":
                    OnBrowserDownloadWillBegin(parameters);
                    break;
                case "Browser.downloadProgress":
                    OnBrowserDownloadProgress(parameters);
                    break;
            }
        }

        private void OnBrowserDownloadWillBegin(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string frameId = p.TryGetProperty("frameId", out JsonElement frameEl) ? frameEl.GetString() : string.Empty;
            string guid = p.TryGetProperty("guid", out JsonElement guidEl) ? guidEl.GetString() : string.Empty;
            string url = p.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : string.Empty;
            string suggested = p.TryGetProperty("suggestedFilename", out JsonElement nameEl) ? nameEl.GetString() : string.Empty;
            CRPage page = FindPageForDownload(frameId);
            if (page != null
                && page.Opener != null
                && !page.HasCommittedNonInitialNavigation)
            {
                page = page.Opener;
            }

            page?.NotifyDownloadWillBegin(guid, url, suggested);
        }

        private void OnBrowserDownloadProgress(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string guid = p.TryGetProperty("guid", out JsonElement guidEl) ? guidEl.GetString() : string.Empty;
            string state = p.TryGetProperty("state", out JsonElement stateEl) ? stateEl.GetString() : string.Empty;

            // Progress is not scoped to a frame. Notify every page so the instance
            // that adopted the download (often the opener) can complete it.
            foreach (CRPage page in _crPages.Values)
            {
                page.NotifyDownloadProgress(guid, state, error: null);
            }
        }

        private CRPage FindPageForOopif(string frameId, string parentFrameId = null)
        {
            if (!string.IsNullOrEmpty(frameId))
            {
                foreach (CRPage page in _crPages.Values)
                {
                    if (page.FrameManager.FrameById(frameId) != null)
                    {
                        return page;
                    }
                }
            }

            if (!string.IsNullOrEmpty(parentFrameId))
            {
                foreach (CRPage page in _crPages.Values)
                {
                    if (page.FrameManager.FrameById(parentFrameId) != null)
                    {
                        return page;
                    }
                }
            }

            CRPage only = null;
            foreach (CRPage page in _crPages.Values)
            {
                if (only != null)
                {
                    return null;
                }

                only = page;
            }

            return only;
        }

        private CRPage FindPageForDownload(string frameId)
        {
            if (!string.IsNullOrEmpty(frameId))
            {
                foreach (CRPage page in _crPages.Values)
                {
                    if (page.FrameManager.FrameById(frameId) != null)
                    {
                        return page;
                    }
                }
            }

            foreach (CRPage page in _crPages.Values)
            {
                return page;
            }

            return null;
        }

        private void OnAttachedToTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;

            if (!payload.TryGetProperty("targetInfo", out JsonElement targetInfo))
            {
                return;
            }

            string type = targetInfo.TryGetProperty("type", out JsonElement typeElement)
                ? typeElement.GetString()
                : string.Empty;

            string targetId = targetInfo.TryGetProperty("targetId", out JsonElement tidElement)
                ? tidElement.GetString()
                : string.Empty;

            string sessionId = payload.TryGetProperty("sessionId", out JsonElement sidElement)
                ? sidElement.GetString()
                : string.Empty;

            string browserContextId = targetInfo.TryGetProperty("browserContextId", out JsonElement ctxElement)
                ? ctxElement.GetString()
                : string.Empty;

            if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            CRBrowserContext context = GetOrCreateContext(browserContextId);

            if (string.Equals(type, "service_worker", StringComparison.Ordinal))
            {
                AttachServiceWorker(targetId, sessionId, context, targetInfo);
                return;
            }

            if (string.Equals(type, "iframe", StringComparison.Ordinal)
                || string.Equals(type, "guest", StringComparison.Ordinal))
            {
                CRSession iframeSession = _connection.RootSession.CreateChildSession(sessionId);
                string parentFrameId = targetInfo.TryGetProperty("parentFrameId", out JsonElement parentFrameEl)
                    ? parentFrameEl.GetString()
                    : string.Empty;
                CRPage owner = FindPageForOopif(targetId, parentFrameId);
                if (owner != null)
                {
                    owner.AttachOopifSession(iframeSession, targetId, parentFrameId);
                }
                else if (_processManager == null)
                {
                    lock (_pendingOopifs)
                    {
                        _pendingOopifs.Add(new PendingOopif(iframeSession, targetId, parentFrameId));
                    }
                }
                else
                {
                    _ = iframeSession.SendAsync("Runtime.runIfWaitingForDebugger");
                }

                return;
            }

            // We only create pages for page targets. Other types (shared_worker,
            // browser, ...) still need a session so waitForDebuggerOnStart
            // does not leave them paused. Dedicated workers stay paused until
            // the page FrameSession runs Runtime.enable (official order).
            if (type != "page")
            {
                CRSession other = _connection.RootSession.CreateChildSession(sessionId);
                if (!string.Equals(type, "worker", StringComparison.Ordinal))
                {
                    _ = other.SendAsync("Runtime.runIfWaitingForDebugger");
                }

                return;
            }

            // Extra attach (user CDP session) for a page we already own — register
            // the session for routing and do not create a second CRPage.
            if (_crPages.ContainsKey(targetId))
            {
                _connection.RootSession.CreateChildSession(sessionId);
                return;
            }

            // Create a child session for this page target.
            CRSession pageSession = _connection.RootSession.CreateChildSession(sessionId);

            // Create the CRPage and initialize it.
            CRPage crPage = new(pageSession, targetId, this, _loggerFactory);
            crPage.SkipDefaultOverrides = _noDefaults && _adoptingExistingTargets;
            _crPages.TryAdd(targetId, crPage);

            // Notify the context that a page was created.
            context?.AddPage(targetId, crPage);

            // Create the public instance before InitializeAsync so context init
            // scripts can be installed prior to Runtime.runIfWaitingForDebugger.
            context?.PublicContext?.GetOrCreatePage(crPage);
            FlushPendingOopifs();

            // Detect popups: if this target was opened by another page we're tracking,
            // fire PopupOpened on the opener.
            string openerId = targetInfo.TryGetProperty("openerId", out JsonElement openerEl)
                ? openerEl.GetString()
                : string.Empty;

            if (!string.IsNullOrEmpty(openerId) && _crPages.TryGetValue(openerId, out CRPage openerPage))
            {
                // noopener + URL: Chrome first creates a blank intermediate (no
                // openerId), then a real target whose openerId is that
                // intermediate. Promote to the original page waiting for Popup.
                // Nested window.open from a popup that is itself waiting for
                // 'popup' must fire on that popup, not the original page.
                if (openerPage.Opener != null
                    && openerPage.PublicPage?.HasPopupListeners != true)
                {
                    PopupOpenedHelper.Suppress(openerPage.PublicPage);
                    SuppressBlankSiblings(context, crPage);
                    crPage.Opener = openerPage.Opener;
                    crPage.WindowOpenViewport = openerPage.Opener.TakeNextWindowOpenViewport()
                        ?? openerPage.TakeNextWindowOpenViewport();
                    openerPage.Opener.FirePopupOpened(crPage);
                }
                else
                {
                    crPage.Opener = openerPage;
                    crPage.WindowOpenViewport = openerPage.TakeNextWindowOpenViewport();
                    openerPage.FirePopupOpened(crPage);
                }
            }
            else
            {
                CRPage inferred = PopupOpenedHelper.InferListenerOrSoleSibling(
                    context?.Pages,
                    crPage,
                    page => page.PublicPage?.HasPopupListeners == true);
                if (inferred?.PublicPage != null && inferred.PublicPage.HasPopupListeners)
                {
                    crPage.Opener = inferred;
                    crPage.WindowOpenViewport = inferred.TakeNextWindowOpenViewport();
                    string newUrl = targetInfo.TryGetProperty("url", out JsonElement urlEl)
                        ? urlEl.GetString() ?? string.Empty
                        : string.Empty;
                    if (PopupOpenedHelper.IsBlankUrl(newUrl))
                    {
                        crPage.PopupEmitDelayMs = 300;
                    }
                    else
                    {
                        SuppressBlankSiblings(context, crPage);
                    }

                    inferred.FirePopupOpened(crPage);
                }
            }

            // Initialize the page asynchronously (enable CDP domains).
            _ = crPage.InitializeAsync().ContinueWith(
                t => _logger?.LogError(t.Exception, "Failed to initialize page {TargetId}", targetId),
                default,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private void SuppressBlankSiblings(CRBrowserContext context, CRPage keep)
        {
            if (context?.Pages == null)
            {
                return;
            }

            foreach (CRPage page in context.Pages)
            {
                if (page == null || ReferenceEquals(page, keep))
                {
                    continue;
                }

                string url = page.MainFrame?.Url ?? string.Empty;
                if (PopupOpenedHelper.IsBlankUrl(url))
                {
                    PopupOpenedHelper.Suppress(page.PublicPage);
                }
            }
        }

        private void AttachServiceWorker(string targetId, string sessionId, CRBrowserContext context, JsonElement targetInfo)
        {
            CRSession session = _connection.RootSession.CreateChildSession(sessionId);

            // Extra attach for a service worker we already own — resume and
            // do not create a second CRWorker.
            if (_serviceWorkers.ContainsKey(targetId))
            {
                _ = session.SendAsync("Runtime.runIfWaitingForDebugger");
                return;
            }

            string url = targetInfo.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : string.Empty;

            CRWorker worker = new(session, sessionId, url);
            if (!_serviceWorkers.TryAdd(targetId, worker))
            {
                _ = session.SendAsync("Runtime.runIfWaitingForDebugger");
                return;
            }

            context?.AddServiceWorker(targetId, worker);
            _ = InitializeServiceWorkerAsync(worker, context);
        }

        private async Task InitializeServiceWorkerAsync(CRWorker worker, CRBrowserContext context)
        {
            try
            {
                if (context?.PublicContext == null)
                {
                    // Official CRServiceWorker applies UA/network before
                    // Runtime.runIfWaitingForDebugger. Persistent extension
                    // workers attach during setAutoAttach before the instance
                    // exists — keep them paused until AdoptExisting. For
                    // connectOverCDP, still resume so page.goto(sw.html) can
                    // finish while the default-context instance is created.
                    await worker.InitializeAsync().ConfigureAwait(false);
                    return;
                }

                await context.PublicContext.PrepareServiceWorkerNetworkAsync(worker).ConfigureAwait(false);
                await worker.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize service worker");
            }
        }

        private void OnDetachedFromTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;

            string targetId = payload.TryGetProperty("targetId", out JsonElement tidElement)
                ? tidElement.GetString()
                : string.Empty;

            string sessionId = payload.TryGetProperty("sessionId", out JsonElement sidElement)
                ? sidElement.GetString()
                : string.Empty;

            if (string.IsNullOrEmpty(targetId) && string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            if (!string.IsNullOrEmpty(targetId)
                && _crPages.TryGetValue(targetId, out CRPage crPage)
                && (string.IsNullOrEmpty(sessionId) || string.Equals(crPage.Session.SessionId, sessionId, StringComparison.Ordinal)))
            {
                _crPages.TryRemove(targetId, out _);

                _defaultContext?.RemovePage(crPage);
                foreach (CRBrowserContext context in _contexts.Values)
                {
                    context.RemovePage(crPage);
                }

                crPage.DidClose();
                return;
            }

            if (!string.IsNullOrEmpty(targetId)
                && _serviceWorkers.TryGetValue(targetId, out CRWorker serviceWorker)
                && (string.IsNullOrEmpty(sessionId) || string.Equals(serviceWorker.SessionId, sessionId, StringComparison.Ordinal)))
            {
                _serviceWorkers.TryRemove(targetId, out _);

                _defaultContext?.RemoveServiceWorker(targetId);
                foreach (CRBrowserContext context in _contexts.Values)
                {
                    context.RemoveServiceWorker(targetId);
                }

                serviceWorker.NotifyClosed();
                return;
            }

            if (!string.IsNullOrEmpty(sessionId)
                && _connection.Sessions.TryGetValue(sessionId, out CRSession extra)
                && extra != _connection.RootSession)
            {
                extra.Dispose();
            }
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            _closed = true;
            CloseRemainingPages();
            RaiseDisconnected();
        }

        private async Task AdoptExistingBrowserContextsAsync()
        {
            JsonElement? response;
            try
            {
                response = await _connection.RootSession.SendAsync("Target.getBrowserContexts").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return;
            }

            if (!response.HasValue
                || !response.Value.TryGetProperty("browserContextIds", out JsonElement ids)
                || ids.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement idElement in ids.EnumerateArray())
            {
                string id = idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()
                    : string.Empty;
                if (string.IsNullOrEmpty(id) || _contexts.ContainsKey(id))
                {
                    continue;
                }

                CRBrowserContext created = new CRBrowserContext(this, id);
                if (_contexts.TryAdd(id, created))
                {
                    lock (_contextOrderLock)
                    {
                        _contextOrder.Add(id);
                    }
                }
            }
        }

        private async Task AdoptExistingOopifsAsync()
        {
            for (int i = 0; i < 50 && _crPages.IsEmpty; i++)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }

            foreach (CRPage page in _crPages.Values)
            {
                try
                {
                    await page.InitializedTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
            }

            FlushPendingOopifs();

            JsonElement? response = await _connection.RootSession.SendAsync("Target.getTargets").ConfigureAwait(false);
            if (!response.HasValue
                || !response.Value.TryGetProperty("targetInfos", out JsonElement infos)
                || infos.ValueKind != JsonValueKind.Array)
            {
                RestoreOopifFramesOnPages();
                return;
            }

            foreach (JsonElement info in infos.EnumerateArray())
            {
                string type = info.TryGetProperty("type", out JsonElement typeEl)
                    ? typeEl.GetString()
                    : string.Empty;
                if (!string.Equals(type, "iframe", StringComparison.Ordinal)
                    && !string.Equals(type, "guest", StringComparison.Ordinal))
                {
                    continue;
                }

                string targetId = info.TryGetProperty("targetId", out JsonElement idEl)
                    ? idEl.GetString()
                    : string.Empty;
                string parentFrameId = info.TryGetProperty("parentFrameId", out JsonElement parentEl)
                    ? parentEl.GetString()
                    : string.Empty;
                if (string.IsNullOrEmpty(targetId))
                {
                    continue;
                }

                CRPage owner = FindPageForOopif(targetId, parentFrameId);
                if (owner == null)
                {
                    foreach (CRPage page in _crPages.Values)
                    {
                        owner = page;
                        break;
                    }
                }

                if (owner == null)
                {
                    continue;
                }

                if (owner.TryGetOopifTargetId(targetId, out _))
                {
                    continue;
                }

                try
                {
                    CRSession session = await AttachToTargetAsync(targetId)
                        .WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    owner.AttachOopifSession(session, targetId, parentFrameId);
                }
                catch (TimeoutException)
                {
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            for (int i = 0; i < 40; i++)
            {
                FlushPendingOopifs();
                RestoreOopifFramesOnPages();
                if (await ExistingIframeTargetsHaveFramesAsync().ConfigureAwait(false))
                {
                    break;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            RestoreOopifFramesOnPages();
        }

        private async Task<bool> ExistingIframeTargetsHaveFramesAsync()
        {
            foreach (CRPage page in _crPages.Values)
            {
                page.RestoreOopifFrames();
            }

            JsonElement? response;
            try
            {
                response = await _connection.RootSession.SendAsync("Target.getTargets").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return false;
            }

            if (!response.HasValue
                || !response.Value.TryGetProperty("targetInfos", out JsonElement infos)
                || infos.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            bool sawIframe = false;
            foreach (JsonElement info in infos.EnumerateArray())
            {
                string type = info.TryGetProperty("type", out JsonElement typeEl)
                    ? typeEl.GetString()
                    : string.Empty;
                if (!string.Equals(type, "iframe", StringComparison.Ordinal)
                    && !string.Equals(type, "guest", StringComparison.Ordinal))
                {
                    continue;
                }

                string targetId = info.TryGetProperty("targetId", out JsonElement idEl)
                    ? idEl.GetString()
                    : string.Empty;
                string parentFrameId = info.TryGetProperty("parentFrameId", out JsonElement parentEl)
                    ? parentEl.GetString()
                    : string.Empty;
                if (string.IsNullOrEmpty(targetId))
                {
                    continue;
                }

                sawIframe = true;
                CRPage owner = FindPageForOopif(targetId, parentFrameId);
                if (owner == null)
                {
                    foreach (CRPage page in _crPages.Values)
                    {
                        owner = page;
                        break;
                    }
                }

                if (owner == null)
                {
                    return false;
                }

                if (owner.MainFrame == null
                    || owner.MainFrame.ChildFrames.Count < 1
                    || !owner.TryGetOopifTargetId(targetId, out _))
                {
                    owner.RestoreOopifFrames();
                }

                if (owner.MainFrame == null
                    || owner.MainFrame.ChildFrames.Count < 1
                    || !owner.TryGetOopifTargetId(targetId, out _))
                {
                    return false;
                }
            }

            return sawIframe;
        }

        private void RestoreOopifFramesOnPages()
        {
            foreach (CRPage page in _crPages.Values)
            {
                page.RestoreOopifFrames();
            }
        }

        private void FlushPendingOopifs()
        {
            List<PendingOopif> pending;
            lock (_pendingOopifs)
            {
                if (_pendingOopifs.Count == 0)
                {
                    return;
                }

                pending = new List<PendingOopif>(_pendingOopifs);
                _pendingOopifs.Clear();
            }

            foreach (PendingOopif item in pending)
            {
                CRPage owner = FindPageForOopif(item.TargetId, item.ParentFrameId);
                if (owner != null)
                {
                    owner.AttachOopifSession(item.Session, item.TargetId, item.ParentFrameId);
                }
                else
                {
                    lock (_pendingOopifs)
                    {
                        _pendingOopifs.Add(item);
                    }
                }
            }
        }

        private void CloseRemainingPages()
        {
            foreach (KeyValuePair<string, CRPage> pair in _crPages.ToArray())
            {
                if (!_crPages.TryRemove(pair.Key, out CRPage page))
                {
                    continue;
                }

                _defaultContext?.RemovePage(page);
                foreach (CRBrowserContext context in _contexts.Values)
                {
                    context.RemovePage(page);
                }

                page.DidClose();
            }
        }

        private void RaiseDisconnected()
        {
            EventHandler handler = Disconnected;
            Disconnected = null;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private sealed class PendingOopif
        {
            internal PendingOopif(CRSession session, string targetId, string parentFrameId)
            {
                Session = session;
                TargetId = targetId;
                ParentFrameId = parentFrameId;
            }

            internal CRSession Session { get; }

            internal string TargetId { get; }

            internal string ParentFrameId { get; }
        }
    }
}
