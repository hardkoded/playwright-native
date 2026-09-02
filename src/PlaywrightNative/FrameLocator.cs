// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="IFrameLocator"/> that re-queries the iframe element
    /// and enters its content document on every action.
    /// </summary>
    public sealed partial class FrameLocator : IFrameLocator
    {
        private const string NthNotAllowed = "Selecting the nth frame is not allowed on frameLocator().";

        private readonly ILocator _iframe;
        private readonly IFrame _frame;
        private readonly bool _anyFrame;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameLocator"/> class.
        /// </summary>
        /// <param name="frame">Parent frame that contains the iframe element.</param>
        /// <param name="selector">A CSS selector for the iframe element.</param>
        public FrameLocator(IFrame frame, string selector)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            _frame = frame;
            _iframe = new Locator(frame, selector);
            _anyFrame = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameLocator"/> class
        /// for official <c>page.frameLocator()</c> any-frame search of
        /// <paramref name="frame"/> and descendants.
        /// </summary>
        /// <param name="frame">Starting frame.</param>
        internal FrameLocator(IFrame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _iframe = null;
            _anyFrame = true;
        }

        internal FrameLocator(ILocator iframe)
        {
            _iframe = iframe ?? throw new ArgumentNullException(nameof(iframe));
            _frame = iframe.Frame();
            _anyFrame = false;
        }

        /// <inheritdoc/>
        public ILocator Owner => _anyFrame
            ? new Locator(_frame, FrameSelector.AnyFrameToken)
            : _iframe;

        /// <inheritdoc/>
#pragma warning disable CA1065 // Official frameLocator().first() throws when any-frame.
        public IFrameLocator First => _anyFrame
            ? throw new PlaywrightNativeException(NthNotAllowed)
            : new FrameLocator(_iframe.First);
#pragma warning restore CA1065

        /// <inheritdoc/>
#pragma warning disable CA1065 // Official frameLocator().last() throws when any-frame.
        public IFrameLocator Last => _anyFrame
            ? throw new PlaywrightNativeException(NthNotAllowed)
            : new FrameLocator(_iframe.Last);
#pragma warning restore CA1065

        /// <inheritdoc/>
        public IFrameLocator Nth(int index)
            => _anyFrame
                ? throw new PlaywrightNativeException(NthNotAllowed)
                : new FrameLocator(_iframe.Nth(index));

        /// <inheritdoc/>
        public ILocator Locator(
            string selector,
            ILocator has = default,
            string hasText = default,
            Regex hasTextRegex = default,
            ILocator hasNot = default,
            string hasNotText = default,
            Regex hasNotTextRegex = default)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return SelectorQuery.ApplyOptions(
                _anyFrame
                    ? PlaywrightNative.Locator.InAnyFrame(_frame, selector)
                    : RequireLocator(_iframe).EnterThen(selector),
                has,
                hasText,
                hasTextRegex,
                hasNot,
                hasNotText,
                hasNotTextRegex);
        }

        /// <inheritdoc/>
        public ILocator Locator(ILocator locator)
        {
            ArgumentNullException.ThrowIfNull(locator);
            Locator inner = RequireLocator(locator);
            if (_anyFrame)
            {
                if (!ReferenceEquals(inner.Frame, _frame))
                {
                    throw new PlaywrightNativeException("Locators must belong to the same frame.");
                }

                return inner.WithAnyFrame();
            }

            if (!ReferenceEquals(inner.Frame(), _iframe.Frame()))
            {
                throw new PlaywrightNativeException("Locators must belong to the same frame.");
            }

            return RequireLocator(_iframe).EnterThenLocator(inner);
        }

        /// <inheritdoc/>
        IFrameLocator IFrameLocator.FrameLocator(string selector)
            => new FrameLocator(Locator(selector));

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
            => Locator(RoleSelector.Build(
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
                nameRegex));

        /// <inheritdoc/>
        public ILocator GetByText(string text, bool? exact = null)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(_frame, GetByAllScript.FindAllByText, text, exact ?? false).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(GetByAllScript.FindAllByText, text, exact ?? false);

        /// <inheritdoc/>
        public ILocator GetByText(Regex text)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(
                    _frame,
                    GetByAllScript.FindAllByTextRegex,
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text)).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(
                    GetByAllScript.FindAllByTextRegex,
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text));

        /// <inheritdoc/>
        public ILocator GetByLabel(string text, bool? exact = null)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(_frame, GetByAllScript.FindAllByLabel, text, exact ?? false).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(GetByAllScript.FindAllByLabel, text, exact ?? false);

        /// <inheritdoc/>
        public ILocator GetByLabel(Regex text)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(
                    _frame,
                    GetByAllScript.FindAllByLabelRegex,
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text)).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(
                    GetByAllScript.FindAllByLabelRegex,
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text));

        /// <inheritdoc/>
        public ILocator GetByPlaceholder(string text, bool? exact = null)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(_frame, GetByAllScript.FindAllByAttribute, "placeholder", text, exact ?? false).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(GetByAllScript.FindAllByAttribute, "placeholder", text, exact ?? false);

        /// <inheritdoc/>
        public ILocator GetByPlaceholder(Regex text)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(
                    _frame,
                    GetByAllScript.FindAllByAttributeRegex,
                    "placeholder",
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text)).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(
                    GetByAllScript.FindAllByAttributeRegex,
                    "placeholder",
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text));

        /// <inheritdoc/>
        public ILocator GetByAltText(string text, bool? exact = null)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(_frame, GetByAllScript.FindAllByAttribute, "alt", text, exact ?? false).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(GetByAllScript.FindAllByAttribute, "alt", text, exact ?? false);

        /// <inheritdoc/>
        public ILocator GetByAltText(Regex text)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(
                    _frame,
                    GetByAllScript.FindAllByAttributeRegex,
                    "alt",
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text)).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(
                    GetByAllScript.FindAllByAttributeRegex,
                    "alt",
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text));

        /// <inheritdoc/>
        public ILocator GetByTitle(string text, bool? exact = null)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(_frame, GetByAllScript.FindAllByAttribute, "title", text, exact ?? false).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(GetByAllScript.FindAllByAttribute, "title", text, exact ?? false);

        /// <inheritdoc/>
        public ILocator GetByTitle(Regex text)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(
                    _frame,
                    GetByAllScript.FindAllByAttributeRegex,
                    "title",
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text)).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(
                    GetByAllScript.FindAllByAttributeRegex,
                    "title",
                    GetByAllScript.Pattern(text),
                    GetByAllScript.Flags(text));

        /// <inheritdoc/>
        public ILocator GetByTestId(string testId)
            => _anyFrame
                ? PlaywrightNative.Locator.InAnyFrame(_frame, GetBySelectorScript.TestIdSelector(testId))
                : RequireLocator(_iframe).EnterThen(GetBySelectorScript.TestIdSelector(testId));

        /// <inheritdoc/>
        public ILocator GetByTestId(Regex testId)
            => _anyFrame
                ? PlaywrightNative.Locator.FromScript(
                    _frame,
                    GetByAllScript.FindAllByAttributeRegex,
                    GetBySelectorScript.TestIdAttributeName(),
                    GetByAllScript.Pattern(testId),
                    GetByAllScript.Flags(testId)).WithAnyFrame()
                : RequireLocator(_iframe).EnterThenScript(
                    GetByAllScript.FindAllByAttributeRegex,
                    GetBySelectorScript.TestIdAttributeName(),
                    GetByAllScript.Pattern(testId),
                    GetByAllScript.Flags(testId));

        private static Locator RequireLocator(ILocator other)
        {
            if (other is Locator locator)
            {
                return locator;
            }

            throw new ArgumentException("iframe locator must be a PlaywrightNative locator.", nameof(other));
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        ILocator IFrameLocator.GetByAltText(string text, FrameLocatorGetByAltTextOptions options) => null!;

        ILocator IFrameLocator.GetByAltText(Regex text, FrameLocatorGetByAltTextOptions options) => null!;

        ILocator IFrameLocator.GetByLabel(string text, FrameLocatorGetByLabelOptions options) => null!;

        ILocator IFrameLocator.GetByLabel(Regex text, FrameLocatorGetByLabelOptions options) => null!;

        ILocator IFrameLocator.GetByPlaceholder(string text, FrameLocatorGetByPlaceholderOptions options) => null!;

        ILocator IFrameLocator.GetByPlaceholder(Regex text, FrameLocatorGetByPlaceholderOptions options) => null!;

        ILocator IFrameLocator.GetByRole(AriaRole role, FrameLocatorGetByRoleOptions options) => null!;

        ILocator IFrameLocator.GetByText(string text, FrameLocatorGetByTextOptions options) => null!;

        ILocator IFrameLocator.GetByText(Regex text, FrameLocatorGetByTextOptions options) => null!;

        ILocator IFrameLocator.GetByTitle(string text, FrameLocatorGetByTitleOptions options) => null!;

        ILocator IFrameLocator.GetByTitle(Regex text, FrameLocatorGetByTitleOptions options) => null!;

        ILocator IFrameLocator.Locator(string selectorOrLocator, FrameLocatorLocatorOptions options) => null!;

        ILocator IFrameLocator.Locator(ILocator selectorOrLocator, FrameLocatorLocatorOptions options) => null!;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
