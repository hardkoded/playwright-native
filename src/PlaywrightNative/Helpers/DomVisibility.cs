/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official injected <c>isElementVisible</c>: style visibility, closed
    /// <c>&lt;details&gt;</c>, <c>display:contents</c>, and a non-empty box.
    /// Opacity 0 and off-screen boxes stay visible. Used by element-handle and
    /// page-level <c>isVisible</c> / <c>isHidden</c>.
    /// </summary>
    internal static class DomVisibility
    {
        /// <summary>
        /// JavaScript function <c>el => boolean</c> matching official
        /// <c>isElementVisible</c> in <c>domUtils.ts</c>.
        /// </summary>
        internal const string IsVisibleFunction = @"el => {
    function isVisibleTextNode(node) {
        const range = node.ownerDocument.createRange();
        range.selectNode(node);
        const rect = range.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
    }
    function isStyleVisible(element, style) {
        const ua = navigator.userAgent || '';
        const isWebKit = /AppleWebKit/.test(ua) && !/Chrome|Chromium|Edg\//.test(ua);
        if (typeof element.checkVisibility === 'function' && !isWebKit) {
            if (!element.checkVisibility()) return false;
        }
        const detailsOrSummary = element.closest && element.closest('details,summary');
        if (detailsOrSummary && detailsOrSummary !== element && detailsOrSummary.nodeName === 'DETAILS' && !detailsOrSummary.open)
            return false;
        if (style.visibility !== 'visible') return false;
        return true;
    }
    function isVisible(element) {
        if (!element) return false;
        const view = element.ownerDocument && element.ownerDocument.defaultView;
        const style = view ? view.getComputedStyle(element) : null;
        if (!style) return true;
        if (style.display === 'contents') {
            for (let child = element.firstChild; child; child = child.nextSibling) {
                if (child.nodeType === 1 && isVisible(child)) return true;
                if (child.nodeType === 3 && isVisibleTextNode(child)) return true;
            }
            return false;
        }
        if (!isStyleVisible(element, style)) return false;
        const rect = element.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
    }
    return isVisible(el);
}";

        private static readonly HashSet<string> KnownEngines = new HashSet<string>(StringComparer.Ordinal)
        {
            "css",
            "xpath",
            "text",
            "id",
            "role",
            "data-testid",
            "data-test-id",
            "data-test",
            "nth",
            "visible",
            "light",
            "attr",
            "placeholder",
            "label",
            "title",
            "alt",
            "text-is",
            "text-matches",
            "has-text",
            "has",
            "has-not",
            "and",
            "or",
            "chain",
            "layout",
            "control",
            "describe",
            "testid",
            "aria-ref",
            "css:light",
        };

        /// <summary>
        /// Official <c>Unknown engine "name" while parsing selector …</c> for
        /// <c>name=body</c> prefixes that are neither builtin nor registered.
        /// </summary>
        /// <param name="selector">A locator selector, possibly a <c>&gt;&gt;</c> chain.</param>
        internal static void ThrowIfUnknownEngine(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return;
            }

            if (selector.Contains("##", StringComparison.Ordinal))
            {
                throw new PlaywrightNativeException(
                    "Unexpected token \"#\" while parsing css selector \"" + selector + "\". Did you mean to CSS.escape it?");
            }

            if (selector.Contains(']') && !selector.Contains('['))
            {
                throw new PlaywrightNativeException(
                    "Unexpected token \"]\" while parsing css selector \"" + selector + "\"");
            }

            IReadOnlyList<string> parts = SplitChain(selector);
            for (int i = 0; i < parts.Count; i++)
            {
                string part = parts[i];
                if (part.Length > 0 && part[0] == '*')
                {
                    part = part.Substring(1);
                }

                int equals = part.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                string name = part.Substring(0, equals);
                if (!IsEngineName(name))
                {
                    continue;
                }

                if (IsKnownEngine(name) || CustomSelectors.TryResolve(part, out _))
                {
                    continue;
                }

                throw new PlaywrightNativeException(
                    "Unknown engine \"" + name + "\" while parsing selector " + selector);
            }
        }

        /// <summary>
        /// Official <c>isVisibleInternal</c> swallows navigation / destroyed-context
        /// errors and returns not-visible. Strict-mode, unknown-engine, and closed
        /// session errors stay fatal.
        /// </summary>
        /// <param name="ex">The error from a visibility query.</param>
        /// <returns><see langword="true"/> when the caller should treat the element as missing.</returns>
        internal static bool IsTransientVisibilityError(Exception ex)
        {
            if (ex == null || ex is TargetClosedException)
            {
                return false;
            }

            string message = ex.Message ?? string.Empty;
            if (message.Contains("strict mode", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Unknown engine", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Target closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Session closed", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return message.Contains("Execution context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cannot find context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Inspected target", StringComparison.OrdinalIgnoreCase)
                || message.Contains("destroyed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("navigat", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Queries <paramref name="selector"/> once and reports whether the first match is visible.
        /// A missing element is not visible. Disposes the intermediate handle.
        /// </summary>
        /// <param name="querySelectorAsync">One-shot CSS query.</param>
        /// <param name="selector">CSS selector.</param>
        /// <returns><see langword="true"/> when the first match is visible.</returns>
        internal static async Task<bool> IsSelectorVisibleAsync(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector)
        {
            if (querySelectorAsync == null)
            {
                throw new ArgumentNullException(nameof(querySelectorAsync));
            }

            IElementHandle handle = await querySelectorAsync(selector).ConfigureAwait(false);
            if (handle == null)
            {
                return false;
            }

            try
            {
                return await handle.IsVisibleAsync().ConfigureAwait(false);
            }
            finally
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static bool IsKnownEngine(string name)
        {
            if (KnownEngines.Contains(name))
            {
                return true;
            }

            return name.StartsWith("internal:", StringComparison.Ordinal)
                || name.StartsWith("css:", StringComparison.Ordinal);
        }

        private static bool IsEngineName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-'
                    || c == ':'
                    || c == '+'
                    || c == '*';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<string> SplitChain(string selector)
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
    }
}
