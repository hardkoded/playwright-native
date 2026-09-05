/*
 * Copyright (c) 2020 Dario Kondratiuk
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
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Helpers;
#pragma warning disable SA1201

namespace PlaywrightNative
{
    /// <summary>
    /// Shared page that implements <see cref="IPage"/> and owns a browser-specific
    /// <see cref="IPageDelegate"/> (today <see cref="CRPage"/>), matching Node Playwright.
    /// </summary>
    internal sealed partial class Page : IPage, IHasPageExtras, IHasDefaultTimeouts, IHasLastPageErrorLocation, IHasClientInitializedPage, IHasExposedFunctionNames, ISupportsVirtualAuthenticator, IAppliesMergedExtraHttpHeaders
    {
        private readonly CRPage _crPage;
        private readonly IBrowserContext _context;
        private readonly ConcurrentDictionary<CRRequest, ChromiumRequest> _directRequests = new();
        private readonly ConcurrentDictionary<CRResponse, ChromiumResponse> _directResponses = new();
        private readonly ConcurrentDictionary<Frame, ChromiumFrame> _directFrames = new();
        private readonly ConcurrentDictionary<string, PageDownload> _downloads = new();
        private readonly ConcurrentDictionary<CRWorker, ChromiumWorker> _directWorkers = new();
        private readonly PageConsoleLog _consoleLog = new();
        private readonly PageEventLog<string> _pageErrors = new();
        private readonly PageEventLog<IRequest> _requests = new(NetworkRequestEvents.RecentRequestLimit);
        private readonly PageDialogTracker _dialogTracker = new();
        private readonly PageListenerRegistry _pageListeners = new();
        private IMouse _directMouse;
        private IKeyboard _directKeyboard;
        private ITouchscreen _directTouchscreen;
        private bool _isClosed;
        private bool _virtualAuthenticatorEnabled;
        private string _virtualAuthenticatorId;
        private string _closeReason;
        private string _extraReferer;
        private Dictionary<string, string> _pageExtraHttpHeaders;
        private float _defaultTimeout = 30_000;
        private float _defaultNavigationTimeout = 30_000;
        private PageViewportSizeResult _viewportSize;
        private float _emulatedDeviceScaleFactor = 1;
        private bool _emulatedIsMobile;
        private ScreenSize _independentScreen;

        internal Page(CRPage crPage, IBrowserContext context)
        {
            _crPage = crPage ?? throw new ArgumentNullException(nameof(crPage));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _crPage.PublicPage = this;
            Coverage = new CRCoverage(_crPage.Session);
            Screencast = new CRScreencast(this);
            LocalStorage = new WebStorage(this, "localStorage");
            SessionStorage = new WebStorage(this, "sessionStorage");

            // Translate CR-level signals into the public IPage events. Subscriptions
            // live for the lifetime of this page; cleanup on dispose is a future
            // cleanup (no IDisposable on IPage today).
            _crPage.Closed += (_, _) =>
            {
                _isClosed = true;
                Close?.Invoke(this, this);
            };
            _crPage.MainFrame.LifecycleChanged += name =>
            {
                if (name == "load")
                {
                    Load?.Invoke(this, this);
                }
                else if (name == "DOMContentLoaded")
                {
                    DOMContentLoaded?.Invoke(this, this);
                }

                // Note: "networkidle" is intentionally not forwarded — it's a Playwright
                // synthetic lifecycle used for WaitForLoadStateAsync and has no IPage event.
            };

            // Dialog: wrap each CRDialog in a ChromiumDialog and fire the public event.
            _crPage.DialogOpening += (_, crDialog) => OnDialogOpening(crDialog);
            _crPage.DialogClosedInBrowser += (_, _) => _dialogTracker.OnBrowserClosedDialog(EmitDialogClosed);

            // Popup: reuse the context's instance cache so OpenerAsync identity matches.
            _crPage.PopupOpened += (_, popupCrPage) =>
            {
                IPage popup = _context is ChromiumBrowserContext ctx
                    ? ctx.GetOrCreatePage(popupCrPage)
                    : new Page(popupCrPage, _context);
                PopupOpenedHelper.EmitWhenReady(
                    popup,
                    popupCrPage.PrepareForPopupReportAsync(),
                    ready => Popup?.Invoke(this, ready),
                    popupCrPage.PopupEmitDelayMs);
            };

            _crPage.Console += (_, message) =>
            {
                if (message is ConsoleMessage consoleMessage)
                {
                    consoleMessage.Page = this;
                }

                _consoleLog.Add(message);
                _pageListeners.Console.Emit(this, message);
            };
            _crPage.PageError += (_, error) =>
            {
                LastPageErrorLocation = _crPage.LastExceptionLocation ?? new WebErrorLocation();
                string message = error?.ToString() ?? string.Empty;
                _pageErrors.Add(message);
                PageError?.Invoke(this, message);
            };

            _crPage.FrameManager.FrameAttached += frame => FrameAttached?.Invoke(this, GetOrCreateFrame(frame));
            _crPage.FrameManager.FrameDetached += frame => FrameDetached?.Invoke(this, GetOrCreateFrame(frame));
            _crPage.FrameManager.FrameNavigated += (frame, _) => FrameNavigated?.Invoke(this, GetOrCreateFrame(frame));
            _crPage.FrameManager.FrameNavigatedToNewDocument += frame =>
            {
                if (frame == _crPage.FrameManager.MainFrame)
                {
                    _consoleLog.MarkNavigation();
                    _pageErrors.MarkNavigation();
                    _ = _crPage.Session.SendAsync("Page.setInterceptFileChooserDialog", new { enabled = true });
                }
            };

            // Network: translate CR network signals into IRequest/IResponse events. The cache
            // ensures the same ChromiumRequest / ChromiumResponse instance is passed to all four
            // events for a given CRRequest / CRResponse, so consumers can do reference equality.
            _crPage.RequestCreated += (_, crRequest) =>
            {
                IRequest request = GetOrCreateDirectRequest(crRequest);
                _requests.Add(request);
                Request?.Invoke(this, request);
            };
            _crPage.ResponseReceived += (_, crResponse) => Response?.Invoke(this, GetOrCreateDirectResponse(crResponse));
            _crPage.RequestFinished += (_, crRequest) => RequestFinished?.Invoke(this, GetOrCreateDirectRequest(crRequest));
            _crPage.RequestFailed += (_, crRequest) => RequestFailed?.Invoke(this, GetOrCreateDirectRequest(crRequest));
            _crPage.DownloadWillBegin += OnCrDownloadWillBegin;
            _crPage.DownloadProgress += OnCrDownloadProgress;
            _crPage.FileChooserOpened += OnCrFileChooserOpened;
            _crPage.WorkerCreated += (_, worker) =>
            {
                // Always wrap the worker so its Runtime.consoleAPICalled events
                // reach page.Console even when nobody subscribed to page.Worker.
                // `Worker?.Invoke(..., GetOrCreateWorker(worker))` would skip the
                // argument when Worker is null.
                ChromiumWorker instance = GetOrCreateWorker(worker);
                Worker?.Invoke(this, instance);
            };
            _crPage.WebSocketCreated += (_, socket) => WebSocket?.Invoke(this, socket);
            _crPage.Crashed += (_, _) => Crash?.Invoke(this, this);
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

        /// <inheritdoc/>
        public IReadOnlyList<IWorker> Workers
        {
            get
            {
                List<IWorker> workers = new List<IWorker>();
                foreach (CRWorker worker in _crPage.Workers)
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
        public ICoverage Coverage { get; }

        /// <inheritdoc/>
        public IClock Clock => _context.Clock;

        /// <inheritdoc/>
        public IWebStorage LocalStorage { get; }

        /// <inheritdoc/>
        public IWebStorage SessionStorage { get; }

        /// <inheritdoc/>
        public IBrowserContext Context => _context;

        /// <inheritdoc/>
        public IAPIRequestContext APIRequest => Context.APIRequest;

        /// <inheritdoc/>
        public IReadOnlyList<IFrame> Frames => FrameLookup.DepthFirst(MainFrame);

        /// <inheritdoc/>
        public bool IsClosed => _isClosed;

        /// <inheritdoc/>
        public IKeyboard Keyboard
        {
            get => _directKeyboard ??= new ChromiumKeyboard(_crPage.Keyboard, _context);
            set => _directKeyboard = value;
        }

        /// <inheritdoc/>
        public IFrame MainFrame => GetOrCreateFrame(_crPage.MainFrame);

        /// <inheritdoc/>
        public IMouse Mouse
        {
            get => _directMouse ??= new ChromiumMouse(_crPage.Mouse, _context);
            set => _directMouse = value;
        }

        /// <inheritdoc/>
        public ITouchscreen Touchscreen
        {
            get => _directTouchscreen ??= new ChromiumTouchscreen(_crPage.Touchscreen);
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
        public string Url => _crPage.MainFrame.Url;

        /// <inheritdoc/>
        public PageViewportSizeResult ViewportSize => _viewportSize;

        /// <inheritdoc/>
        public WebErrorLocation LastPageErrorLocation { get; private set; }

        /// <inheritdoc/>
        public bool IsClientInitialized =>
            (_crPage.Opener == null && _crPage.InitializedTask.IsCompleted)
            || _crPage.HasCommittedNonInitialNavigation;

        /// <summary>
        /// Browser-specific page delegate. Matches Node <c>page.delegate</c>.
        /// </summary>
        internal IPageDelegate Delegate => _crPage;

        /// <summary>
        /// Gets the underlying Chromium page.
        /// </summary>
        internal CRPage CrPage => _crPage;

        /// <summary>
        /// Official <c>_ownedContext</c>: a context created by <c>browser.newPage</c>
        /// is closed when this page closes.
        /// </summary>
        internal IBrowserContext OwnedContext { get; set; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Popup"/> has subscribers
        /// (including <see cref="IPage.WaitForPopupAsync"/>).
        /// </summary>
        internal bool HasPopupListeners => Popup != null;

        /// <inheritdoc/>
        bool IHasExposedFunctionNames.HasExposedFunction(string name) => HasExposedFunction(name);

        /// <inheritdoc/>
        public Task<AccessibilitySnapshotResult> SnapshotAccessibilityAsync(bool? interestingOnly = null, IElementHandle root = null)
            => CRAccessibility.SnapshotAsync(_crPage.Session, interestingOnly, root);

        /// <inheritdoc/>
        public async Task EnableVirtualAuthenticatorAsync()
        {
            if (_virtualAuthenticatorEnabled)
            {
                return;
            }

            await _crPage.Session.SendAsync("WebAuthn.enable").ConfigureAwait(false);
            JsonElement? added = await _crPage.Session.SendAsync(
                "WebAuthn.addVirtualAuthenticator",
                new
                {
                    options = new
                    {
                        protocol = "ctap2",
                        transport = "internal",
                        hasResidentKey = true,
                        hasUserVerification = true,
                        isUserVerified = true,
                        automaticPresenceSimulation = true,
                    },
                }).ConfigureAwait(false);
            if (added.HasValue && added.Value.TryGetProperty("authenticatorId", out JsonElement idElement))
            {
                _virtualAuthenticatorId = idElement.GetString();
            }

            _virtualAuthenticatorEnabled = true;
        }

        /// <inheritdoc/>
        public async Task AddVirtualCredentialAsync(VirtualCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            await EnableVirtualAuthenticatorAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(_virtualAuthenticatorId))
            {
                return;
            }

            await _crPage.Session.SendAsync(
                "WebAuthn.addCredential",
                new
                {
                    authenticatorId = _virtualAuthenticatorId,
                    credential = new
                    {
                        credentialId = VirtualCredentialFactory.ToCdpBase64(credential.Id),
                        isResidentCredential = true,
                        rpId = credential.RpId,
                        privateKey = VirtualCredentialFactory.ToCdpBase64(credential.PrivateKey),
                        userHandle = VirtualCredentialFactory.ToCdpBase64(credential.UserHandle),
                        signCount = 0,
                    },
                }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task RemoveVirtualCredentialAsync(string id)
        {
            if (!_virtualAuthenticatorEnabled || string.IsNullOrEmpty(_virtualAuthenticatorId) || string.IsNullOrEmpty(id))
            {
                return;
            }

            await _crPage.Session.SendAsync(
                "WebAuthn.removeCredential",
                new
                {
                    authenticatorId = _virtualAuthenticatorId,
                    credentialId = VirtualCredentialFactory.ToCdpBase64(id),
                }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IAsyncDisposable> AddInitScriptAsync(string script = null, string scriptPath = null)
        {
            script = AddInitScriptHelper.Resolve(script, scriptPath);
            string identifier = await _crPage.AddInitScriptAsync(script).ConfigureAwait(false);
            return AddInitScriptHelper.CreateDisposable(() => _crPage.RemoveInitScriptAsync(identifier));
        }

        /// <inheritdoc/>
        public Task AddInitScriptAsync(string script, object arg)
        {
            script = AddInitScriptHelper.Resolve(script, null, arg);
            return AddInitScriptAsync(script, null);
        }

        /// <inheritdoc/>
        public async Task<IElementHandle> AddScriptTagAsync(string url = default, string path = default, string content = default, string type = default)
        {
            CRElementHandle handle = await _crPage.AddScriptTagAsync(url: url, content: content, type: type, path: path).ConfigureAwait(false);
            return handle == null ? null : new ChromiumElementHandle(handle);
        }

        /// <inheritdoc/>
        public async Task<IElementHandle> AddStyleTagAsync(string url = default, string path = default, string content = default)
        {
            CRElementHandle handle = await _crPage.AddStyleTagAsync(url: url, content: content, path: path).ConfigureAwait(false);
            return handle == null ? null : new ChromiumElementHandle(handle);
        }

        /// <inheritdoc/>
        public Task CheckAsync(string selector, Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.CheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "page.check", scroll);

        /// <inheritdoc/>
        public Task ClickAsync(string selector, MouseButton button = default, int? clickCount = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default, bool? strict = default)
            => ActionTrace.RunAsync(
                _context,
                clickCount == 2 ? "Double click " + ActionTrace.LocatorLabel(selector) : ActionTrace.ClickTitle(selector),
                "Page",
                clickCount == 2 ? "dblclick" : "click",
                () => PlaywrightApiLog.RunAsync(
                    (_context as IHasPlaywrightLogger)?.Logger,
                    "page.click",
                    () => ClickAction.RunOnSelectorAsync(sel => QueryActionAsync(sel, strict), selector, h => h.ClickAsync(button, clickCount, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll, steps), timeout, "page.click", scroll)),
                new Dictionary<string, object> { ["selector"] = selector });

        /// <inheritdoc/>
        public async Task CloseAsync(bool? runBeforeUnload = default, string reason = default)
        {
            if (_isClosed)
            {
                return;
            }

            await ActionTrace.RunAsync(_context, "Close page", "Page", "close", async () =>
            {
                if (OwnedContext != null)
                {
                    IBrowserContext owned = OwnedContext;
                    OwnedContext = null;
                    await owned.CloseAsync(reason).ConfigureAwait(false);
                    _isClosed = true;
                    return;
                }

                _closeReason = reason;
                _crPage.Session.CloseReason = reason;
                bool runUnload = runBeforeUnload ?? false;
                try
                {
                    await _crPage.ClosePageAsync(runUnload).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex) when (
                    ClosedTarget.IsClosed(ex)
                    || ex.Message.Contains("No target with given id", StringComparison.OrdinalIgnoreCase))
                {
                }

                // Official close({ runBeforeUnload: true }) only requests the dialog;
                // the page stays open until the dialog is accepted or a later hard close.
                if (!runUnload)
                {
                    _isClosed = true;
                    try
                    {
                        await _crPage.ClosedTask.ConfigureAwait(false);
                    }
#pragma warning disable RCS1075
                    catch (Exception)
#pragma warning restore RCS1075
                    {
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => PageDispose.RunAsync(this);

        /// <inheritdoc/>
        public Task<string> ContentAsync()
            => _crPage.ContentAsync();

        /// <inheritdoc/>
        public Task DblClickAsync(string selector, MouseButton button = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ActionTrace.RunAsync(
                _context,
                "Double click " + ActionTrace.LocatorLabel(selector),
                "Page",
                "dblclick",
                () => ClickAction.RunOnSelectorAsync(sel => QueryActionAsync(sel, strict), selector, h => h.DblClickAsync(button, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "page.dblclick", scroll));

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public Task EmulateMediaAsync(ColorScheme? colorScheme)
            => EmulateMediaAsync(media: default, colorScheme: colorScheme);

        /// <inheritdoc/>
        public Task EmulateMediaAsync(Media? media = default, ColorScheme? colorScheme = default)
        {
            // Official emulateMedia({}) leaves current overrides in place. C# null
            // means omitted; Media.Null / ColorScheme.Null reset.
            string mediaValue = null;
            if (media.HasValue)
            {
                mediaValue = media.Value switch
                {
                    Media.Screen => "screen",
                    Media.Print => "print",
                    _ => string.Empty,
                };
            }

            string scheme = null;
            bool updateColorScheme = colorScheme.HasValue;
            if (updateColorScheme)
            {
                scheme = colorScheme.Value switch
                {
                    ColorScheme.Light => "light",
                    ColorScheme.Dark => "dark",
                    ColorScheme.NoPreference => "no-preference",
                    _ => string.Empty,
                };
            }

            return _crPage.SetEmulatedMediaAsync(mediaValue, scheme, updateColorScheme);
        }

        /// <inheritdoc/>
        public Task EmulateMediaAsync(ReducedMotion? reducedMotion = default, ForcedColors? forcedColors = default, Contrast? contrast = default)
        {
            string motion = reducedMotion switch
            {
                ReducedMotion.Reduce => "reduce",
                ReducedMotion.NoPreference => "no-preference",
                _ => null,
            };

            string colors = forcedColors switch
            {
                ForcedColors.Active => "active",
                ForcedColors.None => "none",
                _ => null,
            };

            string contrastValue = contrast switch
            {
                Contrast.More => "more",
                EnumCompat.LessContrast => "less",
                Contrast.NoPreference => "no-preference",
                _ => null,
            };

            return _crPage.SetEmulatedMediaFeaturesAsync(
                motion,
                updateReducedMotion: reducedMotion.HasValue,
                colors,
                updateForcedColors: forcedColors.HasValue,
                contrastValue,
                updateContrast: contrast.HasValue);
        }

        /// <inheritdoc/>
        public Task EmulateVisionDeficiencyAsync(VisionDeficiency type = default)
        {
            string token = type switch
            {
                VisionDeficiency.Achromatopsia => "achromatopsia",
                VisionDeficiency.BlurredVision => "blurredVision",
                VisionDeficiency.Deuteranopia => "deuteranopia",
                VisionDeficiency.Protanopia => "protanopia",
                VisionDeficiency.Tritanopia => "tritanopia",
                _ => "none",
            };
            return _crPage.SetEmulatedVisionDeficiencyAsync(token);
        }

        /// <inheritdoc/>
        public Task<JsonElement?> EvaluateAsync(string expression, object arg = default)
        {
            ThrowIfClosed();
            return ActionTrace.EvaluateUserAsync(_context, () =>
            {
                if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
                {
                    return EvaluatePreparedAsync<JsonElement?>(handleFn, handleArgs);
                }

                string toEval = arg == null
                    ? EvaluateWithArg.InvokeIfFunction(expression)
                    : EvaluateWithArg.Wrap(expression, arg);
                return EvaluateSerializedAsync<JsonElement?>(toEval);
            });
        }

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = default)
        {
            ThrowIfClosed();
            return ActionTrace.EvaluateUserAsync(_context, () =>
            {
                if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
                {
                    return EvaluatePreparedAsync<T>(handleFn, handleArgs);
                }

                string toEval = arg == null
                    ? EvaluateWithArg.InvokeIfFunction(expression)
                    : EvaluateWithArg.Wrap(expression, arg);
                return EvaluateSerializedAsync<T>(toEval);
            });
        }

        /// <inheritdoc/>
        public Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            return ActionTrace.EvaluateHandleUserAsync(_context, async () =>
            {
                if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
                {
                    object[] args = EvaluateHandleArg.AsCallFunctionArguments(handleArgs);
                    CRJSHandle bound = await _crPage
                        .EvaluateFunctionHandleInternalAsync(handleFn, args)
                        .ConfigureAwait(false);
                    return WrapJSHandle(bound);
                }

                string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
                CRJSHandle handle = await _crPage.EvaluateHandleInternalAsync(toEval).ConfigureAwait(false);
                return WrapJSHandle(handle);
            });
        }

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

            return _crPage.ExposeHandleBindingAsync(name, handle =>
            {
                object result = callback(PageExposeBinder.Source(Context, this), WrapJSHandle(handle));
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

            return InstallHandleBindingAsync(name, handle =>
            {
                TResult result = callback(PageExposeBinder.Source(Context, this), WrapJSHandle(handle));
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
                return InstallHandleBindingAsync(name, handle =>
                {
                    TResult result = callback(PageExposeBinder.Source(Context, this), (T)(object)WrapJSHandle(handle));
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
        public Task FillAsync(string selector, string value, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.FillAsync(value, noWaitAfter, timeout, force, scroll), timeout, "page.fill", scroll);

        /// <inheritdoc/>
        public Task FocusAsync(string selector, float? timeout = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.FocusAsync(timeout, scroll), timeout, "page.focus", scroll);

        /// <inheritdoc/>
        public Task<string> GetAttributeAsync(string selector, string name, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.getAttribute(" + JsonSerializer.Serialize(name) + ")",
                timeout,
                "page.getAttribute",
                strict ?? (_context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public IFrame FrameByUrl(string urlString, Regex urlRegex, Func<string, bool> urlFunc)
            => FrameLookup.ByUrl(Frames, urlString, urlRegex, urlFunc);

        /// <inheritdoc/>
        public Task<IResponse> GoToAsync(string url, string waitUntil, float? timeout = default, string referer = default)
            => GoToAsync(url, WaitUntilName.Parse(waitUntil), timeout, referer);

        /// <inheritdoc/>
        public async Task<IResponse> GoToAsync(string url, WaitUntilState waitUntil = default, float? timeout = default, string referer = default)
        {
            url = NavigationTimeout.CompleteUserUrl(NavigationUrl.Resolve(Context, url));
            IResponse result = null;
            await ActionTrace.RunAsync(_context, ActionTrace.NavigateTitle(url), "Page", "goto", async () =>
            {
                int timeoutMs = NavigationTimeout.ResolveMs(
                    timeout,
                    _defaultNavigationTimeout,
                    _defaultTimeout,
                    Context.DefaultNavigationTimeout(),
                    Context.DefaultTimeout());
                CRResponse captured = await _crPage
                    .GoToFrameCapturingResponseAsync(_crPage.MainFrame, url, waitUntil, timeoutMs, referer)
                    .ConfigureAwait(false);
                result = captured == null ? null : GetOrCreateDirectResponse(captured);
            }).ConfigureAwait(false);
            return result;
        }

        /// <inheritdoc/>
        public async Task<IResponse> ReloadAsync(WaitUntilState waitUntil = default, float? timeout = default)
        {
            IResponse result = null;
            await ActionTrace.RunAsync(_context, null, "Page", "reload", async () =>
            {
                int timeoutMs = timeout.HasValue ? (int)timeout.Value : (int)_defaultNavigationTimeout;
                CRResponse captured = await _crPage.ReloadAsync(waitUntil, timeoutMs).ConfigureAwait(false);
                result = captured == null ? null : GetOrCreateDirectResponse(captured);
            }).ConfigureAwait(false);
            return result;
        }

        /// <inheritdoc/>
        public async Task<IResponse> GoBackAsync(WaitUntilState waitUntil = default, float? timeout = default)
        {
            int timeoutMs = timeout.HasValue ? (int)timeout.Value : (int)_defaultNavigationTimeout;
            CRResponse captured = await _crPage.GoHistoryAsync(-1, waitUntil, timeoutMs).ConfigureAwait(false);
            return captured == null ? null : GetOrCreateDirectResponse(captured);
        }

        /// <inheritdoc/>
        public async Task<IResponse> GoForwardAsync(WaitUntilState waitUntil = default, float? timeout = default)
        {
            int timeoutMs = timeout.HasValue ? (int)timeout.Value : (int)_defaultNavigationTimeout;
            CRResponse captured = await _crPage.GoHistoryAsync(1, waitUntil, timeoutMs).ConfigureAwait(false);
            return captured == null ? null : GetOrCreateDirectResponse(captured);
        }

        /// <inheritdoc/>
        public Task BringToFrontAsync()
            => _crPage.Session.SendAsync("Page.bringToFront");

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
            => _crPage.Session.SendAsync("HeapProfiler.collectGarbage");

        /// <inheritdoc/>
        public Task HoverAsync(string selector, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ActionTrace.RunAsync(
                _context,
                ActionTrace.HoverTitle(selector),
                "Page",
                "hover",
                () => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.HoverAsync(position, modifiers, force, timeout, trial, scroll), timeout, "page.hover", scroll));

        /// <inheritdoc/>
        public Task<string> InnerHTMLAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.innerHTML",
                timeout,
                "page.innerHTML",
                strict ?? (_context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<string> InnerTextAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                ElementStateScript.InnerTextValueExpression,
                timeout,
                "page.innerText",
                strict ?? (_context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<bool> IsCheckedAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsCheckedAsync(), timeout, "page.isChecked");

        /// <inheritdoc/>
        public Task<bool> IsDisabledAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsDisabledAsync(), timeout, "page.isDisabled");

        /// <inheritdoc/>
        public Task<bool> IsEditableAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsEditableAsync(), timeout, "page.isEditable");

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsEnabledAsync(), timeout, "page.isEnabled");

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync(string selector, float? timeout = default, bool? strict = default)
            => !await IsVisibleAsync(selector, timeout, strict).ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<bool> IsVisibleAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.IsVisibleAsync(expression => EvaluateAsync<JsonElement?>(expression), selector, strict ?? (_context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<IPage> OpenerAsync()
        {
            if (_crPage.Opener == null)
            {
                return Task.FromResult<IPage>(null);
            }

            IPage opener;
            if (_context is ChromiumBrowserContext ctx)
            {
                opener = ctx.GetOrCreatePage(_crPage.Opener);
            }
            else
            {
                opener = new Page(_crPage.Opener, _context);
            }

            return Task.FromResult(opener != null && opener.IsClosed ? null : opener);
        }

        /// <inheritdoc/>
        public async Task<byte[]> PdfAsync(string path = default, float? scale = default, bool? displayHeaderFooter = default, string headerTemplate = default, string footerTemplate = default, bool? printBackground = default, bool? landscape = default, string pageRanges = default, string format = default, string width = default, string height = default, Margin margin = default, bool? preferCSSPageSize = default, bool? tagged = default, bool? outline = default)
        {
            byte[] bytes = await _crPage.PdfAsync(
                landscape: landscape ?? false,
                printBackground: printBackground ?? false,
                scale: scale,
                width: width,
                height: height,
                format: format,
                margin: margin,
                pageRanges: pageRanges,
                displayHeaderFooter: displayHeaderFooter,
                headerTemplate: headerTemplate,
                footerTemplate: footerTemplate,
                preferCSSPageSize: preferCSSPageSize,
                tagged: tagged,
                outline: outline).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(path))
            {
                PathIo.WriteBytes(path, bytes);
            }

            return bytes;
        }

        /// <inheritdoc/>
        public Task PressAsync(string selector, string key, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.PressAsync(key, delay, noWaitAfter, timeout, force, scroll), timeout, "page.press", scroll);

        /// <inheritdoc/>
        public async Task<IElementHandle> QuerySelectorAsync(string selector)
        {
            CRElementHandle crHandle = await _crPage.QuerySelectorAsync(selector).ConfigureAwait(false);
            if (crHandle == null)
            {
                return null;
            }

            return new ChromiumElementHandle(crHandle);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
        {
            IReadOnlyList<CRElementHandle> handles = await _crPage.QuerySelectorAllAsync(selector).ConfigureAwait(false);
            List<IElementHandle> result = new(handles.Count);
            foreach (CRElementHandle handle in handles)
            {
                result.Add(new ChromiumElementHandle(handle));
            }

            return result;
        }

        /// <inheritdoc/>
        public Task<IElementHandle> GetByRoleAsync(string role, string name = null, bool? exact = null, float? timeout = null, bool? checkedState = null, bool? disabled = null, bool? expanded = null, bool? includeHidden = null, int? level = null, bool? pressed = null, bool? selected = null, string description = null, Regex descriptionRegex = null, Regex nameRegex = null)
            => GetByWaiter.WaitAsync(
                () => QueryByScriptAsync(GetBySelectorScript.FindByRole, role, name, exact ?? false, GetBySelectorScript.RoleOptions(checkedState, disabled, expanded, includeHidden, level, pressed, selected, description, descriptionRegex, nameRegex)),
                timeout,
                "page.getByRole");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByTextAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(
                () => QueryByScriptAsync(GetBySelectorScript.FindByText, text, exact ?? false),
                timeout,
                "page.getByText");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByLabelAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(
                () => QueryByScriptAsync(GetBySelectorScript.FindByLabel, text, exact ?? false),
                timeout,
                "page.getByLabel");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByPlaceholderAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(
                () => QueryByScriptAsync(GetBySelectorScript.FindByPlaceholder, text, exact ?? false),
                timeout,
                "page.getByPlaceholder");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByAltTextAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(
                () => QueryByScriptAsync(GetBySelectorScript.FindByAltText, text, exact ?? false),
                timeout,
                "page.getByAltText");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByTitleAsync(string text, bool? exact = null, float? timeout = null)
            => GetByWaiter.WaitAsync(
                () => QueryByScriptAsync(GetBySelectorScript.FindByTitle, text, exact ?? false),
                timeout,
                "page.getByTitle");

        /// <inheritdoc/>
        public Task<IElementHandle> GetByTestIdAsync(string testId, float? timeout = null)
            => GetByWaiter.WaitAsync(
                () => QuerySelectorAsync(GetBySelectorScript.TestIdSelector(testId)),
                timeout,
                "page.getByTestId");

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
        public async Task<byte[]> ScreenshotAsync(string path = default, ScreenshotType type = default, int? quality = default, bool? fullPage = default, Clip clip = default, bool? omitBackground = default, float? timeout = default, string scale = default, string animations = default, string caret = default, string style = default, IEnumerable<ILocator> mask = default, string maskColor = default)
        {
            type = ScreenshotValidate.ResolveType(path, type);
            ScreenshotValidate.EnsureQuality(type, quality);
            ScreenshotValidate.EnsureClip(clip, fullPage ?? false, _viewportSize);
            string format = ScreenshotFormat.ToProtocol(type);
            int? resolvedQuality = ScreenshotValidate.ResolvedQuality(type, quality);
            double deviceScale = _context is ChromiumBrowserContext crContext ? crContext.DeviceScaleFactor : 1;
            double clipScale = ScreenshotScaleHelper.ClipScale(scale, deviceScale);
            bool captureFullPage = fullPage ?? false;
            ScreenshotClip resolvedClip;
            if (clip != null)
            {
                resolvedClip = new ScreenshotClip
                {
                    X = clip.X,
                    Y = clip.Y,
                    Width = clip.Width,
                    Height = clip.Height,
                    Scale = clipScale,
                };
            }
            else if (captureFullPage)
            {
                JsonElement size = default;
                const string navigating = "Cannot take a screenshot while page is navigating";
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        size = await EvaluateFullPageSizeAsync().ConfigureAwait(false);
                        if (size.ValueKind == JsonValueKind.Object
                            && size.TryGetProperty("w", out JsonElement widthEl)
                            && size.TryGetProperty("h", out JsonElement heightEl)
                            && widthEl.GetDouble() > 0
                            && heightEl.GetDouble() > 0)
                        {
                            break;
                        }
                    }
                    catch (PlaywrightNativeException ex) when (
                        ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                        || ex.Message.Contains("Cannot find context with specified id", StringComparison.Ordinal)
                        || ex.Message.Contains(navigating, StringComparison.Ordinal))
                    {
                    }

                    if (attempt >= 20)
                    {
                        throw new PlaywrightNativeException(navigating);
                    }

                    await Task.Delay(50).ConfigureAwait(false);
                }

                resolvedClip = new ScreenshotClip
                {
                    X = 0,
                    Y = 0,
                    Width = size.GetProperty("w").GetDouble(),
                    Height = size.GetProperty("h").GetDouble(),
                    Scale = clipScale,
                };
            }
            else
            {
                resolvedClip = ScreenshotScaleHelper.ViewportClip(_viewportSize, clipScale, scale);
            }

            ScreenshotOptions options = new()
            {
                Format = format,
                Quality = resolvedQuality,
                FullPage = captureFullPage,
                OmitBackground = omitBackground == true,
                Clip = resolvedClip,
                Scale = scale,
                DeviceScaleFactor = deviceScale,
            };

            byte[] bytes = null;
            await ActionTrace.RunAsync(
                _context,
                null,
                "Page",
                "screenshot",
                async () =>
                {
                    bytes = await ScreenshotTimeout.RunAsync(
                        timeout,
                        () => ScreenshotDecorations.CaptureAsync(
                            this,
                            animations,
                            caret,
                            style,
                            () => _crPage.ScreenshotAsync(options),
                            mask,
                            maskColor)).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(path))
                    {
                        PathIo.WriteBytes(path, bytes);
                    }
                },
                result: new Dictionary<string, object> { ["binary"] = "<Buffer>" }).ConfigureAwait(false);
            return bytes;
        }

        /// <summary>
        /// Official element screenshot: document-coordinate clip plus
        /// <c>captureBeyondViewport</c> when the box does not fit.
        /// </summary>
        /// <param name="documentClip">Clip in document CSS pixels.</param>
        /// <param name="fitsViewport">Whether the element fits the viewport.</param>
        /// <param name="path">Optional output path.</param>
        /// <param name="type">Image format.</param>
        /// <param name="quality">JPEG quality.</param>
        /// <param name="omitBackground">Hide the default background.</param>
        /// <param name="scale">CSS vs device scale.</param>
        /// <returns>The image bytes.</returns>
        public async Task<byte[]> ScreenshotDocumentClipAsync(
            Clip documentClip,
            bool fitsViewport,
            string path,
            ScreenshotType type,
            int? quality,
            bool? omitBackground,
            string scale)
        {
            type = ScreenshotValidate.ResolveType(path, type);
            ScreenshotValidate.EnsureQuality(type, quality);
            string format = ScreenshotFormat.ToProtocol(type);
            int? resolvedQuality = ScreenshotValidate.ResolvedQuality(type, quality);
            double deviceScale = _context is ChromiumBrowserContext crContext ? crContext.DeviceScaleFactor : 1;
            double clipScale = ScreenshotScaleHelper.ClipScale(scale, deviceScale);
            ScreenshotOptions options = new()
            {
                Format = format,
                Quality = resolvedQuality,
                FullPage = false,
                CaptureBeyondViewport = !fitsViewport,
                OmitBackground = omitBackground == true,
                Clip = new ScreenshotClip
                {
                    X = documentClip.X,
                    Y = documentClip.Y,
                    Width = documentClip.Width,
                    Height = documentClip.Height,
                    Scale = clipScale,
                },
                Scale = scale,
                DeviceScaleFactor = deviceScale,
            };
            byte[] bytes = await _crPage.ScreenshotAsync(options).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(path))
            {
                PathIo.WriteBytes(path, bytes);
            }

            return bytes;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force, scroll), timeout, "page.selectOption", scroll);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, string values, bool? noWaitAfter = default, float? timeout = default, bool? force = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<string> values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(Array.Empty<string>(), noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IElementHandle values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, SelectOptionValue values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
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
        {
            int timeoutMs = timeout.HasValue ? (int)timeout.Value : (int)_defaultNavigationTimeout;
            return ActionTrace.RunAsync(
                _context,
                "Set content",
                "Page",
                "setContent",
                () => PlaywrightApiLog.RunAsync(
                    (_context as IHasPlaywrightLogger)?.Logger,
                    "page.setContent",
                    () => _crPage.SetContentAsync(html, waitUntil, timeoutMs)));
        }

        /// <inheritdoc/>
        public async Task SetExtraHttpHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
        {
            _pageExtraHttpHeaders = ExtraHttpHeaders.ToMap(headers);
            await ApplyMergedExtraHttpHeadersAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ApplyMergedExtraHttpHeadersAsync()
        {
            Dictionary<string, string> merged = ExtraHttpHeaders.Merged(_context, _pageExtraHttpHeaders);
            _extraReferer = HeaderMap.Value(merged, "referer");
            await _crPage.Session.SendAsync("Network.setExtraHTTPHeaders", new { headers = merged }).ConfigureAwait(false);
            await _crPage.NetworkManager.ApplyExtraHttpHeadersAsync(merged).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, string files, bool? noWaitAfter = default, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<string> files, bool? noWaitAfter = default, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, FilePayload files, bool? noWaitAfter = default, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout, force, scroll), timeout, "page.setInputFiles", scroll);

        /// <inheritdoc/>
        public async Task SetViewportSizeAsync(int width, int height)
        {
            int screenWidth = _independentScreen?.Width ?? width;
            int screenHeight = _independentScreen?.Height ?? height;
            await _crPage.SetViewportSizeAsync(
                new Input.ViewportSize(width, height),
                _emulatedDeviceScaleFactor,
                _emulatedIsMobile,
                screenWidth,
                screenHeight).ConfigureAwait(false);
            _viewportSize = new PageViewportSizeResult { Width = width, Height = height };
        }

        /// <inheritdoc/>
        public Task TapAsync(string selector, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? noWaitAfter = default, bool? force = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
        {
            TapSupport.ThrowIfDisabled(_context);
            return ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.TapAsync(position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "page.tap", scroll);
        }

        /// <inheritdoc/>
        public Task<string> TextContentAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.textContent",
                timeout,
                "page.textContent",
                strict ?? (_context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<string> TitleAsync()
            => PageTitle.ReadAsync(() => _crPage.EvaluateAsync<string>("document.title"));

        /// <inheritdoc/>
        public Task TypeAsync(string selector, string text, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.TypeAsync(text, delay, noWaitAfter, timeout, force, scroll), timeout, "page.type", scroll);

        /// <inheritdoc/>
        public Task UncheckAsync(string selector, Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.UncheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "page.uncheck", scroll);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crPage.UnrouteAsync(urlString, urlRegex, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crPage.UnrouteAsync(urlString, null, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => _crPage.UnrouteAsync(urlString, null, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crPage.UnrouteAsync(null, urlRegex, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => _crPage.UnrouteAsync(null, urlRegex, null, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => _crPage.UnrouteAsync(null, null, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => _crPage.UnrouteAsync(null, null, urlFunc, handler, behavior);

        /// <inheritdoc/>
        public async Task UnrouteAllAsync(UnrouteBehavior behavior = default)
        {
            await _crPage.UnrouteAllAsync(behavior).ConfigureAwait(false);
            await WebSocketRouter.UnrouteAllAsync(this).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task WaitForLoadStateAsync(LoadState state = LoadState.Load, float? timeout = default)
        {
            // Subscribe before resume so a load that fires as the renderer
            // unpauses is not missed. See LoadStateResume.
            Task wait = _crPage.MainFrame.WaitForLoadStateAsync(state, timeout);
            LoadStateResume.TryResume(_crPage.Session);
            return wait;
        }

        /// <inheritdoc/>
        public Task WaitForLoadStateAsync(string state, float? timeout = default)
            => WaitForLoadStateAsync(LoadStateName.Parse(state), timeout);

        /// <inheritdoc/>
        public Task<IJSHandle> WaitForFunctionAsync(string expression, object arg = default, float? pollingInterval = default, float? timeout = default)
        {
            float? resolvedTimeout = timeout ?? DefaultTimeout;
            Func<Task> rafAsync = () => _crPage.EvaluateAsync("new Promise(r => requestAnimationFrame(() => r(true)))");
            if (EvaluateWithArg.IsHandle(arg))
            {
                string truthyFn =
                    "async (arg) => { const fn = " + expression + "; return !!(await Promise.resolve(fn(arg))); }";
                string valueFn =
                    "async (arg) => { const fn = " + expression + "; return await Promise.resolve(fn(arg)); }";
                return WaitForFunctionHelper.WaitAsync<IJSHandle>(
                    async () =>
                    {
                        bool truthy = await _crPage.EvaluateFunctionAsync<bool>(truthyFn, arg).ConfigureAwait(false);
                        if (!truthy)
                        {
                            return null;
                        }

                        CRExecutionContext context = await _crPage.WaitForExecutionContextAsync().ConfigureAwait(false);
                        JsonElement? remote = await context.EvaluateFunctionHandleAsync(valueFn, arg).ConfigureAwait(false);
                        string objectId = RemoteObject.GetObjectId(remote);
                        if (remote == null || string.IsNullOrEmpty(objectId))
                        {
                            return new ImmediateJSHandle(JsonSerializer.SerializeToElement(true));
                        }

                        string preview = RemoteObject.HandlePreview(remote);
                        if (RemoteObject.IsNode(remote))
                        {
                            return new ChromiumElementHandle(new CRElementHandle(_crPage, context, objectId, "JSHandle@node"));
                        }

                        return new ChromiumJSHandle(new CRJSHandle(context, objectId, preview), _crPage);
                    },
                    pollingInterval,
                    resolvedTimeout,
                    rafAsync);
            }

            return WaitForFunctionHelper.WaitAsync(
                async wrapped =>
                {
                    // Poll with returnByValue so an in-flight handle evaluate cannot
                    // stall a concurrent page.evaluate that unblocks the predicate.
                    bool truthy = await _crPage.EvaluateAsync<bool>("!!(" + wrapped + ")").ConfigureAwait(false);
                    if (!truthy)
                    {
                        return null;
                    }

                    CRJSHandle handle = await _crPage.EvaluateHandleInternalAsync(wrapped).ConfigureAwait(false);
                    if (handle == null || string.IsNullOrEmpty(handle.ObjectId))
                    {
                        if (handle != null)
                        {
                            await handle.DisposeAsync().ConfigureAwait(false);
                        }

                        return new ImmediateJSHandle(JsonSerializer.SerializeToElement(true));
                    }

                    return WrapJSHandle(handle);
                },
                expression,
                pollingInterval,
                resolvedTimeout,
                rafAsync,
                arg: arg);
        }

        /// <inheritdoc/>
        public Task RemoveAllListenersAsync(string type = null, RemoveAllListenersBehavior behavior = default)
            => _pageListeners.RemoveAllListenersAsync(type, behavior);

        /// <inheritdoc/>
        public Task WaitForTimeoutAsync(float timeout)
            => ActionTrace.RunAsync(_context, "Wait for timeout", "Page", "waitForTimeout", () => Task.Delay((int)timeout));

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
                abortOnPageClose: this);

        /// <inheritdoc/>
        public Task<IResponse> WaitForResponseAsync(string urlString, Regex urlRegex, Func<IResponse, bool> predicate, float? timeout = default)
            => WaitForEventHelper.WaitAsync<IResponse>(
                h => Response += h,
                h => Response -= h,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null, NavigationUrl.ContextBase(Context)),
                timeout ?? DefaultTimeout,
                "page.waitForResponse",
                waitingLog: WaitForEventHelper.ResponseWaitingLog(urlString, urlRegex),
                abortOnPageClose: this);

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

        /// <summary>
        /// Returns the cached instance for <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The Chromium frame.</param>
        /// <returns>The public instance, or <see langword="null"/>.</returns>
        internal ChromiumFrame GetOrCreateFrame(Frame frame)
        {
            if (frame == null)
            {
                return null;
            }

            return _directFrames.GetOrAdd(frame, f => new ChromiumFrame(f, this));
        }

        /// <summary>
        /// Returns the cached instance for <paramref name="crResponse"/>.
        /// </summary>
        /// <param name="crResponse">The Chromium response.</param>
        /// <returns>The public instance.</returns>
        internal ChromiumResponse GetOrCreateDirectResponse(CRResponse crResponse)
            => _directResponses.GetOrAdd(crResponse, cr => new ChromiumResponse(cr, GetOrCreateDirectRequest));

        /// <summary>
        /// Applies viewport, device scale factor, mobile, and screen emulation from the owning context.
        /// </summary>
        /// <param name="width">Viewport width in CSS pixels.</param>
        /// <param name="height">Viewport height in CSS pixels.</param>
        /// <param name="deviceScaleFactor">Device pixel ratio.</param>
        /// <param name="isMobile">Whether to emulate a mobile viewport.</param>
        /// <param name="screenSize">Reported <c>window.screen</c> size, or <see langword="null"/>.</param>
        /// <returns>A task that completes when the override has been applied.</returns>
        internal async Task ApplyEmulatedViewportAsync(
            int width,
            int height,
            double deviceScaleFactor,
            bool isMobile,
            ScreenSize screenSize = null)
        {
            _emulatedDeviceScaleFactor = (float)deviceScaleFactor;
            _emulatedIsMobile = isMobile;
            _independentScreen = screenSize;
            int screenWidth = screenSize?.Width ?? width;
            int screenHeight = screenSize?.Height ?? height;
            await _crPage.SetViewportSizeAsync(
                new Input.ViewportSize(width, height),
                deviceScaleFactor,
                isMobile,
                screenWidth,
                screenHeight).ConfigureAwait(false);
            _viewportSize = new PageViewportSizeResult { Width = width, Height = height };
        }

        /// <summary>
        /// Records a close reason from the owning context so later page
        /// operations can surface it.
        /// </summary>
        /// <param name="reason">The context close reason.</param>
        internal void RecordCloseReason(string reason)
        {
            _closeReason = reason;
            _crPage.Session.CloseReason = reason;
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
        /// Completes in-flight downloads and cancels them in Chromium.
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

        internal async Task<IAsyncDisposable> InstallExposedAsync(
            string name,
            Func<JsonElement[], Task<object>> handler,
            bool fromContext = false)
        {
            if (!fromContext)
            {
                ThrowIfContextAlreadyHas(name);
            }

            string identifier = await _crPage.ExposeFunctionAsync(name, handler).ConfigureAwait(false);
            return AddInitScriptHelper.CreateDisposable(() => _crPage.RemoveExposedFunctionAsync(name, identifier));
        }

        internal async Task<IAsyncDisposable> InstallHandleBindingAsync(
            string name,
            Func<CRJSHandle, Task<object>> handler,
            bool fromContext = false)
        {
            if (!fromContext)
            {
                ThrowIfContextAlreadyHas(name);
            }

            string identifier = await _crPage.ExposeHandleBindingAsync(name, handler).ConfigureAwait(false);
            return AddInitScriptHelper.CreateDisposable(() => _crPage.RemoveExposedFunctionAsync(name, identifier));
        }

        internal bool HasExposedFunction(string name)
            => _crPage.HasExposedFunction(name);

        internal IJSHandle WrapJSHandle(CRJSHandle handle)
        {
            if (handle == null)
            {
                return new ImmediateJSHandle(JsonSerializer.SerializeToElement((object)null));
            }

            return handle.ToPublicHandle(_crPage);
        }

        internal void EmitWorkerConsole(IConsoleMessage message)
        {
            if (message is ConsoleMessage consoleMessage)
            {
                consoleMessage.Page = this;
            }

            _consoleLog.Add(message);
            _pageListeners.Console.Emit(this, message);
        }

        private void ThrowIfClosed()
        {
            if (_isClosed)
            {
                throw ClosedTarget.Exception("Page has been closed.", _closeReason);
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
            OfficialTraceSession.Active(_context)?.RecordAction("Route requests", "Page", "route");

            CRRouteEntry entry = new CRRouteEntry(
                urlString,
                urlRegex,
                urlFunc,
                crRoute =>
                {
                    ApplyChromiumRefererOverride(crRoute.Request);
                    return handler(new ChromiumRoute(crRoute, GetOrCreateDirectRequest));
                },
                handlerIdentity,
                isContextRoute: false,
                times);
            return _crPage.RouteAsync(entry);
        }

        private async Task<IElementHandle> QueryByScriptAsync(string functionDeclaration, params object[] args)
        {
            CRElementHandle crHandle = await _crPage.QueryFunctionAsync(functionDeclaration, args).ConfigureAwait(false);
            return crHandle == null ? null : new ChromiumElementHandle(crHandle);
        }

        private ChromiumWorker GetOrCreateWorker(CRWorker worker)
        {
            if (worker == null)
            {
                return null;
            }

            return _directWorkers.GetOrAdd(worker, w =>
            {
                ChromiumWorker instance = new ChromiumWorker(w);
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

        private void ThrowIfContextAlreadyHas(string name)
        {
            if (Context is IHasExposedFunctionNames contextNames && contextNames.HasExposedFunction(name))
            {
                throw new PlaywrightNativeException(PageBindingScript.AlreadyRegisteredInBrowserContext(name));
            }
        }

        /// <summary>
        /// Official screenshotter <c>_fullPageSize</c> via CDP
        /// <c>returnByValue</c> so a deleted page <c>Array</c> cannot break
        /// document-rect capture (no page-world serializer).
        /// </summary>
        private async Task<JsonElement> EvaluateFullPageSizeAsync()
        {
            const string expression = @"(() => {
  const body = document.body;
  const doc = document.documentElement;
  if (!body || !doc)
    return null;
  return {
    w: Math.max(body.scrollWidth, doc.scrollWidth, body.offsetWidth, doc.offsetWidth, body.clientWidth, doc.clientWidth),
    h: Math.max(body.scrollHeight, doc.scrollHeight, body.offsetHeight, doc.offsetHeight, body.clientHeight, doc.clientHeight)
  };
})()";
            CRExecutionContext context = await _crPage.WaitForExecutionContextAsync().ConfigureAwait(false);
            return await context.EvaluateAsync<JsonElement>(expression).ConfigureAwait(false);
        }

        private async Task<T> EvaluatePreparedAsync<T>(string handleFn, object[] handleArgs)
        {
            // Pass live handles through callFunctionOn so CRExecutionContext can
            // adopt ElementHandles (and reject cross-context JSHandles).
            object[] args = EvaluateHandleArg.AsCallFunctionArguments(handleArgs);
            string serializedFn = EvaluateHandleArg.WithSerializedHandleResult(handleFn);
            try
            {
                await Task.Yield();
                CRExecutionContext context = await _crPage.WaitForExecutionContextAsync().ConfigureAwait(false);
                JsonElement? wrapped = await context.EvaluateFunctionAsync(serializedFn, args).ConfigureAwait(false);
                return EvaluateSerialization.ParseRemote<T>(wrapped);
            }
            catch (PlaywrightNativeException ex)
            {
                throw EvaluateSerialization.RewriteException(ex);
            }
        }

        private async Task<T> EvaluateSerializedAsync<T>(string expression)
        {
            try
            {
                // Yield so callers can subscribe to page events (waitForEvent)
                // before Runtime.evaluate is sent — Node's event loop does this
                // automatically between `page.evaluate(...)` and `await waitForEvent`.
                await Task.Yield();
                CRExecutionContext context = await _crPage.WaitForExecutionContextAsync().ConfigureAwait(false);
                if (EvaluateSerialization.CanWrapExpression(expression))
                {
                    JsonElement? wrapped = await context
                        .EvaluateAsync(EvaluateSerialization.WithSerializedResult(expression))
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

        private void ApplyChromiumRefererOverride(CRRequest request)
        {
            if (request == null)
            {
                return;
            }

            string extra = _extraReferer ?? HeaderMap.Value(_crPage.NetworkManager.ExtraHttpHeaders, "referer");
            if (string.IsNullOrEmpty(extra))
            {
                return;
            }

            string doubled = extra.Contains(',', StringComparison.Ordinal) ? extra : extra + ", " + extra;
            request.ChromiumRefererOverride = doubled;
            HeaderMap.Set(request.Headers, "referer", doubled);
        }

        private ChromiumRequest GetOrCreateDirectRequest(CRRequest crRequest)
            => _directRequests.GetOrAdd(crRequest, cr => new ChromiumRequest(cr, GetOrCreateDirectResponse, GetOrCreateDirectRequest, GetOrCreateFrame));

        private void OnDialogOpening(CRDialog crDialog)
        {
            IDialog dialog = _dialogTracker.Wrap(new ChromiumDialog(crDialog, this), EmitDialogClosed);
            IDialogHost host = _context as IDialogHost;
            EventHandler<IDialog> pageDialog = Dialog;
            bool contextHasListeners = host != null && host.HasDialogListeners();
            pageDialog?.Invoke(this, dialog);
            host?.RaiseDialog(dialog);
            PageDialogTracker.AutoDismissIfNeeded(dialog, pageDialog, contextHasListeners);
        }

        private void EmitDialogClosed(IDialog dialog) => DialogClosed?.Invoke(this, dialog);

        private void OnCrDownloadWillBegin(object sender, DownloadWillBeginEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.Guid))
            {
                return;
            }

            ChromiumBrowserContext browserContext = _context as ChromiumBrowserContext;
            string directory = browserContext?.DownloadsPath;
            string contextId = browserContext?.BrowserContextId;
            bool acceptDownloads = browserContext?.AcceptDownloads != false;
            Page target = ResolveDownloadTarget();
            PageDownload download = new PageDownload(
                target,
                e.Url,
                e.SuggestedFilename,
                directory,
                e.Guid,
                () => _crPage.CancelDownloadAsync(e.Guid, contextId),
                acceptDownloads);
            target.AdoptDownload(e.Guid, download, e.SuggestedFilename);
        }

        private void OnCrDownloadProgress(object sender, DownloadProgressEventArgs e)
        {
            if (e == null || !_downloads.TryGetValue(e.Guid, out PageDownload download))
            {
                return;
            }

            if (string.Equals(e.State, "completed", StringComparison.OrdinalIgnoreCase))
            {
                download.MarkCompleted();
            }
            else if (string.Equals(e.State, "canceled", StringComparison.OrdinalIgnoreCase))
            {
                download.MarkFailed(e.Error ?? PageDownload.CanceledError);
            }
        }

        private Page ResolveDownloadTarget()
        {
            // Official new-window downloads: report on the opener when the popup
            // has not committed a real navigation (target=_blank attachment).
            if (_crPage.Opener != null
                && !_crPage.HasCommittedNonInitialNavigation
                && _crPage.Opener.PublicPage is Page opener)
            {
                return opener;
            }

            return this;
        }

        private void AdoptDownload(string guid, PageDownload download, string suggestedFilename)
        {
            if (!_downloads.TryAdd(guid, download))
            {
                if (_downloads.TryGetValue(guid, out PageDownload existing))
                {
                    existing.SetSuggestedFilename(suggestedFilename);
                }

                return;
            }

            Download?.Invoke(this, download);
        }

        private void OnCrFileChooserOpened(object sender, FileChooserOpenedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            _ = FileChooserWaitHelper.RaiseSafelyAsync(() => RaiseFileChooserAsync(e));
        }

        private async Task RaiseFileChooserAsync(FileChooserOpenedEventArgs e)
        {
            IElementHandle element = null;
            try
            {
                CRElementHandle crHandle = await _crPage.ResolveBackendNodeAsync(e.BackendNodeId, e.Session).ConfigureAwait(false);
                element = crHandle == null ? null : new ChromiumElementHandle(crHandle);
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

            FileChooser?.Invoke(this, new FileChooser(this, element, e.Multiple));
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
                strict ?? (_context is IHasStrictSelectors s && s.StrictSelectors),
                "page.dispatchEvent");

        private Task<bool> EvaluateDispatchBoolAsync(string script, object arg)
            => arg == null
                ? EvaluateSerializedAsync<bool>(script)
                : EvaluateAsync<bool>(script, arg);

        private Task<IElementHandle> QueryActionAsync(string selector)
            => QueryActionAsync(selector, default);

        private Task<IElementHandle> QueryActionAsync(string selector, bool? strict)
            => StrictSelector.QueryAsync(QuerySelectorAsync, QuerySelectorAllAsync, selector, strict ?? (_context is IHasStrictSelectors s && s.StrictSelectors));

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IPage.AddLocatorHandlerAsync(ILocator locator, Func<ILocator, Task> handler, PageAddLocatorHandlerOptions options)
        {
            LocatorHandlers.Add(this, locator, handler, options?.Times, options?.NoWaitAfter);
            return Task.CompletedTask;
        }

        Task IPage.AddLocatorHandlerAsync(ILocator locator, Func<Task> handler, PageAddLocatorHandlerOptions options)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            LocatorHandlers.Add(this, locator, _ => handler(), options?.Times, options?.NoWaitAfter);
            return Task.CompletedTask;
        }

        Task<IElementHandle> IPage.AddScriptTagAsync(PageAddScriptTagOptions options)
            => AddScriptTagAsync(options?.Url, options?.Path, options?.Content, options?.Type);

        Task<IElementHandle> IPage.AddStyleTagAsync(PageAddStyleTagOptions options)
            => AddStyleTagAsync(options?.Url, options?.Path, options?.Content);

        Task<string> IPage.AriaSnapshotAsync(PageAriaSnapshotOptions options)
            => PageAriaSnapshot.CaptureAsync(this, options);

        Task IPage.CancelPickLocatorAsync() => Task.CompletedTask;

        Task IPage.CheckAsync(string selector, PageCheckOptions options)
            => CheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.ClickAsync(string selector, PageClickOptions options)
            => ClickAsync(selector, options?.Button ?? default, options?.ClickCount, options?.Delay, options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, null, options?.Strict);

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

        Task<IAsyncDisposable> IPage.ExposeBindingAsync(string name, Action callback)
            => ExposeFunctionAsync(name, callback);

        Task<IAsyncDisposable> IPage.ExposeBindingAsync(string name, Action<BindingSource> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.WrapBinding<object>(Context, this, source =>
            {
                callback(source);
                return null;
            }));
        }

        Task<IAsyncDisposable> IPage.ExposeBindingAsync<T>(string name, Action<BindingSource, T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.WrapBinding<T, object>(Context, this, (source, arg) =>
            {
                callback(source, arg);
                return null;
            }));
        }

        Task<IAsyncDisposable> IPage.ExposeBindingAsync<T1, T2, T3, TResult>(string name, Func<BindingSource, T1, T2, T3, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.WrapBinding(Context, this, callback));
        }

        Task<IAsyncDisposable> IPage.ExposeBindingAsync<T1, T2, T3, T4, TResult>(string name, Func<BindingSource, T1, T2, T3, T4, TResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return InstallExposedAsync(name, PageExposeBinder.WrapBinding(Context, this, callback));
        }

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

        Task<IElementHandle> IPage.QuerySelectorAsync(string selector, PageQuerySelectorOptions options) => QuerySelectorAsync(selector);

        Task<IResponse> IPage.ReloadAsync(PageReloadOptions options)
            => ReloadAsync(options?.WaitUntil ?? default, options?.Timeout);

        Task IPage.RemoveLocatorHandlerAsync(ILocator locator)
        {
            LocatorHandlers.Remove(this, locator);
            return Task.CompletedTask;
        }

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

        Task<IFileChooser> IPage.RunAndWaitForFileChooserAsync(Func<Task> action, PageRunAndWaitForFileChooserOptions options)
            => RunAndWaitInternalAsync(
                action,
                FileChooserWaitHelper.WaitAsync(this, options?.Predicate, options?.Timeout));

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

        async Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, string values, PageSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Force, options?.Strict).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IElementHandle values, PageSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IEnumerable<string> values, PageSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, SelectOptionValue values, PageSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, PageSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IPage.SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, PageSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Force, default, options?.Strict).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        Task IPage.SetCheckedAsync(string selector, bool checkedState, PageSetCheckedOptions options)
            => checkedState
                ? CheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict)
                : UncheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.SetContentAsync(string html, PageSetContentOptions options)
            => SetContentAsync(html, options?.Timeout, options?.WaitUntil ?? default);

        void IPage.SetDefaultNavigationTimeout(float timeout) { }

        void IPage.SetDefaultTimeout(float timeout) { }

        Task IPage.SetExtraHTTPHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers) => Task.CompletedTask;

        Task IPage.SetInputFilesAsync(string selector, string files, PageSetInputFilesOptions options) => Task.CompletedTask;

        Task IPage.SetInputFilesAsync(string selector, IEnumerable<string> files, PageSetInputFilesOptions options) => Task.CompletedTask;

        Task IPage.SetInputFilesAsync(string selector, FilePayload files, PageSetInputFilesOptions options) => Task.CompletedTask;

        Task IPage.SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, PageSetInputFilesOptions options) => Task.CompletedTask;

        Task IPage.TapAsync(string selector, PageTapOptions options)
            => TapAsync(
                selector,
                options?.Position,
                options?.Modifiers,
                options?.NoWaitAfter,
                options?.Force,
                options?.Timeout,
                options?.Trial,
                ActionScrollBridge.FromScrollOption(options?.Scroll),
                options?.Strict);

        Task<string> IPage.TextContentAsync(string selector, PageTextContentOptions options) => TextContentAsync(selector, options?.Timeout, options?.Strict);

        Task IPage.TypeAsync(string selector, string text, PageTypeOptions options)
            => TypeAsync(selector, text, options?.Delay, options?.NoWaitAfter, options?.Timeout, null, default, options?.Strict);

        Task IPage.UncheckAsync(string selector, PageUncheckOptions options)
            => UncheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.UnrouteAllAsync(PageUnrouteAllOptions options) => Task.CompletedTask;

        Task IPage.UnrouteAsync(string url, Action<IRoute> handler) => Task.CompletedTask;

        Task IPage.UnrouteAsync(Regex url, Action<IRoute> handler) => Task.CompletedTask;

        Task IPage.UnrouteAsync(Func<string, bool> url, Action<IRoute> handler) => Task.CompletedTask;

        Task IPage.UnrouteAsync(string url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task IPage.UnrouteAsync(Regex url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task IPage.UnrouteAsync(Func<string, bool> url, Func<IRoute, Task> handler) => Task.CompletedTask;

        Task<IConsoleMessage> IPage.WaitForConsoleMessageAsync(PageWaitForConsoleMessageOptions options) => Task.FromResult<IConsoleMessage>(default!);

        Task<IDownload> IPage.WaitForDownloadAsync(PageWaitForDownloadOptions options) => Task.FromResult<IDownload>(default!);

        Task<IFileChooser> IPage.WaitForFileChooserAsync(PageWaitForFileChooserOptions options)
            => FileChooserWaitHelper.WaitAsync(this, options?.Predicate, options?.Timeout);

        Task<IJSHandle> IPage.WaitForFunctionAsync(string expression, object arg, PageWaitForFunctionOptions options) => WaitForFunctionAsync(expression, arg, options?.PollingInterval, options?.Timeout);

        Task IPage.WaitForLoadStateAsync(LoadState? state, PageWaitForLoadStateOptions options)
        {
            PageWaitForLoadStateOptions o = options;
            return WaitForLoadStateAsync(state ?? LoadState.Load, o?.Timeout);
        }

        Task<IResponse> IPage.WaitForNavigationAsync(PageWaitForNavigationOptions options)
            => WaitForNavigationAsync(options?.Url, options?.UrlRegex, options?.UrlFunc, options?.Timeout, options?.WaitUntil ?? default);

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
            => WaitForSelectorAsync(
                selector,
                options?.State ?? WaitForSelectorState.Visible,
                options?.Timeout,
                options?.Strict);

        Task IPage.WaitForURLAsync(string url, PageWaitForURLOptions options) => Task.CompletedTask;

        Task IPage.WaitForURLAsync(Regex url, PageWaitForURLOptions options) => Task.CompletedTask;

        Task IPage.WaitForURLAsync(Func<string, bool> url, PageWaitForURLOptions options) => Task.CompletedTask;

        Task<IWebSocket> IPage.WaitForWebSocketAsync(PageWaitForWebSocketOptions options) => Task.FromResult<IWebSocket>(default!);

        Task<IWorker> IPage.WaitForWorkerAsync(PageWaitForWorkerOptions options) => Task.FromResult<IWorker>(default!);

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
