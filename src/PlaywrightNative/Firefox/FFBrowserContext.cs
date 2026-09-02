/*
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Threading.Tasks;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Represents an isolated browser context in Firefox (analogous to an incognito window).
    /// </summary>
    internal class FFBrowserContext : IAsyncDisposable
    {
        private readonly FFBrowser _browser;
        private readonly string _browserContextId;
        private readonly List<FFPage> _pages = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<FFPage>> _pendingPageCreations = new();
        private readonly ConcurrentDictionary<string, FFPage> _earlyPages = new();
        private readonly List<(string Pattern, Func<FFRoute, Task> Handler)> _routes = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="FFBrowserContext"/> class.
        /// </summary>
        /// <param name="browser">The owning <see cref="FFBrowser"/> instance.</param>
        /// <param name="browserContextId">The Juggler browser context ID.</param>
        public FFBrowserContext(FFBrowser browser, string browserContextId)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _browserContextId = browserContextId;
        }

        /// <summary>
        /// Gets the owning <see cref="FFBrowser"/> instance.
        /// </summary>
        internal FFBrowser Browser => _browser;

        /// <summary>
        /// Gets the Juggler browser context ID.
        /// </summary>
        internal string BrowserContextId => _browserContextId;

        /// <summary>
        /// Gets the list of pages currently open in this context.
        /// </summary>
        internal IReadOnlyList<FFPage> Pages => _pages;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a route handler for every page in this context, current and future.
        /// </summary>
        /// <param name="pattern">A glob-style URL pattern.</param>
        /// <param name="handler">The async handler invoked on matching requests.</param>
        internal async Task RouteAsync(string pattern, Func<FFRoute, Task> handler)
        {
            List<FFPage> existingPages;
            lock (_routes)
            {
                _routes.Add((pattern, handler));
                existingPages = new List<FFPage>(_pages);
            }

            foreach (FFPage page in existingPages)
            {
                page.NetworkManager.AddRoute(pattern, handler);
                await page.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a new page in this browser context using the Juggler <c>Browser.newPage</c> command.
        /// </summary>
        /// <returns>The newly created <see cref="FFPage"/>.</returns>
        internal async Task<FFPage> NewPageAsync()
        {
            var tcs = new TaskCompletionSource<FFPage>(TaskCreationOptions.RunContinuationsAsynchronously);

            JsonElement? response = string.IsNullOrEmpty(_browserContextId)
                ? await _browser.Session.SendAsync("Browser.newPage").ConfigureAwait(false)
                : await _browser.Session.SendAsync("Browser.newPage", new { browserContextId = _browserContextId }).ConfigureAwait(false);

            string targetId = string.Empty;
            if (response.HasValue &&
                response.Value.TryGetProperty("targetId", out JsonElement tidEl))
            {
                targetId = tidEl.GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(targetId))
            {
                throw new PlaywrightNativeException("Browser.newPage did not return a targetId.");
            }

            _pendingPageCreations.TryAdd(targetId, tcs);

            if (_earlyPages.TryRemove(targetId, out FFPage earlyPage))
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
        /// Closes this browser context. Persistent (default-profile) contexts
        /// close the browser. Isolated contexts send <c>Browser.removeBrowserContext</c>.
        /// </summary>
        internal async Task CloseAsync()
        {
            if (string.IsNullOrEmpty(_browserContextId))
            {
                await _browser.CloseAsync().ConfigureAwait(false);
                return;
            }

            await _browser.Session
                .SendAsync("Browser.removeBrowserContext", new { browserContextId = _browserContextId })
                .ConfigureAwait(false);

            _browser.RemoveContext(_browserContextId);
        }

        /// <summary>
        /// Adds a page to this context when <c>Browser.attachedToTarget</c> fires.
        /// </summary>
        /// <param name="targetId">The Juggler target ID.</param>
        /// <param name="page">The <see cref="FFPage"/> to add.</param>
        internal void AddPage(string targetId, FFPage page)
        {
            List<(string Pattern, Func<FFRoute, Task> Handler)> routesSnapshot;
            lock (_routes)
            {
                _pages.Add(page);
                routesSnapshot = _routes.Count > 0
                    ? new List<(string, Func<FFRoute, Task>)>(_routes)
                    : null;
            }

            if (routesSnapshot != null)
            {
                foreach ((string pattern, Func<FFRoute, Task> handler) in routesSnapshot)
                {
                    page.NetworkManager.AddRoute(pattern, handler);
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await page.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
                    }
                    catch (TargetClosedException)
                    {
                    }
                    catch (Exception ex)
                    {
                        await System.Console.Error.WriteLineAsync(
                            $"[FFBrowserContext] UpdateInterception after AddPage failed: {ex.Message}").ConfigureAwait(false);
                    }
                });
            }

            if (_pendingPageCreations.TryRemove(targetId, out TaskCompletionSource<FFPage> tcs))
            {
                tcs.TrySetResult(page);
            }
            else
            {
                _earlyPages.TryAdd(targetId, page);
            }
        }

        /// <summary>
        /// Removes a page from this context when it is closed or detached.
        /// </summary>
        /// <param name="page">The <see cref="FFPage"/> to remove.</param>
        internal void RemovePage(FFPage page)
        {
            lock (_routes)
            {
                _pages.Remove(page);
            }
        }
    }
}
