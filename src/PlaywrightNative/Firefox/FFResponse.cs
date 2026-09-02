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
