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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Compat;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Handle to a DOM node in WebKit, implementing <see cref="IElementHandle"/> directly.
    /// Exposes element read/state operations (text, attribute, visibility, bounding box) and
    /// interaction operations (click/fill/type/check/select/etc.). Produced by
    /// <c>WKPage.QuerySelectorAsync</c>. Pointer actions use a simple bounding-box-center click
    /// point — no content-quad geometry, hit-testing, or stability waits (sufficient for the
    /// element shapes exercised today; complex layouts would need the full upstream machinery).
    /// </summary>
    internal sealed partial class WKElementHandle : WKJSHandle, IElementHandle
    {
        private readonly WKPage _page;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKElementHandle"/> class.
        /// </summary>
        /// <param name="context">The execution context that owns the remote object.</param>
        /// <param name="objectId">The WIP remote object id.</param>
        /// <param name="page">The owning page, used for pointer/keyboard input during interactions.</param>
        /// <param name="preview">Initial official preview. Defaults to <c>JSHandle@node</c>.</param>
        public WKElementHandle(WKExecutionContext context, string objectId, WKPage page, string preview = null)
            : base(context, objectId, page, preview ?? "JSHandle@node")
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _ = InitializePreviewAsync();
        }

        /// <inheritdoc/>
        public override IElementHandle AsElement() => this;

        /// <inheritdoc/>
        public async Task<ElementHandleBoundingBoxResult> BoundingBoxAsync()
        {
            EnsureNotDisposed();

            // getBoundingClientRect forces layout, works for SVG, and matches
            // inline overflow. Parent-iframe offset makes nested-frame boxes
            // page-relative (official ElementHandle.boundingBox).
            float[] rect = await EvaluateFunctionAsync<float[]>(BoundingBoxHelper.ClientRectFunction).ConfigureAwait(false);
            if (rect == null || rect.Length < 4)
            {
                return null;
            }

            IFrame owner = await OwnerFrameAsync().ConfigureAwait(false);
            (double offsetX, double offsetY) = await BoundingBoxHelper.OwnerFrameOffsetAsync(owner).ConfigureAwait(false);

            return new ElementHandleBoundingBoxResult
            {
                X = (float)(rect[0] + offsetX),
                Y = (float)(rect[1] + offsetY),
                Width = rect[2],
                Height = rect[3],
            };
        }

        /// <inheritdoc/>
        public async Task CheckAsync(Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            if (ActionTrial.IsTrial(trial))
            {
                return;
            }

            await SetCheckedAsync(true, position, scroll, force).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ClickAsync(MouseButton button = default, int? clickCount = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default)
        {
            await ClickAction.RunPointerAsync(_page, async () =>
            {
                await ClickAction.PerformClickAsync(
                    this,
                    force,
                    timeout,
                    trial,
                    position,
                    scroll,
                    async point =>
                    {
                        await _page.RunWithSignalsAsync(
                            noWaitAfter != true,
                            timeout,
                            () => ClickAction.RunModifiersAsync(
                                modifiers,
                                ClickAction.HeldKeys(_page.InputKeyboard.PressedModifiers),
                                _page.Keyboard.DownAsync,
                                _page.Keyboard.UpAsync,
                                () => _page.Mouse.MoveAsync((float)point[0], (float)point[1], steps))).ConfigureAwait(false);
                    },
                    async () =>
                    {
                        await _page.RunWithSignalsAsync(
                            noWaitAfter != true,
                            timeout,
                            () => ClickAction.RunModifiersAsync(
                                modifiers,
                                ClickAction.HeldKeys(_page.InputKeyboard.PressedModifiers),
                                _page.Keyboard.DownAsync,
                                _page.Keyboard.UpAsync,
                                async () =>
                                {
                                    int count = clickCount ?? 1;
                                    for (int i = 1; i <= count; i++)
                                    {
                                        await _page.Mouse.DownAsync(button, i).ConfigureAwait(false);
                                        if (delay.HasValue && delay.Value > 0)
                                        {
                                            await Task.Delay((int)delay.Value).ConfigureAwait(false);
                                        }

                                        await _page.Mouse.UpAsync(button, i).ConfigureAwait(false);
                                    }
                                })).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DblClickAsync(MouseButton button = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default)
        {
            await ClickAction.RunPointerAsync(_page, async () =>
            {
                await ClickAction.PrepareAsync(this, force, timeout, trial, position, scroll).ConfigureAwait(false);
                if (ActionTrial.IsTrial(trial))
                {
                    return;
                }

                double[] point = await ClickAction.PointAsync(this, position, force).ConfigureAwait(false);
                await ClickAction.RunModifiersAsync(
                    modifiers,
                    ClickAction.HeldKeys(_page.InputKeyboard.PressedModifiers),
                    _page.Keyboard.DownAsync,
                    _page.Keyboard.UpAsync,
                    () => _page.Mouse.DblClickAsync((float)point[0], (float)point[1], button, delay, steps)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task FillAsync(string value, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            EnsureNotDisposed();
            await FillAction.WaitUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);

            // Direct DOM fill: set the value and fire input/change. Mirrors the injected-script
            // fill for input/textarea/contenteditable without the keyboard round-trip.
            await EvaluateFunctionAsync<bool>(
                ElementStateScript.FillFunction,
                value,
                scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task FocusAsync()
            => FocusAsync(timeout: null);

        /// <inheritdoc/>
        public async Task FocusAsync(float? timeout, ActionScroll scroll = default)
        {
            EnsureNotDisposed();

            // Official page.focus waits for attached, then focusNode — not a non-empty
            // bounding box. Zero-height tabindex elements are focusable; hidden
            // (display:none) nodes are not and still wait for visibility.
            bool focusable = await EvaluateFunctionAsync<bool>(ElementStateScript.IsFocusableAreaFunction)
                .ConfigureAwait(false);
            if (!focusable)
            {
                await WaitForElementStateHelper.WaitAsync(this, ElementState.Visible, timeout).ConfigureAwait(false);
            }

            bool preventScroll = scroll == ActionScroll.None;
            await EvaluateFunctionAsync<bool>(ElementStateScript.FocusFunction, preventScroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<string> GetAttributeAsync(string name)
        {
            EnsureNotDisposed();
            return EvaluateFunctionAsync<string>("(el, n) => el.getAttribute(n)", name);
        }

        /// <inheritdoc/>
        public async Task HoverAsync(Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
        {
            if (force != true && LocatorHandlers.ShouldHover(_page))
            {
                await ClickAction.PrepareAsync(this, force, timeout, trial, position, scroll).ConfigureAwait(false);
            }
            else
            {
                await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            }

            if (ActionTrial.IsTrial(trial))
            {
                return;
            }

            await ClickAction.EnsureInViewportWhenNoScrollAsync(this, scroll).ConfigureAwait(false);

            double[] point = await ClickPointAsync(position, scroll).ConfigureAwait(false);
            await ActionModifiers.RunAsync(
                modifiers,
                _page.Keyboard.DownAsync,
                _page.Keyboard.UpAsync,
                () => _page.Mouse.MoveAsync((float)point[0], (float)point[1])).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<string> InnerHTMLAsync()
        {
            EnsureNotDisposed();
            return EvaluateFunctionAsync<string>("el => el.innerHTML");
        }

        /// <inheritdoc/>
        public Task<string> InnerTextAsync()
        {
            EnsureNotDisposed();
            return EvaluateFunctionAsync<string>(ElementStateScript.InnerTextFunction);
        }

        /// <inheritdoc/>
        public Task<bool> IsCheckedAsync()
        {
            EnsureNotDisposed();
            return EvaluateFunctionAsync<bool>(ElementStateScript.IsCheckedFunction);
        }

        /// <inheritdoc/>
        public Task<bool> IsDisabledAsync()
            => EvaluateFunctionAsync<bool>(ElementStateScript.IsDisabledFunction);

        /// <inheritdoc/>
        public Task<bool> IsEditableAsync()
            => EvaluateFunctionAsync<bool>(ElementStateScript.IsEditableFunction);

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync()
            => EvaluateFunctionAsync<bool>(ElementStateScript.IsEnabledFunction);

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync()
            => !await IsVisibleAsync().ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<bool> IsVisibleAsync()
        {
            EnsureNotDisposed();

            // Mirrors the Chromium computed-style + getBoundingClientRect visibility heuristic.
            return EvaluateFunctionAsync<bool>(DomVisibility.IsVisibleFunction);
        }

        /// <inheritdoc/>
        public async Task PressAsync(string key, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            bool preventScroll = scroll == ActionScroll.None;
            string snapshot = null;
            if (preventScroll)
            {
                snapshot = await EvaluateFunctionAsync<string>(ElementStateScript.CaptureAncestorScrollsFunction).ConfigureAwait(false);
            }

            await EvaluateFunctionAsync<bool>(ElementStateScript.FocusForTypeFunction, preventScroll).ConfigureAwait(false);
            await _page.Keyboard.PressAsync(key, delay).ConfigureAwait(false);
            if (preventScroll && snapshot != null)
            {
                await EvaluateFunctionAsync<bool>(ElementStateScript.RestoreAncestorScrollsFunction, snapshot).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<IElementHandle> QuerySelectorAsync(string selector)
        {
            EnsureNotDisposed();
            if (FrameSelector.ContainsControl(selector))
            {
                IFrame owner = await OwnerFrameAsync().ConfigureAwait(false);
                IReadOnlyList<IElementHandle> matches = await FrameSelector.QueryAllAsync(owner, this, selector).ConfigureAwait(false);
                return matches.Count > 0 ? matches[0] : null;
            }

            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                JsonElement? custom = await Context
                    .EvaluateHandleOnHandleAsync(ObjectId, call.ElementQueryFunction)
                    .ConfigureAwait(false);
                return _page.WrapElement(Context, custom);
            }

            JsonElement? handleValue = await Context
                .EvaluateHandleOnHandleAsync(ObjectId, "(el, sel) => el.querySelector(sel)", selector)
                .ConfigureAwait(false);
            return _page.WrapElement(Context, handleValue);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
        {
            EnsureNotDisposed();
            if (FrameSelector.ContainsControl(selector))
            {
                IFrame owner = await OwnerFrameAsync().ConfigureAwait(false);
                return await FrameSelector.QueryAllAsync(owner, this, selector).ConfigureAwait(false);
            }

            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                JsonElement? customArray = await Context
                    .EvaluateHandleOnHandleAsync(ObjectId, call.ElementQueryAllFunction)
                    .ConfigureAwait(false);
                return await _page.UnwrapElementArrayAsync(Context, customArray).ConfigureAwait(false);
            }

            JsonElement? arrayRemote = await Context
                .EvaluateHandleOnHandleAsync(ObjectId, "(el, sel) => Array.from(el.querySelectorAll(sel))", selector)
                .ConfigureAwait(false);
            return await _page.UnwrapElementArrayAsync(Context, arrayRemote).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IFrame> ContentFrameAsync()
        {
            EnsureNotDisposed();
            (string contentFrameId, _) = await _page.DescribeNodeFrameIdsAsync(ObjectId).ConfigureAwait(false);
            return _page.TryGetFrameById(contentFrameId);
        }

        /// <inheritdoc/>
        public async Task<IFrame> OwnerFrameAsync()
        {
            EnsureNotDisposed();
            (_, string ownerFrameId) = await _page.DescribeNodeFrameIdsAsync(ObjectId).ConfigureAwait(false);
            return _page.ResolveOwnerFrameById(ownerFrameId);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IEnumerable<SelectOptionValue> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => SelectOptionFromJsonAsync(SelectOptionPayload.FromOptions(values), timeout, force, scroll);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionFromJsonAsync(SelectOptionPayload.FromValues(values == null ? null : new[] { values }), timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IEnumerable<string> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionFromJsonAsync(SelectOptionPayload.FromValues(values), timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IElementHandle values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionFromHandlesAsync(values == null ? Array.Empty<IElementHandle>() : new[] { values }, timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IEnumerable<IElementHandle> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionFromHandlesAsync(values, timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(SelectOptionValue values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionFromJsonAsync(SelectOptionPayload.FromOptions(values == null ? null : new[] { values }), timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(params string[] values)
            => SelectOptionFromJsonAsync(SelectOptionPayload.FromValues(values), timeout: null);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(params SelectOptionValue[] values)
            => SelectOptionFromJsonAsync(SelectOptionPayload.FromOptions(values), timeout: null);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(params IElementHandle[] values)
            => SelectOptionFromHandlesAsync(values, timeout: null);

        /// <inheritdoc/>
        public async Task SelectTextAsync(float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            try
            {
                await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(ex.Message + " element is not visible", ex);
            }

            await EvaluateFunctionAsync<bool>(ElementStateScript.SelectTextFunction, scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task ScrollIntoViewIfNeededAsync(float? timeout = default)
        {
            EnsureNotDisposed();
            return ScrollIntoViewIfNeededAction.RunAsync(this, ScrollRectIntoViewIfNeededAsync, timeout);
        }

        /// <inheritdoc/>
        public async Task<byte[]> ScreenshotAsync(
            string path = default,
            ScreenshotType type = default,
            int? quality = default,
            bool? omitBackground = default,
            float? timeout = default,
            string scale = default,
            string animations = default,
            string caret = default,
            string style = default,
            IEnumerable<ILocator> mask = default,
            string maskColor = default)
        {
            return await ElementScreenshot.CaptureAsync(
                this,
                _page,
                path,
                type,
                quality,
                omitBackground,
                timeout,
                scale,
                animations,
                caret,
                style,
                mask,
                maskColor).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(IEnumerable<FilePayload> files, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => SetInputFilesFromJsonAsync(FilePayloadHelper.ToJson(files), timeout, force, scroll);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string files, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SetInputFilesFromPathsAsync(new[] { files }, timeout);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(IEnumerable<string> files, bool? noWaitAfter = default, float? timeout = default)
            => SetInputFilesFromPathsAsync(files, timeout);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(FilePayload files, bool? noWaitAfter = default, float? timeout = default)
            => SetInputFilesFromJsonAsync(FilePayloadHelper.ToJson(files == null ? null : new[] { files }), timeout);

        /// <inheritdoc/>
        public async Task TapAsync(Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            await TapSupport.WithTrialInterceptorAsync(this, trial, async () =>
            {
                double[] point = await ClickPointAsync(position, scroll).ConfigureAwait(false);
                await ActionModifiers.RunAsync(
                    modifiers,
                    _page.Keyboard.DownAsync,
                    _page.Keyboard.UpAsync,
                    () => _page.Touchscreen.TapAsync((float)point[0], (float)point[1])).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<string> TextContentAsync()
        {
            EnsureNotDisposed();
            return EvaluateFunctionAsync<string>("el => el.textContent");
        }

        /// <inheritdoc/>
        public async Task TypeAsync(string text, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            bool preventScroll = scroll == ActionScroll.None;
            string snapshot = null;
            if (preventScroll)
            {
                snapshot = await EvaluateFunctionAsync<string>(ElementStateScript.CaptureAncestorScrollsFunction).ConfigureAwait(false);
            }

            await EvaluateFunctionAsync<bool>(ElementStateScript.FocusForTypeFunction, preventScroll).ConfigureAwait(false);
            await _page.Keyboard.TypeAsync(text, delay).ConfigureAwait(false);
            if (preventScroll && snapshot != null)
            {
                await EvaluateFunctionAsync<bool>(ElementStateScript.RestoreAncestorScrollsFunction, snapshot).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task UncheckAsync(Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            if (ActionTrial.IsTrial(trial))
            {
                return;
            }

            await SetCheckedAsync(false, position, scroll, force).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets filesystem paths via WebKit <c>DOM.setInputFiles</c> after
        /// <c>Playwright.grantFileReadAccess</c>. No visibility wait — file
        /// chooser inputs may be detached.
        /// </summary>
        /// <param name="paths">Local filesystem paths. Empty clears the input.</param>
        /// <returns>A task that completes when the protocol commands finish.</returns>
        internal async Task SetFileInputFilesFromPathsAsync(IReadOnlyList<string> paths)
        {
            EnsureNotDisposed();
            WKTargetSession target = _page.CurrentTargetSession
                ?? throw new PlaywrightNativeException("WebKit target session is not available.");

            string[] pathArray;
            if (paths == null || paths.Count == 0)
            {
                pathArray = Array.Empty<string>();
            }
            else
            {
                pathArray = new string[paths.Count];
                for (int i = 0; i < paths.Count; i++)
                {
                    pathArray[i] = paths[i];
                }
            }

            await Task.WhenAll(
                _page.Browser.Session.SendAsync(
                    "Playwright.grantFileReadAccess",
                    new { pageProxyId = _page.PageProxyId, paths = pathArray }),
                target.SendAsync(
                    "DOM.setInputFiles",
                    new { objectId = ObjectId, paths = pathArray })).ConfigureAwait(false);
        }

        private async Task<double[]> ClickPointAsync(Position position = null, ActionScroll scroll = default)
        {
            EnsureNotDisposed();

            if (await ClickAction.IsTextNodeAsync(this).ConfigureAwait(false))
            {
                return await ClickAction.PointAsync(this, position).ConfigureAwait(false);
            }

            // Simple click point: optionally scroll the element to the viewport
            // center and click the center of its bounding box, or an explicit
            // offset from the top-left. No content-quad geometry or hit-testing.
            bool shouldScroll = scroll != ActionScroll.None;
            double[] point = position == null
                ? await EvaluateFunctionAsync<double[]>(
                    @"(el, shouldScroll) => {
                        if (shouldScroll) el.scrollIntoView({ block: 'center', inline: 'center' });
                        const r = el.getBoundingClientRect();
                        return [r.left + r.width / 2, r.top + r.height / 2];
                    }",
                    shouldScroll).ConfigureAwait(false)
                : await EvaluateFunctionAsync<double[]>(
                    @"(el, ox, oy, shouldScroll) => {
                        if (shouldScroll) el.scrollIntoView({ block: 'center', inline: 'center' });
                        const r = el.getBoundingClientRect();
                        return [r.left + ox, r.top + oy];
                    }",
                    position.X,
                    position.Y,
                    shouldScroll).ConfigureAwait(false);

            if (point == null || point.Length < 2)
            {
                throw new PlaywrightNativeException("Unable to compute a click point for the element.");
            }

            return point;
        }

        private async Task SetCheckedAsync(bool check, Position position = null, ActionScroll scroll = default, bool? force = default)
        {
            if (await IsCheckedAsync().ConfigureAwait(false) == check)
            {
                return;
            }

            if (!check && await EvaluateFunctionAsync<bool>(ElementStateScript.IsNativeRadioFunction).ConfigureAwait(false))
            {
                throw new PlaywrightNativeException("Cannot uncheck radio button");
            }

            // Prefer the full click pipeline (scroll, hit-test, modifiers). WebKit's
            // pointer path still misses some checkbox/label hit targets on CI, so fall
            // back to the injected DOM click used by ElementStateScript.CheckFunction.
            // Pass force through so force:true skips hit-testing on hidden inputs.
            await ClickAsync(position: position, force: force, scroll: scroll).ConfigureAwait(false);

            if (await IsCheckedAsync().ConfigureAwait(false) == check)
            {
                return;
            }

            await EvaluateFunctionAsync<bool>(
                check ? ElementStateScript.CheckFunction : ElementStateScript.UncheckFunction)
                .ConfigureAwait(false);

            if (await IsCheckedAsync().ConfigureAwait(false) != check)
            {
                throw new PlaywrightNativeException("Clicking the checkbox did not change its state.");
            }
        }

        private async Task<IReadOnlyCollection<string>> SelectOptionFromHandlesAsync(IEnumerable<IElementHandle> handles, float? timeout = default, bool? force = default)
        {
            List<string> values = new List<string>();
            if (handles != null)
            {
                foreach (IElementHandle handle in handles)
                {
                    if (handle == null)
                    {
                        continue;
                    }

                    if (handle is not WKElementHandle element)
                    {
                        throw new ArgumentException(
                            "SelectOptionAsync requires WKElementHandle instances when running against WebKit.",
                            nameof(handles));
                    }

                    string value = await element.EvaluateFunctionAsync<string>("el => el.value").ConfigureAwait(false);
                    if (value != null)
                    {
                        values.Add(value);
                    }
                }
            }

            return await SelectOptionFromJsonAsync(SelectOptionPayload.FromValues(values, matchLabel: false), timeout, force).ConfigureAwait(false);
        }

        private async Task<string> ScrollRectIntoViewIfNeededAsync()
        {
            WKTargetSession target = _page.CurrentTargetSession
                ?? throw new PlaywrightNativeException("WebKit target session is not available.");

            try
            {
                await target.SendAsync(
                    "DOM.scrollIntoViewIfNeeded",
                    new { objectId = ObjectId }).ConfigureAwait(false);
                return ScrollIntoViewIfNeededAction.ResultDone;
            }
            catch (PlaywrightNativeException ex)
            {
                string mapped = ScrollIntoViewIfNeededAction.MapProtocolError(ex.Message);
                if (mapped != null)
                {
                    return mapped;
                }

                throw;
            }
        }

        private Task<IReadOnlyCollection<string>> SelectOptionFromJsonAsync(string json, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => SelectOptionAction.RunAsync(this, json, timeout, force, scroll);

        private async Task SetInputFilesFromPathsAsync(IEnumerable<string> files, float? timeout)
        {
            ResolvedInputFilePaths resolved = SetInputFilesPathHelper.Resolve(files);
            IElementHandle target = await SetInputFilesPathHelper.FollowLabelControlAsync(this).ConfigureAwait(false);
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(target, force: null, timeout).ConfigureAwait(false);
            await target.EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
            await SetInputFilesPathHelper.ValidateAgainstInputAsync(target, resolved).ConfigureAwait(false);
            if (resolved.IsDirectory)
            {
                await SetInputFilesFromJsonAsync(FilePayloadHelper.ToJson(resolved.Payloads), timeout).ConfigureAwait(false);
                return;
            }

            if (target is WKElementHandle webkit)
            {
                await webkit.SetFileInputFilesFromPathsAsync(resolved.AbsolutePaths).ConfigureAwait(false);
                return;
            }

            await target.SetInputFilesAsync(resolved.AbsolutePaths).ConfigureAwait(false);
        }

        private async Task SetInputFilesFromJsonAsync(string json, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            EnsureNotDisposed();
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            if (scroll != ActionScroll.None)
            {
                await EvaluateFunctionAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
            }

            await EvaluateFunctionAsync<bool>(ElementStateScript.SetInputFilesFromJsonFunction, json).ConfigureAwait(false);
        }

        private async Task InitializePreviewAsync()
        {
            try
            {
                string nodePreview = await EvaluateFunctionAsync<string>(RemoteObject.PreviewNodeFunction)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(nodePreview))
                {
                    SetPreview("JSHandle@" + nodePreview);
                }
            }
            catch (PlaywrightNativeException)
            {
                // Best-effort preview, matching upstream ElementHandle._initializePreview.
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IElementHandle.CheckAsync(ElementHandleCheckOptions options)
            => CheckAsync(options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task IElementHandle.ClickAsync(ElementHandleClickOptions options)
            => ClickAsync(
                options?.Button ?? default,
                options?.ClickCount,
                options?.Delay,
                options?.Position,
                options?.Modifiers,
                options?.Force,
                options?.NoWaitAfter,
                options?.Timeout,
                options?.Trial,
                default,
                options?.Steps);

        Task IElementHandle.DblClickAsync(ElementHandleDblClickOptions options)
            => DblClickAsync(
                options?.Button ?? default,
                options?.Delay,
                options?.Position,
                options?.Modifiers,
                options?.Force,
                options?.NoWaitAfter,
                options?.Timeout,
                options?.Trial,
                default,
                options?.Steps);

        Task IElementHandle.DispatchEventAsync(string type, object eventInit)
            => ElementDispatchEventAction.RunAsync(this, type, eventInit, default);

        Task<T> IElementHandle.EvalOnSelectorAllAsync<T>(string selector, string expression, object arg)
            => EvalOnSelector.OnArrayAsync<T>(
                EvaluateHandleAsync(EvalOnSelector.ElementQuerySelectorAllExpression(selector)),
                expression,
                arg);

        Task<JsonElement?> IElementHandle.EvalOnSelectorAsync(string selector, string expression, object arg)
            => EvalOnSelector.OnHandleAsync<JsonElement?>(QuerySelectorAsync(selector), selector, expression, arg, "elementHandle.$eval");

        Task<T> IElementHandle.EvalOnSelectorAsync<T>(string selector, string expression, object arg)
            => EvalOnSelector.OnHandleAsync<T>(QuerySelectorAsync(selector), selector, expression, arg, "elementHandle.$eval");

        Task IElementHandle.FillAsync(string value, ElementHandleFillOptions options)
            => FillAsync(value, options?.NoWaitAfter, options?.Timeout, options?.Force);

        Task IElementHandle.HoverAsync(ElementHandleHoverOptions options)
            => HoverAsync(options?.Position, options?.Modifiers, options?.Force, options?.Timeout, options?.Trial);

        async Task<string> IElementHandle.InputValueAsync(ElementHandleInputValueOptions options)
        {
            await WaitForElementStateHelper.WaitAsync(this, ElementState.Visible, options?.Timeout).ConfigureAwait(false);
            return await EvaluateFunctionAsync<string>(ElementStateScript.InputValueFunction).ConfigureAwait(false);
        }

        Task IElementHandle.PressAsync(string key, ElementHandlePressOptions options)
            => PressAsync(
                key,
                options?.Delay,
                options?.NoWaitAfter,
                options?.Timeout,
                force: (options as LegacyElementHandlePressOptions)?.Force);

        Task<byte[]> IElementHandle.ScreenshotAsync(ElementHandleScreenshotOptions options)
            => ScreenshotAsync(
                options?.Path,
                options?.Type ?? default,
                options?.Quality,
                options?.OmitBackground,
                options?.Timeout,
                options?.Scale?.ToString(),
                options?.Animations?.ToString(),
                options?.Caret?.ToString(),
                options?.Style,
                options?.Mask,
                options?.MaskColor);

        Task IElementHandle.ScrollIntoViewIfNeededAsync(ElementHandleScrollIntoViewIfNeededOptions options)
            => ScrollIntoViewIfNeededAsync(options?.Timeout);

        async Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(string values, ElementHandleSelectOptionOptions options)
            => (await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false)).ToList();

        async Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IElementHandle values, ElementHandleSelectOptionOptions options)
            => (await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false)).ToList();

        async Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IEnumerable<string> values, ElementHandleSelectOptionOptions options)
            => (await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false)).ToList();

        async Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(SelectOptionValue values, ElementHandleSelectOptionOptions options)
            => (await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false)).ToList();

        async Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IEnumerable<IElementHandle> values, ElementHandleSelectOptionOptions options)
            => (await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false)).ToList();

        async Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IEnumerable<SelectOptionValue> values, ElementHandleSelectOptionOptions options)
            => (await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false)).ToList();

        Task IElementHandle.SelectTextAsync(ElementHandleSelectTextOptions options)
            => SelectTextAsync(options?.Timeout, options?.Force);

        Task IElementHandle.SetCheckedAsync(bool checkedState, ElementHandleSetCheckedOptions options)
            => checkedState
                ? CheckAsync(options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial)
                : UncheckAsync(options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task IElementHandle.SetInputFilesAsync(string files, ElementHandleSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout, (options as LegacyElementHandleSetInputFilesOptions)?.Force);

        Task IElementHandle.SetInputFilesAsync(IEnumerable<string> files, ElementHandleSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.SetInputFilesAsync(FilePayload files, ElementHandleSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.SetInputFilesAsync(IEnumerable<FilePayload> files, ElementHandleSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.TapAsync(ElementHandleTapOptions options)
            => TapAsync(options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task IElementHandle.TypeAsync(string text, ElementHandleTypeOptions options)
            => TypeAsync(
                text,
                options?.Delay,
                options?.NoWaitAfter,
                options?.Timeout,
                force: (options as LegacyElementHandleTypeOptions)?.Force);

        Task IElementHandle.UncheckAsync(ElementHandleUncheckOptions options)
            => UncheckAsync(options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task IElementHandle.WaitForElementStateAsync(ElementState state, ElementHandleWaitForElementStateOptions options) => Task.CompletedTask;

        Task<IElementHandle> IElementHandle.WaitForSelectorAsync(string selector, ElementHandleWaitForSelectorOptions options) => Task.FromResult<IElementHandle>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
