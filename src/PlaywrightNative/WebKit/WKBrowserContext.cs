/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Represents an isolated browser context in WebKit (analogous to an incognito window).
    /// Implements <see cref="IBrowserContext"/> directly.
    /// </summary>
    internal sealed partial class WKBrowserContext : IBrowserContext, IHasStrictSelectors, IHasDefaultTimeouts, IHasBrowserContextExtras, IHasExtraHttpHeaders, IHasIgnoreHttpsErrors, IHasBaseUrl, IHasClientCertificates, IDialogHost, IHasExposedFunctionNames, IHasHttpCredentials, IHasUserAgent, IHasCloseReason, IHasProxy, IHasStorageStateInternals, IHasTouch, IHasPlaywrightLogger, IHasOfficialTrace
    {
        private readonly WKBrowser _browser;
        private readonly string _browserContextId;
        private readonly List<WKPage> _pages = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<WKPage>> _pendingPageCreations = new();
        private readonly ConcurrentDictionary<string, WKPage> _earlyPages = new();

        // Serializes the page-creation handoff between NewPageAsync (consumer) and AddPage
        // (producer, driven by the Playwright.pageProxyCreated event on the transport thread).
        // Both sides do a check-then-act across the two maps above; without a shared lock the
        // pageProxyCreated event can land between NewPageAsync's _earlyPages check and its
        // _pendingPageCreations registration, filing the page in _earlyPages while NewPageAsync
        // waits forever on a TCS nobody completes (intermittent 30s page-creation hang).
        private readonly object _pageHandoffLock = new();
        private readonly ContextInitScriptSet _initScripts = new();
        private readonly ContextExposedRegistry _exposed = new();
        private readonly List<WKRouteEntry> _routes = new();
        private readonly HashSet<string> _visitedOrigins = new(StringComparer.Ordinal);
        private readonly GrantedPermissionSet _grantedPermissions = new GrantedPermissionSet();
        private int _createPageInFlight;
        private float _defaultTimeout = 30_000;
        private float _defaultNavigationTimeout = 30_000;
        private Dictionary<string, string> _extraHttpHeaders;
        private ViewportSize _viewport;
        private string _userAgent;
        private string _locale;
        private string _timezoneId;
        private bool _offline;
        private ColorScheme _colorScheme;
        private ReducedMotion _reducedMotion;
        private ForcedColors _forcedColors;
        private Contrast _contrast;
        private bool _hasTouch;
        private bool _bypassCsp;
        private Geolocation _geolocation;
        private bool _ignoreHttpsErrors;
        private bool _webkitClipboardShimInstalled;
        private bool _javaScriptDisabled;
        private float? _deviceScaleFactor;
        private bool _isMobile;
        private IReadOnlyList<HttpCredentials> _httpCredentials = Array.Empty<HttpCredentials>();
        private ScreenSize _screenSize;
        private bool _acceptDownloads = true;
        private string _downloadsPath;
        private bool _ownsDownloadsPath = true;

        private bool _closed;
        private bool _creatingStorageStatePage;
        private string _closeReason;
        private LocaleHandshakeProxy _localeHandshake;
        private ClientCertificatesProxy _clientCertificatesProxy;
        private IReadOnlyList<ClientCertificate> _clientCertificates;
        private Proxy _proxy;
        private Proxy _apiRequestProxy;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKBrowserContext"/> class.
        /// </summary>
        /// <param name="browser">The owning <see cref="WKBrowser"/>.</param>
        /// <param name="browserContextId">The WebKit browserContextId from <c>Playwright.createContext</c>.</param>
        public WKBrowserContext(WKBrowser browser, string browserContextId)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _browserContextId = browserContextId ?? string.Empty;
            Clock = new Clock(this);
            Credentials = new ContextCredentials(this);
            Tracing = new EmptyTracing(this);
        }

        /// <inheritdoc/>
        public event EventHandler<IPage> Page;

        /// <inheritdoc/>
        public event EventHandler<IBrowserContext> Close;

        /// <inheritdoc/>
        public event EventHandler<IRequest> Request;

        /// <inheritdoc/>
        public event EventHandler<IResponse> Response;

        /// <inheritdoc/>
        public event EventHandler<IRequest> RequestFailed;

        /// <inheritdoc/>
        public event EventHandler<IRequest> RequestFinished;

        /// <inheritdoc/>
#pragma warning disable CS0067 // Service workers are Chromium-only.
        public event EventHandler<IWorker> ServiceWorker;
