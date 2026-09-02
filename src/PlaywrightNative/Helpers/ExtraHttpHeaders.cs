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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Builds the header map for <c>Network.setExtraHTTPHeaders</c>.
    /// </summary>
    internal static class ExtraHttpHeaders
    {
        /// <summary>
        /// Copies <paramref name="headers"/> into an ordinal dictionary.
        /// </summary>
        /// <param name="headers">Header pairs. Null is treated as empty.</param>
        /// <returns>A name-to-value map.</returns>
        internal static Dictionary<string, string> ToMap(IEnumerable<KeyValuePair<string, string>> headers)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (headers == null)
            {
                return map;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (header.Value == null)
                {
                    throw new PlaywrightNativeException(
                        "Expected value of header \"" + header.Key + "\" to be String, but \"object\" is found.");
                }

                map[header.Key] = header.Value;
            }

            return map;
        }

        /// <summary>
        /// Copies <paramref name="headers"/> into an ordinal dictionary, throwing when a
        /// value is not a string (Playwright <c>setExtraHTTPHeaders</c> parity).
        /// </summary>
        /// <param name="headers">Header pairs whose values may be boxed. Null is treated as empty.</param>
        /// <returns>A name-to-value map.</returns>
        internal static Dictionary<string, string> ToMap(IEnumerable<KeyValuePair<string, object>> headers)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (headers == null)
            {
                return map;
            }

            foreach (KeyValuePair<string, object> header in headers)
            {
                if (header.Value is string text)
                {
                    map[header.Key] = text;
                    continue;
                }

                throw new PlaywrightNativeException(
                    "Expected value of header \"" + header.Key + "\" to be String, but \"" + JavaScriptTypeName(header.Value) + "\" is found.");
            }

            return map;
        }

        /// <summary>
        /// Official <c>network.mergeHeaders</c>: later maps override earlier ones
        /// by lower-cased name, keeping the last original casing.
        /// </summary>
        /// <param name="context">The owning context, or <see langword="null"/>.</param>
        /// <param name="pageHeaders">Page <c>setExtraHTTPHeaders</c> values.</param>
        /// <returns>The merged header map.</returns>
        internal static Dictionary<string, string> Merged(
            IBrowserContext context,
            IEnumerable<KeyValuePair<string, string>> pageHeaders)
        {
            IEnumerable<KeyValuePair<string, string>> contextHeaders = null;
            if (context is IHasExtraHttpHeaders extra)
            {
                contextHeaders = extra.ExtraHttpHeaders;
            }

            return Merge(contextHeaders, pageHeaders);
        }

        /// <summary>
        /// Applies merged extra headers on pages that store page-level values.
        /// </summary>
        /// <param name="page">The page to update.</param>
        /// <returns>A task that completes when the protocol update is sent.</returns>
        internal static Task ApplyMergedAsync(IPage page)
        {
            if (page is IAppliesMergedExtraHttpHeaders merged)
            {
                return merged.ApplyMergedExtraHttpHeadersAsync();
            }

            return Task.CompletedTask;
        }

        private static Dictionary<string, string> Merge(
            IEnumerable<KeyValuePair<string, string>> contextHeaders,
            IEnumerable<KeyValuePair<string, string>> pageHeaders)
        {
            Dictionary<string, string> lowerToValue = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> lowerToOriginal = new Dictionary<string, string>(StringComparer.Ordinal);
            Append(contextHeaders, lowerToValue, lowerToOriginal);
            Append(pageHeaders, lowerToValue, lowerToOriginal);
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in lowerToValue)
            {
                result[lowerToOriginal[entry.Key]] = entry.Value;
            }

            return result;
        }

        private static void Append(
            IEnumerable<KeyValuePair<string, string>> headers,
            Dictionary<string, string> lowerToValue,
            Dictionary<string, string> lowerToOriginal)
        {
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

#pragma warning disable CA1308 // Official mergeHeaders keys on lower-cased names.
                string lower = header.Key.ToLowerInvariant();
#pragma warning restore CA1308
                lowerToOriginal[lower] = header.Key;
                lowerToValue[lower] = header.Value ?? string.Empty;
            }
        }

        private static string JavaScriptTypeName(object value)
        {
            if (value is null)
            {
                return "object";
            }

            if (value is bool)
            {
                return "boolean";
            }

            if (value is byte
                || value is sbyte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal)
            {
                return "number";
            }

            return "object";
        }
    }
}
