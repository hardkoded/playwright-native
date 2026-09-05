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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Manages network request tracking for a WebKit page by listening to the inner
    /// target session's <c>Network.*</c> events. Creates <see cref="WKRequest"/> and
    /// <see cref="WKResponse"/> instances and fires events on the owning <see cref="WKPage"/>.
    /// Mirrors <c>CRNetworkManager</c> but uses the WebKit Inspector Protocol payload shape
    /// (nested <c>request</c> / <c>response</c> objects, <c>redirectResponse</c>).
    /// Request interception uses <c>Network.setInterceptionEnabled</c> plus
    /// <c>Network.requestIntercepted</c>.
    /// </summary>
    internal class WKNetworkManager
    {
        private readonly WKTargetSession _session;
        private readonly WKPage _page;
        private readonly ConcurrentDictionary<string, WKRequest> _requestsById = new();
        private readonly ConcurrentDictionary<string, WKWebSocket> _webSockets = new();
        private readonly List<WKRouteEntry> _routes = new();
        private readonly ConcurrentDictionary<string, JsonElement> _pendingIntercepts = new();
        private readonly ConcurrentDictionary<string, byte> _handledIntercepts = new();
        private bool _interceptingEnabled;
        private int _inFlightRouteHandlers;
        private bool _popupMainRequestEmitted;
        private IReadOnlyList<HttpCredentials> _httpCredentials = Array.Empty<HttpCredentials>();
        private string _locale;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKNetworkManager"/> class and
        /// subscribes to the target session's <see cref="WKTargetSession.MessageReceived"/> event.
        /// </summary>
        /// <param name="session">The inner target session to listen on.</param>
        /// <param name="page">The owning <see cref="WKPage"/> to fire events on.</param>
        public WKNetworkManager(WKTargetSession session, WKPage page)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _page = page ?? throw new ArgumentNullException(nameof(page));

            _session.MessageReceived += OnMessage;
        }

        /// <summary>
        /// Detaches from the target session. Called when the page tears the session down.
        /// In-flight requests are marked finished so <c>response.finished()</c> cannot
        /// hang across a process swap that drops <c>Network.loadingFinished</c>.
        /// </summary>
        internal void Dispose()
        {
            _session.MessageReceived -= OnMessage;
            foreach (KeyValuePair<string, WKRequest> pair in _requestsById)
            {
                WKRequest request = pair.Value;
                if (request == null)
                {
                    continue;
                }

                if (_page.IsClosed || _page.IsClosing)
                {
                    request.AbortClosed(new TargetClosedException(
                        DriverMessages.BrowserOrContextClosedExceptionMessage));
                    continue;
                }

                request.MarkFinished();
                if (request.IsNavigationRequest
                    && request.Response != null
                    && !request.SuppressPageEvents)
                {
                    _page.OnRequestFinished(request);
                }
            }
        }

        /// <summary>
        /// Rejects inflight request waiters when the page closes.
        /// </summary>
        /// <param name="error">The target-closed error.</param>
        internal void AbortInflightClosed(Exception error)
        {
            foreach (KeyValuePair<string, WKRequest> pair in _requestsById)
            {
                pair.Value?.AbortClosed(error);
            }
        }

        /// <summary>
        /// Whether this manager already listens on <paramref name="session"/>.
        /// Recreating the manager on the same session drops in-flight request
        /// ids and loses <c>loadingFinished</c> / iframe requests.
        /// </summary>
        /// <param name="session">The target session to compare.</param>
        /// <returns><see langword="true"/> when already attached.</returns>
        internal bool AttachedTo(WKTargetSession session) => ReferenceEquals(_session, session);

        /// <summary>
        /// Drops inflight tracking for requests owned by a detaching frame.
        /// </summary>
        /// <param name="frame">The frame that is about to detach.</param>
        internal void FinishInflightForDetachedFrame(WKFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            foreach (KeyValuePair<string, WKRequest> pair in _requestsById)
            {
                WKRequest request = pair.Value;
                WKFrame owner = request.Frame is WebKitFrame instance
                    ? instance.GetWKFrame()
                    : null;
                bool matchesFrame = owner == frame;
                bool matchesUrl = !string.IsNullOrEmpty(frame.Url)
                    && string.Equals(request.Url, frame.Url, StringComparison.Ordinal);
                if (!matchesFrame && !matchesUrl)
                {
                    continue;
                }

                _page.FinishInflight(request);
            }

            frame.ClearInflightForDetach();
        }

        /// <summary>
        /// Registers a route handler.
        /// </summary>
        /// <param name="entry">The route registration.</param>
        internal void AddRoute(WKRouteEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (_routes)
            {
                if (_routes.Contains(entry))
                {
                    return;
                }

                _routes.Add(entry);
            }
        }

        /// <summary>
        /// Removes every route, or only context-level / page-level routes.
        /// </summary>
        /// <param name="contextRoute">
        /// When set, only context-level (<see langword="true"/>) or page-level
        /// (<see langword="false"/>) routes are removed.
        /// </param>
        /// <returns>The registrations that were removed.</returns>
        internal List<WKRouteEntry> ClearRoutes(bool? contextRoute = null)
        {
            lock (_routes)
            {
                if (!contextRoute.HasValue)
                {
                    List<WKRouteEntry> all = new(_routes);
                    _routes.Clear();
                    return all;
                }

                List<WKRouteEntry> removed = new();
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    if (_routes[i].IsContextRoute == contextRoute.Value)
                    {
                        removed.Add(_routes[i]);
                        _routes.RemoveAt(i);
                    }
                }

                return removed;
            }
        }

        /// <summary>
        /// Removes matching route registrations.
        /// </summary>
        /// <param name="urlString">Glob used at registration, or <see langword="null"/>.</param>
        /// <param name="urlRegex">Regex used at registration, or <see langword="null"/>.</param>
        /// <param name="urlFunc">Predicate used at registration, or <see langword="null"/>.</param>
        /// <param name="handlerIdentity">Handler to remove, or <see langword="null"/> for all matching matchers.</param>
        /// <param name="contextRoute">When set, only context-level or page-level routes are removed.</param>
        /// <returns>The registrations that were removed.</returns>
        internal List<WKRouteEntry> RemoveRoute(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            object handlerIdentity,
            bool? contextRoute = null)
        {
            lock (_routes)
            {
                List<WKRouteEntry> removed = new();
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    WKRouteEntry entry = _routes[i];
                    if (contextRoute.HasValue && entry.IsContextRoute != contextRoute.Value)
                    {
                        continue;
                    }

                    if (entry.MatchesRegistration(urlString, urlRegex, urlFunc, handlerIdentity))
                    {
                        removed.Add(entry);
                        _routes.RemoveAt(i);
                    }
                }

                return removed;
            }
        }

        /// <summary>
        /// Removes a specific route registration by reference.
        /// </summary>
        /// <param name="entry">The route to remove.</param>
        internal void RemoveEntry(WKRouteEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            lock (_routes)
            {
                _routes.Remove(entry);
            }
        }

        /// <summary>
        /// Returns a snapshot of the current route list so a replacement manager can inherit it.
        /// </summary>
        /// <returns>The registered routes.</returns>
        internal IReadOnlyList<WKRouteEntry> SnapshotRoutes()
        {
            lock (_routes)
            {
                return _routes.ToArray();
            }
        }

        /// <summary>
        /// Stores HTTP credentials so intercepted requests can send a per-URL
        /// <c>Authorization</c> header (official origin matching).
        /// </summary>
        /// <param name="httpCredentials">Configured credentials, or an empty list.</param>
        internal void SetHttpCredentials(IReadOnlyList<HttpCredentials> httpCredentials)
            => _httpCredentials = HttpBasicAuth.Snapshot(httpCredentials);

        /// <summary>
        /// Stores the context locale so intercepted requests can send a default
        /// <c>Accept-Language</c> without overriding a user <c>fetch</c> header.
        /// </summary>
        /// <param name="locale">BCP 47 locale, or <see langword="null"/>.</param>
        internal void SetLocale(string locale) => _locale = locale;

        /// <summary>
        /// Enables or disables WebKit request interception based on whether any
        /// routes are registered or HTTP credentials need a per-request
        /// <c>Authorization</c> header (origin match, and to avoid overriding
        /// a page-set Bearer token).
        /// </summary>
        /// <returns>A task that completes when the protocol commands are acknowledged.</returns>
        internal async Task UpdateInterceptionAsync()
        {
            int routeCount;
            lock (_routes)
            {
                routeCount = _routes.Count;
            }

            bool extra = HasExtraHttpHeaders();
            bool needIntercept = routeCount > 0
                || _page.WKContext?.HasContextRoutes == true
                || HttpBasicAuth.HasCredentials(_httpCredentials)
                || !string.IsNullOrEmpty(_locale)
                || extra
                || _inFlightRouteHandlers > 0;
            if (needIntercept && !_interceptingEnabled)
            {
                _interceptingEnabled = true;
                await _session.SendAsync("Network.setInterceptionEnabled", new { enabled = true }).ConfigureAwait(false);
                await _session.SendAsync("Network.setResourceCachingDisabled", new { disabled = true }).ConfigureAwait(false);
                await _session.SendAsync(
                    "Network.addInterception",
                    new { url = ".*", stage = "request", isRegex = true }).ConfigureAwait(false);
            }
            else if (!needIntercept && _interceptingEnabled)
            {
                _interceptingEnabled = false;
                await _session.SendAsync("Network.setInterceptionEnabled", new { enabled = false }).ConfigureAwait(false);
                await _session.SendAsync("Network.setResourceCachingDisabled", new { disabled = false }).ConfigureAwait(false);
            }
        }

        private static IReadOnlyList<NameValueEntry> ApplyWebKitHostHeader(
            IReadOnlyList<NameValueEntry> headers,
            string url)
        {
            List<NameValueEntry> list = new(headers);
            bool hasHost = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Name, "host", StringComparison.OrdinalIgnoreCase))
                {
                    hasHost = true;
                    break;
                }
            }

            if (!hasHost && !string.IsNullOrEmpty(url))
            {
                try
                {
                    list.Add(new NameValueEntry("Host", new Uri(url).Authority));
                }
                catch (UriFormatException)
                {
                }
            }

            return list;
        }

        private static bool IsWebSocketUrl(string url)
            => !string.IsNullOrEmpty(url)
                && (url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase));

        private static bool HasWebSocketUpgrade(IEnumerable<KeyValuePair<string, string>> headers)
        {
            string upgrade = HeaderMap.Value(headers, "upgrade");
            return !string.IsNullOrEmpty(upgrade)
                && upgrade.Contains("websocket", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameWebSocketUrl(string socketUrl, string requestUrl)
        {
            if (string.IsNullOrEmpty(socketUrl) || string.IsNullOrEmpty(requestUrl))
            {
                return false;
            }

            if (string.Equals(socketUrl, requestUrl, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(
                NormalizeWebSocketUrl(socketUrl),
                NormalizeWebSocketUrl(requestUrl),
                StringComparison.Ordinal);
        }

        private static string NormalizeWebSocketUrl(string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat("ws://", url.AsSpan("http://".Length));
            }

            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat("wss://", url.AsSpan("https://".Length));
            }

            return url;
        }

        private static IDictionary<string, string> ParseHeaders(JsonElement payload, bool caseInsensitive)
        {
            Dictionary<string, string> result = caseInsensitive
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.Ordinal);

            if (payload.TryGetProperty("headers", out JsonElement headersElement)
                && headersElement.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in headersElement.EnumerateObject())
                {
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            return result;
        }

        private static IReadOnlyList<NameValueEntry> ParseExtraHeaders(JsonElement eventPayload, JsonElement responsePayload)
        {
            IReadOnlyList<NameValueEntry> fromMap = ResponseHeaders.FromWebKitMap(
                ParseHeaders(responsePayload, caseInsensitive: false));
            if (fromMap.Count > 0)
            {
                return fromMap;
            }

            if (eventPayload.TryGetProperty("headersText", out JsonElement eventText))
            {
                IReadOnlyList<NameValueEntry> fromEvent = ResponseHeaders.ParseHeadersText(eventText.GetString());
                if (fromEvent.Count > 0)
                {
                    return fromEvent;
                }
            }

            if (responsePayload.TryGetProperty("headersText", out JsonElement responseText))
            {
                IReadOnlyList<NameValueEntry> fromResponse = ResponseHeaders.ParseHeadersText(responseText.GetString());
                if (fromResponse.Count > 0)
                {
                    return fromResponse;
                }
            }

            return ResponseHeaders.FromWebKitMap(ParseHeaders(responsePayload, caseInsensitive: true));
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement prop)
                ? prop.GetString()
                : null;
        }

        private static bool IsHttpsUrl(string url)
            => !string.IsNullOrEmpty(url)
                && url.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

        private static bool IsTlsProtocol(string protocol)
            => !string.IsNullOrEmpty(protocol)
                && protocol.StartsWith("TLS", StringComparison.OrdinalIgnoreCase);

        private static bool IsAboutBlankUrl(string url)
        {
            return !string.IsNullOrEmpty(url)
                && url.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement prop)
                && prop.TryGetInt32(out int value)
                ? value
                : 0;
        }

        private static int ReadEncodedBodyLength(JsonElement finished)
        {
            if (finished.TryGetProperty("metrics", out JsonElement metrics)
                && metrics.ValueKind == JsonValueKind.Object
                && metrics.TryGetProperty("responseBodyBytesReceived", out JsonElement body)
                && body.ValueKind == JsonValueKind.Number)
            {
                return (int)body.GetDouble();
            }

            return (int)ResourceTimingParser.ReadDouble(finished, "encodedDataLength");
        }

        private static string DecodePostData(string postData)
        {
            if (string.IsNullOrEmpty(postData) || (postData.Length % 4) != 0)
            {
                return postData;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(postData);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return postData;
            }
        }

        private static void LogRouteHandlerError(Exception ex)
        {
            System.Console.Error.WriteLine($"[WKNetworkManager] Route handler error: {ex}");
        }

        private void OnMessage(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Network.requestWillBeSent":
                    OnRequestWillBeSent(parameters);
                    break;
                case "Network.responseReceived":
                    OnResponseReceived(parameters);
                    break;
                case "Network.loadingFinished":
                    OnLoadingFinished(parameters);
                    break;
                case "Network.loadingFailed":
                    OnLoadingFailed(parameters);
                    break;
                case "Network.requestIntercepted":
                    OnRequestIntercepted(parameters);
                    break;
                case "Network.webSocketCreated":
                    OnWebSocketCreated(parameters);
                    break;
                case "Network.webSocketWillSendHandshakeRequest":
                    OnWebSocketHandshakeRequest(parameters);
                    break;
                case "Network.webSocketHandshakeResponseReceived":
                    OnWebSocketHandshakeResponse(parameters);
                    break;
                case "Network.webSocketClosed":
                    OnWebSocketClosed(parameters);
                    break;
                case "Network.webSocketFrameReceived":
                    OnWebSocketFrame(parameters, sent: false);
                    break;
                case "Network.webSocketFrameSent":
                    OnWebSocketFrame(parameters, sent: true);
                    break;
                case "Network.webSocketFrameError":
                    OnWebSocketError(parameters);
                    break;
            }
        }

        private void OnWebSocketCreated(JsonElement? parameters)
        {
            if (!WebSocketProtocol.TryReadCreated(parameters, out string requestId, out string url))
            {
                return;
            }

            foreach (WKWebSocket existing in _webSockets.Values)
            {
                if (!existing.IsClosed
                    && string.Equals(existing.Url, url, StringComparison.Ordinal))
                {
                    _webSockets.TryAdd(requestId, existing);
                    if (_requestsById.TryGetValue(requestId, out WKRequest reused))
                    {
                        existing.Har.ApplyHandshakeRequest(
                            reused.Headers,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            0);
                        _page.FinishInflight(reused);
                    }

                    return;
                }
            }

            WKWebSocket socket = new(requestId, url, _page);
            if (!_webSockets.TryAdd(requestId, socket))
            {
                return;
            }

            if (_requestsById.TryGetValue(requestId, out WKRequest request))
            {
                socket.Har.ApplyHandshakeRequest(
                    request.Headers,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    0);
                _page.FinishInflight(request);
            }

            _page.OnWebSocketCreated(socket);
        }

        private void OnWebSocketHandshakeRequest(JsonElement? parameters)
        {
            string requestId = WebSocketProtocol.ReadRequestId(parameters);
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out WKWebSocket socket))
            {
                return;
            }

            if (!WebSocketProtocol.TryReadHandshakeRequest(
                parameters,
                out List<KeyValuePair<string, string>> headers,
                out double wallTimeMs,
                out double timestampSeconds))
            {
                return;
            }

            socket.Har.ApplyHandshakeRequest(headers, wallTimeMs, timestampSeconds);
        }

        private void OnWebSocketHandshakeResponse(JsonElement? parameters)
        {
            string requestId = WebSocketProtocol.ReadRequestId(parameters);
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out WKWebSocket socket))
            {
                return;
            }

            if (!WebSocketProtocol.TryReadHandshakeResponse(
                parameters,
                out int status,
                out string statusText,
                out List<KeyValuePair<string, string>> headers))
            {
                return;
            }

            socket.Har.ApplyHandshakeResponse(status, statusText, headers);
            if (status > 0 && status != 101)
            {
                socket.NotifyError(WebSocketProtocol.FormatSocketError(string.Empty, status));
            }
        }

        private void OnWebSocketClosed(JsonElement? parameters)
        {
            string requestId = WebSocketProtocol.ReadRequestId(parameters);
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryRemove(requestId, out WKWebSocket socket))
            {
                return;
            }

            socket.NotifyClosed();
        }

        private void OnWebSocketFrame(JsonElement? parameters, bool sent)
        {
            string requestId = WebSocketProtocol.ReadRequestId(parameters);
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out WKWebSocket socket))
            {
                return;
            }

            IWebSocketFrame frame = WebSocketProtocol.ReadFrame(parameters, out double timestamp);
            if (frame is not WebSocketFrame parsed)
            {
                return;
            }

            double wallTimeMs = socket.Har.FrameWallTimeMs(timestamp);
            IWebSocketFrame timed = new WebSocketFrame(parsed.Text, parsed.Binary, parsed.Opcode, wallTimeMs);
            if (sent)
            {
                socket.NotifyFrameSent(timed);
            }
            else
            {
                socket.NotifyFrameReceived(timed);
            }
        }

        private void OnWebSocketError(JsonElement? parameters)
        {
            string requestId = WebSocketProtocol.ReadRequestId(parameters);
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out WKWebSocket socket))
            {
                return;
            }

            string message = string.Empty;
            if (parameters.HasValue
                && parameters.Value.TryGetProperty("errorMessage", out JsonElement errorEl)
                && errorEl.ValueKind == JsonValueKind.String)
            {
                message = errorEl.GetString();
            }

            socket.NotifyError(WebSocketProtocol.FormatSocketError(message, socket.Har.Status));
        }

        private void OnRequestWillBeSent(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string requestId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            // Handle redirect: if redirectResponse is present, the existing request was
            // redirected. WebKit reports the redirect chain by reusing the requestId.
            WKRequest redirectedFrom = null;
            if (p.TryGetProperty("redirectResponse", out JsonElement redirectResponse)
                && redirectResponse.ValueKind == JsonValueKind.Object)
            {
                if (_requestsById.TryRemove(requestId, out WKRequest existingRequest))
                {
                    _handledIntercepts.TryRemove(requestId, out _);
                    string redirectUrl = GetString(redirectResponse, "url");
                    int redirectStatus = GetInt(redirectResponse, "status");
                    string redirectStatusText = GetString(redirectResponse, "statusText");
                    IDictionary<string, string> redirectHeaders = ParseHeaders(redirectResponse, caseInsensitive: true);

                    WKResponse redirectResponseObj = new(
                        _session,
                        existingRequest,
                        redirectUrl,
                        redirectStatus,
                        redirectStatusText,
                        redirectHeaders,
                        ResponseNetworkInfo.ParseServerAddr(redirectResponse),
                        ResponseNetworkInfo.ParseSecurityDetails(redirectResponse),
                        ResponseNetworkInfo.ParseFromServiceWorker(redirectResponse),
                        ResponseNetworkInfo.ParseHttpVersion(redirectResponse));

                    RaiseResponseReceived(redirectResponseObj);
                    RaiseRequestFinished(existingRequest);

                    redirectedFrom = existingRequest;
                }
            }

            // Parse the request payload (nested under "request" in the WebKit event).
            if (!p.TryGetProperty("request", out JsonElement requestPayload))
            {
                return;
            }

            string url = GetString(requestPayload, "url");
            string method = GetString(requestPayload, "method");
            byte[] postDataBuffer = RequestPostData.FromWebKitBase64(GetString(requestPayload, "postData"));
            string postData = RequestPostData.ToUtf8String(postDataBuffer);
            IDictionary<string, string> headers = ParseHeaders(requestPayload, caseInsensitive: false);

            string type = GetString(p, "type");
            bool isNavigationRequest = NetworkRequestEvents.IsDocumentNavigation(type);
            string frameId = GetString(p, "frameId");
            IFrame frame = _page.GetOrCreateFrameById(frameId);
            if (redirectedFrom == null && isNavigationRequest)
            {
                redirectedFrom = _page.ConsumeRedirectSource(url);
            }

            WKRequest request = new(
                requestId,
                url,
                method,
                headers,
                postData,
                type ?? string.Empty,
                isNavigationRequest,
                redirectedFrom,
                frame,
                postDataBuffer);

            request.DocumentUrl = isNavigationRequest ? url : frame?.Url;
            request.TimestampSeconds = ResourceTimingParser.ReadDouble(p, "timestamp");
            double wallTime = ResourceTimingParser.ReadDouble(p, "wallTime");
            if (wallTime <= 0)
            {
                wallTime = request.TimestampSeconds;
            }

            ResourceTimingParser.ApplyWallTime(request.Timing, wallTime);

            _requestsById[requestId] = request;
            RaiseRequestCreated(request);

            if (_pendingIntercepts.TryRemove(requestId, out _))
            {
                OnInterceptedRequest(requestId, request);
            }
        }

        private void OnResponseReceived(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string requestId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            if (!_requestsById.TryGetValue(requestId, out WKRequest request))
            {
                return;
            }

            if (!p.TryGetProperty("response", out JsonElement responsePayload))
            {
                return;
            }

            string url = GetString(responsePayload, "url");
            int status = GetInt(responsePayload, "status");
            string statusText = GetString(responsePayload, "statusText");
            IDictionary<string, string> headers = ParseHeaders(responsePayload, caseInsensitive: true);

            if (responsePayload.TryGetProperty("timing", out JsonElement resourceTiming))
            {
                request.TimingRequestTime = ResourceTimingParser.ApplyResourceTiming(request.Timing, resourceTiming);
            }

            WKRequest publicRequest = request;
            string protocolRequestId = null;
            if (request.SuppressPageEvents)
            {
                WKRequest original = _page.FirstPendingNavigationRequest;
                if (original != null && original != request)
                {
                    if (original.Response != null)
                    {
                        return;
                    }

                    publicRequest = original;
                    protocolRequestId = request.RequestId;
                }
            }

            WKResponse response = new(
                _session,
                publicRequest,
                url,
                status,
                statusText,
                headers,
                ResponseNetworkInfo.ParseServerAddr(responsePayload),
                ResponseNetworkInfo.ParseSecurityDetails(responsePayload),
                ResponseNetworkInfo.ParseFromServiceWorker(responsePayload),
                ResponseNetworkInfo.ParseHttpVersion(responsePayload),
                protocolRequestId)
            {
                ResponsePayload = responsePayload,
            };

            if (IsHttpsUrl(url))
            {
                ResponseSecurityDetailsResult early = ResponseNetworkInfo.ParseWebKitSecurity(responsePayload, null);
                if (early != null)
                {
                    response.SecurityDetails = early;
                }
            }

            IReadOnlyList<NameValueEntry> extra = ParseExtraHeaders(p, responsePayload);
            if (extra.Count > 0)
            {
                response.ApplyExtraHeaders(extra);
            }

            if (responsePayload.TryGetProperty("requestHeaders", out JsonElement requestHeaders)
                && requestHeaders.ValueKind == JsonValueKind.Object
                && requestHeaders.EnumerateObject().GetEnumerator().MoveNext())
            {
                request.SetRawRequestHeaders(ApplyWebKitHostHeader(
                    RawNetworkHeaders.FromObject(requestHeaders, separator: null),
                    request.Url));
            }

            RaiseResponseReceived(response);
            if (publicRequest != request && publicRequest.Finished)
            {
                _page.OnRequestFinished(request);
            }
        }

        private void OnLoadingFinished(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string requestId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            if (_requestsById.TryRemove(requestId, out WKRequest request))
            {
                double baseline = request.TimingRequestTime > 0
                    ? request.TimingRequestTime
                    : request.TimestampSeconds;
                ResourceTimingParser.ApplyResponseEnd(
                    request.Timing,
                    baseline,
                    ResourceTimingParser.ReadDouble(p, "timestamp"));
                ResourceTimingParser.FillMissingFromResponseEnd(request.Timing);
                request.EncodedDataLength = ReadEncodedBodyLength(p);
                request.Finished = true;
                ApplyFinishedMetrics(request, p);
                RaiseRequestFinished(request);
            }
        }

        private void ApplyFinishedMetrics(WKRequest request, JsonElement finished)
        {
            WKRequest target = request;
            if (request.SuppressPageEvents)
            {
                WKRequest publicRequest = _page.FirstPendingNavigationRequest;
                if (publicRequest != null)
                {
                    target = publicRequest;
                    if (request.EncodedDataLength > 0)
                    {
                        publicRequest.EncodedDataLength = request.EncodedDataLength;
                    }
                }
            }

            if (target.Response is not WKResponse wkResponse)
            {
                return;
            }

            string finishedVersion = ResponseNetworkInfo.ParseHttpVersionFromFinished(finished);
            if (!string.IsNullOrEmpty(finishedVersion))
            {
                wkResponse.HttpVersion = finishedVersion;
            }

            if (finished.TryGetProperty("metrics", out JsonElement metrics)
                && metrics.ValueKind == JsonValueKind.Object
                && metrics.TryGetProperty("remoteAddress", out JsonElement remote)
                && remote.ValueKind == JsonValueKind.String)
            {
                ResponseServerAddrResult parsed = ResponseNetworkInfo.ParseRemoteAddress(remote.GetString())
                    ?? wkResponse.ServerAddr;
                wkResponse.ServerAddr = ResponseNetworkInfo.PreferDestinationOverInternalProxy(
                    parsed,
                    target.Url,
                    _page.WKContext?.InternalProxyPort)
                    ?? wkResponse.ServerAddr;
            }

            if (wkResponse.ResponsePayload.ValueKind == JsonValueKind.Object)
            {
                ResponseSecurityDetailsResult details = ResponseNetworkInfo.ParseWebKitSecurity(
                    wkResponse.ResponsePayload,
                    finished);
                if (details != null
                    && (IsHttpsUrl(target.Url) || IsTlsProtocol(details.Protocol)))
                {
                    wkResponse.SecurityDetails = details;
                }
            }
        }

        private void OnLoadingFailed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string requestId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            WKRequest request = null;
            if (_requestsById.TryRemove(requestId, out WKRequest removed))
            {
                request = removed;
                string errorText = GetString(p, "errorText");
                if (request.Response != null && request.Response.Status == 204)
                {
                    request.Finished = true;
                    RaiseRequestFinished(request);
                }
                else
                {
                    request.FailureText = errorText ?? string.Empty;
                    RaiseRequestFailed(request);
                }
            }

            if (FindWebSocket(requestId, request?.Url) is WKWebSocket socket)
            {
                if (request?.Response != null)
                {
                    socket.Har.ApplyHandshakeResponse(
                        request.Response.Status,
                        request.Response.StatusText,
                        request.Response.Headers,
                        overwrite: false);
                }

                string failedText = GetString(p, "errorText");
                if (!string.IsNullOrEmpty(failedText))
                {
                    socket.NotifyError(failedText);
                }
            }
        }

        private void OnRequestIntercepted(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string requestId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            if (_requestsById.TryGetValue(requestId, out WKRequest request))
            {
                OnInterceptedRequest(requestId, request);
                return;
            }

            // Wait for requestWillBeSent so the handler runs once. Handling
            // intercept-first (or continuing it) issues a second network request.
            _pendingIntercepts[requestId] = p;
        }

        private void OnInterceptedRequest(string requestId, WKRequest request)
        {
            if (!_handledIntercepts.TryAdd(requestId, 0))
            {
                return;
            }

            request.SetRawRequestHeaders(HeaderMap.Array(request.Headers));

            if (request.WKRedirectedFrom != null)
            {
                WKRoute redirectRoute = new(_session, _page, requestId, request);
                IDictionary<string, string> continuedHeaders = request.WKRedirectedFrom.ContinuedHeaders;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (continuedHeaders != null)
                        {
                            await redirectRoute.ContinueAsync(headers: continuedHeaders).ConfigureAwait(false);
                        }
                        else
                        {
                            await redirectRoute.ContinueAsync().ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogRouteHandlerError(ex);
                    }
                });
                return;
            }

            List<WKRouteEntry> matches = new();
            lock (_routes)
            {
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    WKRouteEntry entry = _routes[i];
                    if (entry.MatchesUrl(request.Url, NavigationUrl.ContextBase(_page.Context)))
                    {
                        matches.Add(entry);
                    }
                }
            }

            List<WKRouteEntry> remaining = matches.Count > 1
                ? matches.GetRange(1, matches.Count - 1)
                : new List<WKRouteEntry>();
            WKRoute route = new(_session, _page, requestId, request, remaining, IsRouteActive, InvokeRouteAsync, MatchingRoutes);
            if (matches.Count > 0)
            {
                route.MarkInvoked(matches[0]);
            }

            if (NetworkRequestEvents.IsFaviconUrl(request.Url))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await route.AbortAsync("Failed").ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogRouteHandlerError(ex);
                    }
                });
                return;
            }

            if (matches.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await BindTimes(matches[0])(route).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogRouteHandlerError(ex);
                    }
                });
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await route.ContinueAsync(headers: LocaleAndAuthHeaders(request)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogRouteHandlerError(ex);
                }
            });
        }

        private IDictionary<string, string> LocaleAndAuthHeaders(WKRequest request)
        {
            if (request == null)
            {
                return null;
            }

            bool isWebSocket = LocaleAcceptLanguage.IsWebSocket(request.ResourceType)
                || IsWebSocketUrl(request.Url)
                || HasWebSocketUpgrade(request.Headers);
            IDictionary<string, string> localeHeaders = LocaleAcceptLanguage.Merge(
                request.Headers,
                _locale,
                isWebSocket);
            localeHeaders = MergeExtraHttpHeaders(localeHeaders ?? request.Headers);

            IDictionary<string, string> auth = HeadersWithAuth(request);
            if (auth == null)
            {
                return ReferenceEquals(localeHeaders, request.Headers) ? null : localeHeaders;
            }

            Dictionary<string, string> merged = localeHeaders != null
                ? new Dictionary<string, string>(localeHeaders, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in auth)
            {
                merged[pair.Key] = pair.Value;
            }

            return merged;
        }

        private bool HasExtraHttpHeaders()
        {
            Dictionary<string, string> merged = ExtraHttpHeaders.Merged(_page.Context, _page.PageExtraHttpHeaders);
            return merged != null && merged.Count > 0;
        }

        private IDictionary<string, string> MergeExtraHttpHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        {
            Dictionary<string, string> extra = ExtraHttpHeaders.Merged(_page.Context, _page.PageExtraHttpHeaders);
            if (extra == null || extra.Count == 0)
            {
                return headers as IDictionary<string, string>;
            }

            Dictionary<string, string> merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> pair in headers)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            foreach (KeyValuePair<string, string> pair in extra)
            {
                merged[pair.Key] = pair.Value;
            }

            return merged;
        }

        private IDictionary<string, string> HeadersWithAuth(WKRequest request)
        {
            if (request == null)
            {
                return null;
            }

            HttpCredentials picked = HttpBasicAuth.Pick(_httpCredentials, request.Url);
            if (!HttpBasicAuth.HasCredentials(picked))
            {
                return null;
            }

            foreach (KeyValuePair<string, string> pair in request.Headers)
            {
                if (!string.Equals(pair.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(pair.Value)
                    || pair.Value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return null;
            }

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HttpBasicAuth.ApplyTo(headers, picked, request.Url);
            return headers.Count == 0 ? null : headers;
        }

        private void RaiseRequestCreated(WKRequest request)
        {
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            if (_page.Opener != null && !_popupMainRequestEmitted)
            {
                request.FrameUnavailable = true;
                _popupMainRequestEmitted = true;
                _page.PopupMainRequestEmitted = true;
                if (request.Response == null)
                {
                    request.CompleteAsPopupNavigation();
                }
            }

            _page.OnRequestCreated(request);
        }

        private void RaiseResponseReceived(WKResponse response)
        {
            WKRequest request = response?.WKRequest;
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            WKWebSocket socket = FindWebSocket(request.RequestId, request.Url);
            if (socket != null)
            {
                socket.Har.ApplyHandshakeResponse(
                    response.Status,
                    response.StatusText,
                    response.Headers,
                    overwrite: false);
            }

            _page.OnResponseReceived(response);
        }

        private WKWebSocket FindWebSocket(string requestId, string url)
        {
            if (!string.IsNullOrEmpty(requestId) && _webSockets.TryGetValue(requestId, out WKWebSocket byId))
            {
                return byId;
            }

            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            foreach (WKWebSocket candidate in _webSockets.Values)
            {
                if (SameWebSocketUrl(candidate.Url, url))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RaiseRequestFinished(WKRequest request)
        {
            if (request != null && (_page.IsClosed || _page.IsClosing))
            {
                request.AbortClosed(new TargetClosedException(
                    DriverMessages.BrowserOrContextClosedExceptionMessage));
                return;
            }

            request?.MarkFinished();
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            _page.OnRequestFinished(request);
        }

        private void RaiseRequestFailed(WKRequest request)
        {
            if (request != null && (_page.IsClosed || _page.IsClosing))
            {
                request.AbortClosed(new TargetClosedException(
                    DriverMessages.BrowserOrContextClosedExceptionMessage));
                return;
            }

            request?.MarkFinished();
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            _page.OnRequestFailed(request);
        }

        private List<WKRouteEntry> MatchingRoutes(string url)
        {
            List<WKRouteEntry> matches = new();
            lock (_routes)
            {
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    WKRouteEntry entry = _routes[i];
                    if (entry.MatchesUrl(url, NavigationUrl.ContextBase(_page.Context)))
                    {
                        matches.Add(entry);
                    }
                }
            }

            return matches;
        }

        private bool IsRouteActive(WKRouteEntry entry)
        {
            lock (_routes)
            {
                return _routes.Contains(entry);
            }
        }

        private Task InvokeRouteAsync(WKRouteEntry entry, WKRoute route)
            => BindTimes(entry)(route);

        private Func<WKRoute, Task> BindTimes(WKRouteEntry entry)
        {
            return async route =>
            {
                TaskCompletionSource<bool> invocation = entry.Lifetime.Begin();
                Interlocked.Increment(ref _inFlightRouteHandlers);
                try
                {
                    if (entry.ConsumeAndShouldRemove())
                    {
                        lock (_routes)
                        {
                            _routes.Remove(entry);
                        }
                    }

                    await entry.Handler(route).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    if (!route.IsHandled)
                    {
                        try
                        {
                            await route.AbortAsync().ConfigureAwait(false);
                        }
                        catch (PlaywrightNativeException)
                        {
                        }
                    }

                    if (!entry.Lifetime.IgnoreErrors)
                    {
                        throw;
                    }
                }
                finally
                {
                    entry.Lifetime.End(invocation);
                    if (Interlocked.Decrement(ref _inFlightRouteHandlers) == 0)
                    {
                        _ = UpdateInterceptionAsync();
                    }
                }
            };
        }
    }
}
