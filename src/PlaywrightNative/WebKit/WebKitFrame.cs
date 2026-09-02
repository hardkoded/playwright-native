/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>Public <see cref="IFrame"/> wrapping <see cref="WKFrame"/>.</summary>
    internal sealed partial class WebKitFrame : IFrame
    {
        private readonly WKFrame _wkFrame;
        private readonly WKPage _page;

        internal WebKitFrame(WKFrame wkFrame, WKPage page)
        {
            _wkFrame = wkFrame ?? throw new ArgumentNullException(nameof(wkFrame));
            _page = page ?? throw new ArgumentNullException(nameof(page));
        }

        /// <inheritdoc/>
        public IReadOnlyList<IFrame> ChildFrames
        {
            get
            {
                List<IFrame> children = new List<IFrame>();
                foreach (WKFrame child in _wkFrame.ChildFrames)
                {
                    children.Add(_page.GetOrCreateFrame(child));
                }

                return children;
            }
        }

        /// <inheritdoc/>
        public bool IsDetached => _wkFrame.IsDetached;

        /// <inheritdoc/>
        public string Name => _wkFrame.Name;

        /// <inheritdoc/>
        public IPage Page => _page;

        /// <inheritdoc/>
        public IFrame ParentFrame
            => _wkFrame.ParentFrame == null ? null : _page.GetOrCreateFrame(_wkFrame.ParentFrame);

        /// <inheritdoc/>
        public string Url => _wkFrame.Url;

        /// <inheritdoc/>
        public Task CheckAsync(string selector, Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.CheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "frame.check", scroll);

        /// <inheritdoc/>
        public Task ClickAsync(string selector, MouseButton button = default, int? clickCount = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default, bool? strict = default)
            => ClickAction.RunOnSelectorAsync(sel => QueryActionAsync(sel, strict), selector, h => h.ClickAsync(button, clickCount, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll, steps), timeout, "frame.click", scroll);

        /// <inheritdoc/>
        public Task<string> ContentAsync()
            => PageContent.ReadAsync(() => EvaluateAsync<string>(PageContent.EvaluateExpression));

        /// <inheritdoc/>
        public Task DblClickAsync(string selector, MouseButton button = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ClickAction.RunOnSelectorAsync(sel => QueryActionAsync(sel, strict), selector, h => h.DblClickAsync(button, delay, position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "frame.dblclick", scroll);

        /// <inheritdoc/>
        public Task<JsonElement?> EvaluateAsync(string expression, object arg = default)
            => EvaluateInOwnFrameAsync<JsonElement?>(expression, arg);

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = default)
            => EvaluateInOwnFrameAsync<T>(expression, arg);

        /// <inheritdoc/>
        public Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            EvaluateWithArg.ThrowIfDetached(this);
            if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
            {
                return EvaluatePreparedHandleAsync(handleFn, handleArgs);
            }

            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
            return _page.EvaluateHandleInFrameAsync(_wkFrame, toEval);
        }

        /// <inheritdoc/>
        public Task FillAsync(string selector, string value, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.FillAsync(value, noWaitAfter, timeout, force, scroll), timeout, "frame.fill", scroll);

        /// <inheritdoc/>
        public Task FocusAsync(string selector, float? timeout = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.FocusAsync(timeout, scroll), timeout, "frame.focus", scroll);

        /// <inheritdoc/>
        public Task<string> GetAttributeAsync(string selector, string name, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.getAttribute(" + JsonSerializer.Serialize(name) + ")",
                timeout,
                "frame.getAttribute",
                strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<IResponse> GoToAsync(string url, string waitUntil, float? timeout = default, string referer = default)
            => GoToAsync(url, WaitUntilName.Parse(waitUntil), timeout, referer);

        /// <inheritdoc/>
        public Task<IResponse> GoToAsync(string url, WaitUntilState waitUntil = default, float? timeout = default, string referer = default)
            => _page.GoToFrameAsync(_wkFrame, NavigationUrl.Resolve(_page?.Context, url), waitUntil, timeout, referer);

        /// <inheritdoc/>
        public Task HoverAsync(string selector, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.HoverAsync(position, modifiers, force, timeout, trial, scroll), timeout, "frame.hover", scroll);

        /// <inheritdoc/>
        public Task<string> InnerHTMLAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.innerHTML",
                timeout,
                "frame.innerHTML",
                strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<string> InnerTextAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.innerText",
                timeout,
                "frame.innerText",
                strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<bool> IsCheckedAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsCheckedAsync(), timeout, "frame.isChecked");

        /// <inheritdoc/>
        public Task<bool> IsDisabledAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsDisabledAsync(), timeout, "frame.isDisabled");

        /// <inheritdoc/>
        public Task<bool> IsEditableAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsEditableAsync(), timeout, "frame.isEditable");

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync(string selector, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.IsEnabledAsync(), timeout, "frame.isEnabled");

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync(string selector, float? timeout = default, bool? strict = default)
            => !await IsVisibleAsync(selector, timeout, strict).ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<bool> IsVisibleAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.IsVisibleAsync(expression => EvaluateAsync<JsonElement?>(expression), selector, strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task PressAsync(string selector, string key, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.PressAsync(key, delay, noWaitAfter, timeout, force, scroll), timeout, "frame.press", scroll);

        /// <inheritdoc/>
        public Task<IElementHandle> QuerySelectorAsync(string selector)
            => _page.QuerySelectorInFrameAsync(_wkFrame, selector);

        /// <inheritdoc/>
        public Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
            => _page.QuerySelectorAllInFrameAsync(_wkFrame, selector);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force, scroll), timeout, "frame.selectOption", scroll);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, string values, bool? noWaitAfter = default, float? timeout = default, bool? force = default, bool? strict = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "frame.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<string> values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "frame.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(Array.Empty<string>(), noWaitAfter, timeout, force), timeout, "frame.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IElementHandle values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "frame.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "frame.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, SelectOptionValue values, bool? noWaitAfter = default, float? timeout = default, bool? strict = default, bool? force = default)
            => ElementQuery.WaitQueryAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SelectOptionAsync(values, noWaitAfter, timeout, force), timeout, "frame.selectOption", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params string[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QueryActionAsync, selector, h => h.SelectOptionAsync(values), null, "frame.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params SelectOptionValue[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QueryActionAsync, selector, h => h.SelectOptionAsync(values), null, "frame.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string selector, params IElementHandle[] values)
            => CompatCollections.AsCollectionAsync(ElementQuery.WaitQueryAsync(QueryActionAsync, selector, h => h.SelectOptionAsync(values), null, "frame.selectOption", ActionScroll.Undefined));

        /// <inheritdoc/>
        public Task SetContentAsync(string html, float? timeout = default, WaitUntilState waitUntil = default)
            => _page.SetContentInFrameAsync(_wkFrame, html, timeout, waitUntil);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout, force, scroll), timeout, "frame.setInputFiles", scroll);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, string files, bool? noWaitAfter = default, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "frame.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, IEnumerable<string> files, bool? noWaitAfter = default, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "frame.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string selector, FilePayload files, bool? noWaitAfter = default, float? timeout = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.SetInputFilesAsync(files, noWaitAfter, timeout), timeout, "frame.setInputFiles", ActionScroll.Undefined);

        /// <inheritdoc/>
        public Task TapAsync(string selector, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? noWaitAfter = default, bool? force = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
        {
            TapSupport.ThrowIfDisabled(_page.Context);
            return ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.TapAsync(position, modifiers, force, noWaitAfter, timeout, trial, scroll), timeout, "frame.tap", scroll);
        }

        /// <inheritdoc/>
        public Task<string> TextContentAsync(string selector, float? timeout = default, bool? strict = default)
            => AtomicSelectorRead.WaitStringAsync(
                expression => EvaluateAsync<JsonElement?>(expression),
                selector,
                "el.textContent",
                timeout,
                "frame.textContent",
                strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors));

        /// <inheritdoc/>
        public Task<string> TitleAsync()
            => PageTitle.ReadAsync(() => EvaluateAsync<string>("document.title"));

        /// <inheritdoc/>
        public Task TypeAsync(string selector, string text, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.TypeAsync(text, delay, noWaitAfter, timeout, force, scroll), timeout, "frame.type", scroll);

        /// <inheritdoc/>
        public Task UncheckAsync(string selector, Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, bool? strict = default)
            => ElementQuery.WaitRunAsync(sel => QueryActionAsync(sel, strict), selector, h => h.UncheckAsync(position, force, noWaitAfter, timeout, trial, scroll), timeout, "frame.uncheck", scroll);

        /// <inheritdoc/>
        public Task<IResponse> WaitForNavigationAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, float? timeout = default, WaitUntilState waitUntil = default)
            => WaitForNavigationHelper.WaitAsync(
                _page,
                (state, t) => _page.WaitForFrameLoadStateAsync(_wkFrame, state, t),
                urlString,
                urlRegex,
                urlFunc,
                timeout,
                waitUntil,
                frame => ReferenceEquals(frame, this),
                "frame.waitForNavigation");

        /// <inheritdoc/>
        public Task WaitForURLAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, float? timeout = default, WaitUntilState waitUntil = default)
            => WaitForUrlHelper.WaitAsync(
                () => Url,
                (state, t) => _page.WaitForFrameLoadStateAsync(_wkFrame, state, t),
                urlString,
                urlRegex,
                urlFunc,
                timeout,
                waitUntil,
                "frame.waitForURL");

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
            return WaitForSelectorHelper.WaitAsync(
                sel => QueryActionAsync(sel, strict),
                selector,
                state,
                timeout,
                "frame.waitForSelector",
                () => IsDetached);
        }

        /// <inheritdoc/>
        public Task<IElementHandle> WaitForSelectorAsync(string selector, string state, float? timeout = default, bool? strict = default)
            => WaitForSelectorAsync(selector, WaitForSelectorName.Parse(state), timeout, strict);

        /// <inheritdoc/>
        public Task<IElementHandle> WaitForSelectorAsync(string selector, bool state, float? timeout = default, bool? strict = default)
            => WaitForSelectorAsync(selector, WaitForSelectorName.Parse(state), timeout, strict);

        /// <inheritdoc/>
        public Task WaitForLoadStateAsync(LoadState state = LoadState.Load, float? timeout = default)
            => _page.WaitForFrameLoadStateAsync(_wkFrame, state, timeout);

        /// <inheritdoc/>
        public Task WaitForLoadStateAsync(string state, float? timeout = default)
            => WaitForLoadStateAsync(LoadStateName.Parse(state), timeout);

        /// <inheritdoc/>
        public Task<IJSHandle> WaitForFunctionAsync(string expression, object arg = default, float? pollingInterval = default, float? timeout = default)
        {
            float? resolvedTimeout = timeout ?? (_page != null ? _page.DefaultTimeout() : timeout);
            Func<Task> rafAsync = () => EvaluateInOwnFrameAsync<object>("new Promise(r => requestAnimationFrame(() => r(true)))", null);
            if (EvaluateWithArg.IsHandle(arg))
            {
                string boxedFn = WaitForFunctionHelper.BuildPredicateFunction(expression);
                return WaitForFunctionHelper.WaitAsync(
                    async () =>
                    {
                        EvaluateWithArg.ThrowIfDetached(this);
                        bool truthy = await EvaluateInOwnFrameAsync<bool>(
                            "async (arg) => { const boxed = await (" + boxedFn + ")(arg); return !!boxed; }",
                            arg).ConfigureAwait(false);
                        return truthy ? await EvaluateHandleAsync("() => true").ConfigureAwait(false) : null;
                    },
                    pollingInterval,
                    resolvedTimeout,
                    rafAsync,
                    "frame.waitForFunction",
                    () => IsDetached);
            }

            return WaitForFunctionHelper.WaitAsync<IJSHandle>(
                async wrapped =>
                {
                    bool truthy = await EvaluateInOwnFrameAsync<bool>("(async () => !!(await Promise.resolve(" + wrapped + ")))()", null).ConfigureAwait(false);
                    return truthy ? await EvaluateHandleAsync(wrapped).ConfigureAwait(false) : null;
                },
                expression,
                pollingInterval,
                resolvedTimeout,
                rafAsync,
                "frame.waitForFunction",
                () => IsDetached,
                arg);
        }

        /// <inheritdoc/>
        public Task WaitForTimeoutAsync(float timeout)
            => Task.Delay((int)timeout);

        /// <summary>
        /// Returns the wrapped WebKit frame.
        /// </summary>
        /// <returns>The internal frame.</returns>
        internal WKFrame GetWKFrame() => _wkFrame;

        /// <summary>
        /// Whether this frame already has an execution context (stalled
        /// iframes stay empty and must not block any-frame search).
        /// </summary>
        /// <returns><see langword="true"/> when the document is already usable.</returns>
        internal bool HasQueryableContext() => _page.HasFrameContext(_wkFrame);

        private async Task<IJSHandle> EvaluatePreparedHandleAsync(string handleFn, object[] handleArgs)
        {
            await EvaluateHandleArg.StashRemoteHandlesAsync(handleArgs).ConfigureAwait(false);
            return await _page
                .EvaluateFunctionHandleInFrameAsync(_wkFrame, handleFn, EvaluateHandleArg.TreeArgument(handleArgs))
                .ConfigureAwait(false);
        }

        private async Task<T> EvaluatePreparedAsync<T>(string handleFn, object[] handleArgs)
        {
            await EvaluateHandleArg.StashRemoteHandlesAsync(handleArgs).ConfigureAwait(false);
            return await _page
                .EvaluateSerializedInFrameAsync<T>(_wkFrame, EvaluateHandleArg.PreparedExpression(handleFn, handleArgs))
                .ConfigureAwait(false);
        }

        private Task<T> EvaluateInOwnFrameAsync<T>(string expression, object arg)
        {
            EvaluateWithArg.ThrowIfDetached(this);
            if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
            {
                return EvaluatePreparedAsync<T>(handleFn, handleArgs);
            }

            if (EvaluateWithArg.IsHandle(arg))
            {
                return _page.EvaluateAsync<T>(expression, arg);
            }

            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
            return _page.EvaluateSerializedInFrameAsync<T>(_wkFrame, toEval);
        }

        private Task<IElementHandle> QueryActionAsync(string selector)
            => QueryActionAsync(selector, default);

        private Task<IElementHandle> QueryActionAsync(string selector, bool? strict)
            => StrictSelector.QueryAsync(
                QuerySelectorAsync,
                QuerySelectorAllAsync,
                selector,
                strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors));

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IElementHandle> IFrame.AddScriptTagAsync(FrameAddScriptTagOptions options) => Task.FromResult<IElementHandle>(default!);

        Task<IElementHandle> IFrame.AddStyleTagAsync(FrameAddStyleTagOptions options) => Task.FromResult<IElementHandle>(default!);

        Task IFrame.CheckAsync(string selector, FrameCheckOptions options) => Task.CompletedTask;

        Task IFrame.ClickAsync(string selector, FrameClickOptions options) => Task.CompletedTask;

        Task IFrame.DblClickAsync(string selector, FrameDblClickOptions options) => Task.CompletedTask;

        Task IFrame.DispatchEventAsync(string selector, string type, object eventInit, FrameDispatchEventOptions options) => Task.CompletedTask;

        Task IFrame.DragAndDropAsync(string source, string target, FrameDragAndDropOptions options) => Task.CompletedTask;

        Task<JsonElement?> IFrame.EvalOnSelectorAllAsync(string selector, string expression, object arg) => Task.FromResult<JsonElement?>(default!);

        Task<T> IFrame.EvalOnSelectorAllAsync<T>(string selector, string expression, object arg) => Task.FromResult<T>(default!);

        Task<JsonElement?> IFrame.EvalOnSelectorAsync(string selector, string expression, object arg) => Task.FromResult<JsonElement?>(default!);

        Task<T> IFrame.EvalOnSelectorAsync<T>(string selector, string expression, object arg, FrameEvalOnSelectorOptions options) => Task.FromResult<T>(default!);

        Task IFrame.FillAsync(string selector, string value, FrameFillOptions options) => Task.CompletedTask;

        Task IFrame.FocusAsync(string selector, FrameFocusOptions options) => Task.CompletedTask;

        Task<IElementHandle> IFrame.FrameElementAsync() => Task.FromResult<IElementHandle>(default!);

        IFrameLocator IFrame.FrameLocator(string selector) => null!;

        Task<string> IFrame.GetAttributeAsync(string selector, string name, FrameGetAttributeOptions options) => Task.FromResult<string>(default!);

        ILocator IFrame.GetByAltText(string text, FrameGetByAltTextOptions options) => null!;

        ILocator IFrame.GetByAltText(Regex text, FrameGetByAltTextOptions options) => null!;

        ILocator IFrame.GetByLabel(string text, FrameGetByLabelOptions options) => null!;

        ILocator IFrame.GetByLabel(Regex text, FrameGetByLabelOptions options) => null!;

        ILocator IFrame.GetByPlaceholder(string text, FrameGetByPlaceholderOptions options) => null!;

        ILocator IFrame.GetByPlaceholder(Regex text, FrameGetByPlaceholderOptions options) => null!;

        ILocator IFrame.GetByRole(AriaRole role, FrameGetByRoleOptions options) => null!;

        ILocator IFrame.GetByTestId(string testId) => null!;

        ILocator IFrame.GetByTestId(Regex testId) => null!;

        ILocator IFrame.GetByText(string text, FrameGetByTextOptions options) => null!;

        ILocator IFrame.GetByText(Regex text, FrameGetByTextOptions options) => null!;

        ILocator IFrame.GetByTitle(string text, FrameGetByTitleOptions options) => null!;

        ILocator IFrame.GetByTitle(Regex text, FrameGetByTitleOptions options) => null!;

        Task<IResponse> IFrame.GotoAsync(string url, FrameGotoOptions options) => Task.FromResult<IResponse>(default!);

        Task IFrame.HoverAsync(string selector, FrameHoverOptions options) => Task.CompletedTask;

        Task<string> IFrame.InnerHTMLAsync(string selector, FrameInnerHTMLOptions options) => Task.FromResult<string>(default!);

        Task<string> IFrame.InnerTextAsync(string selector, FrameInnerTextOptions options) => Task.FromResult<string>(default!);

        Task<string> IFrame.InputValueAsync(string selector, FrameInputValueOptions options) => Task.FromResult<string>(default!);

        Task<bool> IFrame.IsCheckedAsync(string selector, FrameIsCheckedOptions options) => Task.FromResult<bool>(default!);

        Task<bool> IFrame.IsDisabledAsync(string selector, FrameIsDisabledOptions options) => Task.FromResult<bool>(default!);

        Task<bool> IFrame.IsEditableAsync(string selector, FrameIsEditableOptions options) => Task.FromResult<bool>(default!);

        Task<bool> IFrame.IsEnabledAsync(string selector, FrameIsEnabledOptions options) => Task.FromResult<bool>(default!);

        Task<bool> IFrame.IsHiddenAsync(string selector, FrameIsHiddenOptions options) => Task.FromResult<bool>(default!);

        Task<bool> IFrame.IsVisibleAsync(string selector, FrameIsVisibleOptions options) => Task.FromResult<bool>(default!);

        ILocator IFrame.Locator(string selector, FrameLocatorOptions options) => null!;

        Task IFrame.PressAsync(string selector, string key, FramePressOptions options) => Task.CompletedTask;

        Task<IElementHandle> IFrame.QuerySelectorAsync(string selector, FrameQuerySelectorOptions options) => Task.FromResult<IElementHandle>(default!);

        Task<IResponse> IFrame.RunAndWaitForNavigationAsync(Func<Task> action, FrameRunAndWaitForNavigationOptions options) => Task.FromResult<IResponse>(default!);

        Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, string values, FrameSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IElementHandle values, FrameSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IEnumerable<string> values, FrameSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, SelectOptionValue values, FrameSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, FrameSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, FrameSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task IFrame.SetCheckedAsync(string selector, bool checkedState, FrameSetCheckedOptions options) => Task.CompletedTask;

        Task IFrame.SetContentAsync(string html, FrameSetContentOptions options) => Task.CompletedTask;

        Task IFrame.SetInputFilesAsync(string selector, string files, FrameSetInputFilesOptions options) => Task.CompletedTask;

        Task IFrame.SetInputFilesAsync(string selector, IEnumerable<string> files, FrameSetInputFilesOptions options) => Task.CompletedTask;

        Task IFrame.SetInputFilesAsync(string selector, FilePayload files, FrameSetInputFilesOptions options) => Task.CompletedTask;

        Task IFrame.SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, FrameSetInputFilesOptions options) => Task.CompletedTask;

        Task IFrame.TapAsync(string selector, FrameTapOptions options) => Task.CompletedTask;

        Task<string> IFrame.TextContentAsync(string selector, FrameTextContentOptions options) => Task.FromResult<string>(default!);

        Task IFrame.TypeAsync(string selector, string text, FrameTypeOptions options) => Task.CompletedTask;

        Task IFrame.UncheckAsync(string selector, FrameUncheckOptions options) => Task.CompletedTask;

        Task<IJSHandle> IFrame.WaitForFunctionAsync(string expression, object arg, FrameWaitForFunctionOptions options) => Task.FromResult<IJSHandle>(default!);

        Task IFrame.WaitForLoadStateAsync(LoadState? state, FrameWaitForLoadStateOptions options)
        {
            FrameWaitForLoadStateOptions o = options;
            return WaitForLoadStateAsync(state ?? LoadState.Load, o?.Timeout);
        }

        Task<IResponse> IFrame.WaitForNavigationAsync(FrameWaitForNavigationOptions options) => Task.FromResult<IResponse>(default!);

        Task<IElementHandle> IFrame.WaitForSelectorAsync(string selector, FrameWaitForSelectorOptions options) => Task.FromResult<IElementHandle>(default!);

        Task IFrame.WaitForURLAsync(string url, FrameWaitForURLOptions options) => Task.CompletedTask;

        Task IFrame.WaitForURLAsync(Regex url, FrameWaitForURLOptions options) => Task.CompletedTask;

        Task IFrame.WaitForURLAsync(Func<string, bool> url, FrameWaitForURLOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
