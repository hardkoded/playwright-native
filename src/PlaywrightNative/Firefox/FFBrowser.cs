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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Transport;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Represents a connected Firefox browser instance communicating via the Juggler protocol.
    /// </summary>
    internal class FFBrowser : IAsyncDisposable
    {
        private readonly FFConnection _connection;
        private readonly IConnectionTransport _transport;
        private readonly BrowserProcessManager _processManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, FFBrowserContext> _contexts = new();
        private readonly ConcurrentDictionary<string, FFPage> _ffPages = new();
        private FFBrowserContext _defaultContext;
        private bool _closed;

        private FFBrowser(
            FFConnection connection,
            IConnectionTransport transport,
            string version,
            BrowserProcessManager processManager,
            ILoggerFactory loggerFactory)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Version = version;
            _processManager = processManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<FFBrowser>();

            _connection.Disconnected += OnDisconnected;
            _connection.RootSession.MessageReceived += OnRootSessionMessage;
        }

        /// <summary>
        /// Gets the browser version string.
        /// </summary>
        internal string Version { get; }

        /// <summary>
        /// Gets a value indicating whether the browser is still connected.
        /// </summary>
        internal bool IsConnected => !_connection.IsClosed;

        /// <summary>
        /// Gets the underlying Juggler connection.
        /// </summary>
        internal FFConnection Connection => _connection;

        /// <summary>
        /// Gets the root Juggler session (browser-level commands).
        /// </summary>
        internal FFSession Session => _connection.RootSession;

        /// <summary>
        /// Gets the contexts currently open in this browser.
        /// </summary>
        internal IReadOnlyCollection<FFBrowserContext> Contexts => _contexts.Values.ToArray();

        /// <summary>
        /// Default profile context created by <c>LaunchPersistentContextAsync</c>.
        /// </summary>
        internal FFBrowserContext DefaultContext => _defaultContext;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Connects to a running Firefox browser, initializes the Juggler protocol,
        /// and returns a fully ready <see cref="FFBrowser"/> instance.
        /// </summary>
        /// <param name="connection">The Juggler connection to use.</param>
        /// <param name="transport">The underlying transport.</param>
        /// <param name="processManager">Optional process manager for the browser process.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        /// <param name="persistent">When <see langword="true"/>, attach the default profile context.</param>
        /// <returns>A connected <see cref="FFBrowser"/> instance.</returns>
        internal static async Task<FFBrowser> ConnectAsync(
            FFConnection connection,
            IConnectionTransport transport,
            BrowserProcessManager processManager = null,
            ILoggerFactory loggerFactory = null,
            bool persistent = false)
        {
            // Enable browser-level protocol events. Official Playwright sends
            // attachToDefaultContext: false for a non-persistent launch so
            // Juggler does not attach the default profile context.
            await connection.RootSession
                .SendAsync("Browser.enable", new { attachToDefaultContext = persistent })
                .ConfigureAwait(false);

            // Get browser version info.
            JsonElement? infoResponse = await connection.RootSession
                .SendAsync("Browser.getInfo").ConfigureAwait(false);

            string version = string.Empty;
            if (infoResponse.HasValue)
            {
                JsonElement info = infoResponse.Value;
                if (info.TryGetProperty("userAgent", out JsonElement ua))
                {
                    version = ua.GetString() ?? string.Empty;
                }
            }

            FFBrowser browser = new(connection, transport, version, processManager, loggerFactory);
            if (persistent)
            {
                browser._defaultContext = new FFBrowserContext(browser, browserContextId: null);
            }

            return browser;
        }

        /// <summary>
        /// Creates a new isolated browser context.
        /// </summary>
        /// <returns>The newly created <see cref="FFBrowserContext"/>.</returns>
        internal async Task<FFBrowserContext> NewContextAsync()
        {
            JsonElement? response = await _connection.RootSession
                .SendAsync("Browser.createBrowserContext").ConfigureAwait(false);

            string browserContextId = string.Empty;
            if (response.HasValue &&
                response.Value.TryGetProperty("browserContextId", out JsonElement idElement))
            {
                browserContextId = idElement.GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(browserContextId))
            {
                throw new PlaywrightNativeException("Browser.createBrowserContext did not return a browserContextId.");
            }

            FFBrowserContext context = new(this, browserContextId);
            _contexts.TryAdd(browserContextId, context);

            return context;
        }

        /// <summary>
        /// Gracefully closes the browser.
        /// </summary>
        internal async Task CloseAsync()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;

            try
            {
                await FirefoxBrowserType.AttemptToGracefullyCloseBrowserAsync(_transport).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Browser.close failed, browser may have already disconnected");
            }

            if (_processManager != null)
            {
                await _processManager.EnsureExitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }

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
        }

        /// <summary>
        /// Removes a context from tracking. Called by <see cref="FFBrowserContext.CloseAsync"/>.
        /// </summary>
        /// <param name="browserContextId">The context ID to remove.</param>
        internal void RemoveContext(string browserContextId)
        {
            _contexts.TryRemove(browserContextId, out _);
        }

        private void OnRootSessionMessage(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Browser.attachedToTarget":
                    OnAttachedToTarget(parameters);
                    break;
                case "Browser.detachedFromTarget":
                    OnDetachedFromTarget(parameters);
                    break;
            }
        }

        private void OnAttachedToTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;

            // Official Juggler: { sessionId, targetInfo: { targetId, type, browserContextId, openerId } }
            string sessionId = payload.TryGetProperty("sessionId", out JsonElement sidEl)
                ? sidEl.GetString() : string.Empty;

            JsonElement targetInfo = payload;
            if (payload.TryGetProperty("targetInfo", out JsonElement nestedInfo))
            {
                targetInfo = nestedInfo;
            }

            string targetId = targetInfo.TryGetProperty("targetId", out JsonElement tidEl)
                ? tidEl.GetString() : string.Empty;

            string browserContextId = targetInfo.TryGetProperty("browserContextId", out JsonElement ctxEl)
                ? ctxEl.GetString() : string.Empty;

            string type = targetInfo.TryGetProperty("type", out JsonElement typeEl)
                ? typeEl.GetString() : string.Empty;

            if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(sessionId) || type != "page")
            {
                return;
            }

            // Create a child session for this page.
            FFSession pageSession = _connection.RootSession.CreateChildSession(sessionId);

            // Look up the context. Official Juggler omits browserContextId for
            // the default profile used by LaunchPersistentContext.
            FFBrowserContext context = null;
            if (!string.IsNullOrEmpty(browserContextId))
            {
                _contexts.TryGetValue(browserContextId, out context);
            }
            else
            {
                context = _defaultContext;
            }

            FFPage ffPage = new(pageSession, targetId, this, _loggerFactory);
            _ffPages.TryAdd(targetId, ffPage);

            context?.AddPage(targetId, ffPage);

            // Fire popup event if this page was opened by another page.
            string openerId = targetInfo.TryGetProperty("openerId", out JsonElement openerEl)
                ? openerEl.GetString() : string.Empty;

            if (!string.IsNullOrEmpty(openerId) && _ffPages.TryGetValue(openerId, out FFPage openerPage))
            {
                openerPage.FirePopupOpened(ffPage);
            }

            _ = ffPage.InitializeAsync().ContinueWith(
                t => _logger?.LogError(t.Exception, "Failed to initialize FF page {TargetId}", targetId),
                default,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private void OnDetachedFromTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string targetId = payload.TryGetProperty("targetId", out JsonElement tidEl)
                ? tidEl.GetString() : string.Empty;

            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            if (_ffPages.TryRemove(targetId, out FFPage ffPage))
            {
                _defaultContext?.RemovePage(ffPage);
                foreach (FFBrowserContext context in _contexts.Values)
                {
                    context.RemovePage(ffPage);
                }

                ffPage.DidClose();
            }
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            _closed = true;
        }
    }
}
