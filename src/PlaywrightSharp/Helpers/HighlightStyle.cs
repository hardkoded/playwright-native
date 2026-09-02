/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Text;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>locator.highlight({ style })</c> object form.
    /// </summary>
    internal static class HighlightStyle
    {
        /// <summary>
        /// Converts a JS-style camelCase map to inline CSS.
        /// </summary>
        /// <param name="style">Official object style properties.</param>
        /// <returns>Inline CSS, or <see langword="null"/> when empty.</returns>
        internal static string ToCss(IReadOnlyDictionary<string, string> style)
        {
            if (style == null || style.Count == 0)
            {
                return null;
            }

            StringBuilder css = new();
            foreach (KeyValuePair<string, string> pair in style)
            {
                if (css.Length > 0)
                {
                    css.Append("; ");
                }

                css.Append(ToCssName(pair.Key));
                css.Append(": ");
                css.Append(pair.Value);
            }

            return css.ToString();
        }

        private static string ToCssName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            StringBuilder css = new();
            foreach (char c in name)
            {
                if (char.IsUpper(c))
                {
                    if (css.Length > 0)
                    {
                        css.Append('-');
                    }

                    css.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    css.Append(c);
                }
            }

            return css.ToString();
        }
    }
}
