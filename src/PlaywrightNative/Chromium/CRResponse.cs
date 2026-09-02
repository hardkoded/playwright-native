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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Represents a network response captured via the Chrome DevTools Protocol.
    /// Links back to its originating <see cref="CRRequest"/> and provides methods
    /// for retrieving the response body.
    /// </summary>
    internal class CRResponse
    {
        private readonly CRSession _session;
        private readonly string _requestId;
        private readonly TaskCompletionSource<bool> _extraHeaders =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<IReadOnlyList<NameValueEntry>> _rawHeaders =
            new TaskCompletionSource<IReadOnlyList<NameValueEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);

        private IReadOnlyList<NameValueEntry> _headerPairs;
        private Task<byte[]> _bodyTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRResponse"/> class.
        /// </summary>
        /// <param name="session">The CDP session used to fetch the response body.</param>
        /// <param name="request">The request associated with this response.</param>
        /// <param name="url">The response URL.</param>
        /// <param name="status">The HTTP status code.</param>
        /// <param name="statusText">The HTTP status text.</param>
        /// <param name="headers">The response headers (case-insensitive keys).</param>
        /// <param name="serverAddr">Remote address, or <see langword="null"/>.</param>
        /// <param name="securityDetails">TLS details, or <see langword="null"/>.</param>
        /// <param name="fromServiceWorker">Whether a service worker served the response.</param>
        /// <param name="httpVersion">HTTP protocol version, or <see langword="null"/>.</param>
        public CRResponse(
            CRSession session,
            CRRequest request,
            string url,
            int status,
            string statusText,
            IDictionary<string, string> headers,
            ResponseServerAddrResult serverAddr = null,
            ResponseSecurityDetailsResult securityDetails = null,
            bool fromServiceWorker = false,
            string httpVersion = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _requestId = request?.ProtocolRequestId ?? request?.RequestId ?? throw new ArgumentNullException(nameof(request));
            Request = request;
            Url = url;
            Status = status;
            StatusText = statusText;
            Headers = headers;
            ServerAddr = serverAddr;
            SecurityDetails = securityDetails;
            FromServiceWorker = fromServiceWorker;
            HttpVersion = httpVersion;

            // Link the response back to the request.
            request.Response = this;
            _headerPairs = ResponseHeaders.FromMap(headers);
        }

        /// <summary>
        /// Gets the response URL.
        /// </summary>
        internal string Url { get; }

        /// <summary>
        /// Gets the HTTP status code.
        /// </summary>
        internal int Status { get; }

        /// <summary>
        /// Gets the HTTP status text.
        /// </summary>
        internal string StatusText { get; }

        /// <summary>
        /// Gets the response headers with case-insensitive key comparison.
        /// </summary>
        internal IDictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets the request associated with this response.
        /// </summary>
        internal CRRequest Request { get; }

        /// <summary>
        /// Gets the remote server address, or <see langword="null"/>.
        /// </summary>
        internal ResponseServerAddrResult ServerAddr { get; }

        /// <summary>
        /// Gets TLS details, or <see langword="null"/>.
        /// </summary>
        internal ResponseSecurityDetailsResult SecurityDetails { get; }

        /// <summary>
        /// Gets a value indicating whether a service worker served this response.
        /// </summary>
        internal bool FromServiceWorker { get; }

        /// <summary>
        /// Gets the HTTP protocol version, or <see langword="null"/>.
        /// </summary>
        internal string HttpVersion { get; }

        /// <summary>
        /// Gets a value indicating whether the response status code is in the 200-299 range.
        /// </summary>
        internal bool Ok => ResponseHeaders.IsOkStatus(Status);

        /// <summary>
        /// Gets header name/value pairs, including duplicates when extraInfo arrived.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, string>> HeaderPairs
            => ResponseHeaders.ToPairs(_headerPairs) ?? Headers;

        /// <summary>
        /// Applies raw headers from <c>Network.responseReceivedExtraInfo</c>.
        /// </summary>
        /// <param name="pairs">Parsed extraInfo headers.</param>
        internal void ApplyExtraHeaders(IReadOnlyList<NameValueEntry> pairs)
        {
            if (pairs == null || pairs.Count == 0)
            {
                _extraHeaders.TrySetResult(false);
                SetRawResponseHeaders(null);
                return;
            }

            _headerPairs = pairs;
            _extraHeaders.TrySetResult(true);
            SetRawResponseHeaders(pairs);
        }

        /// <summary>
        /// Waits until extraInfo arrives or the request finishes.
        /// </summary>
        /// <returns>A task that completes when headers are as complete as they will be.</returns>
        internal Task WaitForExtraHeadersAsync()
            => Task.WhenAny(_extraHeaders.Task, Request.WaitUntilFinishedAsync());

        /// <summary>
        /// Fetches the response body as a string using the CDP <c>Network.getResponseBody</c> command.
        /// Handles the <c>base64Encoded</c> flag automatically.
        /// </summary>
        /// <returns>A task that resolves to the response body as a string.</returns>
        internal async Task<string> GetBodyAsync()
        {
            byte[] bytes = await GetBodyBytesAsync().ConfigureAwait(false);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Fetches the response body as a byte array using the CDP <c>Network.getResponseBody</c> command.
        /// Handles the <c>base64Encoded</c> flag automatically.
        /// </summary>
        /// <returns>A task that resolves to the response body as a byte array.</returns>
        internal Task<byte[]> PrefetchBodyAsync()
        {
            _bodyTask ??= LoadBodyAsync();
            return _bodyTask;
        }

        internal Task<byte[]> GetBodyBytesAsync()
            => PrefetchBodyAsync();

        /// <summary>
        /// Stores extra-info response headers (or provisional headers when
        /// <paramref name="headers"/> is <see langword="null"/>).
        /// </summary>
        /// <param name="headers">Raw headers, or <see langword="null"/> for provisional.</param>
        internal void SetRawResponseHeaders(IReadOnlyList<NameValueEntry> headers)
        {
            _rawHeaders.TrySetResult(headers ?? HeaderMap.Array(Headers));
        }

        /// <summary>
        /// Completes raw-header waiters with provisional headers when extra info
        /// never arrived.
        /// </summary>
        internal void EnsureRawResponseHeaders()
        {
            if (!_rawHeaders.Task.IsCompleted)
            {
                SetRawResponseHeaders(null);
            }
        }

        /// <summary>
        /// Waits until extra-info (or provisional) response headers are available.
        /// </summary>
        /// <returns>The raw header list.</returns>
        internal Task<IReadOnlyList<NameValueEntry>> WaitForRawHeadersAsync() => _rawHeaders.Task;

        private async Task<byte[]> LoadBodyAsync()
        {
            await Request.WaitUntilFinishedAsync().ConfigureAwait(false);

            if (ResponseHeaders.IsRedirectStatus(Status))
            {
                throw new PlaywrightNativeException(ResponseHeaders.RedirectBodyUnavailable);
            }

            if (Request.FulfilledBody != null && RouteFulfill.ShouldOverrideBody(Status))
            {
                return Request.FulfilledBody;
            }

            try
            {
                byte[] fromProtocol = await TryGetResponseBodyAsync().ConfigureAwait(false);
                if (fromProtocol.Length > 0)
                {
                    return fromProtocol;
                }

                if (Request.Fulfilled)
                {
                    return Request.FulfilledBody ?? Array.Empty<byte>();
                }

                if (Request.HasNavigatedAway())
                {
                    throw new PlaywrightNativeException(ResponseHeaders.NavigatedAway);
                }

                if (!CanRefetchBody())
                {
                    return fromProtocol;
                }

                return await TryLoadNetworkResourceAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                if (Request.HasNavigatedAway())
                {
                    throw new PlaywrightNativeException(ResponseHeaders.NavigatedAway);
                }

                throw;
            }
        }

        private async Task<byte[]> TryGetResponseBodyAsync()
        {
            try
            {
                if (Request.FetchProtocolBody != null)
                {
                    return await Request.FetchProtocolBody().ConfigureAwait(false);
                }

                CRSession session = Request.NetworkSession ?? _session;
                string requestId = Request.ProtocolRequestId ?? _requestId;
                JsonElement? result = await session.SendAsync("Network.getResponseBody", new { requestId }).ConfigureAwait(false);
                return ResponseContent.DecodeProtocolBody(result);
            }
            catch (PlaywrightNativeException)
            {
                return Array.Empty<byte>();
            }
        }

        private bool CanRefetchBody()
        {
            if (!string.Equals(Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (Request.Frame == null || string.IsNullOrEmpty(Request.Frame.FrameId))
            {
                return false;
            }

            return Headers == null
                || !Headers.TryGetValue("Content-Length", out string contentLength)
                || !string.Equals(contentLength, "0", StringComparison.Ordinal);
        }

        private async Task<byte[]> TryLoadNetworkResourceAsync()
        {
            try
            {
                JsonElement? result = await _session.SendAsync(
                    "Network.loadNetworkResource",
                    new
                    {
                        url = Request.Url,
                        frameId = Request.Frame.FrameId,
                        options = new { disableCache = false, includeCredentials = true },
                    }).ConfigureAwait(false);
                if (!result.HasValue
                    || !result.Value.TryGetProperty("resource", out JsonElement resource)
                    || !resource.TryGetProperty("stream", out JsonElement streamElement))
                {
                    return Array.Empty<byte>();
                }

                string handle = streamElement.GetString();
                if (string.IsNullOrEmpty(handle))
                {
                    return Array.Empty<byte>();
                }

                List<byte> chunks = new();
                while (true)
                {
                    JsonElement? chunk = await _session.SendAsync("IO.read", new { handle }).ConfigureAwait(false);
                    if (!chunk.HasValue)
                    {
                        break;
                    }

                    JsonElement chunkValue = chunk.Value;
                    string data = chunkValue.TryGetProperty("data", out JsonElement dataElement)
                        ? dataElement.GetString() ?? string.Empty
                        : string.Empty;
                    bool base64Encoded = chunkValue.TryGetProperty("base64Encoded", out JsonElement encodedElement)
                        && encodedElement.GetBoolean();
                    byte[] bytes = base64Encoded ? Convert.FromBase64String(data) : Encoding.UTF8.GetBytes(data);
                    if (bytes.Length > 0)
                    {
                        chunks.AddRange(bytes);
                    }

                    if (chunkValue.TryGetProperty("eof", out JsonElement eofElement) && eofElement.GetBoolean())
                    {
                        try
                        {
                            await _session.SendAsync("IO.close", new { handle }).ConfigureAwait(false);
                        }
                        catch (PlaywrightNativeException)
                        {
                        }

                        break;
                    }
                }

                return chunks.ToArray();
            }
            catch (PlaywrightNativeException)
            {
                return Array.Empty<byte>();
            }
        }
    }
}
