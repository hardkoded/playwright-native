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
            return EquatableHeader.FromEntries(HeaderMap.Array(Headers));
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
            => PrefetchBodyAsync(forceRetry: false);

        /// <summary>
        /// Fetches the response body, optionally discarding a prior empty result so
        /// HAR flush can retry after <c>loadingFinished</c> under load.
        /// </summary>
        /// <param name="forceRetry">When <see langword="true"/>, retry after an empty read.</param>
        /// <returns>A task that resolves to the response body as a byte array.</returns>
        internal Task<byte[]> PrefetchBodyAsync(bool forceRetry)
        {
            if (forceRetry && (_body == null || _body.Length == 0))
            {
                Task<byte[]> current = _bodyTask;
                if (current == null || current.IsCompleted)
                {
                    _bodyTask = null;
                }
            }

            return GetBodyBytesAsync();
        }

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

            // A prior empty completion must not block later retries after the
            // inspector buffer is populated by loadingFinished.
            Task<byte[]> next = LoadBodyAsync();
            _bodyTask = next;
            return next;
        }

        private async Task<byte[]> LoadBodyAsync()
        {
            await WKRequest.WaitUntilFinishedAsync().ConfigureAwait(false);

            if (ResponseHeaders.IsRedirectStatus(Status))
            {
                throw new PlaywrightNativeException(ResponseHeaders.RedirectBodyUnavailable);
            }

            if (WKRequest.FulfilledBody != null && RouteFulfill.ShouldOverrideBody(Status))
            {
                _body = WKRequest.FulfilledBody;
                return WKRequest.FulfilledBody;
            }

            if (WKRequest.Fulfilled && WKRequest.FulfilledBody != null)
            {
                _body = WKRequest.FulfilledBody;
                return WKRequest.FulfilledBody;
            }

            // Prefer an immediate read: Ubuntu WebKit clears the inspector
            // buffer quickly after loadingFinished (especially under CI load).
            for (int attempt = 0; attempt < 30; attempt++)
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

                await Task.Delay(attempt < 5 ? 20 : 40).ConfigureAwait(false);
            }

            return Array.Empty<byte>();
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<JsonElement?> IResponse.JsonAsync() => Task.FromResult<JsonElement?>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
