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
    /// Represents a Firefox network request.
    /// </summary>
    internal class FFRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FFRequest"/> class.
        /// </summary>
        /// <param name="payload">The <c>Network.requestWillBeSent</c> payload.</param>
        /// <param name="requestId">The request ID.</param>
        public FFRequest(JsonElement payload, string requestId)
        {
            RequestId = requestId;

            if (payload.TryGetProperty("url", out JsonElement urlEl))
            {
                Url = urlEl.GetString() ?? string.Empty;
            }

            if (payload.TryGetProperty("method", out JsonElement methodEl))
            {
                Method = methodEl.GetString() ?? "GET";
            }

            if (payload.TryGetProperty("resourceType", out JsonElement typeEl))
            {
                ResourceType = typeEl.GetString() ?? string.Empty;
            }

            Headers = ParseHeaders(payload);
            PostData = payload.TryGetProperty("postData", out JsonElement pdEl) ? pdEl.GetString() : null;
        }

        /// <summary>Gets the Juggler request ID.</summary>
        internal string RequestId { get; }

        /// <summary>Gets the request URL.</summary>
        internal string Url { get; }

        /// <summary>Gets the HTTP method.</summary>
        internal string Method { get; }

        /// <summary>Gets the resource type (e.g. "document", "script").</summary>
        internal string ResourceType { get; }

        /// <summary>Gets the request headers.</summary>
        internal IDictionary<string, string> Headers { get; }

        /// <summary>Gets the POST data, if any.</summary>
        internal string PostData { get; }

        /// <summary>Gets or sets the response associated with this request.</summary>
        internal FFResponse Response { get; set; }

        /// <summary>Gets or sets the failure error text.</summary>
        internal string FailureText { get; set; }

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
