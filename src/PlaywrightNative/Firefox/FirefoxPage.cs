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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Firefox <see cref="IPage"/> until <see cref="FFPage"/> is owned by shared
    /// <see cref="Page"/> as an <see cref="IPageDelegate"/>, matching Node.
    /// </summary>
    internal sealed partial class FirefoxPage : IPage, IHasPageExtras, IHasDefaultTimeouts, IHasLastPageErrorLocation, IHasClientInitializedPage
    {
        private readonly FFPage _page;
        private readonly IBrowserContext _context;
        private readonly PageDialogTracker _dialogTracker = new();
        private readonly PageListenerRegistry _pageListeners = new();
        private float _defaultTimeout = 30_000;
        private float _defaultNavigationTimeout = 30_000;
        private PageViewportSizeResult _viewportSize;

        internal FirefoxPage(FFPage page, IBrowserContext context)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            LocalStorage = new WebStorage(this, "localStorage");
            SessionStorage = new WebStorage(this, "sessionStorage");
            Screencast = new EmptyScreencast("Firefox", this);

            _page.Load += (_, _) => Load?.Invoke(this, this);
            _page.DOMContentLoaded += (_, _) => DOMContentLoaded?.Invoke(this, this);
            _page.DialogOpened += (_, d) => OnDialogOpened(d);
            _page.DialogClosedInBrowser += (_, _) => _dialogTracker.OnBrowserClosedDialog(EmitDialogClosed);
            _page.PopupOpened += (_, p) => Popup?.Invoke(this, new FirefoxPage(p, context));
            _page.RequestCreated += (_, r) => Request?.Invoke(this, new FirefoxRequest(r));
            _page.ResponseReceived += (_, r) => Response?.Invoke(this, new FirefoxResponse(r));
            _page.RequestFinished += (_, r) => RequestFinished?.Invoke(this, new FirefoxRequest(r));
            _page.RequestFailed += (_, r) => RequestFailed?.Invoke(this, new FirefoxRequest(r));
            _page.Closed += (_, _) => Close?.Invoke(this, this);
        }

        /// <inheritdoc/>
        public event EventHandler<IPage> Close;

        /// <inheritdoc/>
        public event EventHandler<IConsoleMessage> Console
        {
            add => throw NotImplementedHelper.ForMethod(nameof(Console));
            remove => throw NotImplementedHelper.ForMethod(nameof(Console));
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
        public IReadOnlyList<IWorker> Workers => Array.Empty<IWorker>();

        /// <inheritdoc/>
        public IVideo Video => VideoRecorder.GetVideo(this);

        /// <inheritdoc/>
        public IScreencast Screencast { get; }

        /// <inheritdoc/>
        public ICoverage Coverage { get; } = new EmptyCoverage();

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
        public WebErrorLocation LastPageErrorLocation { get; } = new WebErrorLocation();

        /// <inheritdoc/>
        public bool IsClientInitialized => true;

        /// <inheritdoc/>
        public IReadOnlyList<IFrame> Frames
            => throw NotImplementedHelper.ForMethod(nameof(Frames));

        /// <inheritdoc/>
        public bool IsClosed => false;

        /// <inheritdoc/>
        public IKeyboard Keyboard
        {
            get => throw NotImplementedHelper.ForMethod(nameof(Keyboard));
            set => throw NotImplementedHelper.ForMethod(nameof(Keyboard));
        }

        /// <inheritdoc/>
        public IFrame MainFrame
            => throw NotImplementedHelper.ForMethod(nameof(MainFrame));

        /// <inheritdoc/>
        public IMouse Mouse
        {
            get => throw NotImplementedHelper.ForMethod(nameof(Mouse));
            set => throw NotImplementedHelper.ForMethod(nameof(Mouse));
        }

        /// <inheritdoc/>
        public ITouchscreen Touchscreen
        {
            get => throw NotImplementedHelper.ForMethod(nameof(Touchscreen));
            set => throw NotImplementedHelper.ForMethod(nameof(Touchscreen));
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
        public string Url => _page.Url;

        /// <inheritdoc/>
        public PageViewportSizeResult ViewportSize => _viewportSize;

        /// <inheritdoc/>
        public Task<AccessibilitySnapshotResult> SnapshotAccessibilityAsync(bool? interestingOnly = null, IElementHandle root = null)
        {
            _ = interestingOnly;
            _ = root;
            return Task.FromException<AccessibilitySnapshotResult>(NotImplementedHelper.ForMethod(nameof(SnapshotAccessibilityAsync)));
        }

        /// <inheritdoc/>
        public async Task<IAsyncDisposable> AddInitScriptAsync(string script = null, string scriptPath = null)
        {
            script = AddInitScriptHelper.Resolve(script, scriptPath);
            await _page.AddInitScriptAsync(script).ConfigureAwait(false);
            return AddInitScriptHelper.CreateDisposable(() => Task.CompletedTask);
        }

        /// <inheritdoc/>
        public Task AddInitScriptAsync(string script, object arg)
        {
            script = AddInitScriptHelper.Resolve(script, null, arg);
            return AddInitScriptAsync(script, null);
        }

        /// <inheritdoc/>
        public Task<IElementHandle> AddScriptTagAsync(string url = null, string path = null, string content = null, string type = null)
            => throw NotImplementedHelper.ForMethod(nameof(AddScriptTagAsync));

        /// <inheritdoc/>
        public Task<IElementHandle> AddStyleTagAsync(string url = null, string path = null, string content = null)
            => throw NotImplementedHelper.ForMethod(nameof(AddStyleTagAsync));

        /// <inheritdoc/>
        public Task CheckAsync(string selector, Position position = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.CheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "page.check", scroll);
        }

        /// <inheritdoc/>
        public Task ClickAsync(string selector, MouseButton button = default, int? clickCount = null, float? delay = null, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, int? steps = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.ClickAsync(button, clickCount, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll, steps), timeout, "page.click", scroll);
        }

        /// <inheritdoc/>
        public async Task CloseAsync(bool? runBeforeUnload = null, string reason = null)
        {
            await _page.ClosePageAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => PageDispose.RunAsync(this);

        /// <inheritdoc/>
        public Task<string> ContentAsync() => _page.ContentAsync();

        /// <inheritdoc/>
        public Task DblClickAsync(string selector, MouseButton button = default, float? delay = null, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.DblClickAsync(button, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "page.dblclick", scroll);
        }

        /// <inheritdoc/>
        public Task EmulateMediaAsync(ColorScheme? colorScheme)
            => throw NotImplementedHelper.ForMethod(nameof(EmulateMediaAsync));

        /// <inheritdoc/>
        public Task EmulateMediaAsync(Media? media = null, ColorScheme? colorScheme = null)
            => throw NotImplementedHelper.ForMethod(nameof(EmulateMediaAsync));

        /// <inheritdoc/>
        public Task EmulateMediaAsync(ReducedMotion? reducedMotion = default, ForcedColors? forcedColors = default, Contrast? contrast = default)
            => throw NotImplementedHelper.ForMethod(nameof(EmulateMediaAsync));

        /// <inheritdoc/>
        public Task EmulateVisionDeficiencyAsync(VisionDeficiency type = default)
            => throw new PlaywrightNativeException("EmulateVisionDeficiencyAsync is Chromium-only.");

        /// <inheritdoc/>
        public async Task<T> EvaluateAsync<T>(string expression, object arg = null)
        {
            if (arg != null)
            {
                return await _page.EvaluateFunctionAsync<T>(expression, arg).ConfigureAwait(false);
            }

            return await _page.EvaluateAsync<T>(expression).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<JsonElement?> EvaluateAsync(string expression, object arg = null)
        {
            if (arg != null)
            {
                return await _page.EvaluateFunctionAsync(expression, arg).ConfigureAwait(false);
            }

            return await _page.EvaluateAsync(expression).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = null)
        {
            string toEval = arg == null ? expression : EvaluateWithArg.Wrap(expression, arg);
            JsonElement? remote = await _page.EvaluateHandleAsync(toEval).ConfigureAwait(false);
            return WrapJSHandle(_page.MainContext, remote);
        }

        /// <inheritdoc/>
        public Task ExposeBindingAsync(string name, Action callback, bool? handle = default)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task ExposeBindingAsync(string name, Func<BindingSource, IJSHandle, object> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync) + " with handle");

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<BindingSource, IJSHandle, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync) + " with handle");

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<TResult>(string name, Func<BindingSource, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<T, TResult>(string name, Func<BindingSource, T, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeBindingAsync<T1, T2, TResult>(string name, Func<BindingSource, T1, T2, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeBindingAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync(string name, Action callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T>(string name, Action<T> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<TResult>(string name, Func<TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T, TResult>(string name, Func<T, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, TResult>(string name, Func<T1, T2, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task<IAsyncDisposable> ExposeFunctionAsync<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> callback)
            => throw NotImplementedHelper.ForMethod(nameof(ExposeFunctionAsync));

        /// <inheritdoc/>
        public Task FillAsync(string selector, string value, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.FillAsync(value, noWaitAfter, timeout, force, scroll), timeout, "page.fill", scroll);
        }

        /// <inheritdoc/>
        public Task FocusAsync(string selector, float? timeout = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.FocusAsync(timeout, scroll), timeout, "page.focus", scroll);
        }

        /// <summary>Not yet implemented.</summary>
        public Task<IFrame> FrameAsync(string name)
            => throw NotImplementedHelper.ForMethod(nameof(FrameAsync));

        /// <inheritdoc/>
        public IFrame FrameByUrl(string urlString, Regex urlRegex, Func<string, bool> urlFunc)
            => throw NotImplementedHelper.ForMethod(nameof(FrameByUrl));

        /// <inheritdoc/>
        public Task<string> GetAttributeAsync(string selector, string name, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.GetAttributeAsync(name), timeout, "page.getAttribute");
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
        public Task<IResponse> GoToAsync(string url, string waitUntil, float? timeout = default, string referer = default)
            => GoToAsync(url, WaitUntilName.Parse(waitUntil), timeout, referer);

        /// <inheritdoc/>
        public async Task<IResponse> GoToAsync(string url, WaitUntilState waitUntil = default, float? timeout = default, string referer = default)
        {
            url = NavigationUrl.Resolve(Context, url);
            await _page.NavigateAsync(url).ConfigureAwait(false);
            return null;
        }

        /// <inheritdoc/>
        public Task<IResponse> ReloadAsync(WaitUntilState waitUntil = default, float? timeout = default)
            => throw NotImplementedHelper.ForMethod(nameof(ReloadAsync));

        /// <inheritdoc/>
        public Task<IResponse> GoBackAsync(WaitUntilState waitUntil = default, float? timeout = default)
            => throw NotImplementedHelper.ForMethod(nameof(GoBackAsync));

        /// <inheritdoc/>
        public Task<IResponse> GoForwardAsync(WaitUntilState waitUntil = default, float? timeout = default)
            => throw NotImplementedHelper.ForMethod(nameof(GoForwardAsync));

        /// <inheritdoc/>
        public Task BringToFrontAsync()
            => throw NotImplementedHelper.ForMethod(nameof(BringToFrontAsync));

        /// <inheritdoc/>
        public Task<IReadOnlyList<IConsoleMessage>> ConsoleMessagesAsync(ConsoleMessagesFilter filter = ConsoleMessagesFilter.SinceNavigation)
        {
            _ = filter;
            return Task.FromResult<IReadOnlyList<IConsoleMessage>>(Array.Empty<IConsoleMessage>());
        }

        /// <inheritdoc/>
        public Task ClearConsoleMessagesAsync()
            => Task.CompletedTask;

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> PageErrorsAsync(PageErrorsFilter filter = default)
        {
            _ = filter;
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> PageErrorsAsync()
            => PageErrorsAsync(default);

        /// <inheritdoc/>
        public Task ClearPageErrorsAsync()
            => Task.CompletedTask;

        /// <inheritdoc/>
        public Task<IReadOnlyList<IRequest>> RequestsAsync()
            => Task.FromResult<IReadOnlyList<IRequest>>(Array.Empty<IRequest>());

        /// <inheritdoc/>
        public Task RequestGCAsync()
            => throw NotImplementedHelper.ForMethod(nameof(RequestGCAsync));

        /// <inheritdoc/>
        public Task HoverAsync(string selector, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? force = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.HoverAsync(position, modifiers, force, timeout, trial, scroll), timeout, "page.hover", scroll);
        }

        /// <inheritdoc/>
        public Task<string> InnerHTMLAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.InnerHTMLAsync(), timeout, "page.innerHTML");
        }

        /// <inheritdoc/>
        public Task<string> InnerTextAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.InnerTextAsync(), timeout, "page.innerText");
        }

        /// <inheritdoc/>
        public Task<bool> IsCheckedAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.IsCheckedAsync(), timeout, "page.isChecked");
        }

        /// <inheritdoc/>
        public Task<bool> IsDisabledAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.IsDisabledAsync(), timeout, "page.isDisabled");
        }

        /// <inheritdoc/>
        public Task<bool> IsEditableAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.IsEditableAsync(), timeout, "page.isEditable");
        }

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.IsEnabledAsync(), timeout, "page.isEnabled");
        }

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync(string selector, float? timeout = null, bool? strict = default)
            => !await IsVisibleAsync(selector, timeout, strict).ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<bool> IsVisibleAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return DomVisibility.IsSelectorVisibleAsync(QuerySelectorAsync, selector);
        }

        /// <inheritdoc/>
        public Task<IPage> OpenerAsync()
            => throw NotImplementedHelper.ForMethod(nameof(OpenerAsync));

        /// <inheritdoc/>
        public Task<byte[]> PdfAsync(string path = default, float? scale = default, bool? displayHeaderFooter = default, string headerTemplate = default, string footerTemplate = default, bool? printBackground = default, bool? landscape = default, string pageRanges = default, string format = default, string width = default, string height = default, Margin margin = default, bool? preferCSSPageSize = default, bool? tagged = default, bool? outline = default)
            => throw NotImplementedHelper.ForMethod(nameof(PdfAsync));

        /// <inheritdoc/>
        public Task PressAsync(string selector, string key, float? delay = null, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.PressAsync(key, delay, noWaitAfter, timeout, force, scroll), timeout, "page.press", scroll);
        }

        /// <inheritdoc/>
        public async Task<IElementHandle> QuerySelectorAsync(string selector)
        {
            JsonElement? remote = await _page.QuerySelectorAsync(selector).ConfigureAwait(false);
            return WrapJSHandle(_page.MainContext, remote) as IElementHandle;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
            => throw NotImplementedHelper.ForMethod(nameof(QuerySelectorAllAsync));

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Func<IRoute, Task> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Regex urlRegex, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Regex urlRegex, Func<IRoute, Task> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public Task RouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler, int? times = default)
            => throw NotImplementedHelper.ForMethod(nameof(RouteAsync));

        /// <inheritdoc/>
        public async Task<byte[]> ScreenshotAsync(string path = null, ScreenshotType type = default, int? quality = null, bool? fullPage = null, Clip clip = null, bool? omitBackground = null, float? timeout = null, string scale = null, string animations = null, string caret = null, string style = null, IEnumerable<ILocator> mask = default, string maskColor = default)
        {
            type = ScreenshotValidate.ResolveType(path, type);
            ScreenshotValidate.EnsureQuality(type, quality);
            ScreenshotValidate.EnsureClip(clip, fullPage ?? false, _viewportSize);
            ScreenshotFormat.EnsureSupported(type, "Firefox");
            string format = ScreenshotFormat.ToProtocol(type);
            byte[] bytes = await ScreenshotTimeout.RunAsync(
                timeout,
                () => ScreenshotDecorations.CaptureAsync(
                    this,
                    animations,
                    caret,
                    style,
                    () => _page.ScreenshotAsync(format, quality, fullPage ?? false),
                    mask,
                    maskColor)).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(path))
            {
                PathIo.WriteBytes(path, bytes);
            }

            return bytes;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force, scroll), timeout, "page.selectOption", scroll);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, string values, bool? noWaitAfter = null, float? timeout = null, bool? force = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<string> values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(Array.Empty<string>(), noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IElementHandle values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, SelectOptionValue values, bool? noWaitAfter = null, float? timeout = null, bool? strict = default, bool? force = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "page.selectOption", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params string[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values), null, "page.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params SelectOptionValue[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values), null, "page.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params IElementHandle[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.SelectOptionAsync(values), null, "page.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task SetContentAsync(string html, float? timeout = default, WaitUntilState waitUntil = default)
            => _page.SetContentAsync(html);

        /// <summary>Not yet implemented.</summary>
        public Task SetDefaultNavigationTimeoutAsync(float timeout)
        {
            DefaultNavigationTimeout = timeout;
            return Task.CompletedTask;
        }

        /// <summary>Sets <see cref="DefaultTimeout"/>.</summary>
        public Task SetDefaultTimeoutAsync(float timeout)
        {
            DefaultTimeout = timeout;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SetExtraHttpHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
            => _page.SetExtraHttpHeadersAsync(headers);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, string files, bool? noWaitAfter = null, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<string> files, bool? noWaitAfter = null, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, FilePayload files, bool? noWaitAfter = null, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "page.setInputFiles", ActionScroll.Undefined);
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout, force, scroll), timeout, "page.setInputFiles", scroll);
        }

        /// <inheritdoc/>
        public async Task SetViewportSizeAsync(int width, int height)
        {
            await _page.SetViewportSizeAsync(width, height).ConfigureAwait(false);
            _viewportSize = new PageViewportSizeResult { Width = width, Height = height };
        }

        /// <inheritdoc/>
        public Task TapAsync(string selector, Position position = null, IEnumerable<KeyboardModifier> modifiers = null, bool? noWaitAfter = null, bool? force = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.TapAsync(position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "page.tap", scroll);
        }

        /// <inheritdoc/>
        public Task<string> TextContentAsync(string selector, float? timeout = null, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitQueryAsync(QuerySelectorAsync, selector, h => h.TextContentAsync(), timeout, "page.textContent");
        }

        /// <inheritdoc/>
        public Task<string> TitleAsync()
            => _page.EvaluateAsync<string>("document.title");

        /// <inheritdoc/>
        public Task TypeAsync(string selector, string text, float? delay = null, bool? noWaitAfter = null, float? timeout = null, bool? force = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.TypeAsync(text, delay, noWaitAfter, timeout, force, scroll), timeout, "page.type", scroll);
        }

        /// <inheritdoc/>
        public Task UncheckAsync(string selector, Position position = null, bool? force = null, bool? noWaitAfter = null, float? timeout = null, bool? trial = null, ActionScroll scroll = default, bool? strict = default)
        {
            _ = strict;
            return ElementQuery.WaitRunAsync(QuerySelectorAsync, selector, h => h.UncheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "page.uncheck", scroll);
        }

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Regex urlRegex, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Action<IRoute> handler = null, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(Func<string, bool> urlFunc, Func<IRoute, Task> handler, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, Action<IRoute> handler = default, UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAsync));

        /// <inheritdoc/>
        public Task UnrouteAllAsync(UnrouteBehavior behavior = default)
            => throw NotImplementedHelper.ForMethod(nameof(UnrouteAllAsync));

        /// <inheritdoc/>
        public Task RemoveAllListenersAsync(string type = null, RemoveAllListenersBehavior behavior = default)
            => _pageListeners.RemoveAllListenersAsync(type, behavior);

        /// <inheritdoc/>
        public Task WaitForLoadStateAsync(string state, float? timeout = default)
            => WaitForLoadStateAsync(LoadStateName.Parse(state), timeout);

        /// <inheritdoc/>
        public Task WaitForLoadStateAsync(LoadState state = LoadState.Load, float? timeout = default)
            => LifecycleWaiter.WaitAsync(
                _page.SnapshotLifecycle,
                handler => _page.LifecycleChanged += handler,
                handler => _page.LifecycleChanged -= handler,
                state,
                timeout);

        /// <inheritdoc/>
        public Task<IJSHandle> WaitForFunctionAsync(string expression, object arg = default, float? pollingInterval = default, float? timeout = default)
        {
            return WaitForFunctionHelper.WaitAsync(
                async wrapped =>
                {
                    bool truthy = await _page.EvaluateFunctionAsync<bool>(
                        "async () => !!(await Promise.resolve(" + wrapped + "))").ConfigureAwait(false);
                    if (!truthy)
                    {
                        return null;
                    }

                    JsonElement? remote = await _page.EvaluateHandleAsync(wrapped).ConfigureAwait(false);
                    IJSHandle handle = WrapJSHandle(_page.MainContext, remote);
                    return handle ?? new FFJSHandle(_page.MainContext, null);
                },
                expression,
                pollingInterval,
                timeout ?? DefaultTimeout,
                () => _page.EvaluateAsync("new Promise(r => requestAnimationFrame(() => r(true)))"),
                arg: arg);
        }

        /// <inheritdoc/>
        public Task WaitForTimeoutAsync(float timeout)
            => Task.Delay((int)timeout);

        /// <inheritdoc/>
        public Task WaitForURLAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, float? timeout = default, WaitUntilState waitUntil = default)
            => WaitForUrlHelper.WaitAsync(
                () => Url,
                WaitForLoadStateAsync,
                urlString,
                urlRegex,
                urlFunc,
                timeout,
                waitUntil);

        /// <inheritdoc/>
        public Task<IResponse> WaitForNavigationAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, float? timeout = default, WaitUntilState waitUntil = default)
            => throw NotImplementedHelper.ForMethod(nameof(WaitForNavigationAsync));

        /// <inheritdoc/>
        public Task<IElementHandle> WaitForSelectorAsync(
            string selector,
            WaitForSelectorState state = WaitForSelectorState.Visible,
            float? timeout = default,
            bool? strict = default,
            string waitFor = default,
            string visibility = default)
        {
            _ = strict;
            WaitForSelectorName.Validate(waitFor, visibility);
            return WaitForSelectorHelper.WaitAsync(QuerySelectorAsync, selector, state, timeout);
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
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null),
                timeout ?? DefaultTimeout,
                "page.waitForRequest",
                waitingLog: WaitForEventHelper.RequestWaitingLog(urlString, urlRegex),
                abortOnPageClose: this);

        /// <inheritdoc/>
        public Task<IResponse> WaitForResponseAsync(string urlString, Regex urlRegex, Func<IResponse, bool> predicate, float? timeout = default)
            => WaitForEventHelper.WaitAsync<IResponse>(
                h => Response += h,
                h => Response -= h,
                r => predicate != null ? predicate(r) : UrlMatcher.Matches(r.Url, urlString, urlRegex, null),
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

        /// <summary>Not yet implemented.</summary>
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
        {
            _ = strict;
            throw NotImplementedHelper.ForMethod(nameof(DragAndDropAsync));
        }

        private async Task<IElementHandle> QueryByScriptAsync(string functionDeclaration, params object[] args)
        {
            JsonElement? remote = await _page.QueryFunctionHandleAsync(functionDeclaration, args).ConfigureAwait(false);
            return WrapJSHandle(_page.MainContext, remote) as IElementHandle;
        }

        private IJSHandle WrapJSHandle(FFExecutionContext context, JsonElement? handleValue)
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
                return null;
            }

            if (!remoteObject.TryGetProperty("objectId", out JsonElement objectIdElement)
                || objectIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string objectId = objectIdElement.GetString();
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            if (remoteObject.TryGetProperty("subtype", out JsonElement nodeSubtype)
                && nodeSubtype.ValueKind == JsonValueKind.String
                && nodeSubtype.GetString() == "node")
            {
                return new FFElementHandle(context, objectId);
            }

            return new FFJSHandle(context, objectId, RemoteObject.HandlePreview(remoteObject));
        }

        private void OnDialogOpened(FFDialog ffDialog)
        {
            IDialog dialog = _dialogTracker.Wrap(new FirefoxDialog(ffDialog, this), EmitDialogClosed);
            IDialogHost host = _context as IDialogHost;
            EventHandler<IDialog> pageDialog = Dialog;
            bool contextHasListeners = host != null && host.HasDialogListeners();
            pageDialog?.Invoke(this, dialog);
            host?.RaiseDialog(dialog);
            PageDialogTracker.AutoDismissIfNeeded(dialog, pageDialog, contextHasListeners);
        }

        private void EmitDialogClosed(IDialog dialog) => DialogClosed?.Invoke(this, dialog);

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IPage.AddLocatorHandlerAsync(ILocator locator, Func<ILocator, Task> handler, PageAddLocatorHandlerOptions options) => Task.CompletedTask;

        Task IPage.AddLocatorHandlerAsync(ILocator locator, Func<Task> handler, PageAddLocatorHandlerOptions options) => Task.CompletedTask;

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

        Task<IReadOnlyList<IConsoleMessage>> IPage.ConsoleMessagesAsync(PageConsoleMessagesOptions options) => ConsoleMessagesAsync(options?.Filter ?? ConsoleMessagesFilter.SinceNavigation);

        Task IPage.DblClickAsync(string selector, PageDblClickOptions options)
            => DblClickAsync(selector, options?.Button ?? default, options?.Delay, options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IPage.DispatchEventAsync(string selector, string type, object eventInit, PageDispatchEventOptions options) => Task.CompletedTask;

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
                default,
                options?.Strict);

        Task IPage.EmulateMediaAsync(PageEmulateMediaOptions options) => Task.CompletedTask;

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
                QuerySelectorAsync(selector),
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
                QuerySelectorAsync(selector),
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

        Task IPage.RemoveLocatorHandlerAsync(ILocator locator) => Task.CompletedTask;

        Task<IAsyncDisposable> IPage.RouteAsync(string url, Action<IRoute> handler, PageRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.RouteAsync(Regex url, Action<IRoute> handler, PageRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.RouteAsync(Func<string, bool> url, Action<IRoute> handler, PageRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.RouteAsync(string url, Func<IRoute, Task> handler, PageRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.RouteAsync(Regex url, Func<IRoute, Task> handler, PageRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task<IAsyncDisposable> IPage.RouteAsync(Func<string, bool> url, Func<IRoute, Task> handler, PageRouteOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task IPage.RouteFromHARAsync(string har, PageRouteFromHAROptions options) => Task.CompletedTask;

        Task IPage.RouteWebSocketAsync(string url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IPage.RouteWebSocketAsync(Regex url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task IPage.RouteWebSocketAsync(Func<string, bool> url, Action<IWebSocketRoute> handler) => Task.CompletedTask;

        Task<IConsoleMessage> IPage.RunAndWaitForConsoleMessageAsync(Func<Task> action, PageRunAndWaitForConsoleMessageOptions options) => Task.FromResult<IConsoleMessage>(default!);

        Task<IDownload> IPage.RunAndWaitForDownloadAsync(Func<Task> action, PageRunAndWaitForDownloadOptions options) => Task.FromResult<IDownload>(default!);

        Task<IFileChooser> IPage.RunAndWaitForFileChooserAsync(Func<Task> action, PageRunAndWaitForFileChooserOptions options) => Task.FromResult<IFileChooser>(default!);

        Task<IResponse> IPage.RunAndWaitForNavigationAsync(Func<Task> action, PageRunAndWaitForNavigationOptions options) => Task.FromResult<IResponse>(default!);

        Task<IPage> IPage.RunAndWaitForPopupAsync(Func<Task> action, PageRunAndWaitForPopupOptions options) => Task.FromResult<IPage>(default!);

        Task<IRequest> IPage.RunAndWaitForRequestAsync(Func<Task> action, string urlOrPredicate, PageRunAndWaitForRequestOptions options) => Task.FromResult<IRequest>(default!);

        Task<IRequest> IPage.RunAndWaitForRequestAsync(Func<Task> action, Regex urlOrPredicate, PageRunAndWaitForRequestOptions options) => Task.FromResult<IRequest>(default!);

        Task<IRequest> IPage.RunAndWaitForRequestAsync(Func<Task> action, Func<IRequest, bool> urlOrPredicate, PageRunAndWaitForRequestOptions options) => Task.FromResult<IRequest>(default!);

        Task<IRequest> IPage.RunAndWaitForRequestFinishedAsync(Func<Task> action, PageRunAndWaitForRequestFinishedOptions options) => Task.FromResult<IRequest>(default!);

        Task<IResponse> IPage.RunAndWaitForResponseAsync(Func<Task> action, string urlOrPredicate, PageRunAndWaitForResponseOptions options) => Task.FromResult<IResponse>(default!);

        Task<IResponse> IPage.RunAndWaitForResponseAsync(Func<Task> action, Regex urlOrPredicate, PageRunAndWaitForResponseOptions options) => Task.FromResult<IResponse>(default!);

        Task<IResponse> IPage.RunAndWaitForResponseAsync(Func<Task> action, Func<IResponse, bool> urlOrPredicate, PageRunAndWaitForResponseOptions options) => Task.FromResult<IResponse>(default!);

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
                default,
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

        Task<IFileChooser> IPage.WaitForFileChooserAsync(PageWaitForFileChooserOptions options) => Task.FromResult<IFileChooser>(default!);

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

        Task<IRequest> IPage.WaitForRequestFinishedAsync(PageWaitForRequestFinishedOptions options) => Task.FromResult<IRequest>(default!);

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
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
