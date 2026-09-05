// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="ILocator"/> that re-queries a frame on every action.
    /// </summary>
    public sealed partial class Locator : ILocator
    {
        private const string TagIdFunction = @"el => {
    if (el.__pwLocId == null) {
        window.__pwLocSeq = (window.__pwLocSeq || 0) + 1;
        el.__pwLocId = String(window.__pwLocSeq);
    }
    return String(el.__pwLocId);
}";

        private const string XPathQueryAllOnDocument = @"body => {
    const doc = document;
    if (!doc || !doc.evaluate) {
        return [];
    }
    const expr = String(body || '');
    try {
        const result = doc.evaluate(expr, doc, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
        const out = [];
        for (let i = 0; i < result.snapshotLength; i++) {
            const n = result.snapshotItem(i);
            if (n && n.nodeType === 1) {
                out.push(n);
            }
        }
        return out;
    } catch (e) {
        const sq = String.fromCharCode(39);
        throw new Error(String(e && e.message ? e.message : e) + ' ' + expr.replace(new RegExp(sq, 'g'), String.fromCharCode(92) + sq));
    }
}";

        private const string XPathQueryAllOnElement = @"(el, body) => {
    if (!el) {
        return [];
    }
    const doc = el.nodeType === 9 ? el : el.ownerDocument;
    if (!doc || !doc.evaluate) {
        return [];
    }
    let expr = String(body || '');
    const original = expr;
    if (expr.charAt(0) === '/' && el.nodeType !== 9) {
        expr = '.' + expr;
    }
    try {
        const result = doc.evaluate(expr, el, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
        const out = [];
        for (let i = 0; i < result.snapshotLength; i++) {
            const n = result.snapshotItem(i);
            if (n && n.nodeType === 1) {
                out.push(n);
            }
        }
        return out;
    } catch (e) {
        const sq = String.fromCharCode(39);
        throw new Error(String(e && e.message ? e.message : e) + ' ' + original.replace(new RegExp(sq, 'g'), String.fromCharCode(92) + sq));
    }
}";

        private readonly IFrame _frame;
        private readonly IReadOnlyList<Step> _steps;
        private readonly Locator _scope;
        private readonly Locator _left;
        private readonly Locator _right;
        private readonly string _hasText;
        private readonly Regex _hasTextRegex;
        private readonly CombineKind _combine;
        private readonly int? _sliceIndex;
        private readonly bool _sliceLast;
        private readonly string _description;
        private readonly bool? _visible;
        private readonly bool _anyFrame;

        /// <summary>
        /// Initializes a new instance of the <see cref="Locator"/> class.
        /// </summary>
        /// <param name="frame">Frame to query.</param>
        /// <param name="selector">A CSS selector.</param>
        public Locator(IFrame frame, string selector)
            : this(frame, new[] { CreateStep(selector) })
        {
        }

        private Locator(IFrame frame, IReadOnlyList<Step> steps, Locator scope = null, string description = null, bool anyFrame = false)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            _scope = scope;
            _left = null;
            _right = null;
            _hasText = null;
            _hasTextRegex = null;
            _combine = CombineKind.None;
            _sliceIndex = null;
            _sliceLast = false;
            _description = description;
            _visible = null;
            _anyFrame = anyFrame;
        }

        private Locator(Locator left, Locator right, string hasText, CombineKind combine, int? sliceIndex = null, bool sliceLast = false, string description = null, Regex hasTextRegex = null, bool? visible = null)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right;
            _hasText = hasText;
            _hasTextRegex = hasTextRegex;
            _combine = combine;
            _frame = left._frame;
            _steps = Array.Empty<Step>();
            _scope = null;
            _sliceIndex = sliceIndex;
            _sliceLast = sliceLast;
            _description = description;
            _visible = visible;
            _anyFrame = false;
        }

        private enum CombineKind
        {
            None,
            HasText,
            And,
            Or,
            Has,
            Inside,
            HasNot,
            HasNotText,
            Visible,
        }

        /// <inheritdoc/>
        public IPage Page => _frame.Page;

        /// <inheritdoc/>
        public IFrame Frame => _frame;

        /// <inheritdoc/>
        public ILocator First => Narrow(0, last: false);

        /// <inheritdoc/>
        public ILocator Last => Narrow(null, last: true);

        /// <inheritdoc/>
        public IFrameLocator ContentFrame => new FrameLocator(this);

        /// <inheritdoc/>
        public string Description => _description;

        /// <summary>
        /// Official formatted locator string (selector chain), or the
        /// <see cref="Describe(string)"/> label when one is set.
        /// </summary>
        /// <returns>The locator preview used in logs and <c>toString()</c>.</returns>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(_description))
            {
                return _description;
            }

            return FormatLocator();
        }

        /// <inheritdoc/>
        public ILocator Nth(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            return Narrow(index, last: false);
        }

        /// <inheritdoc/>
        public ILocator Describe(string description)
        {
            if (description == null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            if (_combine != CombineKind.None)
            {
                return new Locator(_left, _right, _hasText, _combine, _sliceIndex, _sliceLast, description, _hasTextRegex, _visible);
            }

            return new Locator(_frame, _steps, _scope, description, _anyFrame);
        }

        /// <inheritdoc/>
        public IFrameLocator FrameLocator(string selector)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return new FrameLocator(((ILocator)this).Locator(selector));
        }

        /// <inheritdoc/>
        public ILocator Filter(
            string hasText,
            ILocator has = default,
            ILocator hasNot = default,
            string hasNotText = default,
            Regex hasNotTextRegex = default)
        {
            if (hasText == null)
            {
                throw new ArgumentNullException(nameof(hasText));
            }

            ILocator result = new Locator(this, null, hasText, CombineKind.HasText, description: _description);
            return SelectorQuery.ApplyOptions(result, has, null, null, hasNot, hasNotText, hasNotTextRegex);
        }

        /// <inheritdoc/>
        public ILocator Filter(
            Regex hasText,
            ILocator has = default,
            ILocator hasNot = default,
            string hasNotText = default,
            Regex hasNotTextRegex = default)
        {
            ArgumentNullException.ThrowIfNull(hasText);
            ILocator result = new Locator(this, null, null, CombineKind.HasText, description: _description, hasTextRegex: hasText);
            return SelectorQuery.ApplyOptions(result, has, null, null, hasNot, hasNotText, hasNotTextRegex);
        }

        /// <inheritdoc/>
        public ILocator Filter(
            ILocator has = default,
            ILocator hasNot = default,
            string hasNotText = default,
            Regex hasNotTextRegex = default,
            bool? visible = default)
        {
            ILocator result = this;
            if (visible.HasValue)
            {
                result = new Locator(
                    result as Locator ?? this,
                    null,
                    null,
                    CombineKind.Visible,
                    description: _description,
                    visible: visible);
            }

            return SelectorQuery.ApplyOptions(result, has, null, null, hasNot, hasNotText, hasNotTextRegex);
        }

        /// <inheritdoc/>
        public ILocator Filter(bool visible)
            => new Locator(this, null, null, CombineKind.Visible, description: _description, visible: visible);

        /// <inheritdoc/>
        public ILocator And(ILocator locator)
            => new Locator(this, RequireSameFrame(locator), null, CombineKind.And, description: _description);

        /// <inheritdoc/>
        public ILocator Or(ILocator locator)
            => new Locator(this, RequireSameFrame(locator), null, CombineKind.Or, description: _description);

        /// <inheritdoc/>
        public ILocator Has(ILocator locator)
            => new Locator(this, RequireInnerLocator(locator, "has"), null, CombineKind.Has, description: _description);

        /// <inheritdoc/>
        public ILocator HasNot(ILocator locator)
            => new Locator(this, RequireInnerLocator(locator, "hasNot"), null, CombineKind.HasNot, description: _description);

        /// <inheritdoc/>
        public ILocator HasNotText(string hasNotText)
        {
            if (hasNotText == null)
            {
                throw new ArgumentNullException(nameof(hasNotText));
            }

            return new Locator(this, null, hasNotText, CombineKind.HasNotText, description: _description);
        }

        /// <inheritdoc/>
        public ILocator HasNotText(Regex hasNotText)
        {
            ArgumentNullException.ThrowIfNull(hasNotText);
            return new Locator(this, null, null, CombineKind.HasNotText, description: _description, hasTextRegex: hasNotText);
        }

        /// <inheritdoc/>
        public ILocator GetByRole(
            string role,
            string name = null,
            bool? exact = null,
            bool? checkedState = null,
            bool? disabled = null,
            bool? expanded = null,
            bool? includeHidden = null,
            int? level = null,
            bool? pressed = null,
            bool? selected = null,
            string description = null,
            Regex descriptionRegex = null,
            Regex nameRegex = null)
            => Inside(new Locator(_frame, RoleSelector.Build(
                role,
                name,
                exact,
                checkedState,
                disabled,
                expanded,
                includeHidden,
                level,
                pressed,
                selected,
                description,
                descriptionRegex,
                nameRegex)));

        /// <inheritdoc/>
        public ILocator GetByText(string text, bool? exact = null)
            => Inside(FromScript(_frame, GetByAllScript.FindAllByText, text, exact ?? false));

        /// <inheritdoc/>
        public ILocator GetByText(Regex text)
            => Inside(FromScript(
                _frame,
                GetByAllScript.FindAllByTextRegex,
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text)));

        /// <inheritdoc/>
        public ILocator GetByLabel(string text, bool? exact = null)
            => Inside(FromScript(_frame, GetByAllScript.FindAllByLabel, text, exact ?? false));

        /// <inheritdoc/>
        public ILocator GetByLabel(Regex text)
            => Inside(FromScript(
                _frame,
                GetByAllScript.FindAllByLabelRegex,
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text)));

        /// <inheritdoc/>
        public ILocator GetByPlaceholder(string text, bool? exact = null)
            => Inside(FromScript(_frame, GetByAllScript.FindAllByAttribute, "placeholder", text, exact ?? false));

        /// <inheritdoc/>
        public ILocator GetByPlaceholder(Regex text)
            => Inside(FromScript(
                _frame,
                GetByAllScript.FindAllByAttributeRegex,
                "placeholder",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text)));

        /// <inheritdoc/>
        public ILocator GetByAltText(string text, bool? exact = null)
            => Inside(FromScript(_frame, GetByAllScript.FindAllByAttribute, "alt", text, exact ?? false));

        /// <inheritdoc/>
        public ILocator GetByAltText(Regex text)
            => Inside(FromScript(
                _frame,
                GetByAllScript.FindAllByAttributeRegex,
                "alt",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text)));

        /// <inheritdoc/>
        public ILocator GetByTitle(string text, bool? exact = null)
            => Inside(FromScript(_frame, GetByAllScript.FindAllByAttribute, "title", text, exact ?? false));

        /// <inheritdoc/>
        public ILocator GetByTitle(Regex text)
            => Inside(FromScript(
                _frame,
                GetByAllScript.FindAllByAttributeRegex,
                "title",
                GetByAllScript.Pattern(text),
                GetByAllScript.Flags(text)));

        /// <inheritdoc/>
        public ILocator GetByTestId(string testId)
            => Inside(new Locator(_frame, GetBySelectorScript.TestIdSelector(testId)));

        /// <inheritdoc/>
        public ILocator GetByTestId(Regex testId)
            => Inside(FromScript(
                _frame,
                GetByAllScript.FindAllByAttributeRegex,
                GetBySelectorScript.TestIdAttributeName(),
                GetByAllScript.Pattern(testId),
                GetByAllScript.Flags(testId)));

        /// <inheritdoc/>
        public async Task<int> CountAsync()
        {
            IReadOnlyList<IElementHandle> all = await ResolveAllAsync().ConfigureAwait(false);
            await ThrowIfAnyFrameMultipleAsync(all).ConfigureAwait(false);
            return all.Count;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ILocator>> AllAsync()
        {
            int count = await CountAsync().ConfigureAwait(false);
            List<ILocator> locators = new(count);
            for (int i = 0; i < count; i++)
            {
                locators.Add(Nth(i));
            }

            return locators;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<IElementHandle>> ElementHandlesAsync()
            => ResolveAllAsync();

        /// <inheritdoc/>
        public Task<IElementHandle> ElementHandleAsync(float? timeout = default)
            => WaitForHandleAsync(timeout, "locator.elementHandle");

        /// <inheritdoc/>
        public async Task ClickAsync(
            MouseButton button = default,
            int? clickCount = default,
            float? delay = default,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            int? steps = default,
            AbortSignal signal = default)
        {
            if (signal != null && signal.Aborted)
            {
                throw AbortError.AlreadyAborted(signal.Reason);
            }

            AbortSignal previous = ClickAction.ActiveSignal.Value;
            ClickAction.ActiveSignal.Value = signal;
            try
            {
                await ClickAction.RunOnSelectorAsync(
                async _ =>
                {
                    if (force != true)
                    {
                        await LocatorHandlers.RunAsync(Page, timeout).ConfigureAwait(false);
                    }

                    return await ResolveOneOrNullAsync().ConfigureAwait(false);
                },
                ToString(),
                h => h.ClickAsync(
                    button,
                    clickCount,
                    delay,
                    position,
                    modifiers,
                    force,
                    noWaitAfter,
                    timeout,
                    trial,
                    scroll,
                    steps),
                timeout,
                "locator.click",
                scroll).ConfigureAwait(false);
            }
            catch (AbortError)
            {
                throw;
            }
            catch (TimeoutException ex)
            {
                throw new PlaywrightNativeException(ex.Message + "\nwaiting for " + ToString(), ex);
            }
            finally
            {
                ClickAction.ActiveSignal.Value = previous;
            }
        }

        /// <inheritdoc/>
        public async Task DblClickAsync(
            MouseButton button = default,
            float? delay = default,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            int? steps = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.dblclick", force == true).ConfigureAwait(false);
            await handle.DblClickAsync(
                button,
                delay,
                position,
                modifiers,
                force,
                noWaitAfter,
                timeout,
                trial,
                scroll,
                steps).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task HoverAsync(
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.hover", force == true).ConfigureAwait(false);
            await handle.HoverAsync(position, modifiers, force, timeout, trial, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task FocusAsync(float? timeout = default, ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.focus").ConfigureAwait(false);
            await handle.FocusAsync(timeout, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task TapAsync(
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
        {
            TapSupport.ThrowIfDisabled(_frame.Page?.Context);
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.tap").ConfigureAwait(false);
            await handle.TapAsync(position, modifiers, force, noWaitAfter, timeout, trial, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task FillAsync(
            string value,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.fill").ConfigureAwait(false);
            try
            {
                await handle.FillAsync(value, noWaitAfter, timeout, force, scroll).ConfigureAwait(false);
            }
            catch (Exception ex) when (FillAction.IsValidation(ex))
            {
                throw FillAction.Wrap(ex, "locator.fill");
            }
        }

        /// <inheritdoc/>
        public async Task<string> TextContentAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.textContent").ConfigureAwait(false);
            return await handle.TextContentAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task CheckAsync(
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.check").ConfigureAwait(false);
            await handle.CheckAsync(position, force, noWaitAfter, timeout, trial, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task UncheckAsync(
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.uncheck").ConfigureAwait(false);
            await handle.UncheckAsync(position, force, noWaitAfter, timeout, trial, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetCheckedAsync(
            bool checkedState,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.setChecked").ConfigureAwait(false);
            await handle.SetCheckedAsync(checkedState, position, force, noWaitAfter, timeout, trial, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> IsCheckedAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.isChecked").ConfigureAwait(false);
            return await handle.IsCheckedAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> IsVisibleAsync()
        {
            ThrowIfUnknownSelectorEngine();
            try
            {
                IElementHandle handle = await ResolveOneOrNullAsync().ConfigureAwait(false);
                if (handle == null)
                {
                    return false;
                }

                return await handle.IsVisibleAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (DomVisibility.IsTransientVisibilityError(ex))
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsHiddenAsync()
        {
            ThrowIfUnknownSelectorEngine();
            try
            {
                IElementHandle handle = await ResolveOneOrNullAsync().ConfigureAwait(false);
                if (handle == null)
                {
                    return true;
                }

                return await handle.IsHiddenAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (DomVisibility.IsTransientVisibilityError(ex))
            {
                return true;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsEnabledAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.isEnabled").ConfigureAwait(false);
            return await handle.IsEnabledAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> IsDisabledAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.isDisabled").ConfigureAwait(false);
            return await handle.IsDisabledAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<bool> IsEditableAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.isEditable").ConfigureAwait(false);
            return await handle.IsEditableAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> GetAttributeAsync(string name, float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.getAttribute").ConfigureAwait(false);
            return await handle.GetAttributeAsync(name).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> InnerTextAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.innerText").ConfigureAwait(false);
            return await handle.InnerTextAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> InnerHTMLAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.innerHTML").ConfigureAwait(false);
            return await handle.InnerHTMLAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> InputValueAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.inputValue").ConfigureAwait(false);
            return await handle.InputValueAsync(timeout).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task PressAsync(
            string key,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.press").ConfigureAwait(false);
            await handle.PressAsync(key, delay, noWaitAfter, timeout, force, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task TypeAsync(
            string text,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.type").ConfigureAwait(false);
            await handle.TypeAsync(text, delay, noWaitAfter, timeout, force, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task WaitForAsync(WaitForSelectorState state = WaitForSelectorState.Visible, float? timeout = default)
            => WaitForStateAsync(state, timeout);

        /// <inheritdoc/>
        public Task ClearAsync(
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
            => FillAsync(string.Empty, noWaitAfter, timeout, force, scroll);

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<string>> SelectOptionAsync(
            IEnumerable<string> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.selectOption").ConfigureAwait(false);
            return await handle.SelectOptionAsync(values, noWaitAfter, timeout, force).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<string>> SelectOptionAsync(
            string values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.selectOption").ConfigureAwait(false);
            return await handle.SelectOptionAsync(values, noWaitAfter, timeout, force).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<string>> SelectOptionAsync(
            IEnumerable<SelectOptionValue> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.selectOption").ConfigureAwait(false);
            return await handle.SelectOptionAsync(values, noWaitAfter, timeout, force, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<string>> SelectOptionAsync(
            IElementHandle values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.selectOption").ConfigureAwait(false);
            return await handle.SelectOptionAsync(values, noWaitAfter, timeout, force).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<string>> SelectOptionAsync(
            IEnumerable<IElementHandle> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.selectOption").ConfigureAwait(false);
            return await handle.SelectOptionAsync(values, noWaitAfter, timeout, force).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<string>> SelectOptionAsync(
            SelectOptionValue values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.selectOption").ConfigureAwait(false);
            return await handle.SelectOptionAsync(values, noWaitAfter, timeout, force).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<string>> SelectOptionAsync(params string[] values)
            => SelectOptionAsync((IEnumerable<string>)values);

        /// <inheritdoc/>
        public async Task<T> EvaluateAsync<T>(string expression, object arg = default, float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.evaluate").ConfigureAwait(false);
            return await handle.EvaluateAsync<T>(expression, arg).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T> EvaluateAllAsync<T>(string expression, object arg = default)
        {
            if (IsAnyFrameLocator())
            {
                IReadOnlyList<IElementHandle> all = await ResolveAllAsync().ConfigureAwait(false);
                await ThrowIfAnyFrameMultipleAsync(all).ConfigureAwait(false);
                return await EvaluateAllResolvedAsync<T>(expression, arg, all).ConfigureAwait(false);
            }

            if (TryGetSimpleSelector(out string selector))
            {
                return await _frame.EvalOnSelectorAllAsync<T>(selector, expression, arg).ConfigureAwait(false);
            }

            return await EvaluateAllResolvedAsync<T>(expression, arg).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ElementHandleBoundingBoxResult> BoundingBoxAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.boundingBox").ConfigureAwait(false);
            return await handle.BoundingBoxAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetInputFilesAsync(string files, bool? noWaitAfter = default, float? timeout = default)
        {
            await ActionTrace.RunAsync(
                _frame.Page?.Context,
                "Set input files " + ToString(),
                "Locator",
                "setInputFiles",
                async () =>
                {
                    IElementHandle handle = await WaitForHandleAsync(timeout, "locator.setInputFiles").ConfigureAwait(false);
                    await handle.SetInputFilesAsync(files, noWaitAfter, timeout).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetInputFilesAsync(IEnumerable<string> files, bool? noWaitAfter = default, float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.setInputFiles").ConfigureAwait(false);
            await handle.SetInputFilesAsync(files, noWaitAfter, timeout).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetInputFilesAsync(FilePayload files, bool? noWaitAfter = default, float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.setInputFiles").ConfigureAwait(false);
            await handle.SetInputFilesAsync(files, noWaitAfter, timeout).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetInputFilesAsync(
            IEnumerable<FilePayload> files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.setInputFiles").ConfigureAwait(false);
            await handle.SetInputFilesAsync(files, noWaitAfter, timeout, force, scroll).ConfigureAwait(false);
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
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.screenshot").ConfigureAwait(false);
            return await handle.ScreenshotAsync(
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
        public async Task DispatchEventAsync(string type, object eventInit = default, float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.dispatchEvent").ConfigureAwait(false);
            await handle.DispatchEventAsync(type, eventInit, timeout).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ScrollIntoViewIfNeededAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.scrollIntoViewIfNeeded").ConfigureAwait(false);
            await handle.EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task BlurAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.blur").ConfigureAwait(false);
            await handle.EvaluateAsync<bool>(ElementStateScript.BlurFunction).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SelectTextAsync(float? timeout = default, bool? force = default, ActionScroll scroll = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.selectText").ConfigureAwait(false);
            await handle.SelectTextAsync(timeout, force, scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> AllInnerTextsAsync()
        {
            IReadOnlyList<IElementHandle> all = await ResolveAllAsync().ConfigureAwait(false);
            List<string> results = new List<string>(all.Count);
            foreach (IElementHandle handle in all)
            {
                results.Add(await handle.EvaluateAsync<string>("el => el.innerText").ConfigureAwait(false));
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> AllTextContentsAsync()
        {
            IReadOnlyList<IElementHandle> all = await ResolveAllAsync().ConfigureAwait(false);
            List<string> results = new List<string>(all.Count);
            foreach (IElementHandle handle in all)
            {
                results.Add(await handle.EvaluateAsync<string>("el => el.textContent || ''").ConfigureAwait(false));
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default, float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.evaluateHandle").ConfigureAwait(false);
            return await handle.EvaluateHandleAsync(expression, arg).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DragToAsync(
            ILocator target,
            Position sourcePosition = default,
            Position targetPosition = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            int? steps = default,
            ActionScroll scroll = default)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!ReferenceEquals(target.Page, Page))
            {
                throw new PlaywrightNativeException("Target locator must belong to the same page.");
            }

            _ = noWaitAfter;
            IElementHandle sourceHandle = await WaitForHandleAsync(timeout, "locator.dragTo").ConfigureAwait(false);
            IElementHandle targetHandle = await target.ElementHandleAsync(timeout).ConfigureAwait(false);
            if (force != true)
            {
                await sourceHandle.WaitForElementStateAsync(ElementState.Visible, timeout).ConfigureAwait(false);
                await targetHandle.WaitForElementStateAsync(ElementState.Visible, timeout).ConfigureAwait(false);
            }

            await DragAndDropHelper.RunHandlesAsync(
                Page,
                sourceHandle,
                targetHandle,
                sourcePosition,
                targetPosition,
                trial,
                steps,
                scroll).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task HighlightAsync(float? timeout = default, string style = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.highlight").ConfigureAwait(false);
            string tooltip = ToString();
            string payload = "{\"tooltip\":" + JsonSerializer.Serialize(tooltip) + ",\"style\":" + JsonSerializer.Serialize(style ?? string.Empty) + ",\"id\":" + JsonSerializer.Serialize(tooltip) + "}";
            await handle.EvaluateAsync<bool>(ElementStateScript.HighlightFunction, payload).ConfigureAwait(false);
            PageHighlights.Remember(Page, this, style, tooltip);
        }

        /// <inheritdoc/>
        public Task HighlightAsync(IReadOnlyDictionary<string, string> style, float? timeout = default)
            => HighlightAsync(timeout, HighlightStyle.ToCss(style));

        /// <inheritdoc/>
        public async Task HideHighlightAsync(float? timeout = default)
        {
            _ = timeout;
            string id = ToString();
            PageHighlights.Forget(Page, id);
            await Page.EvaluateAsync(ElementStateScript.HideHighlightByIdFunction, id).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DropAsync(DropPayload payload, float? timeout = default)
        {
            ArgumentNullException.ThrowIfNull(payload);
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.drop").ConfigureAwait(false);
            await handle.EvaluateAsync<bool>(ElementStateScript.DropPayloadFunction, ToDropJson(payload)).ConfigureAwait(false);

            static string ToDropJson(DropPayload drop)
            {
                List<object> files = new();
                if (drop.Files != null)
                {
                    foreach (FilePayload file in drop.Files)
                    {
                        files.Add(new
                        {
                            name = file?.Name ?? string.Empty,
                            mimeType = file?.MimeType ?? "application/octet-stream",
                            buffer = Convert.ToBase64String(file?.Buffer ?? Array.Empty<byte>()),
                        });
                    }
                }

                List<object> data = new();
                if (drop.Data != null)
                {
                    foreach (KeyValuePair<string, string> entry in drop.Data)
                    {
                        data.Add(new
                        {
                            type = entry.Key ?? string.Empty,
                            value = entry.Value ?? string.Empty,
                        });
                    }
                }

                return JsonSerializer.Serialize(new { files, data });
            }
        }

        /// <inheritdoc/>
        public async Task<ILocator> NormalizeAsync(float? timeout = default)
        {
            IElementHandle handle = await WaitForHandleAsync(timeout, "locator.normalize").ConfigureAwait(false);
            IFrame owner = await handle.OwnerFrameAsync().ConfigureAwait(false) ?? _frame;
            string[] hints = await handle.EvaluateAsync<string[]>(
                ElementStateScript.NormalizeHintFunction,
                GetBySelectorScript.TestIdAttributeName()).ConfigureAwait(false);

            if (hints == null || hints.Length < 7)
            {
                return this;
            }

            string testId = hints[0];
            string role = hints[1];
            string name = hints[2];
            string placeholder = hints[3];
            string alt = hints[4];
            string title = hints[5];
            string id = hints[6];

            ILocator local = null;
            if (!string.IsNullOrEmpty(testId)
                && await MatchesSameElementAsync(owner.GetByTestId(testId), handle).ConfigureAwait(false))
            {
                local = owner.GetByTestId(testId);
            }
            else if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(name)
                && await MatchesSameElementAsync(owner.GetByRole(role, name, exact: true), handle).ConfigureAwait(false))
            {
                local = owner.GetByRole(role, name, exact: true);
            }
            else if (!string.IsNullOrEmpty(role)
                && await MatchesSameElementAsync(owner.GetByRole(role), handle).ConfigureAwait(false))
            {
                local = owner.GetByRole(role);
            }
            else if (!string.IsNullOrEmpty(placeholder)
                && await MatchesSameElementAsync(owner.GetByPlaceholder(placeholder, exact: true), handle).ConfigureAwait(false))
            {
                local = owner.GetByPlaceholder(placeholder, exact: true);
            }
            else if (!string.IsNullOrEmpty(alt)
                && await MatchesSameElementAsync(owner.GetByAltText(alt, exact: true), handle).ConfigureAwait(false))
            {
                local = owner.GetByAltText(alt, exact: true);
            }
            else if (!string.IsNullOrEmpty(title)
                && await MatchesSameElementAsync(owner.GetByTitle(title, exact: true), handle).ConfigureAwait(false))
            {
                local = owner.GetByTitle(title, exact: true);
            }
            else if (!string.IsNullOrEmpty(name)
                && await MatchesSameElementAsync(owner.GetByLabel(name, exact: true), handle).ConfigureAwait(false))
            {
                local = owner.GetByLabel(name, exact: true);
            }
            else if (IsSimpleHtmlId(id)
                && await MatchesSameElementAsync(new Locator(owner, "#" + id), handle).ConfigureAwait(false))
            {
                local = new Locator(owner, "#" + id);
            }

            if (local == null)
            {
                string tag = await handle.EvaluateAsync<string>("el => (el && el.tagName) ? String(el.tagName).toLowerCase() : ''").ConfigureAwait(false);
                if (string.Equals(tag, "body", StringComparison.Ordinal)
                    || string.Equals(tag, "frameset", StringComparison.Ordinal))
                {
                    local = new Locator(owner, tag);
                }
                else if (string.Equals(tag, "iframe", StringComparison.Ordinal)
                    || string.Equals(tag, "frame", StringComparison.Ordinal))
                {
                    local = await NormalizeFrameHostAsync(handle, tag, owner).ConfigureAwait(false);
                }

                if (local == null)
                {
                    string text = await handle.EvaluateAsync<string>("el => String((el && (el.innerText || el.textContent)) || '').replace(/\\s+/g, ' ').trim()").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(text)
                        && await MatchesSameElementAsync(owner.GetByText(text, exact: true), handle).ConfigureAwait(false))
                    {
                        local = owner.GetByText(text, exact: true);
                    }
                }
            }

            return await WrapWithParentFramesAsync(local ?? this, handle).ConfigureAwait(false);

            static bool IsSimpleHtmlId(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                char first = value[0];
                if (!char.IsLetter(first))
                {
                    return false;
                }

                for (int i = 1; i < value.Length; i++)
                {
                    char ch = value[i];
                    if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_' && ch != ':' && ch != '.')
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <inheritdoc/>
        public Task<IJSHandle> WaitForFunctionAsync(string expression, object arg = default, float? pollingInterval = default, float? timeout = default, AbortSignal signal = default)
        {
            if (signal != null && signal.Aborted)
            {
                throw AbortError.AlreadyAborted(signal.Reason);
            }

            string wrapped = WaitForFunctionHelper.BuildLocatorPredicateExpression(expression);
            return WaitForFunctionHelper.WaitAsync(
                async () =>
                {
                    await LocatorHandlers.RunAsync(Page, timeout).ConfigureAwait(false);
                    IReadOnlyList<IElementHandle> all = await ResolveAllAsync().ConfigureAwait(false);
                    if (all.Count == 0)
                    {
                        return null;
                    }

                    if (all.Count > 1)
                    {
                        throw new PlaywrightNativeException(
                            await StrictResolvedMessageAsync(all).ConfigureAwait(false));
                    }

                    return await all[0].EvaluateHandleAsync(wrapped, arg).ConfigureAwait(false);
                },
                pollingInterval,
                timeout,
                () => _frame.EvaluateAsync<object>("new Promise(r => requestAnimationFrame(() => r(true)))"),
                "locator.waitForFunction",
                isDetached: null,
                signal);
        }

        /// <inheritdoc/>
        public Task PressSequentiallyAsync(
            string text,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
            => TypeAsync(text, delay, noWaitAfter, timeout, force, scroll);

        /// <inheritdoc/>
        public async Task<string> AriaSnapshotAsync(
            float? timeout = default,
            AriaSnapshotMode mode = AriaSnapshotMode.Default,
            int? depth = default,
            bool? boxes = default)
        {
            IElementHandle handle;
            if (mode == AriaSnapshotMode.Ai)
            {
                handle = await ResolveOneOrNullAsync().ConfigureAwait(false);
                if (handle == null)
                {
                    throw new PlaywrightNativeException(
                        "locator.ariaSnapshot: Locator does not match any element.");
                }
            }
            else
            {
                handle = await WaitForHandleAsync(timeout, "locator.ariaSnapshot").ConfigureAwait(false);
            }

            return await handle.AriaSnapshotAsync(mode, depth, boxes).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> AriaSnapshotJsonAsync(
            float? timeout = default,
            AriaSnapshotMode mode = AriaSnapshotMode.Default,
            int? depth = default,
            bool? boxes = default)
        {
            IElementHandle handle;
            if (mode == AriaSnapshotMode.Ai)
            {
                handle = await ResolveOneOrNullAsync().ConfigureAwait(false);
                if (handle == null)
                {
                    throw new PlaywrightNativeException(
                        "locator.ariaSnapshotJSON: Locator does not match any element.");
                }
            }
            else
            {
                handle = await WaitForHandleAsync(timeout, "locator.ariaSnapshotJSON").ConfigureAwait(false);
            }

            return await handle.AriaSnapshotJsonAsync(mode, depth, boxes).ConfigureAwait(false);
        }

        internal static Locator FromScript(IFrame frame, string script, params object[] args)
            => new Locator(frame, new[] { CreateScriptStep(script, args) });

        internal static Locator InAnyFrame(IFrame frame, string selector)
            => new Locator(frame, new[] { CreateStep(selector) }, anyFrame: true);

        internal Locator WithAnyFrame()
        {
            if (_anyFrame)
            {
                return this;
            }

            if (_combine != CombineKind.None)
            {
                return new Locator(
                    _left == null ? null : _left.WithAnyFrame(),
                    _right == null ? null : _right.WithAnyFrame(),
                    _hasText,
                    _combine,
                    _sliceIndex,
                    _sliceLast,
                    _description,
                    _hasTextRegex,
                    _visible);
            }

            return new Locator(_frame, _steps, _scope == null ? null : _scope.WithAnyFrame(), _description, anyFrame: true);
        }

        internal Locator EnterThen(string selector)
            => new Locator(_frame, new[] { CreateStep(selector) }, this);

        internal Locator EnterThenScript(string script, params object[] args)
            => new Locator(_frame, new[] { CreateScriptStep(script, args) }, this);

        /// <summary>
        /// Element-relative <c>locator.locator(selector)</c>. When already inside a
        /// <c>contentFrame()</c> (<c>_scope</c> set), appends a step in that
        /// document instead of nesting another frame entry.
        /// </summary>
        /// <param name="selector">Child selector.</param>
        /// <returns>The chained locator.</returns>
        internal Locator ChainLocator(string selector)
        {
            if (_scope != null)
            {
                List<Step> next = CopySteps();
                next.Add(CreateStep(selector));
                return new Locator(_frame, next, _scope, _description, _anyFrame);
            }

            return (Locator)Inside(new Locator(_frame, selector));
        }

        /// <summary>
        /// Element-relative <c>locator.locator(other)</c>. Frame-entered locators
        /// rebase <paramref name="inner"/> into the same content document.
        /// </summary>
        /// <param name="inner">Inner locator from the same page/frame.</param>
        /// <returns>The chained locator.</returns>
        internal Locator ChainLocator(Locator inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            if (!ReferenceEquals(inner._frame, _frame))
            {
                throw new PlaywrightNativeException("Locators must belong to the same frame.");
            }

            if (_scope == null)
            {
                return (Locator)Inside(inner);
            }

            Locator AppendInContentFrame(Locator node)
            {
                if (node._combine != CombineKind.None)
                {
                    Locator left = AppendInContentFrame(node._left);
                    Locator right = node._right == null ? null : AppendInContentFrame(node._right);
                    return new Locator(
                        left,
                        right,
                        node._hasText,
                        node._combine,
                        node._sliceIndex,
                        node._sliceLast,
                        node._description,
                        node._hasTextRegex,
                        node._visible);
                }

                Locator scope = _scope;
                List<Step> next = CopySteps();
                if (node._scope != null)
                {
                    Locator nestedHost = AppendInContentFrame(node._scope);
                    List<Step> nestedSteps = new List<Step>(node._steps.Count);
                    for (int i = 0; i < node._steps.Count; i++)
                    {
                        nestedSteps.Add(node._steps[i]);
                    }

                    return new Locator(_frame, nestedSteps, nestedHost, node._description, _anyFrame);
                }

                for (int i = 0; i < node._steps.Count; i++)
                {
                    next.Add(node._steps[i]);
                }

                return new Locator(_frame, next, scope, node._description, _anyFrame);
            }

            return AppendInContentFrame(inner);
        }

        internal Locator EnterThenLocator(Locator inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            if (inner._combine != CombineKind.None)
            {
                Locator left = EnterThenLocator(inner._left);
                Locator right = inner._right == null ? null : EnterThenLocator(inner._right);
                return new Locator(
                    left,
                    right,
                    inner._hasText,
                    inner._combine,
                    inner._sliceIndex,
                    inner._sliceLast,
                    inner._description,
                    inner._hasTextRegex,
                    inner._visible);
            }

            Locator scope = this;
            if (inner._scope != null)
            {
                scope = EnterThenLocator(inner._scope);
            }

            return new Locator(_frame, inner._steps, scope, inner._description);
        }

        private static async Task<IReadOnlyList<IElementHandle>> QueryStepAsync(IFrame frame, IElementHandle parent, Step step, bool ariaDescendants = true)
        {
            if (step.Script != null)
            {
                return await FrameGetBy.QueryAllAsync(frame, step.Script, step.ScriptArgs).ConfigureAwait(false);
            }

            if (AriaSnapshotAi.TryParse(step.Selector, out string ariaRef))
            {
                IFrame start = parent == null ? frame : await parent.OwnerFrameAsync().ConfigureAwait(false) ?? frame;
                IElementHandle hit = await AriaSnapshotAi.FindAsync(start, ariaRef, descendants: ariaDescendants).ConfigureAwait(false);
                return hit == null ? Array.Empty<IElementHandle>() : new[] { hit };
            }

            if (FrameSelector.ContainsControl(step.Selector))
            {
                return await FrameSelector.QueryAllAsync(frame, parent, step.Selector).ConfigureAwait(false);
            }

            if (TryParseXPathSelector(step.Selector, out string xpath))
            {
                return await QueryXPathAsync(frame, parent, xpath).ConfigureAwait(false);
            }

            if (HasVisibleEngine(step.Selector))
            {
                return await QueryVisibleChainAsync(frame, parent, step.Selector).ConfigureAwait(false);
            }

            if (parent == null)
            {
                return await frame.QuerySelectorAllAsync(step.Selector).ConfigureAwait(false);
            }

            return await parent.QuerySelectorAllAsync(step.Selector).ConfigureAwait(false);
        }

        private static bool TryParseXPathSelector(string selector, out string xpath)
        {
            xpath = null;
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            if (selector.StartsWith("//", StringComparison.Ordinal)
                || selector.StartsWith("..", StringComparison.Ordinal))
            {
                xpath = selector;
                return true;
            }

            int equals = selector.IndexOf('=');
            if (equals <= 0)
            {
                return false;
            }

            string engine = selector.Substring(0, equals).Trim();
            if (!string.Equals(engine, "xpath", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            xpath = selector.Substring(equals + 1).TrimStart();
            return true;
        }

        private static async Task<IReadOnlyList<IElementHandle>> QueryXPathAsync(
            IFrame frame,
            IElementHandle parent,
            string xpath)
        {
            IJSHandle arrayHandle;
            if (parent == null)
            {
                arrayHandle = await frame.EvaluateHandleAsync(XPathQueryAllOnDocument, xpath).ConfigureAwait(false);
            }
            else
            {
                arrayHandle = await parent.EvaluateHandleAsync(XPathQueryAllOnElement, xpath).ConfigureAwait(false);
            }

            return await UnwrapElementArrayAsync(arrayHandle).ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<IElementHandle>> UnwrapElementArrayAsync(IJSHandle arrayHandle)
        {
            if (arrayHandle == null)
            {
                return Array.Empty<IElementHandle>();
            }

            int count = await arrayHandle
                .EvaluateAsync<int>("a => a && a.length ? a.length : 0")
                .ConfigureAwait(false);
            if (count <= 0)
            {
                return Array.Empty<IElementHandle>();
            }

            List<IElementHandle> list = new List<IElementHandle>(count);
            for (int i = 0; i < count; i++)
            {
                IJSHandle item = await arrayHandle
                    .EvaluateHandleAsync("(a, idx) => a[idx]", i)
                    .ConfigureAwait(false);
                IElementHandle element = item?.AsElement();
                if (element != null)
                {
                    list.Add(element);
                }
            }

            return list;
        }

        private static bool HasVisibleEngine(string selector)
        {
            if (string.IsNullOrEmpty(selector) || selector.IndexOf("visible=", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            IReadOnlyList<string> parts = SplitSelectorChain(selector);
            for (int i = 0; i < parts.Count; i++)
            {
                if (TryParseVisibleEngine(parts[i], out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseVisibleEngine(string part, out bool visible)
        {
            visible = false;
            if (string.IsNullOrEmpty(part))
            {
                return false;
            }

            string trimmed = part.Trim();
            if (trimmed.Equals("visible=true", StringComparison.OrdinalIgnoreCase))
            {
                visible = true;
                return true;
            }

            if (trimmed.Equals("visible=false", StringComparison.OrdinalIgnoreCase))
            {
                visible = false;
                return true;
            }

            return false;
        }

        private static IReadOnlyList<string> SplitSelectorChain(string selector)
        {
            List<string> parts = new List<string>();
            StringBuilder current = new StringBuilder();
            char quote = '\0';
            for (int i = 0; i < selector.Length; i++)
            {
                char c = selector[i];
                if (quote != '\0')
                {
                    if (c == '\\' && i + 1 < selector.Length)
                    {
                        current.Append(c);
                        current.Append(selector[++i]);
                        continue;
                    }

                    if (c == quote)
                    {
                        quote = '\0';
                    }

                    current.Append(c);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quote = c;
                    current.Append(c);
                    continue;
                }

                if (c == '>' && i + 1 < selector.Length && selector[i + 1] == '>')
                {
                    string part = current.ToString().Trim();
                    if (part.Length > 0)
                    {
                        parts.Add(part);
                    }

                    current.Clear();
                    i++;
                    continue;
                }

                current.Append(c);
            }

            string last = current.ToString().Trim();
            if (last.Length > 0)
            {
                parts.Add(last);
            }

            return parts;
        }

        private static async Task<IReadOnlyList<IElementHandle>> QueryVisibleChainAsync(
            IFrame frame,
            IElementHandle parent,
            string selector)
        {
            IReadOnlyList<string> parts = SplitSelectorChain(selector);
            IReadOnlyList<IElementHandle> current;
            int start;
            if (parent == null)
            {
                if (parts.Count == 0)
                {
                    return Array.Empty<IElementHandle>();
                }

                if (TryParseVisibleEngine(parts[0], out _))
                {
                    throw new PlaywrightNativeException(
                        "Error: Unknown engine \"visible\" while parsing selector " + parts[0]);
                }

                current = await frame.QuerySelectorAllAsync(parts[0]).ConfigureAwait(false);
                start = 1;
            }
            else
            {
                current = new[] { parent };
                start = 0;
            }

            for (int i = start; i < parts.Count; i++)
            {
                if (TryParseVisibleEngine(parts[i], out bool wantVisible))
                {
                    current = await FilterVisibleAsync(current, wantVisible).ConfigureAwait(false);
                    continue;
                }

                List<IElementHandle> next = new List<IElementHandle>();
                for (int p = 0; p < current.Count; p++)
                {
                    IReadOnlyList<IElementHandle> matches = await current[p]
                        .QuerySelectorAllAsync(parts[i])
                        .ConfigureAwait(false);
                    AddRange(next, matches);
                }

                current = next;
            }

            return current;
        }

        private static async Task<IReadOnlyList<IElementHandle>> FilterVisibleAsync(
            IReadOnlyList<IElementHandle> source,
            bool wantVisible)
        {
            List<IElementHandle> kept = new List<IElementHandle>();
            for (int i = 0; i < source.Count; i++)
            {
                bool isVisible = await source[i].IsVisibleAsync().ConfigureAwait(false);
                if (isVisible == wantVisible)
                {
                    kept.Add(source[i]);
                }
            }

            return kept;
        }

        private static Step CreateStep(string selector)
        {
            ArgumentNullException.ThrowIfNull(selector);

            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("Selector must not be empty.", nameof(selector));
            }

            return new Step(selector, null, null, null, false);
        }

        private static Step CreateScriptStep(string script, object[] args)
        {
            ArgumentNullException.ThrowIfNull(script);
            return new Step(null, script, args ?? Array.Empty<object>(), null, false);
        }

        private static string FormatStep(Step step)
        {
            string suffix = string.Empty;
            if (step.Last)
            {
                suffix = ".last";
            }
            else if (step.Index.HasValue)
            {
                suffix = step.Index.Value == 0
                    ? ".first()"
                    : ".nth(" + step.Index.Value.ToString(CultureInfo.InvariantCulture) + ")";
            }

            if (!string.IsNullOrEmpty(step.Script))
            {
                return FormatScriptStep(step) + suffix;
            }

            if (TryFormatAnyFrameSelector(step.Selector, out string anyFrameText))
            {
                return anyFrameText + suffix;
            }

            if (TryFormatRoleSelector(step.Selector, out string roleText))
            {
                return roleText + suffix;
            }

            return "locator(" + QuoteJs(step.Selector) + ")" + suffix;
        }

        private static bool TryFormatAnyFrameSelector(string selector, out string formatted)
        {
            formatted = null;
            if (string.IsNullOrEmpty(selector) || !FrameSelector.ContainsAnyFrame(selector))
            {
                return false;
            }

            string rest = selector;
            if (rest.StartsWith(FrameSelector.AnyFrameToken, StringComparison.Ordinal))
            {
                rest = rest.Substring(FrameSelector.AnyFrameToken.Length).Trim();
                if (rest.StartsWith(">>", StringComparison.Ordinal))
                {
                    rest = rest.Substring(2).Trim();
                }
            }

            if (string.IsNullOrEmpty(rest))
            {
                formatted = "frameLocator()";
                return true;
            }

            if (TryFormatRoleSelector(rest, out string roleText))
            {
                formatted = "frameLocator()." + roleText;
                return true;
            }

            formatted = "frameLocator().locator(" + QuoteJs(rest) + ")";
            return true;
        }

        private static string FormatScriptStep(Step step)
        {
            object[] args = step.ScriptArgs ?? Array.Empty<object>();
            if (string.Equals(step.Script, GetByAllScript.FindAllByRole, StringComparison.Ordinal))
            {
                string role = args.Length > 0 ? args[0] as string : string.Empty;
                string name = args.Length > 1 ? args[1] as string : null;
                bool exact = args.Length > 2 && args[2] is bool exactValue && exactValue;
                StringBuilder options = new StringBuilder();
                if (!string.IsNullOrEmpty(name))
                {
                    options.Append("name: ").Append(QuoteJs(name));
                    if (exact)
                    {
                        options.Append(", exact: true");
                    }
                }
                else if (exact)
                {
                    options.Append("exact: true");
                }

                if (options.Length > 0)
                {
                    return "getByRole(" + QuoteJs(role ?? string.Empty) + ", { " + options + " })";
                }

                return "getByRole(" + QuoteJs(role ?? string.Empty) + ")";
            }

            if (string.Equals(step.Script, GetByAllScript.FindAllByText, StringComparison.Ordinal))
            {
                string text = args.Length > 0 ? args[0] as string : string.Empty;
                return "getByText(" + QuoteJs(text ?? string.Empty) + ")";
            }

            return "locator('internal')";
        }

        private static string QuoteJs(string value)
        {
            string escaped = (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);
            return "'" + escaped + "'";
        }

        private static string NormalizeWhiteSpace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool pendingSpace = false;
            bool started = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                {
                    if (started)
                    {
                        pendingSpace = true;
                    }

                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(value[i]);
                started = true;
            }

            return builder.ToString();
        }

        private static void AddRange(List<IElementHandle> target, IReadOnlyList<IElementHandle> source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    target.Add(source[i]);
                }
            }
        }

        private static IReadOnlyList<IElementHandle> ApplyIndex(IReadOnlyList<IElementHandle> matches, Step step)
        {
            if (step.Last)
            {
                if (matches.Count == 0)
                {
                    return Array.Empty<IElementHandle>();
                }

                return new[] { matches[matches.Count - 1] };
            }

            if (!step.Index.HasValue)
            {
                return matches;
            }

            int index = step.Index.Value;
            if (index < 0 || index >= matches.Count)
            {
                return Array.Empty<IElementHandle>();
            }

            return new[] { matches[index] };
        }

        private static Locator RequireLocator(ILocator other)
        {
            if (other is Locator locator)
            {
                return locator;
            }

            throw new ArgumentException("other must be a PlaywrightNative locator.", nameof(other));
        }

        private static async Task<string> UniqueElementKeyAsync(IElementHandle handle)
        {
            string id = await handle.EvaluateAsync<string>(TagIdFunction).ConfigureAwait(false);
            IFrame owner = await handle.OwnerFrameAsync().ConfigureAwait(false);
            return (owner != null ? owner.GetHashCode().ToString(CultureInfo.InvariantCulture) : "0") + ":" + id;
        }

        private static async Task<HashSet<string>> CollectIdsAsync(IReadOnlyList<IElementHandle> handles)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (IElementHandle handle in handles)
            {
                ids.Add(await UniqueElementKeyAsync(handle).ConfigureAwait(false));
            }

            return ids;
        }

        private static async Task<IReadOnlyList<IElementHandle>> KeepIdsAsync(IReadOnlyList<IElementHandle> handles, HashSet<string> ids)
        {
            List<IElementHandle> kept = new List<IElementHandle>();
            foreach (IElementHandle handle in handles)
            {
                string id = await UniqueElementKeyAsync(handle).ConfigureAwait(false);
                if (ids.Contains(id))
                {
                    kept.Add(handle);
                }
            }

            return kept;
        }

        private static async Task AppendUniqueAsync(List<IElementHandle> target, HashSet<string> seen, IElementHandle handle)
        {
            if (seen.Add(await UniqueElementKeyAsync(handle).ConfigureAwait(false)))
            {
                target.Add(handle);
            }
        }

        private static bool IsFrameScopeTransient(Exception ex)
        {
            if (DomVisibility.IsTransientVisibilityError(ex))
            {
                return true;
            }

            string message = ex?.Message ?? string.Empty;
            return ex is TimeoutException
                || message.Contains("Missing injected script", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Execution context", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task ThrowIfHostIsNotFrameAsync(IElementHandle host)
        {
            string nodeName = await host.EvaluateAsync<string>("el => el && el.nodeName ? String(el.nodeName) : ''").ConfigureAwait(false);
            if (string.Equals(nodeName, "IFRAME", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, "FRAME", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string html = await host.EvaluateAsync<string>("el => el.outerHTML").ConfigureAwait(false);
            throw new PlaywrightNativeException((html ?? string.Empty) + "\n<iframe> was expected");
        }

        private static async Task<ILocator> NormalizeFrameHostAsync(IElementHandle handle, string tag, IFrame frame)
        {
            string name = await handle.GetAttributeAsync("name").ConfigureAwait(false);
            string selector = !string.IsNullOrEmpty(name)
                ? tag + "[name=\"" + name + "\"]"
                : tag;
            ILocator loc = new Locator(frame, selector);
            IReadOnlyList<IElementHandle> matches = await frame.QuerySelectorAllAsync(selector).ConfigureAwait(false);
            if (matches.Count <= 1)
            {
                return loc;
            }

            string want = await handle.EvaluateAsync<string>(TagIdFunction).ConfigureAwait(false);
            for (int i = 0; i < matches.Count; i++)
            {
                string got = await matches[i].EvaluateAsync<string>(TagIdFunction).ConfigureAwait(false);
                if (string.Equals(want, got, StringComparison.Ordinal))
                {
                    return i == 0 ? loc.First : loc.Nth(i);
                }
            }

            return loc;
        }

        private static bool TryFormatRoleSelector(string selector, out string formatted)
        {
            formatted = null;
            if (string.IsNullOrEmpty(selector) || !selector.StartsWith("internal:role=", StringComparison.Ordinal))
            {
                return false;
            }

            string rest = selector.Substring("internal:role=".Length);
            int bracket = rest.IndexOf('[');
            string role = bracket < 0 ? rest : rest.Substring(0, bracket);
            string name = null;
            if (bracket >= 0)
            {
                Match nameMatch = Regex.Match(rest, "\\[name=\"((?:\\\\.|[^\"])*)\"");
                if (nameMatch.Success)
                {
                    name = nameMatch.Groups[1].Value
                        .Replace("\\\"", "\"", StringComparison.Ordinal)
                        .Replace("\\\\", "\\", StringComparison.Ordinal);
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                formatted = "getByRole(" + QuoteJs(role) + ", { name: " + QuoteJs(name) + " })";
            }
            else
            {
                formatted = "getByRole(" + QuoteJs(role) + ")";
            }

            return true;
        }

        private Locator RequireSameFrame(ILocator other)
        {
            Locator locator = RequireLocator(other);
            if (!ReferenceEquals(locator._frame, _frame))
            {
                throw new PlaywrightNativeException("Locators must belong to the same frame.");
            }

            return locator;
        }

        private Locator RequireInnerLocator(ILocator other, string optionName)
        {
            Locator locator = RequireLocator(other);
            if (!ReferenceEquals(locator._frame, _frame))
            {
                throw new PlaywrightNativeException(
                    "Inner \"" + optionName + "\" locator must belong to the same frame.");
            }

            return locator;
        }

        private async Task WaitForStateAsync(WaitForSelectorState state, float? timeout)
        {
            WaitForSelectorState wanted = state == EnumCompat.UndefinedWaitForSelectorState
                ? WaitForSelectorState.Visible
                : state;
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();

            while (true)
            {
                await LocatorHandlers.RunAsync(Page, timeout).ConfigureAwait(false);
                IReadOnlyList<IElementHandle> all;
                try
                {
                    all = await ResolveAllAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex) when (PlaywrightNativeException.IsDestroyedContext(ex))
                {
                    all = Array.Empty<IElementHandle>();
                }

                bool done = false;

                switch (wanted)
                {
                    case WaitForSelectorState.Detached:
                        done = all.Count == 0;
                        break;

                    case WaitForSelectorState.Hidden:
                        done = true;
                        foreach (IElementHandle handle in all)
                        {
                            if (!await handle.IsHiddenAsync().ConfigureAwait(false))
                            {
                                done = false;
                                break;
                            }
                        }

                        break;

                    case WaitForSelectorState.Attached:
                        if (all.Count > 1)
                        {
                            throw new PlaywrightNativeException(
                                await StrictResolvedMessageAsync(all).ConfigureAwait(false));
                        }

                        done = all.Count == 1;
                        break;

                    default:
                        if (all.Count > 1)
                        {
                            throw new PlaywrightNativeException(
                                await StrictResolvedMessageAsync(all).ConfigureAwait(false));
                        }

                        done = all.Count == 1 && await all[0].IsVisibleAsync().ConfigureAwait(false);
                        break;
                }

                if (done)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException(
                        "locator.waitFor: Timeout " +
                        timeoutMs.ToString(CultureInfo.InvariantCulture) +
                        "ms exceeded.");
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private async Task<bool> MatchesSameElementAsync(ILocator candidate, IElementHandle original)
        {
            if (await candidate.CountAsync().ConfigureAwait(false) != 1)
            {
                return false;
            }

            IReadOnlyList<IElementHandle> matches = await candidate.ElementHandlesAsync().ConfigureAwait(false);
            if (matches.Count != 1)
            {
                return false;
            }

            string originalId = await original.EvaluateAsync<string>(TagIdFunction).ConfigureAwait(false);
            string matchId = await matches[0].EvaluateAsync<string>(TagIdFunction).ConfigureAwait(false);
            return string.Equals(originalId, matchId, StringComparison.Ordinal);
        }

        private async Task<IElementHandle> WaitForHandleAsync(float? timeout, string apiName, bool skipHandlers = false)
        {
            if (TryAriaRefBody(out string ariaRef))
            {
                IElementHandle found = await ResolveOneOrNullAsync().ConfigureAwait(false);
                if (found == null)
                {
                    throw new PlaywrightNativeException("No element matching aria-ref=" + ariaRef);
                }

                return found;
            }

            try
            {
                return await GetByWaiter.WaitAsync(
                    async () =>
                    {
                        if (!skipHandlers)
                        {
                            await LocatorHandlers.RunAsync(Page, timeout).ConfigureAwait(false);
                        }

                        return await ResolveOneOrNullAsync().ConfigureAwait(false);
                    },
                    timeout,
                    apiName).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                if (ex.Message.Contains("locator handler", StringComparison.Ordinal))
                {
                    throw;
                }

                throw new PlaywrightNativeException(ex.Message + "\nwaiting for " + ToString(), ex);
            }
        }

        private async Task<IElementHandle> ResolveOneOrNullAsync()
        {
            IReadOnlyList<IElementHandle> all = await ResolveAllAsync().ConfigureAwait(false);
            if (all.Count == 0)
            {
                return null;
            }

            if (all.Count > 1)
            {
                if (IsAnyFrameLocator() && await FrameSelector.FromMultipleFramesAsync(all).ConfigureAwait(false))
                {
                    throw MultipleFramesException();
                }

                if (IsPierceLocator() && await FrameSelector.FromMultipleFramesAsync(all).ConfigureAwait(false))
                {
                    throw new PlaywrightNativeException("Pierce-frame mode matched elements from multiple frames");
                }

                string strict = await StrictResolvedMessageAsync(all).ConfigureAwait(false);
                if (IsAnyFrameLocator())
                {
                    strict = strict + "\nwaiting for " + ToString();
                }

                throw new PlaywrightNativeException(strict);
            }

            return all[0];
        }

        private async Task<IReadOnlyList<IElementHandle>> ResolveAllAsync(IElementHandle root = null)
        {
            ThrowIfUnknownSelectorEngine();
            if (ContainsCapture() && HasNth())
            {
                throw new PlaywrightNativeException("Can't query n-th element");
            }

            if (_combine != CombineKind.None)
            {
                try
                {
                    IReadOnlyList<IElementHandle> combined = await ResolveCombinedAsync(root).ConfigureAwait(false);
                    IReadOnlyList<IElementHandle> sliced = ApplyIndex(combined, new Step(null, null, null, _sliceIndex, _sliceLast));
                    if (root == null)
                    {
                        await ThrowIfAnyFrameMultipleAsync(sliced).ConfigureAwait(false);
                    }

                    return sliced;
                }
                catch (Exception ex) when (FrameScopeLocator() != null && IsFrameScopeTransient(ex))
                {
                    return Array.Empty<IElementHandle>();
                }
            }

            if (_anyFrame && root == null && _scope == null)
            {
                IReadOnlyList<IElementHandle> any = await QueryAnyFrameTreeAsync(_frame, null).ConfigureAwait(false);
                await ThrowIfAnyFrameMultipleAsync(any).ConfigureAwait(false);
                return any;
            }

            if (root != null)
            {
                IReadOnlyList<IElementHandle> scoped = new[] { root };
                for (int i = 0; i < _steps.Count; i++)
                {
                    Step step = _steps[i];
                    List<IElementHandle> next = new List<IElementHandle>();
                    for (int p = 0; p < scoped.Count; p++)
                    {
                        IReadOnlyList<IElementHandle> matches = await QueryStepAsync(_frame, scoped[p], step).ConfigureAwait(false);
                        AddRange(next, matches);
                    }

                    scoped = ApplyIndex(next, step);
                }

                return scoped;
            }

            if (_scope != null)
            {
                try
                {
                    IFrame content = await ResolveContentFrameAsync().ConfigureAwait(false);
                    if (content == null)
                    {
                        return Array.Empty<IElementHandle>();
                    }

                    return await QueryStepsInFrameAsync(content).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsFrameScopeTransient(ex))
                {
                    return Array.Empty<IElementHandle>();
                }
            }

            IReadOnlyList<IElementHandle> inFrame = await QueryStepsInFrameAsync(_frame).ConfigureAwait(false);
            if (root == null)
            {
                await ThrowIfAnyFrameMultipleAsync(inFrame).ConfigureAwait(false);
            }

            return inFrame;
        }

        private async Task<IFrame> ResolveContentFrameAsync()
        {
            IReadOnlyList<IElementHandle> hosts = await _scope.ResolveAllAsync().ConfigureAwait(false);
            if (hosts.Count == 0)
            {
                return null;
            }

            if (hosts.Count > 1)
            {
                if (_scope.IsAnyFrameLocator() && await FrameSelector.FromMultipleFramesAsync(hosts).ConfigureAwait(false))
                {
                    throw MultipleFramesException();
                }

                throw new PlaywrightNativeException(
                    "Error: strict mode violation: " +
                    _scope.ToString() +
                    " resolved to " +
                    hosts.Count.ToString(CultureInfo.InvariantCulture) +
                    " elements (frame locator)");
            }

            IFrame content;
            try
            {
                content = await hosts[0].ContentFrameAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsFrameScopeTransient(ex))
            {
                return null;
            }

            if (content != null)
            {
                return content;
            }

            try
            {
                await ThrowIfHostIsNotFrameAsync(hosts[0]).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsFrameScopeTransient(ex))
            {
                return null;
            }

            return null;
        }

        private async Task<IReadOnlyList<IElementHandle>> QueryStepsInFrameAsync(IFrame frame, bool ariaDescendants = true)
        {
            IReadOnlyList<IElementHandle> current = null;

            for (int i = 0; i < _steps.Count; i++)
            {
                Step step = _steps[i];
                List<IElementHandle> next = new List<IElementHandle>();

                if (current == null)
                {
                    IReadOnlyList<IElementHandle> matches = await QueryStepAsync(frame, null, step, ariaDescendants).ConfigureAwait(false);
                    AddRange(next, matches);
                }
                else
                {
                    for (int p = 0; p < current.Count; p++)
                    {
                        IReadOnlyList<IElementHandle> matches = await QueryStepAsync(frame, current[p], step, ariaDescendants).ConfigureAwait(false);
                        AddRange(next, matches);
                    }
                }

                current = ApplyIndex(next, step);
            }

            return current ?? Array.Empty<IElementHandle>();
        }

        private async Task<IReadOnlyList<IElementHandle>> QueryAnyFrameTreeAsync(IFrame frame, IElementHandle scope)
        {
            List<IElementHandle> results = new List<IElementHandle>();
            if (!FrameSelector.HasQueryableContext(frame))
            {
                return results;
            }

            try
            {
                if (scope == null)
                {
                    AddRange(results, await QueryStepsInFrameAsync(frame, ariaDescendants: false).ConfigureAwait(false));
                }
                else
                {
                    IReadOnlyList<IElementHandle> scoped = new[] { scope };
                    for (int i = 0; i < _steps.Count; i++)
                    {
                        Step step = _steps[i];
                        List<IElementHandle> next = new List<IElementHandle>();
                        for (int p = 0; p < scoped.Count; p++)
                        {
                            IReadOnlyList<IElementHandle> matches = await QueryStepAsync(frame, scoped[p], step, ariaDescendants: false).ConfigureAwait(false);
                            AddRange(next, matches);
                        }

                        scoped = ApplyIndex(next, step);
                    }

                    AddRange(results, scoped);
                }
            }
            catch (Exception ex) when (IsFrameScopeTransient(ex))
            {
            }

            IReadOnlyList<IElementHandle> childHosts;
            try
            {
                childHosts = scope == null
                    ? await frame.QuerySelectorAllAsync("iframe, frame").ConfigureAwait(false)
                    : await scope.QuerySelectorAllAsync("iframe, frame").ConfigureAwait(false);
            }
            catch (Exception ex) when (IsFrameScopeTransient(ex) || PlaywrightNativeException.IsDestroyedContext(ex))
            {
                childHosts = Array.Empty<IElementHandle>();
            }

            for (int i = 0; i < childHosts.Count; i++)
            {
                IFrame child = await childHosts[i].ContentFrameAsync().ConfigureAwait(false);
                if (child != null)
                {
                    AddRange(results, await QueryAnyFrameTreeAsync(child, null).ConfigureAwait(false));
                }
            }

            return results;
        }

        private async Task<IReadOnlyList<IElementHandle>> ResolveCombinedAsync(IElementHandle root = null)
        {
            if (root == null && (_combine == CombineKind.And || _combine == CombineKind.Or || _combine == CombineKind.Has || _combine == CombineKind.HasNot))
            {
                ThrowIfCompositeContainsAnyFrame();
                bool anyFrameOr = _combine == CombineKind.Or
                    && _left != null
                    && _left.IsAnyFrameLocator()
                    && _right != null
                    && !_right.ContainsAnyFrameToken();
                if (!anyFrameOr)
                {
                    ThrowIfMismatchedFrameLocators();
                }
            }

            switch (_combine)
            {
                case CombineKind.HasText:
                    return await FilterByTextAsync(exclude: false, root).ConfigureAwait(false);

                case CombineKind.And:
                    {
                        IReadOnlyList<IElementHandle> left = await _left.ResolveAllAsync(root).ConfigureAwait(false);
                        IReadOnlyList<IElementHandle> right = await _right.ResolveAllAsync(root).ConfigureAwait(false);
                        HashSet<string> rightIds = await CollectIdsAsync(right).ConfigureAwait(false);
                        return await KeepIdsAsync(left, rightIds).ConfigureAwait(false);
                    }

                case CombineKind.Or:
                    return await UnionAsync(root).ConfigureAwait(false);

                case CombineKind.Has:
                    return await FilterHasAsync(exclude: false, root).ConfigureAwait(false);

                case CombineKind.HasNot:
                    return await FilterHasAsync(exclude: true, root).ConfigureAwait(false);

                case CombineKind.HasNotText:
                    return await FilterByTextAsync(exclude: true, root).ConfigureAwait(false);

                case CombineKind.Inside:
                    return await FilterInsideAsync().ConfigureAwait(false);

                case CombineKind.Visible:
                    return await FilterByVisibleAsync().ConfigureAwait(false);

                default:
                    return Array.Empty<IElementHandle>();
            }
        }

        private async Task<IReadOnlyList<IElementHandle>> FilterByTextAsync(bool exclude, IElementHandle root = null)
        {
            IReadOnlyList<IElementHandle> source = await _left.ResolveAllAsync(root).ConfigureAwait(false);
            List<IElementHandle> kept = new List<IElementHandle>();
            foreach (IElementHandle handle in source)
            {
                string text = await handle.TextContentAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(text))
                {
                    text = await handle.InnerTextAsync().ConfigureAwait(false);
                }

                bool contains = _hasTextRegex != null
                    ? text != null && _hasTextRegex.IsMatch(text)
                    : text != null && NormalizeWhiteSpace(text).Contains(NormalizeWhiteSpace(_hasText), StringComparison.OrdinalIgnoreCase);
                if (contains != exclude)
                {
                    kept.Add(handle);
                }
            }

            return kept;
        }

        private async Task<IReadOnlyList<IElementHandle>> FilterByVisibleAsync()
        {
            IReadOnlyList<IElementHandle> source = await _left.ResolveAllAsync().ConfigureAwait(false);
            List<IElementHandle> kept = new List<IElementHandle>();
            bool wantVisible = _visible != false;
            foreach (IElementHandle handle in source)
            {
                bool isVisible = await handle.IsVisibleAsync().ConfigureAwait(false);
                if (isVisible == wantVisible)
                {
                    kept.Add(handle);
                }
            }

            return kept;
        }

        private async Task<IReadOnlyList<IElementHandle>> UnionAsync(IElementHandle root = null)
        {
            IReadOnlyList<IElementHandle> left = await _left.ResolveAllAsync(root).ConfigureAwait(false);
            Locator rightLocator = _right;
            if (root == null
                && _left != null
                && _left.IsAnyFrameLocator()
                && _right != null
                && !_right.ContainsAnyFrameToken())
            {
                rightLocator = _right.WithAnyFrame();
            }

            IReadOnlyList<IElementHandle> right = await rightLocator.ResolveAllAsync(root).ConfigureAwait(false);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<IElementHandle> union = new List<IElementHandle>();
            foreach (IElementHandle handle in left)
            {
                await AppendUniqueAsync(union, seen, handle).ConfigureAwait(false);
            }

            foreach (IElementHandle handle in right)
            {
                await AppendUniqueAsync(union, seen, handle).ConfigureAwait(false);
            }

            return await OrderByDocumentPositionAsync(union).ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<IElementHandle>> OrderByDocumentPositionAsync(List<IElementHandle> handles)
        {
            if (handles == null || handles.Count <= 1)
            {
                return handles ?? (IReadOnlyList<IElementHandle>)Array.Empty<IElementHandle>();
            }

            List<(int Position, IElementHandle Handle)> ranked = new List<(int, IElementHandle)>(handles.Count);
            foreach (IElementHandle handle in handles)
            {
                int position = await handle.EvaluateAsync<int>(
                    @"el => {
                        const all = el.ownerDocument.querySelectorAll('*');
                        for (let i = 0; i < all.length; i++) {
                          if (all[i] === el) return i;
                        }
                        return -1;
                    }").ConfigureAwait(false);
                ranked.Add((position, handle));
            }

            ranked.Sort(static (left, right) => left.Position.CompareTo(right.Position));
            List<IElementHandle> ordered = new List<IElementHandle>(ranked.Count);
            for (int i = 0; i < ranked.Count; i++)
            {
                ordered.Add(ranked[i].Handle);
            }

            return ordered;
        }

        private async Task<IReadOnlyList<IElementHandle>> FilterHasAsync(bool exclude, IElementHandle root = null)
        {
            IReadOnlyList<IElementHandle> left = await _left.ResolveAllAsync(root).ConfigureAwait(false);
            List<IElementHandle> kept = new List<IElementHandle>();
            foreach (IElementHandle handle in left)
            {
                IReadOnlyList<IElementHandle> inside = await _right.ResolveAllAsync(handle).ConfigureAwait(false);
                bool contains = inside != null && inside.Count > 0;
                if (contains != exclude)
                {
                    kept.Add(handle);
                }
            }

            return kept;
        }

        private async Task<IReadOnlyList<IElementHandle>> FilterInsideAsync()
        {
            IReadOnlyList<IElementHandle> ancestors = await _left.ResolveAllAsync().ConfigureAwait(false);
            IReadOnlyList<IElementHandle> candidates = await _right.ResolveAllAsync().ConfigureAwait(false);

            // Official locator.locator(getBy*) is same-document. Compare
            // __pwLocId, not UniqueElementKey (frameHash:id) — the walk
            // below reads node.__pwLocId only.
            HashSet<string> ancestorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (IElementHandle ancestor in ancestors)
            {
                ancestorIds.Add(await ancestor.EvaluateAsync<string>(TagIdFunction).ConfigureAwait(false));
            }

            string[] idArray = new string[ancestorIds.Count];
            ancestorIds.CopyTo(idArray);
            List<IElementHandle> kept = new List<IElementHandle>();
            foreach (IElementHandle handle in candidates)
            {
                bool inside = await handle.EvaluateAsync<bool>(
                    @"(el, ids) => {
                        const list = ids || [];
                        let node = el;
                        while (node) {
                          if (node.__pwLocId != null && list.indexOf(String(node.__pwLocId)) !== -1)
                            return true;
                          node = node.parentElement;
                        }
                        return false;
                    }",
                    idArray).ConfigureAwait(false);
                if (inside)
                {
                    kept.Add(handle);
                }
            }

            return kept;
        }

        private Task<string> StrictResolvedMessageAsync(IReadOnlyList<IElementHandle> all)
            => StrictModeViolation.FormatAsync(ToString(), all);

        private bool TryGetSimpleSelector(out string selector)
        {
            selector = null;
            if (_combine != CombineKind.None || _scope != null || _steps.Count != 1)
            {
                return false;
            }

            Step step = _steps[0];
            if (string.IsNullOrEmpty(step.Selector) || step.Script != null || step.Index.HasValue || step.Last)
            {
                return false;
            }

            selector = step.Selector;
            return true;
        }

        private async Task<T> EvaluateAllResolvedAsync<T>(string expression, object arg, IReadOnlyList<IElementHandle> resolved = null)
        {
            IReadOnlyList<IElementHandle> all = resolved ?? await ResolveAllAsync().ConfigureAwait(false);
            IJSHandle array = await CreateResolvedArrayHandleAsync(all).ConfigureAwait(false);
            return await EvalOnSelector.OnArrayAsync<T>(Task.FromResult(array), expression, arg).ConfigureAwait(false);
        }

        private async Task<IJSHandle> CreateResolvedArrayHandleAsync(IReadOnlyList<IElementHandle> all)
        {
            if (all == null || all.Count == 0)
            {
                return await _frame.EvaluateHandleAsync("[]").ConfigureAwait(false);
            }

            string key = "__pwEvalAll" + Guid.NewGuid().ToString("N");
            await all[0].EvaluateAsync<object>("(el, k) => { el.ownerDocument.defaultView[k] = []; }", key).ConfigureAwait(false);
            for (int i = 0; i < all.Count; i++)
            {
                await all[i].EvaluateAsync<object>("(el, k) => { el.ownerDocument.defaultView[k].push(el); }", key).ConfigureAwait(false);
            }

            return await all[0].EvaluateHandleAsync(
                @"(el, k) => {
                    const result = el.ownerDocument.defaultView[k] || [];
                    delete el.ownerDocument.defaultView[k];
                    return result;
                }",
                key).ConfigureAwait(false);
        }

        private ILocator Inside(Locator inner)
            => new Locator(this, inner, null, CombineKind.Inside, description: _description);

        private ILocator Narrow(int? index, bool last)
        {
            if (_combine != CombineKind.None)
            {
                return new Locator(_left, _right, _hasText, _combine, index, last, _description, _hasTextRegex, _visible);
            }

            List<Step> next = CopySteps();
            int lastIndex = next.Count - 1;
            Step current = next[lastIndex];
            next[lastIndex] = new Step(current.Selector, current.Script, current.ScriptArgs, index, last);
            return new Locator(_frame, next, _scope, _description, _anyFrame);
        }

        private List<Step> CopySteps()
        {
            List<Step> copy = new List<Step>(_steps.Count);
            for (int i = 0; i < _steps.Count; i++)
            {
                copy.Add(_steps[i]);
            }

            return copy;
        }

        private string FormatLocator()
        {
            if (_combine != CombineKind.None)
            {
                return FormatCombined();
            }

            StringBuilder builder = new StringBuilder();
            if (_scope != null)
            {
                builder.Append(_scope.ToString());
                builder.Append(".contentFrame()");
            }
            else if (_anyFrame)
            {
                builder.Append("frameLocator()");
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append('.');
                }

                builder.Append(FormatStep(_steps[i]));
            }

            return builder.Length == 0 ? "locator('unknown')" : builder.ToString();
        }

        private string FormatCombined()
        {
            string suffix = FormatSliceSuffix();
            if (_combine == CombineKind.Or && _left != null && _right != null)
            {
                return _left.ToString() + ".or(" + _right.ToString() + ")" + suffix;
            }

            if (_combine == CombineKind.And && _left != null && _right != null)
            {
                return _left.ToString() + ".and(" + _right.ToString() + ")" + suffix;
            }

            if (_combine == CombineKind.Inside && _left != null && _right != null)
            {
                return _left.ToString() + ".locator(" + QuoteJs(_right.ToString()) + ")" + suffix;
            }

            if (_left != null)
            {
                return _left.ToString() + suffix;
            }

            return "locator('unknown')";
        }

        private string FormatSliceSuffix()
        {
            if (_sliceLast)
            {
                return ".last";
            }

            if (_sliceIndex.HasValue)
            {
                return ".nth(" + _sliceIndex.Value.ToString(CultureInfo.InvariantCulture) + ")";
            }

            return string.Empty;
        }

        private void ThrowIfUnknownSelectorEngine()
        {
            if (_scope != null)
            {
                _scope.ThrowIfUnknownSelectorEngine();
            }

            if (_left != null)
            {
                _left.ThrowIfUnknownSelectorEngine();
            }

            if (_right != null)
            {
                _right.ThrowIfUnknownSelectorEngine();
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                string selector = _steps[i].Selector;
                if (!string.IsNullOrEmpty(selector))
                {
                    DomVisibility.ThrowIfUnknownEngine(selector);
                }
            }
        }

        private Locator ApplyCommonFramePrefix(Locator inner)
        {
            if (inner == null || inner._combine != CombineKind.None || inner._scope == null)
            {
                return inner;
            }

            IReadOnlyList<string> innerFrames = CollectFramePrefix(inner);
            IReadOnlyList<string> outerFrames = CollectFramePrefix(this);
            if (innerFrames.Count == 0 || outerFrames.Count < innerFrames.Count)
            {
                return inner;
            }

            for (int i = 0; i < innerFrames.Count; i++)
            {
                if (!string.Equals(Normalize(innerFrames[i]), Normalize(outerFrames[i]), StringComparison.Ordinal))
                {
                    return inner;
                }
            }

            if (PrefixHasCapture(inner._scope))
            {
                throw new PlaywrightNativeException("Can not capture the selector before diving into the frame. Only use * after the last frame has been selected");
            }

            return new Locator(inner._frame, inner._steps, null, inner._description, inner._anyFrame);

            static IReadOnlyList<string> CollectFramePrefix(Locator locator)
            {
                List<string> reversed = new List<string>();
                Locator scope = locator?._scope;
                while (scope != null)
                {
                    if (scope._steps.Count > 0 && !string.IsNullOrEmpty(scope._steps[0].Selector))
                    {
                        reversed.Add(scope._steps[0].Selector);
                    }

                    scope = scope._scope;
                }

                reversed.Reverse();
                return reversed;
            }

            static bool PrefixHasCapture(Locator scope)
            {
                Locator current = scope;
                while (current != null)
                {
                    for (int i = 0; i < current._steps.Count; i++)
                    {
                        if (SelectorQuery.HasCapture(current._steps[i].Selector))
                        {
                            return true;
                        }
                    }

                    current = current._scope;
                }

                return false;
            }

            static string Normalize(string selector)
            {
                string trimmed = (selector ?? string.Empty).Trim();
                if (trimmed.Length > 0 && trimmed[0] == '*')
                {
                    trimmed = trimmed.Substring(1);
                }

                if (trimmed.StartsWith("css=", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(4);
                }

                return trimmed;
            }
        }

        private bool ContainsCapture()
        {
            if (_combine != CombineKind.None)
            {
                return (_left != null && _left.ContainsCapture())
                    || (_right != null && _right.ContainsCapture());
            }

            if (_scope != null && _scope.ContainsCapture())
            {
                return true;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (SelectorQuery.HasCapture(_steps[i].Selector))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPierceLocator()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (FrameSelector.ContainsPierce(_steps[i].Selector))
                {
                    return true;
                }
            }

            return _scope != null && _scope.IsPierceLocator();
        }

        private bool IsAnyFrameLocator()
        {
            if (_anyFrame)
            {
                return true;
            }

            if (_combine != CombineKind.None)
            {
                return (_left != null && _left.IsAnyFrameLocator())
                    || (_right != null && _right.IsAnyFrameLocator());
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (FrameSelector.ContainsAnyFrame(_steps[i].Selector))
                {
                    return true;
                }
            }

            return _scope != null && _scope.IsAnyFrameLocator();
        }

        private bool ContainsAnyFrameToken()
        {
            if (_anyFrame)
            {
                return true;
            }

            if (_combine != CombineKind.None)
            {
                return (_left != null && _left.ContainsAnyFrameToken())
                    || (_right != null && _right.ContainsAnyFrameToken());
            }

            if (_scope != null && _scope.ContainsAnyFrameToken())
            {
                return true;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (FrameSelector.ContainsAnyFrame(_steps[i].Selector)
                    || (!string.IsNullOrEmpty(_steps[i].Selector)
                        && _steps[i].Selector.Contains(FrameSelector.AnyFrameToken, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private PlaywrightNativeException MultipleFramesException()
        {
            string locator = ToString();
            return new PlaywrightNativeException(
                "frameLocator() matched elements in multiple frames\nLocator: " +
                locator +
                "\nwaiting for " +
                locator);
        }

        private async Task ThrowIfAnyFrameMultipleAsync(IReadOnlyList<IElementHandle> handles)
        {
            if (!IsAnyFrameLocator())
            {
                return;
            }

            if (await FrameSelector.FromMultipleFramesAsync(handles).ConfigureAwait(false))
            {
                throw MultipleFramesException();
            }
        }

        private void ThrowIfCompositeContainsAnyFrame()
        {
            if (_right != null && _right.ContainsAnyFrameToken())
            {
                throw new PlaywrightNativeException(
                    "frameLocator() is not allowed inside composite locators, while querying \"" +
                    ToString() +
                    "\"");
            }
        }

        private bool HasNth()
        {
            if (_sliceIndex.HasValue || _sliceLast)
            {
                return true;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Index.HasValue || _steps[i].Last)
                {
                    return true;
                }
            }

            return false;
        }

        private Locator FrameScopeLocator()
        {
            if (_scope != null)
            {
                return _scope;
            }

            if (_combine != CombineKind.None)
            {
                Locator leftScope = _left?.FrameScopeLocator();
                if (leftScope != null)
                {
                    return leftScope;
                }

                return _right?.FrameScopeLocator();
            }

            return null;
        }

        private void ThrowIfMismatchedFrameLocators()
        {
            Locator leftScope = _left?.FrameScopeLocator();
            Locator rightScope = _right?.FrameScopeLocator();
            if (leftScope == null && rightScope == null)
            {
                return;
            }

            if (leftScope != null
                && rightScope != null
                && string.Equals(leftScope.ToString(), rightScope.ToString(), StringComparison.Ordinal))
            {
                return;
            }

            throw new PlaywrightNativeException(
                "Frame locators are not allowed inside composite locators, while querying \"" +
                ToString() +
                "\"");
        }

        private bool TryAriaRefBody(out string ariaRef)
        {
            ariaRef = null;
            if (_combine != CombineKind.None || _scope != null || _steps.Count != 1)
            {
                return false;
            }

            return AriaSnapshotAi.TryParse(_steps[0].Selector, out ariaRef);
        }

        private async Task<ILocator> WrapWithParentFramesAsync(ILocator local, IElementHandle handle)
        {
            Locator current = RequireLocator(local);
            IFrame frame = await handle.OwnerFrameAsync().ConfigureAwait(false) ?? _frame;
            while (frame != null && frame.ParentFrame != null)
            {
                IElementHandle host;
                try
                {
                    host = await FrameElementHelper.ResolveAsync(frame).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                    break;
                }

                string tag = await host.EvaluateAsync<string>("el => (el && el.tagName) ? String(el.tagName).toLowerCase() : 'iframe'").ConfigureAwait(false);
                ILocator hostLoc = await NormalizeFrameHostAsync(host, string.IsNullOrEmpty(tag) ? "iframe" : tag, frame.ParentFrame).ConfigureAwait(false);
                if (hostLoc == null)
                {
                    break;
                }

                current = current.RebaseInto(RequireLocator(hostLoc));
                frame = frame.ParentFrame;
            }

            return current;
        }

        private Locator RebaseInto(Locator host)
        {
            if (_scope == null)
            {
                return new Locator(host._frame, _steps, host, description: null);
            }

            Locator rebasedScope = _scope.RebaseInto(host);
            return new Locator(host._frame, _steps, rebasedScope, description: null);
        }

        private readonly struct Step
        {
            internal Step(string selector, string script, object[] scriptArgs, int? index, bool last)
            {
                Selector = selector;
                Script = script;
                ScriptArgs = scriptArgs;
                Index = index;
                Last = last;
            }

            internal string Selector { get; }

            internal string Script { get; }

            internal object[] ScriptArgs { get; }

            internal int? Index { get; }

            internal bool Last { get; }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<string> ILocator.AriaSnapshotAsync(LocatorAriaSnapshotOptions options)
            => AriaSnapshotAsync(
                options?.Timeout,
                options?.Mode ?? AriaSnapshotMode.Default,
                options?.Depth,
                options?.Boxes);

        Task ILocator.BlurAsync(LocatorBlurOptions options) => BlurAsync(options?.Timeout);

        async Task<LocatorBoundingBoxResult> ILocator.BoundingBoxAsync(LocatorBoundingBoxOptions options)
        {
            ElementHandleBoundingBoxResult box = await BoundingBoxAsync(options?.Timeout).ConfigureAwait(false);
            return box.AsLocatorBoundingBox();
        }

        Task ILocator.CheckAsync(LocatorCheckOptions options)
            => CheckAsync(options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task ILocator.ClearAsync(LocatorClearOptions options)
            => ClearAsync(options?.NoWaitAfter, options?.Timeout, options?.Force);

        Task ILocator.ClickAsync(LocatorClickOptions options)
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

        Task ILocator.DblClickAsync(LocatorDblClickOptions options)
            => DblClickAsync(
                options?.Button ?? default,
                options?.Delay,
                options?.Position,
                options?.Modifiers,
                options?.Force,
                options?.NoWaitAfter,
                options?.Timeout,
                options?.Trial);

        Task ILocator.DispatchEventAsync(string type, object eventInit, LocatorDispatchEventOptions options)
            => DispatchEventAsync(type, eventInit, options?.Timeout);

        Task ILocator.DragToAsync(ILocator target, LocatorDragToOptions options)
            => DragToAsync(
                target,
                options?.SourcePosition == null ? null : new Position { X = options.SourcePosition.X, Y = options.SourcePosition.Y },
                options?.TargetPosition == null ? null : new Position { X = options.TargetPosition.X, Y = options.TargetPosition.Y },
                options?.Force,
                options?.NoWaitAfter,
                options?.Timeout,
                options?.Trial,
                options?.Steps);

        Task ILocator.DropAsync(DropPayload payload, LocatorDropOptions options) => Task.CompletedTask;

        Task<IElementHandle> ILocator.ElementHandleAsync(LocatorElementHandleOptions options)
            => ElementHandleAsync(options?.Timeout);

        Task<JsonElement?> ILocator.EvaluateAsync(string expression, object arg, LocatorEvaluateOptions options)
            => EvaluateAsync<JsonElement?>(expression, arg, options?.Timeout);

        Task<T> ILocator.EvaluateAsync<T>(string expression, object arg, LocatorEvaluateOptions options)
            => EvaluateAsync<T>(expression, arg, options?.Timeout);

        Task<IJSHandle> ILocator.EvaluateHandleAsync(string expression, object arg, LocatorEvaluateHandleOptions options)
            => EvaluateHandleAsync(expression, arg, options?.Timeout);

        Task ILocator.FillAsync(string value, LocatorFillOptions options)
            => FillAsync(value, options?.NoWaitAfter, options?.Timeout, options?.Force);

        ILocator ILocator.Filter(LocatorFilterOptions options)
        {
            options ??= new LocatorFilterOptions();
            ILocator result = this;
            if (options.HasText != null || options.HasTextString != null)
            {
                result = result.Filter(options.HasText ?? options.HasTextString);
            }

            if (options.HasTextRegex != null)
            {
                result = result.Filter(options.HasTextRegex);
            }

            if (options.Visible.HasValue)
            {
                result = result.Filter(options.Visible.Value);
            }

            return SelectorQuery.ApplyOptions(
                result,
                options.Has,
                null,
                null,
                options.HasNot,
                options.HasNotText ?? options.HasNotTextString,
                options.HasNotTextRegex);
        }

        Task ILocator.FocusAsync(LocatorFocusOptions options)
            => FocusAsync(options?.Timeout);

        Task<string> ILocator.GetAttributeAsync(string name, LocatorGetAttributeOptions options) => GetAttributeAsync(name, options?.Timeout);

        ILocator ILocator.GetByAltText(string text, LocatorGetByAltTextOptions options) => GetByAltText(text, options?.Exact);

        ILocator ILocator.GetByAltText(Regex text, LocatorGetByAltTextOptions options) => GetByAltText(text);

        ILocator ILocator.GetByLabel(string text, LocatorGetByLabelOptions options) => GetByLabel(text, options?.Exact);

        ILocator ILocator.GetByLabel(Regex text, LocatorGetByLabelOptions options) => GetByLabel(text);

        ILocator ILocator.GetByPlaceholder(string text, LocatorGetByPlaceholderOptions options) => GetByPlaceholder(text, options?.Exact);

        ILocator ILocator.GetByPlaceholder(Regex text, LocatorGetByPlaceholderOptions options) => GetByPlaceholder(text);

        ILocator ILocator.GetByRole(AriaRole role, LocatorGetByRoleOptions options)
            => GetByRole(
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
                options?.NameRegex);

        ILocator ILocator.GetByText(string text, LocatorGetByTextOptions options) => GetByText(text, options?.Exact);

        ILocator ILocator.GetByText(Regex text, LocatorGetByTextOptions options) => GetByText(text);

        ILocator ILocator.GetByTitle(string text, LocatorGetByTitleOptions options) => GetByTitle(text, options?.Exact);

        ILocator ILocator.GetByTitle(Regex text, LocatorGetByTitleOptions options) => GetByTitle(text);

        Task ILocator.HideHighlightAsync() => Task.CompletedTask;

        Task<IAsyncDisposable> ILocator.HighlightAsync(LocatorHighlightOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task ILocator.HoverAsync(LocatorHoverOptions options)
            => HoverAsync(options?.Position, options?.Modifiers, options?.Force, options?.Timeout, options?.Trial);

        Task<string> ILocator.InnerHTMLAsync(LocatorInnerHTMLOptions options) => InnerHTMLAsync(options?.Timeout);

        Task<string> ILocator.InnerTextAsync(LocatorInnerTextOptions options) => InnerTextAsync(options?.Timeout);

        Task<string> ILocator.InputValueAsync(LocatorInputValueOptions options) => InputValueAsync(options?.Timeout);

        Task<bool> ILocator.IsCheckedAsync(LocatorIsCheckedOptions options) => IsCheckedAsync(options?.Timeout);

        Task<bool> ILocator.IsDisabledAsync(LocatorIsDisabledOptions options) => IsDisabledAsync(options?.Timeout);

        Task<bool> ILocator.IsEditableAsync(LocatorIsEditableOptions options) => IsEditableAsync(options?.Timeout);

        Task<bool> ILocator.IsEnabledAsync(LocatorIsEnabledOptions options) => IsEnabledAsync(options?.Timeout);

        Task<bool> ILocator.IsHiddenAsync(LocatorIsHiddenOptions options) => IsHiddenAsync();

        Task<bool> ILocator.IsVisibleAsync(LocatorIsVisibleOptions options) => IsVisibleAsync();

        ILocator ILocator.Locator(string selectorOrLocator, LocatorLocatorOptions options)
        {
            ILocator result = ChainLocator(selectorOrLocator);
            options ??= new LocatorLocatorOptions();
            return SelectorQuery.ApplyOptions(
                result,
                options.Has,
                options.HasText ?? options.HasTextString,
                options.HasTextRegex,
                options.HasNot,
                options.HasNotText ?? options.HasNotTextString,
                options.HasNotTextRegex);
        }

        ILocator ILocator.Locator(ILocator selectorOrLocator, LocatorLocatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(selectorOrLocator);
            ILocator result = ChainLocator(RequireLocator(selectorOrLocator));
            options ??= new LocatorLocatorOptions();
            return SelectorQuery.ApplyOptions(
                result,
                options.Has,
                options.HasText ?? options.HasTextString,
                options.HasTextRegex,
                options.HasNot,
                options.HasNotText ?? options.HasNotTextString,
                options.HasNotTextRegex);
        }

        Task<ILocator> ILocator.NormalizeAsync() => NormalizeAsync();

        Task ILocator.PressAsync(string key, LocatorPressOptions options)
            => PressAsync(key, options?.Delay, options?.NoWaitAfter, options?.Timeout);

        Task ILocator.PressSequentiallyAsync(string text, LocatorPressSequentiallyOptions options)
            => PressSequentiallyAsync(text, options?.Delay, options?.NoWaitAfter, options?.Timeout);

        Task<byte[]> ILocator.ScreenshotAsync(LocatorScreenshotOptions options)
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

        Task ILocator.ScrollIntoViewIfNeededAsync(LocatorScrollIntoViewIfNeededOptions options) => ScrollIntoViewIfNeededAsync(options?.Timeout);

        async Task<IReadOnlyList<string>> ILocator.SelectOptionAsync(string values, LocatorSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> ILocator.SelectOptionAsync(IElementHandle values, LocatorSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> ILocator.SelectOptionAsync(IEnumerable<string> values, LocatorSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> ILocator.SelectOptionAsync(SelectOptionValue values, LocatorSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> ILocator.SelectOptionAsync(IEnumerable<IElementHandle> values, LocatorSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        async Task<IReadOnlyList<string>> ILocator.SelectOptionAsync(IEnumerable<SelectOptionValue> values, LocatorSelectOptionOptions options)
        {
            IReadOnlyCollection<string> result = await SelectOptionAsync(values, options?.NoWaitAfter, options?.Timeout, options?.Force).ConfigureAwait(false);
            return result as IReadOnlyList<string> ?? result.ToList();
        }

        Task ILocator.SelectTextAsync(LocatorSelectTextOptions options)
            => SelectTextAsync(options?.Timeout, options?.Force);

        Task ILocator.SetCheckedAsync(bool checkedState, LocatorSetCheckedOptions options)
            => SetCheckedAsync(checkedState, options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task ILocator.SetInputFilesAsync(string files, LocatorSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task ILocator.SetInputFilesAsync(IEnumerable<string> files, LocatorSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task ILocator.SetInputFilesAsync(FilePayload files, LocatorSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task ILocator.SetInputFilesAsync(IEnumerable<FilePayload> files, LocatorSetInputFilesOptions options)
            => SetInputFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task ILocator.TapAsync(LocatorTapOptions options)
            => TapAsync(options?.Position, options?.Modifiers, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task<string> ILocator.TextContentAsync(LocatorTextContentOptions options) => TextContentAsync(options?.Timeout);

        Task ILocator.TypeAsync(string text, LocatorTypeOptions options)
            => TypeAsync(text, options?.Delay, options?.NoWaitAfter, options?.Timeout);

        Task ILocator.UncheckAsync(LocatorUncheckOptions options)
            => UncheckAsync(options?.Position, options?.Force, options?.NoWaitAfter, options?.Timeout, options?.Trial);

        Task ILocator.WaitForAsync(LocatorWaitForOptions options)
            => WaitForAsync(options?.State ?? WaitForSelectorState.Visible, options?.Timeout);

        Task ILocator.WaitForFunctionAsync(string expression, object arg, LocatorWaitForFunctionOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
