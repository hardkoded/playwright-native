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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IFrame"/> wrapping <see cref="Frame"/>.</summary>
    internal sealed partial class ChromiumFrame : IFrame
    {
        private readonly Frame _crFrame;
        private readonly IPage _page;

        internal ChromiumFrame(Frame crFrame, IPage page)
        {
            _crFrame = crFrame ?? throw new ArgumentNullException(nameof(crFrame));
            _page = page ?? throw new ArgumentNullException(nameof(page));
        }

        /// <inheritdoc/>
        public IReadOnlyList<IFrame> ChildFrames
        {
            get
            {
                List<IFrame> children = new List<IFrame>();
                if (_page is PlaywrightNative.Page instance)
                {
                    foreach (Frame child in _crFrame.ChildFrames)
                    {
                        children.Add(instance.GetOrCreateFrame(child));
                    }
                }

                return children;
            }
        }

        /// <inheritdoc/>
        public bool IsDetached => _crFrame.IsDetached;

        /// <inheritdoc/>
        public string Name => _crFrame.Name;

        /// <inheritdoc/>
        public IPage Page => _page;

        /// <inheritdoc/>
        public IFrame ParentFrame
        {
            get
            {
                if (_crFrame.ParentFrame == null)
                {
                    return null;
                }

                return _page is PlaywrightNative.Page instance
                    ? instance.GetOrCreateFrame(_crFrame.ParentFrame)
                    : null;
            }
        }

        /// <inheritdoc/>
        public string Url => _crFrame.Url;

        /// <summary>
        /// Gets the wrapped Chromium frame.
        /// </summary>
        internal Frame Frame => _crFrame;

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
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            EvaluateWithArg.ThrowIfDetached(this);
            if (_page is not PlaywrightNative.Page instance)
            {
                return await _page.EvaluateHandleAsync(expression, arg).ConfigureAwait(false);
            }

            if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
            {
                object[] args = EvaluateHandleArg.AsCallFunctionArguments(handleArgs);
                CRJSHandle bound = await instance.CrPage
                    .EvaluateFunctionHandleInFrameAsync(_crFrame, handleFn, args)
                    .ConfigureAwait(false);
                return bound == null
                    ? new ImmediateJSHandle(JsonSerializer.SerializeToElement((object)null))
                    : bound.ToPublicHandle(instance.CrPage);
            }

            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
            CRJSHandle handle = await instance.CrPage.EvaluateHandleInFrameAsync(_crFrame, toEval).ConfigureAwait(false);
            if (handle == null)
            {
                return new ImmediateJSHandle(JsonSerializer.SerializeToElement((object)null));
            }

            return handle.ToPublicHandle(instance.CrPage);
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
        public async Task<IResponse> GoToAsync(string url, WaitUntilState waitUntil = default, float? timeout = default, string referer = default)
        {
            url = NavigationUrl.Resolve(_page?.Context, url);
            if (_page is not PlaywrightNative.Page instance)
            {
                return await _page.GoToAsync(url, waitUntil, timeout, referer).ConfigureAwait(false);
            }

            int timeoutMs = timeout.HasValue ? (int)timeout.Value : 30_000;
            CRResponse captured = await instance.CrPage
                .GoToFrameCapturingResponseAsync(_crFrame, url, waitUntil, timeoutMs, referer)
                .ConfigureAwait(false);
            return captured == null ? null : instance.GetOrCreateDirectResponse(captured);
        }

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
        public async Task<IElementHandle> QuerySelectorAsync(string selector)
        {
            if (_page is not PlaywrightNative.Page instance)
            {
                return await _page.QuerySelectorAsync(selector).ConfigureAwait(false);
            }

            CRElementHandle handle = await instance.CrPage.QuerySelectorInFrameAsync(_crFrame, selector).ConfigureAwait(false);
            return handle == null ? null : new ChromiumElementHandle(handle);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
        {
            if (_page is not PlaywrightNative.Page instance)
            {
                return await _page.QuerySelectorAllAsync(selector).ConfigureAwait(false);
            }

            IReadOnlyList<CRElementHandle> handles = await instance.CrPage
                .QuerySelectorAllInFrameAsync(_crFrame, selector)
                .ConfigureAwait(false);
            List<IElementHandle> result = new(handles.Count);
            foreach (CRElementHandle handle in handles)
            {
                result.Add(new ChromiumElementHandle(handle));
            }

            return result;
        }

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
        {
            if (_page is not PlaywrightNative.Page instance)
            {
                return _page.SetContentAsync(html, timeout, waitUntil);
            }

            int timeoutMs = timeout.HasValue ? (int)timeout.Value : (int)_page.DefaultNavigationTimeout();
            return instance.CrPage.SetContentInFrameAsync(_crFrame, html, waitUntil, timeoutMs);
        }

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
        {
            if (_page is not PlaywrightNative.Page)
            {
                return _page.WaitForNavigationAsync(urlString, urlRegex, urlFunc, timeout, waitUntil);
            }

            return WaitForNavigationHelper.WaitAsync(
                _page,
                _crFrame.WaitForLoadStateAsync,
                urlString,
                urlRegex,
                urlFunc,
                timeout,
                waitUntil,
                frame => ReferenceEquals(frame, this),
                "frame.waitForNavigation");
        }

        /// <inheritdoc/>
        public Task WaitForURLAsync(string urlString, Regex urlRegex, Func<string, bool> urlFunc, float? timeout = default, WaitUntilState waitUntil = default)
            => WaitForUrlHelper.WaitAsync(
                () => Url,
                _crFrame.WaitForLoadStateAsync,
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
        {
            Task wait = _crFrame.WaitForLoadStateAsync(state, timeout, "frame.waitForLoadState");
            if (_page is PlaywrightNative.Page page)
            {
                LoadStateResume.TryResume(page.CrPage.Session);
            }

            return wait;
        }

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
                        object boxed = await EvaluateInOwnFrameAsync<object>(boxedFn, arg).ConfigureAwait(false);
                        return boxed == null ? null : await EvaluateHandleAsync("() => true").ConfigureAwait(false);
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
                    bool truthy = await EvaluateInOwnFrameAsync<bool>("!!(" + wrapped + ")", null).ConfigureAwait(false);
                    if (!truthy)
                    {
                        return null;
                    }

                    IJSHandle handle = await EvaluateHandleAsync(wrapped).ConfigureAwait(false);
                    return handle is ImmediateJSHandle ? null : handle;
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

        private async Task<T> EvaluatePreparedInFrameAsync<T>(string handleFn, object[] handleArgs)
        {
            object[] args = EvaluateHandleArg.AsCallFunctionArguments(handleArgs);
            string serializedFn = EvaluateHandleArg.WithSerializedHandleResult(handleFn);
            if (_page is PlaywrightNative.Page handle)
            {
                try
                {
                    CRExecutionContext context = await handle.CrPage.WaitForFrameExecutionContextAsync(_crFrame).ConfigureAwait(false);
                    JsonElement? wrapped = await context.EvaluateFunctionAsync(serializedFn, args).ConfigureAwait(false);
                    return EvaluateSerialization.ParseRemote<T>(wrapped);
                }
                catch (PlaywrightNativeException ex)
                {
                    throw EvaluateSerialization.RewriteException(ex, frameEvaluate: true);
                }
            }

            await EvaluateHandleArg.StashRemoteHandlesAsync(handleArgs).ConfigureAwait(false);
            string expression = EvaluateHandleArg.PreparedExpression(handleFn, handleArgs);
            return await _page.EvaluateAsync<T>(expression).ConfigureAwait(false);
        }

        private async Task<T> EvaluateSerializedInFrameAsync<T>(Page instance, string expression)
        {
            try
            {
                CRExecutionContext context = await instance.CrPage.WaitForFrameExecutionContextAsync(_crFrame).ConfigureAwait(false);
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
                throw EvaluateSerialization.RewriteException(ex, frameEvaluate: true);
            }
        }

        private Task<T> EvaluateInOwnFrameAsync<T>(string expression, object arg)
        {
            EvaluateWithArg.ThrowIfDetached(this);
            if (EvaluateHandleArg.TryPrepareHandleCall(expression, arg, out string handleFn, out object[] handleArgs))
            {
                return EvaluatePreparedInFrameAsync<T>(handleFn, handleArgs);
            }

            if (EvaluateWithArg.IsHandle(arg))
            {
                return _page.EvaluateAsync<T>(expression, arg);
            }

            string toEval = arg == null ? EvaluateWithArg.InvokeIfFunction(expression) : EvaluateWithArg.Wrap(expression, arg);
            if (_page is PlaywrightNative.Page page)
            {
                return EvaluateSerializedInFrameAsync<T>(page, toEval);
            }

            return _page.EvaluateAsync<T>(toEval);
        }

        private Task DispatchEventInternalAsync(string selector, string type, object eventInit, float? timeout, bool? strict)
            => DispatchEventAction.RunAsync(
                EvaluateDispatchBoolAsync,
                selector,
                type,
                eventInit,
                timeout,
                strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors),
                "frame.dispatchEvent");

        private Task<bool> EvaluateDispatchBoolAsync(string script, object arg)
            => EvaluateInOwnFrameAsync<bool>(script, arg);

        private Task<IElementHandle> QueryActionAsync(string selector)
            => QueryActionAsync(selector, default);

        private Task<IElementHandle> QueryActionAsync(string selector, bool? strict)
            => StrictSelector.QueryAsync(
                QuerySelectorAsync,
                QuerySelectorAllAsync,
                selector,
                strict ?? (_page.Context is IHasStrictSelectors s && s.StrictSelectors));

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IElementHandle> IFrame.AddScriptTagAsync(FrameAddScriptTagOptions options)
            => _page.AddScriptTagAsync(new PageAddScriptTagOptions
            {
                Url = options?.Url,
                Path = options?.Path,
                Content = options?.Content,
                Type = options?.Type,
            });

        Task<IElementHandle> IFrame.AddStyleTagAsync(FrameAddStyleTagOptions options)
            => _page.AddStyleTagAsync(new PageAddStyleTagOptions
            {
                Url = options?.Url,
                Path = options?.Path,
                Content = options?.Content,
            });

        Task IFrame.CheckAsync(string selector, FrameCheckOptions options)
            => CheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IFrame.ClickAsync(string selector, FrameClickOptions options)
            => ClickAsync(selector, options?.Button ?? default, options?.ClickCount, options?.Delay, options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, null, options?.Strict);

        Task IFrame.DblClickAsync(string selector, FrameDblClickOptions options)
            => DblClickAsync(selector, options?.Button ?? default, options?.Delay, options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IFrame.DispatchEventAsync(string selector, string type, object eventInit, FrameDispatchEventOptions options)
            => DispatchEventInternalAsync(selector, type, eventInit, options?.Timeout, options?.Strict);

        Task IFrame.DragAndDropAsync(string source, string target, FrameDragAndDropOptions options)
            => DragAndDropHelper.RunAsync(
                this,
                source,
                target,
                options?.SourcePosition == null ? null : new Position { X = options.SourcePosition.X, Y = options.SourcePosition.Y },
                options?.TargetPosition == null ? null : new Position { X = options.TargetPosition.X, Y = options.TargetPosition.Y },
                options?.Force,
                options?.Timeout,
                options?.Trial,
                options?.Steps,
                ActionScrollBridge.FromScrollOption(options?.Scroll),
                options?.Strict);

        Task<JsonElement?> IFrame.EvalOnSelectorAllAsync(string selector, string expression, object arg)
            => EvalOnSelector.OnArrayAsync<JsonElement?>(
                EvaluateHandleAsync(EvalOnSelector.DocumentQuerySelectorAllExpression(selector)),
                expression,
                arg);

        Task<T> IFrame.EvalOnSelectorAllAsync<T>(string selector, string expression, object arg)
            => EvalOnSelector.OnArrayAsync<T>(
                EvaluateHandleAsync(EvalOnSelector.DocumentQuerySelectorAllExpression(selector)),
                expression,
                arg);

        Task<JsonElement?> IFrame.EvalOnSelectorAsync(string selector, string expression, object arg)
            => EvalOnSelector.OnHandleAsync<JsonElement?>(QuerySelectorAsync(selector), selector, expression, arg, "frame.$eval");

        Task<T> IFrame.EvalOnSelectorAsync<T>(string selector, string expression, object arg, FrameEvalOnSelectorOptions options)
            => EvalOnSelector.OnHandleAsync<T>(
                QueryActionAsync(selector, options?.Strict),
                selector,
                expression,
                arg,
                "frame.$eval");

        Task IFrame.FillAsync(string selector, string value, FrameFillOptions options)
            => FillAsync(selector, value, options?.NoWaitAfter, options?.Timeout, options?.Force, default, options?.Strict);

        Task IFrame.FocusAsync(string selector, FrameFocusOptions options)
            => FocusAsync(selector, options?.Timeout, default, options?.Strict);

        Task<IElementHandle> IFrame.FrameElementAsync() => FrameElementHelper.ResolveAsync(this);

        IFrameLocator IFrame.FrameLocator(string selector) => new FrameLocator(this, selector);

        Task<string> IFrame.GetAttributeAsync(string selector, string name, FrameGetAttributeOptions options)
            => GetAttributeAsync(selector, name, options?.Timeout, options?.Strict);

        ILocator IFrame.GetByAltText(string text, FrameGetByAltTextOptions options)
            => Locator.FromScript(this, GetByAllScript.FindAllByAttribute, "alt", text, options?.Exact ?? false);

        ILocator IFrame.GetByAltText(Regex text, FrameGetByAltTextOptions options)
            => Locator.FromScript(
                this,
                GetByAllScript.FindAllByAttributeRegex,
                "alt",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IFrame.GetByLabel(string text, FrameGetByLabelOptions options)
            => Locator.FromScript(this, GetByAllScript.FindAllByLabel, text, options?.Exact ?? false);

        ILocator IFrame.GetByLabel(Regex text, FrameGetByLabelOptions options)
            => Locator.FromScript(
                this,
                GetByAllScript.FindAllByLabelRegex,
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IFrame.GetByPlaceholder(string text, FrameGetByPlaceholderOptions options)
            => Locator.FromScript(this, GetByAllScript.FindAllByAttribute, "placeholder", text, options?.Exact ?? false);

        ILocator IFrame.GetByPlaceholder(Regex text, FrameGetByPlaceholderOptions options)
            => Locator.FromScript(
                this,
                GetByAllScript.FindAllByAttributeRegex,
                "placeholder",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IFrame.GetByRole(AriaRole role, FrameGetByRoleOptions options)
            => new Locator(this, RoleSelector.Build(
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

        ILocator IFrame.GetByTestId(string testId) => new Locator(this, GetBySelectorScript.TestIdSelector(testId));

        ILocator IFrame.GetByTestId(Regex testId)
            => Locator.FromScript(
                this,
                GetByAllScript.FindAllByAttributeRegex,
                GetBySelectorScript.TestIdAttributeName(),
                GetByAllScript.Pattern(testId),
                GetByAllScript.Flags(testId));

        ILocator IFrame.GetByText(string text, FrameGetByTextOptions options)
            => Locator.FromScript(this, GetByAllScript.FindAllByText, text, options?.Exact ?? false);

        ILocator IFrame.GetByText(Regex text, FrameGetByTextOptions options)
            => Locator.FromScript(
                this,
                GetByAllScript.FindAllByTextRegex,
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        ILocator IFrame.GetByTitle(string text, FrameGetByTitleOptions options)
            => Locator.FromScript(this, GetByAllScript.FindAllByAttribute, "title", text, options?.Exact ?? false);

        ILocator IFrame.GetByTitle(Regex text, FrameGetByTitleOptions options)
            => Locator.FromScript(
                this,
                GetByAllScript.FindAllByAttributeRegex,
                "title",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text));

        Task<IResponse> IFrame.GotoAsync(string url, FrameGotoOptions options)
            => GoToAsync(url, options?.WaitUntil ?? default, options?.Timeout, options?.Referer);

        Task IFrame.HoverAsync(string selector, FrameHoverOptions options)
            => HoverAsync(selector, options?.Position, options?.Modifiers, options?.Force, options?.Timeout, options?.Trial, default, options?.Strict);

        Task<string> IFrame.InnerHTMLAsync(string selector, FrameInnerHTMLOptions options)
            => InnerHTMLAsync(selector, options?.Timeout, options?.Strict);

        Task<string> IFrame.InnerTextAsync(string selector, FrameInnerTextOptions options)
            => InnerTextAsync(selector, options?.Timeout, options?.Strict);

        Task<string> IFrame.InputValueAsync(string selector, FrameInputValueOptions options)
            => EvalOnSelector.OnHandleAsync<string>(
                QueryActionAsync(selector, options?.Strict),
                selector,
                ElementStateScript.InputValueFunction,
                null,
                "frame.inputValue");

        Task<bool> IFrame.IsCheckedAsync(string selector, FrameIsCheckedOptions options)
            => IsCheckedAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IFrame.IsDisabledAsync(string selector, FrameIsDisabledOptions options)
            => IsDisabledAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IFrame.IsEditableAsync(string selector, FrameIsEditableOptions options)
            => IsEditableAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IFrame.IsEnabledAsync(string selector, FrameIsEnabledOptions options)
            => IsEnabledAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IFrame.IsHiddenAsync(string selector, FrameIsHiddenOptions options)
            => IsHiddenAsync(selector, options?.Timeout, options?.Strict);

        Task<bool> IFrame.IsVisibleAsync(string selector, FrameIsVisibleOptions options)
            => IsVisibleAsync(selector, options?.Timeout, options?.Strict);

        ILocator IFrame.Locator(string selector, FrameLocatorOptions options)
        {
            ILocator result = new Locator(this, selector);
            options ??= new FrameLocatorOptions();
            return SelectorQuery.ApplyOptions(
                result,
                options.Has,
                options.HasText ?? options.HasTextString,
                options.HasTextRegex,
                options.HasNot,
                options.HasNotText ?? options.HasNotTextString,
                options.HasNotTextRegex);
        }

        Task IFrame.PressAsync(string selector, string key, FramePressOptions options)
            => PressAsync(selector, key, options?.Delay, options?.NoWaitAfter, options?.Timeout, null, default, options?.Strict);

        Task<IElementHandle> IFrame.QuerySelectorAsync(string selector, FrameQuerySelectorOptions options) => QuerySelectorAsync(selector);

        async Task<IResponse> IFrame.RunAndWaitForNavigationAsync(Func<Task> action, FrameRunAndWaitForNavigationOptions options)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Task<IResponse> waitTask = ((IFrame)this).WaitForNavigationAsync(new FrameWaitForNavigationOptions
            {
                Url = options?.Url ?? options?.UrlString,
                UrlRegex = options?.UrlRegex,
                UrlFunc = options?.UrlFunc,
                Timeout = options?.Timeout,
                WaitUntil = options?.WaitUntil,
            });
            Task actionTask = action();
            IResponse response = await waitTask.ConfigureAwait(false);
            await actionTask.ConfigureAwait(false);
            return response;
        }

        async Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, string values, FrameSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Force, options?.Strict).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IElementHandle values, FrameSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IEnumerable<string> values, FrameSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, SelectOptionValue values, FrameSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IEnumerable<IElementHandle> values, FrameSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Strict, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> IFrame.SelectOptionAsync(string selector, IEnumerable<SelectOptionValue> values, FrameSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(selector, values, options?.NoWaitAfter, options?.Timeout, options?.Force, default, options?.Strict).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        Task IFrame.SetCheckedAsync(string selector, bool checkedState, FrameSetCheckedOptions options)
            => checkedState
                ? CheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict)
                : UncheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task IFrame.SetContentAsync(string html, FrameSetContentOptions options)
            => SetContentAsync(html, options?.Timeout, options?.WaitUntil ?? default);

        Task IFrame.SetInputFilesAsync(string selector, string files, FrameSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, options?.Strict);

        Task IFrame.SetInputFilesAsync(string selector, IEnumerable<string> files, FrameSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, options?.Strict);

        Task IFrame.SetInputFilesAsync(string selector, FilePayload files, FrameSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, options?.Strict);

        Task IFrame.SetInputFilesAsync(string selector, IEnumerable<FilePayload> files, FrameSetInputFilesOptions options)
            => SetInputFilesAsync(selector, files, options?.NoWaitAfter, options?.Timeout, null, default, options?.Strict);

        Task IFrame.TapAsync(string selector, FrameTapOptions options)
            => TapAsync(selector, options?.Position, options?.Modifiers, options?.NoWaitAfter, options?.Force, options?.Timeout, options?.Trial, default, options?.Strict);

        Task<string> IFrame.TextContentAsync(string selector, FrameTextContentOptions options)
            => TextContentAsync(selector, options?.Timeout, options?.Strict);

        Task IFrame.TypeAsync(string selector, string text, FrameTypeOptions options)
            => TypeAsync(selector, text, options?.Delay, options?.NoWaitAfter, options?.Timeout, null, default, options?.Strict);

        Task IFrame.UncheckAsync(string selector, FrameUncheckOptions options)
            => UncheckAsync(selector, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial, default, options?.Strict);

        Task<IJSHandle> IFrame.WaitForFunctionAsync(string expression, object arg, FrameWaitForFunctionOptions options)
            => WaitForFunctionAsync(expression, arg, options?.PollingInterval, options?.Timeout);

        Task IFrame.WaitForLoadStateAsync(LoadState? state, FrameWaitForLoadStateOptions options)
        {
            FrameWaitForLoadStateOptions o = options;
            return WaitForLoadStateAsync(state ?? LoadState.Load, o?.Timeout);
        }

        Task<IResponse> IFrame.WaitForNavigationAsync(FrameWaitForNavigationOptions options)
            => WaitForNavigationAsync(
                options?.Url ?? options?.UrlString,
                options?.UrlRegex,
                options?.UrlFunc,
                options?.Timeout,
                options?.WaitUntil ?? default);

        Task<IElementHandle> IFrame.WaitForSelectorAsync(string selector, FrameWaitForSelectorOptions options)
            => WaitForSelectorAsync(selector, options?.State ?? WaitForSelectorState.Visible, options?.Timeout, options?.Strict);

        Task IFrame.WaitForURLAsync(string url, FrameWaitForURLOptions options)
            => WaitForURLAsync(url, null, null, options?.Timeout, options?.WaitUntil ?? default);

        Task IFrame.WaitForURLAsync(Regex url, FrameWaitForURLOptions options)
            => WaitForURLAsync(null, url, null, options?.Timeout, options?.WaitUntil ?? default);

        Task IFrame.WaitForURLAsync(Func<string, bool> url, FrameWaitForURLOptions options)
            => WaitForURLAsync(null, null, url, options?.Timeout, options?.WaitUntil ?? default);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
