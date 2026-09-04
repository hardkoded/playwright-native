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
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Represents a single WebKit page. Owns the page-proxy session plus an inner
    /// <see cref="WKTargetSession"/> created in response to <c>Target.targetCreated</c>.
    /// Wires navigation (<c>Playwright.navigate</c>), JS evaluation (<c>Runtime.evaluate</c>),
    /// and per-page close (<c>Target.close</c>) on top of those two sessions.
    /// Implements <see cref="IPage"/> directly.
    /// </summary>
    /// <remarks>
    /// Cross-process navigation goes through a <em>provisional</em> inner target: WebKit
    /// fires a second <c>Target.targetCreated</c> with <c>isProvisional: true</c> while the
    /// new process boots, then a <c>Target.didCommitProvisionalTarget</c> event swaps the
    /// active session. Both sessions are listened to simultaneously between provisional
    /// creation and commit so events from either side are not lost.
    /// </remarks>
    [SuppressMessage("IDisposable", "CA2213", Justification = "Target sessions are released when the page closes, not from DisposeAsync.")]
    internal sealed partial class WKPage : IPage, IHasPageExtras, IHasDefaultTimeouts, IHasLastPageErrorLocation, IHasClientInitializedPage, IHasExposedFunctionNames, IAppliesMergedExtraHttpHeaders
    {
        private const string UtilityWorldName = "__playwright_utility_world__";

        // WebKit 2245–2255 (e.g. the macOS-14 2251 build) run a per-frame session model
        // where some domains live on frame sessions rather than the page session. Mirrors
        // upstream wkPage's `enableFrameSessions` gate. Computed once from the resolved
        // WebKit revision.
        private static readonly bool EnableFrameSessions = ComputeEnableFrameSessions();

        private readonly WKSession _session;
        private readonly string _pageProxyId;
        private readonly WKBrowser _browser;
        private readonly WKBrowserContext _context;
        private readonly ILogger _logger;
        private readonly TaskCompletionSource<bool> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _initializedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstNonInitialNavigationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _reportAsNewNavigationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _navigationLock = new();
        private readonly HashSet<string> _lifecycleEvents = new();
        private readonly List<string> _initScripts = new() { WebKitFormDataScript.Source };
        private readonly ConcurrentDictionary<string, Func<JsonElement[], Task<object>>> _exposedFunctions = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, (long Ticks, Task<object> Task)> _recentBindingInvocations = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _evaluateCallbackNames = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Func<IJSHandle, Task<object>>> _handleBindings = new(StringComparer.Ordinal);
        private readonly Queue<string[]> _windowOpenFeatures = new();
        private readonly object _exposeSync = new();
        private readonly ConcurrentDictionary<WKFrame, WebKitFrame> _directFrames = new();
        private readonly ConcurrentDictionary<string, WKExecutionContext> _frameContexts = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, WKExecutionContext> _utilityContexts = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, PageDownload> _downloads = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, WKWorker> _workers = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<WKWorker, WebKitWorker> _directWorkers = new();
        private readonly PageConsoleLog _consoleLog = new();
        private readonly PageEventLog<string> _pageErrors = new();
        private readonly PageEventLog<IRequest> _requests = new(NetworkRequestEvents.RecentRequestLimit);
        private readonly PageDialogTracker _dialogTracker = new();
        private readonly PageListenerRegistry _pageListeners = new();
        private readonly List<WKRouteEntry> _pendingRoutes = new();
        private readonly WKFrameManager _frameManager;
        private readonly Input.Keyboard _keyboard;
        private readonly Input.Mouse _mouse;
        private readonly Input.Touchscreen _touchscreen;
        private Task _bindingReady;
        private IKeyboard _directKeyboard;
        private IMouse _directMouse;
        private ITouchscreen _directTouchscreen;
        private WKPage _opener;
        private bool _hasCommittedNonInitialNavigation;
        private int _reportedAsNew;
        private IConsoleMessage _lastConsoleMessage;
        private int _lastConsoleRepeatCount;

        private WKTargetSession _targetSession;
        private WKTargetSession _provisionalSession;
        private WKExecutionContext _executionContext;
        private int _lastBindingContextId;
        private WKNetworkManager _networkManager;
        private WKNetworkManager _provisionalNetworkManager;
        private string _mainFrameId;
        private string _mainFrameUrl = "about:blank";
        private TaskCompletionSource<bool> _pendingLoadTcs;
        private TaskCompletionSource<bool> _pendingDomContentTcs;
        private TaskCompletionSource<bool> _pendingCommitTcs;
        private string _pendingNavigationUrl;
        private string _navigationStartUrl;
        private bool _pendingNavigationCommitted;
        private string _pendingRedirectTarget;
        private WKRequest _pendingRedirectSource;
        private bool _harRedirectInProgress;
        private string _lastCompetingNavigationUrl;
        private bool _provisionalSwapCommitted;
        private bool _awaitingReplacementTarget;
        private string _provisionalMainFrameId;
        private bool _emittedPendingNavigationRequest;
        private bool _emittedPendingNavigationFinished;
        private WKRequest _firstPendingNavigationRequest;
        private int _inflightCount;
        private bool _closed;
        private bool _closing;
        private bool _pageResumed;
        private string _closeReason;
        private bool _crashed;
        private bool _crashRequested;
        private bool _dialogEnabled;
        private IBrowserContext _ownerContext;
        private PageViewportSizeResult _viewportSize;
        private float _emulatedDeviceScaleFactor = 1;
        private bool _emulatedIsMobile;
        private ScreenSize _independentScreen;
        private float _defaultTimeout = 30_000;
        private float _defaultNavigationTimeout = 30_000;
        private string _emulatedMedia = string.Empty;
        private string _emulatedColorScheme = "Light";
        private string _emulatedReducedMotion = "NoPreference";
        private string _emulatedForcedColors = "None";
        private string _emulatedContrast = "NoPreference";
        private Dictionary<string, string> _extraHttpHeaders;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKPage"/> class.
        /// </summary>
        /// <param name="session">The per-page <see cref="WKSession"/> stamping <c>pageProxyId</c> on outbound messages.</param>
        /// <param name="pageProxyId">The WebKit pageProxyId.</param>
        /// <param name="browser">The owning <see cref="WKBrowser"/>.</param>
        /// <param name="context">The owning <see cref="WKBrowserContext"/>, or null if this is an unattached page.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public WKPage(
            WKSession session,
            string pageProxyId,
            WKBrowser browser,
            WKBrowserContext context,
            ILoggerFactory loggerFactory = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _pageProxyId = pageProxyId ?? throw new ArgumentNullException(nameof(pageProxyId));
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _context = context;
            _logger = loggerFactory?.CreateLogger<WKPage>();

            _keyboard = new Input.Keyboard(new WKRawKeyboard(this));
            _mouse = new Input.Mouse(new WKRawMouse(this), _keyboard);
            _touchscreen = new Input.Touchscreen(new WKRawTouchscreen(this), _keyboard);
            LocalStorage = new WebStorage(this, "localStorage");
            SessionStorage = new WebStorage(this, "sessionStorage");

            _frameManager = new WKFrameManager(pageProxyId);
            _frameManager.MainFrame.LifecycleChanged += OnMainFrameLifecycle;
            _frameManager.FrameAttached += frame => FrameAttached?.Invoke(this, GetOrCreateFrame(frame));
            _frameManager.FrameDetached += frame => FrameDetached?.Invoke(this, GetOrCreateFrame(frame));
            _frameManager.FrameNavigated += frame =>
            {
                if (frame == _frameManager.MainFrame)
                {
                    _mainFrameId = frame.FrameId;
                    _mainFrameUrl = frame.Url;
                    _consoleLog.MarkNavigation();
                    _pageErrors.MarkNavigation();
                    EraseEvaluateCallbacks();
                }

                FrameNavigated?.Invoke(this, GetOrCreateFrame(frame));
            };

            _session.MessageReceived += OnPageProxyMessage;
            Screencast = new WKScreencast(this);
        }

        /// <inheritdoc/>
        public event EventHandler<IPage> Close;

        /// <inheritdoc/>
        public event EventHandler<IConsoleMessage> Console
        {
            add => _pageListeners.Console.Add(value);
            remove => _pageListeners.Console.Remove(value);
        }

        /// <inheritdoc/>
        public event EventHandler<IDialog> Dialog;

        /// <inheritdoc/>
        public event EventHandler<IDialog> DialogClosed;

        /// <inheritdoc/>
        public event EventHandler<IPage> DOMContentLoaded;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameAttached;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameDetached;

        /// <inheritdoc/>
        public event EventHandler<IFrame> FrameNavigated;

        /// <inheritdoc/>
        public event EventHandler<IPage> Load;

        /// <inheritdoc/>
        public event EventHandler<string> PageError;

        /// <inheritdoc/>
        public event EventHandler<IPage> Popup;

        /// <inheritdoc/>
        public event EventHandler<IRequest> Request;

        /// <inheritdoc/>
        public event EventHandler<IRequest> RequestFailed;

        /// <inheritdoc/>
        public event EventHandler<IRequest> RequestFinished;

        /// <inheritdoc/>
        public event EventHandler<IResponse> Response;

        /// <inheritdoc/>
        public event EventHandler<IDownload> Download;

        /// <inheritdoc/>
        public event EventHandler<IFileChooser> FileChooser;

        /// <inheritdoc/>
        public event EventHandler<IWorker> Worker;

        /// <inheritdoc/>
        public event EventHandler<IWebSocket> WebSocket;

        /// <inheritdoc/>
        public event EventHandler<IPage> Crash;

        private event Action<string> LifecycleChanged;

        /// <inheritdoc/>
        public bool IsClosed => _closed;

        /// <inheritdoc/>
        public IBrowserContext Context => _ownerContext;

        /// <inheritdoc/>
        public IAPIRequestContext APIRequest => Context.APIRequest;

        /// <inheritdoc/>
        public WebErrorLocation LastPageErrorLocation { get; private set; }

        /// <inheritdoc/>
        public bool IsClientInitialized =>
            (_opener == null && InitializedTask.IsCompleted) || _hasCommittedNonInitialNavigation;

        /// <inheritdoc/>
        public IReadOnlyList<IFrame> Frames => FrameLookup.DepthFirst(MainFrame);

        /// <inheritdoc/>
        public IReadOnlyList<IWorker> Workers
        {
            get
            {
                List<IWorker> workers = new List<IWorker>();
                foreach (WKWorker worker in _workers.Values)
                {
                    workers.Add(GetOrCreateWorker(worker));
                }

                return workers;
            }
        }

        /// <inheritdoc/>
        public IVideo Video => VideoRecorder.GetVideo(this);

        /// <inheritdoc/>
        public IScreencast Screencast { get; }

        /// <inheritdoc/>
        public ICoverage Coverage { get; } = new EmptyCoverage();

        /// <inheritdoc/>
        public IClock Clock => Context.Clock;

        /// <inheritdoc/>
        public IWebStorage LocalStorage { get; }

        /// <inheritdoc/>
        public IWebStorage SessionStorage { get; }

        /// <inheritdoc/>
        public IKeyboard Keyboard
        {
            get => _directKeyboard ??= new WebKitKeyboard(_keyboard, Context);
            set => _directKeyboard = value;
        }

        /// <inheritdoc/>
        public IFrame MainFrame => GetOrCreateFrame(_frameManager.MainFrame);

        /// <inheritdoc/>
        public IMouse Mouse
        {
            get => _directMouse ??= new WebKitMouse(_mouse, Context);
            set => _directMouse = value;
        }

        /// <inheritdoc/>
        public ITouchscreen Touchscreen
        {
            get => _directTouchscreen ??= new WebKitTouchscreen(_touchscreen);
            set => _directTouchscreen = value;
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
        public string Url => string.IsNullOrEmpty(_mainFrameUrl) ? "about:blank" : _mainFrameUrl;

        /// <inheritdoc/>
        public PageViewportSizeResult ViewportSize => _viewportSize;

        /// <summary>
        /// Official mobile WebKit rejects mouse wheel.
        /// </summary>
        internal bool EmulatesMobile => _emulatedIsMobile;

        /// <summary>
        /// Gets the per-page WIP page-proxy session.
        /// </summary>
        internal WKSession Session => _session;

        /// <summary>Raw keyboard used by pointer actions for modifier snapshots.</summary>
        internal Input.Keyboard InputKeyboard => _keyboard;

        /// <summary>
        /// Gets the inner target session currently driving this page, or <see langword="null"/>
        /// if no target has been created yet. Used by the raw keyboard for <c>Page.insertText</c>.
        /// </summary>
        internal WKTargetSession CurrentTargetSession => _targetSession;

        /// <summary>
        /// Gets the WebKit pageProxyId.
        /// </summary>
        internal string PageProxyId => _pageProxyId;

        /// <summary>
        /// Gets a value indicating whether <see cref="CloseAsync"/> has started.
        /// </summary>
        internal bool IsClosing => _closing;

        /// <summary>
        /// Gets or sets the page that opened this page via <c>window.open</c>, if any.
        /// </summary>
        internal WKPage Opener
        {
            get => _opener;
            set
            {
                _opener = value;
                if (value != null)
                {
                    EmitPopupMainRequestIfNeeded();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the popup's main request was already surfaced.
        /// </summary>
        internal bool PopupMainRequestEmitted { get; set; }

        /// <summary>
        /// Context init scripts / bindings applied to this page (including popups).
        /// </summary>
        internal Task ContextChromeTask { get; set; } = Task.CompletedTask;

        /// <summary>
        /// Viewport from the opener's <c>Playwright.windowOpen</c> features, or
        /// <see langword="null"/> when the popup should inherit the context size.
        /// </summary>
        internal ViewportSize WindowOpenViewport { get; set; }

        /// <summary>
        /// Gets the owning browser.
        /// </summary>
        internal WKBrowser Browser => _browser;

        /// <summary>
        /// Gets the owning WebKit context, or null for an unattached page.
        /// </summary>
        internal WKBrowserContext WKContext => _context;

        /// <summary>
        /// Page-level extra HTTP headers last passed to
        /// <see cref="SetExtraHttpHeadersAsync"/>.
        /// </summary>
        internal IReadOnlyDictionary<string, string> PageExtraHttpHeaders => _extraHttpHeaders;

        /// <summary>
        /// Gets or sets the public <see cref="IBrowserContext"/> that owns this page.
        /// Set by <see cref="WKBrowserContext"/> when it hands the page out.
        /// </summary>
        internal IBrowserContext OwnerContext
        {
            get => _ownerContext;
            set => _ownerContext = value;
        }

        /// <summary>
        /// Official <c>_ownedContext</c>: a context created by <c>browser.newPage</c>
        /// is closed when this page closes.
        /// </summary>
        internal IBrowserContext OwnedContext { get; set; }

        /// <summary>
        /// Gets a task that completes when the inner Target session has been created
        /// and the init sequence (Page.enable / Page.getResourceTree / Runtime.enable /
        /// Network.enable, plus Console.enable on non-frame-session builds) has finished.
        /// </summary>
        internal Task InitializedTask => _initializedTcs.Task;

        /// <summary>
        /// Official <c>reportAsNew</c>: true after the first non-initial navigation.
        /// </summary>
        internal bool HasCommittedNonInitialNavigation => _hasCommittedNonInitialNavigation;

        /// <summary>
        /// Official first non-initial URL from getResourceTree / frameNavigated.
        /// </summary>
        internal Task ReportAsNewNavigationTask => _reportAsNewNavigationTcs.Task;

        /// <summary>
        /// Gets a value indicating whether <see cref="Popup"/> has subscribers
        /// (including <see cref="IPage.WaitForPopupAsync"/>).
        /// </summary>
        internal bool HasPopupListeners => Popup != null;

        /// <summary>
        /// Gets a task that completes when this page is closed.
        /// </summary>
        internal Task ClosedTask => _closedTcs.Task;

        /// <summary>
        /// Gets the URL of the main frame. Updates from <c>Page.frameNavigated</c> and
        /// the initial <c>Page.getResourceTree</c> snapshot.
        /// </summary>
        internal string MainFrameUrl => _mainFrameUrl;

        /// <summary>
        /// The first public document request for the in-flight <c>goto</c>, used to
        /// adopt process-swap duplicates onto one request object.
        /// </summary>
        internal WKRequest FirstPendingNavigationRequest => _firstPendingNavigationRequest;

        /// <inheritdoc/>
        public bool HasExposedFunction(string name)
            => !string.IsNullOrEmpty(name)
            && (_exposedFunctions.ContainsKey(name) || _handleBindings.ContainsKey(name));

        /// <inheritdoc/>
        public async Task<AccessibilitySnapshotResult> SnapshotAccessibilityAsync(bool? interestingOnly = null, IElementHandle root = null)
        {
            await InitializedTask.ConfigureAwait(false);
            return await WKAccessibility.SnapshotAsync(_targetSession, interestingOnly, root).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<string> TitleAsync()
            => PageTitle.ReadAsync(() => EvaluateExpressionAsync<string>("document.title"));

        /// <inheritdoc/>
        public Task<string> ContentAsync()
            => PageContent.ReadAsync(() => EvaluateExpressionAsync<string>(PageContent.EvaluateExpression));

        /// <summary>
        /// Emulates CSS media features on the page via the inner target session. Maps
        /// directly to upstream WebKit's <c>_setEmulateMedia</c>: <c>Page.setEmulatedMedia</c>
        /// sets the media type (<c>screen</c>/<c>print</c>, or empty string to reset) and
        /// <c>Page.overrideUserPreference</c> sets <c>PrefersColorScheme</c>
        /// (<c>Light</c>/<c>Dark</c>, or <c>undefined</c> to reset). Both commands are sent
        /// concurrently, mirroring upstream's <c>Promise.all</c>.
        /// </summary>
        /// <param name="media">The media type to emulate, or <c>null</c>/<see cref="Media.Undefined"/> to reset.</param>
        /// <param name="colorScheme">The <c>prefers-color-scheme</c> value, or <c>null</c>/<see cref="EnumCompat.UndefinedColorScheme"/> to reset.</param>
        /// <returns>A task that completes once both emulation commands have been acknowledged.</returns>
        public Task EmulateMediaAsync(Media? media = null, ColorScheme? colorScheme = null)
        {
            // Official emulateMedia({}) leaves current overrides. C# null is omitted;
            // Media.Undefined / EnumCompat.UndefinedColorScheme reset (media → no-override,
            // colorScheme → default light).
            if (media.HasValue)
            {
                _emulatedMedia = media.Value switch
                {
                    Media.Screen => "screen",
                    Media.Print => "print",
                    _ => string.Empty,
                };
            }

            if (colorScheme.HasValue)
            {
                _emulatedColorScheme = colorScheme.Value switch
                {
                    ColorScheme.Light => "Light",
                    ColorScheme.Dark => "Dark",
                    ColorScheme.NoPreference => null,
                    _ => "Light",
                };
            }

            return ApplyEmulatedMediaAsync();
        }

        /// <summary>
        /// Closes this page via <c>Target.close</c> on the page-proxy session. Server fires
        /// <c>Playwright.pageProxyDestroyed</c> which routes back through
        /// <see cref="WKBrowser"/> to <see cref="DidClose"/>.
        /// </summary>
        /// <param name="runBeforeUnload">Whether to run <c>beforeunload</c> handlers (defaults to false).</param>
        /// <param name="reason">The reason to be reported to operations interrupted by this close.</param>
        /// <returns>A task that completes once the server acknowledges the close.</returns>
        public async Task CloseAsync(bool? runBeforeUnload = null, string reason = null)
        {
            await ActionTrace.RunAsync(Context, "Close page", "Page", "close", async () =>
            {
                if (OwnedContext != null)
                {
                    IBrowserContext owned = OwnedContext;
                    OwnedContext = null;
                    await owned.CloseAsync(reason).ConfigureAwait(false);
                    return;
                }

                ApplyCloseReason(reason);
                bool runUnload = runBeforeUnload ?? false;
                if (!runUnload)
                {
                    _closing = true;
                }

                if (_closed)
                {
                    return;
                }

                // Current official wkPage.closePage uses Playwright.closePage on the
                // browser session. Older Playwright WebKit (this MiniBrowser) only
                // has Target.close on the page-proxy session. runBeforeUnload only
                // requests the dialog and must not wait for DidClose.
                Task closePage = _browser.Session.SendAsync("Playwright.closePage", new
                {
                    pageProxyId = _pageProxyId,
                    runBeforeUnload = runUnload,
                });
                if (runUnload)
                {
                    if (!await TryAwaitCloseCommandAsync(closePage).ConfigureAwait(false))
                    {
                        WKTargetSession unloadTarget = _targetSession;
                        if (unloadTarget != null)
                        {
                            await TryAwaitCloseCommandAsync(_session.SendAsync("Target.close", new
                            {
                                targetId = unloadTarget.TargetId,
                                runBeforeUnload = true,
                            })).ConfigureAwait(false);
                        }
                    }

                    return;
                }

                if (_crashed)
                {
                    _ = closePage;
                    if (!_closed)
                    {
                        DidClose();
                    }

                    return;
                }

                await Task.WhenAny(closePage, _closedTcs.Task).ConfigureAwait(false);
                _ = closePage.Exception;
                if (_closed)
                {
                    return;
                }

                WKTargetSession target = _targetSession;
                if (target != null)
                {
                    Task targetClose = _session.SendAsync("Target.close", new
                    {
                        targetId = target.TargetId,
                        runBeforeUnload = runUnload,
                    });
                    await Task.WhenAny(targetClose, _closedTcs.Task).ConfigureAwait(false);
                    _ = targetClose.Exception;
                }

                if (!_closed)
                {
                    await _closedTcs.Task.ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => PageDispose.RunAsync(this);

        /// <inheritdoc/>
        public async Task<IAsyncDisposable> AddInitScriptAsync(string script = null, string scriptPath = null)
        {
            script = AddInitScriptHelper.Resolve(script, scriptPath);
            await AddInitScriptInternalAsync(script).ConfigureAwait(false);
            return AddInitScriptHelper.CreateDisposable(() => RemoveInitScriptInternalAsync(script));
        }

        /// <inheritdoc/>
        public Task AddInitScriptAsync(string script, object arg)
        {
            script = AddInitScriptHelper.Resolve(script, null, arg);
            return AddInitScriptAsync(script, null);
        }

        /// <inheritdoc/>
        public async Task<IElementHandle> AddScriptTagAsync(string url = null, string path = null, string content = null, string type = null)
        {
            AddScriptTagHelper.Resolved resolved = AddScriptTagHelper.Resolve(url, path, content, type);
            return await AddScriptTagAsync(url: resolved.Url, content: resolved.Content, type: resolved.Type).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IElementHandle> AddStyleTagAsync(string url = null, string path = null, string content = null)
        {
            AddStyleTagHelper.Resolved resolved = AddStyleTagHelper.Resolve(url, path, content);
            return await AddStyleTagAsync(url: resolved.Url, content: resolved.Content).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task CheckAsync(string selector, Position position = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.CheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "page.check", scroll);

        /// <inheritdoc/>
        public Task ClickAsync(string selector, MouseButton button = default, int? clickCount = null, float? delay = null, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, int? steps = default, bool? strict = default)
            => ActionTrace.RunAsync(
                Context,
                clickCount == 2 ? "Double click " + ActionTrace.LocatorLabel(selector) : ActionTrace.ClickTitle(selector),
                "Page",
                clickCount == 2 ? "dblclick" : "click",
                () => PlaywrightApiLog.RunAsync(
                    _context?.Logger,
                    "page.click",
                    () => ClickAction.RunOnSelectorAsync(sel => QueryActionAsync(sel, strict), selector, h => h.ClickAsync(button, clickCount, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll, steps), timeout, "page.click", scroll)),
                new Dictionary<string, object> { ["selector"] = selector });

        /// <inheritdoc/>
        public Task DblClickAsync(string selector, MouseButton button = default, float? delay = null, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
            => ActionTrace.RunAsync(
                Context,
                "Double click " + ActionTrace.LocatorLabel(selector),
                "Page",
                "dblclick",
                () => ClickAction.RunOnSelectorAsync(sel => QueryActionAsync(sel, strict), selector, h => h.DblClickAsync(button, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "page.dblclick", scroll));

        /// <inheritdoc/>
        public Task EmulateMediaAsync(ColorScheme? colorScheme)
            => EmulateMediaAsync(media: null, colorScheme: colorScheme);

        /// <inheritdoc/>
        public Task EmulateVisionDeficiencyAsync(VisionDeficiency type = default)
            => throw new PlaywrightNativeException("EmulateVisionDeficiencyAsync is Chromium-only.");

        /// <inheritdoc/>
        public Task EmulateMediaAsync(ReducedMotion? reducedMotion = default, ForcedColors? forcedColors = default, Contrast? contrast = default)
        {
            if (reducedMotion.HasValue)
            {
                _emulatedReducedMotion = reducedMotion.Value switch
                {
                    ReducedMotion.Reduce => "Reduce",
                    ReducedMotion.NoPreference => "NoPreference",
                    _ => "NoPreference",
                };
            }

            if (forcedColors.HasValue)
            {
                _emulatedForcedColors = forcedColors.Value switch
                {
                    ForcedColors.Active => "Active",
                    ForcedColors.None => "None",
                    _ => "None",
                };
            }

            if (contrast.HasValue)
            {
                _emulatedContrast = contrast.Value switch
                {
                    Contrast.More => "More",
                    EnumCompat.LessContrast => "Less",
                    Contrast.NoPreference => "NoPreference",
                    _ => "NoPreference",
                };
            }

            return ApplyEmulatedMediaAsync();
        }

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = null)
        {
            ThrowIfClosed();
            return ActionTrace.EvaluateUserAsync(Context, () =>
            {
                if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
                {
                    return EvaluatePreparedAsync<T>(handleFn, handleArgs);
                }

                if (arg is IJSHandle)
                {
                    return EvaluateFunctionSerializedAsync<T>(expression, arg);
                }

                string toEval = arg == null
                    ? EvaluateWithArg.InvokeIfFunction(expression)
                    : EvaluateWithArg.Wrap(expression, arg);
                return EvaluateSerializedAsync<T>(toEval);
            });
        }

        /// <inheritdoc/>
        public Task<JsonElement?> EvaluateAsync(string expression, object arg = null)
        {
            ThrowIfClosed();
            return ActionTrace.EvaluateUserAsync(Context, () =>
            {
                if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
                {
                    return EvaluatePreparedAsync<JsonElement?>(handleFn, handleArgs);
                }

                if (arg is IJSHandle)
                {
                    return EvaluateFunctionSerializedAsync<JsonElement?>(expression, arg);
                }

                string toEval = arg == null
                    ? EvaluateWithArg.InvokeIfFunction(expression)
                    : EvaluateWithArg.Wrap(expression, arg);
                return EvaluateSerializedAsync<JsonElement?>(toEval);
            });
        }

        /// <inheritdoc/>
        public Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = null)
            => ActionTrace.EvaluateHandleUserAsync(Context, async () =>
            {
                WKExecutionContext context = RequireExecutionContext();
                if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
                {
                    await EvaluateHandleArg.StashRemoteHandlesAsync(handleArgs).ConfigureAwait(false);
                    JsonElement? bound = await context
                        .EvaluateFunctionHandleAsync(handleFn, EvaluateHandleArg.TreeArgument(handleArgs))
                        .ConfigureAwait(false);
                    return WrapRemoteObject(context, bound);
                }

                string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
                JsonElement? handleValue = await context.EvaluateHandleAsync(toEval).ConfigureAwait(false);
                return WrapRemoteObject(context, handleValue);
            });

        /// <inheritdoc/>
        public Task ExposeBindingAsync(string name, Action callback, bool? handle = default)
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
        public Task ExposeBindingAsync(string name, Func<BindingSource, IJSHandle, object> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return ExposeHandleBindingInternalAsync(name, handle =>
            {
                object result = callback(PageExposeBinder.Source(Context, this), handle);
                return Task.FromResult(result);
            });
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<BindingSource, IJSHandle, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallHandleExposedAsync(name, handle =>
            {
                TResult result = callback(PageExposeBinder.Source(Context, this), handle);
                return ExposeFunctionBinder.InvokeAsync(result);
            });
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<BindingSource, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.WrapBinding(Context, this, callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<T, TResult>(string name, Func<BindingSource, T, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (typeof(T) == typeof(IJSHandle))
            {
                return InstallHandleExposedAsync(name, handle =>
                {
                    TResult result = callback(PageExposeBinder.Source(Context, this), (T)(object)handle);
                    return ExposeFunctionBinder.InvokeAsync(result);
                });
            }

            return InstallExposedAsync(name, PageExposeBinder.WrapBinding(Context, this, callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<T1, T2, TResult>(string name, Func<BindingSource, T1, T2, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.WrapBinding(Context, this, callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync(string name, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T>(string name, Action<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<TResult>(string name, Func<TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T, TResult>(string name, Func<T, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, TResult>(string name, Func<T1, T2, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.Wrap(callback));
        }

        /// <inheritdoc/>
        public Task FillAsync(string selector, string value, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.FillAsync(value, noWaitAfter, timeout, force, scroll), timeout, "page.fill", scroll);

        /// <inheritdoc/>
        public Task FocusAsync(string selector, float? timeout = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.FocusAsync(timeout, scroll), timeout, "page.focus", scroll);

        /// <summary>Not yet implemented.</summary>
        /// <param name="name">The frame name.</param>
        /// <returns>The frame.</returns>
        public Task<IFrame> FrameAsync(string name)
            => Task.FromResult(FrameLookup.ByName(Frames, name));

        /// <inheritdoc/>
        public IFrame FrameByUrl(string urlString, Regex urlRegex, Func<string, bool> urlFunc)
            => FrameLookup.ByUrl(Frames, urlString, urlRegex, urlFunc);

        /// <inheritdoc/>
        public Task<string> GetAttributeAsync(string selector, string name, float? timeout = null, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.getAttribute(" + JsonSerializer.Serialize(name) + ")",
                timeout,
                "page.getAttribute",
                strict ?? (Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<IResponse> GoToAsync(string url, string waitUntil, float? timeout = default, string referer = default)
            => GoToAsync(url, WaitUntilName.Parse(waitUntil), timeout, referer);

        /// <inheritdoc/>
        public async Task<IResponse> GoToAsync(string url, WaitUntilState waitUntil = default, float? timeout = default, string referer = default)
        {
            if (_crashed)
            {
                throw new PlaywrightNativeException("page.goto: Target crashed");
            }

            url = NavigationTimeout.CompleteUserUrl(NavigationUrl.Resolve(Context, url));
            IResponse result = null;
            await ActionTrace.RunAsync(Context, ActionTrace.NavigateTitle(url), "Page", "goto", async () =>
            {
                referer = NavigationTimeout.ReferrerFromExtraHeaders(referer, _extraHttpHeaders);
                NavigationTimeout.ThrowIfRefererConflict(url, referer, _extraHttpHeaders);
                timeout = NavigationTimeout.ResolveMs(
                    timeout,
                    _defaultNavigationTimeout,
                    _defaultTimeout,
                    Context.DefaultNavigationTimeout(),
                    Context.DefaultTimeout());
                IResponse captured = null;
                IFrame main = MainFrame;
                void OnResponse(object sender, IResponse response)
                {
                    if (response?.Request == null || !response.Request.IsNavigationRequest)
                    {
                        return;
                    }

                    IFrame frame = response.Request.Frame;
                    if (frame != null && main != null && !ReferenceEquals(frame, main) && frame != main)
                    {
                        return;
                    }

                    if (captured != null
                        && captured.Request?.RedirectedFrom != null
                        && response.Request?.RedirectedFrom == null
                        && string.Equals(
                            NavigationTimeout.WithoutHash(captured.Url),
                            NavigationTimeout.WithoutHash(response.Url),
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    captured = response;
                }

                Response += OnResponse;
                try
                {
                    await NavigateAsync(url, waitUntil, timeout, referer).ConfigureAwait(false);
                    DateTime contextDeadline = DateTime.UtcNow.AddSeconds(5);
                    while (_executionContext == null && DateTime.UtcNow < contextDeadline && !_closed)
                    {
                        await Task.Delay(50).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(url)
                        && (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)))
                    {
                        result = null;
                        return;
                    }

                    result = captured ?? FindNavigationResponseForUrl(Url);
                }
                finally
                {
                    Response -= OnResponse;
                }
            }).ConfigureAwait(false);
            return result;
        }

        /// <inheritdoc/>
        public async Task<IResponse> ReloadAsync(WaitUntilState waitUntil = default, float? timeout = default)
        {
            IResponse result = null;
            await ActionTrace.RunAsync(Context, null, "Page", "reload", async () =>
            {
                if (_crashed)
                {
                    throw new PlaywrightNativeException("Target crashed");
                }

                string currentUrl = Url;
                if (HttpBasicAuth.HasCredentials(_context?.HttpCredentialsList)
                    && !string.IsNullOrEmpty(currentUrl)
                    && currentUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    result = await GoToAsync(currentUrl, waitUntil, timeout).ConfigureAwait(false);
                    return;
                }

                IResponse captured = await RunHistoryNavigationAsync(
                    async () =>
                    {
                        WKTargetSession target = _targetSession
                            ?? throw PageClosedException();
                        await target.SendAsync("Page.reload").ConfigureAwait(false);
                        return true;
                    },
                    waitUntil,
                    timeout,
                    allowSameDocument: false).ConfigureAwait(false);

                if ((!string.IsNullOrEmpty(Url) && Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(captured?.Url) && captured.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)))
                {
                    result = null;
                    return;
                }

                result = captured;
            }).ConfigureAwait(false);
            return result;
        }

        /// <inheritdoc/>
        public Task<IResponse> GoBackAsync(WaitUntilState waitUntil = default, float? timeout = default)
            => RunHistoryNavigationAsync(() => TryGoHistoryAsync("Page.goBack"), waitUntil, timeout);

        /// <inheritdoc/>
        public Task<IResponse> GoForwardAsync(WaitUntilState waitUntil = default, float? timeout = default)
            => RunHistoryNavigationAsync(() => TryGoHistoryAsync("Page.goForward"), waitUntil, timeout);

        /// <inheritdoc/>
        public Task BringToFrontAsync()
        {
            WKTargetSession target = _targetSession
                ?? throw PageClosedException();
            return _session.SendAsync("Target.activate", new { targetId = target.TargetId });
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<IConsoleMessage>> ConsoleMessagesAsync(ConsoleMessagesFilter filter = ConsoleMessagesFilter.SinceNavigation)
            => Task.FromResult(_consoleLog.Snapshot(filter));

        /// <inheritdoc/>
        public Task ClearConsoleMessagesAsync()
        {
            _consoleLog.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> PageErrorsAsync(PageErrorsFilter filter = default)
            => Task.FromResult(filter == PageErrorsFilter.All
                ? _pageErrors.Snapshot()
                : _pageErrors.SnapshotAfterNavigation());

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> PageErrorsAsync()
            => PageErrorsAsync(default);

        /// <inheritdoc/>
        public Task ClearPageErrorsAsync()
        {
            _pageErrors.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<IRequest>> RequestsAsync()
            => Task.FromResult(_requests.Snapshot());

        /// <inheritdoc/>
        public Task RequestGCAsync()
        {
            WKTargetSession target = _targetSession
                ?? throw PageClosedException();
            return target.SendAsync("Heap.gc");
        }

        /// <inheritdoc/>
        public Task HoverAsync(string selector, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? force = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
            => ActionTrace.RunAsync(
                Context,
                ActionTrace.HoverTitle(selector),
                "Page",
                "hover",
                () => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.HoverAsync(position, modifiers, force, timeout, trial, scroll), timeout, "page.hover", scroll));

        /// <inheritdoc/>
        public Task<string> InnerHTMLAsync(string selector, float? timeout = null, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.innerHTML",
                timeout,
                "page.innerHTML",
                strict ?? (Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<string> InnerTextAsync(string selector, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitQueryAsync(
                sel => QueryActionAsync(sel, strict),
                selector,
                h => h.InnerTextAsync(),
                timeout,
                "page.innerText");

        /// <inheritdoc/>
        public Task<bool> IsCheckedAsync(string selector, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsCheckedAsync(), timeout, "page.isChecked");

        /// <inheritdoc/>
        public Task<bool> IsDisabledAsync(string selector, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsDisabledAsync(), timeout, "page.isDisabled");

        /// <inheritdoc/>
        public Task<bool> IsEditableAsync(string selector, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsEditableAsync(), timeout, "page.isEditable");

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync(string selector, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsEnabledAsync(), timeout, "page.isEnabled");

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync(string selector, float? timeout = null, bool? strict = default)
            => !await IsVisibleAsync(selector, timeout, strict).ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<bool> IsVisibleAsync(string selector, float? timeout = null, bool? strict = default)
            => AtomicSelectorRead.IsVisibleAsync(expression => EvaluateAsync<JsonElement?>(expression), selector, strict ?? (Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<IPage> OpenerAsync()
            => Task.FromResult<IPage>(_opener != null && _opener.IsClosed ? null : _opener);

        /// <inheritdoc/>
        /// <remarks>
        /// PDF generation is a Headless-Chromium-only feature (CDP <c>Page.printToPDF</c>);
        /// WebKit has no equivalent, so this always throws.
        /// </remarks>
        public Task<byte[]> PdfAsync(string path = default, float? scale = default, bool? displayHeaderFooter = default, string headerTemplate = default, string footerTemplate = default, bool? printBackground = default, bool? landscape = default, string pageRanges = default, string format = default, string width = default, string height = default, Margin margin = default, bool? preferCSSPageSize = default, bool? tagged = default, bool? outline = default)
            => throw new NotSupportedException("PDF generation is only supported for Headless Chromium");

        /// <inheritdoc/>
        public Task PressAsync(string selector, string key, float? delay = null, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.PressAsync(key, delay, noWaitAfter, timeout, force, scroll), timeout, "page.press", scroll);

        /// <inheritdoc/>
        public Task<IElementHandle> QuerySelectorAsync(string selector)
            => QuerySelectorInFrameAsync(_frameManager.MainFrame, selector);

        /// <inheritdoc/>
        public Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
            => QuerySelectorAllInFrameAsync(_frameManager.MainFrame, selector);

        /// <inheritdoc/>
        public Task<IElementHandle> GetByRoleAsync(string role, string name = null, bool? exact = null, float? timeout = null, bool? checkedState = null, bool? disabled = null, bool? expanded = null, bool? includeHidden = null, int? level = null, bool? pressed = null, bool? selected = null, string description = null, Regex descriptionRegex = null, Regex nameRegex = null)
            => GetByWaiter.WaitAsync(() => QueryByScriptAsync(GetBySelectorScript.FindByRole, role, name, exact ?? false, GetBySelectorScript.RoleOptions(checkedState, disabled, expanded, includeHidden, level, pressed, selected, description, descriptionRegex, nameRegex)), timeout, "page.getByRole");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByTextAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(() => QueryByScriptAsync(GetBySelectorScript.FindByText, text, exact ?? false), timeout, "page.getByText");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByLabelAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(() => QueryByScriptAsync(GetBySelectorScript.FindByLabel, text, exact ?? false), timeout, "page.getByLabel");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByPlaceholderAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(() => QueryByScriptAsync(GetBySelectorScript.FindByPlaceholder, text, exact ?? false), timeout, "page.getByPlaceholder");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByAltTextAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(() => QueryByScriptAsync(GetBySelectorScript.FindByAltText, text, exact ?? false), timeout, "page.getByAltText");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByTitleAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(() => QueryByScriptAsync(GetBySelectorScript.FindByTitle, text, exact ?? false), timeout, "page.getByTitle");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByTestIdAsync(string testId, float? timeout = null)
            => GetByWaiter.WaitAsync(() => QuerySelectorAsync(GetBySelectorScript.TestIdSelector(testId)), timeout, "page.getByTestId");

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

        /// <inheritdoc/>
        public Task<byte[]> ScreenshotAsync(string path = null, ScreenshotType type = default, int? quality = null, bool? fullPage = null, Clip clip = null, bool? omitBackground = null, float? timeout = null, string scale = null, string animations = null, string caret = null, string style = null, IEnumerable<ILocator> mask = default, string maskColor = default)
        {
            type = ScreenshotValidate.ResolveType(path, type);
            ScreenshotValidate.EnsureQuality(type, quality);
            ScreenshotValidate.EnsureClip(clip, fullPage ?? false, _viewportSize);

            async Task<byte[]> CaptureAsync()
            {
                return await ScreenshotDecorations.CaptureAsync(this, animations, caret, style, CaptureRectAsync, mask, maskColor).ConfigureAwait(false);
            }

            async Task<byte[]> CaptureRectAsync()
            {
                WKTargetSession target = _targetSession
                    ?? throw new PlaywrightNativeException("Cannot take a screenshot: the page has no active target session.");

                bool captureFullPage = fullPage ?? false;
                bool hideBackground = omitBackground == true;
                int? resolvedQuality = ScreenshotValidate.ResolvedQuality(type, quality);
                bool cssScale = ScreenshotScaleHelper.IsCss(scale);

                int x = 0;
                int y = 0;
                int width;
                int height;
                if (clip != null)
                {
                    x = (int)clip.X;
                    y = (int)clip.Y;
                    width = (int)clip.Width;
                    height = (int)clip.Height;
                }
                else
                {
                    // WebKit's Page.snapshotRect needs an explicit rect: the layout viewport for a
                    // viewport shot ('Viewport' coordinates), or the full scrollable document for a
                    // full-page shot ('Page' coordinates). Mirrors upstream screenshotter._fullPageSize
                    // and wkPage.takeScreenshot.
                    string sizeExpression = captureFullPage
                        ? "[Math.max(document.body.scrollWidth, document.documentElement.scrollWidth, document.body.offsetWidth, document.documentElement.offsetWidth, document.body.clientWidth, document.documentElement.clientWidth), Math.max(document.body.scrollHeight, document.documentElement.scrollHeight, document.body.offsetHeight, document.documentElement.offsetHeight, document.body.clientHeight, document.documentElement.clientHeight)]"
                        : "[window.innerWidth, window.innerHeight]";

                    const string navigating = "Cannot take a screenshot while page is navigating";
                    int[] size = null;
                    for (int attempt = 0; ; attempt++)
                    {
                        try
                        {
                            size = await EvaluateExpressionAsync<int[]>(sizeExpression).ConfigureAwait(false);
                            if (size != null && size.Length >= 2 && size[0] > 0 && size[1] > 0)
                            {
                                break;
                            }
                        }
                        catch (PlaywrightNativeException ex) when (
                            ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                            || ex.Message.Contains("most likely because of a navigation", StringComparison.Ordinal)
                            || ex.Message.Contains("Missing injected script", StringComparison.Ordinal)
                            || ex.Message.Contains(navigating, StringComparison.Ordinal))
                        {
                        }

                        if (attempt >= 20)
                        {
                            throw new PlaywrightNativeException(navigating);
                        }

                        await Task.Delay(50).ConfigureAwait(false);
                    }

                    width = size[0];
                    height = size[1];
                }

                ScreenshotEncode.EnsureDimension(width, _emulatedDeviceScaleFactor, cssScale);
                ScreenshotEncode.EnsureDimension(height, _emulatedDeviceScaleFactor, cssScale);

                if (hideBackground)
                {
                    await target.SendAsync("Page.setDefaultBackgroundColorOverride", new
                    {
                        color = new { r = 0, g = 0, b = 0, a = 0 },
                    }).ConfigureAwait(false);
                }

                JsonElement? response;
                try
                {
                    response = await target.SendAsync("Page.snapshotRect", new
                    {
                        x,
                        y,
                        width,
                        height,
                        coordinateSystem = captureFullPage ? "Page" : "Viewport",
                        omitDeviceScaleFactor = cssScale,
                        format = ScreenshotFormat.ToProtocol(type),
                        quality = resolvedQuality,
                    }).ConfigureAwait(false);
                }
                finally
                {
                    if (hideBackground)
                    {
                        await target.SendAsync("Page.setDefaultBackgroundColorOverride").ConfigureAwait(false);
                    }
                }

                if (!response.HasValue || !response.Value.TryGetProperty("dataURL", out JsonElement dataUrlElement))
                {
                    throw new PlaywrightNativeException("Page.snapshotRect returned no data.");
                }

                byte[] bytes = ScreenshotEncode.RecodeIfNeeded(
                    ScreenshotEncode.FromDataUrl(dataUrlElement.GetString()),
                    type,
                    resolvedQuality);

                if (!string.IsNullOrEmpty(path))
                {
                    PathIo.WriteBytes(path, bytes);
                }

                return bytes;
            }

            return ActionTrace.RunAsync(
                Context,
                null,
                "Page",
                "screenshot",
                () => ScreenshotTimeout.RunAsync(timeout, CaptureAsync),
                result: new Dictionary<string, object> { ["binary"] = "<Buffer>" });
        }

        /// <summary>
        /// Official element screenshot: <c>Page.snapshotRect</c> in page coordinates.
        /// </summary>
        /// <param name="documentClip">Clip in document CSS pixels.</param>
        /// <param name="path">Optional output path.</param>
        /// <param name="type">Image format.</param>
        /// <param name="quality">JPEG quality.</param>
        /// <param name="omitBackground">Hide the default background.</param>
        /// <param name="scale">CSS vs device scale.</param>
        /// <returns>The image bytes.</returns>
        public async Task<byte[]> ScreenshotDocumentClipAsync(
            Clip documentClip,
            string path,
            ScreenshotType type,
            int? quality,
            bool? omitBackground,
            string scale)
        {
            type = ScreenshotValidate.ResolveType(path, type);
            ScreenshotValidate.EnsureQuality(type, quality);
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot take a screenshot: the page has no active target session.");

            int? resolvedQuality = ScreenshotValidate.ResolvedQuality(type, quality);
            bool cssScale = ScreenshotScaleHelper.IsCss(scale);
            int width = (int)documentClip.Width;
            int height = (int)documentClip.Height;
            ScreenshotEncode.EnsureDimension(width, _emulatedDeviceScaleFactor, cssScale);
            ScreenshotEncode.EnsureDimension(height, _emulatedDeviceScaleFactor, cssScale);

            bool hideBackground = omitBackground == true;
            if (hideBackground)
            {
                await target.SendAsync("Page.setDefaultBackgroundColorOverride", new
                {
                    color = new { r = 0, g = 0, b = 0, a = 0 },
                }).ConfigureAwait(false);
            }

            JsonElement? response;
            try
            {
                response = await target.SendAsync("Page.snapshotRect", new
                {
                    x = (int)documentClip.X,
                    y = (int)documentClip.Y,
                    width,
                    height,
                    coordinateSystem = "Page",
                    omitDeviceScaleFactor = cssScale,
                    format = ScreenshotFormat.ToProtocol(type),
                    quality = resolvedQuality,
                }).ConfigureAwait(false);
            }
            finally
            {
                if (hideBackground)
                {
                    await target.SendAsync("Page.setDefaultBackgroundColorOverride").ConfigureAwait(false);
                }
            }

            if (!response.HasValue || !response.Value.TryGetProperty("dataURL", out JsonElement dataUrlElement))
            {
                throw new PlaywrightNativeException("Page.snapshotRect returned no data.");
            }

            byte[] bytes = ScreenshotEncode.RecodeIfNeeded(
                ScreenshotEncode.FromDataUrl(dataUrlElement.GetString()),
                type,
                resolvedQuality);

            if (!string.IsNullOrEmpty(path))
            {
                PathIo.WriteBytes(path, bytes);
            }

            return bytes;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force, scroll), timeout, "page.selectOption", scroll);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, string values, bool? noWaitAfter = null, float? timeout = null, bool? force = null, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<string> values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(Array.Empty<string>(), noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IElementHandle values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, SelectOptionValue values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params string[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QueryActionAsync, selector, h => h.SelectOptionAsync(values), null, "page.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params SelectOptionValue[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QueryActionAsync, selector, h => h.SelectOptionAsync(values), null, "page.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params IElementHandle[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QueryActionAsync, selector, h => h.SelectOptionAsync(values), null, "page.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task SetContentAsync(string html, float? timeout = default, WaitUntilState waitUntil = default)
            => ActionTrace.RunAsync(
                Context,
                "Set content",
                "Page",
                "setContent",
                () => PlaywrightApiLog.RunAsync(
                    _context?.Logger,
                    "page.setContent",
                    () => SetContentInternalAsync(html, timeout, waitUntil)));

        /// <summary>Sets <see cref="DefaultNavigationTimeout"/>.</summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>A completed task.</returns>
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
            await ApplyMergedExtraHttpHeadersAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ApplyMergedExtraHttpHeadersAsync()
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot set extra HTTP headers: the page has no active target session.");
            await ApplyExtraHttpHeadersOnAsync(target).ConfigureAwait(false);
            if (_provisionalSession != null && !ReferenceEquals(_provisionalSession, target))
            {
                await ApplyExtraHttpHeadersOnAsync(_provisionalSession).ConfigureAwait(false);
            }

            await ApplyWorkerExtraHeadersAsync().ConfigureAwait(false);
            _context?.UpdateHandshakeExtraHeaders(_extraHttpHeaders);
            if (_networkManager != null)
            {
                await _networkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }

            if (_provisionalNetworkManager != null)
            {
                await _provisionalNetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, string files, bool? noWaitAfter = null, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<string> files, bool? noWaitAfter = null, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, FilePayload files, bool? noWaitAfter = null, float? timeout = null, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout, force, scroll), timeout, "page.setInputFiles", scroll);

        /// <inheritdoc/>
        public async Task SetViewportSizeAsync(int width, int height)
        {
            await SetEmulatedViewportAsync(
                width,
                height,
                _emulatedDeviceScaleFactor,
                _emulatedIsMobile,
                _independentScreen ?? new ScreenSize { Width = width, Height = height },
                rememberIndependentScreen: false).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task TapAsync(string selector, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? noWaitAfter = null, bool? force = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
        {
            TapSupport.ThrowIfDisabled(Context);
            return ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.TapAsync(position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "page.tap", scroll);
        }

        /// <inheritdoc/>
        public Task<string> TextContentAsync(string selector, float? timeout = null, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.textContent",
                timeout,
                "page.textContent",
                strict ?? (Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task TypeAsync(string selector, string text, float? delay = null, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.TypeAsync(text, delay, noWaitAfter, timeout, force, scroll), timeout, "page.type", scroll);

        /// <inheritdoc/>
        public Task UncheckAsync(string selector, Position position = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.UncheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "page.uncheck", scroll);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(urlString, null, null, handler, contextRoute: false, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(urlString, null, null, handler, contextRoute: false, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, urlRegex, null, handler, contextRoute: false, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, urlRegex, null, handler, contextRoute: false, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, null, urlFunc, handler, contextRoute: false, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(null, null, urlFunc, handler, contextRoute: false, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => UnrouteInternalAsync(urlString, urlRegex, urlFunc, handler, contextRoute: false, behavior);

        /// <inheritdoc/>
        public async Task UnrouteAllAsync(UnrouteBehavior behavior = default)
        {
            await ClearRoutesAsync(contextRoute: false, behavior).ConfigureAwait(false);
            await WebSocketRouter.UnrouteAllAsync(this).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task WaitForLoadStateAsync(string state, float? timeout = default)
            => WaitForLoadStateAsync(LoadStateName.Parse(state), timeout);

        /// <inheritdoc/>
        public async Task WaitForLoadStateAsync(LoadState state = LoadState.Load, float? timeout = default)
        {
            // about:blank newPage can finish load before Page.loadEventFired is
            // subscribed. Seed from document.readyState so waitForLoadState
            // resolves immediately when the document is already complete.
            await LoadStateSeed.TryFromDocumentAsync(this, RecordLifecycle).ConfigureAwait(false);
            await LifecycleWaiter.WaitAsync(
                SnapshotLifecycle,
                handler => LifecycleChanged += handler,
                handler => LifecycleChanged -= handler,
                state,
                timeout).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<IJSHandle> WaitForFunctionAsync(string expression, object arg = default, float? pollingInterval = default, float? timeout = default)
        {
            return WaitForFunctionHelper.WaitAsync(
                async wrapped =>
                {
                    bool truthy = await EvaluateExpressionAsync<bool>("(async () => !!(await Promise.resolve(" + wrapped + ")))()").ConfigureAwait(false);
                    if (!truthy)
                    {
                        return null;
                    }

                    WKExecutionContext context = RequireExecutionContext();
                    JsonElement? remote = await context.EvaluateHandleAsync(wrapped).ConfigureAwait(false);
                    IJSHandle handle = WrapWKHandle(context, remote);
                    return handle;
                },
                expression,
                pollingInterval,
                timeout,
                () => EvaluateExpressionAsync("new Promise(r => requestAnimationFrame(() => r(true)))"));
        }

        /// <inheritdoc/>
        public Task RemoveAllListenersAsync(string type = null, RemoveAllListenersBehavior behavior = default)
            => _pageListeners.RemoveAllListenersAsync(type, behavior);

        /// <inheritdoc/>
        public Task WaitForTimeoutAsync(float timeout)
            => ActionTrace.RunAsync(Context, "Wait for timeout", "Page", "waitForTimeout", () => Task.Delay((int)timeout));

        /// <inheritdoc/>
        public Task WaitForURLAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, float? timeout = default, WaitUntilState waitUntil = default)
            => WaitForUrlHelper.WaitAsync(
                () => Url,
                WaitForLoadStateAsync,
                urlString,
                urlRegex,
                urlFunc,
                timeout,
                waitUntil,
                baseUrl: NavigationUrl.ContextBase(Context));

        /// <inheritdoc/>
        public Task<IResponse> WaitForNavigationAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, float? timeout = default, WaitUntilState waitUntil = default)
            => WaitForNavigationHelper.WaitAsync(
                this,
                WaitForLoadStateAsync,
                urlString,
                urlRegex,
                urlFunc,
                timeout,
                waitUntil);

        /// <inheritdoc/>
        public Task<IElementHandle> WaitForSelectorAsync(
            string selector,
            WaitForSelectorState state = WaitForSelectorState.Visible,
            float? timeout = default,
            bool? strict = default,
            string waitFor = default,
            string visibility = default)
        {
            WaitForSelectorName.Validate(waitFor, visibility);
            return WaitForSelectorHelper.WaitAsync(sel => QueryActionAsync(sel, strict), selector, state, timeout);
        }

        /// <inheritdoc/>
        public Task<IElementHandle> WaitForSelectorAsync(string selector, string state, float? timeout = default, bool? strict = default)
            => WaitForSelectorAsync(selector, WaitForSelectorName.Parse(state), timeout, strict);

        /// <inheritdoc/>
        public Task<IElementHandle> WaitForSelectorAsync(string selector, bool state, float? timeout = default, bool? strict = default)
            => WaitForSelectorAsync(selector, WaitForSelectorName.Parse(state), timeout, strict);

        /// <inheritdoc/>
        public Task<IRequest> WaitForRequestAsync(string urlString, Regex urlRegex, Func<IRequest, bool> predicate, float? timeout = default)
            => WaitForEventHelper.WaitAsync<IRequest>(
                h => Request += h,
                h => Request -= h,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, NavigationUrl.ContextBase(Context)),
                timeout ?? DefaultTimeout,
                "page.waitForRequest",
                waitingLog: WaitForEventHelper.RequestWaitingLog(urlString, urlRegex),
                abortOnPageClose: this,
                abortOnPageCrash: true);

        /// <inheritdoc/>
        public Task<IResponse> WaitForResponseAsync(string urlString, Regex urlRegex, Func<IResponse, bool> predicate, float? timeout = default)
            => WaitForEventHelper.WaitAsync<IResponse>(
                h => Response += h,
                h => Response -= h,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, NavigationUrl.ContextBase(Context)),
                timeout ?? DefaultTimeout,
                "page.waitForResponse",
                waitingLog: WaitForEventHelper.ResponseWaitingLog(urlString, urlRegex),
                abortOnPageClose: this,
                abortOnPageCrash: true);

        /// <inheritdoc/>
        public Task<IDownload> WaitForDownloadAsync(float? timeout = default)
            => WaitForEventHelper.WaitAsync<IDownload>(
                h => Download += h,
                h => Download -= h,
                _ => true,
                timeout,
                "page.waitForDownload");

        /// <inheritdoc/>
        public Task<IFileChooser> WaitForFileChooserAsync(float? timeout = default, CancellationToken cancellationToken = default)
            => FileChooserWaitHelper.WaitAsync(this, timeout, cancellationToken);

        /// <inheritdoc/>
        public Task<T> WaitForEventAsync<T>(PlaywrightEvent<T> pageEvent, Func<T, bool> predicate = null, float? timeout = null)
            => PageWaitForEventHelper.WaitAsync(this, pageEvent, predicate, timeout);

        /// <summary>Not yet implemented.</summary>
        /// <param name="source">The source selector.</param>
        /// <param name="target">The target selector.</param>
        /// <param name="sourcePosition">The source position.</param>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="force">Whether to bypass actionability checks.</param>
        /// <param name="noWaitAfter">Whether to skip waiting for navigations.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="trial">When <see langword="true"/>, skip the mouse drag.</param>
        /// <param name="steps">Intermediate mouse-move segments. Defaults to 1.</param>
        /// <param name="scroll">When <see cref="ActionScroll.None"/>, skip scrolling into view.</param>
        /// <param name="strict">
        /// When set, both selectors honor official <c>page.dragAndDrop({ strict })</c>.
        /// </param>
        /// <returns>A task.</returns>
        public Task DragAndDropAsync(
            string source,
            string target,
            Position sourcePosition = default,
            Position targetPosition = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            int? steps = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => DragAndDropHelper.RunAsync(this, source, target, sourcePosition, targetPosition, force, timeout, trial, steps, scroll, strict);

        /// <summary>
        /// Fires <see cref="Popup"/> for a page opened from this page.
        /// </summary>
        /// <param name="popup">The new page.</param>
        internal void FirePopupOpened(WKPage popup)
            => PopupOpenedHelper.EmitWhenReady(popup, popup.PrepareForPopupReportAsync(), ready => Popup?.Invoke(this, ready));

        /// <summary>
        /// Marks this page as reported on the context <c>page</c> event.
        /// </summary>
        /// <returns><see langword="true"/> when this is the first report.</returns>
        internal bool TryMarkReportedAsNew()
            => Interlocked.Exchange(ref _reportedAsNew, 1) == 0;

        /// <summary>
        /// Official <c>reportAsNew</c> for popups: wait until init finishes, and if
        /// the main frame is still empty wait for the first real URL.
        /// </summary>
        /// <returns>A task that completes when the popup can be reported.</returns>
        internal async Task PrepareForPopupReportAsync()
        {
            try
            {
                await InitializedTask.ConfigureAwait(false);
                await ContextChromeTask.ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                return;
            }

            if (_closed || !PopupOpenedHelper.IsBlankUrl(_mainFrameUrl))
            {
                return;
            }

            await Task.WhenAny(
                    _firstNonInitialNavigationTcs.Task,
                    _closedTcs.Task,
                    Task.Delay(5_000))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Pushes context HTTP credentials to every network manager.
        /// </summary>
        /// <param name="httpCredentials">Configured credentials.</param>
        internal void SetHttpCredentials(IReadOnlyList<HttpCredentials> httpCredentials)
        {
            _networkManager?.SetHttpCredentials(httpCredentials);
            _provisionalNetworkManager?.SetHttpCredentials(httpCredentials);
        }

        /// <summary>
        /// Stores the context locale so intercepted requests can send a default
        /// <c>Accept-Language</c> without overriding a user <c>fetch</c> header.
        /// </summary>
        /// <param name="locale">BCP 47 locale, or <see langword="null"/>.</param>
        internal void SetLocale(string locale)
        {
            _networkManager?.SetLocale(locale);
            _provisionalNetworkManager?.SetLocale(locale);
        }

        /// <summary>
        /// Enables interception so locale <c>Accept-Language</c> can be applied
        /// as a default that user <c>fetch</c> headers still override.
        /// </summary>
        /// <returns>A task that completes when interception has been updated.</returns>
        internal async Task UpdateLocaleInterceptionAsync()
        {
            List<Task> tasks = new();
            if (_networkManager != null)
            {
                tasks.Add(_networkManager.UpdateInterceptionAsync());
            }

            if (_provisionalNetworkManager != null)
            {
                tasks.Add(_provisionalNetworkManager.UpdateInterceptionAsync());
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            await ApplyWorkerExtraHeadersAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Official <c>Emulation.setAuthCredentials</c> on the page-proxy session.
        /// This MiniBrowser only honors a single triple; real credentials are
        /// applied per request via interception so a page-set Authorization
        /// header is not overwritten. Empty strings cancel the HTTP auth dialog
        /// so a 401 completes instead of hanging.
        /// </summary>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task ApplyAuthCredentialsAsync()
            => _session.SendAsync("Emulation.setAuthCredentials", new
            {
                username = string.Empty,
                password = string.Empty,
                origin = string.Empty,
            });

        /// <summary>
        /// Enables request interception so credentials and 401s are handled.
        /// </summary>
        /// <returns>A task that completes when interception is enabled.</returns>
        internal Task UpdateNetworkInterceptionAsync()
            => UpdateAllInterceptionAsync();

        /// <summary>
        /// Fires <see cref="WebSocket"/> for a socket opened from this page.
        /// </summary>
        /// <param name="socket">The socket.</param>
        internal void OnWebSocketCreated(IWebSocket socket) => WebSocket?.Invoke(this, socket);

        /// <summary>
        /// Handles <c>Playwright.downloadCreated</c> for this page.
        /// </summary>
        /// <param name="uuid">Download identifier.</param>
        /// <param name="url">Download URL.</param>
        internal void OnDownloadCreated(string uuid, string url)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return;
            }

            WKPage target = _opener != null && !_hasCommittedNonInitialNavigation ? _opener : this;
            bool acceptDownloads = _context?.AcceptDownloads != false;
            PageDownload download = new PageDownload(
                target,
                url,
                suggestedFilename: null,
                _context?.DownloadsPath,
                uuid,
                () => CancelDownloadAsync(uuid),
                acceptDownloads);
            if (!target._downloads.TryAdd(uuid, download))
            {
                return;
            }

            // Official: abort the navigation that turned into a download, and
            // delay the Download event until Playwright.downloadFilenameSuggested.
            FailPendingWithReason("Download is starting", url);
        }

        /// <summary>
        /// Handles <c>Playwright.downloadFilenameSuggested</c>.
        /// </summary>
        /// <param name="uuid">Download identifier.</param>
        /// <param name="suggestedFilename">Suggested file name.</param>
        internal void OnDownloadFilenameSuggested(string uuid, string suggestedFilename)
        {
            if (string.IsNullOrEmpty(uuid) || !_downloads.TryGetValue(uuid, out PageDownload download))
            {
                return;
            }

            download.SetSuggestedFilename(suggestedFilename);
            if (download.TryMarkEventFired())
            {
                Download?.Invoke(this, download);
            }
        }

        /// <summary>
        /// Handles <c>Playwright.downloadFinished</c>.
        /// </summary>
        /// <param name="uuid">Download identifier.</param>
        /// <param name="error">Failure message, or <see langword="null"/> on success.</param>
        internal void OnDownloadFinished(string uuid, string error)
        {
            if (string.IsNullOrEmpty(uuid) || !_downloads.TryGetValue(uuid, out PageDownload download))
            {
                return;
            }

            if (string.IsNullOrEmpty(error))
            {
                download.MarkCompleted();
            }
            else
            {
                download.MarkFailed(error);
            }
        }

        /// <summary>
        /// Completes in-flight downloads when the context or browser closes.
        /// </summary>
        /// <param name="error">Official failure text (<c>canceled</c> or the close reason).</param>
        internal void AbortDownloads(string error)
        {
            foreach (PageDownload download in _downloads.Values)
            {
                download.MarkFailed(error);
            }
        }

        /// <summary>
        /// Official <c>artifact.deleteOnContextClose</c> for each download.
        /// </summary>
        internal void DeleteDownloadFiles()
        {
            foreach (PageDownload download in _downloads.Values)
            {
                download.DeleteOnContextClose();
            }
        }

        /// <summary>
        /// Completes in-flight downloads and cancels them in WebKit.
        /// </summary>
        /// <param name="error">Official failure text.</param>
        /// <returns>A task that completes when cancel commands have been issued.</returns>
        internal async Task AbortDownloadsAsync(string error)
        {
            List<Task> cancels = new();
            foreach (PageDownload download in _downloads.Values)
            {
                download.MarkFailed(error);
                cancels.Add(download.CancelAsync());
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

        /// <summary>
        /// Cancels an in-progress download via <c>Playwright.cancelDownload</c>.
        /// </summary>
        /// <param name="uuid">WebKit download identifier.</param>
        /// <returns>A task that completes when the cancel has been issued.</returns>
        internal Task CancelDownloadAsync(string uuid)
            => _browser.Session.SendAsync("Playwright.cancelDownload", new { uuid });

        /// <summary>
        /// Returns the cached instance for <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The WebKit frame.</param>
        /// <returns>The public instance, or <see langword="null"/>.</returns>
        internal WebKitFrame GetOrCreateFrame(WKFrame frame)
        {
            if (frame == null)
            {
                return null;
            }

            return _directFrames.GetOrAdd(frame, f => new WebKitFrame(f, this));
        }

        /// <summary>
        /// Resolves a protocol frame id to the public instance, falling back to the main frame.
        /// </summary>
        /// <param name="frameId">The protocol frame id.</param>
        /// <returns>The matching frame instance.</returns>
        internal IFrame GetOrCreateFrameById(string frameId)
        {
            WKFrame frame = _frameManager.FrameById(frameId);
            return GetOrCreateFrame(frame ?? _frameManager.MainFrame);
        }

        /// <summary>
        /// Resolves a protocol frame id to the public instance, or <see langword="null"/>
        /// when the id is missing or unknown.
        /// </summary>
        /// <param name="frameId">The protocol frame id.</param>
        /// <returns>The matching frame, or <see langword="null"/>.</returns>
        internal IFrame TryGetFrameById(string frameId)
        {
            if (string.IsNullOrEmpty(frameId))
            {
                return null;
            }

            WKFrame frame = _frameManager.FrameById(frameId);
            return frame == null ? null : GetOrCreateFrame(frame);
        }

        /// <summary>
        /// Resolves a protocol owner-frame id on this page, or another page in
        /// the same context when the node was adopted.
        /// </summary>
        /// <param name="frameId">The protocol frame id.</param>
        /// <returns>The public frame, or <see langword="null"/>.</returns>
        internal IFrame ResolveOwnerFrameById(string frameId)
        {
            IFrame local = TryGetFrameById(frameId);
            if (local != null)
            {
                return local;
            }

            if (_context == null)
            {
                return null;
            }

            foreach (WKPage page in _context.WKPages)
            {
                if (ReferenceEquals(page, this))
                {
                    continue;
                }

                IFrame found = page.TryGetFrameById(frameId);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads content/owner frame ids from <c>DOM.describeNode</c> for <paramref name="objectId"/>.
        /// WebKit reports <c>contentFrameId</c> / <c>ownerFrameId</c> at the top level;
        /// a Chromium-shaped <c>node.frameId</c> is accepted as a fallback.
        /// </summary>
        /// <param name="objectId">The WIP remote object id.</param>
        /// <returns>The described frame ids, or <see langword="null"/>s when unknown.</returns>
        internal async Task<(string ContentFrameId, string OwnerFrameId)> DescribeNodeFrameIdsAsync(string objectId)
        {
            if (string.IsNullOrEmpty(objectId) || _targetSession == null)
            {
                return (null, null);
            }

            JsonElement? described;
            try
            {
                described = await _targetSession.SendAsync("DOM.describeNode", new { objectId }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return (null, null);
            }

            if (described == null)
            {
                return (null, null);
            }

            JsonElement payload = described.Value;
            string contentFrameId = payload.TryGetProperty("contentFrameId", out JsonElement contentEl)
                && contentEl.ValueKind == JsonValueKind.String
                ? contentEl.GetString()
                : null;
            string ownerFrameId = payload.TryGetProperty("ownerFrameId", out JsonElement ownerEl)
                && ownerEl.ValueKind == JsonValueKind.String
                ? ownerEl.GetString()
                : null;
            if (string.IsNullOrEmpty(contentFrameId)
                && payload.TryGetProperty("node", out JsonElement node)
                && node.TryGetProperty("frameId", out JsonElement frameIdEl)
                && frameIdEl.ValueKind == JsonValueKind.String)
            {
                contentFrameId = frameIdEl.GetString();
            }

            return (contentFrameId, ownerFrameId);
        }

        /// <summary>
        /// Returns the hosting <c>iframe</c>/<c>frame</c> element via
        /// <c>DOM.resolveNode</c> with the child <c>frameId</c>. Works for closed
        /// and declarative shadow roots.
        /// </summary>
        /// <param name="frame">The child frame.</param>
        /// <returns>The hosting element handle.</returns>
        internal async Task<IElementHandle> GetFrameElementAsync(WKFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            WKFrame parent = frame.ParentFrame;
            if (parent == null || frame.IsDetached || _targetSession == null)
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            WKExecutionContext context = await WaitForFrameContextAsync(parent).ConfigureAwait(false);
            JsonElement? result;
            try
            {
                result = await _targetSession.SendAsync("DOM.resolveNode", new
                {
                    frameId = frame.FrameId,
                    executionContextId = context.ContextId,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex)
            {
                if (ex.Message.Contains("detached", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PlaywrightNativeException("Frame has been detached.");
                }

                throw;
            }

            parent = frame.ParentFrame;
            if (parent == null || frame.IsDetached)
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            if (result == null || !result.Value.TryGetProperty("object", out JsonElement remote))
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            if (remote.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.ValueKind == JsonValueKind.String
                && string.Equals(subtype.GetString(), "null", StringComparison.Ordinal))
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            IElementHandle handle = WrapElement(context, remote);
            if (handle == null)
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            return handle;
        }

        /// <summary>
        /// Copies context-level routes onto a newly attached page (including
        /// popups) before the first document request is intercepted.
        /// </summary>
        /// <param name="entries">Context route registrations.</param>
        internal void SeedContextRoutes(IReadOnlyList<WKRouteEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            if (_networkManager == null)
            {
                lock (_pendingRoutes)
                {
                    foreach (WKRouteEntry entry in entries)
                    {
                        if (!_pendingRoutes.Contains(entry))
                        {
                            _pendingRoutes.Add(entry);
                        }
                    }
                }

                return;
            }

            foreach (WKRouteEntry entry in entries)
            {
                _networkManager.AddRoute(entry);
                _provisionalNetworkManager?.AddRoute(entry);
            }

            _ = UpdateAllInterceptionAsync();
        }

        /// <summary>
        /// Registers a route on this page (page-level or inherited context-level).
        /// </summary>
        /// <param name="entry">The route registration.</param>
        /// <returns>A task that completes when interception has been updated.</returns>
        internal Task AddRouteAsync(WKRouteEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (_networkManager == null)
            {
                lock (_pendingRoutes)
                {
                    if (!_pendingRoutes.Contains(entry))
                    {
                        _pendingRoutes.Add(entry);
                    }
                }

                return Task.CompletedTask;
            }

            _networkManager.AddRoute(entry);
            _provisionalNetworkManager?.AddRoute(entry);
            Task main = _networkManager.UpdateInterceptionAsync();
            Task provisional = _provisionalNetworkManager != null
                ? _provisionalNetworkManager.UpdateInterceptionAsync()
                : Task.CompletedTask;
            return Task.WhenAll(main, provisional);
        }

        /// <summary>
        /// Removes a specific route registration by reference.
        /// </summary>
        /// <param name="entry">The route to remove.</param>
        internal void RemoveRouteEntry(WKRouteEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            lock (_pendingRoutes)
            {
                _pendingRoutes.Remove(entry);
            }

            _networkManager?.RemoveEntry(entry);
            _provisionalNetworkManager?.RemoveEntry(entry);
        }

        /// <summary>
        /// Overrides the User-Agent string via WebKit <c>Page.overrideUserAgent</c>
        /// on the inner target session.
        /// </summary>
        /// <param name="userAgent">The UA string to set.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetUserAgentAsync(string userAgent)
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot override the user agent: the page has no active target session.");

            return target.SendAsync("Page.overrideUserAgent", new { value = userAgent ?? string.Empty });
        }

        /// <summary>
        /// Overrides the timezone via WebKit <c>Page.setTimeZone</c>.
        /// </summary>
        /// <param name="timezoneId">IANA timezone id, for example <c>Europe/Paris</c>.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal async Task SetTimezoneAsync(string timezoneId)
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot override the timezone: the page has no active target session.");

            try
            {
                await target.SendAsync("Page.setTimeZone", new { timeZone = timezoneId ?? string.Empty }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (
                ex.Message.Contains("timezone", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("time zone", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("timeZone", StringComparison.Ordinal))
            {
                throw new PlaywrightNativeException("Invalid timezone ID: " + timezoneId);
            }
        }

        /// <summary>
        /// Emulates offline network conditions via <c>Network.setEmulateOfflineState</c>.
        /// </summary>
        /// <param name="offline">Whether the page should appear offline.</param>
        /// <returns>A task that completes when the emulation has been applied.</returns>
        internal Task SetOfflineAsync(bool offline)
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot emulate offline: the page has no active target session.");

            return target.SendAsync("Network.setEmulateOfflineState", new { offline });
        }

        /// <summary>
        /// Emulates a touch-capable viewport via <c>Page.setTouchEmulationEnabled</c>.
        /// </summary>
        /// <param name="enabled">Whether touch should be emulated.</param>
        /// <returns>A task that completes when the emulation has been applied.</returns>
        internal Task SetTouchEmulationEnabledAsync(bool enabled)
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot emulate touch: the page has no active target session.");

            return target.SendAsync("Page.setTouchEmulationEnabled", new { enabled });
        }

        /// <summary>
        /// Official <c>wkPage</c> <c>Page.overrideSetting</c> list that matches
        /// Safari desktop vs iOS (fullscreen, notifications, pointer lock,
        /// month/week inputs, push, fixed backgrounds).
        /// </summary>
        /// <param name="isMobile">Context <c>isMobile</c>.</param>
        /// <returns>A task that completes when the settings have been sent.</returns>
        internal Task ApplySafariOverrideSettingsAsync(bool isMobile)
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot apply Safari settings: the page has no active target session.");

            return ApplySafariOverrideSettingsOnAsync(target, isMobile);
        }

        /// <summary>
        /// Sends official Safari <c>Page.overrideSetting</c> values on
        /// <paramref name="target"/>. Unknown settings are ignored so older
        /// MiniBrowser builds still launch.
        /// </summary>
        /// <param name="target">The page or provisional session.</param>
        /// <param name="isMobile">Context <c>isMobile</c>.</param>
        /// <returns>A task that completes when every setting has been attempted.</returns>
        internal async Task ApplySafariOverrideSettingsOnAsync(WKTargetSession target, bool isMobile)
        {
            if (target == null)
            {
                return;
            }

            await OverrideSettingAsync(target, "FullScreenEnabled", !isMobile).ConfigureAwait(false);
            await OverrideSettingAsync(target, "NotificationsEnabled", !isMobile).ConfigureAwait(false);
            await OverrideSettingAsync(target, "PointerLockEnabled", !isMobile).ConfigureAwait(false);
            await OverrideSettingAsync(target, "InputTypeMonthEnabled", isMobile).ConfigureAwait(false);
            await OverrideSettingAsync(target, "InputTypeWeekEnabled", isMobile).ConfigureAwait(false);
            await OverrideSettingAsync(target, "FixedBackgroundsPaintRelativeToDocument", isMobile).ConfigureAwait(false);
            await OverrideSettingAsync(target, "PushAPIEnabled", !isMobile).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends one <c>Page.overrideSetting</c>. Missing protocol methods
        /// are ignored.
        /// </summary>
        /// <param name="target">The page session.</param>
        /// <param name="setting">WebKit setting name.</param>
        /// <param name="value">Setting value.</param>
        /// <returns>A task that completes when the command finishes or is skipped.</returns>
        internal async Task OverrideSettingAsync(WKTargetSession target, string setting, bool value)
        {
            try
            {
                await target.SendAsync("Page.overrideSetting", new { setting, value }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        /// <summary>
        /// Bypasses Content-Security-Policy via WebKit <c>Page.setBypassCSP</c>.
        /// </summary>
        /// <param name="enabled">Whether CSP should be bypassed.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetBypassCSPAsync(bool enabled)
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot bypass CSP: the page has no active target session.");

            return target.SendAsync("Page.setBypassCSP", new { enabled });
        }

        /// <summary>
        /// Official <c>Network.setExtraHTTPHeaders</c> on a target session,
        /// including provisional sessions after navigation.
        /// </summary>
        /// <param name="target">The session to configure.</param>
        /// <returns>A task that completes when the headers have been applied.</returns>
        internal async Task ApplyExtraHttpHeadersOnAsync(WKTargetSession target)
        {
            if (target == null)
            {
                return;
            }

            try
            {
                Dictionary<string, string> merged = ExtraHttpHeaders.Merged(_context, _extraHttpHeaders);
                await target.SendAsync("Network.setExtraHTTPHeaders", new { headers = merged ?? new Dictionary<string, string>() }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        /// <summary>
        /// Official <c>Page.setBypassCSP</c> on a target session, including
        /// provisional sessions after cross-process navigation.
        /// </summary>
        /// <param name="target">The session to configure.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task ApplyBypassCspOnAsync(WKTargetSession target)
        {
            if (target == null || _context == null || !_context.BypassCSP)
            {
                return Task.CompletedTask;
            }

            return target.SendAsync("Page.setBypassCSP", new { enabled = true });
        }

        /// <summary>
        /// Overrides geolocation via page-proxy <c>Emulation.setGeolocationOverride</c>.
        /// </summary>
        /// <param name="geolocation">Latitude, longitude, and accuracy.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetGeolocationOverrideAsync(Geolocation geolocation)
        {
            if (geolocation == null)
            {
                return Task.CompletedTask;
            }

            return _session.SendAsync("Emulation.setGeolocationOverride", new
            {
                latitude = geolocation.Latitude,
                longitude = geolocation.Longitude,
                accuracy = geolocation.Accuracy,
            });
        }

        /// <summary>
        /// Grants <paramref name="permissions"/> via page-proxy <c>Emulation.grantPermissions</c>.
        /// </summary>
        /// <param name="origin">Origin to grant for, or <c>*</c> for every origin.</param>
        /// <param name="permissions">Playwright permission names.</param>
        /// <returns>A task that completes when the grant has been applied.</returns>
        internal Task GrantPermissionsAsync(string origin, IEnumerable<string> permissions)
        {
            string[] mapped = ContextPermissionMapper.ToWebKit(permissions);
            return _session.SendAsync("Emulation.grantPermissions", new
            {
                origin = string.IsNullOrEmpty(origin) ? "*" : origin,
                permissions = mapped,
            });
        }

        /// <summary>
        /// Clears permission overrides via page-proxy <c>Emulation.resetPermissions</c>.
        /// </summary>
        /// <returns>A task that completes when permissions have been reset.</returns>
        internal Task ClearPermissionsAsync()
            => _session.SendAsync("Emulation.resetPermissions");

        /// <summary>
        /// Enables or disables page-script execution via page-proxy
        /// <c>Emulation.setJavaScriptEnabled</c>.
        /// </summary>
        /// <param name="enabled">When <see langword="false"/>, page scripts do not run.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetJavaScriptEnabledAsync(bool enabled)
            => _session.SendAsync("Emulation.setJavaScriptEnabled", new { enabled });

        /// <summary>
        /// Applies viewport, device scale factor, mobile layout, and screen size from the owning context.
        /// </summary>
        /// <param name="width">Viewport width in CSS pixels.</param>
        /// <param name="height">Viewport height in CSS pixels.</param>
        /// <param name="deviceScaleFactor">Device pixel ratio.</param>
        /// <param name="isMobile">When <see langword="true"/>, uses WebKit <c>fixedLayout</c>.</param>
        /// <param name="screenSize">Reported <c>window.screen</c> size, or <see langword="null"/> to match the viewport.</param>
        /// <param name="rememberIndependentScreen">When <see langword="true"/>, stores <paramref name="screenSize"/> as the independent screen.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal async Task SetEmulatedViewportAsync(
            int width,
            int height,
            float deviceScaleFactor,
            bool isMobile,
            ScreenSize screenSize = null,
            bool rememberIndependentScreen = true)
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Cannot set the viewport size: the page has no active target session.");

            _emulatedDeviceScaleFactor = deviceScaleFactor;
            _emulatedIsMobile = isMobile;
            if (rememberIndependentScreen)
            {
                _independentScreen = screenSize;
            }

            Task deviceMetricsTask = _session.SendAsync("Emulation.setDeviceMetricsOverride", new
            {
                width,
                height,
                fixedLayout = isMobile,
                deviceScaleFactor,
            });

            int screenWidth = screenSize?.Width ?? width;
            int screenHeight = screenSize?.Height ?? height;
            Task screenSizeTask = target.SendAsync("Page.setScreenSizeOverride", new
            {
                width = screenWidth,
                height = screenHeight,
            });

            if (isMobile)
            {
                int angle = width > height ? 90 : 0;
                await Task.WhenAll(
                    deviceMetricsTask,
                    screenSizeTask,
                    _session.SendAsync("Emulation.setOrientationOverride", new { angle })).ConfigureAwait(false);
            }
            else
            {
                await Task.WhenAll(deviceMetricsTask, screenSizeTask).ConfigureAwait(false);
            }

            _viewportSize = new PageViewportSizeResult { Width = width, Height = height };
        }

        /// <summary>
        /// Removes routes matching the given matcher.
        /// </summary>
        /// <param name="urlString">Glob used at registration, or <see langword="null"/>.</param>
        /// <param name="urlRegex">Regex used at registration, or <see langword="null"/>.</param>
        /// <param name="urlFunc">Predicate used at registration, or <see langword="null"/>.</param>
        /// <param name="handlerIdentity">Handler to remove, or <see langword="null"/>.</param>
        /// <param name="contextRoute">When set, only context-level or page-level routes are removed.</param>
        /// <param name="behavior">How to treat in-flight handlers.</param>
        /// <returns>A task that completes when interception has been updated.</returns>
        internal async Task RemoveRouteAsync(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            object handlerIdentity,
            bool? contextRoute,
            UnrouteBehavior behavior = default)
        {
            List<WKRouteEntry> removed = new();
            lock (_pendingRoutes)
            {
                for (int i = _pendingRoutes.Count - 1; i >= 0; i--)
                {
                    WKRouteEntry entry = _pendingRoutes[i];
                    if (contextRoute.HasValue && entry.IsContextRoute != contextRoute.Value)
                    {
                        continue;
                    }

                    if (entry.MatchesRegistration(urlString, urlRegex, urlFunc, handlerIdentity))
                    {
                        removed.Add(entry);
                        _pendingRoutes.RemoveAt(i);
                    }
                }
            }

            MergeRemovedRoutes(removed, _networkManager?.RemoveRoute(urlString, urlRegex, urlFunc, handlerIdentity, contextRoute));
            MergeRemovedRoutes(removed, _provisionalNetworkManager?.RemoveRoute(urlString, urlRegex, urlFunc, handlerIdentity, contextRoute));

            List<RouteHandlerLifetime> removedLifetimes = new();
            for (int i = 0; i < removed.Count; i++)
            {
                removedLifetimes.Add(removed[i].Lifetime);
            }

            await RouteHandlerLifetime.StopAllAsync(removedLifetimes, behavior).ConfigureAwait(false);
            await UpdateAllInterceptionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Removes page-level or context-level routes and updates interception.
        /// </summary>
        /// <param name="contextRoute">When set, only matching route kinds are removed.</param>
        /// <param name="behavior">How to treat in-flight handlers.</param>
        /// <returns>A task that completes when interception has been updated.</returns>
        internal async Task ClearRoutesAsync(bool? contextRoute, UnrouteBehavior behavior = default)
        {
            List<WKRouteEntry> removed = new();
            lock (_pendingRoutes)
            {
                if (!contextRoute.HasValue)
                {
                    removed.AddRange(_pendingRoutes);
                    _pendingRoutes.Clear();
                }
                else
                {
                    for (int i = _pendingRoutes.Count - 1; i >= 0; i--)
                    {
                        if (_pendingRoutes[i].IsContextRoute == contextRoute.Value)
                        {
                            removed.Add(_pendingRoutes[i]);
                            _pendingRoutes.RemoveAt(i);
                        }
                    }
                }
            }

            MergeRemovedRoutes(removed, _networkManager?.ClearRoutes(contextRoute));
            MergeRemovedRoutes(removed, _provisionalNetworkManager?.ClearRoutes(contextRoute));

            List<RouteHandlerLifetime> clearedLifetimes = new();
            for (int i = 0; i < removed.Count; i++)
            {
                clearedLifetimes.Add(removed[i].Lifetime);
            }

            await RouteHandlerLifetime.StopAllAsync(clearedLifetimes, behavior).ConfigureAwait(false);
            await UpdateAllInterceptionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Issues <c>Playwright.navigate</c> without replacing in-flight goto waiters.
        /// Official HAR <c>redirectNavigation</c> starts a new document request so
        /// the original <c>page.goto</c> can commit the redirected URL.
        /// </summary>
        /// <param name="url">The destination URL.</param>
        /// <returns>A task that completes when the protocol command is sent.</returns>
        internal Task SendHarNavigateAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            lock (_navigationLock)
            {
                // Official _redirectedNavigations: the original goto stays armed
                // and waits for this restarted document instead of throwing
                // "interrupted" / "Load request cancelled".
                _harRedirectInProgress = true;
                _pendingRedirectTarget = url;
                _pendingNavigationUrl = url;
                _emittedPendingNavigationRequest = false;
                _emittedPendingNavigationFinished = false;
                _firstPendingNavigationRequest = null;
                _pendingNavigationCommitted = false;
            }

            string frameId = _frameManager.MainFrame?.FrameId ?? _mainFrameId;
            return _browser.Session.SendAsync("Playwright.navigate", new
            {
                url,
                pageProxyId = _pageProxyId,
                frameId,
            });
        }

        /// <summary>
        /// Navigates the main frame to <paramref name="url"/>. Sends <c>Playwright.navigate</c>
        /// on the browser session — WebKit routes by <c>pageProxyId</c> and may spawn a
        /// provisional target for cross-process loads. Awaits the lifecycle event matching
        /// <paramref name="waitUntil"/> before returning.
        /// </summary>
        /// <param name="url">The destination URL.</param>
        /// <param name="waitUntil">Lifecycle gate to wait on (defaults to <see cref="WaitUntilState.Load"/>).</param>
        /// <param name="timeout">Optional timeout in milliseconds (defaults to 30s).</param>
        /// <param name="referer">Optional <c>Referer</c> header.</param>
        /// <returns>The <c>Playwright.navigate</c> response element (contains <c>loaderId</c>), or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> NavigateAsync(
            string url,
            WaitUntilState waitUntil = WaitUntilState.Load,
            float? timeout = null,
            string referer = null)
        {
            if (_closed)
            {
                throw PageClosedException();
            }

            TaskCompletionSource<bool> loadTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> domTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> commitTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            string previousUrl;
            bool sameDocumentHash;

            lock (_navigationLock)
            {
                previousUrl = _mainFrameUrl;
                sameDocumentHash = IsSameDocumentHashNavigation(previousUrl, url);

                if (_pendingLoadTcs != null || _pendingDomContentTcs != null || _pendingCommitTcs != null)
                {
                    if (_pendingNavigationCommitted)
                    {
                        _pendingLoadTcs?.TrySetResult(true);
                        _pendingDomContentTcs?.TrySetResult(true);
                    }
                    else
                    {
                        PlaywrightNativeException interrupted = new(
                            "page.goto: Navigation to \"" + _pendingNavigationUrl +
                            "\" is interrupted by another navigation to \"" + url + "\"");
                        _pendingLoadTcs?.TrySetException(interrupted);
                        _pendingDomContentTcs?.TrySetException(interrupted);
                        _pendingCommitTcs?.TrySetException(interrupted);
                    }
                }

                _pendingLoadTcs = loadTcs;
                _pendingDomContentTcs = domTcs;
                _pendingCommitTcs = commitTcs;
                _pendingNavigationCommitted = false;
                _pendingRedirectTarget = null;
                _pendingRedirectSource = null;
                _harRedirectInProgress = false;
                _lastCompetingNavigationUrl = null;
                _provisionalSwapCommitted = false;
                _awaitingReplacementTarget = false;
                _lifecycleEvents.Clear();
                if (sameDocumentHash)
                {
                    _mainFrameUrl = url ?? _mainFrameUrl;
                    _frameManager.MainFrame.Url = _mainFrameUrl;
                }
                else
                {
                    _frameManager.MainFrame.ClearLifecycleEvents();
                }

                _pendingNavigationUrl = url;
                _navigationStartUrl = previousUrl;
                _emittedPendingNavigationRequest = false;
                _emittedPendingNavigationFinished = false;
                _firstPendingNavigationRequest = null;
            }

            string frameId = _frameManager.MainFrame?.FrameId ?? _mainFrameId;
            object parameters = string.IsNullOrEmpty(referer)
                ? (object)new { url, pageProxyId = _pageProxyId, frameId }
                : new { url, pageProxyId = _pageProxyId, frameId, referrer = referer };

            int timeoutMs = (int)(timeout ?? _defaultNavigationTimeout);
            if (timeoutMs <= 0)
            {
                timeoutMs = Timeout.Infinite;
            }

            TaskCompletionSource<bool> waitTcs = waitUntil == WaitUntilState.Commit
                ? commitTcs
                : waitUntil == WaitUntilState.DOMContentLoaded
                    ? domTcs
                    : loadTcs;

            // Official reportAsNew waits until a popup's first real URL has committed.
            // Navigating a noopener popup away from an in-flight document load (e.g.
            // /one-style.html → cross-process) kills MiniBrowser; wait for that load.
            if (_opener != null)
            {
                if (PopupOpenedHelper.IsBlankUrl(_mainFrameUrl))
                {
                    await Task.WhenAny(
                            _firstNonInitialNavigationTcs.Task,
                            _closedTcs.Task,
                            Task.Delay(5_000))
                        .ConfigureAwait(false);
                }

                try
                {
                    float settleMs = timeoutMs == Timeout.Infinite ? 5_000f : Math.Min(timeoutMs, 5_000f);
                    await WaitForLoadStateAsync(LoadState.Load, settleMs).ConfigureAwait(false);
                }
#pragma warning disable RCS1075
                catch (Exception)
#pragma warning restore RCS1075
                {
                }

                if (_targetSession != null)
                {
                    try
                    {
                        await _session.SendAsync("Target.activate", new { targetId = _targetSession.TargetId })
                            .ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            }

            Task<JsonElement?> sendTask = _browser.Session.SendAsync("Playwright.navigate", parameters);

            JsonElement? result = null;

            using CancellationTokenSource cts = timeoutMs == Timeout.Infinite
                ? new CancellationTokenSource()
                : new CancellationTokenSource(timeoutMs);
            Task timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);

            try
            {
                while (true)
                {
                    Task completed = await Task.WhenAny(sendTask, waitTcs.Task, timeoutTask).ConfigureAwait(false);
                    if (completed == timeoutTask)
                    {
                        throw NavigationTimeout.Exceeded(
                            "page.goto",
                            url,
                            NavigationTimeout.WaitUntilName(waitUntil),
                            timeoutMs);
                    }

                    if (waitTcs.Task.IsFaulted || waitTcs.Task.IsCanceled)
                    {
                        await ThrowIfCancelledByRendererAsync(waitTcs.Task, url).ConfigureAwait(false);
                        await RethrowNavigationWaitAsync(waitTcs.Task, url).ConfigureAwait(false);
                    }

                    if (waitTcs.Task.IsCompletedSuccessfully && !sameDocumentHash)
                    {
                        if (_pendingNavigationCommitted)
                        {
                            if (sendTask.IsCompleted && !IsHarRedirectSendSuperseded(sendTask))
                            {
                                result = await AwaitNavigateSendAsync(sendTask, url).ConfigureAwait(false);
                            }

                            break;
                        }

                        Task waitCommit = await Task.WhenAny(sendTask, commitTcs.Task, timeoutTask).ConfigureAwait(false);
                        if (waitCommit == timeoutTask)
                        {
                            throw NavigationTimeout.Exceeded(
                                "page.goto",
                                url,
                                NavigationTimeout.WaitUntilName(waitUntil),
                                timeoutMs);
                        }

                        continue;
                    }

                    if (!sendTask.IsCompleted)
                    {
                        continue;
                    }

                    if (sendTask.IsFaulted || sendTask.IsCanceled)
                    {
                        if (IsHarRedirectPending())
                        {
                            Task redirected = await Task.WhenAny(waitTcs.Task, timeoutTask).ConfigureAwait(false);
                            if (redirected == timeoutTask)
                            {
                                throw NavigationTimeout.Exceeded(
                                    "page.goto",
                                    url,
                                    NavigationTimeout.WaitUntilName(waitUntil),
                                    timeoutMs);
                            }

                            if (waitTcs.Task.IsFaulted || waitTcs.Task.IsCanceled)
                            {
                                await ThrowIfCancelledByRendererAsync(waitTcs.Task, url).ConfigureAwait(false);
                                await RethrowNavigationWaitAsync(waitTcs.Task, url).ConfigureAwait(false);
                            }

                            break;
                        }

                        await ThrowIfCancelledByRendererAsync(sendTask, url).ConfigureAwait(false);
                    }

                    result = await AwaitNavigateSendAsync(sendTask, url).ConfigureAwait(false);
                    if (sameDocumentHash)
                    {
                        ApplyRequestedNavigationUrl(url);
                        return result;
                    }

                    CompletePendingIfUrlReached();
                    if (waitTcs.Task.IsCompletedSuccessfully)
                    {
                        break;
                    }

                    Task lifecycle = await Task.WhenAny(waitTcs.Task, timeoutTask).ConfigureAwait(false);
                    if (lifecycle != waitTcs.Task)
                    {
                        throw NavigationTimeout.Exceeded(
                            "page.goto",
                            url,
                            NavigationTimeout.WaitUntilName(waitUntil),
                            timeoutMs);
                    }

                    await ThrowIfCancelledByRendererAsync(waitTcs.Task, url).ConfigureAwait(false);
                    await RethrowNavigationWaitAsync(waitTcs.Task, url).ConfigureAwait(false);
                    break;
                }
            }
            catch (PlaywrightNativeException ex) when (ex is not NavigationException)
            {
                if (HasDownloadForUrl(url))
                {
                    throw new NavigationException("Download is starting", url, ex);
                }

                throw new NavigationException(ex.Message, url, ex);
            }
            catch (NavigationException ex) when (
                HasDownloadForUrl(url)
                && (ex.Message == null
                    || !ex.Message.Contains("Download is starting", StringComparison.Ordinal)))
            {
                throw new NavigationException("Download is starting", url, ex);
            }

            if (waitUntil == WaitUntilState.NetworkIdle)
            {
                try
                {
                    await WaitForLoadStateAsync(LoadState.NetworkIdle, timeoutMs).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    throw NavigationTimeout.Exceeded(
                        "page.goto",
                        url,
                        NavigationTimeout.WaitUntilName(waitUntil),
                        timeoutMs);
                }
            }

            if (!IsHarRedirectPending())
            {
                ApplyRequestedNavigationUrl(NavigationTimeout.PreserveUserInfo(url, _mainFrameUrl));
            }

            return result;
        }

        internal async Task<IResponse> RunHistoryNavigationAsync(
            Func<Task<bool>> navigate,
            WaitUntilState waitUntil,
            float? timeout,
            bool allowSameDocument = true)
        {
            if (navigate == null)
            {
                throw new ArgumentNullException(nameof(navigate));
            }

            if (_closed)
            {
                throw PageClosedException();
            }

            TaskCompletionSource<bool> loadTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> domTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            IResponse captured = null;
            string previousUrl;
            lock (_navigationLock)
            {
                previousUrl = _mainFrameUrl;
                if (!allowSameDocument)
                {
                    _pendingLoadTcs = loadTcs;
                    _pendingDomContentTcs = domTcs;
                    _lifecycleEvents.Clear();
                    _frameManager.MainFrame.ClearLifecycleEvents();
                }
            }

            void OnResponse(object sender, IResponse response)
            {
                if (response?.Request != null
                    && (response.Request.IsNavigationRequest
                        || NetworkRequestEvents.IsDocumentNavigation(response.Request.ResourceType)))
                {
                    captured = response;
                    if (allowSameDocument
                        && !string.Equals(Url, previousUrl, StringComparison.Ordinal)
                        && !string.Equals(response.Url, previousUrl, StringComparison.Ordinal))
                    {
                        loadTcs.TrySetResult(true);
                        domTcs.TrySetResult(true);
                    }
                }
            }

            void TryCompleteHistory()
            {
                if (!string.Equals(Url, previousUrl, StringComparison.Ordinal))
                {
                    loadTcs.TrySetResult(true);
                    domTcs.TrySetResult(true);
                }
            }

            void OnHistoryFrameNavigated(object sender, IFrame frame)
            {
                if (frame?.ParentFrame != null)
                {
                    return;
                }

                if (allowSameDocument)
                {
                    TryCompleteHistory();
                }
            }

            void OnHistoryLoad(object sender, IPage page) => TryCompleteHistory();

            Response += OnResponse;
            FrameNavigated += OnHistoryFrameNavigated;
            if (allowSameDocument)
            {
                Load += OnHistoryLoad;
            }

            try
            {
                bool proceeded = await navigate().ConfigureAwait(false);
                if (!proceeded)
                {
                    lock (_navigationLock)
                    {
                        if (_pendingLoadTcs == loadTcs)
                        {
                            _pendingLoadTcs = null;
                        }

                        if (_pendingDomContentTcs == domTcs)
                        {
                            _pendingDomContentTcs = null;
                        }
                    }

                    return null;
                }

                if (allowSameDocument)
                {
                    TryCompleteHistory();
                }

                TaskCompletionSource<bool> waitTcs = waitUntil == WaitUntilState.Commit
                    ? loadTcs
                    : waitUntil == WaitUntilState.DOMContentLoaded
                        ? domTcs
                        : loadTcs;
                int timeoutMs = (int)(timeout ?? _defaultNavigationTimeout);
                using System.Threading.CancellationTokenSource cts = new(timeoutMs);
                Task delayTask = Task.Delay(System.Threading.Timeout.Infinite, cts.Token);
                Task completed = await Task.WhenAny(waitTcs.Task, delayTask).ConfigureAwait(false);
                if (completed != waitTcs.Task)
                {
                    if (allowSameDocument && !string.Equals(Url, previousUrl, StringComparison.Ordinal))
                    {
                        return captured ?? FindNavigationResponseForUrl(Url);
                    }

                    throw new TimeoutException($"History navigation timed out after {timeoutMs}ms.");
                }

                await waitTcs.Task.ConfigureAwait(false);
                if (waitUntil == WaitUntilState.NetworkIdle)
                {
                    await WaitForLoadStateAsync(LoadState.NetworkIdle, timeout).ConfigureAwait(false);
                }

                await WaitForUsableExecutionContextAsync().ConfigureAwait(false);
                return captured ?? FindNavigationResponseForUrl(Url);
            }
            finally
            {
                if (allowSameDocument)
                {
                    Load -= OnHistoryLoad;
                }

                FrameNavigated -= OnHistoryFrameNavigated;
                Response -= OnResponse;
            }
        }

        internal async Task<bool> TryGoHistoryAsync(string method)
        {
            WKTargetSession target = _targetSession
                ?? throw PageClosedException();

            try
            {
                await target.SendAsync(method).ConfigureAwait(false);
                return true;
            }
            catch (PlaywrightNativeException ex) when (ex.Message != null && ex.Message.Contains("Failed to go", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        /// <summary>
        /// Waits until <paramref name="frame"/> has reached <paramref name="state"/>.
        /// The main frame uses page lifecycle; child frames poll
        /// <c>document.readyState</c> in that frame's world.
        /// </summary>
        /// <param name="frame">The frame to wait on.</param>
        /// <param name="state">The load state to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when the state is reached.</returns>
        internal Task WaitForFrameLoadStateAsync(WKFrame frame, LoadState state, float? timeout)
        {
            if (frame == null || frame.ParentFrame == null)
            {
                return WaitForLoadStateAsync(state, timeout);
            }

            if (state == LoadState.NetworkIdle)
            {
                return frame.WaitForLoadStateAsync(state, timeout, "frame.waitForLoadState");
            }

            WaitUntilState waitUntil = state == LoadState.DOMContentLoaded
                ? WaitUntilState.DOMContentLoaded
                : WaitUntilState.Load;
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            if (timeoutMs == System.Threading.Timeout.Infinite)
            {
                timeoutMs = int.MaxValue;
            }

            return WaitForChildFrameReadyAsync(frame, frame.Url, waitUntil, timeoutMs);
        }

        /// <summary>
        /// Navigates <paramref name="frame"/> to <paramref name="url"/>. Main-frame
        /// navigations reuse <see cref="GoToAsync(string, WaitUntilState, float?, string)"/>; child frames send
        /// <c>Playwright.navigate</c> with that frame's id and wait on the iframe's
        /// URL plus <c>document.readyState</c>.
        /// </summary>
        /// <param name="frame">The frame to navigate.</param>
        /// <param name="url">The destination URL.</param>
        /// <param name="waitUntil">Lifecycle gate to wait on (defaults to <see cref="WaitUntilState.Load"/>).</param>
        /// <param name="timeout">Optional timeout in milliseconds (defaults to 30s).</param>
        /// <param name="referer">Optional <c>Referer</c> header.</param>
        /// <returns>The document <see cref="IResponse"/> for the navigation, or <see langword="null"/>.</returns>
        internal async Task<IResponse> GoToFrameAsync(
            WKFrame frame,
            string url,
            WaitUntilState waitUntil = default,
            float? timeout = default,
            string referer = default)
        {
            if (frame == null || frame.ParentFrame == null)
            {
                return await GoToAsync(url, waitUntil, timeout, referer).ConfigureAwait(false);
            }

            IFrame publicFrame = GetOrCreateFrame(frame);
            IResponse captured = null;
            void OnResponse(object sender, IResponse response)
            {
                if (response?.Request != null
                    && response.Request.IsNavigationRequest
                    && ReferenceEquals(response.Request.Frame, publicFrame))
                {
                    captured = response;
                }
            }

            Response += OnResponse;
            try
            {
                await NavigateChildFrameAsync(frame, url, waitUntil, timeout, referer).ConfigureAwait(false);
                return captured;
            }
            finally
            {
                Response -= OnResponse;
            }
        }

        /// <summary>
        /// Evaluates a JavaScript expression in the main frame's execution context.
        /// </summary>
        /// <typeparam name="T">The target type for the result.</typeparam>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The deserialized result.</returns>
        internal Task<T> EvaluateExpressionAsync<T>(string expression)
        {
            ThrowIfClosed();
            return EvaluateWithInjectedScriptRetryAsync(
                async () =>
                {
                    WKExecutionContext context = await WaitForMainExecutionContextAsync().ConfigureAwait(false);
                    return await context.EvaluateAsync<T>(expression).ConfigureAwait(false);
                });
        }

        /// <summary>
        /// Official dispatcher evaluate: <c>Runtime.evaluate</c> without waiting
        /// for an in-flight page promise.
        /// </summary>
        /// <param name="expression">JavaScript to run in every live context.</param>
        /// <returns>A task that completes when each evaluate has been sent.</returns>
        internal Task EvaluateWithoutAwaitingPromiseAsync(string expression)
        {
            WKTargetSession target = _targetSession;
            if (target == null)
            {
                return Task.CompletedTask;
            }

            List<int> contextIds = new List<int>();
            if (_lastBindingContextId != 0)
            {
                contextIds.Add(_lastBindingContextId);
            }

            if (_executionContext != null && _executionContext.ContextId != 0
                && !contextIds.Contains(_executionContext.ContextId))
            {
                contextIds.Add(_executionContext.ContextId);
            }

            foreach (KeyValuePair<string, WKExecutionContext> entry in _frameContexts)
            {
                if (entry.Value != null && entry.Value.ContextId != 0 && !contextIds.Contains(entry.Value.ContextId))
                {
                    contextIds.Add(entry.Value.ContextId);
                }
            }

            foreach (int contextId in contextIds)
            {
                try
                {
                    _ = target.SendAsync("Runtime.evaluate", new
                    {
                        expression,
                        contextId,
                        returnByValue = true,
                    });
                }
                catch (PlaywrightNativeException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Evaluates a JavaScript expression and returns the raw <c>RemoteObject</c> element.
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The raw result element, or <see langword="null"/>.</returns>
        internal Task<JsonElement?> EvaluateExpressionAsync(string expression)
        {
            ThrowIfClosed();
            return EvaluateWithInjectedScriptRetryAsync(
                async () =>
                {
                    WKExecutionContext context = await WaitForMainExecutionContextAsync().ConfigureAwait(false);
                    return await context.EvaluateAsync(expression).ConfigureAwait(false);
                });
        }

        /// <summary>
        /// Replaces the main-frame document with <paramref name="html"/> via
        /// <c>document.open/write/close</c> so scripts in the HTML execute, then waits
        /// for the <c>load</c> lifecycle event.
        /// </summary>
        /// <param name="html">Raw HTML to load into the current document.</param>
        /// <param name="timeout">Maximum time to wait for the lifecycle event, in milliseconds.</param>
        /// <param name="waitUntil">Lifecycle to wait for. Defaults to load.</param>
        /// <returns>A task that completes when the rewrite has been issued and the lifecycle is reached.</returns>
        internal Task SetContentInternalAsync(string html, float? timeout = default, WaitUntilState waitUntil = default)
            => SetContentInFrameAsync(_frameManager.MainFrame, html, timeout, waitUntil);

        /// <summary>
        /// Evaluates <paramref name="expression"/> in <paramref name="frame"/>'s world.
        /// </summary>
        /// <typeparam name="T">The target type for the result.</typeparam>
        /// <param name="frame">The frame whose execution context is used.</param>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The deserialized result.</returns>
        internal async Task<T> EvaluateInFrameAsync<T>(WKFrame frame, string expression)
        {
            WKExecutionContext context = await WaitForFrameContextAsync(frame).ConfigureAwait(false);
            return await context.EvaluateAsync<T>(expression).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates in <paramref name="frame"/> and structured-clone parses the result.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="frame">The target frame.</param>
        /// <param name="expression">An already-wrapped expression.</param>
        /// <returns>The deserialized result.</returns>
        internal async Task<T> EvaluateSerializedInFrameAsync<T>(WKFrame frame, string expression)
        {
            try
            {
                WKExecutionContext context = await WaitForFrameContextAsync(frame).ConfigureAwait(false);
                if (EvaluateSerialization.CanWrapExpression(expression))
                {
                    JsonElement? wrapped = await context
                        .EvaluateSerializedRemoteAsync(EvaluateSerialization.WithSerializedResult(expression))
                        .ConfigureAwait(false);
                    return EvaluateSerialization.ParseRemote<T>(wrapped);
                }

                JsonElement? remote = await context.EvaluateHandleAsync(expression).ConfigureAwait(false);
                return await EvaluateSerialization.MaterializeAsync<T>(
                    remote,
                    id => context.EvaluateFunctionOnHandleAsync<JsonElement>(id, EvaluateSerialization.SerializeAwaitedJs),
                    id => context.ReleaseHandleAsync(id)).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex)
            {
                throw EvaluateSerialization.RewriteException(ex, frameEvaluate: true);
            }
        }

        /// <summary>
        /// Evaluates a JavaScript function in <paramref name="frame"/>'s world with arguments.
        /// </summary>
        /// <typeparam name="T">The target type for the result.</typeparam>
        /// <param name="frame">The frame whose execution context is used.</param>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Arguments passed to the function, including JS handles.</param>
        /// <returns>The deserialized result.</returns>
        internal async Task<T> EvaluateFunctionInFrameAsync<T>(WKFrame frame, string functionDeclaration, params object[] args)
        {
            WKExecutionContext context = await WaitForFrameContextAsync(frame).ConfigureAwait(false);
            return await context.EvaluateFunctionAsync<T>(functionDeclaration, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Whether <paramref name="frame"/> already has a usable execution
        /// context. Stalled iframes stay empty.
        /// </summary>
        /// <param name="frame">A WebKit frame.</param>
        /// <returns><see langword="true"/> when evaluate/query is safe now.</returns>
        internal bool HasFrameContext(WKFrame frame)
            => TryGetFrameContext(frame, out _);

        /// <summary>
        /// Evaluates <paramref name="expression"/> in <paramref name="frame"/> and
        /// returns a JS handle to the result.
        /// </summary>
        /// <param name="frame">The frame whose world is evaluated.</param>
        /// <param name="expression">A JavaScript expression or function IIFE.</param>
        /// <returns>A handle to the remote object, or <see langword="null"/>.</returns>
        internal async Task<IJSHandle> EvaluateHandleInFrameAsync(WKFrame frame, string expression)
        {
            WKExecutionContext context = await WaitForFrameContextAsync(frame).ConfigureAwait(false);
            JsonElement? handleValue = await context.EvaluateHandleAsync(expression).ConfigureAwait(false);
            return WrapRemoteObject(context, handleValue);
        }

        /// <summary>
        /// Evaluates a JavaScript function with arguments in <paramref name="frame"/>
        /// and returns a JS handle.
        /// </summary>
        /// <param name="frame">The frame whose world is evaluated.</param>
        /// <param name="functionDeclaration">The function declaration.</param>
        /// <param name="args">Arguments, including live JS handles.</param>
        /// <returns>A handle to the remote result, or <see langword="null"/>.</returns>
        internal async Task<IJSHandle> EvaluateFunctionHandleInFrameAsync(
            WKFrame frame,
            string functionDeclaration,
            params object[] args)
        {
            WKExecutionContext context = await WaitForFrameContextAsync(frame).ConfigureAwait(false);
            JsonElement? handleValue = await context.EvaluateFunctionHandleAsync(functionDeclaration, args)
                .ConfigureAwait(false);
            return WrapRemoteObject(context, handleValue);
        }

        internal async Task<WKExecutionContext> GetUtilityWorldAsync(WKFrame frame)
        {
            if (frame == null)
            {
                return null;
            }

            string frameId = frame.FrameId;
            if (string.IsNullOrEmpty(frameId))
            {
                frameId = _mainFrameId;
            }

            if (TryGetUtilityContext(frameId, out WKExecutionContext existing))
            {
                return existing;
            }

            await EnsureUtilityWorldAsync(_targetSession).ConfigureAwait(false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (TryGetUtilityContext(frameId, out existing))
                {
                    return existing;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>
        /// Queries <paramref name="frame"/>'s document for the first element matching
        /// <paramref name="selector"/>.
        /// </summary>
        /// <param name="frame">The frame whose document is queried.</param>
        /// <param name="selector">A CSS selector.</param>
        /// <returns>A handle to the matched element, or <see langword="null"/>.</returns>
        internal async Task<IElementHandle> QuerySelectorInFrameAsync(WKFrame frame, string selector)
        {
            SelectorQuery.EnsureSelector(selector);
            DomVisibility.ThrowIfUnknownEngine(selector);
            if (FrameSelector.ContainsControl(selector))
            {
                IReadOnlyList<IElementHandle> matches = await FrameSelector.QueryAllAsync(
                    GetOrCreateFrame(frame),
                    null,
                    selector).ConfigureAwait(false);
                return matches.Count > 0 ? matches[0] : null;
            }

            WKExecutionContext context = await WaitForFrameContextAsync(frame).ConfigureAwait(false);
            try
            {
                if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
                {
                    WKExecutionContext evalContext = context;
                    if (CustomSelectors.ShouldQueryInIsolatedWorld(selector))
                    {
                        evalContext = await GetUtilityWorldAsync(frame).ConfigureAwait(false) ?? context;
                    }

                    JsonElement? custom = await evalContext.EvaluateHandleAsync(call.DocumentQueryExpression).ConfigureAwait(false);
                    return WrapWKHandle(evalContext, custom) as IElementHandle;
                }

                string selectorLiteral = JsonSerializer.Serialize(selector);
                JsonElement? handleValue = await context
                    .EvaluateHandleAsync($"({ShadowPiercingQuery.QueryFunction})({selectorLiteral})")
                    .ConfigureAwait(false);
                return WrapWKHandle(context, handleValue) as IElementHandle;
            }
            catch (PlaywrightNativeException ex) when (PlaywrightNativeException.IsDestroyedContext(ex))
            {
                string frameId = frame?.FrameId;
                if (!string.IsNullOrEmpty(frameId))
                {
                    _frameContexts.TryRemove(frameId, out _);
                }

                if (ReferenceEquals(context, _executionContext))
                {
                    _executionContext = null;
                }

                throw;
            }
        }

        /// <summary>
        /// Queries <paramref name="frame"/>'s document for every element matching
        /// <paramref name="selector"/>.
        /// </summary>
        /// <param name="frame">The frame whose document is queried.</param>
        /// <param name="selector">A CSS selector.</param>
        /// <returns>Handles for the matching elements, in document order.</returns>
        internal async Task<IReadOnlyList<IElementHandle>> QuerySelectorAllInFrameAsync(WKFrame frame, string selector)
        {
            SelectorQuery.EnsureSelector(selector);
            DomVisibility.ThrowIfUnknownEngine(selector);
            if (FrameSelector.ContainsControl(selector))
            {
                return await FrameSelector.QueryAllAsync(
                    GetOrCreateFrame(frame),
                    null,
                    selector).ConfigureAwait(false);
            }

            WKExecutionContext context = await WaitForFrameContextAsync(frame).ConfigureAwait(false);
            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                JsonElement? customArray = await context.EvaluateHandleAsync(call.DocumentQueryAllExpression).ConfigureAwait(false);
                return await UnwrapElementArrayAsync(context, customArray).ConfigureAwait(false);
            }

            string selectorLiteral = JsonSerializer.Serialize(selector);
            JsonElement? arrayRemote = await context
                .EvaluateHandleAsync($"({ShadowPiercingQuery.QueryAllFunction})({selectorLiteral})")
                .ConfigureAwait(false);
            return await UnwrapElementArrayAsync(context, arrayRemote).ConfigureAwait(false);
        }

        /// <summary>
        /// Walks a remote array of DOM nodes and wraps each item as an element handle.
        /// Releases the array object id when finished.
        /// </summary>
        /// <param name="context">The execution context that owns the array.</param>
        /// <param name="arrayRemote">The remote array object, or <see langword="null"/>.</param>
        /// <returns>Handles for the array items that are DOM nodes.</returns>
        internal async Task<IReadOnlyList<IElementHandle>> UnwrapElementArrayAsync(
            WKExecutionContext context,
            JsonElement? arrayRemote)
        {
            string arrayId = RemoteObject.GetObjectId(arrayRemote);
            if (string.IsNullOrEmpty(arrayId))
            {
                return Array.Empty<IElementHandle>();
            }

            try
            {
                int length = await context.EvaluateFunctionOnHandleAsync<int>(arrayId, "arr => arr.length").ConfigureAwait(false);
                List<IElementHandle> result = new(length);
                for (int i = 0; i < length; i++)
                {
                    JsonElement? item = await context
                        .EvaluateHandleOnHandleAsync(arrayId, "(arr, i) => arr[i]", i)
                        .ConfigureAwait(false);
                    if (WrapWKHandle(context, item) is IElementHandle element)
                    {
                        result.Add(element);
                    }
                }

                return result;
            }
            finally
            {
                await context.ReleaseHandleAsync(arrayId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wraps a WIP remote object as an element handle when it is a DOM node.
        /// </summary>
        /// <param name="context">The execution context that owns the object.</param>
        /// <param name="handleValue">The remote object, or <see langword="null"/>.</param>
        /// <returns>The element handle, or <see langword="null"/>.</returns>
        internal IElementHandle WrapElement(WKExecutionContext context, JsonElement? handleValue)
            => WrapWKHandle(context, handleValue) as IElementHandle;

        /// <summary>
        /// Replaces <paramref name="frame"/>'s document with <paramref name="html"/>.
        /// </summary>
        /// <param name="frame">The frame to rewrite.</param>
        /// <param name="html">Raw HTML to load into the current document.</param>
        /// <param name="timeout">Maximum time to wait for the lifecycle event, in milliseconds.</param>
        /// <param name="waitUntil">Lifecycle to wait for. Defaults to load.</param>
        /// <returns>A task that completes when the rewrite has been issued and the lifecycle is reached.</returns>
        internal async Task SetContentInFrameAsync(WKFrame frame, string html, float? timeout = default, WaitUntilState waitUntil = default)
        {
            if (frame == null || frame.ParentFrame == null)
            {
                lock (_navigationLock)
                {
                    _lifecycleEvents.Clear();
                }
            }

            frame.ClearLifecycleEvents();

            string htmlJsLiteral = JsonSerializer.Serialize(html);
            string expression = $"(() => {{ document.open(); document.write({htmlJsLiteral}); document.close(); }})()";
            await EvaluateInFrameAsync<object>(frame, expression).ConfigureAwait(false);
            if (waitUntil == WaitUntilState.Commit)
            {
                return;
            }

            float resolvedTimeout = timeout ?? DefaultNavigationTimeout;
            LoadState state = waitUntil switch
            {
                WaitUntilState.DOMContentLoaded => LoadState.DOMContentLoaded,
                WaitUntilState.NetworkIdle => LoadState.NetworkIdle,
                _ => LoadState.Load,
            };
            await WaitForFrameLoadStateAsync(frame, state, resolvedTimeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a JavaScript source that runs on every new document this page loads,
        /// before any page scripts execute. WebKit takes a single combined bootstrap script,
        /// so each add accumulates the script and re-sends <c>Page.setBootstrapScript</c>
        /// with the full source of every registered init script.
        /// </summary>
        /// <param name="script">The JavaScript source to run on each new document.</param>
        /// <returns>A task that completes once the bootstrap script has been updated.</returns>
        internal async Task AddInitScriptInternalAsync(string script)
        {
            if (string.IsNullOrEmpty(script))
            {
                throw new ArgumentException("Script cannot be empty.", nameof(script));
            }

            _initScripts.Add(script);
            await SyncBootstrapScriptAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates registered user init scripts on the current document.
        /// Used when the first about:blank already existed before bootstrap ran.
        /// </summary>
        /// <returns>A task that completes when evaluation has been attempted.</returns>
        internal async Task ReplayUserInitScriptsAsync()
        {
            foreach (string script in _initScripts)
            {
                if (string.IsNullOrEmpty(script) || script == WebKitFormDataScript.Source)
                {
                    continue;
                }

                try
                {
                    await EvaluateInAllFramesAsync(script).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        /// <summary>
        /// Unregisters a previously added init script and re-sends the combined
        /// <c>Page.setBootstrapScript</c> source.
        /// </summary>
        /// <param name="script">The exact source previously passed to <see cref="AddInitScriptInternalAsync"/>.</param>
        /// <returns>A task that completes once the bootstrap script has been updated.</returns>
        internal async Task RemoveInitScriptInternalAsync(string script)
        {
            if (string.IsNullOrEmpty(script) || !_initScripts.Remove(script))
            {
                return;
            }

            try
            {
                await SyncBootstrapScriptAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        /// <summary>
        /// Exposes a .NET callback on the page's <c>window</c> object under <paramref name="name"/>.
        /// The first call registers the single WebKit binding (<c>Runtime.addBinding</c>) and installs
        /// the JS bridge as a bootstrap script (so it runs on every future document) plus on the
        /// current document. Each exposed function adds a per-name installer to the bootstrap and wires
        /// <c>window[name]</c> on the current document. Page-side calls arrive as
        /// <c>Runtime.bindingCalled</c> events routed through <see cref="OnBindingCalled"/>.
        /// </summary>
        /// <param name="name">The global name to expose on the page.</param>
        /// <param name="handler">
        /// Called with one <see cref="JsonElement"/> per JS argument; its result is serialized back to
        /// resolve the page-side promise. Throw to reject it.
        /// </param>
        /// <returns>The installer source used to unregister on dispose.</returns>
        internal async Task<string> ExposeFunctionInternalAsync(string name, Func<JsonElement[], Task<object>> handler)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must be non-empty", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handleBindings.ContainsKey(name) || !_exposedFunctions.TryAdd(name, handler))
            {
                throw new PlaywrightNativeException(PageBindingScript.AlreadyRegisteredFunction(name));
            }

            await EnsureBindingInfrastructureAsync().ConfigureAwait(false);
            string installer = PageBindingScript.InstallExpression(name);
            await AddInitScriptInternalAsync(installer).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(installer).ConfigureAwait(false);
            return installer;
        }

        /// <summary>
        /// Registers a hidden evaluate callback (not installed on <c>globalThis[name]</c>).
        /// </summary>
        /// <param name="name">Unguessable callback name.</param>
        /// <param name="handler">Host callback.</param>
        /// <returns>A task that completes when the page-side function is installed.</returns>
        internal async Task RegisterEvaluateCallbackAsync(string name, Func<JsonElement[], Task<object>> handler)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must be non-empty", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_exposedFunctions.TryAdd(name, handler))
            {
                throw new PlaywrightNativeException(PageBindingScript.AlreadyRegisteredFunction(name));
            }

            _evaluateCallbackNames[name] = 0;
            await EnsureBindingInfrastructureAsync().ConfigureAwait(false);
            await EvaluateInAllFramesAsync(PageBindingScript.InstallEvalFnExpression(name)).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a hidden init-script callback that survives navigations.
        /// </summary>
        /// <param name="name">Unguessable callback name.</param>
        /// <param name="handler">Host callback.</param>
        /// <returns>The installer source for later removal.</returns>
        internal async Task<string> RegisterPersistentEvalFnAsync(string name, Func<JsonElement[], Task<object>> handler)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must be non-empty", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_exposedFunctions.TryAdd(name, handler))
            {
                throw new PlaywrightNativeException(PageBindingScript.AlreadyRegisteredFunction(name));
            }

            await EnsureBindingInfrastructureAsync().ConfigureAwait(false);
            string installer = PageBindingScript.InstallEvalFnExpression(name);
            await AddInitScriptInternalAsync(installer).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(installer).ConfigureAwait(false);
            return installer;
        }

        /// <summary>
        /// Removes a persistent init-script callback.
        /// </summary>
        /// <param name="name">Unguessable callback name.</param>
        /// <param name="identifier">Installer source from <see cref="RegisterPersistentEvalFnAsync"/>.</param>
        /// <returns>A task that completes when the callback is removed.</returns>
        internal async Task UnregisterPersistentEvalFnAsync(string name, string identifier)
        {
            _exposedFunctions.TryRemove(name, out _);
            await RemoveInitScriptInternalAsync(identifier).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(PageBindingScript.RemoveEvalFnExpression(name)).ConfigureAwait(false);
        }

        /// <summary>
        /// Drops host-side evaluate callbacks after a main-document navigation.
        /// </summary>
        internal void EraseEvaluateCallbacks()
        {
            foreach (string name in _evaluateCallbackNames.Keys)
            {
                _exposedFunctions.TryRemove(name, out _);
            }

            _evaluateCallbackNames.Clear();
        }

        /// <summary>
        /// Registers <paramref name="handler"/> as a handle-mode binding at
        /// <c>window[name]</c>. The page-side argument is delivered as an
        /// <see cref="IJSHandle"/> instead of JSON.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <param name="handler">
        /// Called with the argument handle; the return value is serialized back to the page.
        /// </param>
        /// <returns>The installer source used to unregister on dispose.</returns>
        internal async Task<string> ExposeHandleBindingInternalAsync(string name, Func<IJSHandle, Task<object>> handler)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must be non-empty", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_exposedFunctions.ContainsKey(name) || !_handleBindings.TryAdd(name, handler))
            {
                throw new PlaywrightNativeException(PageBindingScript.AlreadyRegisteredFunction(name));
            }

            await EnsureBindingInfrastructureAsync().ConfigureAwait(false);
            string installer = PageBindingScript.InstallHandleExpression(name);
            await AddInitScriptInternalAsync(installer).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(installer).ConfigureAwait(false);
            return installer;
        }

        internal async Task<IAsyncDisposable> InstallHandleExposedAsync(
            string name,
            Func<IJSHandle, Task<object>> handler,
            bool fromContext = false)
        {
            if (!fromContext)
            {
                ThrowIfContextAlreadyHas(name);
            }

            string installer = await ExposeHandleBindingInternalAsync(name, handler).ConfigureAwait(false);
            return AddInitScriptHelper.CreateDisposable(() => RemoveExposedFunctionAsync(name, installer));
        }

        internal async Task<IAsyncDisposable> InstallExposedAsync(
            string name,
            Func<JsonElement[], Task<object>> handler,
            bool fromContext = false)
        {
            if (!fromContext)
            {
                ThrowIfContextAlreadyHas(name);
            }

            string installer = await ExposeFunctionInternalAsync(name, handler).ConfigureAwait(false);
            return AddInitScriptHelper.CreateDisposable(() => RemoveExposedFunctionAsync(name, installer));
        }

        internal async Task RemoveExposedFunctionAsync(string name, string installer)
        {
            _exposedFunctions.TryRemove(name, out _);
            _handleBindings.TryRemove(name, out _);
            await RemoveInitScriptInternalAsync(installer).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(PageBindingScript.RemoveExpression(name)).ConfigureAwait(false);
        }

        /// <summary>
        /// Consumes the next queued <c>Playwright.windowOpen</c> feature size.
        /// </summary>
        /// <returns>The feature viewport, or <see langword="null"/>.</returns>
        internal ViewportSize TakeNextWindowOpenViewport()
        {
            lock (_windowOpenFeatures)
            {
                if (_windowOpenFeatures.Count == 0)
                {
                    return null;
                }

                return WindowOpenFeatures.ParseSize(_windowOpenFeatures.Dequeue());
            }
        }

        /// <summary>
        /// Official <c>handleWindowOpen</c>: stores features for the next popup.
        /// </summary>
        /// <param name="parameters">The <c>Playwright.windowOpen</c> payload.</param>
        internal void HandleWindowOpen(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("windowFeatures", out JsonElement featuresEl)
                || featuresEl.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            List<string> features = new List<string>();
            foreach (JsonElement item in featuresEl.EnumerateArray())
            {
                features.Add(item.GetString() ?? string.Empty);
            }

            lock (_windowOpenFeatures)
            {
                _windowOpenFeatures.Enqueue(features.ToArray());
            }
        }

        internal async Task EvaluateInAllFramesAsync(string expression)
        {
            if (!_pageResumed && _opener != null)
            {
                return;
            }

            foreach (WKFrame frame in _frameManager.Frames)
            {
                try
                {
                    await EvaluateInFrameAsync<object>(frame, expression).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        internal Task EnsureBindingInfrastructureAsync()
        {
            lock (_exposeSync)
            {
                if (_bindingReady != null)
                {
                    return _bindingReady;
                }

                _bindingReady = InstallBindingInfrastructureAsync();
                return _bindingReady;
            }
        }

        internal async Task InstallBindingInfrastructureAsync()
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Inner target session is not yet available — the page has not finished initializing.");
            await target.SendAsync("Runtime.addBinding", new { name = PageBindingScript.ChannelName }).ConfigureAwait(false);
            await AddInitScriptInternalAsync(PageBindingScript.InitScript).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(PageBindingScript.InitScript).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a <c>&lt;script&gt;</c> tag to the page by URL or inline content. For URL
        /// scripts, waits for the <c>load</c> event to fire; inline scripts execute
        /// synchronously via <c>script.text</c>.
        /// </summary>
        /// <param name="url">External script URL. Mutually exclusive with <paramref name="content"/>.</param>
        /// <param name="content">Inline script body. Mutually exclusive with <paramref name="url"/>.</param>
        /// <param name="type">Optional <c>type</c> attribute (e.g. <c>module</c>).</param>
        /// <returns>A task that completes once the script has loaded/executed.</returns>
        internal async Task<IElementHandle> AddScriptTagAsync(string url = null, string content = null, string type = null)
        {
            if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(content))
            {
                throw new PlaywrightNativeException(AddScriptTagHelper.MissingOptionsMessage);
            }

            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Specify url or content, not both.");
            }

            return await AddScriptTagHelper.RaceWithCspErrorAsync(
                handler => Console += handler,
                handler => Console -= handler,
                async () =>
                {
                    string typeLiteral = JsonSerializer.Serialize(type ?? string.Empty);

                    if (!string.IsNullOrEmpty(url))
                    {
                        // WebKit's Runtime.evaluate does not reliably block on awaitPromise in this
                        // build (see WKExecutionContext), so an in-page Promise that resolves on
                        // 'onload' can return before the script has actually loaded. Stash the load
                        // result on a sentinel and poll it from .NET to wait deterministically.
                        string sentinel = "__pwScriptTag_" + Guid.NewGuid().ToString("N");
                        string elementKey = sentinel + "El";
                        string sentinelLiteral = JsonSerializer.Serialize(sentinel);
                        string elementLiteral = JsonSerializer.Serialize(elementKey);
                        string urlLiteral = JsonSerializer.Serialize(url);
                        string inject = $@"(() => {{
                            window[{sentinelLiteral}] = 0;
                            const script = document.createElement('script');
                            window[{elementLiteral}] = script;
                            script.src = {urlLiteral};
                            const type = {typeLiteral};
                            if (type) script.type = type;
                            script.onload = () => {{ window[{sentinelLiteral}] = 1; }};
                            script.onerror = () => {{ window[{sentinelLiteral}] = 2; }};
                            document.head.appendChild(script);
                            return true;
                        }})()";
                        await EvaluateExpressionAsync(inject).ConfigureAwait(false);
                        await WaitForSentinelAsync(sentinel, "Failed to load script at " + url).ConfigureAwait(false);
                        IElementHandle handle = await EvaluateElementHandleAsync($"window[{elementLiteral}]").ConfigureAwait(false);
                        await EvaluateExpressionAsync(
                            $"(() => {{ delete window[{sentinelLiteral}]; delete window[{elementLiteral}]; }})()").ConfigureAwait(false);
                        return handle;
                    }

                    string contentLiteral = JsonSerializer.Serialize(content);
                    string expression = $@"(() => {{
                        const script = document.createElement('script');
                        script.type = {typeLiteral} || 'text/javascript';
                        script.text = {contentLiteral};
                        let error = null;
                        script.onerror = e => error = e;
                        document.head.appendChild(script);
                        if (error)
                            throw error;
                        return script;
                    }})()";
                    IElementHandle contentHandle = await EvaluateElementHandleAsync(expression).ConfigureAwait(false);

                    // Official extra round-trip so async CSP console errors can win the race.
                    await EvaluateExpressionAsync("true").ConfigureAwait(false);
                    return contentHandle;
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a <c>&lt;link rel="stylesheet"&gt;</c> (for URL) or <c>&lt;style&gt;</c>
        /// (for inline content) to the document head. Waits for the onload event when
        /// loading by URL.
        /// </summary>
        /// <param name="url">External stylesheet URL. Mutually exclusive with <paramref name="content"/>.</param>
        /// <param name="content">Inline CSS. Mutually exclusive with <paramref name="url"/>.</param>
        /// <returns>A task that completes once the stylesheet has loaded/applied.</returns>
        internal async Task<IElementHandle> AddStyleTagAsync(string url = null, string content = null)
        {
            if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(content))
            {
                throw new PlaywrightNativeException(AddStyleTagHelper.MissingOptionsMessage);
            }

            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Specify url or content, not both.");
            }

            return await AddStyleTagHelper.RaceWithCspErrorAsync(
                handler => Console += handler,
                handler => Console -= handler,
                async () =>
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        // Same awaitPromise caveat as AddScriptTagAsync — poll a sentinel from .NET.
                        string sentinel = "__pwStyleTag_" + Guid.NewGuid().ToString("N");
                        string elementKey = sentinel + "El";
                        string sentinelLiteral = JsonSerializer.Serialize(sentinel);
                        string elementLiteral = JsonSerializer.Serialize(elementKey);
                        string urlLiteral = JsonSerializer.Serialize(url);
                        string inject = $@"(() => {{
                            window[{sentinelLiteral}] = 0;
                            const link = document.createElement('link');
                            window[{elementLiteral}] = link;
                            link.rel = 'stylesheet';
                            link.href = {urlLiteral};
                            link.onload = () => {{ window[{sentinelLiteral}] = 1; }};
                            link.onerror = () => {{ window[{sentinelLiteral}] = 2; }};
                            document.head.appendChild(link);
                            return true;
                        }})()";
                        await EvaluateExpressionAsync(inject).ConfigureAwait(false);
                        await WaitForSentinelAsync(sentinel, "Failed to load style at " + url).ConfigureAwait(false);
                        IElementHandle handle = await EvaluateElementHandleAsync($"window[{elementLiteral}]").ConfigureAwait(false);
                        await EvaluateExpressionAsync(
                            $"(() => {{ delete window[{sentinelLiteral}]; delete window[{elementLiteral}]; }})()").ConfigureAwait(false);
                        return handle;
                    }

                    string contentLiteral = JsonSerializer.Serialize(content);
                    string expression = $@"(() => {{
                        const style = document.createElement('style');
                        style.type = 'text/css';
                        style.appendChild(document.createTextNode({contentLiteral}));
                        document.head.appendChild(style);
                        return style;
                    }})()";
                    return await EvaluateElementHandleAsync(expression).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Asks WebKit to crash the renderer (<c>Page.crash</c>). Used by tests to
        /// exercise <see cref="Crash"/> when no <c>chrome://crash</c> equivalent exists.
        /// </summary>
        /// <returns>A task that completes when the command is sent or the session dies.</returns>
        internal Task CrashForTestsAsync()
        {
            _crashRequested = true;
            WKTargetSession session = _targetSession
                ?? throw new InvalidOperationException("Page has no target session.");

            // Do not await: Page.crash kills the renderer before an ack arrives.
            // Crash is raised from Inspector.targetCrashed (if present), main-target
            // teardown, or page-proxy destruction.
            _ = session.SendAsync("Page.crash");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called by <see cref="WKBrowser"/> when this page's <c>Playwright.pageProxyDestroyed</c>
        /// event fires (or when the browser disconnects).
        /// </summary>
        internal void DidClose()
        {
            if (_closed)
            {
                return;
            }

            if (_crashRequested)
            {
                FireCrash();
            }

            _closed = true;

            TargetClosedException closed = new TargetClosedException(
                DriverMessages.BrowserOrContextClosedExceptionMessage);
            foreach (IRequest request in _requests.Snapshot())
            {
                (request as WKRequest)?.AbortClosed(closed);
            }

            _networkManager?.AbortInflightClosed(closed);
            _provisionalNetworkManager?.AbortInflightClosed(closed);

            _session.MessageReceived -= OnPageProxyMessage;
            _session.Dispose();

            _networkManager?.Dispose();
            _networkManager = null;
            _provisionalNetworkManager?.Dispose();
            _provisionalNetworkManager = null;

            DisposeTargetSession(ref _targetSession);
            DisposeTargetSession(ref _provisionalSession);

            _initializedTcs.TrySetException(PageClosedException(" closed before initialization completed."));

            lock (_navigationLock)
            {
                _pendingLoadTcs?.TrySetException(PageClosedException(" closed during navigation."));
                _pendingDomContentTcs?.TrySetException(PageClosedException(" closed during navigation."));
                _pendingCommitTcs?.TrySetException(PageClosedException(" closed during navigation."));
                _pendingLoadTcs = null;
                _pendingDomContentTcs = null;
                _pendingCommitTcs = null;
            }

            _reportAsNewNavigationTcs.TrySetResult(true);
            _closedTcs.TrySetResult(true);
            Close?.Invoke(this, this);
        }

        /// <summary>
        /// Official <c>handleProvisionalLoadFailed</c>: an initial popup/page load
        /// that fails before <see cref="InitializedTask"/> must unblock waiters.
        /// </summary>
        /// <param name="errorText">The protocol error string.</param>
        internal void HandleProvisionalLoadFailed(string errorText)
        {
            if (!_initializedTcs.Task.IsCompleted)
            {
                _initializedTcs.TrySetException(
                    new PlaywrightNativeException(
                        string.IsNullOrEmpty(errorText) ? "Initial load failed" : errorText));
            }
        }

        /// <summary>
        /// Raises the <see cref="Request"/> event.
        /// Called by <see cref="WKNetworkManager"/> when a new network request is observed.
        /// </summary>
        /// <param name="request">The request that was created.</param>
        internal void OnRequestCreated(WKRequest request)
        {
            _requests.Add(request);
            MaybeInterruptPendingNavigation(request);
            if (request != null
                && NetworkRequestEvents.IsInternalDocumentUrl(request.Url))
            {
                return;
            }

            if (ShouldSuppressDuplicateNavigationRequest(request))
            {
                request.SuppressPageEvents = true;
                return;
            }

            TrackInflight(request, started: true);
            Request?.Invoke(this, request);
        }

        /// <summary>
        /// Surfaces the popup main navigation request before the inner target exists.
        /// </summary>
        internal void EmitPopupMainRequestIfNeeded()
        {
            if (PopupMainRequestEmitted)
            {
                return;
            }

            PopupMainRequestEmitted = true;
            WKRequest request = new(
                "popup-main",
                "about:blank",
                "GET",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                postData: null,
                resourceType: "Document",
                isNavigationRequest: true,
                redirectedFrom: null,
                frame: MainFrame);
            request.CompleteAsPopupNavigation();
            OnRequestCreated(request);
        }

        /// <summary>
        /// Raises the <see cref="Response"/> event.
        /// Called by <see cref="WKNetworkManager"/> when a network response is received.
        /// </summary>
        /// <param name="response">The response that was received.</param>
        internal void OnResponseReceived(WKResponse response)
        {
            if (response?.WKRequest?.SuppressPageEvents == true)
            {
                return;
            }

            if (response != null && ResponseHeaders.IsRedirectStatus(response.Status))
            {
                string location = null;
                foreach (KeyValuePair<string, string> header in response.Headers)
                {
                    if (string.Equals(header.Key, "location", StringComparison.OrdinalIgnoreCase))
                    {
                        location = header.Value;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(location))
                {
                    lock (_navigationLock)
                    {
                        if (!string.IsNullOrEmpty(_pendingNavigationUrl)
                            && string.Equals(
                                NavigationTimeout.WithoutHash(response.Url),
                                NavigationTimeout.WithoutHash(_pendingNavigationUrl),
                                StringComparison.Ordinal))
                        {
                            _pendingRedirectTarget = ResolveRedirectTarget(_pendingNavigationUrl, location);
                            _pendingRedirectSource = response.WKRequest;
                        }
                    }
                }
            }

            Response?.Invoke(this, response);
            if (response?.WKRequest?.Finished == true)
            {
                OnRequestFinished(response.WKRequest);
            }
        }

        /// <summary>
        /// Returns the request that redirected to <paramref name="url"/> when WebKit
        /// reports the continuation on a new process without <c>redirectResponse</c>.
        /// </summary>
        /// <param name="url">The continuation request URL.</param>
        /// <returns>The redirect source, or <see langword="null"/>.</returns>
        internal WKRequest ConsumeRedirectSource(string url)
        {
            lock (_navigationLock)
            {
                if (_pendingRedirectSource == null)
                {
                    return null;
                }

                string requestUrl = NavigationTimeout.WithoutHash(url);
                string target = NavigationTimeout.WithoutHash(_pendingRedirectTarget);
                if (string.IsNullOrEmpty(requestUrl)
                    || (!string.IsNullOrEmpty(target)
                        && !string.Equals(requestUrl, target, StringComparison.Ordinal)))
                {
                    return null;
                }

                WKRequest source = _pendingRedirectSource;
                _pendingRedirectSource = null;
                return source;
            }
        }

        /// <summary>
        /// Raises the <see cref="RequestFinished"/> event.
        /// Called by <see cref="WKNetworkManager"/> when a network request finishes loading.
        /// </summary>
        /// <param name="request">The request that finished.</param>
        internal void OnRequestFinished(WKRequest request)
        {
            if (request == null)
            {
                return;
            }

            WKRequest publicRequest = request.SuppressPageEvents
                ? _firstPendingNavigationRequest ?? request
                : request;
            if (ReferenceEquals(publicRequest, _firstPendingNavigationRequest)
                && _emittedPendingNavigationFinished)
            {
                return;
            }

            if (request.SuppressPageEvents)
            {
                _emittedPendingNavigationFinished = true;
                publicRequest.MarkFinished();
                TrackInflight(publicRequest, started: false);
                RequestFinished?.Invoke(this, publicRequest);
                MaybeCompleteAfterProvisionalSwap(publicRequest);
                return;
            }

            if (ReferenceEquals(request, _firstPendingNavigationRequest))
            {
                _emittedPendingNavigationFinished = true;
            }

            TrackInflight(request, started: false);
            RequestFinished?.Invoke(this, request);
            MaybeCompleteAfterProvisionalSwap(request);
        }

        /// <summary>
        /// Marks <paramref name="request"/> finished for <c>networkidle</c> inflight tracking.
        /// </summary>
        /// <param name="request">The request that should no longer block idle.</param>
        internal void FinishInflight(WKRequest request) => TrackInflight(request, started: false);

        /// <summary>
        /// Raises the <see cref="RequestFailed"/> event.
        /// Called by <see cref="WKNetworkManager"/> when a network request fails.
        /// </summary>
        /// <param name="request">The request that failed.</param>
        internal void OnRequestFailed(WKRequest request)
        {
            if (request?.SuppressPageEvents == true)
            {
                return;
            }

            TrackInflight(request, started: false);
            if (ShouldSuppressHarRedirectFailure(request))
            {
                return;
            }

            RequestFailed?.Invoke(this, request);
            FailPendingNavigationIfNeeded(request);
        }

        /// <summary>
        /// Updates the in-flight <see cref="NavigateAsync"/> target after
        /// <c>route.continue</c> / <c>fallback</c> changes a document URL.
        /// </summary>
        /// <param name="request">The continued navigation request.</param>
        internal void NoteContinuedNavigation(WKRequest request)
        {
            if (request == null || !request.IsNavigationRequest || string.IsNullOrEmpty(request.Url))
            {
                return;
            }

            lock (_navigationLock)
            {
                if (_pendingLoadTcs == null && _pendingDomContentTcs == null && _pendingCommitTcs == null)
                {
                    return;
                }

                bool isCurrent = ReferenceEquals(request, _firstPendingNavigationRequest)
                    || string.Equals(
                        NavigationTimeout.WithoutHash(request.Url),
                        NavigationTimeout.WithoutHash(_pendingNavigationUrl),
                        StringComparison.Ordinal)
                    || string.Equals(
                        NavigationTimeout.WithoutHash(request.ContinuedUrl),
                        NavigationTimeout.WithoutHash(_pendingNavigationUrl),
                        StringComparison.Ordinal);
                if (!isCurrent && _firstPendingNavigationRequest == null)
                {
                    isCurrent = true;
                }

                if (!isCurrent)
                {
                    return;
                }

                _pendingNavigationUrl = request.Url;
                _pendingRedirectTarget = request.Url;
            }
        }

        /// <summary>
        /// Fails an in-flight <see cref="NavigateAsync"/> when the document request is aborted.
        /// </summary>
        /// <param name="request">The failed request.</param>
        internal void FailPendingNavigationIfNeeded(WKRequest request)
        {
            if (request == null)
            {
                return;
            }

            lock (_navigationLock)
            {
                if (_pendingLoadTcs == null && _pendingDomContentTcs == null && _pendingCommitTcs == null)
                {
                    return;
                }

                string requestUrl = NavigationTimeout.WithoutHash(request.Url);
                string pendingUrl = NavigationTimeout.WithoutHash(_pendingNavigationUrl);
                string redirectUrl = NavigationTimeout.WithoutHash(_pendingRedirectTarget);
                if (IsHarRedirectRequestSuperseded(requestUrl, pendingUrl, redirectUrl))
                {
                    return;
                }

                if (_harRedirectInProgress
                    && (string.IsNullOrEmpty(request.FailureText)
                        || IsSupersededNavigationFailure(request.FailureText)))
                {
                    return;
                }

                bool matchesPending = !string.IsNullOrEmpty(pendingUrl)
                    && (string.Equals(requestUrl, pendingUrl, StringComparison.Ordinal)
                        || string.Equals(requestUrl, redirectUrl, StringComparison.Ordinal));
                if (!matchesPending && request.IsNavigationRequest)
                {
                    string continued = NavigationTimeout.WithoutHash(_firstPendingNavigationRequest?.Url);
                    matchesPending = ReferenceEquals(request, _firstPendingNavigationRequest)
                        || (!string.IsNullOrEmpty(requestUrl)
                            && !string.IsNullOrEmpty(continued)
                            && string.Equals(requestUrl, continued, StringComparison.Ordinal));
                }

                if (!matchesPending)
                {
                    return;
                }

                string reason = string.IsNullOrEmpty(request.FailureText) ? "Navigation failed" : request.FailureText;
                string errorUrl = !string.IsNullOrEmpty(request.Url) ? request.Url : _pendingNavigationUrl;
                if (HasDownloadForUrl(errorUrl) || HasDownloadForUrl(_pendingNavigationUrl))
                {
                    reason = "Download is starting";
                }

                if (IsSupersededNavigationFailure(reason))
                {
                    string startUrl = NavigationTimeout.WithoutHash(_navigationStartUrl);
                    string competing = _lastCompetingNavigationUrl;
                    if (string.IsNullOrEmpty(competing)
                        || string.Equals(competing, startUrl, StringComparison.Ordinal)
                        || string.Equals(competing, pendingUrl, StringComparison.Ordinal))
                    {
                        competing = FindCompetingNavigationUrl(pendingUrl, startUrl);
                    }

                    if (!string.IsNullOrEmpty(competing)
                        && !string.Equals(competing, pendingUrl, StringComparison.Ordinal))
                    {
                        reason = "page.goto: Navigation to \"" + pendingUrl +
                            "\" is interrupted by another navigation to \"" + competing + "\"";
                    }
                }

                NavigationException exception = new(reason, errorUrl);
                _pendingLoadTcs?.TrySetException(exception);
                _pendingDomContentTcs?.TrySetException(exception);
                _pendingCommitTcs?.TrySetException(exception);
                _pendingLoadTcs = null;
                _pendingDomContentTcs = null;
                _pendingCommitTcs = null;
                _pendingNavigationUrl = null;
            }
        }

        /// <summary>
        /// Records a close reason from the owning context so later page
        /// operations can surface it.
        /// </summary>
        /// <param name="reason">The context close reason.</param>
        internal void RecordCloseReason(string reason)
        {
            ApplyCloseReason(reason);
            _closing = true;
        }

        /// <summary>
        /// Runs a pointer action and waits for navigations it scheduled to commit.
        /// </summary>
        /// <param name="waitAfter">When <see langword="false"/>, skip the wait.</param>
        /// <param name="timeout">Click timeout in milliseconds.</param>
        /// <param name="action">The pointer action.</param>
        /// <returns>A task that completes when the action and wait finish.</returns>
        internal Task RunWithSignalsAsync(bool waitAfter, float? timeout, Func<Task> action)
            => ActionSignals.RunAsync(
                _frameManager.Signals,
                () => Task.CompletedTask,
                waitAfter,
                timeout,
                action,
                this,
                url =>
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        return;
                    }

                    _frameManager.FrameCommittedSameDocumentNavigation(_frameManager.MainFrame.FrameId, url);
                    _mainFrameUrl = _frameManager.MainFrame.Url;
                });

        private static void DisposeTargetSession(ref WKTargetSession session)
        {
            if (session == null)
            {
                return;
            }

            session.Dispose();
            session = null;
        }

        private static bool ComputeEnableFrameSessions()
        {
            // Honour the upstream escape hatch, then gate on the resolved WebKit revision
            // (2245–2255), matching upstream wkPage.ts.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WK_DISABLE_FRAME_SESSIONS")))
            {
                return false;
            }

            try
            {
                string platformKey = BrowserData.PlaywrightPlatformKey(SupportedBrowser.Webkit, BrowserData.CurrentPlatform());
                string revision = BrowserData.ResolveRevision(SupportedBrowser.Webkit, platformKey, null);
                return int.TryParse(revision, out int parsedRevision)
                    && parsedRevision >= 2245
                    && parsedRevision <= 2255;
            }
            catch (ArgumentException)
            {
                // Unknown/unsupported platform — default to the non-frame-session path.
                return false;
            }
        }

        private static bool IsSupersededNavigationFailure(string reason)
            => !string.IsNullOrEmpty(reason)
                && (reason.Contains("interrupted", StringComparison.OrdinalIgnoreCase)
                    || reason.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
                    || reason.Contains("canceled", StringComparison.OrdinalIgnoreCase));

        private static bool IsCompetingUrl(string requestUrl, string pendingUrl, string startUrl)
        {
            return !string.IsNullOrEmpty(requestUrl)
                && !string.Equals(requestUrl, pendingUrl, StringComparison.Ordinal)
                && !string.Equals(requestUrl, startUrl, StringComparison.Ordinal)
                && !requestUrl.Equals("about:blank", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameDocumentHashNavigation(string currentUrl, string nextUrl)
        {
            if (string.IsNullOrEmpty(nextUrl) || nextUrl.IndexOf('#') < 0)
            {
                return false;
            }

            string current = currentUrl ?? string.Empty;
            int currentHash = current.IndexOf('#');
            int nextHash = nextUrl.IndexOf('#');
            string currentBase = currentHash >= 0 ? current.Substring(0, currentHash) : current;
            string nextBase = nextHash >= 0 ? nextUrl.Substring(0, nextHash) : nextUrl;
            return string.Equals(currentBase, nextBase, StringComparison.Ordinal);
        }

        private static async Task<JsonElement?> AwaitNavigateSendAsync(Task<JsonElement?> sendTask, string url)
        {
            try
            {
                return await sendTask.ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (ex is not NavigationException)
            {
                throw new NavigationException(ex.Message, url, ex);
            }
        }

        private static async Task RethrowNavigationWaitAsync(Task waitTask, string url)
        {
            try
            {
                await waitTask.ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (ex is not NavigationException)
            {
                throw new NavigationException(ex.Message, url, ex);
            }
            catch (NavigationException ex) when (
                ex.Message == null
                || !ex.Message.Contains(url ?? string.Empty, StringComparison.Ordinal))
            {
                throw new NavigationException(ex.Message, url, ex);
            }
        }

        private static string ResolveRedirectTarget(string pendingUrl, string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return location;
            }

            if (location.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || location.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return NavigationTimeout.WithoutHash(location);
            }

            if (Uri.TryCreate(pendingUrl, UriKind.Absolute, out Uri baseUri)
                && Uri.TryCreate(baseUri, location, out Uri resolved))
            {
                return NavigationTimeout.WithoutHash(resolved.AbsoluteUri);
            }

            return location;
        }

        private bool IsHarRedirectPending()
        {
            lock (_navigationLock)
            {
                return _harRedirectInProgress || !string.IsNullOrEmpty(_pendingRedirectTarget);
            }
        }

        private bool IsHarRedirectSendSuperseded(Task sendTask)
        {
            if (sendTask == null || (!sendTask.IsFaulted && !sendTask.IsCanceled))
            {
                return false;
            }

            return IsHarRedirectPending();
        }

        private bool ShouldSuppressHarRedirectFailure(WKRequest request)
        {
            if (request == null || !request.IsNavigationRequest)
            {
                return false;
            }

            lock (_navigationLock)
            {
                if (!_harRedirectInProgress)
                {
                    return false;
                }

                // Cross-process HAR restarts cancel both the original URL and
                // an intermediate request at the final URL (provisional swap).
                // Official _redirectedNavigations treats those aborts as private.
                if (string.IsNullOrEmpty(request.FailureText)
                    || IsSupersededNavigationFailure(request.FailureText))
                {
                    return true;
                }

                string requestUrl = NavigationTimeout.WithoutHash(request.Url);
                string target = NavigationTimeout.WithoutHash(
                    !string.IsNullOrEmpty(_pendingRedirectTarget)
                        ? _pendingRedirectTarget
                        : _pendingNavigationUrl);
                if (string.IsNullOrEmpty(target))
                {
                    return true;
                }

                return !string.Equals(requestUrl, target, StringComparison.Ordinal);
            }
        }

        private bool IsHarRedirectRequestSuperseded(string requestUrl, string pendingUrl, string redirectUrl)
        {
            if (!_harRedirectInProgress)
            {
                return false;
            }

            string target = !string.IsNullOrEmpty(redirectUrl) ? redirectUrl : pendingUrl;
            if (string.IsNullOrEmpty(target))
            {
                return true;
            }

            return !string.Equals(requestUrl, target, StringComparison.Ordinal);
        }

        private void ThrowIfContextAlreadyHas(string name)
        {
            if (Context is IHasExposedFunctionNames contextNames && contextNames.HasExposedFunction(name))
            {
                throw new PlaywrightNativeException(PageBindingScript.AlreadyRegisteredInBrowserContext(name));
            }
        }

        private async Task ThrowIfCancelledByRendererAsync(Task waitTask, string requestedUrl)
        {
            Exception inner = waitTask.Exception?.GetBaseException() ?? waitTask.Exception?.InnerException;
            if (inner?.Message == null
                || (!inner.Message.Contains("Load request cancelled", StringComparison.OrdinalIgnoreCase)
                    && !inner.Message.Contains("cancelled", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string pendingUrl = NavigationTimeout.WithoutHash(requestedUrl);
            string competing = await WaitForCompetingNavigationUrlAsync(pendingUrl, 4000).ConfigureAwait(false);
            if (string.IsNullOrEmpty(competing))
            {
                competing = NavigationTimeout.WithoutHash(_mainFrameUrl);
            }

            if (string.IsNullOrEmpty(competing) || string.Equals(competing, pendingUrl, StringComparison.Ordinal))
            {
                return;
            }

            throw new PlaywrightNativeException(
                "page.goto: Navigation to \"" + pendingUrl +
                "\" is interrupted by another navigation to \"" + competing + "\"");
        }

        private async Task<string> WaitForCompetingNavigationUrlAsync(string pendingUrl, int timeoutMs)
        {
            string startUrl = NavigationTimeout.WithoutHash(_navigationStartUrl);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                string found = FindCompetingNavigationUrl(pendingUrl, startUrl);
                if (!string.IsNullOrEmpty(found))
                {
                    return found;
                }

                string current = NavigationTimeout.WithoutHash(_mainFrameUrl);
                if (!string.IsNullOrEmpty(current)
                    && !string.Equals(current, pendingUrl, StringComparison.Ordinal)
                    && !string.Equals(current, startUrl, StringComparison.Ordinal)
                    && !current.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            return FindCompetingNavigationUrl(pendingUrl, startUrl);
        }

        private void MaybeInterruptPendingNavigation(WKRequest request)
        {
            if (request == null)
            {
                return;
            }

            IFrame frame = request.InternalFrame;
            IFrame main = MainFrame;
            if (frame != null && main != null && !ReferenceEquals(frame, main) && frame != main)
            {
                return;
            }

            string requestUrl = NavigationTimeout.WithoutHash(request.Url);
            lock (_navigationLock)
            {
                if (string.IsNullOrEmpty(_pendingNavigationUrl) || _pendingNavigationCommitted)
                {
                    return;
                }

                string pendingUrl = NavigationTimeout.WithoutHash(_pendingNavigationUrl);
                if (string.Equals(requestUrl, pendingUrl, StringComparison.Ordinal)
                    || string.Equals(requestUrl, NavigationTimeout.WithoutHash(_navigationStartUrl), StringComparison.Ordinal))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(_pendingRedirectTarget)
                    && string.Equals(
                        requestUrl,
                        NavigationTimeout.WithoutHash(_pendingRedirectTarget),
                        StringComparison.Ordinal))
                {
                    _pendingNavigationUrl = requestUrl;
                    _pendingRedirectTarget = null;
                    _emittedPendingNavigationRequest = false;
                    _emittedPendingNavigationFinished = false;
                    _firstPendingNavigationRequest = null;
                    return;
                }

                if (request.WKRedirectedFrom != null)
                {
                    return;
                }

                // Same-origin document requests during an in-flight goto are usually
                // redirect continuations. Renderer interrupts are reported as
                // "Load request cancelled" and rewritten in NavigateAsync.
                _lastCompetingNavigationUrl = requestUrl;
            }
        }

        private bool ShouldSuppressDuplicateNavigationRequest(WKRequest request)
        {
            if (request == null || !request.IsNavigationRequest)
            {
                return false;
            }

            if (request.WKRedirectedFrom != null)
            {
                string redirectedUrl = NavigationTimeout.WithoutHash(request.Url);
                if (_emittedPendingNavigationRequest
                    && _firstPendingNavigationRequest != null
                    && string.Equals(
                        NavigationTimeout.WithoutHash(_firstPendingNavigationRequest.Url),
                        redirectedUrl,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                _pendingRedirectSource = null;
                _emittedPendingNavigationRequest = true;
                _emittedPendingNavigationFinished = false;
                _firstPendingNavigationRequest = request;
                return false;
            }

            string requestUrl = NavigationTimeout.WithoutHash(request.Url);
            string pendingUrl = NavigationTimeout.WithoutHash(_pendingNavigationUrl);
            string redirectUrl = NavigationTimeout.WithoutHash(_pendingRedirectTarget);
            bool matchesPending = !string.IsNullOrEmpty(requestUrl)
                && (string.Equals(requestUrl, pendingUrl, StringComparison.Ordinal)
                    || string.Equals(requestUrl, redirectUrl, StringComparison.Ordinal));
            if (!matchesPending)
            {
                return false;
            }

            if (_emittedPendingNavigationRequest)
            {
                return true;
            }

            _emittedPendingNavigationRequest = true;
            _firstPendingNavigationRequest = request;
            return false;
        }

        private string FindCompetingNavigationUrl(string pendingUrl, string startUrl)
        {
            if (IsCompetingUrl(_lastCompetingNavigationUrl, pendingUrl, startUrl))
            {
                return _lastCompetingNavigationUrl;
            }

            string fallback = null;
            foreach (IRequest request in _requests.Snapshot())
            {
                if (request == null)
                {
                    continue;
                }

                string requestUrl = NavigationTimeout.WithoutHash(request.Url);
                if (!IsCompetingUrl(requestUrl, pendingUrl, startUrl))
                {
                    continue;
                }

                if (request.IsNavigationRequest)
                {
                    return requestUrl;
                }

                fallback ??= requestUrl;
            }

            return fallback;
        }

        private void ApplyRequestedNavigationUrl(string url)
        {
            lock (_navigationLock)
            {
                _mainFrameUrl = url ?? _mainFrameUrl;
                _frameManager.MainFrame.Url = _mainFrameUrl;
            }
        }

        private bool HasDownloadForUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            foreach (PageDownload download in _downloads.Values)
            {
                if (string.Equals(download.Url, url, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void FailPendingWithReason(string reason, string url)
        {
            lock (_navigationLock)
            {
                if (_pendingLoadTcs == null && _pendingDomContentTcs == null && _pendingCommitTcs == null)
                {
                    return;
                }

                NavigationException exception = new(reason, url);
                _pendingLoadTcs?.TrySetException(exception);
                _pendingDomContentTcs?.TrySetException(exception);
                _pendingCommitTcs?.TrySetException(exception);
                _pendingLoadTcs = null;
                _pendingDomContentTcs = null;
                _pendingCommitTcs = null;
                _pendingNavigationUrl = null;
            }
        }

        private void CompletePendingAfterTargetReplacement()
        {
            lock (_navigationLock)
            {
                if (!_awaitingReplacementTarget)
                {
                    return;
                }

                string current = NavigationTimeout.WithoutHash(_mainFrameUrl);
                string pending = NavigationTimeout.WithoutHash(_pendingNavigationUrl);
                string redirect = NavigationTimeout.WithoutHash(_pendingRedirectTarget);
                if (string.IsNullOrEmpty(current)
                    || current.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!string.Equals(current, pending, StringComparison.Ordinal)
                    && !string.Equals(current, redirect, StringComparison.Ordinal))
                {
                    return;
                }

                _awaitingReplacementTarget = false;
                _pendingNavigationCommitted = true;
                _provisionalSwapCommitted = true;
                _pendingCommitTcs?.TrySetResult(true);
            }
        }

        private void MaybeCompleteAfterProvisionalSwap(WKRequest request)
        {
            if (request == null || !request.IsNavigationRequest)
            {
                return;
            }

            lock (_navigationLock)
            {
                string pendingUrl = NavigationTimeout.WithoutHash(_pendingNavigationUrl);
                string requestUrl = NavigationTimeout.WithoutHash(request.Url);
                bool matchesPending = !string.IsNullOrEmpty(pendingUrl)
                    && string.Equals(requestUrl, pendingUrl, StringComparison.Ordinal);
                IResponse existing = request.ExistingResponse;
                if (existing != null && ResponseHeaders.IsRedirectStatus(existing.Status))
                {
                    return;
                }

                if (_provisionalSwapCommitted || _awaitingReplacementTarget)
                {
                    return;
                }

                if (matchesPending && Volatile.Read(ref _inflightCount) <= 0)
                {
                    _pendingNavigationCommitted = true;
                    if (!string.IsNullOrEmpty(_pendingNavigationUrl))
                    {
                        _mainFrameUrl = _pendingNavigationUrl;
                        _frameManager.MainFrame.Url = _mainFrameUrl;
                    }

                    _pendingCommitTcs?.TrySetResult(true);
                }
            }
        }

        private async Task WaitForUsableExecutionContextAsync()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await EvaluateAsync<object>("1").ConfigureAwait(false);
                    return;
                }
                catch (PlaywrightNativeException ex) when (
                    ex.Message != null
                    && (ex.Message.Contains("Missing injected script", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("execution context", StringComparison.OrdinalIgnoreCase)))
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
        }

        private IResponse FindNavigationResponseForUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            IResponse found = null;
            foreach (IRequest request in _requests.Snapshot())
            {
                if (request == null
                    || (!request.IsNavigationRequest
                        && !NetworkRequestEvents.IsDocumentNavigation(request.ResourceType)))
                {
                    continue;
                }

                IResponse response = request.ExistingResponse;
                if (response == null)
                {
                    continue;
                }

                if (string.Equals(response.Url, url, StringComparison.Ordinal)
                    || string.Equals(request.Url, url, StringComparison.Ordinal)
                    || (response.Url != null && response.Url.StartsWith(url, StringComparison.Ordinal)))
                {
                    found = response;
                }
            }

            return found;
        }

        private async Task SyncBootstrapScriptAsync()
        {
            WKTargetSession target = _targetSession
                ?? throw new PlaywrightNativeException("Inner target session is not yet available — the page has not finished initializing.");
            await SyncBootstrapScriptOnAsync(target).ConfigureAwait(false);
        }

        private Task SyncBootstrapScriptOnAsync(WKTargetSession target)
            => target.SendAsync("Page.setBootstrapScript", new { source = AddInitScriptHelper.CombineBootstrap(_initScripts) });

        private async Task<IElementHandle> EvaluateElementHandleAsync(string expression)
        {
            WKExecutionContext context = RequireExecutionContext();
            JsonElement? handleValue = await context.EvaluateHandleAsync(expression).ConfigureAwait(false);
            return WrapRemoteObject(context, handleValue) as IElementHandle;
        }

        private async Task WaitForSentinelAsync(string sentinel, string errorMessage)
        {
            string sentinelLiteral = JsonSerializer.Serialize(sentinel);
            string expression = $"window[{sentinelLiteral}]";

            // Poll the in-page sentinel: 0 = pending, 1 = loaded, 2 = error.
            using System.Threading.CancellationTokenSource cts = new((int)_defaultNavigationTimeout);
            while (true)
            {
                int state = await EvaluateExpressionAsync<int>(expression).ConfigureAwait(false);
                if (state == 1)
                {
                    return;
                }

                if (state == 2)
                {
                    throw new PlaywrightNativeException(errorMessage);
                }

                if (cts.IsCancellationRequested)
                {
                    throw new TimeoutException(errorMessage + " (timed out)");
                }

                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        private IJSHandle WrapRemoteObject(WKExecutionContext context, JsonElement? handleValue)
        {
            if (handleValue == null || context == null)
            {
                return null;
            }

            JsonElement remoteObject = handleValue.Value;
            if (remoteObject.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.ValueKind == JsonValueKind.String
                && subtype.GetString() == "null")
            {
                return RemoteObject.WrapPrimitive(remoteObject);
            }

            if (!remoteObject.TryGetProperty("objectId", out JsonElement objectIdElement)
                || objectIdElement.ValueKind != JsonValueKind.String)
            {
                return RemoteObject.WrapPrimitive(remoteObject);
            }

            string objectId = objectIdElement.GetString();
            if (string.IsNullOrEmpty(objectId))
            {
                return RemoteObject.WrapPrimitive(remoteObject);
            }

            string preview = RemoteObject.HandlePreview(remoteObject);
            if (remoteObject.TryGetProperty("subtype", out JsonElement nodeSubtype)
                && nodeSubtype.ValueKind == JsonValueKind.String
                && nodeSubtype.GetString() == "node")
            {
                return new WKElementHandle(context, objectId, this, "JSHandle@node");
            }

            return new WKJSHandle(context, objectId, this, preview);
        }

        private async Task<T> EvaluatePreparedAsync<T>(string handleFn, object[] handleArgs)
        {
            await EvaluateHandleArg.StashRemoteHandlesAsync(handleArgs).ConfigureAwait(false);
            return await EvaluateSerializedAsync<T>(
                EvaluateHandleArg.PreparedExpression(handleFn, handleArgs)).ConfigureAwait(false);
        }

        private async Task<T> EvaluateSerializedAsync<T>(string expression)
        {
            try
            {
                // Yield so callers can subscribe to page events (waitForEvent)
                // before the evaluate is sent — Node's event loop does this
                // automatically between `page.evaluate(...)` and `await waitForEvent`.
                await Task.Yield();
                WKExecutionContext context = await WaitForMainExecutionContextAsync().ConfigureAwait(false);
                if (EvaluateSerialization.CanWrapExpression(expression))
                {
                    JsonElement? wrapped = await context
                        .EvaluateSerializedRemoteAsync(EvaluateSerialization.WithSerializedResult(expression))
                        .ConfigureAwait(false);
                    return EvaluateSerialization.ParseRemote<T>(wrapped);
                }

                JsonElement? remote = await context.EvaluateHandleAsync(expression).ConfigureAwait(false);
                return await EvaluateSerialization.MaterializeAsync<T>(
                    remote,
                    id => context.EvaluateFunctionOnHandleAsync<JsonElement>(id, EvaluateSerialization.SerializeAwaitedJs),
                    id => context.ReleaseHandleAsync(id)).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex)
            {
                throw EvaluateSerialization.RewriteException(ex);
            }
        }

        private async Task<T> EvaluateFunctionSerializedAsync<T>(string expression, object arg)
        {
            WKExecutionContext context = await WaitForMainExecutionContextAsync().ConfigureAwait(false);
            JsonElement? remote = await context.EvaluateFunctionHandleAsync(expression, arg).ConfigureAwait(false);
            string objectId = RemoteObject.GetObjectId(remote);
            if (!string.IsNullOrEmpty(objectId))
            {
                JsonElement tagged = await context
                    .EvaluateFunctionOnHandleAsync<JsonElement>(objectId, EvaluateSerialization.SerializeJs)
                    .ConfigureAwait(false);
                return JsonValueHelper.Parse<T>(tagged);
            }

            return EvaluateSerialization.ParseRemote<T>(remote);
        }

        /// <summary>
        /// Awaits a close command. Returns <c>false</c> when the MiniBrowser
        /// does not implement the method so the caller can try the older
        /// <c>Target.close</c> path.
        /// </summary>
        /// <param name="closeCommand">The protocol close task.</param>
        /// <returns><c>true</c> when the command was accepted or the page closed.</returns>
        private async Task<bool> TryAwaitCloseCommandAsync(Task closeCommand)
        {
            try
            {
                await closeCommand.ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (
                ex.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("unknown method", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                return true;
            }
        }

        private void ThrowIfClosed()
        {
            if (_closed || _closing)
            {
                throw PageClosedException();
            }
        }

        private TargetClosedException PageClosedException(string suffix = null)
        {
            string message = string.IsNullOrEmpty(suffix)
                ? $"Page {_pageProxyId} is closed"
                : $"Page {_pageProxyId}{suffix}";
            return ClosedTarget.Exception(message, _closeReason);
        }

        private void ApplyCloseReason(string reason)
        {
            _closeReason = reason;
            if (_session != null)
            {
                _session.CloseReason = reason;
            }

            if (_targetSession != null)
            {
                _targetSession.CloseReason = reason;
            }

            if (_provisionalSession != null)
            {
                _provisionalSession.CloseReason = reason;
            }
        }

        private WKExecutionContext RequireExecutionContext()
        {
            ThrowIfClosed();
            if (_crashed)
            {
                throw new PlaywrightNativeException("Target crashed");
            }

            WKExecutionContext ctx = _executionContext
                ?? throw new PlaywrightNativeException("Execution context is not yet available — the page has not finished initializing.");
            return ctx;
        }

        private async Task<T> EvaluateWithInjectedScriptRetryAsync<T>(Func<Task<T>> run)
        {
            try
            {
                return await run().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (
                ex.Message != null
                && (ex.Message.Contains("Missing injected script", StringComparison.Ordinal)
                    || ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                    || ex.Message.Contains("most likely because of a navigation", StringComparison.Ordinal)))
            {
                await Task.Delay(50).ConfigureAwait(false);
                return await run().ConfigureAwait(false);
            }
        }

        private void ClearExecutionContexts()
        {
            _executionContext?.MarkDestroyed();
            foreach (System.Collections.Generic.KeyValuePair<string, WKExecutionContext> entry in _frameContexts)
            {
                entry.Value?.MarkDestroyed();
            }

            foreach (System.Collections.Generic.KeyValuePair<string, WKExecutionContext> entry in _utilityContexts)
            {
                entry.Value?.MarkDestroyed();
            }

            _executionContext = null;
            _frameContexts.Clear();
            _utilityContexts.Clear();
        }

        private async Task EnsureUtilityWorldAsync(WKTargetSession target)
        {
            if (target == null)
            {
                return;
            }

            try
            {
                await target.SendAsync("Page.createUserWorld", new { name = UtilityWorldName }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private bool TryGetUtilityContext(string frameId, out WKExecutionContext context)
        {
            if (!string.IsNullOrEmpty(frameId)
                && _utilityContexts.TryGetValue(frameId, out context)
                && context != null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(_mainFrameId)
                && _utilityContexts.TryGetValue(_mainFrameId, out context)
                && context != null)
            {
                return true;
            }

            context = null;
            return false;
        }

        private void DropFrameContext(string frameId)
        {
            WKExecutionContext dropped = null;
            if (!string.IsNullOrEmpty(frameId) && _frameContexts.TryRemove(frameId, out WKExecutionContext existing))
            {
                dropped = existing;
            }

            if (dropped != null)
            {
                dropped.MarkDestroyed();
                if (ReferenceEquals(dropped, _executionContext))
                {
                    _executionContext = null;
                }

                return;
            }

            if (string.IsNullOrEmpty(frameId)
                || string.Equals(frameId, _mainFrameId, StringComparison.Ordinal)
                || string.Equals(frameId, _frameManager.MainFrame?.FrameId, StringComparison.Ordinal))
            {
                _executionContext?.MarkDestroyed();
                _executionContext = null;
            }
        }

        private async Task NavigateChildFrameAsync(
            WKFrame frame,
            string url,
            WaitUntilState waitUntil,
            float? timeout,
            string referer)
        {
            if (_closed)
            {
                throw PageClosedException();
            }

            string frameId = frame.FrameId
                ?? throw new PlaywrightNativeException("Cannot navigate a frame without a protocol id.");

            if (!string.IsNullOrEmpty(frameId))
            {
                _frameContexts.TryRemove(frameId, out _);
            }

            object parameters = string.IsNullOrEmpty(referer)
                ? (object)new { url, pageProxyId = _pageProxyId, frameId }
                : new { url, pageProxyId = _pageProxyId, frameId, referrer = referer };

            await _browser.Session.SendAsync("Playwright.navigate", parameters).ConfigureAwait(false);

            int timeoutMs = (int)(timeout ?? _defaultNavigationTimeout);
            WaitUntilState childWait = waitUntil == WaitUntilState.NetworkIdle
                ? WaitUntilState.Load
                : waitUntil;
            await WaitForChildFrameReadyAsync(frame, url, childWait, timeoutMs).ConfigureAwait(false);
            if (waitUntil == WaitUntilState.NetworkIdle)
            {
                await frame.WaitForLoadStateAsync(LoadState.NetworkIdle, timeoutMs, "frame.goto").ConfigureAwait(false);
            }
        }

        private async Task WaitForChildFrameReadyAsync(
            WKFrame frame,
            string url,
            WaitUntilState waitUntil,
            int timeoutMs)
        {
            static bool UrlMatches(string current, string target)
            {
                if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(target))
                {
                    return false;
                }

                return current.Contains(target, StringComparison.Ordinal)
                    || target.Contains(current, StringComparison.Ordinal)
                    || string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
            }

            using System.Threading.CancellationTokenSource cts = new(timeoutMs);
            while (!cts.IsCancellationRequested)
            {
                if (frame.IsDetached)
                {
                    throw new PlaywrightNativeException("frame was detached");
                }

                if (UrlMatches(frame.Url, url))
                {
                    try
                    {
                        string readyState = await EvaluateInFrameAsync<string>(frame, "document.readyState").ConfigureAwait(false);
                        bool reached = waitUntil == WaitUntilState.DOMContentLoaded
                            ? readyState == "interactive" || readyState == "complete"
                            : readyState == "complete";
                        if (reached)
                        {
                            return;
                        }
                    }
                    catch (PlaywrightNativeException)
                    {
                        // New document context is not ready yet.
                    }
                }

                try
                {
                    await Task.Delay(50, cts.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            if (frame.IsDetached)
            {
                throw new PlaywrightNativeException("frame was detached");
            }

            throw NavigationTimeout.Exceeded(
                "frame.goto",
                url,
                NavigationTimeout.WaitUntilName(waitUntil),
                timeoutMs);
        }

        private Task<WKExecutionContext> WaitForMainExecutionContextAsync()
        {
            if (_crashed)
            {
                throw new PlaywrightNativeException("Target crashed");
            }

            WKFrame main = _frameManager.MainFrame;
            if (main != null)
            {
                return WaitForFrameContextAsync(main);
            }

            return WaitForFrameContextAsync(null);
        }

        private async Task<WKExecutionContext> WaitForFrameContextAsync(WKFrame frame)
        {
            for (int i = 0; i < 50; i++)
            {
                if (_crashed)
                {
                    throw new PlaywrightNativeException("Target crashed");
                }

                if (_closed || _closing)
                {
                    throw PageClosedException();
                }

                if (TryGetFrameContext(frame, out WKExecutionContext context))
                {
                    return context;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            if (_closed || _closing)
            {
                throw PageClosedException();
            }

            throw new PlaywrightNativeException("Execution context is not yet available — the frame has not finished initializing.");
        }

        private bool TryGetFrameContext(WKFrame frame, out WKExecutionContext context)
        {
            string frameId = frame?.FrameId;
            if (!string.IsNullOrEmpty(frameId) && _frameContexts.TryGetValue(frameId, out context))
            {
                return true;
            }

            if (frame == null || frame.ParentFrame == null)
            {
                context = _executionContext;
                return context != null;
            }

            context = null;
            return false;
        }

        private IReadOnlyCollection<string> SnapshotLifecycle()
        {
            lock (_lifecycleEvents)
            {
                return new List<string>(_lifecycleEvents);
            }
        }

        private void RecordLifecycle(string name)
        {
            bool added;
            lock (_lifecycleEvents)
            {
                added = _lifecycleEvents.Add(name);
            }

            if (!added)
            {
                return;
            }

            LifecycleChanged?.Invoke(name);
        }

        private void OnMainFrameLifecycle(string name)
        {
            if (name == "networkidle")
            {
                RecordLifecycle("networkidle");
            }
        }

        private void TrackInflight(WKRequest request, bool started)
        {
            if (request == null
                || request.FrameUnavailable
                || NetworkIdleRules.IsExcluded(request.Url, request.ResourceType))
            {
                return;
            }

            WKFrame frame = request.InternalFrame is WebKitFrame instance
                ? instance.GetWKFrame()
                : _frameManager.MainFrame;
            if (frame == null)
            {
                return;
            }

            if (started)
            {
                Interlocked.Increment(ref _inflightCount);
                frame.OnInflightRequestStarted(request.RequestId);
            }
            else
            {
                Interlocked.Decrement(ref _inflightCount);
                frame.OnInflightRequestFinished(request.RequestId);
            }
        }

        private async Task<IElementHandle> QueryByScriptAsync(string functionDeclaration, params object[] args)
        {
            WKExecutionContext context = RequireExecutionContext();
            string[] literals = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                literals[i] = JsonSerializer.Serialize(args[i]);
            }

            string call = "(" + functionDeclaration + ")(" + string.Join(", ", literals) + ")";
            JsonElement? handleValue = await context.EvaluateHandleAsync(call).ConfigureAwait(false);
            return WrapWKHandle(context, handleValue) as IElementHandle;
        }

        private IJSHandle WrapWKHandle(WKExecutionContext context, JsonElement? handleValue)
        {
            if (handleValue == null)
            {
                return null;
            }

            JsonElement remoteObject = handleValue.Value;
            if (remoteObject.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.ValueKind == JsonValueKind.String
                && subtype.GetString() == "null")
            {
                return RemoteObject.WrapPrimitive(remoteObject);
            }

            if (!remoteObject.TryGetProperty("objectId", out JsonElement objectIdElement)
                || objectIdElement.ValueKind != JsonValueKind.String)
            {
                return RemoteObject.WrapPrimitive(remoteObject);
            }

            string objectId = objectIdElement.GetString();
            if (string.IsNullOrEmpty(objectId))
            {
                return RemoteObject.WrapPrimitive(remoteObject);
            }

            string preview = RemoteObject.HandlePreview(remoteObject);
            if (remoteObject.TryGetProperty("subtype", out JsonElement nodeSubtype)
                && nodeSubtype.ValueKind == JsonValueKind.String
                && nodeSubtype.GetString() == "node")
            {
                return new WKElementHandle(context, objectId, this, "JSHandle@node");
            }

            return new WKJSHandle(context, objectId, this, preview);
        }

        private async Task<IElementHandle> QuerySelectorOrThrowAsync(string selector)
        {
            IElementHandle handle = await QuerySelectorAsync(selector).ConfigureAwait(false);
            if (handle == null)
            {
                throw new PlaywrightNativeException($"No node found for selector: {selector}");
            }

            return handle;
        }

        private void OnPageProxyMessage(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Target.targetCreated":
                    OnTargetCreated(parameters?.Deserialize<WKTargetCreatedPayload>());
                    break;
                case "Target.dispatchMessageFromTarget":
                    OnDispatchMessageFromTarget(parameters);
                    break;
                case "Target.didCommitProvisionalTarget":
                    OnDidCommitProvisionalTarget(parameters);
                    break;
                case "Target.targetDestroyed":
                    OnTargetDestroyed(parameters);
                    break;
                case "Dialog.javascriptDialogOpening":
                    OnDialogOpening(parameters);
                    break;
                case "Dialog.javascriptDialogClosed":
                    _dialogTracker.OnBrowserClosedDialog(EmitDialogClosed);
                    break;
                case "Inspector.targetCrashed":
                    FireCrash();
                    break;
            }
        }

        private void OnTargetCreated(WKTargetCreatedPayload payload)
        {
            WKTargetInfo info = payload?.TargetInfo;
            if (info == null || string.IsNullOrEmpty(info.TargetId))
            {
                return;
            }

            string targetId = info.TargetId;

            // WebKit reports both page and frame targets under the page proxy. Only page
            // targets drive the main/provisional session. Frame targets (e.g. the
            // "frame-*" target the macOS-14 build emits) must be ignored here — falling
            // through to the page path would dispose the live main session and orphan any
            // in-flight command, producing a 30s hang. Mirrors upstream wkPage._onTargetCreated,
            // which returns early for targetInfo.type === 'frame'. Frame sessions are not
            // yet modelled in this port, so we simply skip them.
            if (string.Equals(info.Type, "frame", StringComparison.Ordinal))
            {
                _logger?.LogDebug("Ignoring frame target {TargetId} reported under the page proxy", targetId);
                return;
            }

            if (string.Equals(info.Type, "worker", StringComparison.Ordinal))
            {
                OnWorkerTargetCreated(info, info.IsPaused);
                return;
            }

            bool isProvisional = info.IsProvisional;
            bool isPaused = info.IsPaused;

            WKTargetSession target = new(_session, _browser.Connection, targetId);
            target.MessageReceived += OnInnerMessage;

            if (isProvisional)
            {
                if (_provisionalSession != null)
                {
                    _logger?.LogWarning("Discarding stale provisional target {OldTargetId} in favor of {NewTargetId}", _provisionalSession.TargetId, targetId);
                    _provisionalSession.MessageReceived -= OnInnerMessage;
                    _provisionalSession.Dispose();
                }

                lock (_navigationLock)
                {
                    if (_pendingLoadTcs != null || _pendingDomContentTcs != null || _pendingCommitTcs != null)
                    {
                        _awaitingReplacementTarget = true;
                    }
                }

                _provisionalSession = target;
                _ = InitializeAndMaybeResumeAsync(target, isMain: false, isPaused);
            }
            else
            {
                // If we already had a main target (WebKit recycles targets between
                // certain navigations), dispose the old one before swapping in the new.
                if (_targetSession != null)
                {
                    lock (_navigationLock)
                    {
                        if (_pendingLoadTcs != null || _pendingDomContentTcs != null || _pendingCommitTcs != null)
                        {
                            _awaitingReplacementTarget = true;
                        }
                    }

                    PreserveRoutesFrom(_networkManager);
                    PreserveRoutesFrom(_provisionalNetworkManager);
                    _networkManager?.Dispose();
                    _networkManager = null;
                    _provisionalNetworkManager?.Dispose();
                    _provisionalNetworkManager = null;
                    _targetSession.MessageReceived -= OnInnerMessage;
                    _targetSession.Dispose();
                    ClearWorkers();
                    ClearExecutionContexts();
                }

                _targetSession = target;
                _ = InitializeAndMaybeResumeAsync(target, isMain: true, isPaused);
            }
        }

        private void OnWorkerTargetCreated(WKTargetInfo info, bool isPaused)
        {
            WKTargetSession pageSession = _targetSession ?? _provisionalSession;
            if (pageSession == null)
            {
                return;
            }

            string targetId = info.TargetId;
            WKWorkerSession session = new(_session, pageSession, _browser.Connection, targetId);
            WKWorker worker = new(session, targetId, info.Url);
            if (!_workers.TryAdd(targetId, worker))
            {
                session.Dispose();
                return;
            }

            worker.ExceptionThrown += (_, error) => RaisePageError(error);
            WebKitWorker created = GetOrCreateWorker(worker);
            Worker?.Invoke(this, created);
#pragma warning disable CA2025 // Worker session is retained on WKWorker until NotifyClosed
            _ = InitializeWorkerAsync(worker, pageSession, isPaused);
#pragma warning restore CA2025
        }

        private void OnWorkerDomainCreated(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("workerId", out JsonElement idEl))
            {
                return;
            }

            string workerId = idEl.GetString();
            if (string.IsNullOrEmpty(workerId))
            {
                return;
            }

            string url = parameters.Value.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : string.Empty;
            WKTargetSession pageSession = _targetSession ?? _provisionalSession;
            if (pageSession == null)
            {
                return;
            }

            WKWorkerSession session = new(_session, pageSession, _browser.Connection, workerId);
            WKWorker worker = new(session, workerId, url);
            if (!_workers.TryAdd(workerId, worker))
            {
                session.Dispose();
                return;
            }

            worker.ExceptionThrown += (_, error) => RaisePageError(error);
            WebKitWorker domainWorker = GetOrCreateWorker(worker);
            Worker?.Invoke(this, domainWorker);
#pragma warning disable CA2025 // Worker session is retained on WKWorker until NotifyClosed
            _ = InitializeWorkerAsync(worker, pageSession, resumeTarget: false);
#pragma warning restore CA2025
        }

        private void OnWorkerDispatchMessageFromWorker(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("workerId", out JsonElement idEl)
                || !parameters.Value.TryGetProperty("message", out JsonElement messageEl)
                || messageEl.ValueKind != JsonValueKind.String)
            {
                return;
            }

            string workerId = idEl.GetString();
            if (string.IsNullOrEmpty(workerId) || !_workers.TryGetValue(workerId, out WKWorker worker))
            {
                return;
            }

            worker.Session.DispatchInboundMessage(messageEl.GetString());
        }

        private void OnWorkerDomainTerminated(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("workerId", out JsonElement idEl))
            {
                return;
            }

            string workerId = idEl.GetString();
            if (string.IsNullOrEmpty(workerId) || !_workers.TryRemove(workerId, out WKWorker worker))
            {
                return;
            }

            worker.NotifyClosed();
        }

        private void ClearWorkers()
        {
            foreach (System.Collections.Generic.KeyValuePair<string, WKWorker> entry in _workers)
            {
                entry.Value.NotifyClosed();
            }

            _workers.Clear();
            _directWorkers.Clear();
        }

        private Dictionary<string, string> WorkerExtraHeaders()
        {
            string locale = _context?.Locale;
            Dictionary<string, string> merged = ExtraHttpHeaders.Merged(_context, _extraHttpHeaders);
            if ((merged == null || merged.Count == 0) && string.IsNullOrEmpty(locale))
            {
                return null;
            }

            Dictionary<string, string> headers = merged != null && merged.Count > 0
                ? new Dictionary<string, string>(merged, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(locale) && !headers.ContainsKey("Accept-Language"))
            {
                headers["Accept-Language"] = locale;
            }

            return headers;
        }

        private async Task ApplyWorkerExtraHeadersAsync()
        {
            Dictionary<string, string> headers = WorkerExtraHeaders();
            if (headers == null || headers.Count == 0)
            {
                return;
            }

            foreach (WKWorker worker in _workers.Values)
            {
                try
                {
                    await worker.Session.SendAsync("Network.enable").ConfigureAwait(false);
                    await worker.Session.SendAsync("Network.setExtraHTTPHeaders", new { headers }).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        private async Task InitializeWorkerAsync(WKWorker worker, WKTargetSession pageSession, bool resumeTarget)
        {
            try
            {
                // Worker.initialized unpauses the worker; Runtime.enable must be in-flight
                // at the same time or the enable response never arrives. Mirrors upstream
                // wkWorkers Promise.all([Runtime.enable, Worker.initialized]).
                Task enableTask = worker.InitializeAsync();
                Task initializedTask = pageSession != null
                    ? pageSession.SendAsync("Worker.initialized", new { workerId = worker.WorkerId })
                    : Task.CompletedTask;
                await Task.WhenAll(enableTask, initializedTask).ConfigureAwait(false);
                Dictionary<string, string> workerHeaders = WorkerExtraHeaders();
                if (workerHeaders != null && workerHeaders.Count > 0)
                {
                    try
                    {
                        await worker.Session.SendAsync("Network.enable").ConfigureAwait(false);
                        await worker.Session.SendAsync("Network.setExtraHTTPHeaders", new { headers = workerHeaders }).ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }

                if (resumeTarget)
                {
                    await _session.SendAsync("Target.resume", new { targetId = worker.WorkerId }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Worker initialize failed for {WorkerId}", worker.WorkerId);
            }
        }

        private WebKitWorker GetOrCreateWorker(WKWorker worker)
        {
            if (worker == null)
            {
                return null;
            }

            return _directWorkers.GetOrAdd(worker, w =>
            {
                WebKitWorker instance = new WebKitWorker(w);
                instance.Console += (_, message) =>
                {
                    if (message is ConsoleMessage consoleMessage)
                    {
                        consoleMessage.Page = this;
                    }

                    _consoleLog.Add(message);
                    _pageListeners.Console.Emit(this, message);
                };
                return instance;
            });
        }

        private void AdoptContextMedia()
        {
            WKBrowserContext owner = _context ?? OwnerContext as WKBrowserContext;
            if (owner == null)
            {
                return;
            }

            if (owner.ColorSchemeOverride != EnumCompat.UndefinedColorScheme)
            {
                _emulatedColorScheme = owner.ColorSchemeOverride switch
                {
                    ColorScheme.Light => "Light",
                    ColorScheme.Dark => "Dark",
                    ColorScheme.NoPreference => null,
                    _ => "Light",
                };
            }

            if (owner.ReducedMotionOverride != EnumCompat.UndefinedReducedMotion)
            {
                _emulatedReducedMotion = owner.ReducedMotionOverride switch
                {
                    ReducedMotion.Reduce => "Reduce",
                    ReducedMotion.NoPreference => "NoPreference",
                    _ => "NoPreference",
                };
            }

            if (owner.ForcedColorsOverride != ForcedColors.Null)
            {
                _emulatedForcedColors = owner.ForcedColorsOverride switch
                {
                    ForcedColors.Active => "Active",
                    ForcedColors.None => "None",
                    _ => "None",
                };
            }

            if (owner.ContrastOverride != EnumCompat.UndefinedContrast)
            {
                _emulatedContrast = owner.ContrastOverride switch
                {
                    Contrast.More => "More",
                    EnumCompat.LessContrast => "Less",
                    Contrast.NoPreference => "NoPreference",
                    _ => "NoPreference",
                };
            }
        }

        private Task ApplyEmulatedMediaAsync()
        {
            List<Task> tasks = new List<Task>();
            if (_targetSession != null)
            {
                tasks.Add(ApplyEmulatedMediaToSessionAsync(_targetSession));
            }

            if (_provisionalSession != null)
            {
                tasks.Add(ApplyEmulatedMediaToSessionAsync(_provisionalSession));
            }

            return tasks.Count == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
        }

        private Task ApplyEmulatedMediaToSessionAsync(WKTargetSession session)
        {
            if (session == null || session.IsDisposed)
            {
                return Task.CompletedTask;
            }

            // Official wkPage._setEmulateMedia always sends media type plus all
            // preference overrides (and Page.setForcedColors) so a recycled target
            // after COOP navigation keeps the last emulation.
            return Task.WhenAll(
                session.SendAsync("Page.setEmulatedMedia", new { media = _emulatedMedia ?? string.Empty }),
                SendUserPreferenceAsync(session, "PrefersColorScheme", _emulatedColorScheme),
                SendUserPreferenceAsync(session, "PrefersReducedMotion", _emulatedReducedMotion),
                SendForcedColorsAsync(session, _emulatedForcedColors),
                SendUserPreferenceAsync(session, "PrefersContrast", _emulatedContrast));
        }

        private Task SendUserPreferenceAsync(WKTargetSession session, string name, string value)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = name,
            };
            if (value != null)
            {
                payload["value"] = value;
            }

            return session.SendAsync("Page.overrideUserPreference", payload);
        }

        private Task SendForcedColorsAsync(WKTargetSession session, string value)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.Ordinal);
            if (value != null)
            {
                payload["forcedColors"] = value;
            }

            return session.SendAsync("Page.setForcedColors", payload);
        }

        private async Task InitializeAndMaybeResumeAsync(WKTargetSession target, bool isMain, bool isPaused)
        {
            if (!isMain)
            {
                // Initializing a paused provisional target (Page.enable / Runtime.enable)
                // and then resuming it crashes MiniBrowser on noopener cross-process
                // popup navigations. Regular page.goto process-swaps can init while
                // paused so bootstrap runs before page scripts.
                if (isPaused && _opener != null)
                {
                    try
                    {
                        await _session.SendAsync("Target.resume", new { targetId = target.TargetId })
                            .ConfigureAwait(false);
                    }
#pragma warning disable RCS1075
                    catch (Exception)
#pragma warning restore RCS1075
                    {
                        return;
                    }

                    await InitializeProvisionalTargetAsync(target).ConfigureAwait(false);
                    return;
                }

                await InitializeProvisionalTargetAsync(target).ConfigureAwait(false);
                if (isPaused)
                {
                    try
                    {
                        await _session.SendAsync("Target.resume", new { targetId = target.TargetId })
                            .ConfigureAwait(false);
                    }
#pragma warning disable RCS1075
                    catch (Exception)
#pragma warning restore RCS1075
                    {
                    }
                }

                return;
            }

            await InitializeTargetAsync(target, isMain).ConfigureAwait(false);

            WKBrowserContext owner = OwnerContext as WKBrowserContext ?? _context;
            if (owner != null)
            {
                try
                {
                    await owner.ApplyEmulationToPageAsync(this).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            if (!isPaused)
            {
                return;
            }

            // WebKit creates targets in a paused state. The provisional target won't
            // start its cross-process navigation until we explicitly resume it; the
            // main target needs the same resume for popups and certain reload paths.
            // Failure here is non-fatal — the target may already be gone.
            try
            {
                await _session.SendAsync("Target.resume", new { targetId = target.TargetId }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Target.resume failed for target {TargetId}", target.TargetId);
            }

            _pageResumed = true;
            if (_opener != null)
            {
                try
                {
                    await ReplayExposedBindingsAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        private async Task ReplayExposedBindingsAsync()
        {
            await EvaluateInAllFramesAsync(PageBindingScript.InitScript).ConfigureAwait(false);
            foreach (string name in _exposedFunctions.Keys)
            {
                string installer = _evaluateCallbackNames.ContainsKey(name)
                    ? PageBindingScript.InstallEvalFnExpression(name)
                    : PageBindingScript.InstallExpression(name);
                await EvaluateInAllFramesAsync(installer).ConfigureAwait(false);
            }
        }

        private void OnMainTargetDestroyed(string targetId)
        {
            if (_targetSession != null && _targetSession.TargetId == targetId)
            {
                _networkManager?.Dispose();
                _networkManager = null;
                _provisionalNetworkManager?.Dispose();
                _provisionalNetworkManager = null;
                _targetSession.MessageReceived -= OnInnerMessage;
                _targetSession.Dispose();
                _targetSession = null;
                ClearWorkers();
                ClearExecutionContexts();
                if (_crashRequested)
                {
                    FireCrash();
                }

                lock (_navigationLock)
                {
                    if (!_closed && !_closing
                        && (_pendingLoadTcs != null || _pendingDomContentTcs != null || _pendingCommitTcs != null))
                    {
                        _awaitingReplacementTarget = true;
                    }
                }
            }
        }

        private async Task InitializeProvisionalTargetAsync(WKTargetSession target)
        {
            try
            {
                await target.SendAsync("Page.enable").ConfigureAwait(false);
                bool isMobile = _emulatedIsMobile
                    || (OwnerContext as WKBrowserContext)?.IsMobile == true
                    || (_context as WKBrowserContext)?.IsMobile == true;
                await ApplySafariOverrideSettingsOnAsync(target, isMobile).ConfigureAwait(false);
                JsonElement? tree = await target.SendAsync("Page.getResourceTree").ConfigureAwait(false);
                _provisionalMainFrameId = ReadMainFrameId(tree);
                await target.SendAsync("Runtime.enable").ConfigureAwait(false);
                await EnsureUtilityWorldAsync(target).ConfigureAwait(false);
                if (_bindingReady != null || !_exposedFunctions.IsEmpty || !_handleBindings.IsEmpty)
                {
                    await target.SendAsync("Runtime.addBinding", new { name = PageBindingScript.ChannelName })
                        .ConfigureAwait(false);
                }

                if (ReferenceEquals(_provisionalSession, target))
                {
                    AttachNetworkManager(target);
                }

                await target.SendAsync("Network.enable").ConfigureAwait(false);
                WKNetworkManager enabledManager = NetworkManagerFor(target);
                if (enabledManager != null)
                {
                    await enabledManager.UpdateInterceptionAsync().ConfigureAwait(false);
                }

                await ApplyExtraHttpHeadersOnAsync(target).ConfigureAwait(false);
                await SyncBootstrapScriptOnAsync(target).ConfigureAwait(false);
                await ApplyBypassCspOnAsync(target).ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                // Official swallows provisional-session init failures; a newer
                // navigation can dispose this session at any time.
            }
        }

        private async Task InitializeTargetAsync(WKTargetSession target, bool isMain)
        {
            if (!isMain)
            {
                await InitializeProvisionalTargetAsync(target).ConfigureAwait(false);
                return;
            }

            try
            {
                // Dialog lives on the page-proxy session (upstream _initializePageProxySession).
                if (isMain && !_dialogEnabled)
                {
                    await _session.SendAsync("Dialog.enable").ConfigureAwait(false);
                    _dialogEnabled = true;
                    try
                    {
                        await _session.SendAsync("Emulation.setActiveAndFocused", new { active = true })
                            .ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }

                    // Official always applies auth credentials during page-proxy
                    // init, including empty ones so 401s do not hang on a dialog.
                    await ApplyAuthCredentialsAsync().ConfigureAwait(false);
                }

                // Order mirrors upstream wkPage.ts: Page.enable + getResourceTree first so
                // we know the main frame id before any Runtime events arrive.
                await target.SendAsync("Page.enable").ConfigureAwait(false);
                bool isMobile = _emulatedIsMobile
                    || (OwnerContext as WKBrowserContext)?.IsMobile == true
                    || (_context as WKBrowserContext)?.IsMobile == true;
                await ApplySafariOverrideSettingsOnAsync(target, isMobile).ConfigureAwait(false);
                await SyncBootstrapScriptOnAsync(target).ConfigureAwait(false);
                await ApplyBypassCspOnAsync(target).ConfigureAwait(false);
                await target.SendAsync("Page.setInterceptFileChooserDialog", new { enabled = true }).ConfigureAwait(false);

                JsonElement? tree = await target.SendAsync("Page.getResourceTree").ConfigureAwait(false);
                CaptureResourceTree(tree);
                CompletePendingAfterTargetReplacement();

                await target.SendAsync("Runtime.enable").ConfigureAwait(false);
                await EnsureUtilityWorldAsync(target).ConfigureAwait(false);
                if (_bindingReady != null || !_exposedFunctions.IsEmpty || !_handleBindings.IsEmpty)
                {
                    await target.SendAsync("Runtime.addBinding", new { name = PageBindingScript.ChannelName }).ConfigureAwait(false);
                }

                await target.SendAsync("Worker.enable").ConfigureAwait(false);

                // Attach the network manager before Network.enable so no Network.* event is
                // missed. COOP / cross-process navigations emit request/response events on
                // the provisional session before didCommitProvisionalTarget; official wkPage
                // also adds Network listeners for that session.
                if (ReferenceEquals(_targetSession, target) || ReferenceEquals(_provisionalSession, target))
                {
                    AttachNetworkManager(target);
                }

                await target.SendAsync("Network.enable").ConfigureAwait(false);
                WKNetworkManager enabledManager = NetworkManagerFor(target);
                if (enabledManager != null)
                {
                    await enabledManager.UpdateInterceptionAsync().ConfigureAwait(false);
                }

                await ApplyExtraHttpHeadersOnAsync(target).ConfigureAwait(false);

                // On the frame-session builds (WebKit 2245–2255, e.g. the macOS-14 2251)
                // build) the Console domain lives on the per-frame sessions, not the page
                // session — sending Console.enable here yields "'Console' domain was not
                // found". Upstream wkPage gates Console.enable behind !enableFrameSessions
                // for exactly this reason; mirror that. (We don't model frame sessions yet,
                // so Console events are simply unavailable on those builds for now.)
                if (!EnableFrameSessions)
                {
                    await target.SendAsync("Console.enable").ConfigureAwait(false);
                }

                AdoptContextMedia();
                await ApplyEmulatedMediaToSessionAsync(target).ConfigureAwait(false);

                // Only signal the page-level InitializedTask if THIS target is still the
                // active main session. WebKit can recycle the initial target before our
                // init sequence finishes (observed on macOS-14 CI), in which case a fresh
                // OnTargetCreated has already disposed us and started a new init that will
                // complete InitializedTask itself.
                if (isMain && ReferenceEquals(_targetSession, target))
                {
                    _initializedTcs.TrySetResult(true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Init sequence on target {TargetId} failed", target.TargetId);

                // Same race guard as the success path: a stale init failing because its
                // session was disposed must not poison the page's InitializedTask when
                // a newer target is taking over.
                if (isMain && ReferenceEquals(_targetSession, target) && !target.IsDisposed)
                {
                    _initializedTcs.TrySetException(ex);
                }
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
            OfficialTraceSession.Active(Context)?.RecordAction("Route requests", "Page", "route");

            WKRouteEntry entry = new(
                urlString,
                urlRegex,
                urlFunc,
                route => handler(new WebKitRoute(route)),
                handlerIdentity,
                isContextRoute: false,
                times);
            return AddRouteAsync(entry);
        }

        private Task UnrouteInternalAsync(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            object handler,
            bool? contextRoute,
            UnrouteBehavior behavior)
            => RemoveRouteAsync(urlString, urlRegex, urlFunc, handler, contextRoute, behavior);

        private void MergeRemovedRoutes(List<WKRouteEntry> removed, List<WKRouteEntry> extra)
        {
            if (removed == null || extra == null)
            {
                return;
            }

            for (int i = 0; i < extra.Count; i++)
            {
                if (!removed.Contains(extra[i]))
                {
                    removed.Add(extra[i]);
                }
            }
        }

        private WKNetworkManager NetworkManagerFor(WKTargetSession session)
        {
            if (session == null)
            {
                return null;
            }

            if (_networkManager != null && _networkManager.AttachedTo(session))
            {
                return _networkManager;
            }

            if (_provisionalNetworkManager != null && _provisionalNetworkManager.AttachedTo(session))
            {
                return _provisionalNetworkManager;
            }

            return null;
        }

        private async Task UpdateAllInterceptionAsync()
        {
            if (_networkManager != null)
            {
                await _networkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }

            if (_provisionalNetworkManager != null)
            {
                await _provisionalNetworkManager.UpdateInterceptionAsync().ConfigureAwait(false);
            }
        }

        private void CopyRoutesTo(WKNetworkManager manager, bool consumePending)
        {
            if (manager == null)
            {
                return;
            }

            IReadOnlyList<WKRouteEntry> existing = _networkManager?.SnapshotRoutes();
            if (existing != null)
            {
                foreach (WKRouteEntry entry in existing)
                {
                    manager.AddRoute(entry);
                }
            }

            lock (_pendingRoutes)
            {
                foreach (WKRouteEntry entry in _pendingRoutes)
                {
                    manager.AddRoute(entry);
                }

                if (consumePending)
                {
                    _pendingRoutes.Clear();
                }
            }
        }

        private void AttachNetworkManager(WKTargetSession session)
        {
            if (session == null)
            {
                return;
            }

            if (NetworkManagerFor(session) != null)
            {
                return;
            }

            // COOP / cross-process navigation creates a provisional target while the
            // current document request is still in flight. Keep the main manager so
            // intercepted responses and loadingFinished are not dropped.
            if (ReferenceEquals(session, _provisionalSession) && _networkManager != null)
            {
                _provisionalNetworkManager?.Dispose();
                _provisionalNetworkManager = new WKNetworkManager(session, this);
                CopyRoutesTo(_provisionalNetworkManager, consumePending: false);
                _provisionalNetworkManager.SetHttpCredentials(_context?.HttpCredentialsList);
                return;
            }

            PreserveRoutesFrom(_networkManager);
            _networkManager?.Dispose();
            _networkManager = new WKNetworkManager(session, this);
            CopyRoutesTo(_networkManager, consumePending: true);
            _networkManager.SetHttpCredentials(_context?.HttpCredentialsList);
        }

        private void PreserveRoutesFrom(WKNetworkManager manager)
        {
            if (manager == null)
            {
                return;
            }

            IReadOnlyList<WKRouteEntry> existing = manager.SnapshotRoutes();
            lock (_pendingRoutes)
            {
                foreach (WKRouteEntry entry in existing)
                {
                    if (!_pendingRoutes.Contains(entry))
                    {
                        _pendingRoutes.Add(entry);
                    }
                }
            }
        }

        private void CaptureResourceTree(JsonElement? tree)
        {
            if (!tree.HasValue
                || !tree.Value.TryGetProperty("frameTree", out JsonElement frameTree)
                || frameTree.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            _frameManager.AdoptResourceTree(frameTree);
            _mainFrameId = _frameManager.MainFrame.FrameId;
            _mainFrameUrl = _frameManager.MainFrame.Url;
            MarkReportAsNewNavigation(_mainFrameUrl);
            CompletePendingIfUrlReached();
        }

        private void MarkReportAsNewNavigation(string url)
        {
            if (url != null
                && url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (PopupOpenedHelper.IsInitialEmptyDocumentUrl(url))
            {
                return;
            }

            _reportAsNewNavigationTcs.TrySetResult(true);
        }

        private string ReadMainFrameId(JsonElement? tree)
        {
            if (!tree.HasValue
                || !tree.Value.TryGetProperty("frameTree", out JsonElement frameTree)
                || frameTree.ValueKind != JsonValueKind.Object
                || !frameTree.TryGetProperty("frame", out JsonElement frame)
                || frame.ValueKind != JsonValueKind.Object
                || !frame.TryGetProperty("id", out JsonElement idEl)
                || idEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return idEl.GetString();
        }

        private void CompletePendingIfUrlReached()
        {
            lock (_navigationLock)
            {
                if (_pendingLoadTcs == null && _pendingDomContentTcs == null && _pendingCommitTcs == null)
                {
                    return;
                }

                string current = NavigationTimeout.WithoutHash(_mainFrameUrl);
                string pending = NavigationTimeout.WithoutHash(_pendingNavigationUrl);
                string redirect = NavigationTimeout.WithoutHash(_pendingRedirectTarget);
                if (string.IsNullOrEmpty(current)
                    || current.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string continued = NavigationTimeout.WithoutHash(_firstPendingNavigationRequest?.Url);
                if (!string.Equals(current, pending, StringComparison.Ordinal)
                    && !string.Equals(current, redirect, StringComparison.Ordinal)
                    && !string.Equals(current, continued, StringComparison.Ordinal))
                {
                    return;
                }

                _pendingNavigationCommitted = true;
                _pendingCommitTcs?.TrySetResult(true);
                bool alreadyLoaded;
                lock (_lifecycleEvents)
                {
                    alreadyLoaded = _lifecycleEvents.Contains("load");
                }

                if (alreadyLoaded)
                {
                    _pendingDomContentTcs?.TrySetResult(true);
                    _pendingLoadTcs?.TrySetResult(true);
                }
            }
        }

        private void OnDispatchMessageFromTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("targetId", out JsonElement idEl)
                || !parameters.Value.TryGetProperty("message", out JsonElement messageEl)
                || messageEl.ValueKind != JsonValueKind.String)
            {
                return;
            }

            string targetId = idEl.GetString();
            string rawJson = messageEl.GetString();

            if (_targetSession != null && _targetSession.TargetId == targetId)
            {
                _targetSession.DispatchInboundMessage(rawJson);
                return;
            }

            if (_provisionalSession != null && _provisionalSession.TargetId == targetId)
            {
                _provisionalSession.DispatchInboundMessage(rawJson);
                return;
            }

            if (_workers.TryGetValue(targetId, out WKWorker worker))
            {
                worker.Session.DispatchInboundMessage(rawJson);
                return;
            }

            _logger?.LogDebug("Dropping dispatchMessageFromTarget for unknown target {TargetId}", targetId);
        }

        private void OnDidCommitProvisionalTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("oldTargetId", out JsonElement oldEl)
                || !parameters.Value.TryGetProperty("newTargetId", out JsonElement newEl))
            {
                return;
            }

            string oldTargetId = oldEl.GetString();
            string newTargetId = newEl.GetString();

            if (_provisionalSession == null || _provisionalSession.TargetId != newTargetId)
            {
                _logger?.LogWarning("didCommitProvisionalTarget: unknown new target {NewTargetId}", newTargetId);
                return;
            }

            if (_targetSession == null || _targetSession.TargetId != oldTargetId)
            {
                _logger?.LogWarning("didCommitProvisionalTarget: unexpected old target {OldTargetId}", oldTargetId);
            }

            WKTargetSession oldSession = _targetSession;
            _targetSession = _provisionalSession;
            _provisionalSession = null;

            // Drop the execution context — a new Runtime.executionContextCreated will
            // arrive on the now-main session.
            ClearExecutionContexts();

            // Promote the provisional manager. Disposing the main manager here used to
            // drop in-flight COOP document events when it ran at provisional start.
            if (_provisionalNetworkManager != null && _provisionalNetworkManager.AttachedTo(_targetSession))
            {
                _networkManager?.Dispose();
                _networkManager = _provisionalNetworkManager;
                _provisionalNetworkManager = null;
            }
            else
            {
                AttachNetworkManager(_targetSession);
            }

            _ = _networkManager?.UpdateInterceptionAsync();

            if (!string.IsNullOrEmpty(_provisionalMainFrameId))
            {
                _frameManager.UpdateMainFrameId(_provisionalMainFrameId);
                _mainFrameId = _provisionalMainFrameId;
                _provisionalMainFrameId = null;
            }

            lock (_navigationLock)
            {
                _provisionalSwapCommitted = true;
                _pendingNavigationCommitted = true;
                if (!string.IsNullOrEmpty(_pendingNavigationUrl))
                {
                    _mainFrameUrl = _pendingNavigationUrl;
                    _frameManager.MainFrame.Url = _mainFrameUrl;
                }

                TaskCompletionSource<bool> commitTcs = _pendingCommitTcs;
                _pendingCommitTcs = null;
                commitTcs?.TrySetResult(true);
            }

            if (oldSession != null)
            {
                oldSession.MessageReceived -= OnInnerMessage;
                oldSession.Dispose();
            }
        }

        private void OnTargetDestroyed(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("targetId", out JsonElement idEl))
            {
                return;
            }

            string targetId = idEl.GetString();

            if (!string.IsNullOrEmpty(targetId) && _workers.TryRemove(targetId, out WKWorker worker))
            {
                worker.NotifyClosed();
                return;
            }

            if (_provisionalSession != null && _provisionalSession.TargetId == targetId)
            {
                _provisionalSession.MessageReceived -= OnInnerMessage;
                _provisionalSession.Dispose();
                _provisionalSession = null;

                // Official HAR redirectNavigation starts a second document load.
                // WebKit drops the first cross-process provisional; that is not
                // a failed goto — wait for the restarted navigation instead.
                if (!IsHarRedirectPending())
                {
                    FailPendingWithReason("Navigation failed", _pendingNavigationUrl);
                }

                return;
            }

            // WebKit also destroys the main target when a same-origin navigation
            // triggers a process swap. The replacement comes in as a fresh non-provisional
            // Target.targetCreated, but in the gap between destroy and create we must
            // tear down the current session so stale events from it stop dispatching.
            OnMainTargetDestroyed(targetId);
        }

        private void OnInnerMessage(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Page.frameAttached":
                    OnFrameAttached(parameters);
                    break;
                case "Page.frameDetached":
                    OnFrameDetached(parameters);
                    break;
                case "Page.frameNavigated":
                    OnFrameNavigated(parameters);
                    break;
                case "Page.navigatedWithinDocument":
                    OnNavigatedWithinDocument(parameters);
                    break;
                case "Page.willCheckNavigationPolicy":
                    OnWillCheckNavigationPolicy(parameters);
                    break;
                case "Page.didCheckNavigationPolicy":
                    OnDidCheckNavigationPolicy(parameters);
                    break;
                case "Page.loadEventFired":
                    OnLoadEventFired(parameters);
                    break;
                case "Page.domContentEventFired":
                    OnDomContentEventFired();
                    break;
                case "Runtime.executionContextCreated":
                    OnExecutionContextCreated(parameters);
                    break;
                case "Runtime.executionContextDestroyed":
                    OnExecutionContextDestroyed(parameters);
                    break;
                case "Runtime.bindingCalled":
                    OnBindingCalled(parameters);
                    break;
                case "Console.messageAdded":
                    OnConsoleMessageAdded(parameters);
                    break;
                case "Console.messageRepeatCountUpdated":
                    OnConsoleRepeatCountUpdated(parameters);
                    break;
                case "Runtime.consoleAPICalled":
                    // macOS-14 WebKit 2251 uses per-frame Console sessions, so
                    // Console.enable is not sent on the page target. Runtime is
                    // enabled there and still delivers consoleAPICalled.
                    if (EnableFrameSessions)
                    {
                        OnConsoleAPICalled(parameters);
                    }

                    break;
                case "Runtime.exceptionThrown":
                    OnExceptionThrown(parameters);
                    break;
                case "Page.fileChooserOpened":
                    OnFileChooserOpened(parameters);
                    break;
                case "Worker.workerCreated":
                    OnWorkerDomainCreated(parameters);
                    break;
                case "Worker.dispatchMessageFromWorker":
                    OnWorkerDispatchMessageFromWorker(parameters);
                    break;
                case "Worker.workerTerminated":
                    OnWorkerDomainTerminated(parameters);
                    break;
                case "Inspector.targetCrashed":
                    FireCrash();
                    break;
            }
        }

        private void FireCrash()
        {
            if (_crashed)
            {
                return;
            }

            _crashed = true;
            PlaywrightNativeException crashed = new PlaywrightNativeException("page.goto: Page crashed");
            lock (_navigationLock)
            {
                _pendingLoadTcs?.TrySetException(crashed);
                _pendingDomContentTcs?.TrySetException(crashed);
                _pendingCommitTcs?.TrySetException(crashed);
                _pendingLoadTcs = null;
                _pendingDomContentTcs = null;
                _pendingCommitTcs = null;
            }

            Crash?.Invoke(this, this);
        }

        private void OnFileChooserOpened(JsonElement? parameters)
        {
            if (!parameters.HasValue || !parameters.Value.TryGetProperty("element", out JsonElement elementObject))
            {
                return;
            }

            _ = FileChooserWaitHelper.RaiseSafelyAsync(() => RaiseFileChooserAsync(elementObject));
        }

        private async Task RaiseFileChooserAsync(JsonElement elementObject)
        {
            IElementHandle element = null;
            bool multiple = false;
            try
            {
                WKExecutionContext context = RequireExecutionContext();
                IJSHandle handle = WrapWKHandle(context, elementObject);
                element = handle as IElementHandle ?? handle?.AsElement();
                if (element != null)
                {
                    multiple = await element.EvaluateAsync<bool>("e => !!e.multiple").ConfigureAwait(false);
                }
            }
            catch (PlaywrightNativeException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (TimeoutException)
            {
            }

            FileChooser?.Invoke(this, new FileChooser(this, element, multiple));
        }

        private void OnConsoleMessageAdded(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("message", out JsonElement message))
            {
                return;
            }

            string text = message.TryGetProperty("text", out JsonElement textEl) ? textEl.GetString() : string.Empty;
            if (message.TryGetProperty("parameters", out JsonElement previewParams)
                && previewParams.ValueKind == JsonValueKind.Array)
            {
                string fromParams = RemoteObject.JoinConsoleArgs(previewParams);
                if (!string.IsNullOrEmpty(fromParams))
                {
                    text = fromParams;
                }
            }

            if (RemoteObject.TryBeautifySparseArrayJoin(text, out string sparsePreview))
            {
                text = sparsePreview;
            }

            string level = message.TryGetProperty("level", out JsonElement levelEl) ? levelEl.GetString() : "log";
            string protocolType = message.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : string.Empty;
            string type;
            if (string.Equals(protocolType, "timing", StringComparison.Ordinal))
            {
                type = "timeEnd";
            }
            else if (!string.IsNullOrEmpty(protocolType)
                && !string.Equals(protocolType, "log", StringComparison.Ordinal))
            {
                type = protocolType;
            }
            else
            {
                type = level switch
                {
                    "warning" => "warning",
                    "error" => "error",
                    "debug" => "debug",
                    "info" => "info",
                    _ => "log",
                };
            }

            string url = message.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : string.Empty;
            int line = message.TryGetProperty("line", out JsonElement lineEl) && lineEl.TryGetInt32(out int ln) ? ln : 0;
            int column = message.TryGetProperty("column", out JsonElement colEl) && colEl.TryGetInt32(out int cn) ? cn : 0;

            // WebKit Console.line/column are 1-based; official location is 0-based.
            int zeroLine = line > 0 ? line - 1 : 0;
            int zeroColumn = column > 0 ? column - 1 : 0;
            string location = string.IsNullOrEmpty(url) && zeroLine == 0 && zeroColumn == 0
                ? string.Empty
                : $"{url}:{zeroLine}:{zeroColumn}";

            // Upstream wkPage maps Console javascript errors to pageerror, not console.
            string source = message.TryGetProperty("source", out JsonElement sourceEl) ? sourceEl.GetString() : string.Empty;
            if (string.Equals(level, "error", StringComparison.Ordinal)
                && string.Equals(source, "javascript", StringComparison.Ordinal))
            {
                string protocolText = message.TryGetProperty("text", out JsonElement rawTextEl)
                    ? rawTextEl.GetString() ?? string.Empty
                    : string.Empty;
                _lastConsoleMessage = null;
                _lastConsoleRepeatCount = 0;
                RaisePageError(
                    PageErrorText.FromWebKitConsole(protocolText, message),
                    new WebErrorLocation
                    {
                        Url = url ?? string.Empty,
                        Line = line > 0 ? line - 1 : 0,
                        Column = column > 0 ? column - 1 : 0,
                    });
                return;
            }

            IReadOnlyCollection<IJSHandle> args = message.TryGetProperty("parameters", out JsonElement paramsEl)
                ? ConsoleArgs.Wrap(paramsEl, remote => _executionContext == null ? null : WrapRemoteObject(_executionContext, remote))
                : ConsoleArgs.FromText(text);

            // Official wkPage: Console.message.timestamp is seconds.
            double timestamp = 0;
            if (message.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetDouble(out double ts) && ts > 0)
            {
                timestamp = ts < 1e12 ? ts * 1000 : ts;
            }

            ConsoleMessage messageAdded = new ConsoleMessage(type, text, location, CompatCollections.AsList(args), this, timestamp);
            _lastConsoleMessage = messageAdded;
            _lastConsoleRepeatCount = 1;
            RaiseConsole(messageAdded);
        }

        private void OnConsoleRepeatCountUpdated(JsonElement? parameters)
        {
            if (_lastConsoleMessage == null || !parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            int count = payload.TryGetProperty("count", out JsonElement countEl)
                && countEl.TryGetInt32(out int parsedCount)
                ? parsedCount
                : 0;
            double timestamp = 0;
            if (payload.TryGetProperty("timestamp", out JsonElement tsEl)
                && tsEl.TryGetDouble(out double ts)
                && ts > 0)
            {
                timestamp = ts < 1e12 ? ts * 1000 : ts;
            }

            for (int i = _lastConsoleRepeatCount; i < count; i++)
            {
                RaiseConsole(new ConsoleMessage(
                    _lastConsoleMessage.Type,
                    _lastConsoleMessage.Text,
                    _lastConsoleMessage.Location,
                    _lastConsoleMessage.Args,
                    this,
                    timestamp));
            }

            _lastConsoleRepeatCount = count;
        }

        private void OnDialogOpening(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string type = payload.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : "alert";
            string message = payload.TryGetProperty("message", out JsonElement msgEl) ? msgEl.GetString() : string.Empty;
            string defaultValue = payload.TryGetProperty("defaultPrompt", out JsonElement defEl) ? defEl.GetString() : string.Empty;
            WKDialog inner = new(_session, type, message, defaultValue, this);
            IDialog dialog = _dialogTracker.Wrap(inner, EmitDialogClosed);
            IDialogHost host = (_ownerContext ?? (IBrowserContext)_context) as IDialogHost;
            EventHandler<IDialog> pageDialog = Dialog;
            bool contextHasListeners = host != null && host.HasDialogListeners();
            pageDialog?.Invoke(this, dialog);
            host?.RaiseDialog(dialog);
            PageDialogTracker.AutoDismissIfNeeded(dialog, pageDialog, contextHasListeners);
        }

        private void EmitDialogClosed(IDialog dialog) => DialogClosed?.Invoke(this, dialog);

        private void OnConsoleAPICalled(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string type = payload.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : "log";
            JsonElement? argsElement = payload.TryGetProperty("args", out JsonElement argsEl) ? argsEl : (JsonElement?)null;
            string text = argsElement.HasValue
                ? RemoteObject.JoinConsoleArgs(argsElement.Value)
                : string.Empty;
            string location = RemoteObject.FormatStackLocation(payload);
            IReadOnlyCollection<IJSHandle> args = ConsoleArgs.Wrap(
                argsElement,
                remote => _executionContext == null ? null : WrapRemoteObject(_executionContext, remote));
            double timestamp = payload.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetDouble(out double ts)
                ? ts
                : 0;
            RaiseConsole(new ConsoleMessage(type, text, location, CompatCollections.AsList(args), this, timestamp));
        }

        private void RaiseConsole(IConsoleMessage message)
        {
            _consoleLog.Add(message);
            _pageListeners.Console.Emit(this, message);
        }

        private void RaisePageError(PageErrorEventArgs error, WebErrorLocation location = null)
        {
            LastPageErrorLocation = location ?? new WebErrorLocation();
            _pageErrors.Add(error.ToString());
            PageError?.Invoke(this, error.ToString());
        }

        private void OnExceptionThrown(JsonElement? parameters)
        {
            // Official wkPage maps Console javascript errors to pageerror and does
            // not also raise from Runtime.exceptionThrown (avoids duplicate entries).
            _ = parameters;
        }

        private void OnFrameAttached(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string frameId = payload.TryGetProperty("frameId", out JsonElement idEl) ? idEl.GetString() : null;
            string parentFrameId = payload.TryGetProperty("parentFrameId", out JsonElement parentEl) ? parentEl.GetString() : null;
            _frameManager.FrameAttachedToTarget(frameId, parentFrameId);
        }

        private void OnFrameDetached(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string frameId = payload.TryGetProperty("frameId", out JsonElement idEl) ? idEl.GetString() : null;
            WKFrame detaching = _frameManager.FrameById(frameId);
            if (detaching != null)
            {
                _networkManager?.FinishInflightForDetachedFrame(detaching);
                _provisionalNetworkManager?.FinishInflightForDetachedFrame(detaching);
            }

            DropFrameContext(frameId);
            _frameManager.FrameDetachedFromTarget(frameId);
        }

        private void OnFrameNavigated(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            JsonElement frame = payload;
            if (payload.TryGetProperty("frame", out JsonElement nested) && nested.ValueKind == JsonValueKind.Object)
            {
                frame = nested;
            }

            string id = frame.TryGetProperty("id", out JsonElement idEl)
                ? idEl.GetString()
                : (payload.TryGetProperty("frameId", out JsonElement fidEl) ? fidEl.GetString() : null);
            DropFrameContext(id);
            string url = frame.TryGetProperty("url", out JsonElement urlEl) && urlEl.ValueKind == JsonValueKind.String
                ? urlEl.GetString()
                : string.Empty;
            if (frame.TryGetProperty("urlFragment", out JsonElement fragmentEl)
                && fragmentEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(fragmentEl.GetString()))
            {
                url += fragmentEl.GetString();
            }

            string name = frame.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : string.Empty;
            string parentId = frame.TryGetProperty("parentId", out JsonElement parentEl)
                ? parentEl.GetString()
                : (payload.TryGetProperty("parentFrameId", out JsonElement pfEl) ? pfEl.GetString() : null);

            _frameManager.FrameCommittedNavigation(id, url, name, parentId);
            if (!string.IsNullOrEmpty(id) && (_mainFrameId == null || id == _mainFrameId || id == _frameManager.MainFrame.FrameId))
            {
                _mainFrameId = _frameManager.MainFrame.FrameId;
                _mainFrameUrl = _frameManager.MainFrame.Url;
                if (!string.IsNullOrEmpty(_mainFrameUrl)
                    && !_mainFrameUrl.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    _hasCommittedNonInitialNavigation = true;
                }

                RecordLifecycle("commit");
                TaskCompletionSource<bool> commitTcs = null;
                lock (_navigationLock)
                {
                    string committedUrl = NavigationTimeout.WithoutHash(_mainFrameUrl);
                    if (!string.IsNullOrEmpty(_pendingRedirectTarget)
                        && string.Equals(
                            committedUrl,
                            NavigationTimeout.WithoutHash(_pendingRedirectTarget),
                            StringComparison.Ordinal))
                    {
                        _pendingNavigationUrl = committedUrl;
                        _pendingRedirectTarget = null;
                    }

                    string pendingUrl = NavigationTimeout.WithoutHash(_pendingNavigationUrl);
                    if (string.IsNullOrEmpty(pendingUrl)
                        || string.Equals(committedUrl, pendingUrl, StringComparison.Ordinal))
                    {
                        _pendingNavigationCommitted = true;
                        commitTcs = _pendingCommitTcs;
                        _pendingCommitTcs = null;
                    }
                    else if (!string.Equals(committedUrl, NavigationTimeout.WithoutHash(_navigationStartUrl), StringComparison.Ordinal))
                    {
                        _lastCompetingNavigationUrl = committedUrl;
                    }
                }

                commitTcs?.TrySetResult(true);
                if (!PopupOpenedHelper.IsBlankUrl(_mainFrameUrl))
                {
                    _firstNonInitialNavigationTcs.TrySetResult(true);
                }

                MarkReportAsNewNavigation(_mainFrameUrl);
            }
        }

        private void OnWillCheckNavigationPolicy(JsonElement? parameters)
        {
            if (_provisionalSession != null)
            {
                return;
            }

            string frameId = null;
            if (parameters.HasValue && parameters.Value.TryGetProperty("frameId", out JsonElement frameIdEl))
            {
                frameId = frameIdEl.GetString();
            }

            _frameManager.FrameRequestedNavigation(frameId);
        }

        private void OnDidCheckNavigationPolicy(JsonElement? parameters)
        {
            if (_provisionalSession != null)
            {
                return;
            }

            bool cancel = parameters.HasValue
                && parameters.Value.TryGetProperty("cancel", out JsonElement cancelEl)
                && cancelEl.ValueKind == JsonValueKind.True;
            if (!cancel)
            {
                return;
            }

            _frameManager.Signals.OnMainFrameNavigated();
        }

        private void OnNavigatedWithinDocument(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string frameId = payload.TryGetProperty("frameId", out JsonElement fidEl)
                ? fidEl.GetString()
                : string.Empty;
            string url = payload.TryGetProperty("url", out JsonElement urlEl) && urlEl.ValueKind == JsonValueKind.String
                ? urlEl.GetString()
                : string.Empty;
            if (payload.TryGetProperty("urlFragment", out JsonElement fragmentEl)
                && fragmentEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(fragmentEl.GetString()))
            {
                string fragment = fragmentEl.GetString();
                if (!url.Contains(fragment, StringComparison.Ordinal))
                {
                    url += fragment[0] == '#' ? fragment : "#" + fragment;
                }
            }

            if (string.IsNullOrEmpty(frameId) && string.IsNullOrEmpty(url))
            {
                return;
            }

            _frameManager.FrameCommittedSameDocumentNavigation(frameId, url ?? string.Empty);
            if (string.IsNullOrEmpty(frameId)
                || frameId == _mainFrameId
                || frameId == _frameManager.MainFrame.FrameId)
            {
                _mainFrameUrl = _frameManager.MainFrame.Url;
                TaskCompletionSource<bool> commitTcs;
                lock (_navigationLock)
                {
                    _pendingNavigationCommitted = true;
                    commitTcs = _pendingCommitTcs;
                    _pendingCommitTcs = null;
                }

                commitTcs?.TrySetResult(true);
            }
        }

        private void OnLoadEventFired(JsonElement? parameters)
        {
            string frameId = null;
            if (parameters.HasValue
                && parameters.Value.TryGetProperty("frameId", out JsonElement frameIdEl)
                && frameIdEl.ValueKind == JsonValueKind.String)
            {
                frameId = frameIdEl.GetString();
            }

            // WebKit fires Page.loadEventFired for every frame. IPage.Load is
            // main-frame only (page-event-load.spec.ts / playwright#15086).
            if (!string.IsNullOrEmpty(frameId)
                && frameId != _mainFrameId
                && frameId != _frameManager.MainFrame.FrameId)
            {
                return;
            }

            TaskCompletionSource<bool> tcs;
            lock (_navigationLock)
            {
                tcs = _pendingLoadTcs;
                _pendingLoadTcs = null;
            }

            // Fire the public event first so user handlers run before the awaiter
            // resuming on TrySetResult sees the task complete. Otherwise, the threadpool
            // continuation can resume the test's await before Load.Invoke finishes on
            // the transport thread — racing the assertion.
            Load?.Invoke(this, this);
            RecordLifecycle("load");
            tcs?.TrySetResult(true);
        }

        private void OnDomContentEventFired()
        {
            TaskCompletionSource<bool> tcs;
            lock (_navigationLock)
            {
                tcs = _pendingDomContentTcs;
                _pendingDomContentTcs = null;
            }

            DOMContentLoaded?.Invoke(this, this);
            RecordLifecycle("DOMContentLoaded");
            tcs?.TrySetResult(true);
        }

        private void OnExecutionContextCreated(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("context", out JsonElement context))
            {
                return;
            }

            // Official wkPage: type "normal" is main; type "user" + utility name
            // is the content-script world. Other worlds stay unadopted.
            string type = context.TryGetProperty("type", out JsonElement typeEl) && typeEl.ValueKind == JsonValueKind.String
                ? typeEl.GetString()
                : null;
            string worldName = context.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : null;

            bool isUtility = string.Equals(type, "user", StringComparison.Ordinal)
                && string.Equals(worldName, UtilityWorldName, StringComparison.Ordinal);
            if (type != null && type != "normal" && !isUtility)
            {
                return;
            }

            if (!context.TryGetProperty("id", out JsonElement idEl)
                || idEl.ValueKind != JsonValueKind.Number)
            {
                return;
            }

            int contextId = idEl.GetInt32();

            string frameId = context.TryGetProperty("frameId", out JsonElement frameIdEl)
                && frameIdEl.ValueKind == JsonValueKind.String
                ? frameIdEl.GetString()
                : null;

            if (string.IsNullOrEmpty(frameId))
            {
                frameId = _mainFrameId;
            }

            WKExecutionContext created = new WKExecutionContext(_targetSession, contextId);
            if (isUtility)
            {
                if (!string.IsNullOrEmpty(frameId)
                    && _utilityContexts.TryGetValue(frameId, out WKExecutionContext previousUtility)
                    && previousUtility != null
                    && previousUtility.ContextId != contextId)
                {
                    previousUtility.MarkDestroyed();
                }

                if (!string.IsNullOrEmpty(frameId))
                {
                    _utilityContexts[frameId] = created;
                }

                return;
            }

            if (!string.IsNullOrEmpty(frameId))
            {
                if (_frameContexts.TryGetValue(frameId, out WKExecutionContext previous)
                    && previous != null
                    && previous.ContextId != contextId)
                {
                    previous.MarkDestroyed();
                }

                _frameContexts[frameId] = created;
            }

            if (string.IsNullOrEmpty(frameId) || string.Equals(frameId, _mainFrameId, StringComparison.Ordinal))
            {
                if (_executionContext != null && _executionContext.ContextId != contextId)
                {
                    _executionContext.MarkDestroyed();
                }

                _executionContext = created;
            }
        }

        private void OnExecutionContextDestroyed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            int contextId;
            if (payload.TryGetProperty("executionContextId", out JsonElement idEl) && idEl.TryGetInt32(out contextId))
            {
            }
            else if (payload.TryGetProperty("id", out JsonElement altId) && altId.TryGetInt32(out contextId))
            {
            }
            else
            {
                return;
            }

            if (_executionContext != null && _executionContext.ContextId == contextId)
            {
                _executionContext.MarkDestroyed();
                _executionContext = null;
            }

            foreach (string key in _frameContexts.Keys)
            {
                if (_frameContexts.TryGetValue(key, out WKExecutionContext existing)
                    && existing != null
                    && existing.ContextId == contextId)
                {
                    existing.MarkDestroyed();
                    _frameContexts.TryRemove(key, out _);
                }
            }

            foreach (string key in _utilityContexts.Keys)
            {
                if (_utilityContexts.TryGetValue(key, out WKExecutionContext utility)
                    && utility != null
                    && utility.ContextId == contextId)
                {
                    utility.MarkDestroyed();
                    _utilityContexts.TryRemove(key, out _);
                }
            }
        }

        private void OnBindingCalled(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            // WebKit's bindingCalled payload is { contextId, name, argument } — the same shape as
            // Chromium's { executionContextId, name, payload } with different field names.
            string nameField = p.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            if (nameField != PageBindingScript.ChannelName)
            {
                return;
            }

            string argument = p.TryGetProperty("argument", out JsonElement argumentEl) ? argumentEl.GetString() : null;
            int contextId = p.TryGetProperty("contextId", out JsonElement ctxEl) && ctxEl.TryGetInt32(out int cid) ? cid : 0;
            if (contextId != 0)
            {
                _lastBindingContextId = contextId;
            }

            if (string.IsNullOrEmpty(argument))
            {
                return;
            }

            // JSON exposeFunction callbacks (used as addEventListener handlers) must run
            // before the triggering evaluate returns, matching official Playwright. Handle
            // bindings still hop off the transport thread so result delivery cannot deadlock.
            if (!TryDispatchJsonBinding(argument, contextId))
            {
                _ = Task.Run(() => DispatchBindingCallAsync(argument, contextId));
            }
        }

        private bool TryDispatchJsonBinding(string argument, int contextId)
        {
            long seq = 0;
            string functionName = null;
            JsonElement[] args;
            try
            {
                JsonElement root = JsonSerializer.Deserialize<JsonElement>(argument);
                functionName = root.GetProperty("name").GetString();
                seq = root.GetProperty("seq").GetInt64();

                bool isHandle = root.TryGetProperty("handle", out JsonElement handleFlag)
                    && handleFlag.ValueKind == JsonValueKind.True;
                if (isHandle)
                {
                    return false;
                }

                if (!PageBindingScript.TryReadSerializedArgs(root, clone: false, out args, out string argsError))
                {
                    _ = Task.Run(() => DeliverBindingErrorAsync(contextId, seq, argsError));
                    return true;
                }
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => DeliverBindingErrorAsync(contextId, seq, ex));
                return true;
            }

            try
            {
                if (_closed)
                {
                    return true;
                }

                if (!_exposedFunctions.TryGetValue(functionName ?? string.Empty, out Func<JsonElement[], Task<object>> handler))
                {
                    _ = Task.Run(() => DeliverBindingErrorAsync(contextId, seq, $"No handler registered for '{functionName}'"));
                    return true;
                }

                Task<object> invoked = CoalesceBindingInvocationAsync(argument, args, handler);
                _ = Task.Run(() => DeliverInvokedBindingAsync(invoked, contextId, seq));
                return true;
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => DeliverBindingErrorAsync(contextId, seq, ex));
                return true;
            }
        }

        private Task<object> CoalesceBindingInvocationAsync(
            string argument,
            JsonElement[] args,
            Func<JsonElement[], Task<object>> handler)
        {
            // After a WebKit process-swap, one page-side call can emit two
            // Runtime.bindingCalled events with the same seq envelope. Key
            // the payload (name + seq + args) so legitimate repeats of the
            // same exposeFunction — official clock timers — still run.
            string key = argument ?? string.Empty;
            long now = DateTime.UtcNow.Ticks;
            if (_recentBindingInvocations.TryGetValue(key, out (long Ticks, Task<object> Task) recent)
                && now - recent.Ticks < TimeSpan.FromMilliseconds(100).Ticks)
            {
                return recent.Task;
            }

            Task<object> invoked = handler(args);
            _recentBindingInvocations[key] = (now, invoked);
            if (_recentBindingInvocations.Count > 64)
            {
                foreach (KeyValuePair<string, (long Ticks, Task<object> Task)> entry in _recentBindingInvocations)
                {
                    if (now - entry.Value.Ticks >= TimeSpan.FromMilliseconds(100).Ticks)
                    {
                        _recentBindingInvocations.TryRemove(entry.Key, out _);
                    }
                }
            }

            return invoked;
        }

        private async Task DeliverInvokedBindingAsync(Task<object> invoked, int contextId, long seq)
        {
            try
            {
                object result = await invoked.ConfigureAwait(false);
                await DeliverBindingResultAsync(contextId, seq, result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DeliverBindingErrorAsync(contextId, seq, ex).ConfigureAwait(false);
            }
        }

        private async Task DispatchBindingCallAsync(string argument, int contextId)
        {
            long seq = 0;
            string functionName = null;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(argument);
                JsonElement root = doc.RootElement;
                functionName = root.GetProperty("name").GetString();
                seq = root.GetProperty("seq").GetInt64();

                bool isHandle = root.TryGetProperty("handle", out JsonElement handleFlag)
                    && handleFlag.ValueKind == JsonValueKind.True;
                if (isHandle)
                {
                    if (!_handleBindings.TryGetValue(functionName ?? string.Empty, out Func<IJSHandle, Task<object>> handleHandler))
                    {
                        await DeliverBindingErrorAsync(contextId, seq, $"No handler registered for '{functionName}'").ConfigureAwait(false);
                        return;
                    }

                    WKTargetSession target = _targetSession
                        ?? throw new PlaywrightNativeException("Inner target session is not yet available.");
                    WKExecutionContext context = new WKExecutionContext(target, contextId);
                    JsonElement? handleValue = await context.EvaluateHandleAsync(PageBindingScript.TakeHandleExpression(seq)).ConfigureAwait(false);
                    IJSHandle jsHandle = WrapRemoteObject(context, handleValue);
                    object handleResult = await handleHandler(jsHandle).ConfigureAwait(false);
                    await DeliverBindingResultAsync(contextId, seq, handleResult).ConfigureAwait(false);
                    return;
                }

                if (!PageBindingScript.TryReadSerializedArgs(root, clone: true, out JsonElement[] args, out string argsError))
                {
                    await DeliverBindingErrorAsync(contextId, seq, argsError).ConfigureAwait(false);
                    return;
                }

                if (!_exposedFunctions.TryGetValue(functionName ?? string.Empty, out Func<JsonElement[], Task<object>> handler))
                {
                    await DeliverBindingErrorAsync(contextId, seq, $"No handler registered for '{functionName}'").ConfigureAwait(false);
                    return;
                }

                object result = await handler(args).ConfigureAwait(false);
                await DeliverBindingResultAsync(contextId, seq, result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DeliverBindingErrorAsync(contextId, seq, ex).ConfigureAwait(false);
            }
        }

        private async Task DeliverBindingResultAsync(int contextId, long seq, object result)
        {
            PageBindingResult.TryExtractHandles(result, out object tree, out List<object> extracted);
            tree = PageBindingResult.InlineImmediateHandles(tree, extracted, out List<object> handles);
            if (handles.Count > 0)
            {
                WKTargetSession target = _targetSession;
                if (target != null)
                {
                    WKExecutionContext context = new WKExecutionContext(target, contextId);
                    for (int i = 0; i < handles.Count; i++)
                    {
                        if (handles[i] is WKJSHandle handle && !string.IsNullOrEmpty(handle.ObjectId))
                        {
                            await context.EvaluateFunctionOnHandleAsync<object>(
                                handle.ObjectId,
                                PageBindingScript.ParkHandleFunction,
                                i).ConfigureAwait(false);
                        }
                    }

                    await context.EvaluateAsync<object>(
                        PageBindingScript.DeliverParkedHandlesExpression(seq, JsonSerializer.Serialize(tree)))
                        .ConfigureAwait(false);
                    return;
                }
            }

            await DeliverBindingAsync(contextId, new { seq, result = tree }).ConfigureAwait(false);
        }

        private Task DeliverBindingErrorAsync(int contextId, long seq, Exception error)
            => DeliverBindingAsync(contextId, new { seq, error = PageBindingResult.FormatError(error) });

        private Task DeliverBindingErrorAsync(int contextId, long seq, string error)
            => DeliverBindingAsync(contextId, new { seq, error });

        private async Task DeliverBindingAsync(int contextId, object envelope)
        {
            WKTargetSession target = _targetSession;
            if (target == null)
            {
                return;
            }

            string json = JsonSerializer.Serialize(envelope);
            try
            {
                await target.SendAsync("Runtime.evaluate", new
                {
                    expression = $"globalThis.__pw_binding_deliver__({json})",
                    contextId,
                    returnByValue = true,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Best-effort delivery — the execution context may have been destroyed by a
                // navigation between the call and the response.
            }
        }

        private async Task<T> RunAndWaitInternalAsync<T>(Func<Task> action, Task<T> waitTask)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Task actionTask = action();
            T result = await waitTask.ConfigureAwait(false);
            await actionTask.ConfigureAwait(false);
            return result;
        }

        private Task<IRequest> WaitForRequestFinishedInternalAsync(
            string urlString,
            Regex urlRegex,
            Func<IRequest, bool> predicate,
            float? timeout)
            => WaitForEventAsync(
                PageEvent.RequestFinished,
                r => predicate != null
                    ? predicate(r)
                    : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, NavigationUrl.ContextBase(Context)),
                timeout);

        private Task DispatchEventInternalAsync(string selector, string type, object eventInit, float? timeout, bool? strict)
            => DispatchEventAction.RunAsync(
                EvaluateDispatchBoolAsync,
                selector,
                type,
                eventInit,
                timeout,
                strict ?? (Context is IHasStrictSelectors s && s.StrictSelectors),
                "page.dispatchEvent");

        private Task<bool> EvaluateDispatchBoolAsync(string script, object arg)
            => arg == null
                ? EvaluateSerializedAsync<bool>(script)
                : EvaluateFunctionSerializedAsync<bool>(script, arg);

        private Task<IElementHandle> QueryActionAsync(string selector)
            => QueryActionAsync(selector, default);

        private Task<IElementHandle> QueryActionAsync(string selector, bool? strict)
            => StrictSelector.QueryAsync(
                QuerySelectorAsync,
                QuerySelectorAllAsync,
                selector,
                strict ?? (Context is IHasStrictSelectors s && s.StrictSelectors));

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IPage.AddLocatorHandlerAsync(ILocator locator, Func<ILocator, Task> handler, PageAddLocatorHandlerOptions options) => Task.CompletedTask;

        Task IPage.AddLocatorHandlerAsync(ILocator locator, Func<Task> handler, PageAddLocatorHandlerOptions options) => Task.CompletedTask;

        Task<IElementHandle> IPage.AddScriptTagAsync(PageAddScriptTagOptions options)
            => AddScriptTagAsync(options?.Url, options?.Path, options?.Content, options?.Type);

        Task<IElementHandle> IPage.AddStyleTagAsync(PageAddStyleTagOptions options)
            => AddStyleTagAsync(options?.Url, options?.Path, options?.Content);

        Task<string> IPage.AriaSnapshotAsync(PageAriaSnapshotOptions options) => Task.FromResult<string>(default!);

        Task IPage.CancelPickLocatorAsync() => Task.CompletedTask;

        Task IPage.CheckAsync(string selector, PageCheckOptions options)
            => CheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.ClickAsync(string selector, PageClickOptions options)
        {
            PageClickOptions o = options;
            return ClickAsync(selector, o?.Button ?? default, o?.ClickCount, o?.Delay, o?.Position, o?.Modifiers, o?.Force, o?.NoWaitAfter, o?.Timeout, o?.Trial, default, null, o?.Strict);
        }

        Task IPage.CloseAsync(PageCloseOptions options)
        {
            return CloseAsync(options?.RunBeforeUnload, options?.Reason);
        }

        Task<IReadOnlyList<IConsoleMessage>> IPage.ConsoleMessagesAsync(PageConsoleMessagesOptions options)
            => ConsoleMessagesAsync(options?.Filter ?? ConsoleMessagesFilter.SinceNavigation);

        Task IPage.DblClickAsync(string selector, PageDblClickOptions options)
            => DblClickAsync(selector, options?.Button ?? default, options?.Delay, options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.DispatchEventAsync(string selector, string type, object eventInit, PageDispatchEventOptions options)
            => DispatchEventInternalAsync(selector, type, eventInit, options?.Timeout, options?.Strict);

        Task IPage.DragAndDropAsync(string source, string target, PageDragAndDropOptions options)
            => DragAndDropAsync(
                source,
                target,
                options?.SourcePosition == null ? null : new Position { X = options.SourcePosition.X, Y = options.SourcePosition.Y },
                options?.TargetPosition == null ? null : new Position { X = options.TargetPosition.X, Y = options.TargetPosition.Y },
                options?.Force,
                options?.NoWaitAfter,
                options?.Timeout,
                options?.Trial,
                options?.Steps,
                ActionScrollBridge.FromScrollOption(options?.Scroll),
                options?.Strict);

        async Task IPage.EmulateMediaAsync(PageEmulateMediaOptions options)
        {
            if (options == null)
            {
                return;
            }

            await EmulateMediaAsync(options.Media, options.ColorScheme).ConfigureAwait(false);
            await EmulateMediaAsync(options.ReducedMotion, options.ForcedColors, options.Contrast).ConfigureAwait(false);
        }

        async Task<JsonElement?> IPage.EvalOnSelectorAllAsync(string selector, string expression, object arg)
            => await EvalOnSelector.OnArrayAsync<JsonElement?>(
                EvaluateHandleAsync(EvalOnSelector.DocumentQuerySelectorAllExpression(selector)),
                expression,
                arg).ConfigureAwait(false);

        Task<T> IPage.EvalOnSelectorAllAsync<T>(string selector, string expression, object arg)
            => EvalOnSelector.OnArrayAsync<T>(
                EvaluateHandleAsync(EvalOnSelector.DocumentQuerySelectorAllExpression(selector)),
                expression,
                arg);

        Task<JsonElement?> IPage.EvalOnSelectorAsync(string selector, string expression, object arg)
            => EvalOnSelector.OnHandleAsync<JsonElement?>(QuerySelectorAsync(selector), selector, expression, arg, "page.$eval");

        Task<T> IPage.EvalOnSelectorAsync<T>(string selector, string expression, object arg, PageEvalOnSelectorOptions options)
            => EvalOnSelector.OnHandleAsync<T>(
                QueryActionAsync(selector, options?.Strict),
                selector,
                expression,
                arg,
                "page.$eval");

        Task<IAsyncDisposable> IPage.ExposeBindingAsync(string name, Action callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.ExposeBindingAsync(string name, Action<BindingSource> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.ExposeBindingAsync<T>(string name, Action<BindingSource, T> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.ExposeBindingAsync<T1, T2, T3, TResult>(string name, Func<BindingSource, T1, T2, T3, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.ExposeBindingAsync<T1, T2, T3, T4, TResult>(string name, Func<BindingSource, T1, T2, T3, T4, TResult> callback) => Task.FromResult<IAsyncDisposable>(default!);

        Task IPage.FillAsync(string selector, string value, PageFillOptions options)
            => FillAsync(selector, value, options?.NoWaitAfter, options?.Timeout, options?.Force, default, options?.Strict);

        Task IPage.FocusAsync(string selector, PageFocusOptions options)
            => FocusAsync(selector, options?.Timeout, default, options?.Strict);

        IFrame IPage.Frame(string name) => FrameLookup.ByName(Frames, name);

        IFrame IPage.FrameByUrl(string url) => FrameByUrl(url, null, null);

        IFrame IPage.FrameByUrl(Regex url) => FrameByUrl(null, url, null);

        IFrame IPage.FrameByUrl(Func<string, bool> url) => FrameByUrl(null, null, url);

        IFrameLocator IPage.FrameLocator(string selector) => new FrameLocator(MainFrame, selector);

        Task<string> IPage.GetAttributeAsync(string selector, string name, PageGetAttributeOptions options)
            => GetAttributeAsync(selector, name, options?.Timeout, options?.Strict);

        ILocator IPage.GetByAltText(string text, PageGetByAltTextOptions options)
            => Locator.FromScript(MainFrame, GetByAllScript.FindAllByAttribute, "alt", text, options?.Exact ?? false);

        ILocator IPage.GetByAltText(Regex text, PageGetByAltTextOptions options)
            => Locator.FromScript(
                MainFrame,
                GetByAllScript.FindAllByAttributeRegex,
                "alt",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IPage.GetByLabel(string text, PageGetByLabelOptions options)
            => Locator.FromScript(MainFrame, GetByAllScript.FindAllByLabel, text, options?.Exact ?? false);

        ILocator IPage.GetByLabel(Regex text, PageGetByLabelOptions options)
            => Locator.FromScript(
                MainFrame,
                GetByAllScript.FindAllByLabelRegex,
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IPage.GetByPlaceholder(string text, PageGetByPlaceholderOptions options)
            => Locator.FromScript(MainFrame, GetByAllScript.FindAllByAttribute, "placeholder", text, options?.Exact ?? false);

        ILocator IPage.GetByPlaceholder(Regex text, PageGetByPlaceholderOptions options)
            => Locator.FromScript(
                MainFrame,
                GetByAllScript.FindAllByAttributeRegex,
                "placeholder",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IPage.GetByRole(AriaRole role, PageGetByRoleOptions options)
            => new Locator(MainFrame, RoleSelector.Build(
                role.ToRoleString(),
                options?.Name ?? options?.NameString,
                options?.Exact,
                options?.Checked,
                options?.Disabled,
                options?.Expanded,
                options?.IncludeHidden,
                options?.Level,
                options?.Pressed,
                options?.Selected,
                options?.Description ?? options?.DescriptionString,
                options?.DescriptionRegex,
                options?.NameRegex));

        ILocator IPage.GetByTestId(string testId) => new Locator(MainFrame, GetBySelectorScript.TestIdSelector(testId));

        ILocator IPage.GetByTestId(Regex testId)
            => Locator.FromScript(
                MainFrame,
                GetByAllScript.FindAllByAttributeRegex,
                GetBySelectorScript.TestIdAttributeName(),
                GetByAllScript.Pattern(testId),
                GetByAllScript.Flags(testId));

        ILocator IPage.GetByText(string text, PageGetByTextOptions options)
            => Locator.FromScript(MainFrame, GetByAllScript.FindAllByText, text, options?.Exact ?? false);

        ILocator IPage.GetByText(Regex text, PageGetByTextOptions options)
            => Locator.FromScript(
                MainFrame,
                GetByAllScript.FindAllByTextRegex,
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IPage.GetByTitle(string text, PageGetByTitleOptions options)
            => Locator.FromScript(MainFrame, GetByAllScript.FindAllByAttribute, "title", text, options?.Exact ?? false);

        ILocator IPage.GetByTitle(Regex text, PageGetByTitleOptions options)
            => Locator.FromScript(
                MainFrame,
                GetByAllScript.FindAllByAttributeRegex,
                "title",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        Task<IResponse> IPage.GoBackAsync(PageGoBackOptions options)
            => GoBackAsync(options?.WaitUntil ?? default, options?.Timeout);

        Task<IResponse> IPage.GoForwardAsync(PageGoForwardOptions options)
            => GoForwardAsync(options?.WaitUntil ?? default, options?.Timeout);

        Task<IResponse> IPage.GotoAsync(string url, PageGotoOptions options)
            => GoToAsync(url, options?.WaitUntil ?? default, options?.Timeout, options?.Referer);

        Task IPage.HideHighlightAsync() => Task.CompletedTask;

        Task IPage.HoverAsync(string selector, PageHoverOptions options)
            => HoverAsync(selector, options?.Position, options?.Modifiers, options?.Force, options?.Timeout, options?.Trial, default, options?.Strict);

        Task<string> IPage.InnerHTMLAsync(string selector, PageInnerHTMLOptions options)
            => InnerHTMLAsync(selector, options?.Timeout, options?.Strict);

        Task<string> IPage.InnerTextAsync(string selector, PageInnerTextOptions options)
            => InnerTextAsync(selector, options?.Timeout, options?.Strict);

        Task<string> IPage.InputValueAsync(string selector, PageInputValueOptions options)
            => EvalOnSelector.OnHandleAsync<string>(
                QueryActionAsync(selector, options?.Strict),
                selector,
                ElementStateScript.InputValueFunction,
                null,
                "page.inputValue");

        Task<bool> IPage.IsCheckedAsync(string selector, PageIsCheckedOptions options)
            => IsCheckedAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IPage.IsDisabledAsync(string selector, PageIsDisabledOptions options)
            => IsDisabledAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IPage.IsEditableAsync(string selector, PageIsEditableOptions options)
            => IsEditableAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IPage.IsEnabledAsync(string selector, PageIsEnabledOptions options)
            => IsEnabledAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IPage.IsHiddenAsync(string selector, PageIsHiddenOptions options)
            => IsHiddenAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IPage.IsVisibleAsync(string selector, PageIsVisibleOptions options)
            => IsVisibleAsync(selector, options?.Timeout, options?.Strict);

        ILocator IPage.Locator(string selector, PageLocatorOptions options)
        {
            ILocator result = new Locator(MainFrame, selector);
            options ??= new PageLocatorOptions();
            return SelectorQuery.ApplyOptions(
                result,
                options.Has,
                options.HasText ?? options.HasTextString,
                options.HasTextRegex,
                options.HasNot,
                options.HasNotText ?? options.HasNotTextString,
                options.HasNotTextRegex);
        }

        Task IPage.PauseAsync() => Task.CompletedTask;

        Task<byte[]> IPage.PdfAsync(PagePdfOptions options) => Task.FromResult<byte[]>(default!);

        Task<ILocator> IPage.PickLocatorAsync() => Task.FromResult<ILocator>(default!);

        Task IPage.PressAsync(string selector, string key, PagePressOptions options)
            => PressAsync(selector, key, options?.Delay, options?.NoWaitAfter, options?.Timeout, null, default, options?.Strict);

        Task<IElementHandle> IPage.QuerySelectorAsync(string selector, PageQuerySelectorOptions options)
            => QueryActionAsync(selector, options?.Strict);

        Task<IResponse> IPage.ReloadAsync(PageReloadOptions options)
            => ReloadAsync(options?.WaitUntil ?? default, options?.Timeout);

        Task IPage.RemoveLocatorHandlerAsync(ILocator locator) => Task.CompletedTask;

        Task<IAsyncDisposable> IPage.RouteAsync(string url, Action<IRoute> handler, PageRouteOptions options)
            => RegisterRouteAsync(() => RouteAsync(url, handler, options?.Times));

        Task<IAsyncDisposable> IPage.RouteAsync(Regex url, Action<IRoute> handler, PageRouteOptions options)
            => RegisterRouteAsync(() => RouteAsync(url, handler, options?.Times));

        Task<IAsyncDisposable> IPage.RouteAsync(Func<string, bool> url, Action<IRoute> handler, PageRouteOptions options)
            => RegisterRouteAsync(() => RouteAsync(url, handler, options?.Times));

        Task<IAsyncDisposable> IPage.RouteAsync(string url, Func<IRoute, Task> handler, PageRouteOptions options)
            => RegisterRouteAsync(() => RouteAsync(url, handler, options?.Times));

        Task<IAsyncDisposable> IPage.RouteAsync(Regex url, Func<IRoute, Task> handler, PageRouteOptions options)
            => RegisterRouteAsync(() => RouteAsync(url, handler, options?.Times));

        Task<IAsyncDisposable> IPage.RouteAsync(Func<string, bool> url, Func<IRoute, Task> handler, PageRouteOptions options)
            => RegisterRouteAsync(() => RouteAsync(url, handler, options?.Times));

        Task IPage.RouteFromHARAsync(string har, PageRouteFromHAROptions options) => Task.CompletedTask;

        Task IPage.RouteWebSocketAsync(string url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IPage.RouteWebSocketAsync(Regex url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IPage.RouteWebSocketAsync(Func<string, bool> url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task<IConsoleMessage> IPage.RunAndWaitForConsoleMessageAsync(Func<Task> action, PageRunAndWaitForConsoleMessageOptions options) => Task.FromResult<IConsoleMessage>(default!);

        Task<IDownload> IPage.RunAndWaitForDownloadAsync(Func<Task> action, PageRunAndWaitForDownloadOptions options) => Task.FromResult<IDownload>(default!);

        Task<IFileChooser> IPage.RunAndWaitForFileChooserAsync(Func<Task> action, PageRunAndWaitForFileChooserOptions options) => Task.FromResult<IFileChooser>(default!);

        Task<IResponse> IPage.RunAndWaitForNavigationAsync(Func<Task> action, PageRunAndWaitForNavigationOptions options) => Task.FromResult<IResponse>(default!);

        Task<IPage> IPage.RunAndWaitForPopupAsync(Func<Task> action, PageRunAndWaitForPopupOptions options)
            => RunAndWaitInternalAsync(
                action,
                WaitForEventAsync(PageEvent.Popup, options?.Predicate, options?.Timeout));

        Task<IRequest> IPage.RunAndWaitForRequestAsync(Func<Task> action, string urlOrPredicate, PageRunAndWaitForRequestOptions options)
            => RunAndWaitInternalAsync(action, WaitForRequestAsync(urlOrPredicate, null, null, options?.Timeout));

        Task<IRequest> IPage.RunAndWaitForRequestAsync(Func<Task> action, Regex urlOrPredicate, PageRunAndWaitForRequestOptions options)
            => RunAndWaitInternalAsync(action, WaitForRequestAsync(null, urlOrPredicate, null, options?.Timeout));

        Task<IRequest> IPage.RunAndWaitForRequestAsync(Func<Task> action, Func<IRequest, bool> urlOrPredicate, PageRunAndWaitForRequestOptions options)
            => RunAndWaitInternalAsync(action, WaitForRequestAsync(null, null, urlOrPredicate, options?.Timeout));

        Task<IRequest> IPage.RunAndWaitForRequestFinishedAsync(Func<Task> action, PageRunAndWaitForRequestFinishedOptions options)
            => RunAndWaitInternalAsync(action, WaitForRequestFinishedInternalAsync(null, null, options?.Predicate, options?.Timeout));

        Task<IResponse> IPage.RunAndWaitForResponseAsync(Func<Task> action, string urlOrPredicate, PageRunAndWaitForResponseOptions options)
            => RunAndWaitInternalAsync(action, WaitForResponseAsync(urlOrPredicate, null, null, options?.Timeout));

        Task<IResponse> IPage.RunAndWaitForResponseAsync(Func<Task> action, Regex urlOrPredicate, PageRunAndWaitForResponseOptions options)
            => RunAndWaitInternalAsync(action, WaitForResponseAsync(null, urlOrPredicate, null, options?.Timeout));

        Task<IResponse> IPage.RunAndWaitForResponseAsync(Func<Task> action, Func<IResponse, bool> urlOrPredicate, PageRunAndWaitForResponseOptions options)
            => RunAndWaitInternalAsync(action, WaitForResponseAsync(null, null, urlOrPredicate, options?.Timeout));

        Task<IWebSocket> IPage.RunAndWaitForWebSocketAsync(Func<Task> action, PageRunAndWaitForWebSocketOptions options) => Task.FromResult<IWebSocket>(default!);

        Task<IWorker> IPage.RunAndWaitForWorkerAsync(Func<Task> action, PageRunAndWaitForWorkerOptions options) => Task.FromResult<IWorker>(default!);

        Task<byte[]> IPage.ScreenshotAsync(PageScreenshotOptions options)
            => ScreenshotAsync(
                options?.Path,
                options?.Type ?? default,
                options?.Quality,
                options?.FullPage,
                options?.Clip,
                options?.OmitBackground,
                options?.Timeout,
                options?.Scale?.ToString(),
                options?.Animations?.ToString(),
                options?.Caret?.ToString(),
                options?.Style,
                options?.Mask,
                options?.MaskColor);

        Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, string values, PageSelectOptionOptions options)
            => AsReadOnlyListAsync(SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Force, options?.Strict));

        Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IElementHandle values, PageSelectOptionOptions options)
            => AsReadOnlyListAsync(SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force));

        Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IEnumerable<string> values, PageSelectOptionOptions options)
            => AsReadOnlyListAsync(SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force));

        Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, SelectOptionValue values, PageSelectOptionOptions options)
            => AsReadOnlyListAsync(SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force));

        Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, PageSelectOptionOptions options)
            => AsReadOnlyListAsync(SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force));

        Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, PageSelectOptionOptions options)
            => AsReadOnlyListAsync(SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Force, default, options?.Strict));

        Task IPage.SetCheckedAsync(string selector, bool checkedState, PageSetCheckedOptions options)
            => checkedState
                ? CheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict)
                : UncheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.SetContentAsync(string html, PageSetContentOptions options)
            => SetContentAsync(html, options?.Timeout, options?.WaitUntil ?? default);

        void IPage.SetDefaultNavigationTimeout(float timeout)
        {
            DefaultNavigationTimeout = timeout;
        }

        void IPage.SetDefaultTimeout(float timeout)
        {
            DefaultTimeout = timeout;
        }

        Task IPage.SetExtraHTTPHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
            => SetExtraHttpHeadersAsync(headers);

        Task IPage.SetInputFilesAsync(string selector, string files, PageSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, options?.Strict);

        Task IPage.SetInputFilesAsync(string selector, IEnumerable<string> files, PageSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, options?.Strict);

        Task IPage.SetInputFilesAsync(string selector, FilePayload files, PageSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, options?.Strict);

        Task IPage.SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, PageSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, default, default, options?.Strict);

        Task IPage.TapAsync(string selector, PageTapOptions options)
            => TapAsync(selector, options?.Position, options?.Modifiers, options?.NoWaitAfter, options?.Force, options?.Timeout, options?.Trial, default, options?.Strict);

        Task<string> IPage.TextContentAsync(string selector, PageTextContentOptions options)
            => TextContentAsync(selector, options?.Timeout, options?.Strict);

        Task IPage.TypeAsync(string selector, string text, PageTypeOptions options)
            => TypeAsync(selector, text, options?.Delay, options?.NoWaitAfter, options?.Timeout, null, default, options?.Strict);

        Task IPage.UncheckAsync(string selector, PageUncheckOptions options)
            => UncheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.UnrouteAllAsync(PageUnrouteAllOptions options)
            => UnrouteAllAsync();

        Task IPage.UnrouteAsync(string url, Action<IRoute> handler)
            => UnrouteAsync(url, handler);

        Task IPage.UnrouteAsync(Regex url, Action<IRoute> handler)
            => UnrouteAsync(url, handler);

        Task IPage.UnrouteAsync(Func<string, bool> url, Action<IRoute> handler)
            => UnrouteAsync(url, handler);

        Task IPage.UnrouteAsync(string url, Func<IRoute, Task> handler)
            => UnrouteAsync(url, handler);

        Task IPage.UnrouteAsync(Regex url, Func<IRoute, Task> handler)
            => UnrouteAsync(url, handler);

        Task IPage.UnrouteAsync(Func<string, bool> url, Func<IRoute, Task> handler)
            => UnrouteAsync(url, handler);

        Task<IConsoleMessage> IPage.WaitForConsoleMessageAsync(PageWaitForConsoleMessageOptions options) => Task.FromResult<IConsoleMessage>(default!);

        Task<IDownload> IPage.WaitForDownloadAsync(PageWaitForDownloadOptions options)
            => WaitForDownloadAsync(options?.Timeout);

        Task<IFileChooser> IPage.WaitForFileChooserAsync(PageWaitForFileChooserOptions options)
            => WaitForFileChooserAsync(options?.Timeout);

        Task<IJSHandle> IPage.WaitForFunctionAsync(string expression, object arg, PageWaitForFunctionOptions options)
            => WaitForFunctionAsync(expression, arg, options?.PollingInterval, options?.Timeout);

        Task IPage.WaitForLoadStateAsync(LoadState? state, PageWaitForLoadStateOptions options)
        {
            PageWaitForLoadStateOptions o = options;
            return WaitForLoadStateAsync(state ?? LoadState.Load, o?.Timeout);
        }

        Task<IResponse> IPage.WaitForNavigationAsync(PageWaitForNavigationOptions options)
            => WaitForNavigationAsync(options?.Url, null, null, options?.Timeout, options?.WaitUntil ?? default);

        Task<IPage> IPage.WaitForPopupAsync(PageWaitForPopupOptions options)
            => WaitForEventAsync(PageEvent.Popup, options?.Predicate, options?.Timeout);

        Task<IRequest> IPage.WaitForRequestAsync(string urlOrPredicate, PageWaitForRequestOptions options)
            => WaitForRequestAsync(urlOrPredicate, null, null, options?.Timeout);

        Task<IRequest> IPage.WaitForRequestAsync(Regex urlOrPredicate, PageWaitForRequestOptions options)
            => WaitForRequestAsync(null, urlOrPredicate, null, options?.Timeout);

        Task<IRequest> IPage.WaitForRequestAsync(Func<IRequest, bool> urlOrPredicate, PageWaitForRequestOptions options)
            => WaitForRequestAsync(null, null, urlOrPredicate, options?.Timeout);

        Task<IRequest> IPage.WaitForRequestFinishedAsync(PageWaitForRequestFinishedOptions options)
            => WaitForRequestFinishedInternalAsync(null, null, options?.Predicate, options?.Timeout);

        Task<IResponse> IPage.WaitForResponseAsync(string urlOrPredicate, PageWaitForResponseOptions options)
            => WaitForResponseAsync(urlOrPredicate, null, null, options?.Timeout);

        Task<IResponse> IPage.WaitForResponseAsync(Regex urlOrPredicate, PageWaitForResponseOptions options)
            => WaitForResponseAsync(null, urlOrPredicate, null, options?.Timeout);

        Task<IResponse> IPage.WaitForResponseAsync(Func<IResponse, bool> urlOrPredicate, PageWaitForResponseOptions options)
            => WaitForResponseAsync(null, null, urlOrPredicate, options?.Timeout);

        Task<IElementHandle> IPage.WaitForSelectorAsync(string selector, PageWaitForSelectorOptions options)
            => WaitForSelectorAsync(selector, options?.State ?? WaitForSelectorState.Visible, options?.Timeout, options?.Strict);

        Task IPage.WaitForURLAsync(string url, PageWaitForURLOptions options)
            => WaitForURLAsync(url, null, null, options?.Timeout, options?.WaitUntil ?? default);

        Task IPage.WaitForURLAsync(Regex url, PageWaitForURLOptions options)
            => WaitForURLAsync(null, url, null, options?.Timeout, options?.WaitUntil ?? default);

        Task IPage.WaitForURLAsync(Func<string, bool> url, PageWaitForURLOptions options)
            => WaitForURLAsync(null, null, url, options?.Timeout, options?.WaitUntil ?? default);

        Task<IWebSocket> IPage.WaitForWebSocketAsync(PageWaitForWebSocketOptions options) => Task.FromResult<IWebSocket>(default!);

        Task<IWorker> IPage.WaitForWorkerAsync(PageWaitForWorkerOptions options) => Task.FromResult<IWorker>(default!);

        private static async Task<IReadOnlyList<string>> AsReadOnlyListAsync(Task<IReadOnlyCollection<string>> task)
        {
            IReadOnlyCollection<string> result = await task.ConfigureAwait(false);
            if (result is IReadOnlyList<string> list)
            {
                return list;
            }

            return result == null ? Array.Empty<string>() : new List<string>(result);
        }

        private static async Task<IAsyncDisposable> RegisterRouteAsync(Func<Task> register)
        {
            await register().ConfigureAwait(false);
            return NoopAsyncDisposable.Instance;
        }

        private sealed class NoopAsyncDisposable : IAsyncDisposable
        {
            internal static readonly NoopAsyncDisposable Instance = new();

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
