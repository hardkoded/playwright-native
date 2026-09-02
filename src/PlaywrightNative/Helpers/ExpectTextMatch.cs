// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Text comparison helpers for locator expect matchers.
    /// </summary>
    internal static class ExpectTextMatch
    {
        /// <summary>
        /// Compares <paramref name="actual"/> to <paramref name="expected"/>,
        /// optionally ignoring case.
        /// </summary>
        /// <param name="actual">Received text.</param>
        /// <param name="expected">Expected text or substring.</param>
        /// <param name="exact">When <see langword="true"/>, require equality.</param>
        /// <param name="ignoreCase">When <see langword="true"/>, ignore letter case.</param>
        /// <returns><see langword="true"/> when the texts match.</returns>
        internal static bool Matches(string actual, string expected, bool exact, bool? ignoreCase)
        {
            StringComparison comparison = ignoreCase == true
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return exact
                ? string.Equals(actual, expected, comparison)
                : actual.Contains(expected, comparison);
        }

        /// <summary>
        /// Official expect whitespace flatten: drop zero-width / soft-hyphen,
        /// collapse remaining whitespace, and trim.
        /// </summary>
        /// <param name="value">Raw accessible or expected text.</param>
        /// <returns>Flattened text.</returns>
        internal static string NormalizeWhiteSpace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string cleaned = value
                .Replace("\u200b", string.Empty, StringComparison.Ordinal)
                .Replace("\u00ad", string.Empty, StringComparison.Ordinal);
            return Regex.Replace(cleaned, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Matches <paramref name="actual"/> against <paramref name="expected"/>.
        /// <paramref name="ignoreCase"/> overrides the regular expression flag when set.
        /// </summary>
        /// <param name="actual">Received text.</param>
        /// <param name="expected">Expected pattern.</param>
        /// <param name="ignoreCase">
        /// When set, overrides <see cref="RegexOptions.IgnoreCase"/>.
        /// </param>
        /// <returns><see langword="true"/> when the pattern matches.</returns>
        internal static bool Matches(string actual, Regex expected, bool? ignoreCase)
        {
            if (ignoreCase == null)
            {
                return expected.IsMatch(actual);
            }

            RegexOptions options = expected.Options;
            if (ignoreCase == true)
            {
                options |= RegexOptions.IgnoreCase;
            }
            else
            {
                options &= ~RegexOptions.IgnoreCase;
            }

            if (options == expected.Options)
            {
                return expected.IsMatch(actual);
            }

            return Regex.IsMatch(actual, expected.ToString(), options);
        }

        /// <summary>
        /// Converts official string list expected values to needles.
        /// </summary>
        /// <param name="expected">Expected strings.</param>
        /// <returns>Needles in order.</returns>
        internal static ExpectTextNeedle[] NeedlesFromStrings(IEnumerable<string> expected)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            List<ExpectTextNeedle> needles = new List<ExpectTextNeedle>();
            foreach (string item in expected)
            {
                needles.Add(new ExpectTextNeedle(item ?? string.Empty));
            }

            return needles.ToArray();
        }

        /// <summary>
        /// Converts official regular-expression list expected values to needles.
        /// </summary>
        /// <param name="expected">Expected patterns.</param>
        /// <returns>Needles in order.</returns>
        internal static ExpectTextNeedle[] NeedlesFromRegex(IEnumerable<Regex> expected)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            List<ExpectTextNeedle> needles = new List<ExpectTextNeedle>();
            foreach (Regex item in expected)
            {
                if (item == null)
                {
                    throw new ArgumentNullException(nameof(expected));
                }

                needles.Add(new ExpectTextNeedle(item));
            }

            return needles.ToArray();
        }

        /// <summary>
        /// Converts a mixed official <c>(string | RegExp)[]</c> to needles.
        /// </summary>
        /// <param name="expected">Expected strings or patterns.</param>
        /// <returns>Needles in order.</returns>
        internal static ExpectTextNeedle[] NeedlesFromObjects(IEnumerable<object> expected)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            List<ExpectTextNeedle> needles = new List<ExpectTextNeedle>();
            foreach (object item in expected)
            {
                switch (item)
                {
                    case string text:
                        needles.Add(new ExpectTextNeedle(text));
                        break;
                    case Regex regex:
                        needles.Add(new ExpectTextNeedle(regex));
                        break;
                    case null:
                        throw new ArgumentNullException(nameof(expected));
                    default:
                        throw new ArgumentException(
                            "Expected text items must be string or Regex.",
                            nameof(expected));
                }
            }

            return needles.ToArray();
        }

        /// <summary>
        /// Official string expect: flatten whitespace on both sides, then
        /// compare exactly or as a substring.
        /// </summary>
        /// <param name="actual">Raw received text.</param>
        /// <param name="expected">Raw expected text.</param>
        /// <param name="exact">When <see langword="true"/>, require equality.</param>
        /// <param name="ignoreCase">When <see langword="true"/>, ignore letter case.</param>
        /// <returns><see langword="true"/> when the texts match.</returns>
        internal static bool MatchesNormalized(string actual, string expected, bool exact, bool? ignoreCase)
        {
            return Matches(
                NormalizeWhiteSpace(actual),
                NormalizeWhiteSpace(expected),
                exact,
                ignoreCase);
        }

        /// <summary>
        /// Matches one official expected string or regular expression.
        /// Regular expressions are tested against raw text (no whitespace flatten).
        /// </summary>
        /// <param name="actual">Raw received text.</param>
        /// <param name="needle">Expected string or pattern.</param>
        /// <param name="exact">When <see langword="true"/>, require string equality.</param>
        /// <param name="ignoreCase">Optional case override.</param>
        /// <returns><see langword="true"/> when the needle matches.</returns>
        internal static bool MatchesNeedle(string actual, ExpectTextNeedle needle, bool exact, bool? ignoreCase)
        {
            if (needle == null)
            {
                throw new ArgumentNullException(nameof(needle));
            }

            if (needle.Regex != null)
            {
                return Matches(actual ?? string.Empty, needle.Regex, ignoreCase);
            }

            return MatchesNormalized(actual, needle.String ?? string.Empty, exact, ignoreCase);
        }

        /// <summary>
        /// Official sequential array match. <paramref name="requireLength"/>
        /// is <c>to.have.text.array</c> (same count); otherwise
        /// <c>to.contain.text.array</c> (subsequence).
        /// </summary>
        /// <param name="received">Raw texts in document order.</param>
        /// <param name="needles">Expected strings or patterns.</param>
        /// <param name="requireLength">When <see langword="true"/>, counts must match.</param>
        /// <param name="exact">String equality versus substring.</param>
        /// <param name="ignoreCase">Optional case override.</param>
        /// <returns><see langword="true"/> when the sequence matches.</returns>
        internal static bool MatchesSequence(
            IReadOnlyList<string> received,
            IReadOnlyList<ExpectTextNeedle> needles,
            bool requireLength,
            bool exact,
            bool? ignoreCase)
        {
            if (received == null)
            {
                throw new ArgumentNullException(nameof(received));
            }

            if (needles == null)
            {
                throw new ArgumentNullException(nameof(needles));
            }

            if (requireLength && received.Count != needles.Count)
            {
                return false;
            }

            int matchIndex = 0;
            int receivedIndex = 0;
            while (matchIndex < needles.Count && receivedIndex < received.Count)
            {
                if (MatchesNeedle(received[receivedIndex], needles[matchIndex], exact, ignoreCase))
                {
                    matchIndex++;
                }

                receivedIndex++;
            }

            return matchIndex == needles.Count;
        }

        /// <summary>
        /// Formats a .NET regular expression as official <c>/pattern/flags</c>.
        /// </summary>
        /// <param name="expected">Pattern to print.</param>
        /// <returns>JavaScript-style regular expression literal.</returns>
        internal static string FormatJsRegex(Regex expected)
        {
            if (expected == null)
            {
                return "//";
            }

            StringBuilder flags = new StringBuilder();
            if ((expected.Options & RegexOptions.IgnoreCase) != 0)
            {
                flags.Append('i');
            }

            if ((expected.Options & RegexOptions.Multiline) != 0)
            {
                flags.Append('m');
            }

            if ((expected.Options & RegexOptions.Singleline) != 0)
            {
                flags.Append('s');
            }

            return "/" + expected + "/" + flags;
        }

        /// <summary>
        /// Official expected-line label for a string or pattern needle.
        /// </summary>
        /// <param name="needle">Expected string or pattern.</param>
        /// <param name="negate">When <see langword="true"/>, prefix <c>not </c>.</param>
        /// <returns>Printed expected value.</returns>
        internal static string FormatNeedle(ExpectTextNeedle needle, bool negate)
        {
            if (needle == null)
            {
                return negate ? "not \"\"" : "\"\"";
            }

            string value = needle.Regex != null
                ? FormatJsRegex(needle.Regex)
                : "\"" + (needle.String ?? string.Empty) + "\"";
            return negate ? "not " + value : value;
        }

        /// <summary>
        /// Jest-style array mismatch used by official <c>toHaveText</c> /
        /// <c>toContainText</c> list failures.
        /// </summary>
        /// <param name="received">Raw received texts.</param>
        /// <param name="needles">Expected strings or patterns.</param>
        /// <param name="exact">When <see langword="true"/>, require string equality.</param>
        /// <param name="ignoreCase">Optional case override.</param>
        /// <returns>Diff block.</returns>
        internal static string FormatArrayDiff(
            IReadOnlyList<string> received,
            IReadOnlyList<ExpectTextNeedle> needles,
            bool exact,
            bool? ignoreCase)
        {
            if (received == null)
            {
                received = Array.Empty<string>();
            }

            if (needles == null)
            {
                needles = Array.Empty<ExpectTextNeedle>();
            }

            int matchIndex = 0;
            int receivedIndex = 0;
            while (matchIndex < needles.Count && receivedIndex < received.Count)
            {
                if (MatchesNeedle(received[receivedIndex], needles[matchIndex], exact, ignoreCase))
                {
                    matchIndex++;
                }

                receivedIndex++;
            }

            int extraExpected = needles.Count - matchIndex;
            int extraReceived = received.Count > needles.Count ? received.Count - needles.Count : 0;

            StringBuilder log = new StringBuilder();
            log.Append("- Expected  - ");
            log.Append(extraExpected.ToString(CultureInfo.InvariantCulture));
            log.Append("\n+ Received  + ");
            log.Append(extraReceived.ToString(CultureInfo.InvariantCulture));
            log.Append("\n\n  Array [\n");
            for (int i = 0; i < received.Count; i++)
            {
                log.Append("    \"");
                log.Append(received[i]);
                log.Append("\",\n");
            }

            for (int i = matchIndex; i < needles.Count; i++)
            {
                log.Append("-   ");
                if (needles[i].Regex != null)
                {
                    log.Append(FormatJsRegex(needles[i].Regex));
                }
                else
                {
                    log.Append('"');
                    log.Append(needles[i].String ?? string.Empty);
                    log.Append('"');
                }

                log.Append(",\n");
            }

            log.Append("  ]");
            return log.ToString();
        }
    }
}
