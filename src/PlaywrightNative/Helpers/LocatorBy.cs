// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Binds a page-free <see cref="IBy"/> to a page, frame, locator, or
    /// frame locator. Official Playwright <c>resolveBy</c> / <c>page.get</c>.
    /// </summary>
    internal static class LocatorBy
    {
        /// <summary>
        /// Factory that creates locators the same way the matching
        /// <c>GetBy*</c> methods do, so <c>ToString</c> stays identical.
        /// </summary>
        internal interface IFactory
        {
            /// <summary>Creates a CSS/XPath locator on the bind target.</summary>
            /// <param name="selector">A CSS selector, or <c>..</c> for the parent.</param>
            /// <returns>A locator.</returns>
            ILocator Locator(string selector);

            /// <summary>Creates a role locator on the bind target.</summary>
            /// <param name="role">ARIA role.</param>
            /// <param name="name">Optional accessible name.</param>
            /// <param name="exact">When set, exact name match.</param>
            /// <param name="checkedState">Optional checked filter.</param>
            /// <param name="disabled">Optional disabled filter.</param>
            /// <param name="expanded">Optional expanded filter.</param>
            /// <param name="includeHidden">Optional hidden filter.</param>
            /// <param name="level">Optional heading level.</param>
            /// <param name="pressed">Optional pressed filter.</param>
            /// <param name="selected">Optional selected filter.</param>
            /// <param name="description">Optional description filter.</param>
            /// <param name="descriptionRegex">Optional description regex.</param>
            /// <param name="nameRegex">Optional name regex.</param>
            /// <returns>A locator.</returns>
            ILocator GetByRole(
                string role,
                string name,
                bool? exact,
                bool? checkedState,
                bool? disabled,
                bool? expanded,
                bool? includeHidden,
                int? level,
                bool? pressed,
                bool? selected,
                string description,
                Regex descriptionRegex,
                Regex nameRegex);

            /// <summary>Creates a text locator on the bind target.</summary>
            /// <param name="text">Text to search for.</param>
            /// <param name="exact">When set, exact text match.</param>
            /// <returns>A locator.</returns>
            ILocator GetByText(string text, bool? exact);

            /// <summary>Creates a text regex locator on the bind target.</summary>
            /// <param name="text">Text pattern.</param>
            /// <returns>A locator.</returns>
            ILocator GetByText(Regex text);

            /// <summary>Creates a label locator on the bind target.</summary>
            /// <param name="text">Label text.</param>
            /// <param name="exact">When set, exact label match.</param>
            /// <returns>A locator.</returns>
            ILocator GetByLabel(string text, bool? exact);

            /// <summary>Creates a label regex locator on the bind target.</summary>
            /// <param name="text">Label pattern.</param>
            /// <returns>A locator.</returns>
            ILocator GetByLabel(Regex text);

            /// <summary>Creates a placeholder locator on the bind target.</summary>
            /// <param name="text">Placeholder text.</param>
            /// <param name="exact">When set, exact placeholder match.</param>
            /// <returns>A locator.</returns>
            ILocator GetByPlaceholder(string text, bool? exact);

            /// <summary>Creates a placeholder regex locator on the bind target.</summary>
            /// <param name="text">Placeholder pattern.</param>
            /// <returns>A locator.</returns>
            ILocator GetByPlaceholder(Regex text);

            /// <summary>Creates an alt-text locator on the bind target.</summary>
            /// <param name="text">Alt text.</param>
            /// <param name="exact">When set, exact alt match.</param>
            /// <returns>A locator.</returns>
            ILocator GetByAltText(string text, bool? exact);

            /// <summary>Creates an alt-text regex locator on the bind target.</summary>
            /// <param name="text">Alt-text pattern.</param>
            /// <returns>A locator.</returns>
            ILocator GetByAltText(Regex text);

            /// <summary>Creates a title locator on the bind target.</summary>
            /// <param name="text">Title text.</param>
            /// <param name="exact">When set, exact title match.</param>
            /// <returns>A locator.</returns>
            ILocator GetByTitle(string text, bool? exact);

            /// <summary>Creates a title regex locator on the bind target.</summary>
            /// <param name="text">Title pattern.</param>
            /// <returns>A locator.</returns>
            ILocator GetByTitle(Regex text);

            /// <summary>Creates a test-id locator on the bind target.</summary>
            /// <param name="testId">Test id value.</param>
            /// <returns>A locator.</returns>
            ILocator GetByTestId(string testId);

            /// <summary>Creates a test-id regex locator on the bind target.</summary>
            /// <param name="testId">Test id pattern.</param>
            /// <returns>A locator.</returns>
            ILocator GetByTestId(Regex testId);

            /// <summary>
            /// Binds <paramref name="by"/> as a relative query in the same
            /// frame (used by <c>and</c>, <c>or</c>, and <c>has</c>).
            /// </summary>
            /// <param name="by">The inner builder.</param>
            /// <returns>A locator used as a filter operand.</returns>
            ILocator BindRelative(IBy by);
        }

        /// <summary>
        /// Binds <paramref name="by"/> to <paramref name="page"/>.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="by">The page-free <see cref="IBy"/> builder.</param>
        /// <returns>A locator on <paramref name="page"/>.</returns>
        internal static ILocator Bind(IPage page, IBy by)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (by == null)
            {
                throw new ArgumentNullException(nameof(by));
            }

            return PlaywrightNative.By.Resolve(by, new PageFactory(page));
        }

        /// <summary>
        /// Binds <paramref name="by"/> to <paramref name="frame"/>.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="by">The page-free <see cref="IBy"/> builder.</param>
        /// <returns>A locator on <paramref name="frame"/>.</returns>
        internal static ILocator Bind(IFrame frame, IBy by)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            if (by == null)
            {
                throw new ArgumentNullException(nameof(by));
            }

            return PlaywrightNative.By.Resolve(by, new FrameFactory(frame));
        }

        /// <summary>
        /// Binds <paramref name="by"/> inside <paramref name="locator"/>.
        /// </summary>
        /// <param name="locator">The scope locator.</param>
        /// <param name="by">The page-free <see cref="IBy"/> builder.</param>
        /// <returns>A locator scoped to <paramref name="locator"/>.</returns>
        internal static ILocator Bind(ILocator locator, IBy by)
        {
            if (locator == null)
            {
                throw new ArgumentNullException(nameof(locator));
            }

            if (by == null)
            {
                throw new ArgumentNullException(nameof(by));
            }

            return PlaywrightNative.By.Resolve(by, new LocatorFactory(locator));
        }

        /// <summary>
        /// Binds <paramref name="by"/> inside <paramref name="frameLocator"/>.
        /// </summary>
        /// <param name="frameLocator">The iframe scope.</param>
        /// <param name="by">The page-free <see cref="IBy"/> builder.</param>
        /// <returns>A locator in the iframe content document.</returns>
        internal static ILocator Bind(IFrameLocator frameLocator, IBy by)
        {
            if (frameLocator == null)
            {
                throw new ArgumentNullException(nameof(frameLocator));
            }

            if (by == null)
            {
                throw new ArgumentNullException(nameof(by));
            }

            return PlaywrightNative.By.Resolve(by, new FrameLocatorFactory(frameLocator));
        }

        private sealed class PageFactory : IFactory
        {
            private readonly IPage _page;

            internal PageFactory(IPage page)
            {
                _page = page;
            }

            public ILocator Locator(string selector) => _page.Locator(selector);

            public ILocator GetByRole(
                string role,
                string name,
                bool? exact,
                bool? checkedState,
                bool? disabled,
                bool? expanded,
                bool? includeHidden,
                int? level,
                bool? pressed,
                bool? selected,
                string description,
                Regex descriptionRegex,
                Regex nameRegex)
                => _page.GetByRole(
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
                    nameRegex);

            public ILocator GetByText(string text, bool? exact) => _page.GetByText(text, exact);

            public ILocator GetByText(Regex text) => _page.GetByText(text);

            public ILocator GetByLabel(string text, bool? exact) => _page.GetByLabel(text, exact);

            public ILocator GetByLabel(Regex text) => _page.GetByLabel(text);

            public ILocator GetByPlaceholder(string text, bool? exact) => _page.GetByPlaceholder(text, exact);

            public ILocator GetByPlaceholder(Regex text) => _page.GetByPlaceholder(text);

            public ILocator GetByAltText(string text, bool? exact) => _page.GetByAltText(text, exact);

            public ILocator GetByAltText(Regex text) => _page.GetByAltText(text);

            public ILocator GetByTitle(string text, bool? exact) => _page.GetByTitle(text, exact);

            public ILocator GetByTitle(Regex text) => _page.GetByTitle(text);

            public ILocator GetByTestId(string testId) => _page.GetByTestId(testId);

            public ILocator GetByTestId(Regex testId) => _page.GetByTestId(testId);

            public ILocator BindRelative(IBy by) => Bind(_page, by);
        }

        private sealed class FrameFactory : IFactory
        {
            private readonly IFrame _frame;

            internal FrameFactory(IFrame frame)
            {
                _frame = frame;
            }

            public ILocator Locator(string selector) => _frame.Locator(selector);

            public ILocator GetByRole(
                string role,
                string name,
                bool? exact,
                bool? checkedState,
                bool? disabled,
                bool? expanded,
                bool? includeHidden,
                int? level,
                bool? pressed,
                bool? selected,
                string description,
                Regex descriptionRegex,
                Regex nameRegex)
                => _frame.GetByRole(
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
                    nameRegex);

            public ILocator GetByText(string text, bool? exact) => _frame.GetByText(text, exact);

            public ILocator GetByText(Regex text) => _frame.GetByText(text);

            public ILocator GetByLabel(string text, bool? exact) => _frame.GetByLabel(text, exact);

            public ILocator GetByLabel(Regex text) => _frame.GetByLabel(text);

            public ILocator GetByPlaceholder(string text, bool? exact) => _frame.GetByPlaceholder(text, exact);

            public ILocator GetByPlaceholder(Regex text) => _frame.GetByPlaceholder(text);

            public ILocator GetByAltText(string text, bool? exact) => _frame.GetByAltText(text, exact);

            public ILocator GetByAltText(Regex text) => _frame.GetByAltText(text);

            public ILocator GetByTitle(string text, bool? exact) => _frame.GetByTitle(text, exact);

            public ILocator GetByTitle(Regex text) => _frame.GetByTitle(text);

            public ILocator GetByTestId(string testId) => _frame.GetByTestId(testId);

            public ILocator GetByTestId(Regex testId) => _frame.GetByTestId(testId);

            public ILocator BindRelative(IBy by) => Bind(_frame, by);
        }

        private sealed class LocatorFactory : IFactory
        {
            private readonly ILocator _locator;

            internal LocatorFactory(ILocator locator)
            {
                _locator = locator;
            }

            public ILocator Locator(string selector) => _locator.Locator(selector);

            public ILocator GetByRole(
                string role,
                string name,
                bool? exact,
                bool? checkedState,
                bool? disabled,
                bool? expanded,
                bool? includeHidden,
                int? level,
                bool? pressed,
                bool? selected,
                string description,
                Regex descriptionRegex,
                Regex nameRegex)
                => _locator.GetByRole(
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
                    nameRegex);

            public ILocator GetByText(string text, bool? exact) => _locator.GetByText(text, exact);

            public ILocator GetByText(Regex text) => _locator.GetByText(text);

            public ILocator GetByLabel(string text, bool? exact) => _locator.GetByLabel(text, exact);

            public ILocator GetByLabel(Regex text) => _locator.GetByLabel(text);

            public ILocator GetByPlaceholder(string text, bool? exact) => _locator.GetByPlaceholder(text, exact);

            public ILocator GetByPlaceholder(Regex text) => _locator.GetByPlaceholder(text);

            public ILocator GetByAltText(string text, bool? exact) => _locator.GetByAltText(text, exact);

            public ILocator GetByAltText(Regex text) => _locator.GetByAltText(text);

            public ILocator GetByTitle(string text, bool? exact) => _locator.GetByTitle(text, exact);

            public ILocator GetByTitle(Regex text) => _locator.GetByTitle(text);

            public ILocator GetByTestId(string testId) => _locator.GetByTestId(testId);

            public ILocator GetByTestId(Regex testId) => _locator.GetByTestId(testId);

            public ILocator BindRelative(IBy by) => Bind(_locator.Frame(), by);
        }

        private sealed class FrameLocatorFactory : IFactory
        {
            private readonly IFrameLocator _frameLocator;

            internal FrameLocatorFactory(IFrameLocator frameLocator)
            {
                _frameLocator = frameLocator;
            }

            public ILocator Locator(string selector) => _frameLocator.Locator(selector);

            public ILocator GetByRole(
                string role,
                string name,
                bool? exact,
                bool? checkedState,
                bool? disabled,
                bool? expanded,
                bool? includeHidden,
                int? level,
                bool? pressed,
                bool? selected,
                string description,
                Regex descriptionRegex,
                Regex nameRegex)
                => _frameLocator.GetByRole(
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
                    nameRegex);

            public ILocator GetByText(string text, bool? exact) => _frameLocator.GetByText(text, exact);

            public ILocator GetByText(Regex text) => _frameLocator.GetByText(text);

            public ILocator GetByLabel(string text, bool? exact) => _frameLocator.GetByLabel(text, exact);

            public ILocator GetByLabel(Regex text) => _frameLocator.GetByLabel(text);

            public ILocator GetByPlaceholder(string text, bool? exact) => _frameLocator.GetByPlaceholder(text, exact);

            public ILocator GetByPlaceholder(Regex text) => _frameLocator.GetByPlaceholder(text);

            public ILocator GetByAltText(string text, bool? exact) => _frameLocator.GetByAltText(text, exact);

            public ILocator GetByAltText(Regex text) => _frameLocator.GetByAltText(text);

            public ILocator GetByTitle(string text, bool? exact) => _frameLocator.GetByTitle(text, exact);

            public ILocator GetByTitle(Regex text) => _frameLocator.GetByTitle(text);

            public ILocator GetByTestId(string testId) => _frameLocator.GetByTestId(testId);

            public ILocator GetByTestId(Regex testId) => _frameLocator.GetByTestId(testId);

            public ILocator BindRelative(IBy by) => Bind(_frameLocator, by);
        }
    }
}
