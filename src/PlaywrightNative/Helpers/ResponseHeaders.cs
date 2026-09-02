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
using System.Collections.Generic;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Parses raw response header text and classifies redirect / navigation-body errors.
    /// </summary>
    internal static class ResponseHeaders
    {
        /// <summary>
        /// Official Playwright message when <c>response.body()</c> is called on a redirect.
        /// </summary>
        internal const string RedirectBodyUnavailable = "Response body is unavailable for redirect responses";

        /// <summary>
        /// Official Playwright message when the page navigated away before the body was read.
        /// </summary>
        internal const string NavigatedAway = "Unable to fetch response body, the page has been navigated away";

        /// <summary>
        /// WebKit joins <c>Set-Cookie</c> with this token on Linux/Windows; macOS uses a comma.
        /// </summary>
        internal const string WebKitSetCookieSeparatorLinux = "playwright-set-cookie-separator";

        private static readonly string[] HeaderLineSeparators = { "\r\n", "\n" };

        /// <summary>
        /// WebKit <c>Set-Cookie</c> join token. Matches official
        /// <c>wkSetCookieSeparator</c>.
        /// </summary>
        internal static string WebKitSetCookieSeparator
            => OperatingSystem.IsMacOS() ? "," : WebKitSetCookieSeparatorLinux;

        /// <summary>
        /// Returns whether <paramref name="status"/> is a 3xx redirect.
        /// </summary>
        /// <param name="status">HTTP status code.</param>
        /// <returns><see langword="true"/> for 300-399.</returns>
        internal static bool IsRedirectStatus(int status)
            => status >= 300 && status <= 399;

        /// <summary>
        /// Official Playwright <c>response.ok()</c>: HTTP 200-299, plus status 0
        /// (file URLs and some WebKit/Firefox navigations).
        /// </summary>
        /// <param name="status">HTTP status code.</param>
        /// <returns><see langword="true"/> when the response is considered successful.</returns>
        internal static bool IsOkStatus(int status)
            => status == 0 || (status >= 200 && status <= 299);

        /// <summary>
        /// Parses an HTTP <c>headersText</c> block into name/value pairs, preserving duplicates.
        /// </summary>
        /// <param name="headersText">Raw header block from extraInfo or the protocol.</param>
        /// <returns>A new list. Never <see langword="null"/>.</returns>
        internal static IReadOnlyList<NameValueEntry> ParseHeadersText(string headersText)
        {
            List<NameValueEntry> list = new();
            if (string.IsNullOrEmpty(headersText))
            {
                return list;
            }

            string[] lines = headersText.Split(HeaderLineSeparators, StringSplitOptions.None);
            foreach (string line in lines)
            {
                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                list.Add(new NameValueEntry(name, value));
            }

            return list;
        }

        /// <summary>
        /// Builds a header pair list from a protocol headers object, expanding
        /// newline-joined <c>set-cookie</c> values into separate entries.
        /// </summary>
        /// <param name="headers">Protocol header map.</param>
        /// <returns>A new list. Never <see langword="null"/>.</returns>
        internal static IReadOnlyList<NameValueEntry> FromMap(IEnumerable<KeyValuePair<string, string>> headers)
        {
            List<NameValueEntry> list = new();
            if (headers == null)
            {
                return list;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                string value = header.Value ?? string.Empty;
                if (string.Equals(header.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                    && value.Contains('\n', StringComparison.Ordinal))
                {
                    foreach (string part in value.Split('\n'))
                    {
                        list.Add(new NameValueEntry(header.Key, part));
                    }
                }
                else
                {
                    list.Add(new NameValueEntry(header.Key, value));
                }
            }

            return list;
        }

        /// <summary>
        /// Expands a WebKit protocol header map. Regular headers are split on
        /// <c>,</c>; <c>set-cookie</c> is split on <see cref="WebKitSetCookieSeparator"/>.
        /// Matches official <c>headersObjectToArray(headers, ',', wkSetCookieSeparator)</c>.
        /// </summary>
        /// <param name="headers">Protocol header map.</param>
        /// <returns>A new list. Never <see langword="null"/>.</returns>
        internal static IReadOnlyList<NameValueEntry> FromWebKitMap(IEnumerable<KeyValuePair<string, string>> headers)
        {
            List<NameValueEntry> list = new();
            if (headers == null)
            {
                return list;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                string value = header.Value ?? string.Empty;
                string separator = string.Equals(header.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                    ? WebKitSetCookieSeparator
                    : ",";
                if (value.Contains(separator, StringComparison.Ordinal))
                {
                    foreach (string part in value.Split(separator))
                    {
                        string trimmed = part.Trim();
                        if (trimmed.Length > 0)
                        {
                            list.Add(new NameValueEntry(header.Key, trimmed));
                        }
                    }
                }
                else if (string.Equals(header.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                    && value.Contains('\n', StringComparison.Ordinal))
                {
                    foreach (string part in value.Split('\n'))
                    {
                        list.Add(new NameValueEntry(header.Key, part));
                    }
                }
                else
                {
                    list.Add(new NameValueEntry(header.Key, value));
                }
            }

            return list;
        }

        /// <summary>
        /// Converts a name/value list into an enumerable of pairs.
        /// </summary>
        /// <param name="entries">Header entries.</param>
        /// <returns>The pairs. Never <see langword="null"/>.</returns>
        internal static IEnumerable<KeyValuePair<string, string>> ToPairs(IEnumerable<NameValueEntry> entries)
        {
            if (entries == null)
            {
                yield break;
            }

            foreach (NameValueEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                yield return new KeyValuePair<string, string>(entry.Name, entry.Value ?? string.Empty);
            }
        }
    }
}
