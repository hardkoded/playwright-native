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
    /// Builds Playwright-style header maps and arrays from a header enumerable.
    /// </summary>
    internal static class HeaderMap
    {
        /// <summary>
        /// Returns a lower-cased name map. Duplicate names are joined with
        /// <c>", "</c>, except <c>set-cookie</c> which is joined with newlines.
        /// </summary>
        /// <param name="headers">Request or response headers.</param>
        /// <returns>A new dictionary. Never <see langword="null"/>.</returns>
        internal static Dictionary<string, string> All(IEnumerable<KeyValuePair<string, string>> headers)
        {
            Dictionary<string, string> map = new(StringComparer.Ordinal);
            if (headers == null)
            {
                return map;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

#pragma warning disable CA1308 // Playwright AllHeadersAsync exposes lower-cased names.
                string key = header.Key.ToLowerInvariant();
#pragma warning restore CA1308
                string value = header.Value ?? string.Empty;
                if (map.TryGetValue(key, out string existing))
                {
                    map[key] = Join(key, existing, value);
                }
                else
                {
                    map[key] = value;
                }
            }

            return map;
        }

        /// <summary>
        /// Returns every header as a name/value entry, preserving order.
        /// </summary>
        /// <param name="headers">Request or response headers.</param>
        /// <returns>A new list. Never <see langword="null"/>.</returns>
        internal static IReadOnlyList<NameValueEntry> Array(IEnumerable<KeyValuePair<string, string>> headers)
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

                list.Add(new NameValueEntry(header.Key, header.Value ?? string.Empty));
            }

            return list;
        }

        /// <summary>
        /// Returns the joined header value matching <paramref name="name"/>, or <see langword="null"/>.
        /// <c>set-cookie</c> values are joined with newlines; other names use <c>", "</c>.
        /// </summary>
        /// <param name="headers">Request or response headers.</param>
        /// <param name="name">Header name.</param>
        /// <returns>The value, or <see langword="null"/>.</returns>
        internal static string Value(IEnumerable<KeyValuePair<string, string>> headers, string name)
        {
            IReadOnlyList<string> values = Values(headers, name);
            if (values.Count == 0)
            {
                return null;
            }

            string joined = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                joined = Join(name, joined, values[i]);
            }

            return joined;
        }

        /// <summary>
        /// Sets <paramref name="name"/> on <paramref name="headers"/>, replacing
        /// any existing key that matches case-insensitively.
        /// </summary>
        /// <param name="headers">The mutable header map.</param>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        internal static void Set(IDictionary<string, string> headers, string name, string value)
        {
            if (headers == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            string existing = null;
            foreach (string key in headers.Keys)
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    existing = key;
                    break;
                }
            }

            if (existing != null)
            {
                headers[existing] = value ?? string.Empty;
            }
            else
            {
                headers[name] = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Returns every header value matching <paramref name="name"/>.
        /// </summary>
        /// <param name="headers">Request or response headers.</param>
        /// <param name="name">Header name.</param>
        /// <returns>A new list. Never <see langword="null"/>.</returns>
        internal static IReadOnlyList<string> Values(IEnumerable<KeyValuePair<string, string>> headers, string name)
        {
            List<string> list = new();
            if (headers == null || string.IsNullOrEmpty(name))
            {
                return list;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    string value = header.Value ?? string.Empty;
                    if (string.Equals(name, "set-cookie", StringComparison.OrdinalIgnoreCase)
                        && value.Contains('\n', StringComparison.Ordinal)
                        && list.Count == 0)
                    {
                        foreach (string part in value.Split('\n'))
                        {
                            list.Add(part);
                        }
                    }
                    else
                    {
                        list.Add(value);
                    }
                }
            }

            return list;
        }

        private static string Join(string name, string existing, string value)
        {
            if (string.Equals(name, "set-cookie", StringComparison.OrdinalIgnoreCase))
            {
                return existing + "\n" + value;
            }

            return existing + ", " + value;
        }
    }
}
