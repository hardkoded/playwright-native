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
using PlaywrightNative.Input;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IElementHandle"/> wrapping <see cref="CRElementHandle"/>.</summary>
    internal sealed partial class ChromiumElementHandle : ChromiumJSHandle, IElementHandle
    {
        private readonly CRElementHandle _crElement;

        internal ChromiumElementHandle(CRElementHandle crElementHandle)
            : base(crElementHandle)
        {
            _crElement = crElementHandle ?? throw new ArgumentNullException(nameof(crElementHandle));
        }

        /// <inheritdoc/>
        public override IElementHandle AsElement() => this;

        /// <inheritdoc/>
        public async Task<ElementHandleBoundingBoxResult> BoundingBoxAsync()
        {
            BoundingBox? box = await _crElement.BoundingBoxAsync().ConfigureAwait(false);
            if (box == null)
            {
                return null;
            }

            BoundingBox b = box.Value;
            return new ElementHandleBoundingBoxResult
            {
                X = (float)b.X,
                Y = (float)b.Y,
                Width = (float)b.Width,
                Height = (float)b.Height,
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

            await _crElement.CheckAsync(position).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ClickAsync(MouseButton button = default, int? clickCount = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default)
        {
            await ClickAction.RunPointerAsync(_crElement.Page.PublicPage ?? (object)_crElement.Page, async () =>
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
                        await _crElement.Page.RunWithSignalsAsync(
                            noWaitAfter != true,
                            timeout,
                            () => ClickAction.RunModifiersAsync(
                                modifiers,
                                ClickAction.HeldKeys(_crElement.Keyboard.PressedModifiers),
                                _crElement.Keyboard.DownAsync,
                                _crElement.Keyboard.UpAsync,
                                () => _crElement.Page.Mouse.MoveAsync(point[0], point[1], steps ?? 1))).ConfigureAwait(false);
                    },
                    async () =>
                    {
                        await _crElement.Page.RunWithSignalsAsync(
                            noWaitAfter != true,
                            timeout,
                            () => ClickAction.RunModifiersAsync(
                                modifiers,
                                ClickAction.HeldKeys(_crElement.Keyboard.PressedModifiers),
                                _crElement.Keyboard.DownAsync,
                                _crElement.Keyboard.UpAsync,
                                async () =>
                                {
                                    Input.MouseButton inputButton = ToInputMouseButton(button);
                                    int count = clickCount ?? 1;
                                    for (int i = 1; i <= count; i++)
                                    {
                                        await _crElement.Page.Mouse.DownAsync(inputButton, i).ConfigureAwait(false);
                                        if (delay.HasValue && delay.Value > 0)
                                        {
                                            await Task.Delay((int)delay.Value).ConfigureAwait(false);
                                        }

                                        await _crElement.Page.Mouse.UpAsync(inputButton, i).ConfigureAwait(false);
                                    }
                                })).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DblClickAsync(MouseButton button = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default)
        {
            await ClickAction.RunPointerAsync(_crElement.Page.PublicPage ?? (object)_crElement.Page, async () =>
            {
                await ClickAction.PrepareAsync(this, force, timeout, trial, position, scroll).ConfigureAwait(false);
                if (ActionTrial.IsTrial(trial))
                {
                    return;
                }

                double[] point = await ClickAction.PointAsync(this, position, force).ConfigureAwait(false);
                await ClickAction.RunModifiersAsync(
                    modifiers,
                    ClickAction.HeldKeys(_crElement.Keyboard.PressedModifiers),
                    _crElement.Keyboard.DownAsync,
                    _crElement.Keyboard.UpAsync,
                    () => _crElement.Page.Mouse.ClickAsync(
                        point[0],
                        point[1],
                        ToInputMouseButton(button),
                        clickCount: 2,
                        delayMs: (int)(delay ?? 0),
                        steps ?? 1)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task FillAsync(string value, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            await FillAction.WaitUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            await _crElement.FillAsync(value, preventScroll: scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task FocusAsync()
            => FocusAsync(timeout: null);

        /// <inheritdoc/>
        public async Task FocusAsync(float? timeout, ActionScroll scroll = default)
        {
            // Official page.focus waits for attached, then focusNode — not a non-empty
            // bounding box. Zero-height tabindex elements are focusable; hidden
            // (display:none) nodes are not and still wait for visibility.
            bool focusable = await _crElement.EvaluateFunctionAsync<bool>(ElementStateScript.IsFocusableAreaFunction)
                .ConfigureAwait(false);
            if (!focusable)
            {
                await WaitForElementStateHelper.WaitAsync(this, ElementState.Visible, timeout).ConfigureAwait(false);
            }

            await _crElement.FocusAsync(preventScroll: scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<string> GetAttributeAsync(string name)
            => _crElement.EvaluateFunctionAsync<string>("(el, n) => el.getAttribute(n)", name);

        /// <inheritdoc/>
        public async Task HoverAsync(Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
        {
            IPage page = _crElement.Page.PublicPage;
            if (force != true && LocatorHandlers.ShouldHover(page))
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

            await ActionModifiers.RunAsync(
                modifiers,
                _crElement.Keyboard.DownAsync,
                _crElement.Keyboard.UpAsync,
                () => _crElement.HoverAsync(position)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<string> InnerHTMLAsync()
            => _crElement.EvaluateFunctionAsync<string>("el => el.innerHTML");

        /// <inheritdoc/>
        public Task<string> InnerTextAsync()
            => _crElement.EvaluateFunctionAsync<string>(ElementStateScript.InnerTextFunction);

        /// <inheritdoc/>
        public Task<bool> IsCheckedAsync()
            => _crElement.IsCheckedAsync();

        /// <inheritdoc/>
        public Task<bool> IsDisabledAsync()
            => _crElement.EvaluateFunctionAsync<bool>(ElementStateScript.IsDisabledFunction);

        /// <inheritdoc/>
        public Task<bool> IsEditableAsync()
            => _crElement.EvaluateFunctionAsync<bool>(ElementStateScript.IsEditableFunction);

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync()
            => _crElement.EvaluateFunctionAsync<bool>(ElementStateScript.IsEnabledFunction);

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync()
            => !await IsVisibleAsync().ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<bool> IsVisibleAsync()
            => _crElement.EvaluateFunctionAsync<bool>(DomVisibility.IsVisibleFunction);

        /// <inheritdoc/>
        public async Task PressAsync(string key, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            await _crElement.PressAsync(key, (int)(delay ?? 0), preventScroll: scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IElementHandle> QuerySelectorAsync(string selector)
        {
            CRElementHandle handle = await _crElement.QuerySelectorAsync(selector).ConfigureAwait(false);
            return handle == null ? null : new ChromiumElementHandle(handle);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
        {
            IReadOnlyList<CRElementHandle> handles = await _crElement.QuerySelectorAllAsync(selector).ConfigureAwait(false);
            List<IElementHandle> result = new(handles.Count);
            foreach (CRElementHandle handle in handles)
            {
                result.Add(new ChromiumElementHandle(handle));
            }

            return result;
        }

        /// <inheritdoc/>
        public Task<IFrame> ContentFrameAsync()
            => _crElement.ContentFrameAsync();

        /// <inheritdoc/>
        public Task<IFrame> OwnerFrameAsync()
            => _crElement.OwnerFrameAsync();

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
            IFrame frame = await OwnerFrameAsync().ConfigureAwait(false);
            IPage page = frame?.Page;
            if (page != null)
            {
                return await ElementScreenshot.CaptureAsync(
                    this,
                    page,
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

            return await _crElement.ScreenshotAsync(path, type, quality, omitBackground).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IEnumerable<SelectOptionValue> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => SelectOptionAction.RunAsync(this, SelectOptionPayload.FromOptions(values), timeout, force, scroll);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(string values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionAction.RunAsync(this, SelectOptionPayload.FromValues(values == null ? null : new[] { values }), timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IEnumerable<string> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionAction.RunAsync(this, SelectOptionPayload.FromValues(values), timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IElementHandle values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionFromHandlesAsync(values == null ? Array.Empty<IElementHandle>() : new[] { values }, timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IEnumerable<IElementHandle> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionFromHandlesAsync(values, timeout, force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(SelectOptionValue values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
            => SelectOptionAction.RunAsync(
                this,
                SelectOptionPayload.FromOptions(values == null ? null : new[] { values }),
                timeout,
                force);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(params string[] values)
            => SelectOptionAction.RunAsync(this, SelectOptionPayload.FromValues(values), timeout: null, force: null);

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(params SelectOptionValue[] values)
            => SelectOptionAction.RunAsync(this, SelectOptionPayload.FromOptions(values), timeout: null, force: null);

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

            await _crElement.SelectTextAsync(preventScroll: scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task ScrollIntoViewIfNeededAsync(float? timeout = default)
            => ScrollIntoViewIfNeededAction.RunAsync(this, ScrollRectIntoViewIfNeededAsync, timeout);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(IEnumerable<FilePayload> files, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            FilePayload[] array = files == null ? System.Array.Empty<FilePayload>() : System.Linq.Enumerable.ToArray(files);
            return SetInputFilesInternalAsync(array, timeout, force, scroll);
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(string files, bool? noWaitAfter = default, float? timeout = default)
            => SetInputFilesFromPathsAsync(new[] { files }, timeout);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(IEnumerable<string> files, bool? noWaitAfter = default, float? timeout = default)
            => SetInputFilesFromPathsAsync(files, timeout);

        /// <inheritdoc/>
        public Task SetInputFilesAsync(FilePayload files, bool? noWaitAfter = default, float? timeout = default)
            => SetInputFilesInternalAsync(files == null ? Array.Empty<FilePayload>() : new[] { files }, timeout);

        /// <inheritdoc/>
        public async Task TapAsync(Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            await TapSupport.WithTrialInterceptorAsync(
                this,
                trial,
                () => ActionModifiers.RunAsync(
                    modifiers,
                    _crElement.Keyboard.DownAsync,
                    _crElement.Keyboard.UpAsync,
                    () => _crElement.TapAsync(position))).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<string> TextContentAsync()
            => _crElement.EvaluateFunctionAsync<string>("el => el.textContent");

        /// <inheritdoc/>
        public async Task TypeAsync(string text, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            await _crElement.TypeAsync(text, (int)(delay ?? 0), preventScroll: scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task UncheckAsync(Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            if (ActionTrial.IsTrial(trial))
            {
                return;
            }

            await _crElement.UncheckAsync(position).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets filesystem paths via <c>DOM.setFileInputFiles</c> without a
        /// visibility wait (file chooser inputs may be detached).
        /// </summary>
        /// <param name="paths">Local filesystem paths. Empty clears the input.</param>
        /// <returns>A task that completes when the protocol command finishes.</returns>
        internal Task SetFileInputFilesFromPathsAsync(IReadOnlyList<string> paths)
            => _crElement.SetFileInputFilesFromPathsAsync(paths);

        /// <summary>Underlying Chromium element handle.</summary>
        /// <returns>The direct-CDP element.</returns>
        internal CRElementHandle Unwrap() => _crElement;

        private static Input.MouseButton ToInputMouseButton(MouseButton button)
            => button switch
            {
                MouseButton.Right => Input.MouseButton.Right,
                MouseButton.Middle => Input.MouseButton.Middle,
                _ => Input.MouseButton.Left,
            };

        private async Task SetInputFilesFromPathsAsync(IEnumerable<string> files, float? timeout)
        {
            ResolvedInputFilePaths resolved = SetInputFilesPathHelper.Resolve(files);
            IElementHandle target = await SetInputFilesPathHelper.FollowLabelControlAsync(this).ConfigureAwait(false);
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(target, force: null, timeout).ConfigureAwait(false);
            await target.EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
            await SetInputFilesPathHelper.ValidateAgainstInputAsync(target, resolved).ConfigureAwait(false);
            if (resolved.IsDirectory)
            {
                PlaywrightFilePayload[] payloads = resolved.Payloads;
                FilePayload[] official = payloads == null
                    ? Array.Empty<FilePayload>()
                    : Array.ConvertAll(payloads, payload => payload.ToOfficial());
                await SetInputFilesInternalAsync(official, timeout).ConfigureAwait(false);
                return;
            }

            if (target is ChromiumElementHandle chromium)
            {
                await chromium.SetFileInputFilesFromPathsAsync(resolved.AbsolutePaths).ConfigureAwait(false);
                return;
            }

            await target.SetInputFilesAsync(resolved.AbsolutePaths).ConfigureAwait(false);
        }

        private async Task<string> ScrollRectIntoViewIfNeededAsync()
        {
            try
            {
                await _crElement.Page.Session.SendAsync(
                    "DOM.scrollIntoViewIfNeeded",
                    new { objectId = _crElement.ObjectId }).ConfigureAwait(false);
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

        private async Task SetInputFilesInternalAsync(FilePayload[] files, float? timeout, bool? force = default, ActionScroll scroll = default)
        {
            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            if (scroll != ActionScroll.None)
            {
                await EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
            }

            await _crElement.SetInputFilesAsync(
                files == null
                    ? Array.Empty<PlaywrightFilePayload>()
                    : Array.ConvertAll(files, file => PlaywrightFilePayload.FromOfficial(file)))
                .ConfigureAwait(false);
        }

        private async Task<IReadOnlyCollection<string>> SelectOptionFromHandlesAsync(IEnumerable<IElementHandle> handles, float? timeout = default, bool? force = default)
        {
            if (handles == null)
            {
                return await SelectOptionAction.RunAsync(this, "[]", timeout, force).ConfigureAwait(false);
            }

            List<Input.SelectOption> options = new List<Input.SelectOption>();
            foreach (IElementHandle handle in handles)
            {
                if (handle == null)
                {
                    continue;
                }

                if (handle is not ChromiumElementHandle direct)
                {
                    throw new ArgumentException(
                        "SelectOptionAsync requires ChromiumElementHandle instances when running against a direct-CDP connection.",
                        nameof(handles));
                }

                string value = await direct._crElement.EvaluateFunctionAsync<string>("el => el.value").ConfigureAwait(false);
                options.Add(new Input.SelectOption { Value = value });
            }

            return await SelectOptionAction.RunAsync(
                this,
                SelectOptionPayload.FromInputOptions(options),
                timeout,
                force).ConfigureAwait(false);
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

        Task IElementHandle.DispatchEventAsync(string type, object eventInit) => Task.CompletedTask;

        Task<T> IElementHandle.EvalOnSelectorAllAsync<T>(string selector, string expression, object arg) => Task.FromResult<T>(default!);

        Task<JsonElement?> IElementHandle.EvalOnSelectorAsync(string selector, string expression, object arg) => Task.FromResult<JsonElement?>(default!);

        Task<T> IElementHandle.EvalOnSelectorAsync<T>(string selector, string expression, object arg) => Task.FromResult<T>(default!);

        Task IElementHandle.FillAsync(string value, ElementHandleFillOptions options)
            => FillAsync(value, options?.NoWaitAfter, options?.Timeout, options?.Force);

        Task IElementHandle.HoverAsync(ElementHandleHoverOptions options)
            => HoverAsync(options?.Position, options?.Modifiers, options?.Force, options?.Timeout, options?.Trial);

        Task<string> IElementHandle.InputValueAsync(ElementHandleInputValueOptions options)
            => _crElement.EvaluateFunctionAsync<string>(ElementStateScript.InputValueFunction);

        Task IElementHandle.PressAsync(string key, ElementHandlePressOptions options)
            => PressAsync(key, options?.Delay, options?.NoWaitAfter, options?.Timeout);

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
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.SetInputFilesAsync(IEnumerable<string> files, ElementHandleSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.SetInputFilesAsync(FilePayload files, ElementHandleSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.SetInputFilesAsync(IEnumerable<FilePayload> files, ElementHandleSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.TapAsync(ElementHandleTapOptions options)
            => TapAsync(options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task IElementHandle.TypeAsync(string text, ElementHandleTypeOptions options)
            => TypeAsync(text, options?.Delay, options?.NoWaitAfter, options?.Timeout);

        Task IElementHandle.UncheckAsync(ElementHandleUncheckOptions options)
            => UncheckAsync(options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task IElementHandle.WaitForElementStateAsync(ElementState state, ElementHandleWaitForElementStateOptions options) => Task.CompletedTask;

        Task<IElementHandle> IElementHandle.WaitForSelectorAsync(string selector, ElementHandleWaitForSelectorOptions options) => Task.FromResult<IElementHandle>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
