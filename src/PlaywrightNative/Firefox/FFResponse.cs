/*
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Represents a Firefox network response.
    /// </summary>
    internal class FFResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FFResponse"/> class.
        /// </summary>
        /// <param name="payload">The <c>Network.responseReceived</c> payload.</param>
        /// <param name="request">The associated request.</param>
        public FFResponse(JsonElement payload, FFRequest request)
        {
            Request = request;

            if (payload.TryGetProperty("status", out JsonElement statusEl))
            {
                Status = statusEl.GetInt32();
            }

            if (payload.TryGetProperty("statusText", out JsonElement statusTextEl))
            {
                StatusText = statusTextEl.GetString() ?? string.Empty;
            }

            if (payload.TryGetProperty("url", out JsonElement urlEl))
            {
                Url = urlEl.GetString() ?? string.Empty;
            }

            Headers = ParseHeaders(payload);
        }

        /// <summary>Gets the associated request.</summary>
        internal FFRequest Request { get; }

        /// <summary>Gets the HTTP status code.</summary>
        internal int Status { get; }

        /// <summary>Gets the HTTP status text.</summary>
        internal string StatusText { get; }

        /// <summary>Gets the response URL.</summary>
        internal string Url { get; }

        /// <summary>Gets the response headers.</summary>
        internal IDictionary<string, string> Headers { get; }

        private static IDictionary<string, string> ParseHeaders(JsonElement payload)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!payload.TryGetProperty("headers", out JsonElement headersEl))
            {
                return headers;
            }

            foreach (JsonProperty prop in headersEl.EnumerateObject())
            {
                headers[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }

            return headers;
        }
    }
}