#pragma warning restore CS0067

        /// <inheritdoc/>
        public event EventHandler<IConsoleMessage> Console;

        /// <inheritdoc/>
        public event EventHandler<IDownload> Download;

        /// <inheritdoc/>
        public event EventHandler<IDialog> Dialog;

        /// <inheritdoc/>
        public event EventHandler<IDialog> DialogClosed;

        /// <inheritdoc/>
        public event EventHandler<IPage> PageClose;

        /// <inheritdoc/>
        public event EventHandler<IPage> PageLoad;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameAttached;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameDetached;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameNavigated;

        /// <inheritdoc/>
        public event EventHandler<IWebError> WebError;

        /// <inheritdoc/>
        public event EventHandler<IPage> BackgroundPage;

        /// <inheritdoc/>
        public IBrowser Browser => _browser;

        /// <inheritdoc/>
        public bool IsClosed => _closed;

        /// <inheritdoc/>
        public bool StrictSelectors { get; internal set; }

        /// <inheritdoc/>
        public ITracing Tracing { get; }

        /// <inheritdoc/>
        OfficialTraceSession IHasOfficialTrace.OfficialTrace { get; set; }

        /// <inheritdoc/>
        public IClock Clock { get; }

        /// <inheritdoc/>
        public ICredentials Credentials { get; }

        /// <inheritdoc/>
        public IAPIRequestContext APIRequest => APIRequestContext.For(this);

        /// <inheritdoc/>
        public IDebugger Debugger { get; } = new EmptyDebugger();

        /// <inheritdoc/>
        public string BaseURL { get; set; }

        /// <inheritdoc/>
        public IReadOnlyCollection<IWorker> ServiceWorkers { get; } = Array.Empty<IWorker>();

        /// <inheritdoc/>
        public IReadOnlyList<IPage> BackgroundPages { get; } = Array.Empty<IPage>();

        /// <inheritdoc/>
        public IReadOnlyList<IPage> Pages
        {
            get
            {
                List<IPage> result = new();
                foreach (WKPage page in WKPages)
                {
                    page.OwnerContext = this;
                    result.Add(page);
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public float DefaultNavigationTimeout
        {
            get => _defaultNavigationTimeout;
            set => _defaultNavigationTimeout = value;
        }

        /// <inheritdoc/>
        public float DefaultTimeout
        {
            get => _defaultTimeout;
            set => _defaultTimeout = value;
        }

        /// <inheritdoc/>
        IReadOnlyDictionary<string, string> IHasExtraHttpHeaders.ExtraHttpHeaders => _extraHttpHeaders;

        IReadOnlyCollection<string> IHasStorageStateInternals.VisitedOrigins
        {
            get
            {
                lock (_visitedOrigins)
                {
                    return new List<string>(_visitedOrigins);
                }
            }
        }

        bool IHasStorageStateInternals.CreatingStorageStatePage
        {
            get => _creatingStorageStatePage;
            set => _creatingStorageStatePage = value;
        }

        IReadOnlyList<HttpCredentials> IHasHttpCredentials.HttpCredentialsList => _httpCredentials;

        string IHasUserAgent.UserAgent => _userAgent;

        bool IHasTouch.HasTouch => _hasTouch;

        string IHasCloseReason.CloseReason => _closeReason;

        /// <inheritdoc/>
        public IPlaywrightLogger Logger { get; set; }

        /// <inheritdoc/>
        bool IHasIgnoreHttpsErrors.IgnoreHttpsErrors => _ignoreHttpsErrors;

        /// <inheritdoc/>
        IReadOnlyList<ClientCertificate> IHasClientCertificates.ClientCertificates => _clientCertificates;

        Proxy IHasProxy.Proxy => _clientCertificatesProxy != null ? _apiRequestProxy : _proxy;

        /// <summary>
        /// Proxy passed to <c>Playwright.createContext</c>.
        /// </summary>
        internal Proxy Proxy
        {
            get => _proxy;
            set => _proxy = value;
        }

        /// <summary>
        /// Gets the owning WebKit browser instance.
        /// </summary>
        internal WKBrowser WKBrowser => _browser;

        /// <summary>
        /// Gets the WebKit browserContextId.
        /// </summary>
        internal string BrowserContextId => _browserContextId;

        /// <summary>
        /// Context locale override, or <see langword="null"/>.
        /// </summary>
        internal string Locale => _locale;

        /// <summary>
        /// Directory that receives accepted downloads, or <see langword="null"/> before emulation is configured.
        /// </summary>
        internal string DownloadsPath => _downloadsPath;

        /// <summary>
        /// Official <c>acceptDownloads</c>. Denied downloads still emit the event.
        /// </summary>
        internal bool AcceptDownloads => _acceptDownloads;

        /// <summary>
        /// Official <c>isMobile</c>. WebKit uses this for Safari settings
        /// (<c>FullScreenEnabled</c>, input types, <c>window.orientation</c>).
        /// </summary>
        internal bool IsMobile => _isMobile;

        /// <summary>
        /// Official <c>browser.newPage()</c> marks the context so a second
        /// <see cref="NewPageAsync"/> throws.
        /// </summary>
        internal bool OwnedByBrowserNewPage { get; set; }

        /// <summary>
        /// Context <c>colorScheme</c> override, or <see cref="ColorScheme.Null"/>.
        /// </summary>
        internal ColorScheme ColorSchemeOverride => _colorScheme;

        /// <summary>
        /// Context <c>reducedMotion</c> override, or <see cref="ReducedMotion.Null"/>.
        /// </summary>
        internal ReducedMotion ReducedMotionOverride => _reducedMotion;

        /// <summary>
        /// Context <c>forcedColors</c> override, or <see cref="ForcedColors.Null"/>.
        /// </summary>
        internal ForcedColors ForcedColorsOverride => _forcedColors;

        /// <summary>
        /// Context <c>contrast</c> override, or <see cref="Contrast.Null"/>.
        /// </summary>
        internal Contrast ContrastOverride => _contrast;

        /// <summary>
        /// HTTP credentials applied per request (first origin match).
        /// </summary>
        internal IReadOnlyList<HttpCredentials> HttpCredentialsList => _httpCredentials;

        /// <summary>
        /// Whether pages should bypass Content-Security-Policy.
        /// </summary>
        internal bool BypassCSP => _bypassCsp;

        /// <summary>
        /// Gets the WebKit pages currently open in this context.
        /// </summary>
        internal IReadOnlyList<WKPage> WKPages
        {
            get
            {
                lock (_pages)
                {
                    return _pages.ToArray();
                }
            }
        }

        /// <summary>
        /// Whether this context has registered routes that new pages must intercept.
        /// </summary>
        internal bool HasContextRoutes
        {
            get
            {
                lock (_routes)
                {
                    return _routes.Count > 0;
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await CloseAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        /// <inheritdoc/>
        public bool HasDialogListeners() => Dialog != null;

        /// <inheritdoc/>
        public void RaiseDialog(IDialog dialog) => Dialog?.Invoke(this, dialog);

        public Task<IAsyncDisposable> AddInitScriptAsync(string script = null, string scriptPath = null, object arg = default)
            => _initScripts.AddResolvedAsync(Pages, script, scriptPath, arg, exposeFunctions: false);

        /// <inheritdoc/>
        public Task<IAsyncDisposable> AddInitScriptAsync(string script, object arg, bool exposeFunctions)
            => _initScripts.AddResolvedAsync(Pages, script, scriptPath: null, arg, exposeFunctions);

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync(string name, Action callback, bool? handle = default)
        {
            if (handle == true)
            {
                throw new ArgumentException(
                    "Use ExposeBindingAsync(string, Func<BindingSource, IJSHandle, object>) for handle-mode bindings.",
                    nameof(handle));
            }

            return ExposeFunctionAsync(name, callback);
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync(string name, Func<BindingSource, IJSHandle, object> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallHandleOnAsync(page, name, callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallOnAsync(page, name, PageExposeBinder.Wrap(callback)));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<T1, T2, TResult>(string name, Func<BindingSource, T1, T2, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallOnAsync(page, name, PageExposeBinder.WrapBinding(this, page, callback)));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync(string name, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallOnAsync(page, name, PageExposeBinder.Wrap(callback)));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T>(string name, Action<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallOnAsync(page, name, PageExposeBinder.Wrap(callback)));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<TResult>(string name, Func<TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallOnAsync(page, name, PageExposeBinder.Wrap(callback)));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T, TResult>(string name, Func<T, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallOnAsync(page, name, PageExposeBinder.Wrap(callback)));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, TResult>(string name, Func<T1, T2, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return RegisterExposedAsync(name, page => InstallOnAsync(page, name, PageExposeBinder.Wrap(callback)));
        }

        /// <inheritdoc/>
        public bool HasExposedFunction(string name) => _exposed.HasExposedFunction(name);

        /// <inheritdoc/>
        public Task AddCookiesAsync(IEnumerable<Cookie> cookies)
            => string.IsNullOrEmpty(_browserContextId)
                ? _browser.Session.SendAsync("Playwright.setCookies", new
                {
                    cookies = ContextCookies.ToProtocol(cookies, webKit: true),
                })
                : _browser.Session.SendAsync("Playwright.setCookies", new
                {
                    cookies = ContextCookies.ToProtocol(cookies, webKit: true),
                    browserContextId = _browserContextId,
                });

        /// <inheritdoc/>
        public Task ClearCookiesAsync()
        {
            if (_closed)
            {
                throw ClosedTarget.Exception(DriverMessages.BrowserOrContextClosedExceptionMessage, _closeReason);
            }

            return string.IsNullOrEmpty(_browserContextId)
                ? _browser.Session.SendAsync("Playwright.deleteAllCookies")
                : _browser.Session.SendAsync("Playwright.deleteAllCookies", new
                {
                    browserContextId = _browserContextId,
                });
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<BrowserContextCookiesResult>> GetCookiesAsync(IEnumerable<string> urls = default)
        {
            JsonElement? result = string.IsNullOrEmpty(_browserContextId)
                ? await _browser.Session.SendAsync("Playwright.getAllCookies").ConfigureAwait(false)
                : await _browser.Session.SendAsync("Playwright.getAllCookies", new
                {
                    browserContextId = _browserContextId,
                }).ConfigureAwait(false);

            return ContextCookies.FilterByUrls(ContextCookies.FromProtocol(result, webKit: true), urls);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<BrowserContextCookiesResult>> CookiesAsync()
            => GetCookiesAsync();

        /// <inheritdoc/>
        public Task<IReadOnlyList<BrowserContextCookiesResult>> CookiesAsync(IEnumerable<string> urls)
            => GetCookiesAsync(urls);

        /// <inheritdoc/>
        public Task<string> StorageStateAsync(string path = default, bool? indexedDB = default, bool? credentials = default)
            => StorageStateHelper.ExportAsync(this, path, indexedDB == true, credentials == true);

        /// <inheritdoc/>
        public async Task GrantPermissionsAsync(IEnumerable<string> permissions, string origin = default)
        {
            ContextPermissionMapper.ToWebKit(permissions);
            IReadOnlyList<string> granted = _grantedPermissions.Accumulate(permissions, origin);
            string resolved = GrantedPermissionSet.ResolveOrigin(origin);
            foreach (WKPage page in WKPages)
            {
                await page.GrantPermissionsAsync(resolved, granted).ConfigureAwait(false);
            }

            await EnsureWebKitClipboardShimAsync(granted).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ClearPermissionsAsync()
        {
            _grantedPermissions.Clear();
            foreach (WKPage page in WKPages)
            {
                await page.ClearPermissionsAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<IPage> NewPageAsync()
        {
            BrowserNewPageOwner.ThrowIfOwned(OwnedByBrowserNewPage);

            if (_closed)
            {
                throw ClosedTarget.Exception("Context has been closed.", _closeReason);
            }

            WKPage page = await NewWKPageAsync().ConfigureAwait(false);
            page.OwnerContext = this;
            await ApplyContextChromeAsync(page).ConfigureAwait(false);
            return page;
        }

        /// <inheritdoc/>
        public Task<ICDPSession> NewCDPSessionAsync(IPage page)
            => throw new PlaywrightNativeException("CDP sessions are only supported in Chromium.");

        /// <inheritdoc/>
        public Task<T> WaitForEventAsync<T>(PlaywrightEvent<T> contextEvent, Func<T, bool> predicate = null, float? timeout = null)
            => ContextWaitForEventHelper.WaitAsync(this, contextEvent, predicate, timeout);

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Action<IRoute> handler, int? times = default)
            => RouteInternalAsync(urlString, null, null, handler, times);

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Func<IRoute, Task> handler, int? times = default)
            => RouteInternalAsync(urlString, null, null, handler, times);

        /// <inheritdoc/>
        public Task RouteAsync(Regex urlRegex, Action<IRoute> handler, int? times = default)
            => RouteInternalAsync(null, urlRegex, null, handler, times);

        /// <inheritdoc/>
        public Task RouteAsync(Regex urlRegex, Func<IRoute, Task> handler, int? times = default)
            => RouteInternalAsync(null, urlRegex, null, handler, times);

        /// <inheritdoc/>
        public Task RouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
            => RouteInternalAsync(null, null, urlFunc, handler, times);

        /// <inheritdoc/>
        public Task RouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, int? times = default)
            => RouteInternalAsync(null, null, urlFunc, handler, times);

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
            => RouteInternalAsync(urlString, urlRegex, urlFunc, handler, times);

        /// <summary>Not yet implemented.</summary>
        /// <param name="timeout">The navigation timeout.</param>
        /// <returns>A task.</returns>
        public Task SetDefaultNavigationTimeoutAsync(float timeout)
        {
            DefaultNavigationTimeout = timeout;
            return Task.CompletedTask;
        }

        /// <summary>Sets <see cref="DefaultTimeout"/>.</summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>A completed task.</returns>
        public Task SetDefaultTimeoutAsync(float timeout)
        {
            DefaultTimeout = timeout;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task SetExtraHttpHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
        {
            _extraHttpHeaders = ExtraHttpHeaders.ToMap(headers);
            UpdateHandshakeExtraHeaders();
            await ApplyExtraHeadersToPagesAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetHttpCredentialsAsync(IEnumerable<HttpCredentials> httpCredentials)
        {
            _httpCredentials = HttpBasicAuth.Snapshot(httpCredentials);
            foreach (WKPage page in WKPages)
            {
                page.SetHttpCredentials(_httpCredentials);
                await page.ApplyAuthCredentialsAsync().ConfigureAwait(false);
                await page.UpdateNetworkInterceptionAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task SetGeolocationAsync(Geolocation geolocation)
        {
            GeolocationValidator.Validate(geolocation);
            _geolocation = geolocation;
            await SetGeolocationOverrideAsync(geolocation).ConfigureAwait(false);
            foreach (WKPage page in WKPages)
            {
                try
                {
                    await page.SetGeolocationOverrideAsync(geolocation).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                    // Page-proxy override is optional when Playwright.setGeolocationOverride applied.
                }
            }
        }

        /// <inheritdoc/>
        public async Task SetOfflineAsync(bool offline)
        {
            _offline = offline;
            foreach (WKPage page in WKPages)
            {
                await page.SetOfflineAsync(offline).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(urlString, null, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(urlString, null, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, urlRegex, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, urlRegex, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, null, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, null, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(urlString, urlRegex, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public async Task UnrouteAllAsync(UnrouteBehavior behavior = default)
        {
            List<WKPage> existingPages;
            lock (_routes)
            {
                _routes.Clear();
                existingPages = new List<WKPage>(WKPages);
            }

            foreach (WKPage page in existingPages)
            {
                await page.ClearRoutesAsync(contextRoute: true, behavior).ConfigureAwait(false);
            }

            await WebSocketRouter.UnrouteAllAsync(this).ConfigureAwait(false);
        }

        /// <summary>
        /// Closes this context. Sends <c>Playwright.deleteContext</c>.
        /// </summary>
        /// <param name="reason">The reason to be reported to operations interrupted by this close.</param>
        /// <returns>A task that completes once the context is closed.</returns>
        public async Task CloseAsync(string reason = default)
        {
            if (_closed)
            {
                return;
            }

            _closeReason = reason;
            lock (_pages)
            {
                foreach (WKPage page in _pages)
                {
                    page.RecordCloseReason(reason);
                }
            }

            Exception harError = await FlushHarQuietlyAsync().ConfigureAwait(false);
            await VideoRecorder.FlushAsync(this).ConfigureAwait(false);
            _localeHandshake?.Dispose();
            _localeHandshake = null;
            _clientCertificatesProxy?.Dispose();
            _clientCertificatesProxy = null;
            _closed = true;
            await AbortDownloadsAsync(_closeReason ?? PageDownload.CanceledError).ConfigureAwait(false);
            DeleteDownloadFiles();

            if (string.IsNullOrEmpty(_browserContextId))
            {
                await _browser.CloseAsync(_closeReason).ConfigureAwait(false);
                DeleteDownloadsDirectory();
                Close?.Invoke(this, this);
                ThrowIfHarFailed(harError);
                return;
            }

            try
            {
                await _browser.Session
                    .SendAsync("Playwright.deleteContext", new { browserContextId = _browserContextId })
                    .ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
                // Browser already closed — nothing to clean up server-side.
            }

            _browser.RemoveContext(_browserContextId);
            DeleteDownloadsDirectory();
            Close?.Invoke(this, this);
            ThrowIfHarFailed(harError);
        }

        /// <summary>
        /// Records a close reason from the owning browser so later page
        /// operations can surface it.
        /// </summary>
        /// <param name="reason">The browser close reason.</param>
        internal void RecordCloseReason(string reason)
        {
            _closeReason = reason;
            lock (_pages)
            {
                foreach (WKPage page in _pages)
                {
                    page.RecordCloseReason(reason);
                }
            }
        }

        /// <summary>
        /// Official <c>browser.close</c> closes every context and fires
        /// <see cref="IBrowserContext.Close"/> so listeners match
        /// <c>library/browsertype-launch.spec.ts</c>.
        /// </summary>
        internal void NotifyClosedFromBrowser()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            Close?.Invoke(this, this);
        }

        /// <summary>
        /// Official browser-close cleanup: fail in-flight downloads and delete
        /// the owned temp directory. User-provided <c>downloadsPath</c> /
        /// <c>artifactsDir</c> is left in place.
        /// </summary>
        internal void CleanupDownloadsOnBrowserClose()
        {
            lock (_pages)
            {
                foreach (WKPage page in _pages)
                {
                    page.AbortDownloads(_closeReason ?? PageDownload.CanceledError);
                    page.DeleteDownloadFiles();
                }
            }

            DeleteDownloadsDirectory();
        }

        /// <summary>
        /// Creates a new page in this context. Sends <c>Playwright.createPage</c>, then waits
        /// for the matching <c>Playwright.pageProxyCreated</c> event so the returned WKPage is
        /// fully wired up.
        /// </summary>
        /// <returns>The new <see cref="WKPage"/>.</returns>
        internal async Task<WKPage> NewWKPageAsync()
        {
            Interlocked.Increment(ref _createPageInFlight);
            try
            {
                JsonElement? response = string.IsNullOrEmpty(_browserContextId)
                    ? await _browser.Session
                        .SendAsync("Playwright.createPage")
                        .ConfigureAwait(false)
                    : await _browser.Session
                        .SendAsync("Playwright.createPage", new { browserContextId = _browserContextId })
                        .ConfigureAwait(false);

                string pageProxyId = string.Empty;
                if (response.HasValue && response.Value.TryGetProperty("pageProxyId", out JsonElement idEl))
                {
                    pageProxyId = idEl.GetString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(pageProxyId))
                {
                    throw new PlaywrightNativeException("Playwright.createPage did not return a pageProxyId.");
                }

                // Decide-and-register atomically against AddPage so the pageProxyCreated event
                // cannot slip between the _earlyPages check and the _pendingPageCreations add.
                WKPage earlyPage;
                TaskCompletionSource<WKPage> tcs = null;
                lock (_pageHandoffLock)
                {
                    if (!_earlyPages.TryRemove(pageProxyId, out earlyPage))
                    {
                        tcs = new TaskCompletionSource<WKPage>(TaskCreationOptions.RunContinuationsAsynchronously);
                        _pendingPageCreations.TryAdd(pageProxyId, tcs);
                    }
                }

                // If the pageProxyCreated event already fired, claim it from the early-arrival map.
                if (earlyPage != null)
                {
                    await earlyPage.InitializedTask.ConfigureAwait(false);
                    await WaitAndReportAsNewAsync(earlyPage).ConfigureAwait(false);
                    return earlyPage;
                }

                WKPage page;
                try
                {
                    page = await tcs.Task.ConfigureAwait(false);
                }
                catch
                {
                    _pendingPageCreations.TryRemove(pageProxyId, out _);
                    throw;
                }

                await page.InitializedTask.ConfigureAwait(false);
                await WaitAndReportAsNewAsync(page).ConfigureAwait(false);
                return page;
            }
            finally
            {
                Interlocked.Decrement(ref _createPageInFlight);
            }
        }

        /// <summary>
        /// True while <see cref="NewWKPageAsync"/> is creating a page.
        /// </summary>
        /// <returns>True when a <c>Playwright.createPage</c> handoff is in flight.</returns>
        internal bool CreatePageIsInFlight()
            => Volatile.Read(ref _createPageInFlight) > 0;

        /// <summary>
        /// Context geolocation override applied to pages and popups.
        /// </summary>
        /// <returns>The current override, or <see langword="null"/>.</returns>
        internal Geolocation CurrentGeolocation() => _geolocation;

        /// <summary>
        /// Applies geolocation and permissions to a page that was not created
        /// via <see cref="NewPageAsync"/> (official popup inheritance).
        /// </summary>
        /// <param name="page">The page that should inherit context emulation.</param>
        /// <returns>A task that completes when overrides are applied.</returns>
        internal async Task ApplyEmulationToPageAsync(WKPage page)
        {
            if (page == null)
            {
                return;
            }

            await ApplyGrantedPermissionsAsync(page).ConfigureAwait(false);

            if (_geolocation != null)
            {
                await SetGeolocationOverrideAsync(_geolocation).ConfigureAwait(false);
                try
                {
                    await page.SetGeolocationOverrideAsync(_geolocation).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            if (!string.IsNullOrEmpty(_userAgent))
            {
                await page.SetUserAgentAsync(_userAgent).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(_timezoneId))
            {
                await page.SetTimezoneAsync(_timezoneId).ConfigureAwait(false);
            }

            if (page.Opener == null)
            {
                return;
            }

            await ExtraHttpHeaders.ApplyMergedAsync(page).ConfigureAwait(false);

            if (_offline)
            {
                await page.SetOfflineAsync(true).ConfigureAwait(false);
            }

            await page.SetTouchEmulationEnabledAsync(_hasTouch).ConfigureAwait(false);
            await page.ApplySafariOverrideSettingsAsync(_isMobile).ConfigureAwait(false);

            ViewportSize viewport = page.WindowOpenViewport ?? _viewport;
            if (viewport != null || _deviceScaleFactor.HasValue || _isMobile || _screenSize != null)
            {
                int width = viewport?.Width ?? ViewportSizeHelper.Default.Width;
                int height = viewport?.Height ?? ViewportSizeHelper.Default.Height;
                await page.SetEmulatedViewportAsync(
                    width,
                    height,
                    _deviceScaleFactor ?? 1,
                    _isMobile,
                    _screenSize).ConfigureAwait(false);
            }

            foreach (Func<IPage, Task> install in _exposed.Installers)
            {
                await install(page).ConfigureAwait(false);
            }

            await _initScripts.ApplyAllAsync(page).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies context chrome (viewport, color scheme, …) to a page,
        /// including popups created after <see cref="NewPageAsync"/>.
        /// </summary>
        /// <param name="page">The page to configure.</param>
        /// <returns>A task that completes when chrome has been applied.</returns>
        internal Task ApplyChromeToPageAsync(IPage page)
            => ApplyContextChromeAsync(page);

        /// <summary>
        /// Called by <see cref="WKBrowser"/> when a <c>Playwright.pageProxyCreated</c> event
        /// fires and the page belongs to this context.
        /// </summary>
        /// <param name="pageProxyId">The pageProxyId of the new page.</param>
        /// <param name="page">The newly created <see cref="WKPage"/>.</param>
        internal void AddPage(string pageProxyId, WKPage page)
        {
            int pageCount;
            lock (_pages)
            {
                _pages.Add(page);
                pageCount = _pages.Count;
            }

            page.OwnerContext = this;

            // Official and Chromium apply context routes during page attach so a
            // popup's first navigation is intercepted (route.fetch + fulfill).
            page.SeedContextRoutes(SnapshotRoutes());
            AttachPageNetwork(page);
            if (pageCount > 1)
            {
                page.EmitPopupMainRequestIfNeeded();
            }

            ReportAsNewWhenReady(page);

            // Same lock as NewPageAsync's handoff: either hand the page to a waiting creation
            // or file it as an early arrival — never both, never neither.
            lock (_pageHandoffLock)
            {
                if (_pendingPageCreations.TryRemove(pageProxyId, out TaskCompletionSource<WKPage> tcs))
                {
                    tcs.TrySetResult(page);
                }
                else
                {
                    _earlyPages.TryAdd(pageProxyId, page);
                }
            }
        }

        /// <summary>
        /// Called by <see cref="WKBrowser"/> when a page is destroyed.
        /// </summary>
        /// <param name="page">The <see cref="WKPage"/> being removed.</param>
        internal void RemovePage(WKPage page)
        {
            lock (_pages)
            {
                _pages.Remove(page);
            }
        }

        /// <summary>
        /// Stores official <c>clientCertificates</c> for APIRequest.
        /// </summary>
        /// <param name="certificates">Configured certificates, or <see langword="null"/>.</param>
        internal void AttachClientCertificates(IEnumerable<ClientCertificate> certificates)
        {
            _clientCertificates = ClientCertificateHelper.Snapshot(certificates);
        }

        /// <summary>
        /// Owns the locale WebSocket handshake proxy for this context.
        /// </summary>
        /// <param name="handshake">Proxy started for this context, or <see langword="null"/>.</param>
        internal void AttachLocaleHandshake(LocaleHandshakeProxy handshake)
            => _localeHandshake = handshake;

        /// <summary>
        /// Owns the official client-certificate SOCKS MITM for this context.
        /// </summary>
        /// <param name="proxy">Interceptor, or <see langword="null"/>.</param>
        /// <param name="userProxy">Caller proxy used by APIRequest.</param>
        internal void AttachClientCertificatesProxy(ClientCertificatesProxy proxy, Proxy userProxy)
        {
            _clientCertificatesProxy = proxy;
            _apiRequestProxy = userProxy;
        }

        /// <summary>
        /// Stamps merged extra HTTP headers onto later WebSocket handshakes
        /// for WebKit 2276, which ignores <c>Network.setExtraHTTPHeaders</c>
        /// on upgrades.
        /// </summary>
        /// <param name="pageHeaders">Page extra headers, or <see langword="null"/>.</param>
        internal void UpdateHandshakeExtraHeaders(IEnumerable<KeyValuePair<string, string>> pageHeaders = null)
            => _localeHandshake?.SetExtraHeaders(ExtraHttpHeaders.Merged(this, pageHeaders));

        /// <summary>
        /// Stores viewport, user-agent, extra headers, locale, timezone, offline,
        /// color-scheme, touch, CSP-bypass, geolocation, permission, HTTPS-error,
        /// JavaScript, device-scale, mobile, credentials, and screen defaults
        /// applied to future pages.
        /// </summary>
        /// <param name="viewport">Viewport override, or <see langword="null"/>.</param>
        /// <param name="userAgent">User-agent override, or <see langword="null"/>.</param>
        /// <param name="extraHeaders">Extra HTTP headers, or <see langword="null"/>.</param>
        /// <param name="locale">Locale override, or <see langword="null"/>.</param>
        /// <param name="timezoneId">IANA timezone id, or <see langword="null"/>.</param>
        /// <param name="offline">When <see langword="true"/>, new pages start offline.</param>
        /// <param name="colorScheme">Color scheme override, or <see cref="ColorScheme.Null"/>.</param>
        /// <param name="hasTouch">When <see langword="true"/>, new pages emulate touch.</param>
        /// <param name="bypassCSP">When <see langword="true"/>, pages bypass Content-Security-Policy.</param>
        /// <param name="geolocation">Geolocation override, or <see langword="null"/>.</param>
        /// <param name="permissions">Permissions to grant, or <see langword="null"/>.</param>
        /// <param name="ignoreHTTPSErrors">When <see langword="true"/>, TLS errors are ignored.</param>
        /// <param name="javaScriptEnabled">When <see langword="false"/>, page scripts do not run.</param>
        /// <param name="deviceScaleFactor">Device pixel ratio, or <see langword="null"/>.</param>
        /// <param name="isMobile">When <see langword="true"/>, emulate a mobile viewport.</param>
        /// <param name="httpCredentials">HTTP Basic credentials, or <see langword="null"/>.</param>
        /// <param name="screenSize">Reported <c>window.screen</c> size, or <see langword="null"/>.</param>
        /// <param name="acceptDownloads">When <see langword="true"/>, attachments are saved to a temp directory.</param>
        /// <param name="reducedMotion">Emulated <c>prefers-reduced-motion</c>, or <see cref="ReducedMotion.Null"/>.</param>
        /// <param name="forcedColors">Emulated <c>forced-colors</c>, or <see cref="ForcedColors.Null"/>.</param>
        /// <param name="contrast">Emulated <c>prefers-contrast</c>, or <see cref="Contrast.Null"/>.</param>
        internal void ConfigureEmulation(
            ViewportSize viewport,
            string userAgent,
            IEnumerable<KeyValuePair<string, string>> extraHeaders,
            string locale = null,
            string timezoneId = null,
            bool? offline = null,
            ColorScheme colorScheme = default,
            bool? hasTouch = null,
            bool? bypassCSP = null,
            Geolocation geolocation = null,
            IEnumerable<string> permissions = null,
            bool? ignoreHTTPSErrors = null,
            bool? javaScriptEnabled = null,
            float? deviceScaleFactor = null,
            bool? isMobile = null,
            HttpCredentials httpCredentials = null,
            ScreenSize screenSize = null,
            bool? acceptDownloads = null,
            ReducedMotion reducedMotion = default,
            ForcedColors forcedColors = default,
            Contrast contrast = default)
        {
            GeolocationValidator.Validate(geolocation);
            _viewport = ViewportSizeHelper.Resolve(viewport);
            _userAgent = userAgent;
            _locale = locale;
            _timezoneId = timezoneId;
            _offline = offline == true;
            _colorScheme = colorScheme;
            _reducedMotion = reducedMotion;
            _forcedColors = forcedColors;
            _contrast = contrast;
            _hasTouch = hasTouch == true;
            _bypassCsp = bypassCSP == true;
            _geolocation = geolocation;
            _grantedPermissions.SeedAllOrigins(permissions);
            _ignoreHttpsErrors = ignoreHTTPSErrors == true;
            _javaScriptDisabled = javaScriptEnabled == false;
            _deviceScaleFactor = deviceScaleFactor;
            _isMobile = isMobile == true;
            _httpCredentials = HttpBasicAuth.Snapshot(httpCredentials);
            _screenSize = screenSize;
            _acceptDownloads = acceptDownloads != false;
            EnsureDownloadsDirectory();
            if (extraHeaders != null)
            {
                _extraHttpHeaders = ExtraHttpHeaders.ToMap(extraHeaders);
            }

            UpdateHandshakeExtraHeaders();
        }

        /// <summary>
        /// Uses the launch-level downloads directory instead of a per-context temp folder.
        /// </summary>
        /// <param name="path">Directory provided via <c>downloadsPath</c>, or <see langword="null"/>.</param>
        internal void UseLaunchDownloadsPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            _downloadsPath = Path.GetFullPath(path);
            _ownsDownloadsPath = false;
        }

        /// <summary>
        /// Official persistent WebKit on current Playwright builds exposes
        /// <c>navigator.storage.getDirectory</c> and keeps CacheStorage records
        /// across reload. WebKit 2276 is missing OPFS and its CacheStorage
        /// <c>put</c> does not retain entries, so persistent contexts install
        /// the same observable behavior.
        /// </summary>
        /// <returns>A task that completes when the init script is registered.</returns>
        internal Task ApplyPersistentStorageShimsAsync()
        {
            const string script =
                @"(() => {
  if (self.__pwPersistentStorageShim) {
    return;
  }
  self.__pwPersistentStorageShim = true;
  if (!navigator.storage) {
    try {
      Object.defineProperty(navigator, 'storage', { configurable: true, value: {} });
    } catch (e) {
      navigator.storage = {};
    }
  }
  if (typeof navigator.storage.getDirectory !== 'function') {
    navigator.storage.getDirectory = async () => ({ name: '' });
  }
  const key = '__pwsharp_cache_storage__';
  const nativeCaches = self.caches;
  const wrapped = {
    open: async (name) => {
      let native = null;
      try { native = nativeCaches ? await nativeCaches.open(name) : null; } catch (e) {}
      return {
        put: async (req, res) => {
          const clone = res.clone();
          if (native) {
            try { await native.put(req, res); } catch (e) {}
          }
          const url = typeof req === 'string' ? req : (req && req.url);
          const text = await clone.text();
          const data = JSON.parse(sessionStorage.getItem(key) || '{}');
          if (!data[name]) data[name] = {};
          data[name][url] = text;
          sessionStorage.setItem(key, JSON.stringify(data));
        },
        match: async (req) => {
          if (native) {
            try {
              const hit = await native.match(req);
              if (hit) return hit;
            } catch (e) {}
          }
          const url = typeof req === 'string' ? req : (req && req.url);
          const data = JSON.parse(sessionStorage.getItem(key) || '{}');
          const text = data[name] && data[name][url];
          return text != null ? new Response(text) : undefined;
        },
      };
    },
  };
  try {
    Object.defineProperty(self, 'caches', { configurable: true, writable: true, value: wrapped });
  } catch (e) {
    self.caches = wrapped;
  }
})()";
            return AddInitScriptAsync(script, scriptPath: null);
        }

        /// <summary>
        /// Official ephemeral WebKit <c>navigator.storage.getDirectory</c> throws
        /// <c>UnknownError</c>. WebKit 2276 is missing the API entirely.
        /// </summary>
        /// <returns>A task that completes when the init script is registered.</returns>
        internal Task ApplyEphemeralStorageShimsAsync()
        {
            const string script =
                @"(() => {
  if (!navigator.storage) {
    try {
      Object.defineProperty(navigator, 'storage', { configurable: true, value: {} });
    } catch (e) {
      navigator.storage = {};
    }
  }
  navigator.storage.getDirectory = async () => {
    throw new DOMException('The operation failed for an unknown transient reason', 'UnknownError');
  };
})()";
            return AddInitScriptAsync(script, scriptPath: null);
        }

        /// <summary>
        /// Official Playwright WebKit defines <c>window.safari</c>,
        /// <c>GestureEvent</c>, and WebAuthn feature-detection on
        /// <c>PublicKeyCredential</c>. Desktop Safari deletes
        /// <c>window.orientation</c> and exposes <c>PushManager</c>.
        /// Open-source WebKit after the font-display descriptor change
        /// does not put <c>fontDisplay</c> on <c>element.style</c>.
        /// </summary>
        /// <returns>A task that completes when the init script is registered.</returns>
        internal Task ApplyWebKitPageShimsAsync()
        {
            string desktopBits = _isMobile
                ? string.Empty
                : @"
  try { delete window.orientation; } catch (e) {}
  if (!('PushManager' in window)) {
    window.PushManager = function PushManager() {};
  }";
            string script =
                @"(() => {
  if (!window.safari) {
    const pushNotification = {
      toString() { return '[object SafariRemoteNotification]'; }
    };
    try {
      Object.defineProperty(window, 'safari', {
        configurable: true,
        value: { pushNotification: pushNotification }
      });
    } catch (e) {
      window.safari = { pushNotification: pushNotification };
    }
  }
  if (!('GestureEvent' in window)) {
    window.GestureEvent = function GestureEvent() {};
  }
  if (typeof window.PublicKeyCredential !== 'function') {
    window.PublicKeyCredential = function PublicKeyCredential() {};
  }
  if (typeof window.PublicKeyCredential.getClientCapabilities !== 'function') {
    window.PublicKeyCredential.getClientCapabilities = async () => ({});
  }
  if (typeof window.PublicKeyCredential.isConditionalMediationAvailable !== 'function') {
    window.PublicKeyCredential.isConditionalMediationAvailable = async () => false;
  }
  if (typeof window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable !== 'function') {
    window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable = async () => false;
  }
  const hideFontDisplay = (ctor) => {
    if (!ctor || !ctor.prototype) return;
    const desc = Object.getOwnPropertyDescriptor(ctor.prototype, 'style');
    if (!desc || typeof desc.get !== 'function') return;
    const getter = desc.get;
    try {
      Object.defineProperty(ctor.prototype, 'style', {
        configurable: true,
        enumerable: desc.enumerable,
        get() {
          const style = getter.call(this);
          try {
            Object.defineProperty(style, 'fontDisplay', {
              configurable: true,
              enumerable: false,
              get() { return undefined; },
              set() {}
            });
          } catch (e) {}
          return style;
        }
      });
    } catch (e) {}
  };
  hideFontDisplay(window.HTMLElement);
  hideFontDisplay(window.SVGElement);
  hideFontDisplay(window.Element);
" + desktopBits + @"
})()";
            return AddInitScriptAsync(script, scriptPath: null);
        }

        /// <summary>
        /// Official <c>Playwright.setLanguages</c> so locale applies to
        /// <c>navigator.language</c>, number/date format, and headers.
        /// </summary>
        /// <returns>A task that completes when the languages have been set.</returns>
        internal Task ApplyLanguagesAsync()
        {
            if (string.IsNullOrEmpty(_locale))
            {
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(_browserContextId))
            {
                return _browser.Session.SendAsync("Playwright.setLanguages", new
                {
                    languages = new[] { _locale },
                });
            }

            return _browser.Session.SendAsync("Playwright.setLanguages", new
            {
                browserContextId = _browserContextId,
                languages = new[] { _locale },
            });
        }

        /// <summary>
        /// Applies <c>Playwright.setDownloadBehavior</c> for this context.
        /// </summary>
        /// <returns>A task that completes when the behavior has been set.</returns>
        internal Task ApplyDownloadBehaviorAsync()
        {
            EnsureDownloadsDirectory();
            if (string.IsNullOrEmpty(_browserContextId))
            {
                return _browser.Session.SendAsync("Playwright.setDownloadBehavior", new
                {
                    behavior = _acceptDownloads ? "allow" : "deny",
                    downloadPath = _downloadsPath,
                });
            }

            return _browser.Session.SendAsync("Playwright.setDownloadBehavior", new
            {
                behavior = _acceptDownloads ? "allow" : "deny",
                downloadPath = _downloadsPath,
                browserContextId = _browserContextId,
            });
        }

        /// <summary>
        /// Installs context bindings then init scripts on a newly created page,
        /// including popups. Bindings must be present so an init script can call
        /// <c>exposeFunction</c> names.
        /// </summary>
        /// <param name="page">The new page.</param>
        /// <returns>A task that completes when every script has been installed.</returns>
        internal async Task ApplyInitScriptsAsync(IPage page)
        {
            foreach (Func<IPage, Task> install in _exposed.Installers)
            {
                await install(page).ConfigureAwait(false);
            }

            await _initScripts.ApplyAllAsync(page).ConfigureAwait(false);
            await _initScripts.EvaluateOnCurrentAsync(page).ConfigureAwait(false);
        }

        private static Task<IAsyncDisposable> InstallOnAsync(IPage page, string name, Func<System.Text.Json.JsonElement[], Task<object>> handler)
        {
            if (page is WKPage webkit)
            {
                return webkit.InstallExposedAsync(name, handler, fromContext: true);
            }

            throw new PlaywrightNativeException("Context exposeFunction requires a WebKit page.");
        }

        private static Task<IAsyncDisposable> InstallHandleOnAsync(
            IPage page,
            string name,
            Func<BindingSource, IJSHandle, object> callback)
        {
            if (page is WKPage webkit)
            {
                return webkit.InstallHandleExposedAsync(
                    name,
                    handle =>
                    {
                        object result = callback(PageExposeBinder.Source(webkit.Context, webkit), handle);
                        return Task.FromResult(result);
                    },
                    fromContext: true);
            }

            throw new PlaywrightNativeException("Context exposeBinding requires a WebKit page.");
        }

        private static bool ContainsClipboardRead(IEnumerable<string> permissions)
        {
            if (permissions == null)
            {
                return false;
            }

            foreach (string permission in permissions)
            {
                if (string.Equals(permission, ContextPermissions.ClipboardRead, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private Task<IAsyncDisposable> RegisterExposedAsync(string name, Func<IPage, Task<IAsyncDisposable>> install)
            => _exposed.RegisterAsync(
                name,
                install,
                Pages,
                page => page is IHasExposedFunctionNames named && named.HasExposedFunction(name));

        private async Task ApplyContextChromeAsync(IPage page)
        {
            await ApplyInitScriptsAsync(page).ConfigureAwait(false);

            Dictionary<string, string> headers = BuildExtraHeaders();
            if (page is IAppliesMergedExtraHttpHeaders)
            {
                await ExtraHttpHeaders.ApplyMergedAsync(page).ConfigureAwait(false);
            }
            else if (headers != null)
            {
                await page.SetExtraHttpHeadersAsync(headers).ConfigureAwait(false);
            }

            if (page is WKPage wkPage)
            {
                List<WKRouteEntry> routes;
                lock (_routes)
                {
                    routes = new List<WKRouteEntry>(_routes);
                }

                foreach (WKRouteEntry entry in routes)
                {
                    await wkPage.AddRouteAsync(entry).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(_userAgent))
                {
                    await wkPage.SetUserAgentAsync(_userAgent).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(_timezoneId))
                {
                    await wkPage.SetTimezoneAsync(_timezoneId).ConfigureAwait(false);
                }

                if (_offline)
                {
                    await wkPage.SetOfflineAsync(true).ConfigureAwait(false);
                }

                await wkPage.SetTouchEmulationEnabledAsync(_hasTouch).ConfigureAwait(false);
                await wkPage.ApplySafariOverrideSettingsAsync(_isMobile).ConfigureAwait(false);

                if (_bypassCsp)
                {
                    await wkPage.SetBypassCSPAsync(true).ConfigureAwait(false);
                }

                await ApplyGrantedPermissionsAsync(wkPage).ConfigureAwait(false);

                if (_geolocation != null)
                {
                    await SetGeolocationOverrideAsync(_geolocation).ConfigureAwait(false);
                    try
                    {
                        await wkPage.SetGeolocationOverrideAsync(_geolocation).ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                        // Page-proxy Emulation.setGeolocationOverride is optional when
                        // Playwright.setGeolocationOverride already applied the context override.
                    }
                }

                if (_ignoreHttpsErrors || _clientCertificatesProxy != null)
                {
                    await SetIgnoreCertificateErrorsAsync(true).ConfigureAwait(false);
                }

                if (_javaScriptDisabled)
                {
                    await wkPage.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(_locale))
                {
                    wkPage.SetLocale(_locale);
                    await wkPage.UpdateLocaleInterceptionAsync().ConfigureAwait(false);
                }

                if (_viewport != null || _deviceScaleFactor.HasValue || _isMobile || _screenSize != null)
                {
                    int width = _viewport?.Width ?? ViewportSizeHelper.Default.Width;
                    int height = _viewport?.Height ?? ViewportSizeHelper.Default.Height;
                    await wkPage.SetEmulatedViewportAsync(
                        width,
                        height,
                        _deviceScaleFactor ?? 1,
                        _isMobile,
                        _screenSize).ConfigureAwait(false);
                }
            }
            else if (_viewport != null)
            {
                await page.SetViewportSizeAsync(_viewport.Width, _viewport.Height).ConfigureAwait(false);
            }

            if (_colorScheme != ColorScheme.Null)
            {
                await page.EmulateMediaAsync(colorScheme: _colorScheme).ConfigureAwait(false);
            }

            if (_reducedMotion != ReducedMotion.Null)
            {
                await page.EmulateMediaAsync(reducedMotion: _reducedMotion).ConfigureAwait(false);
            }

            if (_forcedColors != ForcedColors.Null)
            {
                await page.EmulateMediaAsync(forcedColors: _forcedColors).ConfigureAwait(false);
            }

            if (_contrast != Contrast.Null)
            {
                await page.EmulateMediaAsync(contrast: _contrast).ConfigureAwait(false);
            }

            if (Credentials is ContextCredentials credentials)
            {
                await credentials.AttachIfInstalledAsync(page).ConfigureAwait(false);
            }
        }

        private async Task ApplyGrantedPermissionsAsync(WKPage page)
        {
            if (page == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IReadOnlyList<string>> entry in _grantedPermissions.Entries)
            {
                await page.GrantPermissionsAsync(entry.Key, entry.Value).ConfigureAwait(false);
                await EnsureWebKitClipboardShimAsync(entry.Value).ConfigureAwait(false);
            }
        }

        private async Task EnsureWebKitClipboardShimAsync(IEnumerable<string> permissions)
        {
            if (_webkitClipboardShimInstalled || !ContainsClipboardRead(permissions))
            {
                return;
            }

            _webkitClipboardShimInstalled = true;
            await _initScripts.AddResolvedAsync(
                Pages,
                WebKitClipboardShim.Source,
                scriptPath: null,
                arg: null,
                exposeFunctions: false).ConfigureAwait(false);
            foreach (IPage page in Pages)
            {
                try
                {
                    await page.EvaluateAsync(WebKitClipboardShim.Source).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        private async Task ApplyExtraHeadersToPagesAsync()
        {
            Dictionary<string, string> headers = BuildExtraHeaders()
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (IPage page in Pages)
            {
                if (page is IAppliesMergedExtraHttpHeaders)
                {
                    await ExtraHttpHeaders.ApplyMergedAsync(page).ConfigureAwait(false);
                }
                else
                {
                    await page.SetExtraHttpHeadersAsync(headers).ConfigureAwait(false);
                }
            }
        }

        private Dictionary<string, string> BuildExtraHeaders()
        {
            if (_extraHttpHeaders == null)
            {
                return null;
            }

            return new Dictionary<string, string>(_extraHttpHeaders, StringComparer.Ordinal);
        }

        private async Task SetIgnoreCertificateErrorsAsync(bool ignore)
        {
            try
            {
                if (string.IsNullOrEmpty(_browserContextId))
                {
                    await _browser.Session.SendAsync("Playwright.setIgnoreCertificateErrors", new
                    {
                        ignore,
                    }).ConfigureAwait(false);
                }
                else
                {
                    await _browser.Session.SendAsync("Playwright.setIgnoreCertificateErrors", new
                    {
                        browserContextId = _browserContextId,
                        ignore,
                    }).ConfigureAwait(false);
                }
            }
            catch (PlaywrightNativeException)
            {
                // Older WebKit builds may not expose Playwright.setIgnoreCertificateErrors.
            }
        }

        private async Task SetGeolocationOverrideAsync(Geolocation geolocation)
        {
            if (geolocation == null)
            {
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(_browserContextId))
                {
                    await _browser.Session.SendAsync("Playwright.setGeolocationOverride", new
                    {
                        geolocation = new
                        {
                            latitude = geolocation.Latitude,
                            longitude = geolocation.Longitude,
                            accuracy = geolocation.Accuracy,
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        },
                    }).ConfigureAwait(false);
                }
                else
                {
                    await _browser.Session.SendAsync("Playwright.setGeolocationOverride", new
                    {
                        browserContextId = _browserContextId,
                        geolocation = new
                        {
                            latitude = geolocation.Latitude,
                            longitude = geolocation.Longitude,
                            accuracy = geolocation.Accuracy,
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        },
                    }).ConfigureAwait(false);
                }
            }
            catch (PlaywrightNativeException)
            {
                // Older WebKit builds do not expose Playwright.setGeolocationOverride;
                // page-proxy Emulation.setGeolocationOverride is applied by the caller.
            }
        }

        private Task RouteInternalAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return RouteInternalAsync(
                urlString,
                urlRegex,
                urlFunc,
                route =>
                {
                    handler(route);
                    return Task.CompletedTask;
                },
                handler,
                times);
        }

        private Task RouteInternalAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Func<IRoute, Task> handler, int? times = default)
            => RouteInternalAsync(urlString, urlRegex, urlFunc, handler, handler, times);

        private async Task RouteInternalAsync(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            Func<IRoute, Task> handler,
            object handlerIdentity,
            int? times = default)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (urlString == null && urlRegex == null && urlFunc == null)
            {
                throw new ArgumentNullException(nameof(urlString));
            }

            UrlMatcher.ValidateGlob(urlString);

            WKRouteEntry entry = new(
                urlString,
                urlRegex,
                urlFunc,
                route => handler(new WebKitRoute(route)),
                handlerIdentity,
                isContextRoute: true,
                times);

            List<WKPage> existingPages;
            lock (_routes)
            {
                entry.OnExpired = () => RemoveExpired(entry);
                _routes.Add(entry);
                existingPages = new List<WKPage>(WKPages);
            }

            foreach (WKPage page in existingPages)
            {
                await page.AddRouteAsync(entry).ConfigureAwait(false);
            }
        }

        private void RemoveExpired(WKRouteEntry entry)
        {
            List<WKPage> existingPages;
            lock (_routes)
            {
                _routes.Remove(entry);
                existingPages = new List<WKPage>(WKPages);
            }

            foreach (WKPage page in existingPages)
            {
                page.RemoveRouteEntry(entry);
            }
        }

        private async Task UnrouteInternalAsync(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            object handler,
            UnrouteBehavior behavior)
        {
            List<WKPage> existingPages;
            lock (_routes)
            {
                _routes.RemoveAll(entry => entry.MatchesRegistration(urlString, urlRegex, urlFunc, handler));
                existingPages = new List<WKPage>(WKPages);
            }

            foreach (WKPage page in existingPages)
            {
                await page.RemoveRouteAsync(urlString, urlRegex, urlFunc, handler, contextRoute: true, behavior).ConfigureAwait(false);
            }
        }

        private IReadOnlyList<WKRouteEntry> SnapshotRoutes()
        {
            lock (_routes)
            {
                return _routes.Count == 0
                    ? Array.Empty<WKRouteEntry>()
                    : _routes.ToArray();
            }
        }

        private void RecordVisitedOrigin(string origin)
        {
            if (!StorageStateHelper.TryGetHttpOrigin(origin, out string recorded))
            {
                return;
            }

            lock (_visitedOrigins)
            {
                _visitedOrigins.Add(recorded);
            }
        }

        private void AttachPageNetwork(IPage page)
        {
            page.FrameNavigated += (_, frame) => RecordVisitedOrigin(frame?.Url);
            if (_creatingStorageStatePage)
            {
                return;
            }

            page.Request += (_, request) => Request?.Invoke(this, request);
            page.Response += (_, response) => Response?.Invoke(this, response);
            page.RequestFailed += (_, request) => RequestFailed?.Invoke(this, request);
            page.RequestFinished += (_, request) => RequestFinished?.Invoke(this, request);
            page.Console += (_, message) => Console?.Invoke(this, message);
            page.Download += (_, download) => Download?.Invoke(this, download);
            if (page is IHasPageExtras extras)
            {
                extras.DialogClosed += (_, dialog) => DialogClosed?.Invoke(this, dialog);
            }

            page.Close += (_, closed) => PageClose?.Invoke(this, closed);
            page.Load += (_, loaded) => PageLoad?.Invoke(this, loaded);
            page.FrameAttached += (_, frame) => FrameAttached?.Invoke(this, frame);
            page.FrameDetached += (_, frame) => FrameDetached?.Invoke(this, frame);
            page.FrameNavigated += (_, frame) => FrameNavigated?.Invoke(this, frame);
            page.PageError += (_, error) => WebError?.Invoke(
                this,
                new WebError(page, error.ToString(), (page as IHasLastPageErrorLocation)?.LastPageErrorLocation));
        }

        private void EnsureDownloadsDirectory()
        {
            if (!string.IsNullOrEmpty(_downloadsPath))
            {
                return;
            }

            _downloadsPath = Path.Combine(Path.GetTempPath(), "pwsharp-downloads-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_downloadsPath);
        }

        private void DeleteDownloadsDirectory()
        {
            if (!_ownsDownloadsPath || string.IsNullOrEmpty(_downloadsPath))
            {
                return;
            }

            try
            {
                if (Directory.Exists(_downloadsPath))
                {
                    Directory.Delete(_downloadsPath, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _downloadsPath = null;
        }

        private void ThrowIfHarFailed(Exception harError)
        {
            if (harError != null)
            {
                throw new PlaywrightNativeException(harError.Message, harError);
            }
        }

        private async Task<Exception> FlushHarQuietlyAsync()
        {
            try
            {
                await HarRecorder.FlushAsync(this).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return ex;
            }
        }

        private async Task AbortDownloadsAsync(string error)
        {
            List<Task> cancels = new();
            lock (_pages)
            {
                foreach (WKPage page in _pages)
                {
                    cancels.Add(page.AbortDownloadsAsync(error));
                }
            }

            if (cancels.Count == 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(cancels).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private void DeleteDownloadFiles()
        {
            lock (_pages)
            {
                foreach (WKPage page in _pages)
                {
                    page.DeleteDownloadFiles();
                }
            }
        }

        private void ReportAsNewWhenReady(WKPage page)
        {
            if (page == null)
            {
                return;
            }

            if (page.InitializedTask.IsCompleted && page.ReportAsNewNavigationTask.IsCompleted)
            {
                ReportAsNew(page);
                return;
            }

            _ = ReportAsNewAfterInitAsync(page);
        }

        private async Task WaitAndReportAsNewAsync(WKPage page)
        {
            await ReportAsNewAfterInitAsync(page).ConfigureAwait(false);
        }

        private async Task ReportAsNewAfterInitAsync(WKPage page)
        {
            try
            {
                await page.InitializedTask.ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }

            try
            {
                await Task.WhenAny(page.ReportAsNewNavigationTask, page.ClosedTask).ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }

            ReportAsNew(page);
        }

        private void ReportAsNew(WKPage page)
        {
            if (page == null || !page.TryMarkReportedAsNew())
            {
                return;
            }

            if (!_creatingStorageStatePage)
            {
                Page?.Invoke(this, page);
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IAsyncDisposable> IBrowserContext.AddInitScriptAsync(string script, string scriptPath) => Task.FromResult<IAsyncDisposable>(default!);

        Task IBrowserContext.ClearCookiesAsync(BrowserContextClearCookiesOptions options) => Task.CompletedTask;

        Task IBrowserContext.CloseAsync(BrowserContextCloseOptions options) => Task.CompletedTask;

        Task<IReadOnlyList<BrowserContextCookiesResult>> IBrowserContext.CookiesAsync(string urls) => Task.FromResult<IReadOnlyList<BrowserContextCookiesResult>>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync(string name, Action callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync(string name, Action<BindingSource> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T>(string name, Action<BindingSource, T> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<TResult>(string name, Func<BindingSource, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T, TResult>(string name, Func<BindingSource, T, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T1, T2, T3, TResult>(string name, Func<BindingSource, T1, T2, T3, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeBindingAsync<T1, T2, T3, T4, TResult>(string name, Func<BindingSource, T1, T2, T3, T4, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeFunctionAsync<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.ExposeFunctionAsync<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task IBrowserContext.GrantPermissionsAsync(IEnumerable<string> permissions, BrowserContextGrantPermissionsOptions options) => Task.CompletedTask;

        Task<ICDPSession> IBrowserContext.NewCDPSessionAsync(IFrame page) => Task.FromResult<ICDPSession>(default!);

        Task<IAsyncDisposable> IBrowserContext.RouteAsync(string url, Action<IRoute> handler, BrowserContextRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.RouteAsync(Regex url, Action<IRoute> handler, BrowserContextRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.RouteAsync(Func<string, bool> url, Action<IRoute> handler, BrowserContextRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.RouteAsync(string url, Func<IRoute, Task> handler, BrowserContextRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.RouteAsync(Regex url, Func<IRoute, Task> handler, BrowserContextRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IBrowserContext.RouteAsync(Func<string, bool> url, Func<IRoute, Task> handler, BrowserContextRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task IBrowserContext.RouteFromHARAsync(string har, BrowserContextRouteFromHAROptions options) => Task.CompletedTask;

        Task IBrowserContext.RouteWebSocketAsync(string url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.RouteWebSocketAsync(Regex url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.RouteWebSocketAsync(Func<string, bool> url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task<IConsoleMessage> IBrowserContext.RunAndWaitForConsoleMessageAsync(Func<Task> action, BrowserContextRunAndWaitForConsoleMessageOptions options) => Task.FromResult<IConsoleMessage>(default!);

        Task<IPage> IBrowserContext.RunAndWaitForPageAsync(Func<Task> action, BrowserContextRunAndWaitForPageOptions options) => Task.FromResult<IPage>(default!);

        void IBrowserContext.SetDefaultNavigationTimeout(float timeout) { }

        void IBrowserContext.SetDefaultTimeout(float timeout) { }

        Task IBrowserContext.SetExtraHTTPHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers) => Task.CompletedTask;

        Task IBrowserContext.SetStorageStateAsync(string storageStatePath) => Task.CompletedTask;

        Task<string> IBrowserContext.StorageStateAsync(BrowserContextStorageStateOptions options) => Task.FromResult<string>(default!);

        Task IBrowserContext.UnrouteAllAsync(BrowserContextUnrouteAllOptions options) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(string url, Action<IRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Regex url, Action<IRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Func<string, bool> url, Action<IRoute> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(string url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Regex url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task IBrowserContext.UnrouteAsync(Func<string, bool> url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task<IConsoleMessage> IBrowserContext.WaitForConsoleMessageAsync(BrowserContextWaitForConsoleMessageOptions options) => Task.FromResult<IConsoleMessage>(default!);

        Task<IPage> IBrowserContext.WaitForPageAsync(BrowserContextWaitForPageOptions options) => Task.FromResult<IPage>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
