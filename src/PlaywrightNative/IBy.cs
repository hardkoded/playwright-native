// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System.Text.RegularExpressions;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Page-free locator builder. Official Playwright <c>by</c> from
    /// <c>playwright-core</c>. Create one with <see cref="By"/> and bind it
    /// with <see cref="IPage.Get(IBy)"/>, <see cref="IFrame.Get(IBy)"/>,
    /// <see cref="ILocator.Get(IBy)"/>, or <see cref="IFrameLocator.Get(IBy)"/>.
    /// The configured test-id attribute is read when the builder is bound,
    /// not when it is built. Chaining never mutates the original.
    /// </summary>
    public interface IBy
    {
        /// <summary>
        /// Builder narrowed to the first match. Official <c>by.first()</c>.
        /// </summary>
        IBy First { get; }

        /// <summary>
        /// Builder narrowed to the last match. Official <c>by.last()</c>.
        /// </summary>
        IBy Last { get; }

        /// <summary>
        /// Chains a CSS or XPath query. Official <c>by.get(selector)</c>.
        /// </summary>
        /// <param name="selector">A CSS selector, or <c>..</c> for the parent.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Get(string selector);

        /// <summary>
        /// Chains another builder. Official <c>by.get(other)</c>. Nested
        /// <c>get</c> composes the same way as sequential chaining.
        /// </summary>
        /// <param name="by">A builder whose steps are appended.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Get(IBy by);

        /// <summary>
        /// Chains an alt-text query. Official <c>by.altText()</c>.
        /// </summary>
        /// <param name="text">Alt text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the alt text must match exactly.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy AltText(string text, bool? exact = null);

        /// <summary>
        /// Chains an alt-text regex query. Official <c>by.altText()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the alt text.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy AltText(Regex text);

        /// <summary>
        /// Chains a label query. Official <c>by.label()</c>.
        /// </summary>
        /// <param name="text">Label text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the label must match exactly.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Label(string text, bool? exact = null);

        /// <summary>
        /// Chains a label regex query. Official <c>by.label()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the label or aria-label.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Label(Regex text);

        /// <summary>
        /// Chains a placeholder query. Official <c>by.placeholder()</c>.
        /// </summary>
        /// <param name="text">Placeholder text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the placeholder must match exactly.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Placeholder(string text, bool? exact = null);

        /// <summary>
        /// Chains a placeholder regex query. Official <c>by.placeholder()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the placeholder.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Placeholder(Regex text);

        /// <summary>
        /// Chains a role query. Official <c>by.role()</c>.
        /// </summary>
        /// <param name="role">ARIA role, e.g. <c>button</c>.</param>
        /// <param name="name">Optional accessible name filter.</param>
        /// <param name="exact">When <see langword="true"/>, the name must match exactly.</param>
        /// <param name="checkedState">Optional checked-state filter.</param>
        /// <param name="disabled">Optional disabled-state filter.</param>
        /// <param name="expanded">Optional expanded-state filter.</param>
        /// <param name="includeHidden">When <see langword="false"/>, skip hidden elements.</param>
        /// <param name="level">Optional heading level.</param>
        /// <param name="pressed">Optional pressed-state filter.</param>
        /// <param name="selected">Optional selected-state filter.</param>
        /// <param name="description">Optional accessible description filter.</param>
        /// <param name="descriptionRegex">Optional accessible description regular expression.</param>
        /// <param name="nameRegex">Optional accessible name regular expression.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Role(
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
            Regex nameRegex = null);

        /// <inheritdoc cref="Role(string, string, bool?, bool?, bool?, bool?, bool?, int?, bool?, bool?, string, Regex, Regex)"/>
        IBy Role(
            AriaRole role,
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
            => Role(
                role.ToRoleString(),
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

        /// <summary>
        /// Chains a test-id query. Official <c>by.testId()</c>. The attribute
        /// name is resolved when the builder is bound.
        /// </summary>
        /// <param name="testId">The test id to match exactly.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy TestId(string testId);

        /// <summary>
        /// Chains a test-id regex query. Official <c>by.testId()</c>.
        /// </summary>
        /// <param name="testId">Regular expression tested against the test id.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy TestId(Regex testId);

        /// <summary>
        /// Chains a text query. Official <c>by.text()</c>.
        /// </summary>
        /// <param name="text">Text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the text must match exactly.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Text(string text, bool? exact = null);

        /// <summary>
        /// Chains a text regex query. Official <c>by.text()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the element's text.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Text(Regex text);

        /// <summary>
        /// Chains a title query. Official <c>by.title()</c>.
        /// </summary>
        /// <param name="text">Title text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the title must match exactly.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Title(string text, bool? exact = null);

        /// <summary>
        /// Chains a title regex query. Official <c>by.title()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the title.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Title(Regex text);

        /// <summary>
        /// Narrows matches. Official <c>by.filter({ has, hasNot, hasText, hasNotText, visible })</c>.
        /// </summary>
        /// <param name="hasText">Optional case-insensitive substring.</param>
        /// <param name="hasTextRegex">Optional text regular expression.</param>
        /// <param name="has">Optional inner builder the match must contain.</param>
        /// <param name="hasNot">Optional inner builder the match must not contain.</param>
        /// <param name="hasNotText">Optional substring that must not appear.</param>
        /// <param name="hasNotTextRegex">Optional regular expression that must not match.</param>
        /// <param name="visible">When set, keep only visible or hidden matches.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Filter(
            string hasText = default,
            Regex hasTextRegex = default,
            IBy has = default,
            IBy hasNot = default,
            string hasNotText = default,
            Regex hasNotTextRegex = default,
            bool? visible = default);

        /// <summary>
        /// Intersection with <paramref name="by"/>. Official <c>by.and()</c>.
        /// </summary>
        /// <param name="by">Another builder bound in the same frame.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy And(IBy by);

        /// <summary>
        /// Union with <paramref name="by"/>. Official <c>by.or()</c>.
        /// </summary>
        /// <param name="by">Another builder bound in the same frame.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Or(IBy by);

        /// <summary>
        /// Builder narrowed to the match at <paramref name="index"/>.
        /// Official <c>by.nth()</c>.
        /// </summary>
        /// <param name="index">Zero-based index.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Nth(int index);

        /// <summary>
        /// Sets the description used in strict-mode errors and
        /// <see cref="ILocator.Description"/>. Official <c>by.describe()</c>.
        /// </summary>
        /// <param name="description">Human-readable name.</param>
        /// <returns>A new builder. This instance is not mutated.</returns>
        IBy Describe(string description);
    }
}
