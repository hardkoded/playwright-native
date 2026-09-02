/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Represents a network response captured via the WebKit Inspector Protocol
    /// (<c>Network.responseReceived</c>). Links back to its originating
    /// <see cref="WKRequest"/> and provides methods for retrieving the response body.
    /// Mirrors <c>CRResponse</c>. Implements <see cref="IResponse"/> directly.
    /// </summary>
    internal partial class WKResponse : IResponse
    {
        private readonly WKTargetSession _session;
        private readonly string _requestId;
        private readonly TaskCompletionSource<bool> _extraHeaders =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private IReadOnlyList<NameValueEntry> _headerPairs;
        private Task<byte[]> _bodyTask;
        private byte[] _body;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKResponse"/> class.
        /// </summary>
        /// <param name="session">The target session used to fetch the response body.</param>
        /// <param name="request">The request associated with this response.</param>
        /// <param name="url">The response URL.</param>
        /// <param name="status">The HTTP status code.</param>
        /// <param name="statusText">The HTTP status text.</param>
        /// <param name="headers">The response headers (case-insensitive keys).</param>
        /// <param name="serverAddr">Remote address, or <see langword="null"/>.</param>
        /// <param name="securityDetails">TLS details, or <see langword="null"/>.</param>
        /// <param name="fromServiceWorker">Whether a service worker served the response.</param>
        /// <param name="httpVersion">HTTP protocol version, or <see langword="null"/>.</param>
        /// <param name="protocolRequestId">
        /// Protocol request id for <c>Network.getResponseBody</c>. When omitted,
        /// <paramref name="request"/>'s id is used. Process-swap adoption passes
        /// the new session's id while keeping the original public request.
        /// </param>
        public WKResponse(
            WKTargetSession session,
            WKRequest request,
            string url,
            int status,
            string statusText,
            IDictionary<string, string> headers,
            ResponseServerAddrResult serverAddr = null,
            ResponseSecurityDetailsResult securityDetails = null,
            bool fromServiceWorker = false,
            string httpVersion = null,
            string protocolRequestId = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _requestId = !string.IsNullOrEmpty(protocolRequestId)
                ? protocolRequestId
                : request?.RequestId ?? throw new ArgumentNullException(nameof(request));
            WKRequest = request;
            Status = status;
            StatusText = statusText;
            ServerAddr = serverAddr;
            SecurityDetails = securityDetails;
            FromServiceWorker = fromServiceWorker;
            HttpVersion = httpVersion;

            // Link the response back to the request.
            request.Response = this;
            _headerPairs = ResponseHeaders.FromWebKitMap(headers);
        }

        /// <inheritdoc/>
        public string Url => WKRequest.Url;

        /// <inheritdoc/>
        public int Status { get; }

        /// <inheritdoc/>
        public string StatusText { get; }

        /// <inheritdoc/>
        public Dictionary<string, string> Headers
            => HeaderMap.All(ResponseHeaders.ToPairs(_headerPairs));

        /// <inheritdoc/>
        public IRequest Request => WKRequest;

        /// <inheritdoc/>
        public bool Ok => ResponseHeaders.IsOkStatus(Status);

        /// <inheritdoc/>
        public IFrame Frame => Request?.Frame;

        /// <inheritdoc/>
        public HttpStatusCode StatusCode => (HttpStatusCode)Status;

        /// <inheritdoc/>
        public bool FromServiceWorker { get; }

        /// <summary>
        /// Gets the HTTP protocol version, or <see langword="null"/>.
        /// </summary>
        internal string HttpVersion { get; set; }

        /// <summary>
        /// Gets the WebKit request associated with this response.
        /// </summary>
        internal WKRequest WKRequest { get; }

        /// <summary>
        /// Gets the remote server address, or <see langword="null"/>.
        /// </summary>
        internal ResponseServerAddrResult ServerAddr { get; set; }

        /// <summary>
        /// Gets TLS details, or <see langword="null"/>.
        /// </summary>
        internal ResponseSecurityDetailsResult SecurityDetails { get; set; }

        /// <summary>
        /// Raw <c>Network.responseReceived</c> payload for WebKit metrics.
        /// </summary>
        internal JsonElement ResponsePayload { get; set; }

        /// <inheritdoc/>
        public Task<byte[]> BodyAsync()
            => GetBodyBytesAsync();

        /// <inheritdoc/>
        public Task<string> FinishedAsync()
            => WKRequest.WaitUntilFinishedAsync();

        /// <inheritdoc/>
        public Task<T> JsonAsync<T>()
            => ResponseContent.ReadJsonAsync<T>(GetBodyBytesAsync);

        /// <inheritdoc/>
        public Task<string> TextAsync()
            => ResponseContent.ReadTextAsync(GetBodyBytesAsync);

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> AllHeadersAsync()
        {
            await WaitForExtraHeadersAsync().ConfigureAwait(false);
            return HeaderMap.All(Headers);
        }

        /// <inheritdoc/>
        public async Task<string> HeaderValueAsync(string name)
        {
            await WaitForExtraHeadersAsync().ConfigureAwait(false);
            return HeaderMap.Value(Headers, name);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> HeaderValuesAsync(string name)
        {
            await WaitForExtraHeadersAsync().ConfigureAwait(false);
            return HeaderMap.Values(Headers, name);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Header>> HeadersArrayAsync()
        {
            await WaitForExtraHeadersAsync().ConfigureAwait(false);
            return HeaderMap.Array(Headers)
                .Select(e => new Header { Name = e.Name, Value = e.Value })
                .ToList();
        }

        /// <inheritdoc/>
        public Task<string> HttpVersionAsync()
            => Task.FromResult(ResponseNetworkInfo.NormalizeHttpVersion(HttpVersion));

        public async Task<ResponseServerAddrResult> ServerAddrAsync()
        {
            await WKRequest.WaitUntilFinishedAsync().ConfigureAwait(false);
            return ServerAddr;
        }

        /// <inheritdoc/>
        public async Task<ResponseSecurityDetailsResult> SecurityDetailsAsync()
        {
            await WKRequest.WaitUntilFinishedAsync().ConfigureAwait(false);
            return SecurityDetails;
        }

        /// <summary>
        /// Applies raw headers from extraInfo / headersText.
        /// </summary>
        /// <param name="pairs">Parsed extra headers.</param>
        internal void ApplyExtraHeaders(IReadOnlyList<NameValueEntry> pairs)
        {
            if (pairs == null || pairs.Count == 0)
            {
                _extraHeaders.TrySetResult(false);
                return;
            }

            _headerPairs = pairs;
            _extraHeaders.TrySetResult(true);
        }

        /// <summary>
        /// Waits until extra headers arrive or the request finishes.
        /// </summary>
        /// <returns>A task that completes when headers are as complete as they will be.</returns>
        internal Task WaitForExtraHeadersAsync()
            => Task.WhenAny(_extraHeaders.Task, WKRequest.WaitUntilFinishedAsync());

        /// <summary>
        /// Fetches the response body as a string using the WebKit <c>Network.getResponseBody</c> command.
        /// Handles the <c>base64Encoded</c> flag automatically.
        /// </summary>
        /// <returns>A task that resolves to the response body as a string.</returns>
        internal async Task<string> GetBodyTextAsync()
        {
            byte[] bytes = await GetBodyBytesAsync().ConfigureAwait(false);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Fetches the response body as a byte array using the WebKit <c>Network.getResponseBody</c> command.
        /// Handles the <c>base64Encoded</c> flag automatically.
        /// </summary>
        /// <returns>A task that resolves to the response body as a byte array.</returns>
        internal Task<byte[]> PrefetchBodyAsync()
            => GetBodyBytesAsync();

        internal Task<byte[]> GetBodyBytesAsync()
        {
            if (_body != null && _body.Length > 0)
            {
                return Task.FromResult(_body);
            }

            Task<byte[]> current = _bodyTask;
            if (current != null && !current.IsCompleted)
            {
                return current;
            }

            Task<byte[]> next = LoadBodyAsync();
            _bodyTask = next;
            return next;
        }

        private async Task<byte[]> LoadBodyAsync()
        {
            await WKRequest.WaitUntilFinishedAsync().ConfigureAwait(false);
            await Task.Delay(50).ConfigureAwait(false);

            if (ResponseHeaders.IsRedirectStatus(Status))
            {
                throw new PlaywrightNativeException(ResponseHeaders.RedirectBodyUnavailable);
            }

            if (WKRequest.FulfilledBody != null && RouteFulfill.ShouldOverrideBody(Status))
            {
                return WKRequest.FulfilledBody;
            }

            if (WKRequest.Fulfilled && WKRequest.FulfilledBody != null)
            {
                return WKRequest.FulfilledBody;
            }

            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    JsonElement? result = await _session.SendAsync("Network.getResponseBody", new { requestId = _requestId }).ConfigureAwait(false);
                    byte[] bytes = ResponseContent.DecodeProtocolBody(result);
                    if (bytes.Length > 0)
                    {
                        _body = bytes;
                        return bytes;
                    }
                }
                catch (PlaywrightNativeException)
                {
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            return Array.Empty<byte>();
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<JsonElement?> IResponse.JsonAsync() => Task.FromResult<JsonElement?>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
