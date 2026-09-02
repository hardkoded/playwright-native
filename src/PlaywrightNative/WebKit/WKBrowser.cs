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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;
using PlaywrightNative.Transport;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Represents a connected WebKit browser instance. Owns a <see cref="WKConnection"/>,
    /// tracks page proxies (one per WKPage), and routes inbound page-proxy messages to
    /// the matching <see cref="WKPage"/>. Implements <see cref="IBrowser"/> directly.
    /// </summary>
    /// <remarks>
    /// All WebKit protocol commands live under the <c>Playwright.*</c> namespace
    /// (not <c>Browser.*</c>). The graceful-close command is <c>Playwright.close</c>
    /// sent with the <see cref="WKConnection.BrowserCloseMessageId"/> sentinel id so
    /// the response is discarded.
    /// </remarks>
    internal sealed partial class WKBrowser : IBrowser, IHasPlaywrightLogger, IHasLaunchProxy, IHasTracesDir, IHasArtifactsDir
    {
        private readonly WKConnection _connection;
        private readonly BrowserProcessManager _processManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, WKBrowserContext> _contexts = new();
        private readonly List<string> _contextOrder = new();
        private readonly object _contextOrderLock = new();
        private readonly ConcurrentDictionary<string, WKPage> _pages = new();
        private readonly ConcurrentDictionary<string, WKPage> _downloads = new(StringComparer.Ordinal);
        private WKBrowserContext _defaultContext;
        private bool _closed;

        private WKBrowser(
            WKConnection connection,
            BrowserProcessManager processManager,
            ILoggerFactory loggerFactory)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _processManager = processManager;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<WKBrowser>();

            _connection.Disconnected += OnDisconnected;
            _connection.BrowserSession.MessageReceived += OnBrowserSessionMessage;
            _connection.PageProxyMessageReceived += OnPageProxyMessageReceived;
        }

        /// <inheritdoc/>
        public event EventHandler<IBrowser> Disconnected;

        /// <inheritdoc/>
        public event EventHandler<IBrowserContext> Context;

        /// <inheritdoc/>
        public bool IsConnected => !_closed && !_connection.IsClosed;

        /// <inheritdoc/>
        public string Version => "26.4";

        /// <inheritdoc/>
        public IBrowserType BrowserType => BrowserTypeInfo.Webkit;

        /// <inheritdoc/>
        public IPlaywrightLogger Logger { get; set; }

        /// <inheritdoc/>
        Proxy IHasLaunchProxy.LaunchProxy => LaunchProxy;

        /// <inheritdoc/>
        string IHasTracesDir.TracesDir { get; set; }

        /// <inheritdoc/>
        string IHasArtifactsDir.ArtifactsDir { get; set; }

        /// <inheritdoc/>
        public IReadOnlyList<IBrowserContext> Contexts
        {
            get
            {
                List<IBrowserContext> contexts = new List<IBrowserContext>();
                if (_defaultContext != null)
                {
                    contexts.Add(_defaultContext);
                }

                lock (_contextOrderLock)
                {
                    foreach (string id in _contextOrder)
                    {
                        if (_contexts.TryGetValue(id, out WKBrowserContext context))
                        {
                            contexts.Add(context);
                        }
                    }
                }

                return contexts;
            }
        }

        /// <summary>
        /// Launch-level downloads directory. When set, new contexts save accepted
        /// downloads here instead of a temporary folder.
        /// </summary>
        internal string LaunchDownloadsPath { get; set; }

        /// <summary>
        /// Launch-level <c>proxy</c> from <c>browserType.launch</c>.
        /// </summary>
        internal Proxy LaunchProxy { get; set; }

        /// <summary>
        /// Gets the underlying WKConnection.
        /// </summary>
        internal WKConnection Connection => _connection;

        /// <summary>
        /// Gets the browser-level session.
        /// </summary>
        internal WKSession Session => _connection.BrowserSession;

        /// <summary>
        /// Gets a snapshot of currently open WebKit contexts.
        /// </summary>
        internal IReadOnlyCollection<WKBrowserContext> WKContexts => _contexts.Values.ToArray();

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            _connection.Disconnected -= OnDisconnected;
            _connection.Dispose();

            if (_processManager != null)
            {
                await _processManager.KillAsync().ConfigureAwait(false);
                _processManager.Dispose();
            }
        }

        /// <inheritdoc/>
        public async Task<IBrowserContext> NewContextAsync(BrowserContextOptions options)
        {
            IBrowserContext created = options == null
                ? await NewContextAsync().ConfigureAwait(false)
                : await NewContextAsync(
                extraHTTPHeaders: options.ExtraHTTPHeaders,
                userAgent: options.UserAgent,
                viewportSize: options.Viewport,
                locale: options.Locale,
                timezoneId: options.TimezoneId,
                offline: options.Offline,
                colorScheme: options.ColorScheme,
                hasTouch: options.HasTouch,
                bypassCSP: options.BypassCSP,
                geolocation: options.Geolocation,
                permissions: options.Permissions,
                ignoreHTTPSErrors: options.IgnoreHTTPSErrors,
                javaScriptEnabled: options.JavaScriptEnabled,
                deviceScaleFactor: options.DeviceScaleFactor,
                isMobile: options.IsMobile,
                httpCredentials: options.HttpCredentials,
                screenSize: options.ScreenSize,
                acceptDownloads: options.AcceptDownloads,
                storageState: options.StorageState,
                storageStatePath: options.StorageStatePath,
                proxy: options.Proxy,
                recordHarPath: options.RecordHarPath,
                recordHarOmitContent: options.RecordHarOmitContent,
                recordHarUrl: options.RecordHarUrl,
                baseURL: options.BaseURL,
                recordHarMode: options.RecordHarMode,
                serviceWorkers: options.ServiceWorkers,
                reducedMotion: options.ReducedMotion,
                forcedColors: options.ForcedColors,
                contrast: options.Contrast,
                recordHarContent: options.RecordHarContent,
                recordHarUrlRegex: options.RecordHarUrlRegex,
                recordVideoDir: options.RecordVideoDir,
                recordVideoSize: options.RecordVideoSize,
                strictSelectors: options.StrictSelectors,
                clientCertificates: options.ClientCertificates).ConfigureAwait(false);

            if (created is IHasPlaywrightLogger has)
            {
                has.Logger = options?.Logger ?? Logger;
            }

            return created;
        }

        /// <inheritdoc/>
        public async Task<IBrowserContext> NewContextAsync(
            bool? acceptDownloads = default,
            bool? bypassCSP = default,
            ColorScheme colorScheme = default,
            float? deviceScaleFactor = default,
            IEnumerable<KeyValuePair<string, string>> extraHTTPHeaders = default,
            Geolocation geolocation = default,
            bool? hasTouch = default,
            HttpCredentials httpCredentials = default,
            bool? ignoreHTTPSErrors = default,
            bool? isMobile = default,
            bool? javaScriptEnabled = default,
            string locale = default,
            bool? offline = default,
            IEnumerable<string> permissions = default,
            Proxy proxy = default,
            bool? recordHarOmitContent = default,
            string recordHarPath = default,
            string recordVideoDir = default,
            RecordVideoSize recordVideoSize = default,
            ScreenSize screenSize = default,
            string storageState = default,
            string storageStatePath = default,
            string timezoneId = default,
            string userAgent = default,
            ViewportSize viewportSize = default,
            string recordHarUrl = default,
            string baseURL = default,
            HarMode recordHarMode = default,
            ServiceWorkerPolicy serviceWorkers = default,
            ReducedMotion reducedMotion = default,
            ForcedColors forcedColors = default,
            Contrast contrast = default,
            HarContentPolicy recordHarContent = default,
            Regex recordHarUrlRegex = default,
            bool? strictSelectors = default,
            IEnumerable<ClientCertificate> clientCertificates = default)
        {
            return await PlaywrightApiLog.RunAsync(Logger, "browser.newContext", async () =>
            {
                // Official browserContext.ts: context._options.proxy || browser.options.proxy
                proxy ??= LaunchProxy;
                BrowserContextOptionGuard.ThrowIfNullViewportConflicts(viewportSize, deviceScaleFactor, isMobile);
                BrowserContextOptionGuard.ThrowIfInvalidProxy(proxy);
                extraHTTPHeaders = ProxySettings.WithProxyAuthorization(proxy, extraHTTPHeaders);
                ClientCertificatesProxy certsProxy = ClientCertificatesProxy.TryStart(
                    clientCertificates,
                    ignoreHTTPSErrors == true,
                    proxy,
                    out Proxy browserProxy);
                LocaleHandshakeProxy handshake = certsProxy == null
                    ? LocaleHandshakeProxy.TryStart(locale, browserProxy, force: true, out browserProxy)
                    : null;
                WKBrowserContext context;
                try
                {
                    context = await NewWKContextAsync(browserProxy).ConfigureAwait(false);
                }
                catch
                {
                    handshake?.Dispose();
                    certsProxy?.Dispose();
                    throw;
                }

                context.AttachLocaleHandshake(handshake);
                context.AttachClientCertificatesProxy(certsProxy, proxy);
                context.AttachClientCertificates(clientCertificates);
                context.BaseURL = baseURL;
                context.StrictSelectors = strictSelectors == true;
                context.ConfigureEmulation(
                    viewportSize,
                    userAgent,
                    extraHTTPHeaders,
                    locale,
                    timezoneId,
                    offline,
                    colorScheme,
                    hasTouch,
                    bypassCSP,
                    geolocation,
                    permissions,
                    ignoreHTTPSErrors,
                    javaScriptEnabled,
                    deviceScaleFactor,
                    isMobile,
                    httpCredentials,
                    screenSize,
                    acceptDownloads,
                    reducedMotion,
                    forcedColors,
                    contrast);
                await context.ApplyDownloadBehaviorAsync().ConfigureAwait(false);
                await context.ApplyLanguagesAsync().ConfigureAwait(false);
                await context.ApplyWebKitPageShimsAsync().ConfigureAwait(false);
                await context.ApplyEphemeralStorageShimsAsync().ConfigureAwait(false);
                await StorageStateHelper.ApplyAsync(context, storageState, storageStatePath).ConfigureAwait(false);
                HarRecorder.Start(context, recordHarPath, recordHarOmitContent, recordHarUrl, recordHarMode, recordHarContent, recordHarUrlRegex);
                VideoRecorder.Start(context, recordVideoDir, recordVideoSize, viewportSize);
                await ServiceWorkerPolicyHelper.ApplyAsync(context, serviceWorkers).ConfigureAwait(false);
                Context?.Invoke(this, context);
                context.Logger = Logger;
                return context;
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IPage> NewPageAsync(
            bool? acceptDownloads = default,
            bool? bypassCSP = default,
            ColorScheme colorScheme = default,
            float? deviceScaleFactor = default,
            IEnumerable<KeyValuePair<string, string>> extraHTTPHeaders = default,
            Geolocation geolocation = default,
            bool? hasTouch = default,
            HttpCredentials httpCredentials = default,
            bool? ignoreHTTPSErrors = default,
            bool? isMobile = default,
            bool? javaScriptEnabled = default,
            string locale = default,
            bool? offline = default,
            IEnumerable<string> permissions = default,
            Proxy proxy = default,
            bool? recordHarOmitContent = default,
            string recordHarPath = default,
            string recordVideoDir = default,
            RecordVideoSize recordVideoSize = default,
            ScreenSize screenSize = default,
            string storageState = default,
            string storageStatePath = default,
            string timezoneId = default,
            string userAgent = default,
            ViewportSize viewportSize = default,
            string recordHarUrl = default,
            string baseURL = default,
            HarMode recordHarMode = default,
            ServiceWorkerPolicy serviceWorkers = default,
            ReducedMotion reducedMotion = default,
            ForcedColors forcedColors = default,
            Contrast contrast = default,
            HarContentPolicy recordHarContent = default,
            Regex recordHarUrlRegex = default,
            bool? strictSelectors = default,
            IEnumerable<ClientCertificate> clientCertificates = default)
        {
            IBrowserContext context = await NewContextAsync(
                extraHTTPHeaders: extraHTTPHeaders,
                userAgent: userAgent,
                viewportSize: viewportSize,
                locale: locale,
                timezoneId: timezoneId,
                offline: offline,
                colorScheme: colorScheme,
                hasTouch: hasTouch,
                bypassCSP: bypassCSP,
                geolocation: geolocation,
                permissions: permissions,
                ignoreHTTPSErrors: ignoreHTTPSErrors,
                javaScriptEnabled: javaScriptEnabled,
                deviceScaleFactor: deviceScaleFactor,
                isMobile: isMobile,
                httpCredentials: httpCredentials,
                screenSize: screenSize,
                acceptDownloads: acceptDownloads,
                storageState: storageState,
                storageStatePath: storageStatePath,
                proxy: proxy,
                recordHarPath: recordHarPath,
                recordHarOmitContent: recordHarOmitContent,
                recordHarUrl: recordHarUrl,
                baseURL: baseURL,
                recordHarMode: recordHarMode,
                serviceWorkers: serviceWorkers,
                reducedMotion: reducedMotion,
                forcedColors: forcedColors,
                contrast: contrast,
                recordHarContent: recordHarContent,
                recordHarUrlRegex: recordHarUrlRegex,
                strictSelectors: strictSelectors,
                clientCertificates: clientCertificates).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            if (page is WKPage wkPage)
            {
                wkPage.OwnedContext = context;
            }

            if (context is WKBrowserContext owned)
            {
                owned.OwnedByBrowserNewPage = true;
            }

            return page;
        }

        /// <summary>
        /// Gracefully closes the browser by sending <c>Playwright.close</c> with the
        /// <see cref="WKConnection.BrowserCloseMessageId"/> sentinel and waiting for the
        /// process to exit (with a kill fallback).
        /// </summary>
        /// <param name="reason">The reason to be reported to operations interrupted by this close.</param>
        public async Task CloseAsync(string reason = default)
        {
            if (_closed)
            {
                return;
            }

            _defaultContext?.RecordCloseReason(reason);
            _defaultContext?.CleanupDownloadsOnBrowserClose();
            if (_defaultContext != null)
            {
                try
                {
                    await VideoRecorder.FlushAsync(_defaultContext).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Video flush during browser close failed");
                }
            }

            _defaultContext?.NotifyClosedFromBrowser();
            foreach (WKBrowserContext context in _contexts.Values)
            {
                context.RecordCloseReason(reason);
                context.CleanupDownloadsOnBrowserClose();
                try
                {
                    await VideoRecorder.FlushAsync(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Video flush during browser close failed");
                }

                context.NotifyClosedFromBrowser();
            }

            _closed = true;

            try
            {
                // Sentinel id — the response will be discarded by WKConnection.
                await _connection.BrowserSession
                    .SendAsync("Playwright.close", parameters: null, messageId: WKConnection.BrowserCloseMessageId)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Playwright.close failed, browser may have already disconnected");
            }

            if (_processManager != null)
            {
                await _processManager.EnsureExitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }

            RaiseDisconnected();
        }

        /// <inheritdoc/>
        public Task<ICDPSession> NewBrowserCDPSessionAsync()
            => throw new PlaywrightNativeException("CDP sessions are only supported in Chromium.");

        /// <inheritdoc/>
        public Task StartTracingAsync(IPage page = default, string path = default, bool screenshots = default, IEnumerable<string> categories = default)
            => throw new PlaywrightNativeException("startTracing is only supported in Chromium.");

        /// <inheritdoc/>
        public Task<byte[]> StopTracingAsync()
            => throw new PlaywrightNativeException("stopTracing is only supported in Chromium.");

        /// <summary>
        /// Connects to a running WebKit browser process: enables Playwright protocol events
        /// and returns a fully ready <see cref="WKBrowser"/>.
        /// </summary>
        /// <param name="connection">The WKConnection to use.</param>
        /// <param name="processManager">Optional process manager.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        /// <param name="persistent">When <see langword="true"/>, create the default context.</param>
        /// <returns>A connected <see cref="WKBrowser"/>.</returns>
        internal static async Task<WKBrowser> ConnectAsync(
            WKConnection connection,
            BrowserProcessManager processManager = null,
            ILoggerFactory loggerFactory = null,
            bool persistent = false)
        {
            WKBrowser browser = new(connection, processManager, loggerFactory);

            // Enable Playwright protocol events.
            await connection.BrowserSession.SendAsync("Playwright.enable").ConfigureAwait(false);

            if (persistent)
            {
                browser._defaultContext = new WKBrowserContext(browser, browserContextId: null);
            }

            return browser;
        }

        /// <summary>
        /// Persistent context created by <c>LaunchPersistentContextAsync</c>.
        /// </summary>
        /// <returns>The default context.</returns>
        internal IBrowserContext PersistentContext()
        {
            if (_defaultContext == null)
            {
                throw new PlaywrightNativeException("Browser was not launched as a persistent context.");
            }

            _defaultContext.UseLaunchDownloadsPath(LaunchDownloadsPath);
            return _defaultContext;
        }

        /// <summary>
        /// Creates a new isolated browser context. This is the way to get
        /// a usable context — there is no implicit default context.
        /// </summary>
        /// <returns>The new <see cref="WKBrowserContext"/>.</returns>
        internal async Task<WKBrowserContext> NewWKContextAsync(Proxy proxy = null)
        {
            JsonElement? response;
            string proxyServer = ProxySettings.FormatServer(proxy, includeCredentials: true);
            string proxyBypassList = ProxySettings.NormalizeBypass(proxy?.Bypass);
            if (!string.IsNullOrEmpty(proxyServer) && !string.IsNullOrEmpty(proxyBypassList))
            {
                response = await _connection.BrowserSession
                    .SendAsync("Playwright.createContext", new
                    {
                        proxyServer,
                        proxyBypassList,
                    }).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(proxyServer))
            {
                response = await _connection.BrowserSession
                    .SendAsync("Playwright.createContext", new
                    {
                        proxyServer,
                    }).ConfigureAwait(false);
            }
            else
            {
                response = await _connection.BrowserSession
                    .SendAsync("Playwright.createContext").ConfigureAwait(false);
            }

            string browserContextId = string.Empty;
            if (response.HasValue && response.Value.TryGetProperty("browserContextId", out JsonElement idEl))
            {
                browserContextId = idEl.GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(browserContextId))
            {
                throw new PlaywrightNativeException("Playwright.createContext did not return a browserContextId.");
            }

            WKBrowserContext context = new(this, browserContextId);
            context.Proxy = proxy;
            context.UseLaunchDownloadsPath(LaunchDownloadsPath);
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
        /// Removes a context from tracking. Called by <see cref="WKBrowserContext.CloseAsync"/>.
        /// </summary>
        /// <param name="browserContextId">The context ID to forget.</param>
        internal void RemoveContext(string browserContextId)
        {
            _contexts.TryRemove(browserContextId, out _);
            lock (_contextOrderLock)
            {
                _contextOrder.Remove(browserContextId);
            }
        }

        private void OnBrowserSessionMessage(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Playwright.pageProxyCreated":
                    OnPageProxyCreated(parameters);
                    break;
                case "Playwright.pageProxyDestroyed":
                    OnPageProxyDestroyed(parameters);
                    break;
                case "Playwright.downloadCreated":
                    OnDownloadCreated(parameters);
                    break;
                case "Playwright.downloadFilenameSuggested":
                    OnDownloadFilenameSuggested(parameters);
                    break;
                case "Playwright.downloadFinished":
                    OnDownloadFinished(parameters);
                    break;
                case "Playwright.provisionalLoadFailed":
                    OnProvisionalLoadFailed(parameters);
                    break;
                case "Playwright.windowOpen":
                    OnWindowOpen(parameters);
                    break;
            }
        }

        private void OnProvisionalLoadFailed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string pageProxyId = p.TryGetProperty("pageProxyId", out JsonElement idEl)
                ? idEl.GetString() ?? string.Empty
                : string.Empty;
            string errorText = p.TryGetProperty("error", out JsonElement errorEl)
                ? errorEl.GetString()
                : string.Empty;
            if (!string.IsNullOrEmpty(pageProxyId) && _pages.TryGetValue(pageProxyId, out WKPage page))
            {
                page.HandleProvisionalLoadFailed(errorText);
            }
        }

        private void OnDownloadCreated(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string pageProxyId = p.TryGetProperty("pageProxyId", out JsonElement pageEl) ? pageEl.GetString() : string.Empty;
            string uuid = p.TryGetProperty("uuid", out JsonElement uuidEl) ? uuidEl.GetString() : string.Empty;
            string url = p.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : string.Empty;
            if (!_pages.TryGetValue(pageProxyId ?? string.Empty, out WKPage page))
            {
                foreach (WKPage candidate in _pages.Values)
                {
                    page = candidate;
                    break;
                }
            }

            if (page == null || string.IsNullOrEmpty(uuid))
            {
                return;
            }

            if (page.Opener != null && !page.HasCommittedNonInitialNavigation)
            {
                page = page.Opener;
            }

            _downloads[uuid] = page;
            page.OnDownloadCreated(uuid, url);
        }

        private void OnDownloadFilenameSuggested(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string uuid = p.TryGetProperty("uuid", out JsonElement uuidEl) ? uuidEl.GetString() : string.Empty;
            string suggested = p.TryGetProperty("suggestedFilename", out JsonElement nameEl) ? nameEl.GetString() : string.Empty;
            if (!string.IsNullOrEmpty(uuid) && _downloads.TryGetValue(uuid, out WKPage page))
            {
                page.OnDownloadFilenameSuggested(uuid, suggested);
            }
        }

        private void OnDownloadFinished(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string uuid = p.TryGetProperty("uuid", out JsonElement uuidEl) ? uuidEl.GetString() : string.Empty;
            string error = p.TryGetProperty("error", out JsonElement errorEl) ? errorEl.GetString() : null;
            if (!string.IsNullOrEmpty(uuid) && _downloads.TryRemove(uuid, out WKPage page))
            {
                page.OnDownloadFinished(uuid, error);
            }
        }

        private void OnPageProxyCreated(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string pageProxyId = payload.TryGetProperty("pageProxyId", out JsonElement idEl)
                ? idEl.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrEmpty(pageProxyId))
            {
                return;
            }

            string browserContextId = payload.TryGetProperty("browserContextId", out JsonElement ctxEl)
                ? ctxEl.GetString() ?? string.Empty
                : string.Empty;

            string openerId = payload.TryGetProperty("openerId", out JsonElement openerEl)
                ? openerEl.GetString() ?? string.Empty
                : string.Empty;
            WKPage openerPage = null;
            if (!string.IsNullOrEmpty(openerId))
            {
                _pages.TryGetValue(openerId, out openerPage);
            }

            WKBrowserContext context = openerPage?.WKContext;
            if (context == null && !string.IsNullOrEmpty(browserContextId))
            {
                _contexts.TryGetValue(browserContextId, out context);
            }

            if (context == null)
            {
                context = _defaultContext;
            }

            // Per-page session — sessionId is unused on the wire; the routing key is pageProxyId,
            // which the session stamps on every outbound message.
            WKSession pageSession = new(_connection, sessionId: string.Empty, pageProxyId: pageProxyId);

            WKPage page = new(pageSession, pageProxyId, this, context, _loggerFactory);
            _pages.TryAdd(pageProxyId, page);

            context?.AddPage(pageProxyId, page);

            if (openerPage != null)
            {
                page.Opener = openerPage;
                page.WindowOpenViewport = openerPage.TakeNextWindowOpenViewport();
                if (context != null)
                {
                    page.ContextChromeTask = ApplyContextChromeWhenReadyAsync(context, page);
                }

                openerPage.FirePopupOpened(page);
            }
            else if (context == null || !context.CreatePageIsInFlight())
            {
                // Official only sets opener from protocol openerId. Infer a
                // sibling solely for noopener popups that omit it — never for
                // Playwright.createPage (context.newPage), or the new page
                // takes the popup-navigation path and can close on first goto.
                WKPage inferred = PopupOpenedHelper.InferListenerOrSoleSibling(
                    context?.WKPages,
                    page,
                    sibling => sibling.HasPopupListeners);
                if (inferred != null)
                {
                    // Official opener() is only the protocol openerId. Infer the
                    // sibling for chrome / popup events, not page.opener().
                    if (context != null)
                    {
                        page.WindowOpenViewport = inferred.TakeNextWindowOpenViewport();
                        page.ContextChromeTask = ApplyContextChromeWhenReadyAsync(context, page);
                    }

                    if (inferred.HasPopupListeners)
                    {
                        inferred.FirePopupOpened(page);
                    }
                }
            }
        }

        private async Task ApplyContextChromeWhenReadyAsync(WKBrowserContext context, WKPage page)
        {
            try
            {
                await page.InitializedTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            if (page.Opener == null)
            {
                await context.ApplyInitScriptsAsync(page).ConfigureAwait(false);
            }
        }

        private void OnWindowOpen(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string pageProxyId = parameters.Value.TryGetProperty("pageProxyId", out JsonElement idEl)
                ? idEl.GetString() ?? string.Empty
                : string.Empty;
            if (!string.IsNullOrEmpty(pageProxyId) && _pages.TryGetValue(pageProxyId, out WKPage page))
            {
                page.HandleWindowOpen(parameters);
            }
        }

        private void OnPageProxyDestroyed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string pageProxyId = parameters.Value.TryGetProperty("pageProxyId", out JsonElement idEl)
                ? idEl.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrEmpty(pageProxyId))
            {
                return;
            }

            if (_pages.TryRemove(pageProxyId, out WKPage page))
            {
                page.WKContext?.RemovePage(page);
                page.DidClose();
            }
        }

        private void OnPageProxyMessageReceived(string pageProxyId, ProtocolResponse message)
        {
            if (_pages.TryGetValue(pageProxyId, out WKPage page))
            {
                page.Session.OnMessage(message);
            }
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            _closed = true;

            foreach (WKPage page in _pages.Values)
            {
                page.WKContext?.RemovePage(page);
                page.DidClose();
            }

            _pages.Clear();
            RaiseDisconnected();
        }

        private void RaiseDisconnected()
        {
            EventHandler<IBrowser> handler = Disconnected;
            Disconnected = null;
            handler?.Invoke(this, this);
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<BrowserBindResult> IBrowser.BindAsync(string title, BrowserBindOptions options) => Task.FromResult<BrowserBindResult>(default!);

        Task IBrowser.CloseAsync(BrowserCloseOptions options) => CloseAsync();

        Task<IBrowserContext> IBrowser.NewContextAsync(BrowserNewContextOptions options)
            => NewContextAsync(MicrosoftOptionsBridge.ToBrowserContextOptions(options));

        Task<IPage> IBrowser.NewPageAsync(BrowserNewPageOptions options)
        {
            BrowserContextOptions sharpOptions = MicrosoftOptionsBridge.ToBrowserContextOptions(options);
            if (sharpOptions == null)
            {
                return NewPageAsync();
            }

            return BrowserCompatExtensions.NewPageAsync(this, sharpOptions);
        }

        Task IBrowser.UnbindAsync() => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
