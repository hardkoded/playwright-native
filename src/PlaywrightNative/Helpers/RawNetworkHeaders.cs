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
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Builds official Playwright raw-header arrays from protocol extra-info
    /// objects (values split on <c>\n</c>) and joins them for
    /// <c>allHeaders</c> / <c>headerValue</c>.
    /// </summary>
    internal static class RawNetworkHeaders
    {
        /// <summary>
        /// Converts a protocol headers object into a name/value list, splitting
        /// each value on <paramref name="separator"/>.
        /// </summary>
        /// <param name="headers">The protocol headers object.</param>
        /// <param name="separator">Multi-value separator. Defaults to newline.</param>
        /// <returns>The header list.</returns>
        internal static IReadOnlyList<NameValueEntry> FromObject(JsonElement headers, string separator = "\n")
        {
            List<NameValueEntry> list = new();
            if (headers.ValueKind != JsonValueKind.Object)
            {
                return list;
            }

            foreach (JsonProperty property in headers.EnumerateObject())
            {
                string value = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();
                if (!string.IsNullOrEmpty(separator) && value.Contains(separator, StringComparison.Ordinal))
                {
                    string[] parts = value.Split(new[] { separator }, StringSplitOptions.None);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        list.Add(new NameValueEntry(property.Name, parts[i]));
                    }
                }
                else
                {
                    list.Add(new NameValueEntry(property.Name, value));
                }
            }

            return list;
        }

        /// <summary>
        /// Lower-cased name map; duplicate names are joined with <c>, </c>.
        /// </summary>
        /// <param name="headers">The header list.</param>
        /// <returns>The joined map.</returns>
        internal static Dictionary<string, string> AllJoined(IEnumerable<NameValueEntry> headers)
        {
            Dictionary<string, List<string>> grouped = new(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (NameValueEntry header in headers)
                {
                    if (string.IsNullOrEmpty(header.Name))
                    {
                        continue;
                    }

                    if (!grouped.TryGetValue(header.Name, out List<string> values))
                    {
                        values = new List<string>();
                        grouped[header.Name] = values;
                    }

                    values.Add(header.Value ?? string.Empty);
                }
            }

            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> pair in grouped)
            {
#pragma warning disable CA1308 // Playwright AllHeadersAsync exposes lower-cased names.
                string key = pair.Key.ToLowerInvariant();
#pragma warning restore CA1308
                map[key] = string.Join(", ", pair.Value);
            }

            return map;
        }

        /// <summary>
        /// Joins every value for <paramref name="name"/> with <c>, </c>
        /// (or newline for <c>set-cookie</c>).
        /// </summary>
        /// <param name="headers">The header list.</param>
        /// <param name="name">Header name.</param>
        /// <returns>The joined value, or <see langword="null"/>.</returns>
        internal static string JoinedValue(IEnumerable<NameValueEntry> headers, string name)
        {
            if (headers == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            List<string> values = new();
            foreach (NameValueEntry header in headers)
            {
                if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(header.Value ?? string.Empty);
                }
            }

            if (values.Count == 0)
            {
                return null;
            }

            string separator = string.Equals(name, "set-cookie", StringComparison.OrdinalIgnoreCase)
                ? "\n"
                : ", ";
            return string.Join(separator, values);
        }
    }
}
