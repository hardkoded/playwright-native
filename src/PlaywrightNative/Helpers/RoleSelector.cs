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
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>internal:role=</c> builder used by <c>getByRole</c>.
    /// Extra optionals stay at the end.
    /// </summary>
    internal static class RoleSelector
    {
        /// <summary>
        /// Builds <c>internal:role=…</c> matching official Playwright
        /// <c>GetByRoleSelector</c>.
        /// </summary>
        /// <param name="role">ARIA role name.</param>
        /// <param name="name">Optional accessible name filter.</param>
        /// <param name="exact">When <see langword="true"/>, name/description match exactly.</param>
        /// <param name="checkedState">Optional checked state.</param>
        /// <param name="disabled">Optional disabled state.</param>
        /// <param name="expanded">Optional expanded state.</param>
        /// <param name="includeHidden">When set, include hidden matches.</param>
        /// <param name="level">Optional heading / listitem level.</param>
        /// <param name="pressed">Optional pressed state.</param>
        /// <param name="selected">Optional selected state.</param>
        /// <param name="description">Optional accessible description filter.</param>
        /// <param name="descriptionRegex">Optional description regular expression.</param>
        /// <param name="nameRegex">Optional name regular expression.</param>
        /// <returns>An <c>internal:role</c> selector.</returns>
        internal static string Build(
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

            bool exactMatch = exact == true;
            StringBuilder selector = new StringBuilder("internal:role=");
            selector.Append(role);
            if (checkedState.HasValue)
            {
                selector.Append("[checked=").Append(BoolToken(checkedState.Value)).Append(']');
            }

            if (disabled.HasValue)
            {
                selector.Append("[disabled=").Append(BoolToken(disabled.Value)).Append(']');
            }

            if (selected.HasValue)
            {
                selector.Append("[selected=").Append(BoolToken(selected.Value)).Append(']');
            }

            if (expanded.HasValue)
            {
                selector.Append("[expanded=").Append(BoolToken(expanded.Value)).Append(']');
            }

            if (includeHidden.HasValue)
            {
                selector.Append("[include-hidden=").Append(BoolToken(includeHidden.Value)).Append(']');
            }

            if (level.HasValue)
            {
                selector.Append("[level=").Append(level.Value.ToString(CultureInfo.InvariantCulture)).Append(']');
            }

            if (name != null)
            {
                selector.Append("[name=").Append(EscapeForAttributeSelector(name, exactMatch)).Append(']');
            }
            else if (nameRegex != null)
            {
                selector.Append("[name=").Append(EscapeRegexForSelector(nameRegex)).Append(']');
            }

            if (description != null)
            {
                selector.Append("[description=").Append(EscapeForAttributeSelector(description, exactMatch)).Append(']');
            }
            else if (descriptionRegex != null)
            {
                selector.Append("[description=").Append(EscapeRegexForSelector(descriptionRegex)).Append(']');
            }

            if (pressed.HasValue)
            {
                selector.Append("[pressed=").Append(BoolToken(pressed.Value)).Append(']');
            }

            return selector.ToString();
        }

        private static string BoolToken(bool value) => value ? "true" : "false";

        private static string EscapeForAttributeSelector(string value, bool exact)
        {
            string exactFlag = exact ? string.Empty : "i";
            return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"" + exactFlag;
        }

        private static string EscapeRegexForSelector(Regex text)
        {
            ArgumentNullException.ThrowIfNull(text);
            string source = text.ToString();
            string flags = GetByAllScript.Flags(text);
            string raw = "/" + source + "/" + flags;
            string escaped = Regex.Replace(raw, @"(^|[^\\])(\\\\)*([""'`])", "$1$2\\$3");
            return escaped.Replace(">>", "\\>\\>", StringComparison.Ordinal);
        }
    }
}
