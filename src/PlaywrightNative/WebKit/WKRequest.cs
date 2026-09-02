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
    /// Represents a network request captured via the WebKit Inspector Protocol
    /// (<c>Network.requestWillBeSent</c>). Holds the request metadata and links to the
    /// corresponding response and redirect chain. Mirrors <c>CRRequest</c>.
    /// Implements <see cref="IRequest"/> directly.
    /// </summary>
    internal partial class WKRequest : IRequest, IHasOwningPage
    {
        private readonly TaskCompletionSource<string> _finished =
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<IReadOnlyList<NameValueEntry>> _rawHeaders =
            new TaskCompletionSource<IReadOnlyList<NameValueEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<IResponse> _responseReady =
            new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly string _resourceType;
        private readonly IFrame _frame;
        private byte[] _postDataBuffer;
        private WKRequest _redirectedTo;
        private WKResponse _wkResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKRequest"/> class.
        /// </summary>
        /// <param name="requestId">The WebKit request identifier.</param>
        /// <param name="url">The request URL.</param>
        /// <param name="method">The HTTP method (e.g. GET, POST).</param>
        /// <param name="headers">The request headers.</param>
        /// <param name="postData">The POST data, if any.</param>
        /// <param name="resourceType">The resource type (e.g. Document, Script).</param>
        /// <param name="isNavigationRequest">Whether this is a navigation request.</param>
        /// <param name="redirectedFrom">The request that was redirected to this request, if any.</param>
        /// <param name="frame">The frame that issued the request.</param>
        /// <param name="postDataBuffer">Raw POST body bytes, if already decoded.</param>
        /// <param name="frameUnavailable">
        /// When <see langword="true"/>, <see cref="IRequest.Frame"/> throws because
        /// the request was issued before the frame/page existed (popup main request).
        /// </param>
        public WKRequest(
            string requestId,
            string url,
            string method,
            IDictionary<string, string> headers,
            string postData,
            string resourceType,
            bool isNavigationRequest,
            WKRequest redirectedFrom,
            IFrame frame = null,
            byte[] postDataBuffer = null,
            bool frameUnavailable = false)
        {
            RequestId = requestId;
            Url = NavigationTimeout.WithoutHash(url);
            Method = method;
            Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            _postDataBuffer = postDataBuffer ?? RequestPostData.FromWebKitBase64(postData);
            PostData = RequestPostData.ToUtf8String(_postDataBuffer) ?? postData;
            _resourceType = resourceType;
            IsNavigationRequest = isNavigationRequest;
            WKRedirectedFrom = redirectedFrom;
            _frame = frame;
            FrameUnavailable = frameUnavailable;

            if (redirectedFrom != null)
            {
                redirectedFrom._redirectedTo = this;
            }

            Timing = ResourceTimingParser.Create();
        }

        /// <inheritdoc/>
        public string Url { get; private set; }

        /// <inheritdoc/>
        public string Method { get; private set; }

        /// <inheritdoc/>
        public Dictionary<string, string> Headers { get; private set; }

        /// <inheritdoc/>
        public string PostData { get; private set; }

        /// <inheritdoc/>
        public string ResourceType => NetworkRequestEvents.NormalizeResourceType(_resourceType);

        /// <inheritdoc/>
        public bool IsNavigationRequest { get; }

        /// <inheritdoc/>
        public string Failure => FailureText;

        /// <inheritdoc/>
        public IFrame Frame
        {
            get
            {
                if (FrameUnavailable)
                {
                    throw new PlaywrightNativeException(
                        "Frame for this navigation request is not available, because the request\nwas issued before the frame is created. You can check whether the request\nis a navigation request by calling isNavigationRequest() method.");
                }

                if (_frame != null && _frame.Page == null)
                {
                    throw new PlaywrightNativeException(
                        "Frame for this navigation request is not available, because the request\nwas issued before the frame is created. You can check whether the request\nis a navigation request by calling isNavigationRequest() method.");
                }

                return _frame;
            }
        }

        /// <inheritdoc/>
        public IPage OwningPage => _frame?.Page;

        /// <inheritdoc/>
        public byte[] PostDataBuffer => _postDataBuffer;

        /// <inheritdoc/>
        public IRequest RedirectedFrom => WKRedirectedFrom;

        /// <inheritdoc/>
        public IRequest RedirectedTo => WKRedirectedTo;

        /// <inheritdoc/>
        public RequestTimingResult Timing { get; }

        /// <inheritdoc/>
        public IResponse ExistingResponse => Response;

        /// <summary>
        /// Cookie-stripped headers last passed to <c>route.continue</c>, replayed
        /// on WebKit redirects.
        /// </summary>
        internal IDictionary<string, string> ContinuedHeaders { get; private set; }

        /// <summary>
        /// URL last passed to <c>route.continue</c> / <c>route.fallback</c>.
        /// </summary>
        internal string ContinuedUrl { get; private set; }

        /// <summary>
        /// Method last passed to <c>route.continue</c> / <c>route.fallback</c>.
        /// </summary>
        internal string ContinuedMethod { get; private set; }

        /// <summary>
        /// Post-data last passed to <c>route.continue</c> / <c>route.fallback</c>.
        /// </summary>
        internal byte[] ContinuedPostData { get; private set; }

        /// <summary>
        /// Whether <c>route.fulfill</c> handled this request.
        /// </summary>
        internal bool Fulfilled { get; private set; }

        /// <summary>
        /// Body stored by <c>route.fulfill</c> for statuses where the protocol
        /// body is unavailable (1xx / 4xx / 5xx).
        /// </summary>
        internal byte[] FulfilledBody { get; private set; }

        /// <summary>
        /// Gets the WebKit request identifier.
        /// </summary>
        internal string RequestId { get; }

        /// <summary>
        /// Gets or sets the protocol <c>timing.requestTime</c> baseline (seconds).
        /// </summary>
        internal double TimingRequestTime { get; set; }

        /// <summary>
        /// Gets or sets the protocol monotonic <c>timestamp</c> from
        /// <c>Network.requestWillBeSent</c> (seconds).
        /// </summary>
        internal double TimestampSeconds { get; set; }

        /// <summary>
        /// Gets or sets the response associated with this request.
        /// Set when the corresponding <c>Network.responseReceived</c> event arrives.
        /// </summary>
        internal WKResponse Response
        {
            get => _wkResponse;
            set
            {
                _wkResponse = value;
                if (value != null)
                {
                    _responseReady.TrySetResult(value);
                    if (FrameUnavailable)
                    {
                        MarkFinished();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the WebKit request that was redirected to this request, if any.
        /// </summary>
        internal WKRequest WKRedirectedFrom { get; }

        /// <summary>
        /// Gets the WebKit request that this request was redirected to, if any.
        /// </summary>
        internal WKRequest WKRedirectedTo => _redirectedTo;

        /// <summary>
        /// Gets or sets the failure text when the request fails.
        /// Set when the corresponding <c>Network.loadingFailed</c> event arrives.
        /// </summary>
        internal string FailureText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <c>Network.loadingFinished</c> arrived.
        /// </summary>
        internal bool Finished { get; set; }

        /// <summary>
        /// Gets or sets <c>encodedDataLength</c> from <c>Network.loadingFinished</c>.
        /// </summary>
        internal int EncodedDataLength { get; set; }

        /// <summary>
        /// Gets or sets the frame URL when this request was issued.
        /// </summary>
        internal string DocumentUrl { get; set; }

        /// <summary>
        /// Gets a value indicating whether <see cref="IRequest.Frame"/> must throw
        /// because this request was issued before the owning page existed.
        /// </summary>
        internal bool FrameUnavailable { get; set; }

        /// <summary>
        /// Gets the originating frame without the popup <see cref="IRequest.Frame"/> throw.
        /// </summary>
        internal IFrame InternalFrame => _frame;

        /// <summary>
        /// When <see langword="true"/>, page-level request/response events are omitted
        /// because this is a process-swap duplicate of an already-emitted navigation.
        /// </summary>
        internal bool SuppressPageEvents { get; set; }

        /// <inheritdoc/>
        public Task<IResponse> GetResponseAsync() => _responseReady.Task;

        /// <inheritdoc/>
        public Task<IResponse> ResponseAsync() => GetResponseAsync();

        /// <inheritdoc/>
        public async Task<RequestSizesResult> GetSizesAsync()
        {
            await WaitUntilFinishedAsync().ConfigureAwait(false);
            if (Response == null)
            {
                throw new PlaywrightNativeException("Unable to fetch sizes for failed request");
            }

            return RequestSizesCalculator.Compute(
                Method,
                Url,
                Headers,
                PostData,
                Response.Status,
                Response.StatusText,
                Response.Headers,
                EncodedDataLength);
        }

        /// <inheritdoc/>
        public Task<RequestSizesResult> SizesAsync() => GetSizesAsync();

        /// <inheritdoc/>
        public JsonDocument GetPayloadAsJson(JsonDocumentOptions documentOptions = default)
            => RequestPostData.ParseJson(PostData, Headers, documentOptions);

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> AllHeadersAsync()
        {
            if (ContinuedHeaders != null)
            {
                return HeaderMap.All(Headers);
            }

            IReadOnlyList<NameValueEntry> raw = await WaitForRawHeadersAsync().ConfigureAwait(false);
            await _responseReady.Task.ConfigureAwait(false);
            Dictionary<string, string> map = RawNetworkHeaders.AllJoined(raw);
            foreach (KeyValuePair<string, string> header in Headers)
            {
                if (string.IsNullOrEmpty(header.Key) || string.IsNullOrEmpty(header.Value))
                {
                    continue;
                }

#pragma warning disable CA1308
                string key = header.Key.ToLowerInvariant();
#pragma warning restore CA1308
                if (!map.ContainsKey(key))
                {
                    map[key] = header.Value;
                }
            }

            if (!map.ContainsKey("cookie"))
            {
                string cookie = HeaderMap.Value(Headers, "cookie")
                    ?? await CookieHeaderFromContextAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(cookie))
                {
                    map["cookie"] = cookie;
                }
            }

            return map;
        }

        /// <inheritdoc/>
        public async Task<string> HeaderValueAsync(string name)
        {
            if (ContinuedHeaders != null)
            {
                return HeaderMap.Value(Headers, name);
            }

            IReadOnlyList<NameValueEntry> raw = await WaitForRawHeadersAsync().ConfigureAwait(false);
            return RawNetworkHeaders.JoinedValue(raw, name);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Header>> HeadersArrayAsync()
        {
            IReadOnlyList<NameValueEntry> raw;
            if (ContinuedHeaders != null)
            {
                raw = HeaderMap.Array(ContinuedHeaders);
            }
            else
            {
                raw = await WaitForRawHeadersAsync().ConfigureAwait(false);
            }

            return raw.Select(e => new Header { Name = e.Name, Value = e.Value }).ToList();
        }

        /// <summary>
        /// Returns whether the owning frame navigated away after this request.
        /// </summary>
        /// <returns><see langword="true"/> when the body is no longer available because of navigation.</returns>
        internal bool HasNavigatedAway()
        {
            IFrame frame = InternalFrame;
            if (frame == null || string.IsNullOrEmpty(DocumentUrl))
            {
                return false;
            }

            return !string.Equals(DocumentUrl, frame.Url, StringComparison.Ordinal);
        }

        /// <summary>
        /// Completes <see cref="WaitUntilFinishedAsync"/> after loading finished or failed.
        /// </summary>
        internal void MarkFinished()
        {
            EnsureRawRequestHeaders();
            _responseReady.TrySetResult(Response);
            _finished.TrySetResult(FailureText);
        }

        /// <summary>
        /// Rejects waiters when the owning page closes.
        /// </summary>
        /// <param name="error">The target-closed error.</param>
        internal void AbortClosed(Exception error)
        {
            _responseReady.TrySetException(error);
            _finished.TrySetException(error);
            _rawHeaders.TrySetException(error);
        }

        /// <summary>
        /// Marks this request as the popup main navigation and completes
        /// <see cref="GetResponseAsync"/> / finished waiters.
        /// </summary>
        internal void CompleteAsPopupNavigation()
        {
            FrameUnavailable = true;
            EnsureRawRequestHeaders();
            _responseReady.TrySetResult(new PopupMainResponse(this));
            _finished.TrySetResult(null);
        }

        /// <summary>
        /// Stores extra-info / response-provided request headers (or provisional
        /// headers when <paramref name="headers"/> is <see langword="null"/>).
        /// </summary>
        /// <param name="headers">Raw headers, or <see langword="null"/> for provisional.</param>
        internal void SetRawRequestHeaders(IReadOnlyList<NameValueEntry> headers)
        {
            _rawHeaders.TrySetResult(headers ?? HeaderMap.Array(Headers));
        }

        /// <summary>
        /// Applies a <see cref="IRoute.FallbackAsync"/> URL override used for
        /// subsequent handler matching and intercept-continue.
        /// </summary>
        /// <param name="url">The replacement URL.</param>
        internal void ApplyFallbackUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Url = url;
                ContinuedUrl = url;
            }
        }

        /// <summary>
        /// Records a <c>route.fulfill</c> so <see cref="WKResponse"/> can return
        /// the mocked body without hitting the network.
        /// </summary>
        /// <param name="body">Fulfilled body, or <see langword="null"/>.</param>
        internal void ApplyFulfill(byte[] body)
        {
            Fulfilled = true;
            FulfilledBody = body ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Applies official <c>route.continue</c> overrides to the public request
        /// surface and stores cookie-stripped headers for redirect replay.
        /// </summary>
        /// <param name="url">Optional URL override.</param>
        /// <param name="method">Optional method override.</param>
        /// <param name="headers">Optional header override.</param>
        /// <param name="postData">Optional body override, including empty.</param>
        internal void ApplyContinueOverrides(
            string url,
            string method,
            IDictionary<string, string> headers,
            byte[] postData)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Url = url;
                ContinuedUrl = url;
            }

            if (!string.IsNullOrEmpty(method))
            {
                Method = method;
                ContinuedMethod = method;
            }

            if (headers != null)
            {
                Dictionary<string, string> merged = RouteContinue.ApplyHeadersOverrides(Headers, headers);
                Headers = merged;
                ContinuedHeaders = RouteContinue.RemoveCookie(merged);
            }

            if (postData != null)
            {
                _postDataBuffer = postData;
                PostData = RequestPostData.ToUtf8String(postData);
                ContinuedPostData = postData;
            }
        }

        /// <summary>
        /// Completes raw-header waiters with provisional headers when extra info
        /// never arrived.
        /// </summary>
        internal void EnsureRawRequestHeaders()
        {
            if (!_rawHeaders.Task.IsCompleted)
            {
                SetRawRequestHeaders(null);
            }
        }

        /// <summary>
        /// Waits until extra-info (or provisional) request headers are available.
        /// </summary>
        /// <returns>The raw header list.</returns>
        internal Task<IReadOnlyList<NameValueEntry>> WaitForRawHeadersAsync() => _rawHeaders.Task;

        /// <summary>
        /// Waits until this request has finished or failed.
        /// </summary>
        /// <returns>The failure text, or <see langword="null"/> on success.</returns>
        internal Task<string> WaitUntilFinishedAsync() => _finished.Task;

        private async Task<string> CookieHeaderFromContextAsync()
        {
            IBrowserContext context = InternalFrame?.Page?.Context;
            if (context == null || string.IsNullOrEmpty(Url))
            {
                return null;
            }

            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await context.GetCookiesAsync(new[] { Url }).ConfigureAwait(false);
            if (cookies == null || cookies.Count == 0)
            {
                return null;
            }

            StringBuilder builder = new();
            for (int i = 0; i < cookies.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(cookies[i].Name).Append('=').Append(cookies[i].Value);
            }

            return builder.ToString();
        }

        private sealed class PopupMainResponse : IResponse
        {
            private readonly WKRequest _request;

            internal PopupMainResponse(WKRequest request)
            {
                _request = request;
            }

            public IFrame Frame => null;

            public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public bool Ok => true;

            public IRequest Request => _request;

            public int Status => 200;

            public string StatusText => "OK";

            public string Url => _request.Url;

            public HttpStatusCode StatusCode => HttpStatusCode.OK;

            public bool FromServiceWorker => false;

            public Task<byte[]> BodyAsync() => Task.FromResult(Array.Empty<byte>());

            public Task<string> FinishedAsync() => Task.FromResult<string>(null);

            public Task<T> JsonAsync<T>() => throw new PlaywrightNativeException("Response has no body.");

            public Task<JsonElement?> JsonAsync() => throw new PlaywrightNativeException("Response has no body.");

            public Task<string> TextAsync() => Task.FromResult(string.Empty);

            public Task<Dictionary<string, string>> AllHeadersAsync() => Task.FromResult(Headers);

            public Task<IReadOnlyList<Header>> HeadersArrayAsync() => Task.FromResult<IReadOnlyList<Header>>(Array.Empty<Header>());

            public Task<string> HeaderValueAsync(string name) => Task.FromResult<string>(null);

            public Task<IReadOnlyList<string>> HeaderValuesAsync(string name) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            public Task<string> HttpVersionAsync() => Task.FromResult("HTTP/1.1");

            public Task<ResponseSecurityDetailsResult> SecurityDetailsAsync() => Task.FromResult<ResponseSecurityDetailsResult>(null);

            public Task<ResponseServerAddrResult> ServerAddrAsync() => Task.FromResult<ResponseServerAddrResult>(null);
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        JsonElement? IRequest.PostDataJSON() => default!;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
