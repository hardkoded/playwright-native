/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Handle to a DOM node in Firefox. Read operations, JS-backed click/fill/focus/check,
    /// hover/dblclick/tap, select-option, and set-input-files are wired; keyboard type/press
    /// are not implemented yet.
    /// </summary>
    internal sealed partial class FFElementHandle : FFJSHandle, IElementHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FFElementHandle"/> class.
        /// </summary>
        /// <param name="context">The execution context that owns the remote object.</param>
        /// <param name="objectId">The Juggler remote object id.</param>
        public FFElementHandle(FFExecutionContext context, string objectId)
            : base(context, objectId)
        {
        }

        /// <inheritdoc/>
        public override IElementHandle AsElement() => this;

        /// <inheritdoc/>
        public Task<ElementHandleBoundingBoxResult> BoundingBoxAsync()
            => throw NotImplementedHelper.ForMethod(nameof(BoundingBoxAsync));

        /// <inheritdoc/>
        public Task CheckAsync(Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
            => ActionTrial.IsTrial(trial)
                ? Task.CompletedTask
                : Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.CheckFunction);

        /// <inheritdoc/>
        public async Task ClickAsync(MouseButton button = default, int? clickCount = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default)
        {
            if (ActionTrial.IsTrial(trial))
            {
                return;
            }

            await ClickAction.PrepareAsync(this, force, timeout).ConfigureAwait(false);
            await Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.ClickFunction).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DblClickAsync(MouseButton button = default, float? delay = default, Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default, int? steps = default)
        {
            if (ActionTrial.IsTrial(trial))
            {
                return;
            }

            await ClickAction.PrepareAsync(this, force, timeout).ConfigureAwait(false);
            await Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.DblClickFunction).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task FillAsync(string value, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            await FillAction.WaitUnlessForcedAsync(this, force, timeout).ConfigureAwait(false);
            await Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.FillFunction, value, scroll == ActionScroll.None).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task FocusAsync()
            => FocusAsync(timeout: null);

        /// <inheritdoc/>
        public Task FocusAsync(float? timeout, ActionScroll scroll = default)
            => Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.FocusFunction, scroll == ActionScroll.None);

        /// <inheritdoc/>
        public Task<string> GetAttributeAsync(string name)
            => Context.EvaluateFunctionOnHandleAsync<string>(ObjectId, "(el, n) => el.getAttribute(n)", name);

        /// <inheritdoc/>
        public Task HoverAsync(Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
            => ActionTrial.IsTrial(trial)
                ? Task.CompletedTask
                : Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.HoverFunction);

        /// <inheritdoc/>
        public Task<string> InnerHTMLAsync()
            => Context.EvaluateFunctionOnHandleAsync<string>(ObjectId, "el => el.innerHTML");

        /// <inheritdoc/>
        public Task<string> InnerTextAsync()
            => Context.EvaluateFunctionOnHandleAsync<string>(ObjectId, ElementStateScript.InnerTextFunction);

        /// <inheritdoc/>
        public Task<bool> IsCheckedAsync()
            => Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.IsCheckedFunction);

        /// <inheritdoc/>
        public Task<bool> IsDisabledAsync()
            => Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.IsDisabledFunction);

        /// <inheritdoc/>
        public Task<bool> IsEditableAsync()
            => Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.IsEditableFunction);

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync()
            => Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.IsEnabledFunction);

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync()
            => !await IsVisibleAsync().ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<bool> IsVisibleAsync()
            => Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, DomVisibility.IsVisibleFunction);

        /// <inheritdoc/>
        public Task PressAsync(string key, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => throw NotImplementedHelper.ForMethod(nameof(PressAsync));

        /// <inheritdoc/>
        public Task<IElementHandle> QuerySelectorAsync(string selector)
            => throw NotImplementedHelper.ForMethod(nameof(QuerySelectorAsync));

        /// <inheritdoc/>
        public Task<IReadOnlyList<IElementHandle>> QuerySelectorAllAsync(string selector)
            => throw NotImplementedHelper.ForMethod(nameof(QuerySelectorAllAsync));

        /// <inheritdoc/>
        public Task<IFrame> ContentFrameAsync()
            => throw NotImplementedHelper.ForMethod(nameof(ContentFrameAsync));

        /// <inheritdoc/>
        public Task<IFrame> OwnerFrameAsync()
            => throw NotImplementedHelper.ForMethod(nameof(OwnerFrameAsync));

        /// <inheritdoc/>
        public Task<byte[]> ScreenshotAsync(
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
            => throw NotImplementedHelper.ForMethod(nameof(ScreenshotAsync));

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
        {
            _ = force;
            throw NotImplementedHelper.ForMethod(nameof(SelectOptionAsync));
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(IEnumerable<IElementHandle> values, bool? noWaitAfter = default, float? timeout = default, bool? force = default)
        {
            _ = force;
            throw NotImplementedHelper.ForMethod(nameof(SelectOptionAsync));
        }

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
            => throw NotImplementedHelper.ForMethod(nameof(SelectOptionAsync));

        /// <inheritdoc/>
        public Task SetInputFilesAsync(IEnumerable<FilePayload> files, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => SetInputFilesFromJsonAsync(FilePayloadHelper.ToJson(files));

        /// <inheritdoc/>
        public async Task SetInputFilesAsync(string files, bool? noWaitAfter = default, float? timeout = default)
        {
            await SetInputFilesFromPathsAsync(new[] { files }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetInputFilesAsync(IEnumerable<string> files, bool? noWaitAfter = default, float? timeout = default)
        {
            await SetInputFilesFromPathsAsync(files).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task SetInputFilesAsync(FilePayload files, bool? noWaitAfter = default, float? timeout = default)
            => SetInputFilesFromJsonAsync(FilePayloadHelper.ToJson(files == null ? null : new[] { files }));

        /// <inheritdoc/>
        public Task TapAsync(Position position = default, IEnumerable<KeyboardModifier> modifiers = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
            => ActionTrial.IsTrial(trial)
                ? Task.CompletedTask
                : Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.ClickFunction);

        /// <inheritdoc/>
        public Task<string> TextContentAsync()
            => Context.EvaluateFunctionOnHandleAsync<string>(ObjectId, "el => el.textContent");

        /// <inheritdoc/>
        public Task TypeAsync(string text, float? delay = default, bool? noWaitAfter = default, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => throw NotImplementedHelper.ForMethod(nameof(TypeAsync));

        /// <inheritdoc/>
        public Task UncheckAsync(Position position = default, bool? force = default, bool? noWaitAfter = default, float? timeout = default, bool? trial = default, ActionScroll scroll = default)
            => ActionTrial.IsTrial(trial)
                ? Task.CompletedTask
                : Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.UncheckFunction);

        private async Task SetInputFilesFromPathsAsync(IEnumerable<string> files)
        {
            ResolvedInputFilePaths resolved = SetInputFilesPathHelper.Resolve(files);
            await SetInputFilesPathHelper.ValidateAgainstInputAsync(this, resolved).ConfigureAwait(false);
            await SetInputFilesFromJsonAsync(FilePayloadHelper.ToJson(resolved.Payloads)).ConfigureAwait(false);
        }

        private Task SetInputFilesFromJsonAsync(string json)
            => Context.EvaluateFunctionOnHandleAsync<bool>(ObjectId, ElementStateScript.SetInputFilesFromJsonFunction, json);

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IElementHandle.CheckAsync(ElementHandleCheckOptions options) => Task.CompletedTask;

        Task IElementHandle.ClickAsync(ElementHandleClickOptions options) => Task.CompletedTask;

        Task IElementHandle.DblClickAsync(ElementHandleDblClickOptions options) => Task.CompletedTask;

        Task IElementHandle.DispatchEventAsync(string type, object eventInit) => Task.CompletedTask;

        Task<T> IElementHandle.EvalOnSelectorAllAsync<T>(string selector, string expression, object arg) => Task.FromResult<T>(default!);

        Task<JsonElement?> IElementHandle.EvalOnSelectorAsync(string selector, string expression, object arg) => Task.FromResult<JsonElement?>(default!);

        Task<T> IElementHandle.EvalOnSelectorAsync<T>(string selector, string expression, object arg) => Task.FromResult<T>(default!);

        Task IElementHandle.FillAsync(string value, ElementHandleFillOptions options) => Task.CompletedTask;

        Task IElementHandle.HoverAsync(ElementHandleHoverOptions options) => Task.CompletedTask;

        Task<string> IElementHandle.InputValueAsync(ElementHandleInputValueOptions options) => Task.FromResult<string>(default!);

        Task IElementHandle.PressAsync(string key, ElementHandlePressOptions options) => Task.CompletedTask;

        Task<byte[]> IElementHandle.ScreenshotAsync(ElementHandleScreenshotOptions options) => Task.FromResult<byte[]>(default!);

        Task IElementHandle.ScrollIntoViewIfNeededAsync(ElementHandleScrollIntoViewIfNeededOptions options) => Task.CompletedTask;

        Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(string values, ElementHandleSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IElementHandle values, ElementHandleSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IEnumerable<string> values, ElementHandleSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(SelectOptionValue values, ElementHandleSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IEnumerable<IElementHandle> values, ElementHandleSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task<IReadOnlyList<string>> IElementHandle.SelectOptionAsync(IEnumerable<SelectOptionValue> values, ElementHandleSelectOptionOptions options) => Task.FromResult<IReadOnlyList<string>>(default!);

        Task IElementHandle.SelectTextAsync(ElementHandleSelectTextOptions options) => Task.CompletedTask;

        Task IElementHandle.SetCheckedAsync(bool checkedState, ElementHandleSetCheckedOptions options) => Task.CompletedTask;

        Task IElementHandle.SetInputFilesAsync(string files, ElementHandleSetInputFilesOptions options) => Task.CompletedTask;

        Task IElementHandle.SetInputFilesAsync(IEnumerable<string> files, ElementHandleSetInputFilesOptions options) => Task.CompletedTask;

        Task IElementHandle.SetInputFilesAsync(FilePayload files, ElementHandleSetInputFilesOptions options) => Task.CompletedTask;

        Task IElementHandle.SetInputFilesAsync(IEnumerable<FilePayload> files, ElementHandleSetInputFilesOptions options) => Task.CompletedTask;

        Task IElementHandle.TapAsync(ElementHandleTapOptions options) => Task.CompletedTask;

        Task IElementHandle.TypeAsync(string text, ElementHandleTypeOptions options) => Task.CompletedTask;

        Task IElementHandle.UncheckAsync(ElementHandleUncheckOptions options) => Task.CompletedTask;

        Task IElementHandle.WaitForElementStateAsync(ElementState state, ElementHandleWaitForElementStateOptions options) => Task.CompletedTask;

        Task<IElementHandle> IElementHandle.WaitForSelectorAsync(string selector, ElementHandleWaitForSelectorOptions options) => Task.FromResult<IElementHandle>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
