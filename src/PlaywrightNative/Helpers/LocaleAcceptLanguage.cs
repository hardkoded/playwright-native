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
    /// Official locale Accept-Language: a default that user <c>fetch</c> headers
    /// can override. Chromium before 151 and WebKit send the browser language
    /// on WebSocket handshakes unless we rewrite them.
    /// </summary>
    internal static class LocaleAcceptLanguage
    {
        /// <summary>
        /// Returns headers with the locale Accept-Language when the request
        /// still has the browser default (or none).
        /// </summary>
        /// <param name="headers">Current request headers.</param>
        /// <param name="locale">Context locale, or <see langword="null"/>.</param>
        /// <param name="isWebSocket">When <see langword="true"/>, always prefer the locale.</param>
        /// <returns>The original map, a merged map, or <see langword="null"/>.</returns>
        internal static IDictionary<string, string> Merge(
            IEnumerable<KeyValuePair<string, string>> headers,
            string locale,
            bool isWebSocket = false)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return headers as IDictionary<string, string>;
            }

            string existing = HeaderMap.Value(headers, "accept-language");
            if (ShouldKeep(existing, locale, isWebSocket))
            {
                return headers as IDictionary<string, string>;
            }

            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    if (!string.IsNullOrEmpty(header.Key))
                    {
                        result[header.Key] = header.Value;
                    }
                }
            }

            result["Accept-Language"] = locale;
            return result;
        }

        /// <summary>
        /// Whether <paramref name="resourceType"/> is a WebSocket handshake.
        /// </summary>
        /// <param name="resourceType">Playwright / CDP resource type.</param>
        /// <returns><see langword="true"/> for WebSocket requests.</returns>
        internal static bool IsWebSocket(string resourceType)
            => !string.IsNullOrEmpty(resourceType)
                && resourceType.Contains("websocket", StringComparison.OrdinalIgnoreCase);

        private static bool ShouldKeep(string existing, string locale, bool isWebSocket)
        {
            if (string.IsNullOrEmpty(existing))
            {
                return false;
            }

            if (existing.Contains(locale, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (isWebSocket)
            {
                return false;
            }

            // Browser defaults usually include q-values or a list. A single
            // tag such as "de" is a user fetch() override (issue #23732).
            if (existing.Contains(',') || existing.Contains(';'))
            {
                return false;
            }

            return !IsBrowserDefaultTag(existing);
        }

        private static bool IsBrowserDefaultTag(string existing)
        {
            return existing.Equals("en", StringComparison.OrdinalIgnoreCase)
                || existing.Equals("en-US", StringComparison.OrdinalIgnoreCase);
        }
    }
}
