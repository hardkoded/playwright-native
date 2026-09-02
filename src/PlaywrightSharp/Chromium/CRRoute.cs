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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp.Chromium
{
    /// <summary>
    /// Represents an intercepted network request that can be continued, fulfilled, or aborted
    /// using the CDP <c>Fetch</c> domain. Created when a <c>Fetch.requestPaused</c> event
    /// matches a registered route pattern.
    /// </summary>
    internal class CRRoute
    {
        private readonly CRSession _session;
        private readonly string _interceptionId;
        private readonly List<CRRouteEntry> _remaining;
        private readonly HashSet<CRRouteEntry> _invoked = new();
        private readonly Func<CRRouteEntry, bool> _isActive;
        private readonly Func<CRRouteEntry, CRRoute, Task> _invoke;
        private readonly Func<string, List<CRRouteEntry>> _matching;
        private readonly string _baseUrl;
        private bool _handled;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRoute"/> class.
        /// </summary>
        /// <param name="session">The CDP session to send Fetch commands on.</param>
        /// <param name="interceptionId">The Fetch interception request ID from <c>Fetch.requestPaused</c>.</param>
        /// <param name="request">The <see cref="CRRequest"/> associated with this intercepted request.</param>
        /// <param name="remaining">Later matching handlers invoked by <see cref="FallbackAsync"/>.</param>
        /// <param name="isActive">Returns whether a remaining entry is still registered.</param>
        /// <param name="invoke">Invokes a remaining entry.</param>
        /// <param name="matching">Returns every currently registered handler that matches a URL.</param>
        /// <param name="baseUrl">Optional context <c>baseURL</c> for relative globs.</param>
        public CRRoute(
            CRSession session,
            string interceptionId,
            CRRequest request,
            IEnumerable<CRRouteEntry> remaining = null,
            Func<CRRouteEntry, bool> isActive = null,
            Func<CRRouteEntry, CRRoute, Task> invoke = null,
            Func<string, List<CRRouteEntry>> matching = null,
            string baseUrl = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _interceptionId = interceptionId ?? throw new ArgumentNullException(nameof(interceptionId));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _remaining = remaining == null ? new List<CRRouteEntry>() : new List<CRRouteEntry>(remaining);
            _isActive = isActive;
            _invoke = invoke;
            _matching = matching;
            _baseUrl = baseUrl;
        }

        /// <summary>
        /// Gets the intercepted request.
        /// </summary>
        internal CRRequest Request { get; }

        /// <summary>
        /// Marks <paramref name="entry"/> as already invoked so fallback skips it.
        /// </summary>
        /// <param name="entry">The handler that is about to run.</param>
        internal void MarkInvoked(CRRouteEntry entry)
        {
            if (entry != null)
            {
                _invoked.Add(entry);
            }
        }

        /// <summary>
        /// Continues the intercepted request, optionally overriding the URL, method, headers, or post data.
        /// Sends the CDP <c>Fetch.continueRequest</c> command. Only non-null overrides are
        /// included in the command parameters — Chrome's Fetch domain rejects commands that
        /// include null-valued optional fields, so unchanged fields must be omitted entirely.
        /// </summary>
        /// <param name="url">Optional URL override.</param>
        /// <param name="method">Optional HTTP method override.</param>
        /// <param name="headers">Optional headers override. Converted to an array of <c>{ name, value }</c> objects.</param>
        /// <param name="postData">Optional post data override. Will be base64-encoded before sending.</param>
        /// <param name="postDataBytes">Optional raw post-data override. Wins over <paramref name="postData"/>.</param>
        /// <returns>A task that completes when the CDP command is acknowledged.</returns>
        internal async Task ContinueAsync(
            string url = null,
            string method = null,
            IDictionary<string, string> headers = null,
            string postData = null,
            byte[] postDataBytes = null)
        {
            RouteContinue.EnsureSameProtocol(Request.Url, url);
            byte[] body = postDataBytes ?? (postData == null ? null : Encoding.UTF8.GetBytes(postData));
            Request.ApplyContinueOverrides(url, method, headers, body);

            EnsureNotHandled();

            Dictionary<string, object> parameters = new()
            {
                ["requestId"] = _interceptionId,
            };

            string sendUrl = url ?? Request.ContinuedUrl;
            if (sendUrl != null)
            {
                parameters["url"] = sendUrl;
            }

            string sendMethod = method ?? Request.ContinuedMethod;
            if (sendMethod != null)
            {
                parameters["method"] = sendMethod;
            }

            IDictionary<string, string> protocolHeaders = Request.ContinuedHeaders;
            if (protocolHeaders != null)
            {
                parameters["headers"] = protocolHeaders
                    .Select(kvp => new { name = kvp.Key, value = kvp.Value })
                    .ToArray();
            }

            byte[] sendBody = body ?? Request.ContinuedPostData;
            if (sendBody != null)
            {
                parameters["postData"] = Convert.ToBase64String(sendBody);
            }

            try
            {
                await _session.SendAsync("Fetch.continueRequest", parameters).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException ex) when (IsCancelledInterception(ex))
            {
                // Official: continue after the page cancelled the request must not throw.
            }
        }

        /// <summary>
        /// Fulfills the intercepted request with a custom response.
        /// Sends the CDP <c>Fetch.fulfillRequest</c> command. Only non-null/non-empty optional
        /// fields are included in the command parameters — Chrome's Fetch domain rejects
        /// commands that include null-valued optional fields.
        /// </summary>
        /// <param name="statusCode">The HTTP status code for the response.</param>
        /// <param name="body">Optional UTF-8 response body. Ignored when <paramref name="bodyBytes"/> is set.</param>
        /// <param name="contentType">Optional content type header value.</param>
        /// <param name="headers">Optional response headers.</param>
        /// <param name="bodyBytes">Optional raw response body. Takes precedence over <paramref name="body"/>.</param>
        /// <returns>A task that completes when the CDP command is acknowledged.</returns>
        internal async Task FulfillAsync(
            int statusCode,
            string body = null,
            string contentType = null,
            IDictionary<string, string> headers = null,
            byte[] bodyBytes = null)
        {
            EnsureNotHandled();

            List<object> responseHeaders = new();

            foreach (KeyValuePair<string, string> kvp in RouteFulfill.SplitSetCookie(headers))
            {
                responseHeaders.Add(new { name = kvp.Key, value = kvp.Value });
            }

            if (contentType != null)
            {
                responseHeaders.Add(new { name = "content-type", value = contentType });
            }

            AddDefaultCorsOrigin(responseHeaders, headers);

            byte[] rawBody = bodyBytes ?? (body == null ? null : Encoding.UTF8.GetBytes(body));
            bool hasContentLength = headers != null
                && headers.Keys.Any(key => string.Equals(key, "content-length", StringComparison.OrdinalIgnoreCase));
            if (rawBody != null && !hasContentLength)
            {
                responseHeaders.Add(new { name = "content-length", value = rawBody.Length.ToString(CultureInfo.InvariantCulture) });
            }

            Dictionary<string, string> fulfillHeaders = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kvp in RouteFulfill.SplitSetCookie(headers ?? new Dictionary<string, string>()))
            {
                fulfillHeaders[kvp.Key] = kvp.Value;
            }

            if (contentType != null)
            {
                fulfillHeaders["content-type"] = contentType;
            }

            Request.ApplyFulfill(rawBody, statusCode, fulfillHeaders);

            Dictionary<string, object> parameters = new()
            {
                ["requestId"] = _interceptionId,
                ["responseCode"] = statusCode,
                ["responsePhrase"] = HttpStatusText.For(statusCode),
            };

            if (responseHeaders.Count > 0)
            {
                parameters["responseHeaders"] = responseHeaders.ToArray();
            }

            if (rawBody != null)
            {
                parameters["body"] = Convert.ToBase64String(rawBody);
            }

            try
            {
                await _session.SendAsync("Fetch.fulfillRequest", parameters).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException ex) when (IsCancelledInterception(ex))
            {
                // Official: fulfill after the page cancelled the request must not throw.
            }
        }

        /// <summary>
        /// Invokes the next matching handler, or continues the request when none remain.
        /// </summary>
        /// <param name="url">Optional URL override when falling through to the network.</param>
        /// <param name="method">Optional method override when falling through to the network.</param>
        /// <param name="headers">Optional header override when falling through to the network.</param>
        /// <param name="postData">Optional post-data override when falling through to the network.</param>
        /// <param name="postDataBytes">Optional raw post-data override. Wins over <paramref name="postData"/>.</param>
        /// <returns>A task that completes when the next handler or continue has been invoked.</returns>
        internal Task FallbackAsync(
            string url = null,
            string method = null,
            IDictionary<string, string> headers = null,
            string postData = null,
            byte[] postDataBytes = null)
        {
            if (_handled)
            {
                throw new PlaywrightSharpException("Route is already handled!");
            }

            byte[] body = postDataBytes ?? (postData == null ? null : Encoding.UTF8.GetBytes(postData));
            Request.ApplyContinueOverrides(url, method, headers, body);
            List<CRRouteEntry> candidates = _matching != null
                ? _matching(Request.Url)
                : _remaining;
            foreach (CRRouteEntry next in candidates)
            {
                if (_invoked.Contains(next))
                {
                    continue;
                }

                if (_isActive != null && !_isActive(next))
                {
                    continue;
                }

                if (!next.MatchesUrl(Request.Url, _baseUrl))
                {
                    continue;
                }

                _invoked.Add(next);
                if (_invoke != null)
                {
                    return _invoke(next, this);
                }

                return next.Handler(this);
            }

            return ContinueAsync(url, method, headers, postData, postDataBytes: body);
        }

        /// <summary>
        /// Aborts the intercepted request with the specified error reason.
        /// Sends the CDP <c>Fetch.failRequest</c> command.
        /// </summary>
        /// <param name="errorReason">The error reason string. Defaults to <c>"Failed"</c>.</param>
        /// <returns>A task that completes when the CDP command is acknowledged.</returns>
        internal async Task AbortAsync(string errorReason = "Failed")
        {
            EnsureNotHandled();

            try
            {
                await _session.SendAsync("Fetch.failRequest", new
                {
                    requestId = _interceptionId,
                    errorReason = FetchErrorReason.ToProtocol(errorReason),
                }).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException ex) when (IsCancelledInterception(ex))
            {
                // Official: abort after the page cancelled the request must not throw.
            }
        }

        /// <summary>
        /// Ensures this route has not already been handled. Throws if it has.
        /// </summary>
        private static bool IsCancelledInterception(Exception ex)
            => ClosedTarget.IsClosed(ex)
                || ex.Message.Contains("Invalid InterceptionId", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("Invalid Interception Id", StringComparison.OrdinalIgnoreCase);

        private void EnsureNotHandled()
        {
            if (_handled)
            {
                throw new PlaywrightSharpException("Route is already handled!");
            }

            _handled = true;
        }

        private void AddDefaultCorsOrigin(List<object> responseHeaders, IDictionary<string, string> headers)
        {
            bool hasOrigin = headers != null
                && headers.Keys.Any(key => string.Equals(key, "access-control-allow-origin", StringComparison.OrdinalIgnoreCase));
            if (hasOrigin)
            {
                return;
            }

            string origin = HeaderMap.Value(Request.Headers, "origin");
            if (!string.IsNullOrEmpty(origin))
            {
                responseHeaders.Add(new { name = "Access-Control-Allow-Origin", value = origin });
            }
        }
    }
}
