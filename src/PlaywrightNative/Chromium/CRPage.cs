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
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Chromium page delegate owned by shared <see cref="Page"/>. Speaks CDP for
    /// one target; the public <see cref="Microsoft.Playwright.IPage"/> surface lives
    /// on <see cref="Page"/>, matching Node <c>CRPage</c> / <c>PageDelegate</c>.
    /// </summary>
    internal class CRPage : IPageDelegate
    {
        private readonly CRSession _client;
        private readonly string _targetId;
        private readonly CRBrowser _browser;
        private readonly ConcurrentDictionary<int, CRExecutionContext> _contextIdToContext = new();
        private readonly TaskCompletionSource<bool> _initializationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstNonInitialNavigationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstNonBlankNavigationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly FrameManager _frameManager;
        private readonly CRNetworkManager _networkManager;
        private readonly Input.Keyboard _keyboard;
        private readonly Input.Mouse _mouse;
        private readonly Input.Touchscreen _touchscreen;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, Func<JsonElement[], Task<object>>> _exposedFunctions = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _evaluateCallbackNames = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Func<CRJSHandle, Task<object>>> _handleBindings = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, CRWorker> _workers = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, CRSession> _oopifSessions = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _oopifParents = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _oopifSwappedIn = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _oopifOwnedWorkers = new(StringComparer.Ordinal);
        private readonly List<string> _initScriptSources = new();
        private readonly object _exposeSync = new();
        private readonly object _initScriptSync = new();
        private readonly Queue<string[]> _windowOpenFeatures = new();
        private readonly string _utilityWorldName;
        private string _userAgentOverride;
        private string _acceptLanguageOverride;
        private bool _userAgentIsMobile;
        private bool _userAgentIncludeMetadata;
        private bool _hasUserAgentOverride;
        private string _localeOverride;
        private string _timezoneOverride;
        private bool _offline;
        private bool _touchEnabled;
        private string _touchConfiguration;
        private bool _bindingInfrastructureInstalled;
        private CRSession _lastBindingSession;
        private Task _bindingReady;
        private int _lastBindingContextId;
        private int _reportedAsNew;
        private bool _debuggerResumed;
        private bool _crashed;
        private string _emulatedMedia = string.Empty;
        private string _emulatedColorScheme = "light";
        private string _emulatedReducedMotion = "no-preference";
        private string _emulatedForcedColors = "none";
        private string _emulatedContrast = "no-preference";

        /// <summary>
        /// Initializes a new instance of the <see cref="CRPage"/> class.
        /// </summary>
        /// <param name="client">The CDP session associated with this page target.</param>
        /// <param name="targetId">The CDP target identifier for this page.</param>
        /// <param name="browser">The parent <see cref="CRBrowser"/> instance.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public CRPage(CRSession client, string targetId, CRBrowser browser, ILoggerFactory loggerFactory = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _targetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _logger = loggerFactory?.CreateLogger<CRPage>();
            _utilityWorldName = "__playwright_utility_world_" + targetId;

            // Create the frame manager which owns the main frame.
            _frameManager = new FrameManager(targetId);

            // Create the network manager eagerly so routes registered on the
            // owning context can be propagated at CRBrowserContext.AddPage time,
            // which runs before InitializeAsync. The constructor only subscribes
            // to the session's MessageReceived event; no CDP calls happen here.
            _networkManager = new CRNetworkManager(_client, this);
            _ = _client.SendAsync("Network.enable");

            // Create the input simulators. The keyboard is shared with mouse/touch so
            // that chorded inputs (e.g. Shift+Click) pick up currently-pressed modifiers.
            _keyboard = new Input.Keyboard(new CRRawKeyboard(_client));
            _mouse = new Input.Mouse(new CRRawMouse(_client), _keyboard);
            _touchscreen = new Input.Touchscreen(new CRRawTouchscreen(_client), _keyboard);

            // Subscribe to CDP events from the page session.
            _client.MessageReceived += OnSessionEvent;
        }

        /// <summary>
        /// Occurs when a network request is created.
        /// </summary>
        internal event EventHandler<CRRequest> RequestCreated;

        /// <summary>
        /// Occurs when a network request finishes loading successfully.
        /// </summary>
        internal event EventHandler<CRRequest> RequestFinished;

        /// <summary>
        /// Occurs when a network request fails.
        /// </summary>
        internal event EventHandler<CRRequest> RequestFailed;

        /// <summary>
        /// Occurs when a network response is received.
        /// </summary>
        internal event EventHandler<CRResponse> ResponseReceived;

        /// <summary>
        /// Fires when a JavaScript dialog (alert, confirm, prompt, beforeunload) opens.
        /// The handler MUST call <see cref="CRDialog.AcceptAsync"/> or
        /// <see cref="CRDialog.DismissAsync"/> — otherwise the page hangs.
        /// </summary>
        internal event EventHandler<CRDialog> DialogOpening;

        /// <summary>
        /// Fires when CDP reports <c>Page.javascriptDialogClosed</c>.
        /// </summary>
        internal event EventHandler DialogClosedInBrowser;

        /// <summary>
        /// Fires when this page opens a popup (new target via <c>window.open()</c> or
        /// <c>&lt;a target=_blank&gt;</c>). The event argument is the new <see cref="CRPage"/>.
        /// </summary>
        internal event EventHandler<CRPage> PopupOpened;

        /// <summary>
        /// Fires when <c>console.*</c> is called in the page.
        /// </summary>
        internal event EventHandler<IConsoleMessage> Console;

        /// <summary>
        /// Fires when an uncaught exception is thrown in the page.
        /// </summary>
        internal event EventHandler<PageErrorEventArgs> PageError;

        /// <summary>
        /// Fires when this page has been detached from its CDP target (e.g. the user closed
        /// the page, the tab crashed, or <see cref="ClosePageAsync"/> was invoked). No payload.
        /// </summary>
        internal event EventHandler Closed;

        /// <summary>
        /// Fires when CDP reports a download is starting for this page.
        /// </summary>
        internal event EventHandler<DownloadWillBeginEventArgs> DownloadWillBegin;

        /// <summary>
        /// Fires when CDP reports download progress or a terminal state.
        /// </summary>
        internal event EventHandler<DownloadProgressEventArgs> DownloadProgress;

        /// <summary>
        /// Fires when CDP reports a file chooser for this page.
        /// </summary>
        internal event EventHandler<FileChooserOpenedEventArgs> FileChooserOpened;

        /// <summary>
        /// Fires when a dedicated worker is attached to this page.
        /// </summary>
        internal event EventHandler<CRWorker> WorkerCreated;

        /// <summary>
        /// Fires when the page opens a WebSocket.
        /// </summary>
        internal event EventHandler<IWebSocket> WebSocketCreated;

        /// <summary>
        /// Fires when the renderer process crashes (<c>Inspector.targetCrashed</c>).
        /// </summary>
        internal event EventHandler Crashed;

        /// <summary>
        /// Gets the dedicated workers currently attached to this page.
        /// </summary>
        internal IReadOnlyCollection<CRWorker> Workers => _workers.Values.ToArray();

        /// <summary>
        /// Gets the CDP target identifier for this page.
        /// </summary>
        internal string TargetId => _targetId;

        /// <summary>
        /// Location of the exception that last raised <see cref="PageError"/>.
        /// </summary>
        internal WebErrorLocation LastExceptionLocation { get; private set; }

        /// <summary>
        /// Gets or sets the page that opened this page via <c>window.open</c>, if any.
        /// </summary>
        internal CRPage Opener { get; set; }

        /// <summary>
        /// Viewport from the opener's <c>Page.windowOpen</c> features, or
        /// <see langword="null"/> when the popup should inherit the context size.
        /// </summary>
        internal ViewportSize WindowOpenViewport { get; set; }

        /// <summary>
        /// Delay before raising <see cref="PopupOpened"/> for inferred
        /// <c>about:blank</c> intermediates so a successor target can promote.
        /// </summary>
        internal int PopupEmitDelayMs { get; set; }

        /// <summary>
        /// Whether <see cref="FirePopupOpened"/> has already reported this page.
        /// </summary>
        internal bool PopupReported { get; set; }

        /// <summary>
        /// Gets the CDP session associated with this page.
        /// </summary>
        internal CRSession Session => _client;

        /// <summary>
        /// Gets the frame manager for this page.
        /// </summary>
        internal FrameManager FrameManager => _frameManager;

        /// <summary>
        /// Shared <see cref="Page"/> that owns this delegate. Used to resolve
        /// <see cref="IFrame"/> instances from protocol frame ids.
        /// </summary>
        internal Page PublicPage { get; set; }

        /// <summary>
        /// Gets the main frame of this page.
        /// </summary>
        internal Frame MainFrame => _frameManager.MainFrame;

        /// <summary>
        /// Gets the network manager for this page.
        /// </summary>
        internal CRNetworkManager NetworkManager => _networkManager;

        /// <summary>
        /// Gets the keyboard simulator for this page.
        /// </summary>
        internal Input.Keyboard Keyboard => _keyboard;

        /// <summary>
        /// Gets the mouse simulator for this page.
        /// </summary>
        internal Input.Mouse Mouse => _mouse;

        /// <summary>
        /// Gets the touchscreen simulator for this page.
        /// </summary>
        internal Input.Touchscreen Touchscreen => _touchscreen;

        /// <summary>
        /// Gets a task that completes when the page has been fully initialized
        /// (CDP domains enabled, execution context available).
        /// </summary>
        internal Task InitializedTask => _initializationTcs.Task;

        /// <summary>
        /// Official <c>connectOverCDP({ noDefaults })</c>: skip focus and media
        /// defaults when adopting a page that already existed.
        /// </summary>
        internal bool SkipDefaultOverrides { get; set; }

        /// <summary>
        /// Official first non-initial main-frame commit (not <c>:</c> / empty).
        /// </summary>
        internal Task FirstNonInitialNavigationTask => _firstNonInitialNavigationTcs.Task;

        /// <summary>
        /// Completes when <see cref="DidClose"/> has fired the closed signal.
        /// </summary>
        internal Task ClosedTask => _closedTcs.Task;

        /// <summary>
        /// Official <c>reportAsNew</c>: true after the first non-initial
        /// main-frame <c>Page.frameNavigated</c> (not <c>javascript:</c>).
        /// </summary>
        internal bool HasCommittedNonInitialNavigation { get; private set; }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            // The network manager is created in the constructor so it can receive
            // route handlers from CRBrowserContext.AddPage (which runs before
            // InitializeAsync). It subscribes to CDP events from construction time
            // and will capture events as soon as Network.enable is acknowledged.
            // Popup targets omit waitForDebuggerOnStart — a paused noopener
            // successor otherwise never reaches Runtime.executionContextCreated.
            // Network/Page/lifecycle must be enabled before resume so popup
            // navigations (target=_blank) are recorded. Optional CDP may hang.
            bool waitForDebuggerOnStart = Opener == null;
            Task critical = Task.WhenAll(
                _client.SendAsync("Page.enable"),
                _client.SendAsync("Page.setLifecycleEventsEnabled", new { enabled = true }),
                _client.SendAsync("Runtime.enable"),
                _client.SendAsync("Network.enable"));
            List<Task> optionalTasks = new List<Task>
            {
                _client.SendAsync("Page.setInterceptFileChooserDialog", new { enabled = true }),
                _client.SendAsync("DOM.enable"),
                _client.SendAsync("Log.enable"),
                _client.SendAsync("Inspector.enable"),
                _client.SendAsync("Target.setAutoAttach", new { autoAttach = true, waitForDebuggerOnStart, flatten = true }),
            };
            if (!SkipDefaultOverrides)
            {
                optionalTasks.Add(_client.SendAsync("Emulation.setFocusEmulationEnabled", new { enabled = true }));
                optionalTasks.Add(ApplyEmulatedMediaAsync());
            }

            Task optional = Task.WhenAll(optionalTasks);
            if (Opener != null)
            {
                await Task.WhenAny(critical, Task.Delay(2_000)).ConfigureAwait(false);
                await Task.WhenAny(optional, Task.Delay(1_000)).ConfigureAwait(false);
            }
            else
            {
                await Task.WhenAll(critical, optional).ConfigureAwait(false);
            }

            // Official applies context geolocation before resume so popup
            // documents (geolocation.html) see the override on first read.
            CRBrowserContext owner = null;
            if (_browser.DefaultContext != null && _browser.DefaultContext.Pages.Contains(this))
            {
                owner = _browser.DefaultContext;
            }
            else
            {
                foreach (CRBrowserContext context in _browser.Contexts)
                {
                    if (context.Pages.Contains(this))
                    {
                        owner = context;
                        break;
                    }
                }
            }

            if (owner?.Geolocation != null)
            {
                await SetGeolocationOverrideAsync(owner.Geolocation).ConfigureAwait(false);
            }

            // Official applies context init scripts before resume so the first
            // about:blank (NewPage and window.open popups) sees them.
            Task initScripts = Task.CompletedTask;
            if (PublicPage != null && owner?.PublicContext != null)
            {
                initScripts = owner.PublicContext.ApplyInitScriptsBeforeResumeAsync(PublicPage);
                if (Opener != null)
                {
                    // --disable-web-security popups can leave Page.addScriptToEvaluateOnNewDocument
                    // pending while the target is paused. Resume first, then finish apply.
                    await Task.WhenAny(initScripts, Task.Delay(1_000)).ConfigureAwait(false);
                }
                else
                {
                    await initScripts.ConfigureAwait(false);
                }

                // Locale must be on the session before the first document runs.
                // Do not wait forever — a paused popup that never acks CDP
                // still has to be resumed (noopener successor hang).
                Task localeTask = owner.PublicContext.ApplyLocaleEmulationBeforeResumeAsync(PublicPage);
                await Task.WhenAny(localeTask, Task.Delay(1_000)).ConfigureAwait(false);

                // Extra headers / touch / viewport must land before resume so
                // window.open's first request and the popup window see them.
                // A paused noopener successor may never ack CDP — do not wait.
                if (Opener != null)
                {
                    Task chromeTask = owner.PublicContext.ApplyPopupChromeBeforeResumeAsync(PublicPage);
                    await Task.WhenAny(chromeTask, Task.Delay(1_000)).ConfigureAwait(false);
                }
            }

            await _client.SendAsync("Runtime.runIfWaitingForDebugger").ConfigureAwait(false);
            _debuggerResumed = true;

            if (PublicPage != null && owner?.PublicContext != null
                && (Opener == null || !initScripts.IsCompletedSuccessfully))
            {
                // NewPage about:blank already exists when scripts are registered, so
                // addScriptToEvaluateOnNewDocument does not run them. Replay on the
                // current document after resume. Popups replay only when the paused
                // addScript hung (--disable-web-security); otherwise once-only holds.
                await owner.PublicContext.EvaluateInitScriptsOnCurrentAsync(PublicPage).ConfigureAwait(false);
            }

            if (Opener != null && owner?.PublicContext != null && PublicPage != null)
            {
                owner.PublicContext.ReportPopupAsNew(PublicPage);
                Task replay = ReplayExposedBindingsAsync();
                await Task.WhenAny(replay, Task.Delay(1_000)).ConfigureAwait(false);
            }

            if (PublicPage != null && owner?.PublicContext != null)
            {
                await owner.PublicContext.ApplyMediaEmulationAsync(PublicPage).ConfigureAwait(false);
                await owner.PublicContext.ApplyCallbackInitScriptsAsync(PublicPage).ConfigureAwait(false);
            }

            // Official _initialize waits for the first non-initial navigation
            // (getFrameTree URL is ":" / "" for the empty document) before
            // reportAsNew. Popups that already committed have a real URL.
            // window.open() / about:blank may never emit a second frameNavigated
            // when the target was not paused — poll then treat as about:blank.
            try
            {
                await WaitForFirstNonInitialNavigationAsync().ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                _firstNonInitialNavigationTcs.TrySetResult(true);
            }

            _initializationTcs.TrySetResult(true);
        }

        /// <inheritdoc/>
        public async Task ClosePageAsync(bool runBeforeUnload)
        {
            if (runBeforeUnload)
            {
                // Official crPage.closePage awaits Page.close. Chromium acks
                // after DispatchBeforeUnload (dialog may still be open). Do not
                // mark the page closed — runBeforeUnload only requests the dialog.
                try
                {
                    await _client.SendAsync("Page.close").ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }

                return;
            }

            await _browser.Connection.RootSession
                .SendAsync("Target.closeTarget", new { targetId = _targetId }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<GotoResult> NavigateFrameAsync(Frame frame, string url, string referrer)
        {
            object parameters = string.IsNullOrEmpty(referrer)
                ? (object)new { url, frameId = frame.FrameId, referrerPolicy = "unsafeUrl" }
                : new { url, referrer, frameId = frame.FrameId, referrerPolicy = "unsafeUrl" };
            JsonElement? response = await SessionForFrame(frame).SendAsync("Page.navigate", parameters).ConfigureAwait(false);

            if (!response.HasValue)
            {
                return new GotoResult(null);
            }

            JsonElement responseValue = response.Value;

            if (responseValue.TryGetProperty("isDownload", out JsonElement downloadEl)
                && downloadEl.ValueKind == JsonValueKind.True)
            {
                throw new NavigationException("Download is starting", url);
            }

            if (responseValue.TryGetProperty("errorText", out JsonElement errorTextElement))
            {
                string errorText = errorTextElement.GetString();
                if (!string.IsNullOrEmpty(errorText))
                {
                    throw new NavigationException($"Navigation failed: {errorText}", url);
                }
            }

            string loaderId = null;
            if (responseValue.TryGetProperty("loaderId", out JsonElement loaderIdElement))
            {
                loaderId = loaderIdElement.GetString();
            }

            return new GotoResult(loaderId);
        }

        /// <inheritdoc/>
        public async Task<T> EvaluateInFrameAsync<T>(Frame frame, string expression)
        {
            CRExecutionContext context = await WaitForFrameExecutionContextAsync(frame).ConfigureAwait(false);
            return await context.EvaluateAsync<T>(expression).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T> EvaluateFunctionInFrameAsync<T>(Frame frame, string functionDeclaration, params object[] args)
        {
            CRExecutionContext context = await WaitForFrameExecutionContextAsync(frame).ConfigureAwait(false);
            return await context.EvaluateFunctionAsync<T>(functionDeclaration, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a route handler that intercepts requests matching the given URL matcher.
        /// Uses the CDP <c>Fetch</c> domain to pause matching requests and invoke the handler,
        /// which can continue, fulfill, or abort the request via <see cref="CRRoute"/>.
        /// </summary>
        /// <param name="entry">The route registration.</param>
        /// <returns>A task that completes when the Fetch domain has been enabled.</returns>
        internal async Task RouteAsync(CRRouteEntry entry)
        {
            _networkManager.AddRoute(entry);
            await _networkManager.UpdateInterceptionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a glob-pattern route handler (Chromium-layer convenience used by tests).
        /// </summary>
        /// <param name="pattern">A glob-style URL pattern (e.g. <c>**/api/**</c>).</param>
        /// <param name="handler">The async handler invoked when a matching request is intercepted.</param>
        /// <returns>A task that completes when the Fetch domain has been enabled.</returns>
        internal Task RouteAsync(string pattern, Func<CRRoute, Task> handler)
            => RouteAsync(new CRRouteEntry(pattern, null, null, handler, handler, isContextRoute: false));

        /// <summary>
        /// Removes page-level routes matching the given matcher and optional handler.
        /// </summary>
        /// <param name="urlString">Glob used at registration, or <see langword="null"/>.</param>
        /// <param name="urlRegex">Regex used at registration, or <see langword="null"/>.</param>
        /// <param name="urlFunc">Predicate used at registration, or <see langword="null"/>.</param>
        /// <param name="handlerIdentity">Handler to remove, or <see langword="null"/> for all matching matchers.</param>
        /// <param name="behavior">How to treat in-flight handlers.</param>
        /// <returns>A task that completes when Fetch interception has been updated.</returns>
        internal async Task UnrouteAsync(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            object handlerIdentity,
            UnrouteBehavior behavior = default)
        {
            List<CRRouteEntry> removed = _networkManager.RemoveRoute(urlString, urlRegex, urlFunc, handlerIdentity, contextRoute: false);
            await RouteHandlerLifetime.StopAllAsync(removed.Select(entry => entry.Lifetime), behavior).ConfigureAwait(false);
            await _networkManager.UpdateInterceptionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Removes every page-level route and updates Fetch interception.
        /// </summary>
        /// <param name="behavior">How to treat in-flight handlers.</param>
        /// <returns>A task that completes when interception has been updated.</returns>
        internal async Task UnrouteAllAsync(UnrouteBehavior behavior = default)
        {
            List<CRRouteEntry> removed = _networkManager.ClearRoutes(contextRoute: false);
            await RouteHandlerLifetime.StopAllAsync(removed.Select(entry => entry.Lifetime), behavior).ConfigureAwait(false);
            await _networkManager.UpdateInterceptionAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Navigates the main frame to the given URL via the CDP <c>Page.navigate</c> command.
        /// </summary>
        /// <param name="url">The URL to navigate to.</param>
        /// <param name="referrer">Optional referrer URL.</param>
        /// <returns>A task that completes when the navigation command is acknowledged.
        /// The returned string is the loader ID (document ID) for the new navigation, or null for same-document navigations.</returns>
        internal async Task<string> NavigateAsync(string url, string referrer = null)
        {
            JsonElement? response = await _client.SendAsync("Page.navigate", new
            {
                url,
                referrer = referrer ?? string.Empty,
                frameId = MainFrame.FrameId,
            }).ConfigureAwait(false);

            if (!response.HasValue)
            {
                return null;
            }

            JsonElement responseValue = response.Value;

            // Check for navigation errors.
            if (responseValue.TryGetProperty("errorText", out JsonElement errorTextElement))
            {
                string errorText = errorTextElement.GetString();
                if (!string.IsNullOrEmpty(errorText))
                {
                    throw new PlaywrightNativeException($"Navigation failed: {errorText}");
                }
            }

            // Return the loader ID (document ID) if present.
            if (responseValue.TryGetProperty("loaderId", out JsonElement loaderIdElement))
            {
                return loaderIdElement.GetString();
            }

            return null;
        }

        /// <summary>
        /// Wires a flattened OOPIF / guest target session so the matching frame
        /// can evaluate on the child CDP session.
        /// </summary>
        /// <param name="session">The auto-attached iframe session.</param>
        /// <param name="targetId">The CDP target id (same as the OOPIF frame id).</param>
        /// <param name="parentFrameId">CDP <c>targetInfo.parentFrameId</c> when known.</param>
        internal void AttachOopifSession(CRSession session, string targetId, string parentFrameId = null)
        {
            if (!string.IsNullOrEmpty(targetId) && _frameManager.FrameById(targetId) == null)
            {
                string parent = !string.IsNullOrEmpty(parentFrameId)
                    ? parentFrameId
                    : _frameManager.MainFrame?.FrameId;
                if (!string.IsNullOrEmpty(parent))
                {
                    _oopifParents[targetId] = parent;
                    _frameManager.FrameAttachedToTarget(targetId, parent);
                }
            }
            else if (!string.IsNullOrEmpty(targetId) && !string.IsNullOrEmpty(parentFrameId))
            {
                _oopifParents[targetId] = parentFrameId;
            }

            RegisterOopifSession(targetId, session);
            OopifExecution.Attach(session, this, targetId);
        }

        /// <summary>
        /// Official <c>_sessions.set(targetId, frameSession)</c> for an OOPIF.
        /// </summary>
        /// <param name="targetId">CDP target id, equal to the OOPIF frame id.</param>
        /// <param name="session">The flattened iframe session.</param>
        internal void RegisterOopifSession(string targetId, CRSession session)
        {
            if (string.IsNullOrEmpty(targetId) || session == null)
            {
                return;
            }

            _oopifSessions[targetId] = session;
        }

        /// <summary>
        /// Official connect-over-CDP: parent <c>getFrameTree</c> omits OOPIFs,
        /// then <c>Target.attachedToTarget</c> inserts remote frames. A later
        /// main-frame commit must not drop those sessions.
        /// </summary>
        internal void RestoreOopifFrames()
        {
            foreach (string oopifId in _oopifSessions.Keys)
            {
                if (string.IsNullOrEmpty(oopifId) || _frameManager.FrameById(oopifId) != null)
                {
                    continue;
                }

                string parent = _oopifParents.TryGetValue(oopifId, out string stored) && !string.IsNullOrEmpty(stored)
                    ? stored
                    : _frameManager.MainFrame?.FrameId;
                if (!string.IsNullOrEmpty(parent))
                {
                    _frameManager.FrameAttachedToTarget(oopifId, parent);
                }
            }
        }

        /// <summary>
        /// Official: dedicated workers are gone when the owning document is replaced.
        /// </summary>
        internal void CloseAllWorkers()
        {
            foreach (CRWorker worker in _workers.Values)
            {
                RemoveWorker(worker.SessionId);
            }
        }

        /// <summary>
        /// Official <c>_sessions.delete(targetId)</c> when an OOPIF session is gone.
        /// Disposes child worker sessions with the frame session (issue 42278).
        /// </summary>
        /// <param name="targetId">CDP target id, equal to the OOPIF frame id.</param>
        internal void UnregisterOopifSession(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            CloseOwnedOopifWorkers(targetId);
            _oopifParents.TryRemove(targetId, out _);
            _oopifSwappedIn.TryRemove(targetId, out _);
            if (_oopifSessions.TryRemove(targetId, out CRSession session))
            {
                _networkManager.RemoveWorkerSession(session);
            }
        }

        /// <summary>
        /// Official <c>_sessions.get(frame._id)</c> lookup for <c>newCDPSession</c>.
        /// </summary>
        /// <param name="frameId">The frame / target id.</param>
        /// <param name="targetId">The OOPIF target id when present.</param>
        /// <returns><see langword="true"/> when this frame has its own session.</returns>
        internal bool TryGetOopifTargetId(string frameId, out string targetId)
        {
            targetId = null;
            if (string.IsNullOrEmpty(frameId) || !_oopifSessions.ContainsKey(frameId))
            {
                return false;
            }

            targetId = frameId;
            return true;
        }

        /// <summary>
        /// Official FrameSession <c>_onFrameAttached</c>: a session for
        /// <paramref name="frameId"/> means remote → local; keep the frame.
        /// </summary>
        /// <param name="frameId">CDP frame id.</param>
        /// <param name="parentFrameId">Parent frame id, or empty for the main frame.</param>
        internal void HandleFrameAttached(string frameId, string parentFrameId)
        {
            if (string.IsNullOrEmpty(frameId))
            {
                return;
            }

            if (_oopifSessions.ContainsKey(frameId)
                && !string.Equals(frameId, _targetId, StringComparison.Ordinal))
            {
                // Official remote → local: mark swappedIn so detachedFromTarget
                // disposes the OOPIF session without removing the frame.
                _oopifSwappedIn[frameId] = 1;
                Frame swapped = _frameManager.FrameById(frameId);
                if (swapped != null)
                {
                    _frameManager.RemoveChildFrames(swapped);
                }

                return;
            }

            if (!string.IsNullOrEmpty(parentFrameId) && _frameManager.FrameById(parentFrameId) == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(parentFrameId))
            {
                _frameManager.FrameAttachedToTarget(frameId, parentFrameId);
            }
        }

        /// <summary>
        /// Official FrameSession <c>_onFrameDetached</c>. Sessions live until
        /// <c>Target.detachedFromTarget</c>. A late parent <c>frameDetached</c>
        /// after OOPIF attach is local → remote and must not drop the frame.
        /// </summary>
        /// <param name="frameId">CDP frame id.</param>
        /// <param name="reason">CDP detach reason (<c>remove</c> or <c>swap</c>).</param>
        internal void HandleFrameDetached(string frameId, string reason)
        {
            if (string.IsNullOrEmpty(frameId))
            {
                return;
            }

            if (_oopifSessions.ContainsKey(frameId))
            {
                return;
            }

            if (string.Equals(reason, "swap", StringComparison.Ordinal))
            {
                Frame swapped = _frameManager.FrameById(frameId);
                if (swapped != null)
                {
                    _frameManager.RemoveChildFrames(swapped);
                }

                return;
            }

            Frame detaching = _frameManager.FrameById(frameId);
            if (detaching != null)
            {
                _networkManager.FinishInflightForDetachedFrame(detaching);
            }

            _frameManager.FrameDetachedFromTarget(frameId);
        }

        /// <summary>
        /// Official FrameSession init after Page/Runtime enable: network, bindings,
        /// init scripts, emulation, and file-chooser interception.
        /// </summary>
        /// <param name="session">The OOPIF CDP session.</param>
        /// <param name="targetId">The OOPIF target / frame id.</param>
        /// <returns>A task that completes when session state is applied.</returns>
        internal async Task ApplyOopifSessionAsync(CRSession session, string targetId)
        {
            if (session == null)
            {
                return;
            }

            RegisterOopifSession(targetId, session);
            Frame ownerFrame = !string.IsNullOrEmpty(targetId)
                ? _frameManager.FrameById(targetId)
                : null;
            await _networkManager.AddWorkerSessionAsync(session, ownerFrame, isWorker: false).ConfigureAwait(false);

            await SendIgnoreClosedAsync(session, "Page.setInterceptFileChooserDialog", new { enabled = true }).ConfigureAwait(false);
            if (_bindingInfrastructureInstalled)
            {
                await SendIgnoreClosedAsync(session, "Runtime.addBinding", new { name = PageBindingScript.ChannelName }).ConfigureAwait(false);
            }

            List<string> scripts;
            lock (_initScriptSync)
            {
                scripts = new List<string>(_initScriptSources);
            }

            foreach (string script in scripts)
            {
                await SendIgnoreClosedAsync(
                    session,
                    "Page.addScriptToEvaluateOnNewDocument",
                    new { source = script, runImmediately = true }).ConfigureAwait(false);
            }

            await ApplyEmulatedMediaOnSessionAsync(session).ConfigureAwait(false);
            if (_hasUserAgentOverride)
            {
                await SetUserAgentOnSessionAsync(session, _userAgentOverride, _acceptLanguageOverride, _userAgentIsMobile, _userAgentIncludeMetadata).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(_localeOverride))
            {
                await SendIgnoreClosedAsync(session, "Emulation.setLocaleOverride", new { locale = _localeOverride }).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(_timezoneOverride))
            {
                await SendIgnoreClosedAsync(session, "Emulation.setTimezoneOverride", new { timezoneId = _timezoneOverride }).ConfigureAwait(false);
            }

            if (_offline)
            {
                await SendIgnoreClosedAsync(session, "Network.emulateNetworkConditions", new
                {
                    offline = true,
                    latency = 0,
                    downloadThroughput = -1,
                    uploadThroughput = -1,
                }).ConfigureAwait(false);
            }

            if (_touchEnabled)
            {
                Dictionary<string, object> touch = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                };
                if (!string.IsNullOrEmpty(_touchConfiguration))
                {
                    touch["configuration"] = _touchConfiguration;
                }

                await SendIgnoreClosedAsync(session, "Emulation.setTouchEmulationEnabled", touch).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Official Fetch.enable on an OOPIF session, started with resume.
        /// </summary>
        /// <param name="session">The OOPIF CDP session.</param>
        /// <returns>A task that completes when Fetch is enabled or skipped.</returns>
        internal Task EnableOopifFetchAsync(CRSession session)
            => _networkManager.EnableFetchOnSessionIfNeededAsync(session);

        /// <summary>
        /// Official <c>_onAttachedToTarget</c> worker path for a session that
        /// already created the child CDP session (OOPIF FrameSession).
        /// </summary>
        /// <param name="child">The worker's flattened session.</param>
        /// <param name="sessionId">CDP session id.</param>
        /// <param name="targetInfo">CDP <c>targetInfo</c>.</param>
        /// <param name="ownerTargetId">OOPIF target that owns this worker, when known.</param>
        internal void AttachChildWorker(CRSession child, string sessionId, JsonElement targetInfo, string ownerTargetId = null)
        {
            if (child == null || string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            string url = targetInfo.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : string.Empty;
            string parentFrameId = targetInfo.TryGetProperty("parentFrameId", out JsonElement parentFrameEl)
                ? parentFrameEl.GetString()
                : string.Empty;
            CRWorker worker = new(child, sessionId, url);
            if (!_workers.TryAdd(sessionId, worker))
            {
                return;
            }

            string owner = !string.IsNullOrEmpty(ownerTargetId) ? ownerTargetId : parentFrameId;
            if (!string.IsNullOrEmpty(owner) && _oopifSessions.ContainsKey(owner))
            {
                _oopifOwnedWorkers.GetOrAdd(owner, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))
                    .TryAdd(sessionId, 0);
            }

            worker.ExceptionThrown += (_, error) => PageError?.Invoke(this, error);
            child.MessageReceived += OnWorkerSessionMessage;
            _ = AttachWorkerAndReportAsync(worker, parentFrameId);
        }

        /// <summary>
        /// Official <c>_sessionForFrame</c>: OOPIF frame id equals target id.
        /// </summary>
        /// <param name="frame">The frame whose session is needed.</param>
        /// <returns>The OOPIF session or the page session.</returns>
        internal CRSession SessionForFrame(Frame frame)
        {
            Frame current = frame;
            while (current != null)
            {
                if (_oopifSessions.TryGetValue(current.FrameId, out CRSession session))
                {
                    return session;
                }

                current = current.ParentFrame;
            }

            return _client;
        }

        /// <summary>
        /// Official FrameSession <c>_onDetachedFromTarget</c> for OOPIF workers.
        /// </summary>
        /// <param name="parameters">CDP <c>Target.detachedFromTarget</c> payload.</param>
        internal void DetachChildTarget(JsonElement? parameters)
            => OnDetachedFromTarget(parameters);

        /// <summary>
        /// Forwards OOPIF-session events that official FrameSession raises on the page.
        /// </summary>
        /// <param name="session">The OOPIF session that received the event.</param>
        /// <param name="method">CDP method name.</param>
        /// <param name="parameters">Event parameters.</param>
        internal void HandleOopifProtocolMessage(CRSession session, string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Runtime.consoleAPICalled":
                    OnConsoleAPICalled(parameters);
                    break;
                case "Runtime.bindingCalled":
                    OnBindingCalled(parameters, session);
                    break;
                case "Page.fileChooserOpened":
                    OnFileChooserOpened(parameters, session);
                    break;
                case "Runtime.exceptionThrown":
                    OnExceptionThrown(parameters);
                    break;
            }
        }

        /// <summary>
        /// Waits until the main frame has an execution context available.
        /// </summary>
        /// <param name="timeout">Maximum time to wait in milliseconds.</param>
        /// <returns>The execution context.</returns>
        internal Task<CRExecutionContext> WaitForExecutionContextAsync(int timeout = 5_000)
            => WaitForFrameExecutionContextAsync(MainFrame, timeout);

        /// <summary>
        /// Waits until <paramref name="frame"/> has a default execution context.
        /// </summary>
        /// <param name="frame">The frame to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>The frame's execution context.</returns>
        internal async Task<CRExecutionContext> WaitForFrameExecutionContextAsync(Frame frame, int timeout = 5_000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            while (true)
            {
                CRExecutionContext context = frame.ExecutionContext;
                if (context != null)
                {
                    return context;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Execution context not available after {timeout}ms");
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Evaluates a JavaScript expression in the main frame's execution context.
        /// Always reads the latest execution context to avoid stale context IDs.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The result of the evaluation.</returns>
        internal async Task<T> EvaluateAsync<T>(string expression)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            context = MainFrame.ExecutionContext ?? context;
            try
            {
                return await context.EvaluateAsync<T>(expression).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (
                ex.Message != null
                && ex.Message.Contains("Cannot find context", StringComparison.Ordinal))
            {
                await Task.Delay(50).ConfigureAwait(false);
                context = await WaitForExecutionContextAsync().ConfigureAwait(false);
                context = MainFrame.ExecutionContext ?? context;
                return await context.EvaluateAsync<T>(expression).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Evaluates a JavaScript expression in the main frame's execution context.
        /// Returns the raw result as a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The raw evaluation result.</returns>
        internal async Task<JsonElement?> EvaluateAsync(string expression)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            context = MainFrame.ExecutionContext ?? context;

            return await context.EvaluateAsync(expression).ConfigureAwait(false);
        }

        /// <summary>
        /// Official dispatcher evaluate: <c>Runtime.evaluate</c> with
        /// <c>awaitPromise: false</c> so a promise-returning <c>page.evaluate</c>
        /// does not deadlock WebSocket route delivery.
        /// </summary>
        /// <param name="expression">JavaScript to run in every live frame.</param>
        /// <returns>A task that completes when each evaluate has been sent.</returns>
        internal Task EvaluateWithoutAwaitingPromiseAsync(string expression)
        {
            List<int?> contextIds = new List<int?> { null };
            if (_lastBindingContextId != 0)
            {
                contextIds.Add(_lastBindingContextId);
            }

            foreach (int knownId in _contextIdToContext.Keys)
            {
                if (!contextIds.Contains(knownId))
                {
                    contextIds.Add(knownId);
                }
            }

            foreach (Frame frame in _frameManager.Frames)
            {
                CRExecutionContext context = frame?.ExecutionContext;
                if (context != null && !contextIds.Contains(context.ContextId))
                {
                    contextIds.Add(context.ContextId);
                }
            }

            foreach (int? contextId in contextIds)
            {
                try
                {
                    object args = contextId.HasValue
                        ? (object)new
                        {
                            expression,
                            contextId = contextId.Value,
                            returnByValue = true,
                            awaitPromise = false,
                            userGesture = true,
                        }
                        : new
                        {
                            expression,
                            returnByValue = true,
                            awaitPromise = false,
                            userGesture = true,
                        };
                    _ = _client.SendAsync("Runtime.evaluate", args);
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
        /// Evaluates a JavaScript function with the given arguments in the main frame's execution context.
        /// Uses <c>Runtime.callFunctionOn</c> internally.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="functionDeclaration">The JavaScript function declaration (e.g. "(a) => a + 1").</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The result of the function call.</returns>
        internal async Task<T> EvaluateFunctionAsync<T>(string functionDeclaration, params object[] args)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            context = MainFrame.ExecutionContext ?? context;

            return await context.EvaluateFunctionAsync<T>(functionDeclaration, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluates a JavaScript function with the given arguments, returning the raw result.
        /// </summary>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The raw result as a <see cref="JsonElement"/>.</returns>
        internal async Task<JsonElement?> EvaluateFunctionAsync(string functionDeclaration, params object[] args)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            context = MainFrame.ExecutionContext ?? context;

            return await context.EvaluateFunctionAsync(functionDeclaration, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Replaces the page's entire document with the given HTML. Uses
        /// <c>document.open/write/close</c> so scripts in the HTML execute. Waits for
        /// the <c>load</c> lifecycle event by default.
        /// </summary>
        /// <param name="html">Raw HTML content.</param>
        /// <param name="waitUntil">Lifecycle to wait for. Defaults to <see cref="WaitUntilState.Load"/>.</param>
        /// <param name="timeout">Wait timeout in milliseconds. Defaults to 30000.</param>
        internal Task SetContentAsync(string html, WaitUntilState waitUntil = WaitUntilState.Load, int timeout = 30_000)
            => SetContentInFrameAsync(MainFrame, html, waitUntil, timeout);

        /// <summary>
        /// Replaces <paramref name="frame"/>'s document with <paramref name="html"/> via
        /// <c>document.open/write/close</c> and waits for the requested lifecycle event.
        /// </summary>
        /// <param name="frame">The frame whose document is replaced.</param>
        /// <param name="html">Raw HTML content.</param>
        /// <param name="waitUntil">Lifecycle to wait for. Defaults to <see cref="WaitUntilState.Load"/>.</param>
        /// <param name="timeout">Wait timeout in milliseconds. Defaults to 30000.</param>
        internal async Task SetContentInFrameAsync(
            Frame frame,
            string html,
            WaitUntilState waitUntil = WaitUntilState.Load,
            int timeout = 30_000)
        {
            frame.ClearLifecycleEvents();
            frame.OnLifecycleEvent("commit");

            string targetLifecycleEvent = WaitUntilMapping.ToLifecycleEvent(waitUntil);

            TaskCompletionSource<bool> lifecycleTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnLifecycle(string name)
            {
                if (name == targetLifecycleEvent)
                {
                    lifecycleTcs.TrySetResult(true);
                }
            }

            frame.LifecycleChanged += OnLifecycle;

            try
            {
                const string writeHtml = @"html => {
                        document.open();
                        document.write(html);
                        document.close();
                        return true;
                    }";

                // Official Playwright writes from the utility world so parser-inserted
                // scripts (including exposeFunction calls) do not nest inside this evaluate.
                CRSession frameSession = SessionForFrame(frame);
                CRExecutionContext writeContext = null;
                JsonElement? isolated = null;
                try
                {
                    isolated = await frameSession.SendAsync("Page.createIsolatedWorld", new
                    {
                        frameId = frame.FrameId,
                        worldName = _utilityWorldName,
                        grantUniveralAccess = true,
                    }).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
                catch (PlaywrightNativeException)
                {
                }

                if (isolated.HasValue
                    && isolated.Value.TryGetProperty("executionContextId", out JsonElement isolatedId)
                    && isolatedId.TryGetInt32(out int isolatedContextId))
                {
                    writeContext = new CRExecutionContext(frameSession, isolatedContextId);
                    await writeContext.EvaluateFunctionAsync<bool>(writeHtml, html).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                else
                {
                    await EvaluateFunctionInFrameAsync<bool>(frame, writeHtml, html).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }

                // document.open() destroys the utility world used for the write.
                writeContext = null;
                try
                {
                    JsonElement? after = await frameSession.SendAsync("Page.createIsolatedWorld", new
                    {
                        frameId = frame.FrameId,
                        worldName = _utilityWorldName + ":setContent",
                        grantUniveralAccess = true,
                    }).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    if (after.HasValue
                        && after.Value.TryGetProperty("executionContextId", out JsonElement afterId)
                        && afterId.TryGetInt32(out int afterContextId))
                    {
                        writeContext = new CRExecutionContext(frameSession, afterContextId);
                    }
                }
                catch (TimeoutException)
                {
                }
                catch (PlaywrightNativeException)
                {
                }

                if (frame.LifecycleEvents.Contains(targetLifecycleEvent))
                {
                    return;
                }

                // Prefer the utility world used for document.write — the main-world
                // context is often destroyed mid-parse, which previously caused
                // SetContent to wait the full timeout for a load event.
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    if (frame.LifecycleEvents.Contains(targetLifecycleEvent))
                    {
                        return;
                    }

                    try
                    {
                        string ready;
                        if (writeContext != null)
                        {
                            ready = await writeContext.EvaluateFunctionAsync<string>("() => document.readyState")
                                .WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                        }
                        else
                        {
                            ready = await EvaluateFunctionInFrameAsync<string>(frame, "() => document.readyState")
                                .WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                        }

                        if (string.Equals(ready, "interactive", StringComparison.Ordinal)
                            || string.Equals(ready, "complete", StringComparison.Ordinal))
                        {
                            frame.OnLifecycleEvent("DOMContentLoaded");
                        }

                        if (string.Equals(ready, "complete", StringComparison.Ordinal))
                        {
                            frame.OnLifecycleEvent("load");
                        }

                        if (frame.LifecycleEvents.Contains(targetLifecycleEvent))
                        {
                            return;
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (PlaywrightNativeException)
                    {
                    }

                    await Task.Delay(25).ConfigureAwait(false);
                }

                if (frame.LifecycleEvents.Contains(targetLifecycleEvent))
                {
                    return;
                }

                // Static document.write HTML is complete once the write evaluate
                // returns. Prefer synthesizing the common lifecycle targets over
                // hanging until the navigation timeout when CDP load events are
                // lost after the utility-world context swap.
                if (!string.Equals(targetLifecycleEvent, "networkidle", StringComparison.Ordinal))
                {
                    frame.OnLifecycleEvent("DOMContentLoaded");
                    frame.OnLifecycleEvent("load");
                }

                // document.open destroys the main-world context. Wait until a new
                // one exists so callers do not hang on the first post-SetContent
                // evaluate / query. CDP may also raise OnNavigated during this
                // window and ClearLifecycleEvents, wiping the synthetic load.
                try
                {
                    await WaitForFrameExecutionContextAsync(frame, timeout: 5_000).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }

                if (!string.Equals(targetLifecycleEvent, "networkidle", StringComparison.Ordinal))
                {
                    if (!frame.LifecycleEvents.Contains("DOMContentLoaded"))
                    {
                        frame.OnLifecycleEvent("DOMContentLoaded");
                    }

                    if (!frame.LifecycleEvents.Contains("load"))
                    {
                        frame.OnLifecycleEvent("load");
                    }

                    return;
                }

                if (frame.LifecycleEvents.Contains(targetLifecycleEvent))
                {
                    return;
                }

                using var cts = new System.Threading.CancellationTokenSource(timeout);
                cts.Token.Register(() => lifecycleTcs.TrySetException(
                    new TimeoutException($"SetContentAsync timed out waiting for '{targetLifecycleEvent}' after {timeout}ms")));

                await lifecycleTcs.Task.ConfigureAwait(false);
            }
            finally
            {
                frame.LifecycleChanged -= OnLifecycle;
            }
        }

        internal async Task<CRExecutionContext> GetUtilityWorldAsync(Frame frame)
        {
            if (frame == null)
            {
                return null;
            }

            CRSession frameSession = SessionForFrame(frame);
            JsonElement? isolated = await frameSession.SendAsync("Page.createIsolatedWorld", new
            {
                frameId = frame.FrameId,
                worldName = _utilityWorldName,
                grantUniveralAccess = true,
            }).ConfigureAwait(false);
            if (isolated.HasValue
                && isolated.Value.TryGetProperty("executionContextId", out JsonElement isolatedId)
                && isolatedId.TryGetInt32(out int isolatedContextId))
            {
                return new CRExecutionContext(frameSession, isolatedContextId);
            }

            return null;
        }

        /// <summary>
        /// Returns the full HTML of the current document, including doctype and root element.
        /// </summary>
        /// <returns>The serialized document HTML.</returns>
        internal Task<string> ContentAsync()
            => PageContent.ReadAsync(() => EvaluateAsync<string>(PageContent.EvaluateExpression));

        /// <summary>
        /// Adds a <c>&lt;script&gt;</c> tag to the page by URL or inline content. For URL
        /// scripts, waits for the <c>load</c> event to fire; inline scripts execute
        /// synchronously via <c>script.text</c>.
        /// </summary>
        /// <param name="url">External script URL. Mutually exclusive with <paramref name="content"/>.</param>
        /// <param name="content">Inline script body. Mutually exclusive with <paramref name="url"/>.</param>
        /// <param name="type">Optional <c>type</c> attribute (e.g. <c>module</c>).</param>
        /// <param name="path">Local file injected as content with a <c>sourceURL</c> suffix.</param>
        /// <returns>A handle to the injected <c>script</c> element.</returns>
        internal async Task<CRElementHandle> AddScriptTagAsync(string url = null, string content = null, string type = null, string path = null)
        {
            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Specify url or content, not both.");
            }

            AddScriptTagHelper.Resolved resolved = AddScriptTagHelper.Resolve(url, path, content, type);
            return await AddScriptTagHelper.RaceWithCspErrorAsync(
                handler => Console += handler,
                handler => Console -= handler,
                async () =>
                {
                    if (!string.IsNullOrEmpty(resolved.Url))
                    {
                        return await QueryFunctionAsync(
                            AddScriptTagHelper.AddScriptUrlFunction,
                            resolved.Url,
                            resolved.Type).ConfigureAwait(false);
                    }

                    CRElementHandle handle = await QueryFunctionAsync(
                        AddScriptTagHelper.AddScriptContentFunction,
                        resolved.Content,
                        resolved.Type).ConfigureAwait(false);

                    // Official extra round-trip so async CSP console errors can win the race.
                    await EvaluateAsync("true").ConfigureAwait(false);
                    return handle;
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a <c>&lt;link rel="stylesheet"&gt;</c> (for URL) or <c>&lt;style&gt;</c>
        /// (for inline content) to the document head. Waits for the onload event.
        /// </summary>
        /// <param name="url">External stylesheet URL. Mutually exclusive with <paramref name="content"/>.</param>
        /// <param name="content">Inline CSS. Mutually exclusive with <paramref name="url"/>.</param>
        /// <param name="path">Local file injected as content with a CSS <c>sourceURL</c> suffix.</param>
        /// <returns>A handle to the injected <c>link</c> or <c>style</c> element.</returns>
        internal async Task<CRElementHandle> AddStyleTagAsync(string url = null, string content = null, string path = null)
        {
            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Specify url or content, not both.");
            }

            AddStyleTagHelper.Resolved resolved = AddStyleTagHelper.Resolve(url, path, content);
            return await AddStyleTagHelper.RaceWithCspErrorAsync(
                handler => Console += handler,
                handler => Console -= handler,
                async () =>
                {
                    if (!string.IsNullOrEmpty(resolved.Url))
                    {
                        return await QueryFunctionAsync(
                            AddStyleTagHelper.AddStyleUrlFunction,
                            resolved.Url).ConfigureAwait(false);
                    }

                    return await QueryFunctionAsync(
                        AddStyleTagHelper.AddStyleContentFunction,
                        resolved.Content).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a JavaScript source that runs on every new document this page loads,
        /// before any page scripts execute. Returns an identifier that can be passed to
        /// <see cref="RemoveInitScriptAsync"/> to unregister.
        /// </summary>
        /// <param name="script">The JavaScript source to run on each new document.</param>
        /// <returns>The CDP script identifier.</returns>
        internal async Task<string> AddInitScriptAsync(string script)
        {
            if (string.IsNullOrEmpty(script))
            {
                throw new ArgumentException("Script cannot be empty.", nameof(script));
            }

            lock (_initScriptSync)
            {
                _initScriptSources.Add(script);
            }

            JsonElement? response = await _client.SendAsync("Page.addScriptToEvaluateOnNewDocument", new
            {
                source = script,
            }).ConfigureAwait(false);

            foreach (CRSession session in _oopifSessions.Values)
            {
                await SendIgnoreClosedAsync(
                    session,
                    "Page.addScriptToEvaluateOnNewDocument",
                    new { source = script, runImmediately = true }).ConfigureAwait(false);
            }

            if (response.HasValue && response.Value.TryGetProperty("identifier", out JsonElement id))
            {
                return id.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Removes a previously-registered init script. Safe to call on an unknown identifier
        /// (CDP returns an error which is swallowed — idempotent).
        /// </summary>
        /// <param name="identifier">The identifier returned by <see cref="AddInitScriptAsync"/>.</param>
        internal async Task RemoveInitScriptAsync(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            try
            {
                await _client.SendAsync("Page.removeScriptToEvaluateOnNewDocument", new
                {
                    identifier,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Swallow — unknown identifier, session closed, etc.
            }
        }

        /// <summary>
        /// Returns whether <paramref name="name"/> is an installed binding.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <returns>True when the name is registered.</returns>
        internal bool HasExposedFunction(string name)
            => !string.IsNullOrEmpty(name)
            && (_exposedFunctions.ContainsKey(name) || _handleBindings.ContainsKey(name));

        /// <summary>
        /// Registers <paramref name="handler"/> as a global JS function accessible at
        /// <c>window[name]</c>. When page JavaScript calls it with arguments, they are
        /// JSON-serialized, the C# <paramref name="handler"/> is invoked with the
        /// deserialized args, and its result is sent back as the function's Promise
        /// resolution.
        /// </summary>
        /// <remarks>
        /// The exposed function is installed via <c>Page.addScriptToEvaluateOnNewDocument</c>
        /// so it survives navigation. Passing the same <paramref name="name"/> twice throws.
        /// </remarks>
        /// <param name="name">The JS global name (e.g. "helloFromCSharp").</param>
        /// <param name="handler">
        /// Called with an array of <see cref="JsonElement"/> (one per JS argument) and
        /// returns an object serialized back to JS. Throw to reject the page-side promise.
        /// </param>
        /// <returns>The init-script identifier used to unregister on dispose.</returns>
        internal async Task<string> ExposeFunctionAsync(string name, Func<JsonElement[], Task<object>> handler)
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
            string identifier = await AddInitScriptAsync(installer).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(installer).ConfigureAwait(false);
            return identifier;
        }

        /// <summary>
        /// Registers a hidden evaluate callback (not installed on <c>globalThis[name]</c>).
        /// Erased on the next main-frame navigation.
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
        /// <returns>The init-script identifier for later removal.</returns>
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
            string identifier = await AddInitScriptAsync(installer).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(installer).ConfigureAwait(false);
            return identifier;
        }

        /// <summary>
        /// Removes a persistent init-script callback.
        /// </summary>
        /// <param name="name">Unguessable callback name.</param>
        /// <param name="identifier">Identifier from <see cref="RegisterPersistentEvalFnAsync"/>.</param>
        /// <returns>A task that completes when the callback is removed.</returns>
        internal async Task UnregisterPersistentEvalFnAsync(string name, string identifier)
        {
            _exposedFunctions.TryRemove(name, out _);
            await RemoveInitScriptAsync(identifier).ConfigureAwait(false);
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
        /// Unregisters <paramref name="name"/> and removes its init script so later
        /// documents no longer get <c>window[name]</c>.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <param name="identifier">The identifier returned by <see cref="ExposeFunctionAsync"/>.</param>
        /// <returns>A task that completes when the binding has been removed.</returns>
        internal async Task RemoveExposedFunctionAsync(string name, string identifier)
        {
            _exposedFunctions.TryRemove(name, out _);
            _handleBindings.TryRemove(name, out _);
            await RemoveInitScriptAsync(identifier).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(PageBindingScript.RemoveExpression(name)).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers <paramref name="handler"/> as a handle-mode binding at
        /// <c>window[name]</c>. The page-side argument is delivered as a
        /// <see cref="CRJSHandle"/> instead of JSON.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <param name="handler">
        /// Called with the argument handle; the return value is serialized back to the page.
        /// </param>
        /// <returns>The init-script identifier used to unregister on dispose.</returns>
        internal async Task<string> ExposeHandleBindingAsync(string name, Func<CRJSHandle, Task<object>> handler)
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
            string identifier = await AddInitScriptAsync(installer).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(installer).ConfigureAwait(false);
            return identifier;
        }

        /// <summary>
        /// Queries the DOM for the first element matching the CSS selector and returns a
        /// <see cref="CRElementHandle"/>, or <c>null</c> if no match. The handle must be
        /// disposed (via <c>await using</c> or <c>DisposeAsync</c>) when no longer needed.
        /// </summary>
        /// <param name="selector">A CSS selector (e.g. "input#email", ".submit", "textarea").</param>
        /// <returns>A handle to the matched element, or <c>null</c>.</returns>
        internal Task<CRElementHandle> QuerySelectorAsync(string selector)
            => QuerySelectorInFrameAsync(MainFrame, selector);

        /// <summary>
        /// Queries <paramref name="frame"/>'s document for the first element matching
        /// <paramref name="selector"/>.
        /// </summary>
        /// <param name="frame">The frame whose document is queried.</param>
        /// <param name="selector">A CSS selector.</param>
        /// <returns>A handle to the matched element, or <see langword="null"/>.</returns>
        internal async Task<CRElementHandle> QuerySelectorInFrameAsync(Frame frame, string selector)
        {
            SelectorQuery.EnsureSelector(selector);
            DomVisibility.ThrowIfUnknownEngine(selector);
            if (FrameSelector.ContainsControl(selector))
            {
                IReadOnlyList<IElementHandle> matches = await FrameSelector.QueryAllAsync(
                    WrapPublicFrame(frame),
                    null,
                    selector).ConfigureAwait(false);
                return matches.Count > 0 ? UnwrapPublicElement(matches[0]) : null;
            }

            CRExecutionContext context = await WaitForFrameExecutionContextAsync(frame).ConfigureAwait(false);

            try
            {
                if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
                {
                    CRExecutionContext evalContext = context;
                    if (CustomSelectors.ShouldQueryInIsolatedWorld(selector))
                    {
                        evalContext = await GetUtilityWorldAsync(frame).ConfigureAwait(false) ?? context;
                    }

                    JsonElement? custom = await evalContext.EvaluateHandleAsync(call.DocumentQueryExpression).ConfigureAwait(false);
                    return WrapElementHandle(evalContext, custom);
                }

                JsonElement? handleValue = await context.EvaluateFunctionHandleAsync(
                    ShadowPiercingQuery.QueryFunction,
                    selector).ConfigureAwait(false);

                return WrapElementHandle(context, handleValue);
            }
            catch (PlaywrightNativeException ex) when (PlaywrightNativeException.IsDestroyedContext(ex))
            {
                if (frame != null && frame.ExecutionContext == context)
                {
                    frame.ExecutionContext = null;
                }

                throw;
            }
        }

        /// <summary>
        /// Queries the main frame for every element matching <paramref name="selector"/>.
        /// </summary>
        /// <param name="selector">A CSS selector.</param>
        /// <returns>Handles for the matching elements, in document order.</returns>
        internal Task<IReadOnlyList<CRElementHandle>> QuerySelectorAllAsync(string selector)
            => QuerySelectorAllInFrameAsync(MainFrame, selector);

        /// <summary>
        /// Queries <paramref name="frame"/>'s document for every element matching
        /// <paramref name="selector"/>.
        /// </summary>
        /// <param name="frame">The frame whose document is queried.</param>
        /// <param name="selector">A CSS selector.</param>
        /// <returns>Handles for the matching elements, in document order.</returns>
        internal async Task<IReadOnlyList<CRElementHandle>> QuerySelectorAllInFrameAsync(Frame frame, string selector)
        {
            SelectorQuery.EnsureSelector(selector);
            DomVisibility.ThrowIfUnknownEngine(selector);
            if (FrameSelector.ContainsControl(selector))
            {
                IReadOnlyList<IElementHandle> matches = await FrameSelector.QueryAllAsync(
                    WrapPublicFrame(frame),
                    null,
                    selector).ConfigureAwait(false);
                List<CRElementHandle> converted = new List<CRElementHandle>(matches.Count);
                for (int i = 0; i < matches.Count; i++)
                {
                    CRElementHandle inner = UnwrapPublicElement(matches[i]);
                    if (inner != null)
                    {
                        converted.Add(inner);
                    }
                }

                return converted;
            }

            CRExecutionContext context = await WaitForFrameExecutionContextAsync(frame).ConfigureAwait(false);
            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                JsonElement? customArray = await context.EvaluateHandleAsync(call.DocumentQueryAllExpression).ConfigureAwait(false);
                return await UnwrapElementArrayAsync(context, customArray).ConfigureAwait(false);
            }

            JsonElement? arrayRemote = await context.EvaluateFunctionHandleAsync(
                ShadowPiercingQuery.QueryAllFunction,
                selector).ConfigureAwait(false);
            return await UnwrapElementArrayAsync(context, arrayRemote).ConfigureAwait(false);
        }

        /// <summary>
        /// Walks a remote array of DOM nodes and wraps each item as an element handle.
        /// Releases the array object id when finished.
        /// </summary>
        /// <param name="context">The execution context that owns the array.</param>
        /// <param name="arrayRemote">The remote array object, or <see langword="null"/>.</param>
        /// <returns>Handles for the array items that are DOM nodes.</returns>
        internal async Task<IReadOnlyList<CRElementHandle>> UnwrapElementArrayAsync(
            CRExecutionContext context,
            JsonElement? arrayRemote)
        {
            string arrayId = RemoteObject.GetObjectId(arrayRemote);
            if (string.IsNullOrEmpty(arrayId))
            {
                return Array.Empty<CRElementHandle>();
            }

            try
            {
                int length = await context.EvaluateFunctionOnHandleAsync<int>(arrayId, "arr => arr.length").ConfigureAwait(false);
                List<CRElementHandle> result = new(length);
                for (int i = 0; i < length; i++)
                {
                    JsonElement? item = await context
                        .EvaluateHandleOnHandleAsync(arrayId, "(arr, i) => arr[i]", i)
                        .ConfigureAwait(false);
                    CRElementHandle element = WrapElementHandle(context, item);
                    if (element != null)
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
        /// Wraps a CDP remote object as an element handle when it is a DOM node.
        /// </summary>
        /// <param name="context">The execution context that owns the object.</param>
        /// <param name="handleValue">The remote object, or <see langword="null"/>.</param>
        /// <returns>The element handle, or <see langword="null"/>.</returns>
        internal CRElementHandle WrapElementHandle(CRExecutionContext context, JsonElement? handleValue)
        {
            if (handleValue == null)
            {
                return null;
            }

            JsonElement remoteObject = handleValue.Value;
            if (remoteObject.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.GetString() == "null")
            {
                return null;
            }

            if (!remoteObject.TryGetProperty("objectId", out JsonElement objectIdElement))
            {
                return null;
            }

            string objectId = objectIdElement.GetString();
            return string.IsNullOrEmpty(objectId)
                ? null
                : new CRElementHandle(this, context, objectId);
        }

        /// <summary>
        /// Resolves the frame id reported by <c>DOM.describeNode</c> for <paramref name="objectId"/>.
        /// For an iframe element this is the content frame; for <c>documentElement</c> it is
        /// the owner frame.
        /// </summary>
        /// <param name="objectId">The CDP remote object id.</param>
        /// <returns>The matching frame, or <see langword="null"/>.</returns>
        internal async Task<Frame> FrameFromDescribedNodeAsync(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            JsonElement? described;
            try
            {
                described = await _client.SendAsync("DOM.describeNode", new { objectId }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return null;
            }

            if (described == null
                || !described.Value.TryGetProperty("node", out JsonElement node)
                || !node.TryGetProperty("frameId", out JsonElement frameIdElement)
                || frameIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string frameId = frameIdElement.GetString();
            return string.IsNullOrEmpty(frameId) ? null : _frameManager.FrameById(frameId);
        }

        /// <summary>
        /// Reads <c>node.frameId</c> from <c>DOM.describeNode</c> on
        /// <paramref name="session"/>. Enables DOM on that session when needed
        /// so OOPIF handles can be described.
        /// </summary>
        /// <param name="session">The CDP session that owns <paramref name="objectId"/>.</param>
        /// <param name="objectId">The CDP remote object id.</param>
        /// <returns>The described frame id, or <see langword="null"/>.</returns>
        internal async Task<string> DescribeNodeFrameIdAsync(CRSession session, string objectId)
        {
            if (session == null || string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            JsonElement? described = await TryDescribeNodeAsync(session, objectId).ConfigureAwait(false);
            if (described == null)
            {
                try
                {
                    await session.SendAsync("DOM.enable").ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }

                described = await TryDescribeNodeAsync(session, objectId).ConfigureAwait(false);
            }

            if (described == null
                || !described.Value.TryGetProperty("node", out JsonElement node)
                || !node.TryGetProperty("frameId", out JsonElement frameIdElement)
                || frameIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return frameIdElement.GetString();
        }

        /// <summary>
        /// Reads the content-frame id from <c>DOM.describeNode</c> when the node
        /// is an <c>IFRAME</c>, <c>FRAME</c>, or <c>OBJECT</c>. Other nodes
        /// (including <c>documentElement</c>) return <see langword="null"/> even
        /// though CDP still reports <c>node.frameId</c> as the owner frame.
        /// </summary>
        /// <param name="session">The CDP session that owns <paramref name="objectId"/>.</param>
        /// <param name="objectId">The CDP remote object id.</param>
        /// <returns>The content frame id, or <see langword="null"/>.</returns>
        internal async Task<string> DescribeNodeContentFrameIdAsync(CRSession session, string objectId)
        {
            if (session == null || string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            JsonElement? described = await TryDescribeNodeAsync(session, objectId).ConfigureAwait(false);
            if (described == null)
            {
                try
                {
                    await session.SendAsync("DOM.enable").ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }

                described = await TryDescribeNodeAsync(session, objectId).ConfigureAwait(false);
            }

            if (described == null
                || !described.Value.TryGetProperty("node", out JsonElement node))
            {
                return null;
            }

            string nodeName = node.TryGetProperty("nodeName", out JsonElement nameEl)
                && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : null;
            bool isHost = string.Equals(nodeName, "IFRAME", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, "FRAME", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, "OBJECT", StringComparison.OrdinalIgnoreCase);
            if (!isHost)
            {
                return null;
            }

            if (!node.TryGetProperty("frameId", out JsonElement frameIdElement)
                || frameIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return frameIdElement.GetString();
        }

        /// <summary>
        /// Resolves a protocol frame id to a public frame on this page, or
        /// another page in the same browser when the node was adopted.
        /// </summary>
        /// <param name="frameId">The protocol frame id.</param>
        /// <returns>The public frame, or <see langword="null"/>.</returns>
        internal IFrame ResolvePublicFrameById(string frameId)
        {
            if (string.IsNullOrEmpty(frameId))
            {
                return null;
            }

            Frame local = _frameManager.FrameById(frameId);
            if (local != null)
            {
                return WrapPublicFrame(local);
            }

            foreach (CRPage page in _browser.AttachedPages)
            {
                if (ReferenceEquals(page, this))
                {
                    continue;
                }

                Frame frame = page.FrameManager.FrameById(frameId);
                if (frame != null)
                {
                    return page.WrapPublicFrame(frame);
                }
            }

            return null;
        }

        /// <summary>
        /// Unwraps a public element handle produced by this page.
        /// </summary>
        /// <param name="handle">A public element handle.</param>
        /// <returns>The Chromium handle, or <see langword="null"/>.</returns>
        internal CRElementHandle UnwrapPublicElement(IElementHandle handle)
            => handle is ChromiumElementHandle instance ? instance.Unwrap() : handle as CRElementHandle;

        /// <summary>
        /// Wraps a protocol frame as the public <see cref="IFrame"/> for this page.
        /// </summary>
        /// <param name="frame">The Chromium frame, or <see langword="null"/>.</param>
        /// <returns>The public frame, or <see langword="null"/>.</returns>
        internal IFrame WrapPublicFrame(Frame frame)
            => frame == null ? null : PublicPage?.GetOrCreateFrame(frame);

        /// <summary>
        /// Returns the hosting <c>iframe</c>/<c>frame</c> element via
        /// <c>DOM.getFrameOwner</c>. Works for closed and declarative shadow roots.
        /// </summary>
        /// <param name="frame">The child frame.</param>
        /// <returns>The hosting element handle.</returns>
        internal async Task<IElementHandle> GetFrameElementAsync(Frame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            Frame parent = frame.ParentFrame;
            if (parent == null || frame.IsDetached)
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            JsonElement? response;
            try
            {
                response = await _client.SendAsync("DOM.getFrameOwner", new { frameId = frame.FrameId })
                    .ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex)
            {
                if (ex.Message.Contains("Frame with the given id was not found.", StringComparison.Ordinal)
                    || ex.Message.Contains("detached", StringComparison.OrdinalIgnoreCase))
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

            if (response == null
                || !response.Value.TryGetProperty("backendNodeId", out JsonElement backendEl)
                || !backendEl.TryGetInt32(out int backendNodeId))
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            CRExecutionContext context = await WaitForFrameExecutionContextAsync(parent).ConfigureAwait(false);
            JsonElement? resolved = await _client.SendAsync("DOM.resolveNode", new
            {
                backendNodeId,
                executionContextId = context.ContextId,
            }).ConfigureAwait(false);

            if (resolved == null || !resolved.Value.TryGetProperty("object", out JsonElement remote))
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            CRElementHandle handle = WrapElementHandle(context, remote);
            if (handle == null)
            {
                throw new PlaywrightNativeException("Frame has been detached.");
            }

            return new ChromiumElementHandle(handle);
        }

        /// <summary>
        /// Runs a JavaScript function that returns a DOM node and wraps it as an element handle.
        /// </summary>
        /// <param name="functionDeclaration">A function declaration returning an Element or null.</param>
        /// <param name="args">Arguments passed to the function.</param>
        /// <returns>The matched element, or <see langword="null"/>.</returns>
        internal async Task<CRElementHandle> QueryFunctionAsync(string functionDeclaration, params object[] args)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            context = MainFrame.ExecutionContext ?? context;

            JsonElement? handleValue = await context.EvaluateFunctionHandleAsync(functionDeclaration, args).ConfigureAwait(false);
            return WrapElementHandle(context, handleValue);
        }

        /// <summary>
        /// Evaluates <paramref name="expression"/> and returns a JS handle to the result.
        /// </summary>
        /// <param name="expression">A JavaScript expression or function IIFE.</param>
        /// <returns>A handle to the remote object, or <see langword="null"/>.</returns>
        internal Task<CRJSHandle> EvaluateHandleInternalAsync(string expression)
            => EvaluateHandleInFrameAsync(MainFrame, expression);

        /// <summary>
        /// Evaluates a JavaScript function with arguments and returns a JS handle
        /// (<c>returnByValue: false</c>). Used when evaluate-handle must pass
        /// nested <see cref="IJSHandle"/> arguments.
        /// </summary>
        /// <param name="functionDeclaration">The function declaration.</param>
        /// <param name="args">Arguments, including live JS handles.</param>
        /// <returns>A handle to the remote result, or <see langword="null"/>.</returns>
        internal async Task<CRJSHandle> EvaluateFunctionHandleInternalAsync(
            string functionDeclaration,
            params object[] args)
        {
            CRExecutionContext context = await WaitForExecutionContextAsync().ConfigureAwait(false);
            context = MainFrame.ExecutionContext ?? context;
            JsonElement? handleValue = await context.EvaluateFunctionHandleAsync(functionDeclaration, args)
                .ConfigureAwait(false);
            return WrapJSHandle(context, handleValue);
        }

        /// <summary>
        /// Evaluates <paramref name="expression"/> in <paramref name="frame"/> and
        /// returns a JS handle to the result.
        /// </summary>
        /// <param name="frame">The frame whose world is evaluated.</param>
        /// <param name="expression">A JavaScript expression or function IIFE.</param>
        /// <returns>A handle to the remote object, or <see langword="null"/>.</returns>
        internal async Task<CRJSHandle> EvaluateHandleInFrameAsync(Frame frame, string expression)
        {
            CRExecutionContext context = await WaitForFrameExecutionContextAsync(frame).ConfigureAwait(false);
            JsonElement? handleValue = await context.EvaluateHandleAsync(expression).ConfigureAwait(false);
            return WrapJSHandle(context, handleValue);
        }

        /// <summary>
        /// Evaluates a JavaScript function with arguments in <paramref name="frame"/>
        /// and returns a JS handle.
        /// </summary>
        /// <param name="frame">The frame whose world is evaluated.</param>
        /// <param name="functionDeclaration">The function declaration.</param>
        /// <param name="args">Arguments, including live JS handles.</param>
        /// <returns>A handle to the remote result, or <see langword="null"/>.</returns>
        internal async Task<CRJSHandle> EvaluateFunctionHandleInFrameAsync(
            Frame frame,
            string functionDeclaration,
            params object[] args)
        {
            CRExecutionContext context = await WaitForFrameExecutionContextAsync(frame).ConfigureAwait(false);
            JsonElement? handleValue = await context.EvaluateFunctionHandleAsync(functionDeclaration, args)
                .ConfigureAwait(false);
            return WrapJSHandle(context, handleValue);
        }

        /// <summary>
        /// Navigates the main frame to the given URL and waits for the specified lifecycle event.
        /// This is the high-level navigation method that coordinates the CDP command
        /// with lifecycle event waiting.
        /// </summary>
        /// <param name="url">The URL to navigate to.</param>
        /// <param name="waitUntil">The lifecycle event to wait for. Defaults to <see cref="WaitUntilState.Load"/>.</param>
        /// <param name="timeout">Maximum time to wait in milliseconds. Defaults to 30000.</param>
        /// <param name="referrer">Optional referrer URL.</param>
        /// <returns>A task that completes when navigation finishes and the lifecycle event fires.</returns>
        internal Task GoToAsync(
            string url,
            WaitUntilState waitUntil = WaitUntilState.Load,
            int timeout = 30_000,
            string referrer = null)
            => GoToFrameAsync(MainFrame, url, waitUntil, timeout, referrer);

        /// <summary>
        /// Navigates <paramref name="frame"/> to <paramref name="url"/> and waits for
        /// that frame's lifecycle event (not the main frame's).
        /// </summary>
        /// <param name="frame">The frame to navigate.</param>
        /// <param name="url">The URL to navigate to.</param>
        /// <param name="waitUntil">The lifecycle event to wait for. Defaults to <see cref="WaitUntilState.Load"/>.</param>
        /// <param name="timeout">Maximum time to wait in milliseconds. Defaults to 30000.</param>
        /// <param name="referrer">Optional referrer URL.</param>
        internal async Task GoToFrameAsync(
            Frame frame,
            string url,
            WaitUntilState waitUntil = WaitUntilState.Load,
            int timeout = 30_000,
            string referrer = null)
        {
            url = NavigationTimeout.CompleteUserUrl(url);
            ThrowIfWebUiWouldCrashIsolatedContext();
            referrer = NavigationTimeout.ReferrerFromExtraHeaders(referrer, _networkManager.ExtraHttpHeaders);
            NavigationTimeout.ThrowIfRefererConflict(url, referrer, _networkManager.ExtraHttpHeaders);
            _networkManager.RememberNavigateReferrer(referrer);
            string targetLifecycleEvent = WaitUntilMapping.ToLifecycleEvent(waitUntil);
            string apiName = frame.ParentFrame == null ? "page.goto" : "frame.goto";
            int waitMs = timeout <= 0 ? System.Threading.Timeout.Infinite : timeout;

            // Race Page.navigate with the navigation timeout. A hanging server
            // can keep the CDP command outstanding; official progress.race
            // aborts the whole goto, not just the lifecycle wait.
            Task<GotoResult> navigateTask = NavigateFrameAsync(frame, url, referrer);
            if (waitMs != System.Threading.Timeout.Infinite)
            {
                using System.Threading.CancellationTokenSource navigateCts = new(waitMs);
                try
                {
                    await navigateTask.WaitAsync(navigateCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw NavigationTimeout.Exceeded(apiName, url, NavigationTimeout.WaitUntilName(waitUntil), timeout);
                }
            }

            GotoResult result = await navigateTask.ConfigureAwait(false);
            string expectedDocumentId = result.NewDocumentId;

            TaskCompletionSource<bool> lifecycleTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnLifecycle(string name)
            {
                // Accept only lifecycle events that belong to the navigation we issued.
                // Stale events from prior documents have a different DocumentId.
                if (name == targetLifecycleEvent &&
                    (expectedDocumentId == null || frame.DocumentId == expectedDocumentId))
                {
                    lifecycleTcs.TrySetResult(true);
                }
            }

            void OnDetached(Frame detached)
            {
                if (ReferenceEquals(detached, frame))
                {
                    lifecycleTcs.TrySetException(new PlaywrightNativeException("frame was detached"));
                }
            }

            void OnNavigated(Frame navigated, string documentId)
            {
                if (!ReferenceEquals(navigated, frame))
                {
                    return;
                }

                // Same-document (hash) navigations have no loaderId. Complete
                // once the frame URL has been updated.
                if (string.IsNullOrEmpty(expectedDocumentId))
                {
                    lifecycleTcs.TrySetResult(true);
                    return;
                }

                // A later document committed while we were still waiting for
                // waitUntil on the original navigation — official page.goto
                // returns the already-committed response.
                if (!string.IsNullOrEmpty(documentId) && documentId != expectedDocumentId)
                {
                    lifecycleTcs.TrySetResult(true);
                }
            }

            void OnClosed(object sender, EventArgs e)
            {
                lifecycleTcs.TrySetException(
                    new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage));
            }

            frame.LifecycleChanged += OnLifecycle;
            _frameManager.FrameDetached += OnDetached;
            _frameManager.FrameNavigated += OnNavigated;
            Closed += OnClosed;

            try
            {
                if (frame.IsDetached)
                {
                    throw new PlaywrightNativeException("frame was detached");
                }

                // Fast-path: lifecycle may have already fired before we subscribed
                // (very fast navigation or events processed by the receive loop before
                // this continuation was scheduled). Subscribe first, then check, to
                // prevent a window where the event fires between check and subscribe.
                if ((expectedDocumentId == null || frame.DocumentId == expectedDocumentId) &&
                    frame.LifecycleEvents.Contains(targetLifecycleEvent) &&
                    (string.IsNullOrEmpty(expectedDocumentId)
                        ? string.Equals(
                            NavigationTimeout.WithoutUserInfo(frame.Url),
                            NavigationTimeout.WithoutUserInfo(url),
                            StringComparison.Ordinal)
                        : true))
                {
                    frame.Url = NavigationTimeout.PreserveUserInfo(url, frame.Url);
                    return;
                }

                using var cts = new System.Threading.CancellationTokenSource(waitMs);
                cts.Token.Register(
                    () => lifecycleTcs.TrySetException(
                        NavigationTimeout.Exceeded(apiName, url, NavigationTimeout.WaitUntilName(waitUntil), timeout)));

                await lifecycleTcs.Task.ConfigureAwait(false);
                frame.Url = NavigationTimeout.PreserveUserInfo(url, frame.Url);
            }
            finally
            {
                Closed -= OnClosed;
                frame.LifecycleChanged -= OnLifecycle;
                _frameManager.FrameDetached -= OnDetached;
                _frameManager.FrameNavigated -= OnNavigated;
            }

            void ThrowIfWebUiWouldCrashIsolatedContext()
            {
                CRBrowserContext owner = _browser.DefaultContext != null && _browser.DefaultContext.Pages.Contains(this)
                    ? _browser.DefaultContext
                    : null;
                if (owner == null)
                {
                    foreach (CRBrowserContext context in _browser.Contexts)
                    {
                        if (context.Pages.Contains(this))
                        {
                            owner = context;
                            break;
                        }
                    }
                }

                if (owner == null || owner.IsPersistent)
                {
                    return;
                }

                Match match = Regex.Match(url, @"^(?:view-source:)?(?:chrome|edge):\/*([^/?#]+)", RegexOptions.IgnoreCase);
                string host = match.Success && Uri.TryCreate("http://" + match.Groups[1].Value, UriKind.Absolute, out Uri parsed)
                    ? parsed.Host
                    : string.Empty;
                if (string.IsNullOrEmpty(host))
                {
                    return;
                }

                bool isEdge = (_browser.UserAgent ?? string.Empty).Contains("Edg/", StringComparison.Ordinal);
                bool crashes = isEdge
                    ? string.Equals(host, "history", StringComparison.OrdinalIgnoreCase)
                    : host.Equals("apps", StringComparison.OrdinalIgnoreCase)
                        || host.Equals("extensions", StringComparison.OrdinalIgnoreCase)
                        || host.Equals("help", StringComparison.OrdinalIgnoreCase)
                        || host.Equals("history", StringComparison.OrdinalIgnoreCase)
                        || host.Equals("password-manager", StringComparison.OrdinalIgnoreCase)
                        || host.Equals("settings", StringComparison.OrdinalIgnoreCase);
                if (crashes)
                {
                    throw new PlaywrightNativeException(
                        "Cannot navigate to \"" + url + "\": this page is not available in an isolated browser context, and opening it crashes the browser. Use browserType.launchPersistentContext() instead.");
                }
            }
        }

        /// <summary>
        /// Navigates <paramref name="frame"/> and returns the last document
        /// <see cref="CRResponse"/> observed during that navigation, if any.
        /// </summary>
        /// <param name="frame">The frame to navigate.</param>
        /// <param name="url">The URL to navigate to.</param>
        /// <param name="waitUntil">The lifecycle event to wait for.</param>
        /// <param name="timeout">Maximum time to wait in milliseconds.</param>
        /// <param name="referrer">Optional referrer URL.</param>
        /// <returns>The navigation response, or <see langword="null"/>.</returns>
        internal async Task<CRResponse> GoToFrameCapturingResponseAsync(
            Frame frame,
            string url,
            WaitUntilState waitUntil = WaitUntilState.Load,
            int timeout = 30_000,
            string referrer = null)
        {
            CRResponse captured = null;
            void OnResponse(object sender, CRResponse response)
            {
                if (response?.Request != null
                    && response.Request.IsNavigationRequest
                    && response.Request.Frame == frame)
                {
                    captured = response;
                }
            }

            ResponseReceived += OnResponse;
            try
            {
                try
                {
                    await GoToFrameAsync(frame, url, waitUntil, timeout, referrer).ConfigureAwait(false);
                }
                catch (NavigationException ex) when (
                    captured != null
                    && ex.Message != null
                    && ex.Message.Contains("ERR_HTTP_RESPONSE_CODE_FAILURE", StringComparison.Ordinal))
                {
                    return captured;
                }

                if (!string.IsNullOrEmpty(url)
                    && (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                        || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                return captured;
            }
            finally
            {
                ResponseReceived -= OnResponse;
            }
        }

        /// <summary>
        /// Waits for the main frame's <c>load</c> lifecycle event.
        /// </summary>
        /// <param name="timeout">Maximum time to wait in milliseconds.</param>
        /// <returns>A task that completes when the load event fires.</returns>
        internal async Task WaitForLoadAsync(int timeout = 30_000)
        {
            // Note: Do not short-circuit on MainFrame.LifecycleEvents here — those may be
            // stale events from the previous document (e.g. about:blank load). Always wait
            // for a fresh event via the TCS subscription.
            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnLifecycle(string name)
            {
                if (name == "load")
                {
                    tcs.TrySetResult(true);
                }
            }

            MainFrame.LifecycleChanged += OnLifecycle;

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                cts.Token.Register(() => tcs.TrySetException(
                    new TimeoutException($"Waiting for load event timed out after {timeout}ms")));

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                MainFrame.LifecycleChanged -= OnLifecycle;
            }
        }

        /// <summary>
        /// Captures a screenshot of the page or a subregion via CDP
        /// <c>Page.captureScreenshot</c>. Returns raw image bytes (PNG by default,
        /// JPEG if requested).
        /// </summary>
        /// <param name="options">Screenshot options. Defaults to a full-viewport PNG.</param>
        /// <returns>The image bytes.</returns>
        internal async Task<byte[]> ScreenshotAsync(ScreenshotOptions options = null)
        {
            options ??= new ScreenshotOptions();

            System.Collections.Generic.Dictionary<string, object> parameters = new()
            {
                ["format"] = options.Format,
                ["captureBeyondViewport"] = options.FullPage || options.CaptureBeyondViewport,
            };

            if (options.Quality.HasValue && ScreenshotFormat.SupportsQuality(options.Format))
            {
                parameters["quality"] = options.Quality.Value;
            }

            if (options.Clip != null)
            {
                parameters["clip"] = new
                {
                    x = options.Clip.X,
                    y = options.Clip.Y,
                    width = options.Clip.Width,
                    height = options.Clip.Height,
                    scale = options.Clip.Scale > 0 ? options.Clip.Scale : 1.0,
                };
            }

            if (options.OmitBackground && options.Format != "jpeg")
            {
                await _client.SendAsync("Emulation.setDefaultBackgroundColorOverride", new
                {
                    color = new { r = 0, g = 0, b = 0, a = 0 },
                }).ConfigureAwait(false);
            }

            Frame main = MainFrame;
            if (main != null)
            {
                IReadOnlyCollection<string> lifecycle = main.LifecycleEvents;
                if (!lifecycle.Contains("DOMContentLoaded") && !lifecycle.Contains("load"))
                {
                    throw new PlaywrightNativeException("Cannot take a screenshot while page is navigating");
                }
            }

            try
            {
                return await CaptureWithNavigationRetryAsync(parameters).ConfigureAwait(false);
            }
            finally
            {
                if (options.OmitBackground && options.Format != "jpeg")
                {
                    await _client.SendAsync("Emulation.setDefaultBackgroundColorOverride").ConfigureAwait(false);
                }
            }
        }

        internal async Task<byte[]> CaptureWithNavigationRetryAsync(System.Collections.Generic.Dictionary<string, object> parameters)
        {
            const string navigating = "Cannot take a screenshot while page is navigating";
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    JsonElement? response = await _client.SendAsync("Page.captureScreenshot", parameters)
                        .WithTimeout(
                            TimeSpan.FromSeconds(8),
                            _ => new TimeoutException(navigating))
                        .ConfigureAwait(false);
                    if (!response.HasValue || !response.Value.TryGetProperty("data", out JsonElement data))
                    {
                        throw new PlaywrightNativeException("Page.captureScreenshot returned no data.");
                    }

                    string base64 = data.GetString();
                    return string.IsNullOrEmpty(base64) ? Array.Empty<byte>() : Convert.FromBase64String(base64);
                }
                catch (Exception ex) when (
                    ex is TimeoutException
                    || (ex is PlaywrightNativeException
                        && (ex.Message.Contains("Not attached to an active page", StringComparison.Ordinal)
                            || ex.Message.Contains("Cannot take screenshot with 0 width", StringComparison.Ordinal)
                            || ex.Message.Contains("Cannot take screenshot with 0 height", StringComparison.Ordinal)
                            || ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                            || ex.Message.Contains("Cannot find context with specified id", StringComparison.Ordinal)
                            || ex.Message.Contains(navigating, StringComparison.Ordinal))))
                {
                    if (attempt >= 20)
                    {
                        throw new PlaywrightNativeException(navigating);
                    }

                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Generates a PDF of the page via CDP <c>Page.printToPDF</c>. Chromium headless
        /// only — throws in headful mode. Returns raw PDF bytes.
        /// </summary>
        /// <param name="landscape">Paper orientation. Defaults to portrait.</param>
        /// <param name="printBackground">Include CSS backgrounds. Defaults to false.</param>
        /// <param name="paperWidthInches">Paper width in inches. Defaults to 8.5.</param>
        /// <param name="paperHeightInches">Paper height in inches. Defaults to 11.</param>
        /// <param name="scale">Page scale (1 is 100%). Omit to use Chromium's default.</param>
        /// <param name="width">Paper width (CSS length). Defaults to 8.5in.</param>
        /// <param name="height">Paper height (CSS length). Defaults to 11in.</param>
        /// <param name="format">Named paper format (Letter, A4, …). Used when width/height are omitted.</param>
        /// <param name="margin">Paper margins. CSS lengths, converted to inches.</param>
        /// <param name="pageRanges">Paper pages to print, e.g. <c>1-5, 8, 11-13</c>.</param>
        /// <param name="displayHeaderFooter">Whether to show header and footer. Defaults to false.</param>
        /// <param name="headerTemplate">HTML template for the print header.</param>
        /// <param name="footerTemplate">HTML template for the print footer.</param>
        /// <param name="preferCSSPageSize">
        /// When <see langword="true"/>, use CSS <c>@page</c> size instead of
        /// <paramref name="width"/>/<paramref name="height"/>/<paramref name="format"/>.
        /// </param>
        /// <param name="tagged">When <see langword="true"/>, generate a tagged (accessible) PDF.</param>
        /// <param name="outline">When <see langword="true"/>, embed a document outline.</param>
        /// <returns>The PDF bytes.</returns>
        internal async Task<byte[]> PdfAsync(
            bool landscape = false,
            bool printBackground = false,
            double paperWidthInches = 8.5,
            double paperHeightInches = 11,
            float? scale = null,
            string width = null,
            string height = null,
            string format = null,
            Margin margin = null,
            string pageRanges = null,
            bool? displayHeaderFooter = null,
            string headerTemplate = null,
            string footerTemplate = null,
            bool? preferCSSPageSize = null,
            bool? tagged = null,
            bool? outline = null)
        {
            double resolvedWidth = PdfPaperSize.ToInches(width) ?? paperWidthInches;
            double resolvedHeight = PdfPaperSize.ToInches(height) ?? paperHeightInches;
            if (!string.IsNullOrWhiteSpace(format) && string.IsNullOrWhiteSpace(width) && string.IsNullOrWhiteSpace(height))
            {
                (resolvedWidth, resolvedHeight) = PdfPaperSize.FormatToInches(format);
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["landscape"] = landscape,
                ["printBackground"] = printBackground,
                ["paperWidth"] = resolvedWidth,
                ["paperHeight"] = resolvedHeight,
                ["transferMode"] = "ReturnAsBase64",
            };

            if (scale.HasValue)
            {
                parameters["scale"] = scale.Value;
            }

            if (margin != null)
            {
                parameters["marginTop"] = PdfPaperSize.ToInches(margin.Top) ?? 0;
                parameters["marginBottom"] = PdfPaperSize.ToInches(margin.Bottom) ?? 0;
                parameters["marginLeft"] = PdfPaperSize.ToInches(margin.Left) ?? 0;
                parameters["marginRight"] = PdfPaperSize.ToInches(margin.Right) ?? 0;
            }

            if (!string.IsNullOrWhiteSpace(pageRanges))
            {
                parameters["pageRanges"] = pageRanges;
            }

            if (displayHeaderFooter.HasValue)
            {
                parameters["displayHeaderFooter"] = displayHeaderFooter.Value;
            }

            if (headerTemplate != null)
            {
                parameters["headerTemplate"] = headerTemplate;
            }

            if (footerTemplate != null)
            {
                parameters["footerTemplate"] = footerTemplate;
            }

            if (preferCSSPageSize.HasValue)
            {
                parameters["preferCSSPageSize"] = preferCSSPageSize.Value;
            }

            if (tagged.HasValue)
            {
                parameters["generateTaggedPDF"] = tagged.Value;
            }

            if (outline.HasValue)
            {
                parameters["generateDocumentOutline"] = outline.Value;
            }

            JsonElement? response = await _client.SendAsync("Page.printToPDF", parameters).ConfigureAwait(false);

            if (!response.HasValue || !response.Value.TryGetProperty("data", out JsonElement data))
            {
                throw new PlaywrightNativeException("Page.printToPDF returned no data.");
            }

            string base64 = data.GetString();
            return string.IsNullOrEmpty(base64) ? Array.Empty<byte>() : Convert.FromBase64String(base64);
        }

        /// <summary>
        /// Overrides the viewport dimensions and device scale factor via CDP
        /// <c>Emulation.setDeviceMetricsOverride</c>. Pass <c>null</c> to reset.
        /// </summary>
        /// <param name="size">Viewport in CSS pixels, or null to clear the override.</param>
        /// <param name="deviceScaleFactor">Device pixel ratio (e.g. 2.0 for retina). Ignored when resetting.</param>
        /// <param name="isMobile">Whether to emulate mobile. Ignored when resetting.</param>
        /// <param name="screenWidth">Reported <c>window.screen.width</c>, or <see langword="null"/> to leave unset.</param>
        /// <param name="screenHeight">Reported <c>window.screen.height</c>, or <see langword="null"/> to leave unset.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal async Task SetViewportSizeAsync(
            Input.ViewportSize? size,
            double deviceScaleFactor = 1.0,
            bool isMobile = false,
            int? screenWidth = null,
            int? screenHeight = null)
        {
            if (size == null)
            {
                await _client.SendAsync("Emulation.clearDeviceMetricsOverride").ConfigureAwait(false);
                return;
            }

            Input.ViewportSize v = size.Value;
            int resolvedScreenWidth = screenWidth ?? v.Width;
            int resolvedScreenHeight = screenHeight ?? v.Height;
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["width"] = v.Width,
                ["height"] = v.Height,
                ["deviceScaleFactor"] = deviceScaleFactor,
                ["mobile"] = isMobile,
                ["screenWidth"] = resolvedScreenWidth,
                ["screenHeight"] = resolvedScreenHeight,
                ["screenOrientation"] = EmulatedViewport.ScreenOrientation(
                    isMobile,
                    resolvedScreenWidth,
                    resolvedScreenHeight),
            };

            await _client.SendAsync("Emulation.setDeviceMetricsOverride", parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a CDP <c>backendNodeId</c> to an element handle in the main world.
        /// </summary>
        /// <param name="backendNodeId">The backend node id from <c>Page.fileChooserOpened</c>.</param>
        /// <returns>The element handle, or <see langword="null"/>.</returns>
        internal Task<CRElementHandle> ResolveBackendNodeAsync(int backendNodeId)
            => ResolveBackendNodeAsync(backendNodeId, _client);

        /// <summary>
        /// Resolves a CDP <c>backendNodeId</c> on <paramref name="session"/>.
        /// </summary>
        /// <param name="backendNodeId">The backend node id from <c>Page.fileChooserOpened</c>.</param>
        /// <param name="session">The CDP session that owns the node.</param>
        /// <returns>The element handle, or <see langword="null"/>.</returns>
        internal async Task<CRElementHandle> ResolveBackendNodeAsync(int backendNodeId, CRSession session)
        {
            session ??= _client;
            CRExecutionContext context = null;
            foreach (Frame frame in _frameManager.Frames)
            {
                if (frame.ExecutionContext != null && frame.ExecutionContext.Session == session)
                {
                    context = frame.ExecutionContext;
                    break;
                }
            }

            context ??= await WaitForExecutionContextAsync().ConfigureAwait(false);
            JsonElement? response = await session.SendAsync("DOM.resolveNode", new
            {
                backendNodeId,
                executionContextId = context.ContextId,
            }).ConfigureAwait(false);

            if (response == null || !response.Value.TryGetProperty("object", out JsonElement remote))
            {
                return null;
            }

            return WrapElementHandle(context, remote);
        }

        /// <summary>
        /// Emulates CSS media type and/or <c>prefers-color-scheme</c>. Other emulated
        /// features (reduced motion, forced colors) are preserved.
        /// </summary>
        /// <param name="media">"screen", "print", empty string to reset, or <see langword="null"/> to leave unchanged.</param>
        /// <param name="colorScheme">"light", "dark", "no-preference", empty string to restore default light, or <see langword="null"/> when not updating.</param>
        /// <param name="updateColorScheme">Whether to change the color-scheme override. Omitted options must not reset.</param>
        /// <returns>A task that completes when the emulation has been applied.</returns>
        internal Task SetEmulatedMediaAsync(string media, string colorScheme, bool updateColorScheme = true)
        {
            if (media != null)
            {
                _emulatedMedia = media;
            }

            if (updateColorScheme)
            {
                _emulatedColorScheme = string.IsNullOrEmpty(colorScheme) ? "light" : colorScheme;
            }

            return ApplyEmulatedMediaAsync();
        }

        /// <summary>
        /// Emulates <c>prefers-reduced-motion</c>, <c>forced-colors</c>, and/or
        /// <c>prefers-contrast</c>. Other emulated media features are preserved.
        /// </summary>
        /// <param name="reducedMotion">"reduce", "no-preference", or <see langword="null"/> to reset.</param>
        /// <param name="updateReducedMotion">Whether to change the reduced-motion override.</param>
        /// <param name="forcedColors">"active", "none", or <see langword="null"/> to reset.</param>
        /// <param name="updateForcedColors">Whether to change the forced-colors override.</param>
        /// <param name="contrast">"more", "less", "no-preference", or <see langword="null"/> to reset.</param>
        /// <param name="updateContrast">Whether to change the contrast override.</param>
        /// <returns>A task that completes when the emulation has been applied.</returns>
        internal Task SetEmulatedMediaFeaturesAsync(
            string reducedMotion,
            bool updateReducedMotion,
            string forcedColors,
            bool updateForcedColors,
            string contrast = null,
            bool updateContrast = false)
        {
            if (updateReducedMotion)
            {
                _emulatedReducedMotion = reducedMotion ?? "no-preference";
            }

            if (updateForcedColors)
            {
                _emulatedForcedColors = forcedColors ?? "none";
            }

            if (updateContrast)
            {
                _emulatedContrast = contrast ?? "no-preference";
            }

            return ApplyEmulatedMediaAsync();
        }

        /// <summary>
        /// Emulates <c>prefers-color-scheme</c>. Pass <c>null</c> to reset.
        /// </summary>
        /// <param name="colorScheme">"light", "dark", "no-preference", or null.</param>
        /// <returns>A task that completes when the emulation has been applied.</returns>
        internal Task SetColorSchemeAsync(string colorScheme)
            => SetEmulatedMediaAsync(media: null, colorScheme: colorScheme);

        /// <summary>
        /// Overrides the User-Agent string via CDP <c>Emulation.setUserAgentOverride</c>.
        /// Pass <c>null</c> to reset.
        /// </summary>
        /// <param name="userAgent">The UA string to set, or null to reset.</param>
        /// <param name="acceptLanguage">Optional <c>Accept-Language</c> override.</param>
        /// <param name="isMobile">Context <c>isMobile</c> for Client Hints metadata.</param>
        /// <param name="includeMetadata">
        /// When <see langword="true"/>, send official <c>userAgentMetadata</c>
        /// so Chromium Client Hints match the override.
        /// </param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal async Task SetUserAgentAsync(string userAgent, string acceptLanguage = null, bool isMobile = false, bool includeMetadata = false)
        {
            _hasUserAgentOverride = true;
            _userAgentOverride = userAgent ?? string.Empty;
            _acceptLanguageOverride = acceptLanguage;
            _userAgentIsMobile = isMobile;
            _userAgentIncludeMetadata = includeMetadata;
            string ua = userAgent ?? string.Empty;
            object metadata = includeMetadata && !string.IsNullOrEmpty(ua)
                ? BuildUserAgentMetadata(ua, isMobile)
                : null;
            if (string.IsNullOrEmpty(acceptLanguage))
            {
                if (metadata != null)
                {
                    await _client.SendAsync("Emulation.setUserAgentOverride", new { userAgent = ua, userAgentMetadata = metadata }).ConfigureAwait(false);
                }
                else
                {
                    await _client.SendAsync("Emulation.setUserAgentOverride", new { userAgent = ua }).ConfigureAwait(false);
                }

                await BroadcastUserAgentAsync(ua, acceptLanguage, metadata).ConfigureAwait(false);
                return;
            }

            if (metadata != null)
            {
                await _client.SendAsync("Emulation.setUserAgentOverride", new
                {
                    userAgent = ua,
                    acceptLanguage,
                    userAgentMetadata = metadata,
                }).ConfigureAwait(false);
            }
            else
            {
                await _client.SendAsync("Emulation.setUserAgentOverride", new
                {
                    userAgent = ua,
                    acceptLanguage,
                }).ConfigureAwait(false);
            }

            try
            {
                await _client.SendAsync("Network.setUserAgentOverride", new
                {
                    userAgent = ua,
                    acceptLanguage,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Older Chromium builds only expose Emulation.setUserAgentOverride.
            }

            await BroadcastUserAgentAsync(ua, acceptLanguage, metadata).ConfigureAwait(false);

            static object BuildUserAgentMetadata(string userAgent, bool isMobile)
            {
                string platform = "Windows";
                string platformVersion = string.Empty;
                string architecture = "x86";
                Match android = Regex.Match(userAgent, @"Android (\d+(\.\d+)?(\.\d+)?)");
                Match iPhone = Regex.Match(userAgent, @"iPhone OS (\d+(_\d+)?)");
                Match iPad = Regex.Match(userAgent, @"iPad; CPU OS (\d+(_\d+)?)");
                Match macOS = Regex.Match(userAgent, @"Mac OS X (\d+(_\d+)?(_\d+)?)");
                Match windows = Regex.Match(userAgent, @"Windows\D+(\d+(\.\d+)?(\.\d+)?)");
                if (android.Success)
                {
                    platform = "Android";
                    platformVersion = android.Groups[1].Value;
                    architecture = "arm";
                }
                else if (iPhone.Success)
                {
                    platform = "iOS";
                    platformVersion = iPhone.Groups[1].Value;
                    architecture = "arm";
                }
                else if (iPad.Success)
                {
                    platform = "iOS";
                    platformVersion = iPad.Groups[1].Value;
                    architecture = "arm";
                }
                else if (macOS.Success)
                {
                    platform = "macOS";
                    platformVersion = macOS.Groups[1].Value;
                    if (!userAgent.Contains("Intel", StringComparison.Ordinal))
                    {
                        architecture = "arm";
                    }
                }
                else if (windows.Success)
                {
                    platform = "Windows";
                    platformVersion = windows.Groups[1].Value;
                }
                else if (userAgent.Contains("linux", StringComparison.OrdinalIgnoreCase))
                {
                    platform = "Linux";
                }

                if (userAgent.Contains("ARM", StringComparison.Ordinal))
                {
                    architecture = "arm";
                }

                return new
                {
                    mobile = isMobile,
                    model = string.Empty,
                    architecture,
                    platform,
                    platformVersion,
                };
            }
        }

        /// <summary>
        /// Overrides the locale via CDP <c>Emulation.setLocaleOverride</c>.
        /// </summary>
        /// <param name="locale">BCP 47 locale, for example <c>de-DE</c>.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal async Task SetLocaleOverrideAsync(string locale)
        {
            _localeOverride = locale ?? string.Empty;
            await _client.SendAsync("Emulation.setLocaleOverride", new { locale = _localeOverride }).ConfigureAwait(false);
            await SendToOopifSessionsAsync("Emulation.setLocaleOverride", new { locale = _localeOverride }).ConfigureAwait(false);
        }

        /// <summary>
        /// Overrides the timezone via CDP <c>Emulation.setTimezoneOverride</c>.
        /// </summary>
        /// <param name="timezoneId">IANA timezone id, for example <c>Europe/Paris</c>.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal async Task SetTimezoneOverrideAsync(string timezoneId)
        {
            _timezoneOverride = timezoneId ?? string.Empty;
            try
            {
                await _client.SendAsync("Emulation.setTimezoneOverride", new { timezoneId = _timezoneOverride }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (
                ex.Message.Contains("timezone", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("time zone", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightNativeException("Invalid timezone ID: " + timezoneId);
            }

            await SendToOopifSessionsAsync("Emulation.setTimezoneOverride", new { timezoneId = _timezoneOverride }).ConfigureAwait(false);
        }

        /// <summary>
        /// Emulates offline network conditions via CDP <c>Network.emulateNetworkConditions</c>.
        /// </summary>
        /// <param name="offline">Whether the page should appear offline.</param>
        /// <returns>A task that completes when the emulation has been applied.</returns>
        internal async Task SetOfflineAsync(bool offline)
        {
            _offline = offline;
            object parameters = new
            {
                offline,
                latency = 0,
                downloadThroughput = -1,
                uploadThroughput = -1,
            };
            await _client.SendAsync("Network.emulateNetworkConditions", parameters).ConfigureAwait(false);
            await SendToOopifSessionsAsync("Network.emulateNetworkConditions", parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Emulates a touch-capable viewport via <c>Emulation.setTouchEmulationEnabled</c>.
        /// </summary>
        /// <param name="enabled">Whether touch should be emulated.</param>
        /// <param name="configuration"><c>desktop</c> or <c>mobile</c> pointer/hover, or <see langword="null"/>.</param>
        /// <returns>A task that completes when the emulation has been applied.</returns>
        internal async Task SetTouchEmulationEnabledAsync(bool enabled, string configuration = null)
        {
            _touchEnabled = enabled;
            _touchConfiguration = configuration;
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["enabled"] = enabled,
            };
            if (!string.IsNullOrEmpty(configuration))
            {
                parameters["configuration"] = configuration;
            }

            await _client.SendAsync("Emulation.setTouchEmulationEnabled", parameters).ConfigureAwait(false);
            await SendToOopifSessionsAsync("Emulation.setTouchEmulationEnabled", parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Bypasses Content-Security-Policy via CDP <c>Page.setBypassCSP</c>.
        /// </summary>
        /// <param name="enabled">Whether CSP should be bypassed.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetBypassCSPAsync(bool enabled)
            => _client.SendAsync("Page.setBypassCSP", new { enabled });

        /// <summary>
        /// Overrides geolocation via CDP <c>Emulation.setGeolocationOverride</c>.
        /// </summary>
        /// <param name="geolocation">Latitude, longitude, and accuracy.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetGeolocationOverrideAsync(Geolocation geolocation)
        {
            if (geolocation == null)
            {
                return Task.CompletedTask;
            }

            return _client.SendAsync("Emulation.setGeolocationOverride", new
            {
                latitude = geolocation.Latitude,
                longitude = geolocation.Longitude,
                accuracy = geolocation.Accuracy ?? 0f,
            });
        }

        /// <summary>
        /// Ignores TLS certificate errors via CDP <c>Security.setIgnoreCertificateErrors</c>.
        /// </summary>
        /// <param name="ignore">Whether certificate errors should be ignored.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetIgnoreCertificateErrorsAsync(bool ignore)
            => _client.SendAsync("Security.setIgnoreCertificateErrors", new { ignore });

        /// <summary>
        /// Enables or disables page-script execution via
        /// <c>Emulation.setScriptExecutionDisabled</c>.
        /// </summary>
        /// <param name="enabled">When <see langword="false"/>, page scripts do not run.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetJavaScriptEnabledAsync(bool enabled)
            => _client.SendAsync("Emulation.setScriptExecutionDisabled", new { value = !enabled });

        /// <summary>
        /// Emulates a vision deficiency via CDP <c>Emulation.setEmulatedVisionDeficiency</c>.
        /// </summary>
        /// <param name="type">CDP type token, e.g. <c>none</c> or <c>achromatopsia</c>.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal Task SetEmulatedVisionDeficiencyAsync(string type)
            => _client.SendAsync("Emulation.setEmulatedVisionDeficiency", new { type = type ?? "none" });

        /// <summary>
        /// Reloads the main frame via CDP <c>Page.reload</c> and waits for lifecycle.
        /// </summary>
        /// <param name="waitUntil">Lifecycle event to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>The document response, if one was observed.</returns>
        internal async Task<CRResponse> ReloadAsync(WaitUntilState waitUntil, int timeout)
        {
            CRResponse captured = await RunMainFrameNavigationAsync(
                async () =>
                {
                    await _client.SendAsync("Page.reload").ConfigureAwait(false);
                    return true;
                },
                waitUntil,
                timeout,
                allowSameDocument: false).ConfigureAwait(false);

            // Official Playwright returns null for data: URL reloads (no HTTP document).
            if ((!string.IsNullOrEmpty(MainFrame.Url) && MainFrame.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(captured?.Url) && captured.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return captured;
        }

        /// <summary>
        /// Moves through session history by <paramref name="delta"/> entries.
        /// </summary>
        /// <param name="delta">Negative for back, positive for forward.</param>
        /// <param name="waitUntil">Lifecycle event to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>The document response, or <see langword="null"/> if there is no entry.</returns>
        internal Task<CRResponse> GoHistoryAsync(int delta, WaitUntilState waitUntil, int timeout)
            => RunMainFrameNavigationAsync(
                () => TryNavigateHistoryAsync(delta),
                waitUntil,
                timeout,
                allowSameDocument: true);

        /// <summary>
        /// Handles a browser-session <c>Browser.downloadWillBegin</c> routed to this page.
        /// </summary>
        /// <param name="guid">CDP download guid.</param>
        /// <param name="url">Download URL.</param>
        /// <param name="suggestedFilename">Suggested file name.</param>
        internal void NotifyDownloadWillBegin(string guid, string url, string suggestedFilename)
            => DownloadWillBegin?.Invoke(this, new DownloadWillBeginEventArgs(guid, url, suggestedFilename));

        /// <summary>
        /// Handles a browser-session <c>Browser.downloadProgress</c> routed to this page.
        /// </summary>
        /// <param name="guid">CDP download guid.</param>
        /// <param name="state">inProgress, completed, or canceled.</param>
        /// <param name="error">Optional failure message.</param>
        internal void NotifyDownloadProgress(string guid, string state, string error)
            => DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(guid, state, error));

        /// <summary>
        /// Cancels an in-progress download via <c>Browser.cancelDownload</c>.
        /// </summary>
        /// <param name="guid">CDP download guid.</param>
        /// <param name="browserContextId">Owning CDP browser context id, if any.</param>
        /// <returns>A task that completes when the cancel has been issued.</returns>
        internal Task CancelDownloadAsync(string guid, string browserContextId)
        {
            object parameters = string.IsNullOrEmpty(browserContextId)
                ? new { guid }
                : new { guid, browserContextId };
            return _browser.Connection.RootSession.SendAsync("Browser.cancelDownload", parameters);
        }

        /// <summary>
        /// Called when the CDP <c>Target.detachedFromTarget</c> event fires for this page's target.
        /// </summary>
        internal void DidClose()
        {
            _logger?.LogDebug("Page {TargetId} closed.", _targetId);
            _client.MessageReceived -= OnSessionEvent;
            _client.Dispose();

            // Release any pending network-idle timers so they do not keep frames alive
            // via thread-pool queue references after the page is detached.
            DisposeFrameNetworkIdleTimersRecursive(MainFrame);
            _networkManager.AbortInflightClosed();

            _initializationTcs.TrySetResult(true);
            _firstNonInitialNavigationTcs.TrySetResult(true);
            Closed?.Invoke(this, EventArgs.Empty);
            _closedTcs.TrySetResult(true);
        }

        /// <summary>
        /// Marks this page as reported on the context <c>page</c> event.
        /// </summary>
        /// <returns><see langword="true"/> when this is the first report.</returns>
        internal bool TryMarkReportedAsNew()
            => Interlocked.Exchange(ref _reportedAsNew, 1) == 0;

        /// <summary>
        /// Consumes the next queued <c>Page.windowOpen</c> feature size.
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
        /// Fires the <see cref="WebSocketCreated"/> event.
        /// </summary>
        /// <param name="socket">The socket that was created.</param>
        internal void OnWebSocketCreated(IWebSocket socket) => WebSocketCreated?.Invoke(this, socket);

        /// <summary>
        /// Fires the <see cref="RequestCreated"/> event.
        /// Called by <see cref="CRNetworkManager"/> when a new network request is observed.
        /// </summary>
        /// <param name="request">The request that was created.</param>
        internal void OnRequestCreated(CRRequest request)
        {
            // Official page.goto data: navigations do not emit Request events.
            if (request != null
                && request.IsNavigationRequest
                && !string.IsNullOrEmpty(request.Url)
                && request.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RequestCreated?.Invoke(this, request);
        }

        /// <summary>
        /// Fires the <see cref="RequestFinished"/> event.
        /// Called by <see cref="CRNetworkManager"/> when a network request finishes loading.
        /// </summary>
        /// <param name="request">The request that finished.</param>
        internal void OnRequestFinished(CRRequest request) => RequestFinished?.Invoke(this, request);

        /// <summary>
        /// Fires the <see cref="RequestFailed"/> event.
        /// Called by <see cref="CRNetworkManager"/> when a network request fails.
        /// </summary>
        /// <param name="request">The request that failed.</param>
        internal void OnRequestFailed(CRRequest request) => RequestFailed?.Invoke(this, request);

        /// <summary>
        /// Fires the <see cref="ResponseReceived"/> event.
        /// Called by <see cref="CRNetworkManager"/> when a network response is received.
        /// </summary>
        /// <param name="response">The response that was received.</param>
        internal void OnResponseReceived(CRResponse response) => ResponseReceived?.Invoke(this, response);

        /// <summary>
        /// Invoked by <see cref="CRBrowser"/> when a new target whose <c>openerId</c>
        /// matches this page's target attaches. Fires <see cref="PopupOpened"/>.
        /// </summary>
        /// <param name="popupPage">The newly-created popup page.</param>
        internal void FirePopupOpened(CRPage popupPage)
        {
            if (popupPage != null)
            {
                popupPage.PopupReported = true;
            }

            PopupOpened?.Invoke(this, popupPage);
        }

        /// <summary>
        /// Official popup <c>reportAsNew</c>: wait for init, then for a
        /// non-blank URL so <c>target=_blank</c> reports the navigated page.
        /// </summary>
        /// <returns>A task that completes when the popup can be reported.</returns>
        internal async Task PrepareForPopupReportAsync()
        {
            try
            {
                await InitializedTask.ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                return;
            }

            if (Opener == null || _closedTcs.Task.IsCompleted)
            {
                return;
            }

            if (!PopupOpenedHelper.IsBlankUrl(_frameManager.MainFrame?.Url))
            {
                return;
            }

            await Task.WhenAny(
                    _firstNonBlankNavigationTcs.Task,
                    _closedTcs.Task,
                    Task.Delay(5_000))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Official Chromium input epilogue: a <c>Page.enable</c> round-trip so
        /// <c>frameRequestedNavigation</c> events scheduled by the last input
        /// are flushed before click auto-wait decides whether to wait.
        /// </summary>
        /// <returns>A task that completes when the protocol call finishes.</returns>
        internal Task InputActionEpilogueAsync()
            => _client.SendAsync("Page.enable");

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
                InputActionEpilogueAsync,
                waitAfter,
                timeout,
                action,
                PublicPage,
                CommitLiveSameDocumentUrl);

        private void CommitLiveSameDocumentUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            _frameManager.FrameCommittedSameDocumentNavigation(MainFrame.FrameId, url);
        }

        private async Task<JsonElement?> TryDescribeNodeAsync(CRSession session, string objectId)
        {
            try
            {
                return await session.SendAsync("DOM.describeNode", new { objectId }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return null;
            }
        }

        private Task ApplyEmulatedMediaAsync()
        {
            // Official Chromium always sends media plus the four CSS features. Empty
            // media is no-override (screen). Defaults match Playwright context options:
            // colorScheme light, reducedMotion no-preference, forcedColors none,
            // contrast no-preference.
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["media"] = _emulatedMedia ?? string.Empty,
                ["features"] = new object[]
                {
                    new { name = "prefers-color-scheme", value = _emulatedColorScheme ?? "light" },
                    new { name = "prefers-reduced-motion", value = _emulatedReducedMotion ?? "no-preference" },
                    new { name = "forced-colors", value = _emulatedForcedColors ?? "none" },
                    new { name = "prefers-contrast", value = _emulatedContrast ?? "no-preference" },
                },
            };
            return ApplyEmulatedMediaToAllSessionsAsync(parameters);
        }

        private async Task ApplyEmulatedMediaToAllSessionsAsync(Dictionary<string, object> parameters)
        {
            await _client.SendAsync("Emulation.setEmulatedMedia", parameters).ConfigureAwait(false);
            await SendToOopifSessionsAsync("Emulation.setEmulatedMedia", parameters).ConfigureAwait(false);
        }

        private Task ApplyEmulatedMediaOnSessionAsync(CRSession session)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["media"] = _emulatedMedia ?? string.Empty,
                ["features"] = new object[]
                {
                    new { name = "prefers-color-scheme", value = _emulatedColorScheme ?? "light" },
                    new { name = "prefers-reduced-motion", value = _emulatedReducedMotion ?? "no-preference" },
                    new { name = "forced-colors", value = _emulatedForcedColors ?? "none" },
                    new { name = "prefers-contrast", value = _emulatedContrast ?? "no-preference" },
                },
            };
            return SendIgnoreClosedAsync(session, "Emulation.setEmulatedMedia", parameters);
        }

        private async Task BroadcastUserAgentAsync(string userAgent, string acceptLanguage, object metadata)
        {
            foreach (CRSession session in _oopifSessions.Values)
            {
                await SetUserAgentOnSessionAsync(session, userAgent, acceptLanguage, _userAgentIsMobile, metadata != null).ConfigureAwait(false);
            }
        }

        private async Task SetUserAgentOnSessionAsync(
            CRSession session,
            string userAgent,
            string acceptLanguage,
            bool isMobile,
            bool includeMetadata)
        {
            if (session == null)
            {
                return;
            }

            string ua = userAgent ?? string.Empty;
            if (includeMetadata && !string.IsNullOrEmpty(ua))
            {
                await SendIgnoreClosedAsync(
                    session,
                    "Emulation.setUserAgentOverride",
                    new
                    {
                        userAgent = ua,
                        acceptLanguage,
                        userAgentMetadata = new
                        {
                            mobile = isMobile,
                            model = string.Empty,
                            architecture = "arm",
                            platform = "iOS",
                            platformVersion = string.Empty,
                        },
                    }).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrEmpty(acceptLanguage))
            {
                await SendIgnoreClosedAsync(session, "Emulation.setUserAgentOverride", new { userAgent = ua }).ConfigureAwait(false);
                return;
            }

            await SendIgnoreClosedAsync(session, "Emulation.setUserAgentOverride", new { userAgent = ua, acceptLanguage }).ConfigureAwait(false);
            await SendIgnoreClosedAsync(session, "Network.setUserAgentOverride", new { userAgent = ua, acceptLanguage }).ConfigureAwait(false);
        }

        private async Task SendToOopifSessionsAsync(string method, object parameters)
        {
            foreach (CRSession session in _oopifSessions.Values)
            {
                await SendIgnoreClosedAsync(session, method, parameters).ConfigureAwait(false);
            }
        }

        private async Task SendIgnoreClosedAsync(CRSession session, string method, object parameters)
        {
            if (session == null)
            {
                return;
            }

            try
            {
                await session.SendAsync(method, parameters).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        private void DisposeFrameNetworkIdleTimersRecursive(Frame frame)
        {
            if (frame == null)
            {
                return;
            }

            frame.DisposeNetworkIdleTimer();
            foreach (Frame child in frame.ChildFrames)
            {
                DisposeFrameNetworkIdleTimersRecursive(child);
            }
        }

        private async Task<CRResponse> RunMainFrameNavigationAsync(
            Func<Task<bool>> navigate,
            WaitUntilState waitUntil,
            int timeout,
            bool allowSameDocument)
        {
            if (navigate == null)
            {
                throw new ArgumentNullException(nameof(navigate));
            }

            string targetEvent = WaitUntilMapping.ToLifecycleEvent(waitUntil);
            string previousDocumentId = MainFrame.DocumentId;
            string previousUrl = MainFrame.Url;
            CRResponse captured = null;
            TaskCompletionSource<bool> committed = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnResponse(object sender, CRResponse response)
            {
                if (response?.Request != null
                    && response.Request.IsNavigationRequest
                    && response.Request.Frame == MainFrame)
                {
                    captured = response;
                }
            }

            void TryComplete()
            {
                bool newDocument = !string.Equals(MainFrame.DocumentId, previousDocumentId, StringComparison.Ordinal);
                bool lifecycleReady = MainFrame.LifecycleEvents.Contains(targetEvent);
                bool urlChanged = !string.Equals(MainFrame.Url, previousUrl, StringComparison.Ordinal);

                // Full navigation (reload / network history): new document + requested lifecycle.
                // BFCache / same-document history (goBack/goForward): URL changes without a new loader.
                // Reload must not resolve on pushState — that is same-document, not a reload.
                if ((newDocument && lifecycleReady) || (allowSameDocument && urlChanged && !newDocument))
                {
                    committed.TrySetResult(true);
                }
            }

            void OnLifecycle(string name)
            {
                if (name == targetEvent)
                {
                    TryComplete();
                }
            }

            void OnNavigated() => TryComplete();

            // Subscribe before sending the command so a fast history restore cannot
            // commit (and fire load) in the gap between SendAsync completing and the waiter starting.
            ResponseReceived += OnResponse;
            MainFrame.LifecycleChanged += OnLifecycle;
            MainFrame.Navigated += OnNavigated;
            try
            {
                bool proceeded = await navigate().ConfigureAwait(false);
                if (!proceeded)
                {
                    return null;
                }

                TryComplete();
                if (committed.Task.IsCompleted)
                {
                    return captured;
                }

                using System.Threading.CancellationTokenSource cts = new(timeout);
                cts.Token.Register(() =>
                {
                    if (allowSameDocument && !string.Equals(MainFrame.Url, previousUrl, StringComparison.Ordinal))
                    {
                        committed.TrySetResult(true);
                        return;
                    }

                    committed.TrySetException(new TimeoutException(
                        $"Waiting for {targetEvent} after navigation timed out after {timeout}ms"));
                });

                await committed.Task.ConfigureAwait(false);
                return captured;
            }
            finally
            {
                ResponseReceived -= OnResponse;
                MainFrame.LifecycleChanged -= OnLifecycle;
                MainFrame.Navigated -= OnNavigated;
            }
        }

        private async Task<bool> TryNavigateHistoryAsync(int delta)
        {
            JsonElement? history = null;
            const int attempts = 8;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    history = await _client.SendAsync("Page.getNavigationHistory").ConfigureAwait(false);
                    break;
                }
                catch (PlaywrightNativeException ex) when (
                    attempt < attempts - 1
                    && ex.Message != null
                    && ex.Message.Contains("Not attached to an active page", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }

            if (!history.HasValue
                || !history.Value.TryGetProperty("currentIndex", out JsonElement indexEl)
                || !history.Value.TryGetProperty("entries", out JsonElement entriesEl)
                || entriesEl.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            int currentIndex = indexEl.GetInt32();
            int targetIndex = currentIndex + delta;
            if (targetIndex < 0 || targetIndex >= entriesEl.GetArrayLength())
            {
                return false;
            }

            JsonElement entry = entriesEl[targetIndex];
            if (!entry.TryGetProperty("id", out JsonElement idEl))
            {
                return false;
            }

            await _client.SendAsync("Page.navigateToHistoryEntry", new { entryId = idEl.GetInt32() }).ConfigureAwait(false);
            return true;
        }

        private void OnSessionEvent(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Runtime.executionContextCreated":
                    OnExecutionContextCreated(parameters);
                    break;
                case "Runtime.executionContextDestroyed":
                    OnExecutionContextDestroyed(parameters);
                    break;
                case "Runtime.executionContextsCleared":
                    OnExecutionContextsCleared();
                    break;
                case "Page.lifecycleEvent":
                    OnLifecycleEvent(parameters);
                    break;
                case "Page.frameNavigated":
                    OnFrameNavigated(parameters);
                    break;
                case "Page.frameAttached":
                    OnFrameAttached(parameters);
                    break;
                case "Page.frameDetached":
                    OnFrameDetached(parameters);
                    break;
                case "Page.navigatedWithinDocument":
                    OnNavigatedWithinDocument(parameters);
                    break;
                case "Page.frameRequestedNavigation":
                    OnFrameRequestedNavigation(parameters);
                    break;
                case "Runtime.bindingCalled":
                    OnBindingCalled(parameters);
                    break;
                case "Page.javascriptDialogOpening":
                    OnDialogOpening(parameters);
                    break;
                case "Page.javascriptDialogClosed":
                    DialogClosedInBrowser?.Invoke(this, EventArgs.Empty);
                    break;
                case "Runtime.consoleAPICalled":
                    OnConsoleAPICalled(parameters);
                    break;
                case "Log.entryAdded":
                    OnLogEntryAdded(parameters);
                    break;
                case "Runtime.exceptionThrown":
                    OnExceptionThrown(parameters);
                    break;
                case "Page.downloadWillBegin":
                    OnDownloadWillBegin(parameters);
                    break;
                case "Page.downloadProgress":
                    OnDownloadProgress(parameters);
                    break;
                case "Page.fileChooserOpened":
                    OnFileChooserOpened(parameters);
                    break;
                case "Page.windowOpen":
                    OnWindowOpen(parameters);
                    break;
                case "Target.attachedToTarget":
                    OnAttachedToTarget(parameters);
                    break;
                case "Target.detachedFromTarget":
                    OnDetachedFromTarget(parameters);
                    break;
                case "Inspector.targetCrashed":
                    OnInspectorTargetCrashed();
                    break;
            }
        }

        private void OnInspectorTargetCrashed()
        {
            if (_crashed)
            {
                return;
            }

            _crashed = true;
            Crashed?.Invoke(this, EventArgs.Empty);
        }

        private void OnAttachedToTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            if (!payload.TryGetProperty("targetInfo", out JsonElement targetInfo)
                || !payload.TryGetProperty("sessionId", out JsonElement sessionEl))
            {
                return;
            }

            string sessionId = sessionEl.GetString();
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            string type = targetInfo.TryGetProperty("type", out JsonElement typeEl)
                ? typeEl.GetString()
                : string.Empty;
            string targetId = targetInfo.TryGetProperty("targetId", out JsonElement targetIdEl)
                ? targetIdEl.GetString()
                : string.Empty;
            string parentFrameId = targetInfo.TryGetProperty("parentFrameId", out JsonElement parentFrameEl)
                ? parentFrameEl.GetString()
                : string.Empty;
            CRSession child = _client.CreateChildSession(sessionId);
            if (string.Equals(type, "iframe", StringComparison.Ordinal)
                || string.Equals(type, "guest", StringComparison.Ordinal))
            {
                AttachOopifSession(child, targetId, parentFrameId);
                return;
            }

            if (!string.Equals(type, "worker", StringComparison.Ordinal))
            {
                _ = child.SendAsync("Runtime.runIfWaitingForDebugger");
                return;
            }

            string url = targetInfo.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : string.Empty;
            CRWorker worker = new(child, sessionId, url);
            if (!_workers.TryAdd(sessionId, worker))
            {
                return;
            }

            worker.ExceptionThrown += (_, error) => PageError?.Invoke(this, error);
            child.MessageReceived += OnWorkerSessionMessage;
            _ = AttachWorkerAndReportAsync(worker, parentFrameId);
        }

        private void OnWorkerSessionMessage(string method, JsonElement? parameters)
        {
            if (method == "Target.attachedToTarget")
            {
                OnAttachedToTarget(parameters);
                return;
            }

            if (method == "Target.detachedFromTarget")
            {
                OnDetachedFromTarget(parameters);
            }
        }

        private async Task AttachWorkerAndReportAsync(CRWorker worker, string parentFrameId)
        {
            try
            {
                await worker.EnableRuntimeAsync().ConfigureAwait(false);
                Frame ownerFrame = !string.IsNullOrEmpty(parentFrameId)
                    ? _frameManager.FrameById(parentFrameId)
                    : null;
                await _networkManager.AddWorkerSessionAsync(
                    worker.Session,
                    ownerFrame ?? MainFrame,
                    isWorker: true,
                    parentFrameId).ConfigureAwait(false);
                try
                {
                    await worker.Session.SendAsync(
                        "Target.setAutoAttach",
                        new { autoAttach = true, waitForDebuggerOnStart = true, flatten = true }).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                    // Nested-worker auto-attach is best-effort on older Chrome.
                }

                // Official adds the worker before resume so page.console listeners
                // are attached before the worker script runs.
                WorkerCreated?.Invoke(this, worker);
                await worker.ResumeDebuggerAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // The worker may close before domains are enabled.
                WorkerCreated?.Invoke(this, worker);
            }
        }

        private void OnDetachedFromTarget(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            if (parameters.Value.TryGetProperty("sessionId", out JsonElement sessionEl))
            {
                string sessionId = sessionEl.GetString();
                if (RemoveWorker(sessionId))
                {
                    return;
                }
            }

            if (!parameters.Value.TryGetProperty("targetId", out JsonElement targetEl))
            {
                return;
            }

            string targetId = targetEl.GetString();
            string detachedSessionId = parameters.Value.TryGetProperty("sessionId", out JsonElement detachedSessionEl)
                ? detachedSessionEl.GetString()
                : string.Empty;
            if (string.IsNullOrEmpty(targetId)
                || string.IsNullOrEmpty(detachedSessionId)
                || !_oopifSessions.TryGetValue(targetId, out CRSession current)
                || !string.Equals(current.SessionId, detachedSessionId, StringComparison.Ordinal))
            {
                // Browser-level auto-attach and page setAutoAttach both create
                // iframe sessions. A stale extra session must not drop the frame.
                return;
            }

            // Official FrameSession._onDetachedFromTarget: swappedIn means
            // remote → local already happened. Delay off the receive thread so
            // an in-flight parent frameAttached can mark swappedIn. Do not send
            // Page.enable — that deadlocks the main session under load.
            _ = ConfirmRemoteOopifDetachAsync(targetId, detachedSessionId);
        }

        private async Task ConfirmRemoteOopifDetachAsync(string targetId, string sessionId)
        {
            await Task.Delay(50).ConfigureAwait(false);

            if (!_oopifSessions.TryGetValue(targetId, out CRSession current)
                || !string.Equals(current.SessionId, sessionId, StringComparison.Ordinal))
            {
                return;
            }

            if (!_oopifSwappedIn.ContainsKey(targetId))
            {
                Frame detaching = _frameManager.FrameById(targetId);
                if (detaching != null)
                {
                    _networkManager.FinishInflightForDetachedFrame(detaching);
                }

                _frameManager.FrameDetachedFromTarget(targetId);
            }

            UnregisterOopifSession(targetId);
        }

        private bool RemoveWorker(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || !_workers.TryRemove(sessionId, out CRWorker worker))
            {
                return false;
            }

            foreach (ConcurrentDictionary<string, byte> owned in _oopifOwnedWorkers.Values)
            {
                owned.TryRemove(sessionId, out _);
            }

            worker.Session.MessageReceived -= OnWorkerSessionMessage;
            _networkManager.RemoveWorkerSession(worker.Session);
            worker.NotifyClosed();
            return true;
        }

        private void CloseOwnedOopifWorkers(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)
                || !_oopifOwnedWorkers.TryRemove(targetId, out ConcurrentDictionary<string, byte> owned))
            {
                return;
            }

            foreach (string sessionId in owned.Keys)
            {
                RemoveWorker(sessionId);
            }
        }

        private void OnDownloadWillBegin(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            if (PublicPage?.Context is ChromiumBrowserContext downloadContext
                && !downloadContext.DownloadEventsEnabled)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string guid = p.TryGetProperty("guid", out JsonElement guidEl) ? guidEl.GetString() : string.Empty;
            string url = p.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : string.Empty;
            string suggested = p.TryGetProperty("suggestedFilename", out JsonElement nameEl) ? nameEl.GetString() : string.Empty;
            NotifyDownloadWillBegin(guid, url, suggested);
        }

        private void OnDownloadProgress(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string guid = p.TryGetProperty("guid", out JsonElement guidEl) ? guidEl.GetString() : string.Empty;
            string state = p.TryGetProperty("state", out JsonElement stateEl) ? stateEl.GetString() : string.Empty;
            NotifyDownloadProgress(guid, state, error: null);
        }

        private void OnFileChooserOpened(JsonElement? parameters)
            => OnFileChooserOpened(parameters, _client);

        private void OnFileChooserOpened(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            int backendNodeId = 0;
            if (p.TryGetProperty("backendNodeId", out JsonElement idEl))
            {
                idEl.TryGetInt32(out backendNodeId);
            }

            string mode = p.TryGetProperty("mode", out JsonElement modeEl) ? modeEl.GetString() : string.Empty;
            bool multiple = string.Equals(mode, "selectMultiple", StringComparison.Ordinal);
            FileChooserOpened?.Invoke(this, new FileChooserOpenedEventArgs(backendNodeId, multiple, session));
        }

        private void OnDialogOpening(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string type = p.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : "alert";
            string message = p.TryGetProperty("message", out JsonElement msgEl) ? msgEl.GetString() : string.Empty;
            string defaultValue = p.TryGetProperty("defaultPrompt", out JsonElement defEl) ? defEl.GetString() : string.Empty;

            CRDialog dialog = new(_client, type, message, defaultValue);
            DialogOpening?.Invoke(this, dialog);
        }

        private void OnLogEntryAdded(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("entry", out JsonElement entry))
            {
                return;
            }

            string source = entry.TryGetProperty("source", out JsonElement sourceEl)
                ? sourceEl.GetString()
                : string.Empty;
            if (string.Equals(source, "worker", StringComparison.Ordinal)
                || string.Equals(source, "network", StringComparison.Ordinal))
            {
                return;
            }

            if (entry.TryGetProperty("args", out JsonElement args)
                && args.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement arg in args.EnumerateArray())
                {
                    string objectId = RemoteObject.GetObjectId(arg);
                    if (!string.IsNullOrEmpty(objectId))
                    {
                        _ = _client.SendAsync("Runtime.releaseObject", new { objectId });
                    }
                }
            }

            string level = entry.TryGetProperty("level", out JsonElement levelEl)
                ? levelEl.GetString()
                : "log";
            string text = entry.TryGetProperty("text", out JsonElement textEl)
                ? textEl.GetString()
                : string.Empty;
            string url = entry.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : string.Empty;
            int line = entry.TryGetProperty("lineNumber", out JsonElement lineEl)
                && lineEl.TryGetInt32(out int ln)
                ? ln
                : 0;
            string location = string.IsNullOrEmpty(url) && line == 0
                ? string.Empty
                : $"{url}:{line}:0";
            double timestamp = entry.TryGetProperty("timestamp", out JsonElement tsEl)
                && tsEl.TryGetDouble(out double ts)
                ? ts
                : 0;
            Console?.Invoke(
                this,
                new ConsoleMessage(level ?? "log", text, location, Array.Empty<IJSHandle>(), timestamp: timestamp));
        }

        private void OnConsoleAPICalled(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            // DevTools replays buffered logs with executionContextId = 0 after
            // Runtime.enable. Those objects are already gone.
            if (p.TryGetProperty("executionContextId", out JsonElement ctxIdEl)
                && ctxIdEl.TryGetInt32(out int replayContextId)
                && replayContextId == 0)
            {
                return;
            }

            string type = p.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : "log";
            JsonElement? argsElement = p.TryGetProperty("args", out JsonElement argsEl) ? argsEl : (JsonElement?)null;
            string text = argsElement.HasValue
                ? RemoteObject.JoinConsoleArgs(argsElement.Value)
                : string.Empty;
            string location = RemoteObject.FormatStackLocation(p);
            IReadOnlyCollection<IJSHandle> args = ConsoleArgs.Wrap(argsElement, remote => WrapConsoleRemote(remote, p));
            double timestamp = p.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetDouble(out double ts)
                ? ts
                : 0;
            Console?.Invoke(this, new ConsoleMessage(type, text, location, CompatCollections.AsList(args), timestamp: timestamp));
        }

        private IJSHandle WrapConsoleRemote(JsonElement remote, JsonElement payload)
        {
            string objectId = RemoteObject.GetObjectId(remote);
            if (objectId == null)
            {
                return null;
            }

            int contextId = payload.TryGetProperty("executionContextId", out JsonElement ctxEl)
                && ctxEl.TryGetInt32(out int cid)
                ? cid
                : 0;
            if (contextId == 0 || !_contextIdToContext.TryGetValue(contextId, out CRExecutionContext context))
            {
                context = new CRExecutionContext(_client, contextId);
            }

            if (RemoteObject.IsNode(remote))
            {
                return new ChromiumElementHandle(new CRElementHandle(this, context, objectId, "JSHandle@node"));
            }

            return new ChromiumJSHandle(new CRJSHandle(context, objectId, RemoteObject.HandlePreview(remote)), this);
        }

        private void OnExceptionThrown(JsonElement? parameters)
        {
            if (!parameters.HasValue
                || !parameters.Value.TryGetProperty("exceptionDetails", out JsonElement details))
            {
                return;
            }

            PageErrorEventArgs error = PageErrorText.FromExceptionDetails(details);
            LastExceptionLocation = RemoteObject.ParseWebErrorLocation(details);
            PageError?.Invoke(this, error);
        }

        private void OnExecutionContextCreated(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            if (!parameters.Value.TryGetProperty("context", out JsonElement contextPayload))
            {
                return;
            }

            int contextId = contextPayload.TryGetProperty("id", out JsonElement idElement)
                ? idElement.GetInt32()
                : 0;

            if (contextId == 0)
            {
                return;
            }

            // Check if this is the default (main world) context for the main frame.
            bool isDefault = false;
            string frameId = string.Empty;

            if (contextPayload.TryGetProperty("auxData", out JsonElement auxData))
            {
                if (auxData.TryGetProperty("isDefault", out JsonElement isDefaultElement))
                {
                    isDefault = isDefaultElement.GetBoolean();
                }

                if (auxData.TryGetProperty("frameId", out JsonElement frameIdElement))
                {
                    frameId = frameIdElement.GetString() ?? string.Empty;
                }
            }

            CRExecutionContext executionContext = new(_client, contextId);
            _contextIdToContext.TryAdd(contextId, executionContext);

            // Assign to the appropriate frame if this is the default context.
            if (isDefault)
            {
                // Look up the frame. If not found and this could be the initial main frame
                // ID correction (CDP may use a different frame ID than the target ID),
                // update the main frame ID. Only do this if the frame ID is truly unknown
                // — child frames' default contexts should NOT overwrite the main frame ID.
                Frame targetFrame = _frameManager.FrameById(frameId);
                if (targetFrame == null && !string.IsNullOrEmpty(frameId))
                {
                    // This is an unknown frame ID. It's the initial main frame correction
                    // only if no other frame has been registered with a different ID yet
                    // (i.e., the main frame still has the original target ID).
                    if (_frameManager.MainFrame.FrameId == _targetId ||
                        _frameManager.Frames.Count == 1)
                    {
                        _frameManager.UpdateMainFrameId(frameId);
                        targetFrame = MainFrame;
                    }
                }

                targetFrame ??= MainFrame;
                targetFrame.ExecutionContext = executionContext;
            }
        }

        private void OnExecutionContextDestroyed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            int contextId = parameters.Value.TryGetProperty("executionContextId", out JsonElement idElement)
                ? idElement.GetInt32()
                : 0;

            if (_contextIdToContext.TryRemove(contextId, out CRExecutionContext context))
            {
                foreach (Frame frame in _frameManager.Frames)
                {
                    if (frame.ExecutionContext == context)
                    {
                        frame.ExecutionContext = null;
                    }
                }
            }
        }

        private void OnExecutionContextsCleared()
        {
            _contextIdToContext.Clear();
            foreach (Frame frame in _frameManager.Frames)
            {
                frame.ExecutionContext = null;
            }
        }

        private void OnLifecycleEvent(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string name = parameters.Value.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString()
                : string.Empty;

            string frameId = parameters.Value.TryGetProperty("frameId", out JsonElement frameIdElement)
                ? frameIdElement.GetString()
                : string.Empty;

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(frameId))
            {
                _frameManager.FrameLifecycleEvent(frameId, name);
                if (string.Equals(name, "DOMContentLoaded", StringComparison.Ordinal)
                    || string.Equals(name, "load", StringComparison.Ordinal))
                {
                    _networkManager.FinishNavigationRequestsForFrame(frameId);
                }
            }
        }

        private void OnFrameNavigated(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            if (!parameters.Value.TryGetProperty("frame", out JsonElement framePayload))
            {
                return;
            }

            string frameId = framePayload.TryGetProperty("id", out JsonElement idElement)
                ? idElement.GetString()
                : string.Empty;

            string url = framePayload.TryGetProperty("url", out JsonElement urlElement)
                ? urlElement.GetString()
                : string.Empty;

            // Append URL fragment if present (upstream behavior).
            if (framePayload.TryGetProperty("urlFragment", out JsonElement fragmentElement))
            {
                string fragment = fragmentElement.GetString();
                if (!string.IsNullOrEmpty(fragment))
                {
                    url += fragment;
                }
            }

            string name = framePayload.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString()
                : string.Empty;

            string loaderId = framePayload.TryGetProperty("loaderId", out JsonElement loaderElement)
                ? loaderElement.GetString()
                : string.Empty;

            string parentId = framePayload.TryGetProperty("parentId", out JsonElement parentEl)
                ? parentEl.GetString()
                : string.Empty;

            // OOPIF navigations can land before Page.frameAttached. Create the
            // child from parentId so the frame id is not stolen by the main frame.
            if (!string.IsNullOrEmpty(frameId) && !string.IsNullOrEmpty(parentId))
            {
                _frameManager.FrameAttachedToTarget(frameId, parentId);
            }

            if (!string.IsNullOrEmpty(frameId))
            {
                Frame before = _frameManager.FrameById(frameId) ?? _frameManager.MainFrame;
                string previousDocumentId = before?.DocumentId;
                _frameManager.FrameCommittedNewDocumentNavigation(frameId, url ?? string.Empty, name ?? string.Empty, loaderId);
                Frame navigated = _frameManager.FrameById(frameId) ?? _frameManager.MainFrame;
                if (navigated != null && navigated.ParentFrame == null
                    && !string.Equals(navigated.DocumentId, previousDocumentId, StringComparison.Ordinal))
                {
                    RestoreOopifFrames();
                    EraseEvaluateCallbacks();
                    if (!string.IsNullOrEmpty(url)
                        && !url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                    {
                        HasCommittedNonInitialNavigation = true;
                    }

                    MarkFirstNonInitialNavigation(url);
                }
            }
        }

        private void OnFrameAttached(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string frameId = parameters.Value.TryGetProperty("frameId", out JsonElement fidElement)
                ? fidElement.GetString()
                : string.Empty;

            string parentFrameId = parameters.Value.TryGetProperty("parentFrameId", out JsonElement pidElement)
                ? pidElement.GetString()
                : string.Empty;

            HandleFrameAttached(frameId, parentFrameId);
        }

        private void OnFrameDetached(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string frameId = parameters.Value.TryGetProperty("frameId", out JsonElement fidElement)
                ? fidElement.GetString()
                : string.Empty;

            string reason = parameters.Value.TryGetProperty("reason", out JsonElement reasonEl)
                ? reasonEl.GetString()
                : string.Empty;

            HandleFrameDetached(frameId, reason);
        }

        private void OnFrameRequestedNavigation(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string disposition = parameters.Value.TryGetProperty("disposition", out JsonElement dispositionEl)
                ? dispositionEl.GetString()
                : string.Empty;
            if (!string.IsNullOrEmpty(disposition)
                && !string.Equals(disposition, "currentTab", StringComparison.Ordinal))
            {
                return;
            }

            string frameId = parameters.Value.TryGetProperty("frameId", out JsonElement frameIdEl)
                ? frameIdEl.GetString()
                : string.Empty;
            string url = parameters.Value.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : string.Empty;
            _frameManager.FrameRequestedNavigation(frameId, url);
        }

        private void OnNavigatedWithinDocument(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string frameId = parameters.Value.TryGetProperty("frameId", out JsonElement fidElement)
                ? fidElement.GetString()
                : string.Empty;

            string url = parameters.Value.TryGetProperty("url", out JsonElement urlElement)
                ? urlElement.GetString()
                : string.Empty;

            if (!string.IsNullOrEmpty(frameId))
            {
                _frameManager.FrameCommittedSameDocumentNavigation(frameId, url ?? string.Empty);
            }
        }

        private void OnBindingCalled(JsonElement? parameters)
            => OnBindingCalled(parameters, _client);

        private void OnBindingCalled(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string nameField = p.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            if (nameField != PageBindingScript.ChannelName)
            {
                return;
            }

            string payload = p.TryGetProperty("payload", out JsonElement payloadEl) ? payloadEl.GetString() : null;
            int executionContextId = p.TryGetProperty("executionContextId", out JsonElement ctxEl) && ctxEl.TryGetInt32(out int cid) ? cid : 0;
            if (executionContextId != 0)
            {
                _lastBindingContextId = executionContextId;
            }

            if (session != null)
            {
                _lastBindingSession = session;
            }

            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            // JSON exposeFunction callbacks (used as addEventListener handlers) must run
            // before the triggering evaluate returns, matching official Playwright. Handle
            // bindings still hop off the CDP thread so result delivery cannot deadlock.
            if (!TryDispatchJsonBinding(payload, executionContextId))
            {
                _ = Task.Run(() => DispatchBindingCallAsync(payload, executionContextId));
            }
        }

        private bool TryDispatchJsonBinding(string payload, int executionContextId)
        {
            long seq = 0;
            string functionName = null;
            JsonElement[] args;
            try
            {
                JsonElement root = JsonSerializer.Deserialize<JsonElement>(payload);
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
                    _ = Task.Run(() => DeliverBindingErrorAsync(executionContextId, seq, argsError));
                    return true;
                }
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => DeliverBindingErrorAsync(executionContextId, seq, ex));
                return true;
            }

            try
            {
                if (ClosedForBindings())
                {
                    return true;
                }

                if (!_exposedFunctions.TryGetValue(functionName ?? string.Empty, out Func<JsonElement[], Task<object>> handler))
                {
                    _ = Task.Run(() => DeliverBindingErrorAsync(executionContextId, seq, $"No handler registered for '{functionName}'"));
                    return true;
                }

                Task<object> invoked = handler(args);
                _ = Task.Run(() => DeliverInvokedBindingAsync(invoked, executionContextId, seq));
                return true;
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => DeliverBindingErrorAsync(executionContextId, seq, ex));
                return true;
            }
        }

        private async Task DeliverInvokedBindingAsync(Task<object> invoked, int executionContextId, long seq)
        {
            try
            {
                object result = await invoked.ConfigureAwait(false);
                await DeliverBindingResultAsync(executionContextId, seq, result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DeliverBindingErrorAsync(executionContextId, seq, ex).ConfigureAwait(false);
            }
        }

        private async Task DispatchBindingCallAsync(string payload, int executionContextId)
        {
            long seq = 0;
            string functionName = null;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                JsonElement root = doc.RootElement;
                functionName = root.GetProperty("name").GetString();
                seq = root.GetProperty("seq").GetInt64();

                bool isHandle = root.TryGetProperty("handle", out JsonElement handleFlag)
                    && handleFlag.ValueKind == JsonValueKind.True;
                if (isHandle)
                {
                    if (ClosedForBindings())
                    {
                        return;
                    }

                    if (!_handleBindings.TryGetValue(functionName ?? string.Empty, out Func<CRJSHandle, Task<object>> handleHandler))
                    {
                        await DeliverBindingErrorAsync(executionContextId, seq, $"No handler registered for '{functionName}'").ConfigureAwait(false);
                        return;
                    }

                    CRExecutionContext context = new CRExecutionContext(_client, executionContextId);
                    JsonElement? handleValue = await context.EvaluateHandleAsync(PageBindingScript.TakeHandleExpression(seq)).ConfigureAwait(false);
                    CRJSHandle jsHandle = WrapJSHandle(context, handleValue);
                    object handleResult = await handleHandler(jsHandle).ConfigureAwait(false);
                    await DeliverBindingResultAsync(executionContextId, seq, handleResult).ConfigureAwait(false);
                    return;
                }

                if (!PageBindingScript.TryReadSerializedArgs(root, clone: true, out JsonElement[] args, out string argsError))
                {
                    await DeliverBindingErrorAsync(executionContextId, seq, argsError).ConfigureAwait(false);
                    return;
                }

                if (ClosedForBindings())
                {
                    return;
                }

                if (!_exposedFunctions.TryGetValue(functionName ?? string.Empty, out Func<JsonElement[], Task<object>> handler))
                {
                    await DeliverBindingErrorAsync(executionContextId, seq, $"No handler registered for '{functionName}'").ConfigureAwait(false);
                    return;
                }

                object result = await handler(args).ConfigureAwait(false);
                await DeliverBindingResultAsync(executionContextId, seq, result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await DeliverBindingErrorAsync(executionContextId, seq, ex).ConfigureAwait(false);
            }
        }

        private async Task DeliverBindingResultAsync(int executionContextId, long seq, object result)
        {
            PageBindingResult.TryExtractHandles(result, out object tree, out List<object> extracted);
            tree = PageBindingResult.InlineImmediateHandles(tree, extracted, out List<object> handles);
            if (handles.Count > 0)
            {
                object[] callArgs = new object[2 + handles.Count];
                callArgs[0] = seq;
                callArgs[1] = JsonSerializer.Serialize(tree);
                for (int i = 0; i < handles.Count; i++)
                {
                    callArgs[2 + i] = handles[i];
                }

                CRExecutionContext context = new CRExecutionContext(_client, executionContextId);
                await context.EvaluateFunctionAsync<object>(PageBindingScript.DeliverWithHandlesFunction, callArgs).ConfigureAwait(false);
                return;
            }

            await DeliverBindingAsync(executionContextId, new { seq, result = tree }).ConfigureAwait(false);
        }

        private Task DeliverBindingErrorAsync(int executionContextId, long seq, Exception error)
            => DeliverBindingAsync(executionContextId, new { seq, error = PageBindingResult.FormatError(error) });

        private Task DeliverBindingErrorAsync(int executionContextId, long seq, string error)
            => DeliverBindingAsync(executionContextId, new { seq, error });

        private Task EnsureBindingInfrastructureAsync()
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

        private async Task InstallBindingInfrastructureAsync()
        {
            _bindingInfrastructureInstalled = true;
            await _client.SendAsync("Runtime.addBinding", new { name = PageBindingScript.ChannelName }).ConfigureAwait(false);
            foreach (CRSession session in _oopifSessions.Values)
            {
                await SendIgnoreClosedAsync(session, "Runtime.addBinding", new { name = PageBindingScript.ChannelName }).ConfigureAwait(false);
            }

            await AddInitScriptAsync(PageBindingScript.InitScript).ConfigureAwait(false);
            await EvaluateInAllFramesAsync(PageBindingScript.InitScript).ConfigureAwait(false);
        }

        private async Task EvaluateInAllFramesAsync(string expression)
        {
            if (!_debuggerResumed && Opener != null)
            {
                return;
            }

            foreach (Frame frame in _frameManager.Frames)
            {
                try
                {
                    CRExecutionContext context = frame.ExecutionContext;
                    if (context == null && frame.ParentFrame == null)
                    {
                        context = await WaitForFrameExecutionContextAsync(frame, 1_000).ConfigureAwait(false);
                    }

                    if (context == null)
                    {
                        continue;
                    }

                    await context.EvaluateAsync<object>(expression).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        private async Task DeliverBindingAsync(int executionContextId, object envelope)
        {
            string json = JsonSerializer.Serialize(envelope);
            CRSession session = _lastBindingSession ?? _client;
            try
            {
                await session.SendAsync("Runtime.evaluate", new
                {
                    expression = $"globalThis.__pw_binding_deliver__({json})",
                    contextId = executionContextId,
                    returnByValue = true,
                    awaitPromise = false,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Best-effort delivery — the execution context may have been destroyed
                // by a navigation between the call and the response.
            }
        }

        private CRJSHandle WrapJSHandle(CRExecutionContext context, JsonElement? handleValue)
        {
            if (handleValue == null)
            {
                return null;
            }

            JsonElement remoteObject = handleValue.Value;
            if (remoteObject.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.GetString() == "null")
            {
                return null;
            }

            string preview = RemoteObject.HandlePreview(remoteObject);
            string objectId = RemoteObject.GetObjectId(remoteObject);
            if (string.IsNullOrEmpty(objectId))
            {
                return new CRJSHandle(context, null, preview, remoteObject);
            }

            if (remoteObject.TryGetProperty("subtype", out JsonElement nodeSubtype)
                && nodeSubtype.GetString() == "node")
            {
                return new CRElementHandle(this, context, objectId, "JSHandle@node");
            }

            return new CRJSHandle(context, objectId, preview);
        }

        private void MarkFirstNonInitialNavigation(string url)
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

            HasCommittedNonInitialNavigation = true;
            _firstNonInitialNavigationTcs.TrySetResult(true);
            if (!PopupOpenedHelper.IsBlankUrl(url))
            {
                _firstNonBlankNavigationTcs.TrySetResult(true);
            }

            if (Opener != null && !PopupReported && !PopupOpenedHelper.IsBlankUrl(url))
            {
                if (_frameManager.MainFrame != null)
                {
                    _frameManager.FrameLifecycleEvent(_frameManager.MainFrame.FrameId, "DOMContentLoaded");
                    _frameManager.FrameLifecycleEvent(_frameManager.MainFrame.FrameId, "load");
                }

                PopupReported = true;
                Opener.FirePopupOpened(this);
            }
        }

        private bool ClosedForBindings()
            => _closedTcs.Task.IsCompleted || (PublicPage != null && PublicPage.IsClosed);

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

        private void OnWindowOpen(JsonElement? parameters)
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

        private async Task WaitForFirstNonInitialNavigationAsync()
        {
            await SyncMainFrameFromTreeAsync().ConfigureAwait(false);
            if (_firstNonInitialNavigationTcs.Task.IsCompleted)
            {
                return;
            }

            await Task.WhenAny(_firstNonInitialNavigationTcs.Task, Task.Delay(500)).ConfigureAwait(false);
            if (_firstNonInitialNavigationTcs.Task.IsCompleted)
            {
                return;
            }

            if (_frameManager.MainFrame != null
                && PopupOpenedHelper.IsInitialEmptyDocumentUrl(_frameManager.MainFrame.Url))
            {
                _frameManager.MainFrame.Url = "about:blank";
                _frameManager.FrameLifecycleEvent(_frameManager.MainFrame.FrameId, "DOMContentLoaded");
                _frameManager.FrameLifecycleEvent(_frameManager.MainFrame.FrameId, "load");
            }

            _firstNonInitialNavigationTcs.TrySetResult(true);
        }

        private async Task SyncMainFrameFromTreeAsync()
        {
            JsonElement? response;
            try
            {
                response = await _client.SendAsync("Page.getFrameTree").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return;
            }

            if (!response.HasValue
                || !response.Value.TryGetProperty("frameTree", out JsonElement tree)
                || !tree.TryGetProperty("frame", out JsonElement frame))
            {
                return;
            }

            string url = frame.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : string.Empty;

            if (_frameManager.MainFrame != null && !PopupOpenedHelper.IsInitialEmptyDocumentUrl(url))
            {
                _frameManager.MainFrame.Url = url;
            }

            MarkFirstNonInitialNavigation(url);
        }
    }
}
