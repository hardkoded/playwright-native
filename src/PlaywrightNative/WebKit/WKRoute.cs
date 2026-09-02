/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// An intercepted WebKit request handled via <c>Network.interceptContinue</c> /
    /// <c>interceptRequestWithResponse</c> / <c>interceptRequestWithError</c>.
    /// </summary>
    internal sealed class WKRoute
    {
        private readonly WKTargetSession _session;
        private readonly WKPage _page;
        private readonly string _requestId;
        private readonly List<WKRouteEntry> _remaining;
        private readonly HashSet<WKRouteEntry> _invoked = new();
        private readonly Func<WKRouteEntry, bool> _isActive;
        private readonly Func<WKRouteEntry, WKRoute, Task> _invoke;
        private readonly Func<string, List<WKRouteEntry>> _matching;
        private bool _handled;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKRoute"/> class.
        /// </summary>
        /// <param name="session">The inner target session.</param>
        /// <param name="page">The owning page, used to fail in-flight navigations on abort.</param>
        /// <param name="requestId">The intercepted request id.</param>
        /// <param name="request">The associated <see cref="WKRequest"/>.</param>
        /// <param name="remaining">Later matching handlers invoked by <see cref="FallbackAsync"/>.</param>
        /// <param name="isActive">Returns whether a remaining entry is still registered.</param>
        /// <param name="invoke">Invokes a remaining entry.</param>
        /// <param name="matching">Returns every currently registered handler that matches a URL.</param>
        public WKRoute(
            WKTargetSession session,
            WKPage page,
            string requestId,
            WKRequest request,
            IEnumerable<WKRouteEntry> remaining = null,
            Func<WKRouteEntry, bool> isActive = null,
            Func<WKRouteEntry, WKRoute, Task> invoke = null,
            Func<string, List<WKRouteEntry>> matching = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _requestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _remaining = remaining == null ? new List<WKRouteEntry>() : new List<WKRouteEntry>(remaining);
            _isActive = isActive;
            _invoke = invoke;
            _matching = matching;
        }

        /// <summary>
        /// Gets the intercepted request.
        /// </summary>
        internal WKRequest Request { get; }

        /// <summary>
        /// Gets a value indicating whether continue / fulfill / abort already ran.
        /// </summary>
        internal bool IsHandled => _handled;

        /// <summary>
        /// Marks <paramref name="entry"/> as already invoked so fallback skips it.
        /// </summary>
        /// <param name="entry">The handler that is about to run.</param>
        internal void MarkInvoked(WKRouteEntry entry)
        {
            if (entry != null)
            {
                _invoked.Add(entry);
            }
        }

        /// <summary>
        /// Official HAR navigation redirect: mark the intercept handled and
        /// start a new document navigation at <paramref name="url"/>.
        /// </summary>
        /// <param name="url">The final document URL from the HAR.</param>
        /// <returns>A task that completes when <c>Playwright.navigate</c> is sent.</returns>
        internal Task RedirectHarNavigationAsync(string url)
        {
            EnsureNotHandled();
            return _page.SendHarNavigateAsync(url);
        }

        /// <summary>
        /// Continues the request, optionally overriding URL, method, headers, or post data.
        /// </summary>
        /// <param name="url">Optional URL override.</param>
        /// <param name="method">Optional method override.</param>
        /// <param name="headers">Optional header override.</param>
        /// <param name="postData">Optional post-data override.</param>
        /// <param name="postDataBytes">Optional raw post-data override. Wins over <paramref name="postData"/>.</param>
        /// <returns>A task that completes when the protocol command is acknowledged.</returns>
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
            _page.NoteContinuedNavigation(Request);

            EnsureNotHandled();

            string sendUrl = url ?? Request.ContinuedUrl;
            string sendMethod = method ?? Request.ContinuedMethod;
            byte[] sendBody = body ?? Request.ContinuedPostData;
            IDictionary<string, string> protocolHeaders = Request.ContinuedHeaders;
            if (sendBody != null)
            {
                protocolHeaders = WithContentLength(protocolHeaders, sendBody.Length);
            }

            if (sendUrl == null && sendMethod == null && protocolHeaders == null && sendBody == null)
            {
                try
                {
                    await _session.SendAsync("Network.interceptContinue", new
                    {
                        requestId = _requestId,
                        stage = "request",
                    }).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex) when (IsCancelledInterception(ex))
                {
                }

                return;
            }

            Dictionary<string, object> parameters = new()
            {
                ["requestId"] = _requestId,
            };

            if (sendUrl != null)
            {
                parameters["url"] = sendUrl;
            }

            if (sendMethod != null)
            {
                parameters["method"] = sendMethod;
            }

            if (protocolHeaders != null)
            {
                parameters["headers"] = protocolHeaders;
            }

            if (sendBody != null)
            {
                parameters["postData"] = Convert.ToBase64String(sendBody);
            }

            try
            {
                await _session.SendAsync("Network.interceptWithRequest", parameters).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (IsCancelledInterception(ex))
            {
            }
        }

        /// <summary>
        /// Fulfills the request with a synthetic response.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="body">Optional UTF-8 body.</param>
        /// <param name="contentType">Optional content type.</param>
        /// <param name="headers">Optional response headers.</param>
        /// <param name="bodyBytes">Optional raw body; takes precedence over <paramref name="body"/>.</param>
        /// <returns>A task that completes when the protocol command is acknowledged.</returns>
        internal async Task FulfillAsync(
            int statusCode,
            string body = null,
            string contentType = null,
            IDictionary<string, string> headers = null,
            byte[] bodyBytes = null)
        {
            EnsureNotHandled();

            if (statusCode >= 300 && statusCode < 400)
            {
                throw new PlaywrightNativeException("Cannot fulfill with redirect status");
            }

            Dictionary<string, string> responseHeaders = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kvp in RouteFulfill.SplitSetCookie(headers))
            {
                if (string.Equals(kvp.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                    && responseHeaders.TryGetValue(kvp.Key, out string existing))
                {
                    responseHeaders[kvp.Key] = existing + "\n" + kvp.Value;
                }
                else
                {
                    responseHeaders[kvp.Key] = kvp.Value;
                }
            }

            if (contentType != null)
            {
                responseHeaders["content-type"] = contentType;
            }

            AddDefaultCorsOrigin(responseHeaders);

            byte[] rawBody = bodyBytes ?? (body == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body));
            responseHeaders.Remove("content-encoding");
            responseHeaders.Remove("transfer-encoding");
            responseHeaders["content-length"] = rawBody.Length.ToString(CultureInfo.InvariantCulture);

            string mimeType = MimeTypeFor(contentType, responseHeaders);
            string statusText = HttpStatusText.For(statusCode);
            Request.ApplyFulfill(rawBody);

            try
            {
                await _session.SendAsync("Network.interceptRequestWithResponse", new
                {
                    requestId = _requestId,
                    content = Convert.ToBase64String(rawBody),
                    base64Encoded = true,
                    mimeType,
                    status = statusCode,
                    statusText,
                    headers = responseHeaders,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (
                IsCancelledInterception(ex)
                || ex.Message.Contains("already been processed", StringComparison.OrdinalIgnoreCase))
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
                throw new PlaywrightNativeException("Route is already handled!");
            }

            byte[] body = postDataBytes ?? (postData == null ? null : Encoding.UTF8.GetBytes(postData));
            Request.ApplyContinueOverrides(url, method, headers, body);
            List<WKRouteEntry> candidates = _matching != null
                ? _matching(Request.Url)
                : _remaining;
            foreach (WKRouteEntry next in candidates)
            {
                if (_invoked.Contains(next))
                {
                    continue;
                }

                if (_isActive != null && !_isActive(next))
                {
                    continue;
                }

                if (!next.MatchesUrl(Request.Url, NavigationUrl.ContextBase(_page.Context)))
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
        /// Aborts the request.
        /// </summary>
        /// <param name="errorReason">Playwright error code (mapped onto WebKit <c>ResourceErrorType</c>).</param>
        /// <returns>A task that completes when the protocol command is acknowledged.</returns>
        internal async Task AbortAsync(string errorReason = "Failed")
        {
            EnsureNotHandled();

            string errorType = MapErrorType(errorReason);
            try
            {
                await _session.SendAsync("Network.interceptRequestWithError", new
                {
                    requestId = _requestId,
                    errorType,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (
                IsCancelledInterception(ex)
                || ex.Message.Contains("already been processed", StringComparison.OrdinalIgnoreCase))
            {
                // Official: abort after the page cancelled the request must not throw.
            }
            finally
            {
                _page.FailPendingNavigationIfNeeded(Request);
            }
        }

        private static IDictionary<string, string> WithContentLength(IDictionary<string, string> headers, int length)
        {
            Dictionary<string, string> result = headers == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            if (!result.ContainsKey("content-length"))
            {
                result["content-length"] = length.ToString(CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static bool IsCancelledInterception(Exception ex)
            => ClosedTarget.IsClosed(ex)
                || ex.Message.Contains("Invalid InterceptionId", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("Invalid Interception Id", StringComparison.OrdinalIgnoreCase);

        private static string MimeTypeFor(string contentType, IDictionary<string, string> headers)
        {
            string raw = contentType;
            if (string.IsNullOrEmpty(raw) && headers != null)
            {
                headers.TryGetValue("content-type", out raw);
            }

            if (string.IsNullOrEmpty(raw))
            {
                return "text/plain";
            }

            int separator = raw.IndexOf(';', StringComparison.Ordinal);
            return (separator < 0 ? raw : raw.Substring(0, separator)).Trim();
        }

        private static string MapErrorType(string errorReason)
        {
            string code = (errorReason ?? "Failed").Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (code.Contains("access", StringComparison.OrdinalIgnoreCase))
            {
                return "AccessControl";
            }

            if (code.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("timedout", StringComparison.OrdinalIgnoreCase))
            {
                return "Timeout";
            }

            if (code.Contains("abort", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("cancel", StringComparison.OrdinalIgnoreCase))
            {
                return "Cancellation";
            }

            return "General";
        }

        private void EnsureNotHandled()
        {
            if (_handled)
            {
                throw new PlaywrightNativeException("Route is already handled!");
            }

            _handled = true;
        }

        private void AddDefaultCorsOrigin(Dictionary<string, string> responseHeaders)
        {
            if (responseHeaders.ContainsKey("access-control-allow-origin"))
            {
                return;
            }

            string origin = HeaderMap.Value(Request.Headers, "origin");
            if (!string.IsNullOrEmpty(origin))
            {
                responseHeaders["Access-Control-Allow-Origin"] = origin;
            }
        }
    }
}
