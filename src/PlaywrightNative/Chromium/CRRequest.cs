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
using System.Text;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Represents a network request captured via the Chrome DevTools Protocol.
    /// Holds the request metadata and links to the corresponding response and redirect chain.
    /// </summary>
    internal class CRRequest
    {
        private readonly TaskCompletionSource<string> _finished =
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<IReadOnlyList<NameValueEntry>> _rawHeaders =
            new TaskCompletionSource<IReadOnlyList<NameValueEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<CRResponse> _responseReady =
            new TaskCompletionSource<CRResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        private CRRequest _redirectedTo;
        private CRResponse _response;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRequest"/> class.
        /// </summary>
        /// <param name="requestId">The CDP request identifier.</param>
        /// <param name="url">The request URL.</param>
        /// <param name="method">The HTTP method (e.g. GET, POST).</param>
        /// <param name="headers">The request headers.</param>
        /// <param name="postData">The POST data, if any.</param>
        /// <param name="resourceType">The resource type (e.g. Document, Script).</param>
        /// <param name="isNavigationRequest">Whether this is a navigation request.</param>
        /// <param name="frame">The frame that initiated this request.</param>
        /// <param name="redirectedFrom">The request that was redirected to this request, if any.</param>
        /// <param name="postDataBuffer">Raw POST body bytes, if already decoded.</param>
        /// <param name="frameUnavailable">
        /// When <see langword="true"/>, <see cref="IRequest.Frame"/> throws because
        /// the request was issued before the frame/page existed (popup main request).
        /// </param>
        public CRRequest(
            string requestId,
            string url,
            string method,
            IDictionary<string, string> headers,
            string postData,
            string resourceType,
            bool isNavigationRequest,
            Frame frame,
            CRRequest redirectedFrom,
            byte[] postDataBuffer = null,
            bool frameUnavailable = false)
        {
            RequestId = requestId;
            Url = url;
            Method = method;
            Headers = headers;
            PostDataBuffer = postDataBuffer ?? (postData == null ? null : Encoding.UTF8.GetBytes(postData));
            PostData = postData ?? RequestPostData.ToUtf8String(PostDataBuffer);
            ResourceType = resourceType;
            IsNavigationRequest = isNavigationRequest;
            Frame = frame;
            RedirectedFrom = redirectedFrom;
            FrameUnavailable = frameUnavailable;
            IsFavicon = url != null && url.Contains("/favicon.ico");

            if (redirectedFrom != null)
            {
                redirectedFrom._redirectedTo = this;
            }

            Timing = ResourceTimingParser.Create();
        }

        /// <summary>
        /// Gets the CDP request identifier.
        /// </summary>
        internal string RequestId { get; }

        /// <summary>
        /// Raw CDP <c>requestId</c> without a session prefix. Used for
        /// <c>Network.getResponseBody</c> on OOPIF sessions.
        /// </summary>
        internal string ProtocolRequestId { get; set; }

        /// <summary>
        /// CDP session that currently owns this request. Official OOPIF document
        /// requests start on the parent session and finish on the child session.
        /// </summary>
        internal CRSession NetworkSession { get; set; }

        /// <summary>
        /// Fetches the protocol body from the owning network manager, trying
        /// the OOPIF session when the request started in the parent.
        /// </summary>
        internal Func<Task<byte[]>> FetchProtocolBody { get; set; }

        /// <summary>
        /// Chromium concatenates extra <c>referer</c> with the navigate referrer.
        /// When set, <see cref="ChromiumRequest"/> exposes this value.
        /// </summary>
        internal string ChromiumRefererOverride { get; set; }

        /// <summary>
        /// Gets the request URL.
        /// </summary>
        internal string Url { get; private set; }

        /// <summary>
        /// Gets the HTTP method (e.g. GET, POST).
        /// </summary>
        internal string Method { get; private set; }

        /// <summary>
        /// Cookie-stripped headers last passed to <c>route.continue</c>, replayed
        /// on Chromium redirects.
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
        /// Whether <c>route.fulfill</c> handled this request. Fulfilled bodies
        /// must not be re-fetched from the network.
        /// </summary>
        internal bool Fulfilled { get; private set; }

        /// <summary>
        /// Body stored by <c>route.fulfill</c> for statuses where
        /// <c>Network.getResponseBody</c> is empty (1xx / 4xx / 5xx).
        /// </summary>
        internal byte[] FulfilledBody { get; private set; }

        /// <summary>
        /// Status used by <c>route.fulfill</c>.
        /// </summary>
        internal int FulfilledStatus { get; private set; } = 200;

        /// <summary>
        /// Headers used by <c>route.fulfill</c>.
        /// </summary>
        internal IDictionary<string, string> FulfilledHeaders { get; private set; }

        /// <summary>
        /// Gets the request headers.
        /// </summary>
        internal IDictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets the POST data, if any.
        /// </summary>
        internal string PostData { get; private set; }

        /// <summary>
        /// Gets the raw POST body, if any.
        /// </summary>
        internal byte[] PostDataBuffer { get; private set; }

        /// <summary>
        /// Gets a value indicating whether <see cref="IRequest.Frame"/> must throw
        /// because this request was issued before the owning page existed.
        /// </summary>
        internal bool FrameUnavailable { get; set; }

        /// <summary>
        /// Gets the resource type (e.g. Document, Script, Stylesheet).
        /// </summary>
        internal string ResourceType { get; }

        /// <summary>
        /// Gets a value indicating whether this is a navigation request.
        /// </summary>
        internal bool IsNavigationRequest { get; }

        /// <summary>
        /// Gets the frame that initiated this request.
        /// </summary>
        internal Frame Frame { get; }

        /// <summary>
        /// Gets or sets the service worker that issued this request, if any.
        /// </summary>
        internal IWorker ServiceWorker { get; set; }

        /// <summary>
        /// Gets or sets the response associated with this request.
        /// Set when the corresponding <c>Network.responseReceived</c> event arrives.
        /// </summary>
        internal CRResponse Response
        {
            get => _response;
            set
            {
                _response = value;
                if (value != null)
                {
                    _responseReady.TrySetResult(value);
                }
            }
        }

        /// <summary>
        /// Gets the request that was redirected to this request, if any.
        /// </summary>
        internal CRRequest RedirectedFrom { get; }

        /// <summary>
        /// Gets the request that this request was redirected to, if any.
        /// </summary>
        internal CRRequest RedirectedTo => _redirectedTo;

        /// <summary>
        /// Gets or sets the failure text when the request fails.
        /// Set when the corresponding <c>Network.loadingFailed</c> event arrives.
        /// </summary>
        internal string FailureText { get; set; }

        /// <summary>
        /// Gets a value indicating whether this request targets a favicon resource.
        /// </summary>
        internal bool IsFavicon { get; }

        /// <summary>
        /// Gets the Playwright resource timing for this request. Updated as CDP
        /// network events arrive.
        /// </summary>
        internal RequestTimingResult Timing { get; }

        /// <summary>
        /// Gets or sets the CDP <c>timing.requestTime</c> baseline (seconds) used to
        /// compute <see cref="RequestTimingResult.ResponseEnd"/>.
        /// </summary>
        internal double TimingRequestTime { get; set; }

        /// <summary>
        /// Gets or sets the protocol monotonic <c>timestamp</c> from
        /// <c>Network.requestWillBeSent</c> (seconds).
        /// </summary>
        internal double TimestampSeconds { get; set; }

        /// <summary>
        /// Official <c>Network.requestServedFromCache</c> / memory-cache.
        /// </summary>
        internal bool ServedFromCache { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <c>Network.loadingFinished</c> arrived.
        /// </summary>
        internal bool Finished { get; set; }

        /// <summary>
        /// Gets or sets <c>encodedDataLength</c> from <c>Network.loadingFinished</c>.
        /// </summary>
        internal int EncodedDataLength { get; set; }

        /// <summary>
        /// Gets or sets the frame document id when this request was issued.
        /// </summary>
        internal string DocumentId { get; set; }

        /// <summary>
        /// Gets or sets the frame URL when this request was issued.
        /// </summary>
        internal string DocumentUrl { get; set; }

        /// <summary>
        /// Returns whether the owning frame committed a different document after this request.
        /// </summary>
        /// <returns><see langword="true"/> when the body is no longer available because of navigation.</returns>
        internal bool HasNavigatedAway()
        {
            if (Frame == null)
            {
                return false;
            }

            if (IsNavigationRequest
                && !string.IsNullOrEmpty(Url)
                && string.Equals(Url, Frame.Url, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(DocumentId) && !string.IsNullOrEmpty(Frame.DocumentId))
            {
                return !string.Equals(DocumentId, Frame.DocumentId, StringComparison.Ordinal);
            }

            return !string.IsNullOrEmpty(DocumentUrl)
                && !string.Equals(DocumentUrl, Frame.Url, StringComparison.Ordinal);
        }

        /// <summary>
        /// Completes <see cref="WaitUntilFinishedAsync"/> after loading finished or failed.
        /// </summary>
        internal void MarkFinished()
        {
            _responseReady.TrySetResult(_response);
            _finished.TrySetResult(FailureText);
        }

        /// <summary>
        /// Rejects waiters when the owning page closes (official
        /// <c>interrupt request.response() / response.finished()</c>).
        /// </summary>
        /// <param name="error">The target-closed error.</param>
        internal void AbortClosed(Exception error)
        {
            _responseReady.TrySetException(error);
            _finished.TrySetException(error);
            _rawHeaders.TrySetException(error);
        }

        /// <summary>
        /// Waits until a response is associated or the request finishes without one.
        /// </summary>
        /// <returns>The response, or <see langword="null"/>.</returns>
        internal Task<CRResponse> WaitForResponseAsync() => _responseReady.Task;

        /// <summary>
        /// Stores extra-info request headers (or falls back to provisional headers
        /// when <paramref name="headers"/> is <see langword="null"/>).
        /// </summary>
        /// <param name="headers">Raw headers, or <see langword="null"/> for provisional.</param>
        /// <param name="completeWithoutCookie">
        /// When <see langword="false"/>, skip completing the waiter if the list
        /// has no <c>Cookie</c> so extra-info can still supply the jar value.
        /// </param>
        internal void SetRawRequestHeaders(IReadOnlyList<NameValueEntry> headers, bool completeWithoutCookie = true)
        {
            IReadOnlyList<NameValueEntry> resolved = MergeCookieIntoRaw(headers ?? HeaderMap.Array(Headers));
            if (!completeWithoutCookie && !HasCookie(resolved) && !_rawHeaders.Task.IsCompleted)
            {
                return;
            }

            string cookie = RawNetworkHeaders.JoinedValue(resolved, "cookie");
            if (!string.IsNullOrEmpty(cookie))
            {
                HeaderMap.Set(Headers, "cookie", cookie);
            }

            _rawHeaders.TrySetResult(resolved);
        }

        /// <summary>
        /// Copies intercepted Fetch headers onto the public header map so
        /// <c>request.headers()</c> matches official Playwright.
        /// </summary>
        /// <param name="headers">Headers from <c>Fetch.requestPaused</c>.</param>
        /// <param name="extraHeaders">Page extra HTTP headers, or <see langword="null"/>.</param>
        internal void ApplyInterceptedHeaders(
            IEnumerable<KeyValuePair<string, string>> headers,
            IReadOnlyDictionary<string, string> extraHeaders)
        {
            if (headers != null)
            {
                List<KeyValuePair<string, string>> snapshot = new(headers);
                foreach (KeyValuePair<string, string> header in snapshot)
                {
                    if (!string.IsNullOrEmpty(header.Key))
                    {
                        HeaderMap.Set(Headers, header.Key, header.Value);
                    }
                }
            }

            string referer = HeaderMap.Value(Headers, "referer");
            string concatenated = NavigationTimeout.ConcatenateChromiumReferer(referer, extraHeaders);
            if (!string.IsNullOrEmpty(concatenated)
                && !string.Equals(concatenated, referer, StringComparison.Ordinal))
            {
                HeaderMap.Set(Headers, "referer", concatenated);
            }
        }

        /// <summary>
        /// Applies a <see cref="IRoute.FallbackAsync"/> URL override used for
        /// subsequent handler matching and <c>Fetch.continueRequest</c>.
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
        /// Records a <c>route.fulfill</c> so <see cref="CRResponse"/> can return
        /// the mocked body without hitting the network.
        /// </summary>
        /// <param name="body">Fulfilled body, or <see langword="null"/>.</param>
        /// <param name="status">Fulfilled HTTP status code.</param>
        /// <param name="headers">Fulfilled response headers, or <see langword="null"/>.</param>
        internal void ApplyFulfill(byte[] body, int status = 200, IDictionary<string, string> headers = null)
        {
            Fulfilled = true;
            FulfilledBody = body ?? Array.Empty<byte>();
            FulfilledStatus = status > 0 ? status : 200;
            FulfilledHeaders = headers;
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
                ReplaceHeaders(merged);
                ContinuedHeaders = RouteContinue.RemoveCookie(merged);
            }

            if (postData != null)
            {
                PostDataBuffer = postData;
                PostData = RequestPostData.ToUtf8String(postData);
                ContinuedPostData = postData;
            }
        }

        /// <summary>
        /// Replaces the public header map so omitted continue headers disappear.
        /// </summary>
        /// <param name="headers">The merged continue headers.</param>
        internal void ReplaceHeaders(IDictionary<string, string> headers)
        {
            Headers.Clear();
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!string.IsNullOrEmpty(header.Key))
                {
                    Headers[header.Key] = header.Value ?? string.Empty;
                }
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
        /// Replaces post data from a later protocol event (e.g. Fetch.requestPaused).
        /// </summary>
        /// <param name="buffer">The raw body.</param>
        internal void UpdatePostData(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            PostDataBuffer = buffer;
            PostData = RequestPostData.ToUtf8String(buffer);
        }

        /// <summary>
        /// Waits until this request has finished or failed.
        /// </summary>
        /// <returns>The failure text, or <see langword="null"/> on success.</returns>
        internal Task<string> WaitUntilFinishedAsync() => _finished.Task;

        private IReadOnlyList<NameValueEntry> MergeCookieIntoRaw(IReadOnlyList<NameValueEntry> headers)
        {
            if (HasCookie(headers))
            {
                return headers;
            }

            string cookie = HeaderMap.Value(Headers, "cookie");
            if (string.IsNullOrEmpty(cookie))
            {
                return headers;
            }

            List<NameValueEntry> merged = new(headers);
            merged.Add(new NameValueEntry("cookie", cookie));
            return merged;
        }

        private bool HasCookie(IReadOnlyList<NameValueEntry> headers)
        {
            if (headers == null)
            {
                return false;
            }

            for (int i = 0; i < headers.Count; i++)
            {
                if (string.Equals(headers[i].Name, "cookie", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(headers[i].Value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
