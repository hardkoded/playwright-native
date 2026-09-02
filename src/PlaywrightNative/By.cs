// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Factories for page-free <see cref="IBy"/> builders. Official Playwright
    /// <c>by</c> from <c>playwright-core</c>. Bind with
    /// <see cref="IPage.Get(IBy)"/>, <see cref="IFrame.Get(IBy)"/>,
    /// <see cref="ILocator.Get(IBy)"/>, or <see cref="IFrameLocator.Get(IBy)"/>.
    /// </summary>
    public static class By
    {
        /// <summary>
        /// Empty starter. Official Playwright <c>by</c>. Passing this to
        /// <see cref="IPage.Get(IBy)"/> throws.
        /// </summary>
        public static IBy Empty { get; } = Impl.EmptyInstance;

        /// <summary>
        /// Starts a CSS or XPath query. Official <c>by.get(selector)</c>.
        /// </summary>
        /// <param name="selector">A CSS selector, or <c>..</c> for the parent.</param>
        /// <returns>A new builder.</returns>
        public static IBy Get(string selector) => Empty.Get(selector);

        /// <summary>
        /// Starts an alt-text query. Official <c>by.altText()</c>.
        /// </summary>
        /// <param name="text">Alt text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the alt text must match exactly.</param>
        /// <returns>A new builder.</returns>
        public static IBy AltText(string text, bool? exact = null) => Empty.AltText(text, exact);

        /// <summary>
        /// Starts an alt-text regex query. Official <c>by.altText()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the alt text.</param>
        /// <returns>A new builder.</returns>
        public static IBy AltText(Regex text) => Empty.AltText(text);

        /// <summary>
        /// Starts a label query. Official <c>by.label()</c>.
        /// </summary>
        /// <param name="text">Label text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the label must match exactly.</param>
        /// <returns>A new builder.</returns>
        public static IBy Label(string text, bool? exact = null) => Empty.Label(text, exact);

        /// <summary>
        /// Starts a label regex query. Official <c>by.label()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the label or aria-label.</param>
        /// <returns>A new builder.</returns>
        public static IBy Label(Regex text) => Empty.Label(text);

        /// <summary>
        /// Starts a placeholder query. Official <c>by.placeholder()</c>.
        /// </summary>
        /// <param name="text">Placeholder text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the placeholder must match exactly.</param>
        /// <returns>A new builder.</returns>
        public static IBy Placeholder(string text, bool? exact = null) => Empty.Placeholder(text, exact);

        /// <summary>
        /// Starts a placeholder regex query. Official <c>by.placeholder()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the placeholder.</param>
        /// <returns>A new builder.</returns>
        public static IBy Placeholder(Regex text) => Empty.Placeholder(text);

        /// <summary>
        /// Starts a role query. Official <c>by.role()</c>.
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
        /// <returns>A new builder.</returns>
        public static IBy Role(
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
            => Empty.Role(
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

        /// <inheritdoc cref="Role(string, string, bool?, bool?, bool?, bool?, bool?, int?, bool?, bool?, string, Regex, Regex)"/>
        public static IBy Role(
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
        /// Starts a test-id query. Official <c>by.testId()</c>. The attribute
        /// name is resolved when the builder is bound.
        /// </summary>
        /// <param name="testId">The test id to match exactly.</param>
        /// <returns>A new builder.</returns>
        public static IBy TestId(string testId) => Empty.TestId(testId);

        /// <summary>
        /// Starts a test-id regex query. Official <c>by.testId()</c>.
        /// </summary>
        /// <param name="testId">Regular expression tested against the test id.</param>
        /// <returns>A new builder.</returns>
        public static IBy TestId(Regex testId) => Empty.TestId(testId);

        /// <summary>
        /// Starts a text query. Official <c>by.text()</c>.
        /// </summary>
        /// <param name="text">Text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the text must match exactly.</param>
        /// <returns>A new builder.</returns>
        public static IBy Text(string text, bool? exact = null) => Empty.Text(text, exact);

        /// <summary>
        /// Starts a text regex query. Official <c>by.text()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the element's text.</param>
        /// <returns>A new builder.</returns>
        public static IBy Text(Regex text) => Empty.Text(text);

        /// <summary>
        /// Starts a title query. Official <c>by.title()</c>.
        /// </summary>
        /// <param name="text">Title text to search for.</param>
        /// <param name="exact">When <see langword="true"/>, the title must match exactly.</param>
        /// <returns>A new builder.</returns>
        public static IBy Title(string text, bool? exact = null) => Empty.Title(text, exact);

        /// <summary>
        /// Starts a title regex query. Official <c>by.title()</c>.
        /// </summary>
        /// <param name="text">Regular expression tested against the title.</param>
        /// <returns>A new builder.</returns>
        public static IBy Title(Regex text) => Empty.Title(text);

        /// <summary>
        /// Binds <paramref name="by"/> using <paramref name="factory"/>.
        /// </summary>
        /// <param name="by">The page-free builder.</param>
        /// <param name="factory">Page, frame, locator, or frame-locator factory.</param>
        /// <returns>A locator equivalent to the matching <c>GetBy*</c> chain.</returns>
        internal static ILocator Resolve(IBy by, LocatorBy.IFactory factory)
        {
            if (by == null)
            {
                throw new ArgumentNullException(nameof(by));
            }

            Impl impl = by as Impl;
            if (impl == null)
            {
                throw new ArgumentException("Unknown IBy implementation.", nameof(by));
            }

            return impl.Resolve(factory);
        }

        /// <summary>
        /// Immutable <see cref="IBy"/> implementation. Each chain method
        /// returns a new instance.
        /// </summary>
        internal sealed class Impl : IBy
        {
            internal static readonly Impl EmptyInstance = new Impl(Array.Empty<IBindOp>());

            private readonly IReadOnlyList<IBindOp> _ops;

            private Impl(IReadOnlyList<IBindOp> ops)
            {
                _ops = ops ?? Array.Empty<IBindOp>();
            }

            /// <summary>
            /// One immutable builder step applied when the <see cref="IBy"/> is bound.
            /// </summary>
            private interface IBindOp
            {
                /// <summary>
                /// Applies this step to <paramref name="current"/>, or starts a
                /// locator from <paramref name="factory"/> when
                /// <paramref name="current"/> is <see langword="null"/>.
                /// </summary>
                /// <param name="factory">The bind target.</param>
                /// <param name="current">The locator built so far, or <see langword="null"/>.</param>
                /// <returns>The locator after this step.</returns>
                ILocator Apply(LocatorBy.IFactory factory, ILocator current);
            }

            public IBy First => Nth(0);

            public IBy Last => Append(LastOp.Instance);

            public IBy Get(string selector)
            {
                if (selector == null)
                {
                    throw new ArgumentNullException(nameof(selector));
                }

                return Append(new SelectorOp(selector));
            }

            public IBy Get(IBy by)
            {
                if (by == null)
                {
                    throw new ArgumentNullException(nameof(by));
                }

                Impl other = by as Impl;
                if (other == null)
                {
                    throw new ArgumentException("Unknown IBy implementation.", nameof(by));
                }

                if (other._ops.Count == 0)
                {
                    return this;
                }

                if (_ops.Count == 0)
                {
                    return other;
                }

                List<IBindOp> next = new List<IBindOp>(_ops.Count + other._ops.Count);
                next.AddRange(_ops);
                next.AddRange(other._ops);
                return new Impl(next);
            }

            public IBy AltText(string text, bool? exact = null) => Append(new AltTextOp(text, exact, null));

            public IBy AltText(Regex text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                return Append(new AltTextOp(null, null, text));
            }

            public IBy Label(string text, bool? exact = null) => Append(new LabelOp(text, exact, null));

            public IBy Label(Regex text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                return Append(new LabelOp(null, null, text));
            }

            public IBy Placeholder(string text, bool? exact = null) => Append(new PlaceholderOp(text, exact, null));

            public IBy Placeholder(Regex text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                return Append(new PlaceholderOp(null, null, text));
            }

            public IBy Role(
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
            {
                if (role == null)
                {
                    throw new ArgumentNullException(nameof(role));
                }

                return Append(new RoleOp(
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
            }

            public IBy TestId(string testId) => Append(new TestIdOp(testId, null));

            public IBy TestId(Regex testId)
            {
                if (testId == null)
                {
                    throw new ArgumentNullException(nameof(testId));
                }

                return Append(new TestIdOp(null, testId));
            }

            public IBy Text(string text, bool? exact = null) => Append(new TextOp(text, exact, null));

            public IBy Text(Regex text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                return Append(new TextOp(null, null, text));
            }

            public IBy Title(string text, bool? exact = null) => Append(new TitleOp(text, exact, null));

            public IBy Title(Regex text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                return Append(new TitleOp(null, null, text));
            }

            public IBy Filter(
                string hasText = default,
                Regex hasTextRegex = default,
                IBy has = default,
                IBy hasNot = default,
                string hasNotText = default,
                Regex hasNotTextRegex = default,
                bool? visible = default)
            {
                if (hasText == null
                    && hasTextRegex == null
                    && has == null
                    && hasNot == null
                    && hasNotText == null
                    && hasNotTextRegex == null
                    && !visible.HasValue)
                {
                    return this;
                }

                return Append(new FilterOp(hasText, hasTextRegex, has, hasNot, hasNotText, hasNotTextRegex, visible));
            }

            public IBy And(IBy by)
            {
                if (by == null)
                {
                    throw new ArgumentNullException(nameof(by));
                }

                return Append(new AndOp(by));
            }

            public IBy Or(IBy by)
            {
                if (by == null)
                {
                    throw new ArgumentNullException(nameof(by));
                }

                return Append(new OrOp(by));
            }

            public IBy Nth(int index)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                return Append(new NthOp(index));
            }

            public IBy Describe(string description)
            {
                if (description == null)
                {
                    throw new ArgumentNullException(nameof(description));
                }

                return Append(new DescribeOp(description));
            }

            internal ILocator Resolve(LocatorBy.IFactory factory)
            {
                if (factory == null)
                {
                    throw new ArgumentNullException(nameof(factory));
                }

                if (_ops.Count == 0)
                {
                    throw new PlaywrightNativeException(
                        "Empty \"by\" locator. Start with one of by.role(), by.text(), by.testId() and friends.");
                }

                ILocator current = null;
                for (int i = 0; i < _ops.Count; i++)
                {
                    current = _ops[i].Apply(factory, current);
                }

                return current;
            }

            private Impl Append(IBindOp op)
            {
                List<IBindOp> next = new List<IBindOp>(_ops.Count + 1);
                next.AddRange(_ops);
                next.Add(op);
                return new Impl(next);
            }

            private sealed class SelectorOp : IBindOp
            {
                private readonly string _selector;

                internal SelectorOp(string selector)
                {
                    _selector = selector;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                    => current == null ? factory.Locator(_selector) : current.Locator(_selector);
            }

            private sealed class AltTextOp : IBindOp
            {
                private readonly string _text;
                private readonly bool? _exact;
                private readonly Regex _regex;

                internal AltTextOp(string text, bool? exact, Regex regex)
                {
                    _text = text;
                    _exact = exact;
                    _regex = regex;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (_regex != null)
                    {
                        return current == null ? factory.GetByAltText(_regex) : current.GetByAltText(_regex);
                    }

                    return current == null ? factory.GetByAltText(_text, _exact) : current.GetByAltText(_text, _exact);
                }
            }

            private sealed class LabelOp : IBindOp
            {
                private readonly string _text;
                private readonly bool? _exact;
                private readonly Regex _regex;

                internal LabelOp(string text, bool? exact, Regex regex)
                {
                    _text = text;
                    _exact = exact;
                    _regex = regex;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (_regex != null)
                    {
                        return current == null ? factory.GetByLabel(_regex) : current.GetByLabel(_regex);
                    }

                    return current == null ? factory.GetByLabel(_text, _exact) : current.GetByLabel(_text, _exact);
                }
            }

            private sealed class PlaceholderOp : IBindOp
            {
                private readonly string _text;
                private readonly bool? _exact;
                private readonly Regex _regex;

                internal PlaceholderOp(string text, bool? exact, Regex regex)
                {
                    _text = text;
                    _exact = exact;
                    _regex = regex;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (_regex != null)
                    {
                        return current == null ? factory.GetByPlaceholder(_regex) : current.GetByPlaceholder(_regex);
                    }

                    return current == null
                        ? factory.GetByPlaceholder(_text, _exact)
                        : current.GetByPlaceholder(_text, _exact);
                }
            }

            private sealed class RoleOp : IBindOp
            {
                private readonly string _role;
                private readonly string _name;
                private readonly bool? _exact;
                private readonly bool? _checkedState;
                private readonly bool? _disabled;
                private readonly bool? _expanded;
                private readonly bool? _includeHidden;
                private readonly int? _level;
                private readonly bool? _pressed;
                private readonly bool? _selected;
                private readonly string _description;
                private readonly Regex _descriptionRegex;
                private readonly Regex _nameRegex;

                internal RoleOp(
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
                {
                    _role = role;
                    _name = name;
                    _exact = exact;
                    _checkedState = checkedState;
                    _disabled = disabled;
                    _expanded = expanded;
                    _includeHidden = includeHidden;
                    _level = level;
                    _pressed = pressed;
                    _selected = selected;
                    _description = description;
                    _descriptionRegex = descriptionRegex;
                    _nameRegex = nameRegex;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (current == null)
                    {
                        return factory.GetByRole(
                            _role,
                            _name,
                            _exact,
                            _checkedState,
                            _disabled,
                            _expanded,
                            _includeHidden,
                            _level,
                            _pressed,
                            _selected,
                            _description,
                            _descriptionRegex,
                            _nameRegex);
                    }

                    return current.GetByRole(
                        _role,
                        _name,
                        _exact,
                        _checkedState,
                        _disabled,
                        _expanded,
                        _includeHidden,
                        _level,
                        _pressed,
                        _selected,
                        _description,
                        _descriptionRegex,
                        _nameRegex);
                }
            }

            private sealed class TestIdOp : IBindOp
            {
                private readonly string _testId;
                private readonly Regex _regex;

                internal TestIdOp(string testId, Regex regex)
                {
                    _testId = testId;
                    _regex = regex;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (_regex != null)
                    {
                        return current == null ? factory.GetByTestId(_regex) : current.GetByTestId(_regex);
                    }

                    return current == null ? factory.GetByTestId(_testId) : current.GetByTestId(_testId);
                }
            }

            private sealed class TextOp : IBindOp
            {
                private readonly string _text;
                private readonly bool? _exact;
                private readonly Regex _regex;

                internal TextOp(string text, bool? exact, Regex regex)
                {
                    _text = text;
                    _exact = exact;
                    _regex = regex;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (_regex != null)
                    {
                        return current == null ? factory.GetByText(_regex) : current.GetByText(_regex);
                    }

                    return current == null ? factory.GetByText(_text, _exact) : current.GetByText(_text, _exact);
                }
            }

            private sealed class TitleOp : IBindOp
            {
                private readonly string _text;
                private readonly bool? _exact;
                private readonly Regex _regex;

                internal TitleOp(string text, bool? exact, Regex regex)
                {
                    _text = text;
                    _exact = exact;
                    _regex = regex;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (_regex != null)
                    {
                        return current == null ? factory.GetByTitle(_regex) : current.GetByTitle(_regex);
                    }

                    return current == null ? factory.GetByTitle(_text, _exact) : current.GetByTitle(_text, _exact);
                }
            }

            private sealed class FilterOp : IBindOp
            {
                private readonly string _hasText;
                private readonly Regex _hasTextRegex;
                private readonly IBy _has;
                private readonly IBy _hasNot;
                private readonly string _hasNotText;
                private readonly Regex _hasNotTextRegex;
                private readonly bool? _visible;

                internal FilterOp(
                    string hasText,
                    Regex hasTextRegex,
                    IBy has,
                    IBy hasNot,
                    string hasNotText,
                    Regex hasNotTextRegex,
                    bool? visible)
                {
                    _hasText = hasText;
                    _hasTextRegex = hasTextRegex;
                    _has = has;
                    _hasNot = hasNot;
                    _hasNotText = hasNotText;
                    _hasNotTextRegex = hasNotTextRegex;
                    _visible = visible;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (current == null)
                    {
                        throw new PlaywrightNativeException(
                            "Empty \"by\" locator. Start with one of by.role(), by.text(), by.testId() and friends.");
                    }

                    ILocator result = current;
                    if (_hasText != null)
                    {
                        result = result.Filter(_hasText);
                    }

                    if (_hasTextRegex != null)
                    {
                        result = result.Filter(_hasTextRegex);
                    }

                    if (_has != null)
                    {
                        result = result.Has(factory.BindRelative(_has));
                    }

                    if (_hasNot != null)
                    {
                        result = result.HasNot(factory.BindRelative(_hasNot));
                    }

                    if (_hasNotText != null)
                    {
                        result = result.HasNotText(_hasNotText);
                    }

                    if (_hasNotTextRegex != null)
                    {
                        result = result.HasNotText(_hasNotTextRegex);
                    }

                    if (_visible.HasValue)
                    {
                        result = result.Filter(_visible.Value);
                    }

                    return result;
                }
            }

            private sealed class AndOp : IBindOp
            {
                private readonly IBy _other;

                internal AndOp(IBy other)
                {
                    _other = other;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (current == null)
                    {
                        throw new PlaywrightNativeException(
                            "Empty \"by\" locator. Start with one of by.role(), by.text(), by.testId() and friends.");
                    }

                    return current.And(factory.BindRelative(_other));
                }
            }

            private sealed class OrOp : IBindOp
            {
                private readonly IBy _other;

                internal OrOp(IBy other)
                {
                    _other = other;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (current == null)
                    {
                        throw new PlaywrightNativeException(
                            "Empty \"by\" locator. Start with one of by.role(), by.text(), by.testId() and friends.");
                    }

                    return current.Or(factory.BindRelative(_other));
                }
            }

            private sealed class NthOp : IBindOp
            {
                private readonly int _index;

                internal NthOp(int index)
                {
                    _index = index;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (current == null)
                    {
                        throw new PlaywrightNativeException(
                            "Empty \"by\" locator. Start with one of by.role(), by.text(), by.testId() and friends.");
                    }

                    return current.Nth(_index);
                }
            }

            private sealed class LastOp : IBindOp
            {
                internal static readonly LastOp Instance = new LastOp();

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (current == null)
                    {
                        throw new PlaywrightNativeException(
                            "Empty \"by\" locator. Start with one of by.role(), by.text(), by.testId() and friends.");
                    }

                    return current.Last;
                }
            }

            private sealed class DescribeOp : IBindOp
            {
                private readonly string _description;

                internal DescribeOp(string description)
                {
                    _description = description;
                }

                public ILocator Apply(LocatorBy.IFactory factory, ILocator current)
                {
                    if (current == null)
                    {
                        throw new PlaywrightNativeException(
                            "Empty \"by\" locator. Start with one of by.role(), by.text(), by.testId() and friends.");
                    }

                    return current.Describe(_description);
                }
            }
        }
    }
}
