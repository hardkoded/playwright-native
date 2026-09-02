/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightSharp.Firefox
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
