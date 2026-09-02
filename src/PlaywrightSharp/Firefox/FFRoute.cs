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
    /// Represents a route handler for a Firefox intercepted request. Exposes
    /// <see cref="ContinueAsync"/>, <see cref="FulfillAsync"/>, and <see cref="AbortAsync"/>.
    /// </summary>
    internal class FFRoute
    {
        private readonly FFSession _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFRoute"/> class.
        /// </summary>
        /// <param name="session">The Juggler page session.</param>
        /// <param name="request">The intercepted request.</param>
        public FFRoute(FFSession session, FFRequest request)
        {
            _session = session;
            Request = request;
        }

        /// <summary>Gets the intercepted request.</summary>
        internal FFRequest Request { get; }

        /// <summary>
        /// Continues the request, optionally overriding URL, method, headers, or POST data.
        /// </summary>
        /// <param name="url">Optional URL override.</param>
        /// <param name="method">Optional method override.</param>
        /// <param name="postData">Optional POST data override.</param>
        /// <param name="headers">Optional headers override.</param>
        internal Task ContinueAsync(
            string url = null,
            string method = null,
            string postData = null,
            IDictionary<string, string> headers = null)
            => _session.SendAsync("Network.resumeInterceptedRequest", new
            {
                requestId = Request.RequestId,
                url,
                method,
                postData,
                headers = headers != null ? SerializeHeaders(headers) : null,
            });

        /// <summary>
        /// Fulfills the request with a synthetic response.
        /// </summary>
        /// <param name="status">The HTTP status code.</param>
        /// <param name="body">The response body.</param>
        /// <param name="headers">Optional response headers.</param>
        /// <param name="contentType">Optional Content-Type header.</param>
        internal Task FulfillAsync(
            int status = 200,
            string body = null,
            IDictionary<string, string> headers = null,
            string contentType = null)
        {
            var responseHeaders = headers != null
                ? new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(contentType))
            {
                responseHeaders["content-type"] = contentType;
            }

            return _session.SendAsync("Network.fulfillInterceptedRequest", new
            {
                requestId = Request.RequestId,
                status,
                headers = SerializeHeaders(responseHeaders),
                base64body = body != null
                    ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body))
                    : null,
            });
        }

        /// <summary>
        /// Aborts the request with the given error code.
        /// </summary>
        /// <param name="errorCode">The network error code (e.g. "Failed", "Aborted").</param>
        internal Task AbortAsync(string errorCode = "Failed")
            => _session.SendAsync("Network.abortInterceptedRequest", new
            {
                requestId = Request.RequestId,
                errorCode,
            });

        private static object[] SerializeHeaders(IDictionary<string, string> headers)
        {
            var result = new List<object>();
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                result.Add(new { name = kvp.Key, value = kvp.Value });
            }

            return result.ToArray();
        }
    }
}
