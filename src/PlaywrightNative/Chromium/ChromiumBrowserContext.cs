/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IBrowserContext"/> wrapping <see cref="CRBrowserContext"/>.</summary>
    internal sealed partial class ChromiumBrowserContext : IBrowserContext, IHasStrictSelectors, IHasDefaultTimeouts, IHasBrowserContextExtras, IHasExtraHttpHeaders, IHasIgnoreHttpsErrors, IHasBaseUrl, IHasClientCertificates, IDialogHost, IHasExposedFunctionNames, IHasHttpCredentials, IHasUserAgent, IHasCloseReason, IHasProxy, IHasStorageStateInternals, IHasTouch, IHasPlaywrightLogger, IHasOfficialTrace
    {
        private readonly CRBrowserContext _crCtx;
        private readonly IBrowser _browser;

        // Keyed by CRPage.TargetId so repeated reads of Pages or NewPageAsync
        // surface the same Page wrapper for a given underlying CRPage.
        private readonly ConcurrentDictionary<string, Page> _directPages = new();
        private readonly ConcurrentDictionary<CRWorker, ChromiumWorker> _directServiceWorkers = new();
        private readonly ConcurrentDictionary<CRWorker, CRServiceWorkerNetwork> _serviceWorkerNetworks = new();
        private readonly ConcurrentDictionary<CRRequest, ChromiumRequest> _serviceWorkerRequests = new();
        private readonly ContextInitScriptSet _initScripts = new();
        private readonly ContextExposedRegistry _exposed = new();
        private readonly HashSet<string> _visitedOrigins = new(StringComparer.Ordinal);
        private readonly GrantedPermissionSet _grantedPermissions = new GrantedPermissionSet();
        private float _defaultTimeout = 30_000;
        private float _defaultNavigationTimeout = 30_000;
        private LocaleHandshakeProxy _localeHandshake;
        private ClientCertificatesProxy _clientCertificatesProxy;
        private Proxy _apiRequestProxy;
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
        private IReadOnlyList<ClientCertificate> _clientCertificates;

        internal ChromiumBrowserContext(CRBrowserContext crCtx, IBrowser browser)
        {
            _crCtx = crCtx ?? throw new ArgumentNullException(nameof(crCtx));
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            Tracing = new CRTracing(_crCtx.Browser.Connection.RootSession, this);
            Clock = new Clock(this);
            Credentials = new ContextCredentials(this);
            _crCtx.ServiceWorkerCreated += OnServiceWorkerCreated;
            _crCtx.PublicContext = this;
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
        public event EventHandler<IWorker> ServiceWorker;

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
        public IReadOnlyList<IPage> BackgroundPages { get; } = Array.Empty<IPage>();

        /// <inheritdoc/>
        public IReadOnlyCollection<IWorker> ServiceWorkers
        {
            get
            {
                List<IWorker> workers = new List<IWorker>();
                foreach (CRWorker worker in _crCtx.ServiceWorkers)
                {
                    workers.Add(GetOrCreateServiceWorker(worker));
                }

                return workers;
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<IPage> Pages
        {
            get
            {
                List<IPage> result = new();
                foreach (CRPage cr in _crCtx.Pages)
                {
                    result.Add(GetOrCreatePage(cr));
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

        Proxy IHasProxy.Proxy => _clientCertificatesProxy != null ? _apiRequestProxy : _crCtx.Proxy;

        /// <summary>
        /// Directory that receives accepted downloads, or <see langword="null"/> before emulation is configured.
        /// </summary>
        internal string DownloadsPath => _downloadsPath;

        /// <summary>
        /// Official <c>acceptDownloads</c>. Denied downloads still emit the event.
        /// </summary>
        internal bool AcceptDownloads => _acceptDownloads;

        /// <summary>
        /// Official download events require <c>Browser.setDownloadBehavior</c>
        /// with <c>eventsEnabled</c>. <c>connectOverCDP({ noDefaults })</c>
        /// leaves this unset on the default context.
        /// </summary>
        internal bool DownloadEventsEnabled { get; private set; }

        /// <summary>
        /// Official <c>browser.newPage()</c> marks the context so a second
        /// <see cref="NewPageAsync"/> throws.
        /// </summary>
        internal bool OwnedByBrowserNewPage { get; set; }

        /// <summary>
        /// Device pixel ratio applied to pages in this context. Defaults to <c>1</c>.
        /// </summary>
        internal float DeviceScaleFactor => _deviceScaleFactor ?? 1;

        /// <summary>
        /// CDP browser context id used for Browser-domain commands.
        /// </summary>
        internal string BrowserContextId => _crCtx.BrowserContextId;

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
        bool IHasExposedFunctionNames.HasExposedFunction(string name) => _exposed.HasExposedFunction(name);

        /// <inheritdoc/>
        public Task AddCookiesAsync(IEnumerable<Cookie> cookies)
            => _crCtx.AddCookiesAsync(cookies);

        /// <inheritdoc/>
        public async Task CloseAsync(string reason = default)
        {
            if (_closed)
            {
                return;
            }

            _closeReason = reason;
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    instance.RecordCloseReason(reason);
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
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    instance.DeleteDownloadFiles();
                }
            }

            try
            {
                await _crCtx.CloseAsync().ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
                // Browser already closed — nothing to clean up server-side.
            }

            DeleteDownloadsDirectory();
            foreach (CRWorker worker in new List<CRWorker>(_directServiceWorkers.Keys))
            {
                worker.NotifyClosed();
            }

            Close?.Invoke(this, this);
            if (harError != null)
            {
                throw new PlaywrightNativeException(harError.Message, harError);
            }
        }

        /// <inheritdoc/>
        public Task ClearCookiesAsync()
        {
            if (_closed)
            {
                throw ClosedTarget.Exception(DriverMessages.BrowserOrContextClosedExceptionMessage, _closeReason);
            }

            return _crCtx.ClearCookiesAsync();
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<BrowserContextCookiesResult>> GetCookiesAsync(IEnumerable<string> urls = default)
            => _crCtx.GetCookiesAsync(urls);

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
            Helpers.ContextPermissionMapper.ToChromium(permissions);
            IReadOnlyList<string> granted = _grantedPermissions.Accumulate(permissions, origin);
            string resolved = GrantedPermissionSet.ResolveOrigin(origin);
            await _crCtx.GrantPermissionsAsync(
                granted,
                string.Equals(resolved, "*", StringComparison.Ordinal) ? null : resolved).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ClearPermissionsAsync()
        {
            _grantedPermissions.Clear();
            await _crCtx.ResetPermissionsAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IPage> NewPageAsync()
        {
            BrowserNewPageOwner.ThrowIfOwned(OwnedByBrowserNewPage);

            if (_closed)
            {
                throw ClosedTarget.Exception("Context has been closed.", _closeReason);
            }

            CRPage crPage = await _crCtx.NewPageAsync().ConfigureAwait(false);

            // Wait for the target to finish initial attach/session setup so the
            // returned page is immediately usable (GoToAsync depends on lifecycle
            // wiring that InitializedTask gates on).
            await crPage.InitializedTask.ConfigureAwait(false);

            Page directPage = GetOrCreatePage(crPage);
            await ApplyContextChromeAsync(directPage).ConfigureAwait(false);
            return directPage;
        }

        /// <inheritdoc/>
        public async Task<ICDPSession> NewCDPSessionAsync(IPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (page is not Page instance)
            {
                throw new PlaywrightNativeException("CDP sessions require a Chromium page.");
            }

            CRSession session = await _crCtx.Browser.AttachToTargetAsync(instance.CrPage.TargetId).ConfigureAwait(false);
            try
            {
                await session.SendAsync("Runtime.runIfWaitingForDebugger").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Session is already running.
            }

            CRCDPSession cdp = new CRCDPSession(session, _crCtx.Browser.Connection.RootSession);
            instance.CrPage.Closed += (_, _) => cdp.NotifyTargetClosed();
            return cdp;
        }

        /// <inheritdoc/>
        public async Task<ICDPSession> NewCDPSessionAsync(IFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            // Official: main frame uses the page target. OOPIF frames have their
            // own target in CRPage._sessions. In-process iframes share the parent
            // session and must throw this exact message.
            if (frame.ParentFrame == null)
            {
                return await NewCDPSessionAsync(frame.Page).ConfigureAwait(false);
            }

            if (frame is ChromiumFrame crFrame
                && frame.Page is Page instance
                && instance.CrPage.TryGetOopifTargetId(crFrame.Frame.FrameId, out string targetId))
            {
                CRSession session = await _crCtx.Browser.AttachToTargetAsync(targetId).ConfigureAwait(false);
                try
                {
                    await session.SendAsync("Runtime.runIfWaitingForDebugger").ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                    // Session is already running.
                }

                CRCDPSession cdp = new CRCDPSession(session, _crCtx.Browser.Connection.RootSession);
                instance.CrPage.Closed += (_, _) => cdp.NotifyTargetClosed();
                return cdp;
            }

            throw new PlaywrightNativeException(
                "This frame does not have a separate CDP session, it is a part of the parent frame's session");
        }

        /// <inheritdoc/>
        public Task<T> WaitForEventAsync<T>(PlaywrightEvent<T> contextEvent, Func<T, bool> predicate = null, float? timeout = null)
            => ContextWaitForEventHelper.WaitAsync(this, contextEvent, predicate, timeout);

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
            => RouteInternalAsync(urlString, urlRegex, urlFunc, handler, times);

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
        public async Task SetExtraHttpHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
        {
            _extraHttpHeaders = ExtraHttpHeaders.ToMap(headers);
            await ApplyExtraHeadersToPagesAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetHttpCredentialsAsync(IEnumerable<HttpCredentials> httpCredentials)
        {
            _httpCredentials = HttpBasicAuth.Snapshot(httpCredentials);
            _crCtx.HttpCredentials = _httpCredentials;
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    instance.CrPage.NetworkManager.SetHttpCredentials(_httpCredentials);
                    await instance.CrPage.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
                }
            }

            await ApplyExtraHeadersToPagesAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetGeolocationAsync(Geolocation geolocation)
        {
            GeolocationValidator.Validate(geolocation);
            _geolocation = geolocation;
            _crCtx.Geolocation = geolocation;
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    await instance.CrPage.SetGeolocationOverrideAsync(geolocation).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc/>
        public async Task SetOfflineAsync(bool offline)
        {
            _offline = offline;
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    await instance.CrPage.SetOfflineAsync(offline).ConfigureAwait(false);
                }
            }

            foreach (CRServiceWorkerNetwork network in _serviceWorkerNetworks.Values)
            {
                await network.SetOfflineAsync(offline).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crCtx.UnrouteAsync(urlString, urlRegex, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crCtx.UnrouteAsync(urlString, null, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => _crCtx.UnrouteAsync(urlString, null, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crCtx.UnrouteAsync(null, urlRegex, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => _crCtx.UnrouteAsync(null, urlRegex, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crCtx.UnrouteAsync(null, null, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => _crCtx.UnrouteAsync(null, null, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public async Task UnrouteAllAsync(UnrouteBehavior behavior = default)
        {
            await _crCtx.UnrouteAllAsync(behavior).ConfigureAwait(false);
            await WebSocketRouter.UnrouteAllAsync(this).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await CloseAsync().ConfigureAwait(false);
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
        /// Applies <c>Browser.setDownloadBehavior</c> for this context.
        /// </summary>
        /// <returns>A task that completes when the behavior has been set.</returns>
        internal Task ApplyDownloadBehaviorAsync()
        {
            EnsureDownloadsDirectory();
            DownloadEventsEnabled = true;
            if (string.IsNullOrEmpty(_crCtx.BrowserContextId))
            {
                return _crCtx.Browser.Connection.RootSession.SendAsync("Browser.setDownloadBehavior", new
                {
                    behavior = _acceptDownloads ? "allowAndName" : "deny",
                    downloadPath = _downloadsPath,
                    eventsEnabled = true,
                });
            }

            return _crCtx.Browser.Connection.RootSession.SendAsync("Browser.setDownloadBehavior", new
            {
                behavior = _acceptDownloads ? "allowAndName" : "deny",
                downloadPath = _downloadsPath,
                eventsEnabled = true,
                browserContextId = _crCtx.BrowserContextId,
            });
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
            _crCtx.Geolocation = geolocation;
            _grantedPermissions.SeedAllOrigins(permissions);
            _ignoreHttpsErrors = ignoreHTTPSErrors == true;
            _javaScriptDisabled = javaScriptEnabled == false;
            _deviceScaleFactor = deviceScaleFactor;
            _isMobile = isMobile == true;
            _httpCredentials = HttpBasicAuth.Snapshot(httpCredentials);
            _crCtx.HttpCredentials = _httpCredentials;
            _screenSize = screenSize;
            _acceptDownloads = acceptDownloads != false;
            EnsureDownloadsDirectory();
            if (extraHeaders != null)
            {
                _extraHttpHeaders = ExtraHttpHeaders.ToMap(extraHeaders);
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
        /// Records a close reason from the owning browser so later page
        /// operations can surface it.
        /// </summary>
        /// <param name="reason">The browser close reason.</param>
        internal void RecordCloseReason(string reason)
        {
            _closeReason = reason;
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    instance.RecordCloseReason(reason);
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
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    instance.AbortDownloads(_closeReason ?? PageDownload.CanceledError);
                    instance.DeleteDownloadFiles();
                }
            }

            DeleteDownloadsDirectory();
        }

        /// <summary>
        /// Applies context chrome (viewport, offline, color scheme, …) to a page,
        /// including popups created after <see cref="NewPageAsync"/>.
        /// </summary>
        /// <param name="page">The page to configure.</param>
        /// <returns>A task that completes when chrome has been applied.</returns>
        internal Task ApplyChromeToPageAsync(IPage page)
            => ApplyContextChromeAsync(page);

        /// <summary>
        /// Official popup inheritance: extra headers, offline, touch, and
        /// viewport (window features when present) before the first document
        /// resumes.
        /// </summary>
        /// <param name="page">The popup instance.</param>
        /// <returns>A task that completes when chrome has been applied.</returns>
        internal async Task ApplyPopupChromeBeforeResumeAsync(IPage page)
        {
            if (page is not Page crPage)
            {
                return;
            }

            await ExtraHttpHeaders.ApplyMergedAsync(page).ConfigureAwait(false);
            crPage.CrPage.NetworkManager.SetHttpCredentials(_httpCredentials);
            await crPage.CrPage.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);

            ViewportSize viewport = crPage.CrPage.WindowOpenViewport ?? _viewport;
            if (viewport != null || _deviceScaleFactor.HasValue || _isMobile || _screenSize != null)
            {
                int width = viewport?.Width ?? ViewportSizeHelper.Default.Width;
                int height = viewport?.Height ?? ViewportSizeHelper.Default.Height;
                await crPage.ApplyEmulatedViewportAsync(
                    width,
                    height,
                    _deviceScaleFactor ?? 1,
                    _isMobile,
                    _screenSize).ConfigureAwait(false);
            }

            if (_offline)
            {
                await crPage.CrPage.SetOfflineAsync(true).ConfigureAwait(false);
            }

            await crPage.CrPage.SetTouchEmulationEnabledAsync(
                _hasTouch,
                _hasTouch || _isMobile ? "mobile" : "desktop").ConfigureAwait(false);

            if (_bypassCsp)
            {
                await crPage.CrPage.SetBypassCSPAsync(true).ConfigureAwait(false);
            }

            if (_ignoreHttpsErrors || _clientCertificatesProxy != null)
            {
                await crPage.CrPage.SetIgnoreCertificateErrorsAsync(true).ConfigureAwait(false);
            }

            if (_javaScriptDisabled)
            {
                await crPage.CrPage.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Official popup <c>page</c> event must fire before the opener calls
        /// a context <c>exposeFunction</c> on the new window.
        /// </summary>
        /// <param name="page">The popup instance.</param>
        internal void ReportPopupAsNew(Page page)
            => ReportAsNew(page);

        /// <summary>
        /// Applies color-scheme and related media emulation to a popup before
        /// it is reported. Full chrome (viewport) is applied after initialize.
        /// </summary>
        /// <param name="page">The page to configure.</param>
        /// <returns>A task that completes when media emulation has been applied.</returns>
        internal Task ApplyMediaEmulationAsync(IPage page)
            => ApplyMediaEmulationCoreAsync(page);

        /// <summary>
        /// Returns the cached instance for <paramref name="crPage"/>, creating one if needed.
        /// </summary>
        /// <param name="crPage">The underlying page.</param>
        /// <returns>The public instance.</returns>
        internal Page GetOrCreatePage(CRPage crPage)
        {
            if (crPage == null)
            {
                throw new ArgumentNullException(nameof(crPage));
            }

            if (_directPages.TryGetValue(crPage.TargetId, out Page existing))
            {
                ReportAsNewWhenReady(existing);
                return existing;
            }

            Page created = new Page(crPage, this);
            if (_directPages.TryAdd(crPage.TargetId, created))
            {
                AttachPageNetwork(created);
                ReportAsNewWhenReady(created);
                return created;
            }

            Page existingPage = _directPages[crPage.TargetId];
            ReportAsNewWhenReady(existingPage);
            return existingPage;
        }

        /// <summary>
        /// Installs context bindings then init scripts before the first document resumes.
        /// Bindings must be present so an init script can call <c>exposeFunction</c> names.
        /// </summary>
        /// <param name="page">The new page instance.</param>
        /// <returns>A task that completes when every script has been installed.</returns>
        internal async Task ApplyInitScriptsBeforeResumeAsync(IPage page)
        {
            foreach (Func<IPage, Task> install in _exposed.Installers)
            {
                await install(page).ConfigureAwait(false);
            }

            await _initScripts.ApplyAllAsync(page, callbacks: false).ConfigureAwait(false);
        }

        /// <summary>
        /// Official locale/timezone/UA before <c>Runtime.runIfWaitingForDebugger</c>
        /// so the first document (including popups) sees the override.
        /// </summary>
        /// <param name="page">The new page instance.</param>
        /// <returns>A task that completes when emulation has been applied.</returns>
        internal async Task ApplyLocaleEmulationBeforeResumeAsync(IPage page)
        {
            if (page is not Page crPage)
            {
                return;
            }

            string userAgent = _userAgent ?? string.Empty;
            if (!string.IsNullOrEmpty(userAgent) || !string.IsNullOrEmpty(_locale))
            {
                await crPage.CrPage.SetUserAgentAsync(
                    userAgent,
                    _locale,
                    _isMobile,
                    includeMetadata: !string.IsNullOrEmpty(_userAgent)).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(_locale))
            {
                await crPage.CrPage.SetLocaleOverrideAsync(_locale).ConfigureAwait(false);
            }

            crPage.CrPage.NetworkManager.SetLocale(_locale);
            await crPage.CrPage.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_timezoneId))
            {
                await crPage.CrPage.SetTimezoneOverrideAsync(_timezoneId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Installs <c>exposeFunctions</c> init scripts after the first document
        /// has an execution context.
        /// </summary>
        /// <param name="page">The new page instance.</param>
        /// <returns>A task that completes when callback scripts have been installed.</returns>
        internal Task ApplyCallbackInitScriptsAsync(IPage page)
            => _initScripts.ApplyAllAsync(page, callbacks: true);

        /// <summary>
        /// Replays string context init scripts on the current document.
        /// </summary>
        /// <param name="page">The page whose current document should run the scripts.</param>
        /// <returns>A task that completes when evaluation has been attempted.</returns>
        internal Task EvaluateInitScriptsOnCurrentAsync(IPage page)
            => _initScripts.EvaluateOnCurrentAsync(page);

        /// <summary>
        /// Official: instrument the service-worker session before
        /// <c>Runtime.runIfWaitingForDebugger</c> so the main script request
        /// and start-of-file console are observed.
        /// </summary>
        internal async Task PrepareServiceWorkerNetworkAsync(CRWorker worker)
        {
            if (worker == null
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_DISABLE_SERVICE_WORKER_NETWORK")))
            {
                return;
            }

            ChromiumWorker instance = GetOrCreateServiceWorker(worker);
            AttachServiceWorkerNetwork(worker, instance);
            if (!_serviceWorkerNetworks.TryGetValue(worker, out CRServiceWorkerNetwork network))
            {
                return;
            }

            // Official crServiceWorker updateRequestInterception runs before
            // Network.enable so Fetch is already on when the main script resumes.
            await network.SetRoutesAsync(_crCtx.SnapshotRoutes()).ConfigureAwait(false);
            await network.StartAsync().ConfigureAwait(false);
            await network.SetOfflineAsync(_offline).ConfigureAwait(false);
            Dictionary<string, string> headers = BuildExtraHeaders();
            if (headers != null)
            {
                await network.SetExtraHttpHeadersAsync(headers).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(_userAgent) || !string.IsNullOrEmpty(_locale))
            {
                try
                {
                    // Official CRServiceWorker.updateUserAgent, plus Network so
                    // fetch() on older Chromium picks up the override.
                    string ua = _userAgent ?? string.Empty;
                    await worker.Session.SendAsync("Emulation.setUserAgentOverride", new
                    {
                        userAgent = ua,
                        acceptLanguage = _locale,
                    }).ConfigureAwait(false);
                    await worker.Session.SendAsync("Network.setUserAgentOverride", new
                    {
                        userAgent = ua,
                        acceptLanguage = _locale,
                    }).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        /// <summary>
        /// Official persistent launch: extension service workers attach during
        /// <c>Target.setAutoAttach</c> before this instance exists. Instrument
        /// those workers once the context is ready (network + user-agent).
        /// </summary>
        internal async Task AdoptExistingServiceWorkersAsync()
        {
            foreach (CRWorker worker in _crCtx.ServiceWorkers)
            {
                await PrepareServiceWorkerNetworkAsync(worker).ConfigureAwait(false);
                try
                {
                    await worker.InitializeAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        internal async Task UpdateServiceWorkerInterceptionAsync()
        {
            IReadOnlyList<CRRouteEntry> routes = _crCtx.SnapshotRoutes();
            foreach (CRServiceWorkerNetwork network in _serviceWorkerNetworks.Values)
            {
                await network.SetRoutesAsync(routes).ConfigureAwait(false);
            }
        }

        private static Task<IAsyncDisposable> InstallOnAsync(IPage page, string name, Func<System.Text.Json.JsonElement[], Task<object>> handler)
        {
            if (page is Page chromium)
            {
                return chromium.InstallExposedAsync(name, handler, fromContext: true);
            }

            throw new PlaywrightNativeException("Context exposeFunction requires a Chromium page.");
        }

        private static Task<IAsyncDisposable> InstallHandleOnAsync(
            IPage page,
            string name,
            Func<BindingSource, IJSHandle, object> callback)
        {
            if (page is Page chromium)
            {
                return chromium.InstallHandleBindingAsync(
                    name,
                    handle =>
                    {
                        object result = callback(PageExposeBinder.Source(chromium.Context, chromium), chromium.WrapJSHandle(handle));
                        return Task.FromResult(result);
                    },
                    fromContext: true);
            }

            throw new PlaywrightNativeException("Context exposeBinding requires a Chromium page.");
        }

        private void OnServiceWorkerCreated(object sender, CRWorker worker)
        {
            if (worker == null)
            {
                return;
            }

            ChromiumWorker instance = GetOrCreateServiceWorker(worker);
            ServiceWorker?.Invoke(this, instance);
        }

        private ChromiumWorker GetOrCreateServiceWorker(CRWorker worker)
        {
            return _directServiceWorkers.GetOrAdd(worker, w =>
            {
                ChromiumWorker instance = new ChromiumWorker(w);
                instance.Console += (_, message) => ForwardServiceWorkerConsole(w, message);
                w.Closed += (_, _) =>
                {
                    _directServiceWorkers.TryRemove(w, out _);
                    if (_serviceWorkerNetworks.TryRemove(w, out CRServiceWorkerNetwork network))
                    {
                        network.Dispose();
                    }
                };
                return instance;
            });
        }

        private void AttachServiceWorkerNetwork(CRWorker worker, IWorker instance)
        {
            CRServiceWorkerNetwork network = new CRServiceWorkerNetwork(worker, instance);
            if (!_serviceWorkerNetworks.TryAdd(worker, network))
            {
                network.Dispose();
                return;
            }

            network.Request += (_, request) => Request?.Invoke(this, WrapServiceWorkerRequest(request));
            network.Response += (_, response) => Response?.Invoke(this, new ChromiumResponse(response, WrapServiceWorkerRequest));
            network.RequestFinished += (_, request) => RequestFinished?.Invoke(this, WrapServiceWorkerRequest(request));
            network.RequestFailed += (_, request) => RequestFailed?.Invoke(this, WrapServiceWorkerRequest(request));
        }

        private void ForwardServiceWorkerConsole(CRWorker worker, IConsoleMessage message)
        {
            string workerUrl = worker?.Url;
            if (string.IsNullOrEmpty(workerUrl))
            {
                return;
            }

            int slash = workerUrl.LastIndexOf('/');
            string scope = slash >= 0 ? workerUrl.Substring(0, slash + 1) : workerUrl;
            foreach (Page page in _directPages.Values)
            {
                if ((page.Url ?? string.Empty).StartsWith(scope, StringComparison.Ordinal))
                {
                    page.EmitWorkerConsole(message);
                }
            }
        }

        private ChromiumRequest WrapServiceWorkerRequest(CRRequest request)
        {
            return _serviceWorkerRequests.GetOrAdd(
                request,
                crRequest => new ChromiumRequest(
                    crRequest,
                    response => new ChromiumResponse(response, WrapServiceWorkerRequest),
                    WrapServiceWorkerRequest,
                    _ => null));
        }

        private ChromiumRequest WrapContextRouteRequest(CRRequest request)
        {
            if (request?.ServiceWorker != null)
            {
                return WrapServiceWorkerRequest(request);
            }

            return new ChromiumRequest(request, _ => null, resolveRequest: null, ResolveContextRouteFrame);
        }

        private IFrame ResolveContextRouteFrame(Frame frame)
        {
            if (frame == null)
            {
                return null;
            }

            foreach (Page page in _directPages.Values)
            {
                foreach (Frame candidate in page.CrPage.FrameManager.Frames)
                {
                    if (ReferenceEquals(candidate, frame))
                    {
                        return page.GetOrCreateFrame(frame);
                    }
                }
            }

            return null;
        }

        private Task<IAsyncDisposable> RegisterExposedAsync(string name, Func<IPage, Task<IAsyncDisposable>> install)
            => _exposed.RegisterAsync(
                name,
                install,
                Pages,
                page => page is IHasExposedFunctionNames named && named.HasExposedFunction(name));

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

        private Task RouteInternalAsync(
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

            CRRouteEntry entry = new CRRouteEntry(
                urlString,
                urlRegex,
                urlFunc,
                crRoute => handler(new ChromiumRoute(crRoute, WrapContextRouteRequest)),
                handlerIdentity,
                isContextRoute: true,
                times);
            return _crCtx.RouteAsync(entry);
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

        private async Task ApplyContextChromeAsync(IPage page)
        {
            Dictionary<string, string> headers = BuildExtraHeaders();
            if (page is IAppliesMergedExtraHttpHeaders)
            {
                await ExtraHttpHeaders.ApplyMergedAsync(page).ConfigureAwait(false);
            }
            else if (headers != null)
            {
                await page.SetExtraHttpHeadersAsync(headers).ConfigureAwait(false);
            }

            if (page is Page crPage)
            {
                crPage.CrPage.NetworkManager.SetHttpCredentials(_httpCredentials);
                await crPage.CrPage.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);

                if (_viewport != null || _deviceScaleFactor.HasValue || _isMobile || _screenSize != null)
                {
                    int width = _viewport?.Width ?? ViewportSizeHelper.Default.Width;
                    int height = _viewport?.Height ?? ViewportSizeHelper.Default.Height;
                    await crPage.ApplyEmulatedViewportAsync(
                        width,
                        height,
                        _deviceScaleFactor ?? 1,
                        _isMobile,
                        _screenSize).ConfigureAwait(false);
                }

                string userAgent = _userAgent;
                if (!string.IsNullOrEmpty(_locale) && string.IsNullOrEmpty(userAgent))
                {
                    userAgent = await page.EvaluateAsync<string>("navigator.userAgent").ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(userAgent) || !string.IsNullOrEmpty(_locale))
                {
                    await crPage.CrPage.SetUserAgentAsync(
                        userAgent,
                        _locale,
                        _isMobile,
                        includeMetadata: !string.IsNullOrEmpty(_userAgent)).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(_locale))
                {
                    await crPage.CrPage.SetLocaleOverrideAsync(_locale).ConfigureAwait(false);
                }

                crPage.CrPage.NetworkManager.SetLocale(_locale);
                await crPage.CrPage.NetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(_timezoneId))
                {
                    await crPage.CrPage.SetTimezoneOverrideAsync(_timezoneId).ConfigureAwait(false);
                }

                if (_offline)
                {
                    await crPage.CrPage.SetOfflineAsync(true).ConfigureAwait(false);
                }

                await crPage.CrPage.SetTouchEmulationEnabledAsync(
                    _hasTouch,
                    _hasTouch || _isMobile ? "mobile" : "desktop").ConfigureAwait(false);

                if (!_isMobile)
                {
                    await crPage.CrPage.AddInitScriptAsync(DesktopHoverMedia.Script).ConfigureAwait(false);
                    await page.EvaluateAsync<object>(DesktopHoverMedia.Script).ConfigureAwait(false);
                }

                if (_bypassCsp)
                {
                    await crPage.CrPage.SetBypassCSPAsync(true).ConfigureAwait(false);
                }

                await ApplyGrantedPermissionsAsync().ConfigureAwait(false);

                if (_geolocation != null)
                {
                    await crPage.CrPage.SetGeolocationOverrideAsync(_geolocation).ConfigureAwait(false);
                }

                if (_ignoreHttpsErrors || _clientCertificatesProxy != null)
                {
                    await crPage.CrPage.SetIgnoreCertificateErrorsAsync(true).ConfigureAwait(false);
                }

                if (_javaScriptDisabled)
                {
                    await crPage.CrPage.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);
                }
            }

            await ApplyMediaEmulationCoreAsync(page).ConfigureAwait(false);

            if (Credentials is ContextCredentials credentials)
            {
                await credentials.AttachIfInstalledAsync(page).ConfigureAwait(false);
            }
        }

        private async Task ApplyGrantedPermissionsAsync()
        {
            foreach (KeyValuePair<string, IReadOnlyList<string>> entry in _grantedPermissions.Entries)
            {
                string origin = string.Equals(entry.Key, "*", StringComparison.Ordinal) ? null : entry.Key;
                await _crCtx.GrantPermissionsAsync(entry.Value, origin).ConfigureAwait(false);
            }
        }

        private async Task ApplyMediaEmulationCoreAsync(IPage page)
        {
            if (_colorScheme != ColorScheme.Null)
            {
                await page.EmulateMediaAsync(_colorScheme).ConfigureAwait(false);
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

            foreach (CRServiceWorkerNetwork network in _serviceWorkerNetworks.Values)
            {
                await network.SetExtraHttpHeadersAsync(headers).ConfigureAwait(false);
            }
        }

        private Dictionary<string, string> BuildExtraHeaders()
        {
            bool sendAuth = HttpBasicAuth.ShouldSendPreemptively(_httpCredentials, defaultAlways: false);
            if (_extraHttpHeaders == null && !sendAuth)
            {
                return null;
            }

            Dictionary<string, string> headers = _extraHttpHeaders != null
                ? new Dictionary<string, string>(_extraHttpHeaders, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);

            if (sendAuth)
            {
                HttpBasicAuth.ApplyTo(headers, _httpCredentials);
            }

            return headers;
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
            foreach (IPage page in Pages)
            {
                if (page is Page instance)
                {
                    cancels.Add(instance.AbortDownloadsAsync(error));
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

        private void ReportAsNewWhenReady(Page page)
        {
            if (page == null)
            {
                return;
            }

            if (page.CrPage.InitializedTask.IsCompleted)
            {
                ReportAsNew(page);
                return;
            }

            _ = ReportAsNewAfterInitAsync(page);
        }

        private async Task ReportAsNewAfterInitAsync(Page page)
        {
            try
            {
                await page.CrPage.InitializedTask.ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }

            ReportAsNew(page);
        }

        private void ReportAsNew(Page page)
        {
            if (page == null || !page.CrPage.TryMarkReportedAsNew())
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
