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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Manages network request tracking for a Chromium page by listening to CDP
    /// <c>Network.*</c> events. Creates <see cref="CRRequest"/> and <see cref="CRResponse"/>
    /// instances and fires events on the owning <see cref="CRPage"/>.
    /// </summary>
    internal class CRNetworkManager
    {
        private readonly CRSession _session;
        private readonly CRPage _page;
        private readonly ConcurrentDictionary<string, CRRequest> _requestsById = new();
        private readonly ConcurrentDictionary<string, CRWebSocket> _webSockets = new();
        private readonly List<CRRouteEntry> _routes = new();
        private readonly ConcurrentDictionary<string, BufferedFetch> _networkIdToFetchRequestPaused = new();
        private readonly ConcurrentDictionary<string, BufferedWillBeSent> _pendingRequestWillBeSent = new();
        private readonly ConcurrentDictionary<string, CRRequest> _requestsByRawId = new();
        private readonly ConcurrentDictionary<string, byte> _handledFetchIds = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, IReadOnlyList<NameValueEntry>> _pendingExtraHeaders = new();
        private readonly ConcurrentDictionary<string, byte> _attemptedAuthentications = new();
        private readonly ConcurrentDictionary<string, byte> _servedFromCache = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, WorkerSessionState> _workerSessions = new(StringComparer.Ordinal);
        private readonly CRExtraInfoTracker _extraInfo = new();
        private bool _interceptingEnabled;
        private bool _handleAuthRequests;
        private bool _interceptAllRequests;
        private bool _webSocketInterceptingEnabled;
        private bool _popupMainRequestEmitted;
        private Proxy _proxy;
        private IReadOnlyList<HttpCredentials> _httpCredentials = Array.Empty<HttpCredentials>();
        private Dictionary<string, string> _extraHttpHeaders;
        private string _navigateReferrer;
        private string _locale;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRNetworkManager"/> class
        /// and subscribes to the CDP session's <see cref="CRSession.MessageReceived"/> event.
        /// </summary>
        /// <param name="session">The CDP session to listen on.</param>
        /// <param name="page">The owning <see cref="CRPage"/> to fire events on.</param>
        public CRNetworkManager(CRSession session, CRPage page)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _page = page ?? throw new ArgumentNullException(nameof(page));

            _session.MessageReceived += OnMessage;
        }

        /// <summary>
        /// Extra HTTP headers last applied via <see cref="ApplyExtraHttpHeadersAsync"/>.
        /// </summary>
        internal IReadOnlyDictionary<string, string> ExtraHttpHeaders => _extraHttpHeaders;

        /// <summary>
        /// Enables <c>Network</c> on a worker or OOPIF session and applies already-set
        /// extra HTTP headers. Worker fetches are attributed to <paramref name="workerFrame"/>.
        /// Official <c>addSession</c>: Fetch is enabled on OOPIF sessions, not workers.
        /// </summary>
        /// <param name="session">The worker or OOPIF CDP session.</param>
        /// <param name="workerFrame">The owner frame for requests that omit <c>frameId</c>.</param>
        /// <param name="isWorker"><see langword="true"/> for a dedicated worker session.</param>
        /// <param name="parentFrameId">CDP <c>targetInfo.parentFrameId</c> when the frame is not attached yet.</param>
        /// <returns>A task that completes when the session is attached.</returns>
        internal async Task AddWorkerSessionAsync(CRSession session, Frame workerFrame, bool isWorker = true, string parentFrameId = null)
        {
            if (session == null)
            {
                return;
            }

            WorkerSessionState state = new WorkerSessionState(session, workerFrame, isWorker)
            {
                ParentFrameId = parentFrameId ?? workerFrame?.FrameId,
            };
            state.Handler = (method, parameters) => OnSessionMessage(session, method, parameters);
            if (!_workerSessions.TryAdd(session.SessionId ?? string.Empty, state))
            {
                return;
            }

            session.MessageReceived += state.Handler;
            try
            {
                List<Task> tasks = new List<Task>
                {
                    session.SendAsync("Network.enable"),
                };
                if (_extraHttpHeaders != null && _extraHttpHeaders.Count > 0)
                {
                    tasks.Add(session.SendAsync("Network.setExtraHTTPHeaders", new { headers = _extraHttpHeaders }));
                }

                if (_interceptingEnabled)
                {
                    tasks.Add(SetCacheDisabledAsync(session, true));
                    if (!isWorker)
                    {
                        tasks.Add(EnableFetchBoundedAsync(session));
                    }
                }

                if (_webSocketInterceptingEnabled)
                {
                    tasks.Add(session.SendAsync(
                        "Network.setRequestInterception",
                        new
                        {
                            patterns = new object[]
                            {
                                new { urlPattern = "*", resourceType = "WebSocket" },
                                new { urlPattern = "ws://*/*" },
                                new { urlPattern = "wss://*/*" },
                            },
                        }));
                }

                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightNativeException)
            {
                RemoveWorkerSession(session);
            }
        }

        /// <summary>
        /// Official <c>_updateProtocolRequestInterceptionForSession</c> Fetch.enable
        /// for a non-worker extra session, started concurrently with resume.
        /// </summary>
        /// <param name="session">The OOPIF CDP session.</param>
        /// <returns>A task that completes when Fetch is enabled or skipped.</returns>
        internal async Task EnableFetchOnSessionIfNeededAsync(CRSession session)
        {
            if (session == null || session.IsClosed || !_interceptingEnabled)
            {
                return;
            }

            try
            {
                await EnableFetchBoundedAsync(session).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        /// <summary>
        /// Detaches a worker session from network tracking.
        /// </summary>
        /// <param name="session">The worker session.</param>
        internal void RemoveWorkerSession(CRSession session)
        {
            if (session == null)
            {
                return;
            }

            if (_workerSessions.TryRemove(session.SessionId ?? string.Empty, out WorkerSessionState state))
            {
                session.MessageReceived -= state.Handler;
            }
        }

        /// <summary>
        /// When an OOPIF document's <c>loadingFinished</c> is delivered on a session
        /// we did not subscribe to yet, finish the navigation request on lifecycle.
        /// </summary>
        /// <param name="frameId">The frame that reached DOMContentLoaded or load.</param>
        internal void FinishNavigationRequestsForFrame(string frameId)
        {
            if (string.IsNullOrEmpty(frameId))
            {
                return;
            }

            List<CRRequest> pending = new();
            foreach (CRRequest request in _requestsById.Values)
            {
                if (request.IsNavigationRequest
                    && request.Response != null
                    && !request.Finished
                    && request.Frame != null
                    && string.Equals(request.Frame.FrameId, frameId, StringComparison.Ordinal))
                {
                    pending.Add(request);
                }
            }

            foreach (CRRequest request in pending)
            {
                request.Finished = true;
                _requestsById.TryRemove(request.RequestId, out _);
                if (!string.IsNullOrEmpty(request.ProtocolRequestId))
                {
                    ForgetRawRequest(request.ProtocolRequestId, request);
                }

                RaiseRequestFinished(request);
                request.Frame?.OnInflightRequestFinished(request.RequestId);
                _extraInfo.Finished(request.RequestId);
            }
        }

        /// <summary>
        /// Official OOPIF document bodies live on the child session. Try the
        /// current session, then every extra Network session.
        /// </summary>
        /// <param name="request">The request whose body is needed.</param>
        /// <returns>The protocol body, or an empty array.</returns>
        internal async Task<byte[]> GetProtocolBodyAsync(CRRequest request)
        {
            if (request == null)
            {
                return Array.Empty<byte>();
            }

            string requestId = request.ProtocolRequestId ?? request.RequestId;
            List<CRSession> sessions = new();
            if (request.NetworkSession != null)
            {
                sessions.Add(request.NetworkSession);
            }

            sessions.Add(_session);
            foreach (WorkerSessionState state in _workerSessions.Values)
            {
                if (!state.IsWorker && state.Session != null)
                {
                    sessions.Add(state.Session);
                }
            }

            HashSet<CRSession> seen = new();
            foreach (CRSession session in sessions)
            {
                if (session == null || session.IsClosed || !seen.Add(session))
                {
                    continue;
                }

                try
                {
                    JsonElement? result = await session.SendAsync("Network.getResponseBody", new { requestId }).ConfigureAwait(false);
                    byte[] bytes = ResponseContent.DecodeProtocolBody(result);
                    if (bytes.Length > 0)
                    {
                        request.NetworkSession = session;
                        return bytes;
                    }
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Stores the referrer last passed to <c>Page.navigate</c> so intercepted
        /// Chromium headers can expose the official <c>url, url</c> concatenation.
        /// </summary>
        /// <param name="referrer">The navigate referrer, or <see langword="null"/>.</param>
        internal void RememberNavigateReferrer(string referrer) => _navigateReferrer = referrer;

        /// <summary>
        /// Stores extra HTTP headers and applies them to every worker session.
        /// The page session is updated by the caller.
        /// </summary>
        /// <param name="headers">Header map from <see cref="ExtraHttpHeaders"/>.</param>
        /// <returns>A task that completes when worker sessions are updated.</returns>
        internal async Task ApplyExtraHttpHeadersAsync(Dictionary<string, string> headers)
        {
            _extraHttpHeaders = headers;
            if (headers == null || headers.Count == 0)
            {
                return;
            }

            foreach (WorkerSessionState state in _workerSessions.Values)
            {
                if (state.Session.IsClosed)
                {
                    continue;
                }

                try
                {
                    await state.Session.SendAsync("Network.setExtraHTTPHeaders", new { headers }).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        /// <summary>
        /// Stores the context proxy so <c>Fetch.authRequired</c> can supply credentials.
        /// </summary>
        /// <param name="proxy">The context proxy, or <see langword="null"/>.</param>
        internal void SetProxy(Proxy proxy) => _proxy = proxy;

        /// <summary>
        /// Rejects inflight request waiters when the page closes.
        /// </summary>
        internal void AbortInflightClosed()
        {
            TargetClosedException error = new TargetClosedException(
                DriverMessages.BrowserOrContextClosedExceptionMessage);
            foreach (KeyValuePair<string, CRRequest> pair in _requestsById)
            {
                pair.Value.AbortClosed(error);
            }
        }

        /// <summary>
        /// Drops inflight tracking for every request owned by <paramref name="frame"/>
        /// (or targeting its URL) so a detached iframe cannot keep <c>networkidle</c>
        /// waiting forever.
        /// </summary>
        /// <param name="frame">The frame that is about to detach.</param>
        internal void FinishInflightForDetachedFrame(Frame frame)
        {
            if (frame == null)
            {
                return;
            }

            foreach (KeyValuePair<string, CRRequest> pair in _requestsById)
            {
                CRRequest request = pair.Value;
                bool matchesFrame = request.Frame == frame;
                bool matchesUrl = !string.IsNullOrEmpty(frame.Url)
                    && string.Equals(request.Url, frame.Url, StringComparison.Ordinal);
                if (!matchesFrame && !matchesUrl)
                {
                    continue;
                }

                request.Frame?.OnInflightRequestFinished(pair.Key);
                _page.MainFrame?.OnInflightRequestFinished(pair.Key);
            }

            frame.ClearInflightForDetach();
        }

        /// <summary>
        /// Stores page HTTP credentials so <c>Fetch.authRequired</c> can answer server challenges.
        /// </summary>
        /// <param name="httpCredentials">Credentials, or <see langword="null"/> to clear them.</param>
        internal void SetHttpCredentials(HttpCredentials httpCredentials)
            => SetHttpCredentials(HttpBasicAuth.Snapshot(httpCredentials));

        internal void SetHttpCredentials(IReadOnlyList<HttpCredentials> httpCredentials)
            => _httpCredentials = HttpBasicAuth.Snapshot(httpCredentials);

        /// <summary>
        /// Stores the context locale so WebSocket handshakes can send
        /// <c>Accept-Language</c> (Chromium before 151 ignores emulation).
        /// </summary>
        /// <param name="locale">BCP 47 locale, or <see langword="null"/>.</param>
        internal void SetLocale(string locale) => _locale = locale;

        /// <summary>
        /// Registers a route handler. When Fetch interception is enabled and a request URL
        /// matches the entry, the handler is invoked with a <see cref="CRRoute"/>.
        /// </summary>
        /// <param name="entry">The route registration.</param>
        internal void AddRoute(CRRouteEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (_routes)
            {
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
        internal List<CRRouteEntry> ClearRoutes(bool? contextRoute = null)
        {
            lock (_routes)
            {
                if (!contextRoute.HasValue)
                {
                    List<CRRouteEntry> all = new(_routes);
                    _routes.Clear();
                    return all;
                }

                List<CRRouteEntry> removed = new();
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
        /// Removes route registrations that match the given matcher and optional handler.
        /// </summary>
        /// <param name="urlString">Glob used at registration, or <see langword="null"/>.</param>
        /// <param name="urlRegex">Regex used at registration, or <see langword="null"/>.</param>
        /// <param name="urlFunc">Predicate used at registration, or <see langword="null"/>.</param>
        /// <param name="handlerIdentity">Handler to remove, or <see langword="null"/> for all matching matchers.</param>
        /// <param name="contextRoute">
        /// When set, only context-level (<see langword="true"/>) or page-level
        /// (<see langword="false"/>) routes are removed.
        /// </param>
        /// <returns>The registrations that were removed.</returns>
        internal List<CRRouteEntry> RemoveRoute(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            object handlerIdentity,
            bool? contextRoute = null)
        {
            lock (_routes)
            {
                List<CRRouteEntry> removed = new();
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    CRRouteEntry entry = _routes[i];
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
        internal void RemoveEntry(CRRouteEntry entry)
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
        /// Enables or disables the CDP <c>Fetch</c> domain based on whether any route handlers
        /// are registered. When routes are present and interception is not yet enabled,
        /// <c>Fetch.enable</c> is sent and the disk cache is disabled (Playwright
        /// <c>Network.setCacheDisabled</c>). When all routes are removed, <c>Fetch.disable</c>
        /// is sent and caching is restored.
        /// </summary>
        /// <returns>A task that completes when the CDP command is acknowledged.</returns>
        internal async Task UpdateInterceptionAsync()
        {
            int routeCount;
            lock (_routes)
            {
                routeCount = _routes.Count;
            }

            bool handleAuth = ProxySettings.HasCredentials(_proxy)
                || HttpBasicAuth.HasCredentials(_httpCredentials);
            bool interceptAll = routeCount > 0 || handleAuth;
            bool needFetch = interceptAll;
            if (needFetch && (!_interceptingEnabled || _handleAuthRequests != handleAuth || _interceptAllRequests != interceptAll))
            {
                _handleAuthRequests = handleAuth;
                _interceptAllRequests = interceptAll;
                await EnableFetchAsync(_session).ConfigureAwait(false);
                _interceptingEnabled = true;
                await SetCacheDisabledAsync(_session, true).ConfigureAwait(false);
                foreach (WorkerSessionState worker in _workerSessions.Values)
                {
                    if (worker.Session.IsClosed)
                    {
                        continue;
                    }

                    try
                    {
                        await SetCacheDisabledAsync(worker.Session, true).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        if (!worker.IsWorker)
                        {
                            await EnableFetchBoundedAsync(worker.Session).ConfigureAwait(false);
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            }
            else if (!needFetch && _interceptingEnabled)
            {
                _interceptingEnabled = false;
                _handleAuthRequests = false;
                _interceptAllRequests = false;
                try
                {
                    await _session.SendAsync("Fetch.disable").WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    await SetCacheDisabledAsync(_session, false).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
                catch (PlaywrightNativeException)
                {
                }

                foreach (WorkerSessionState worker in _workerSessions.Values)
                {
                    if (worker.Session.IsClosed)
                    {
                        continue;
                    }

                    try
                    {
                        if (!worker.IsWorker)
                        {
                            await worker.Session.SendAsync("Fetch.disable").WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        }

                        await SetCacheDisabledAsync(worker.Session, false).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            }
        }

        private static IDictionary<string, string> ParseHeaders(JsonElement payload)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);

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

        private static IDictionary<string, string> ParseResponseHeaders(JsonElement payload)
        {
            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

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

        private static IReadOnlyList<NameValueEntry> ParseExtraHeaders(JsonElement payload)
        {
            if (payload.TryGetProperty("headersText", out JsonElement textElement))
            {
                IReadOnlyList<NameValueEntry> fromText = ResponseHeaders.ParseHeadersText(textElement.GetString());
                if (fromText.Count > 0)
                {
                    return fromText;
                }
            }

            return ResponseHeaders.FromMap(ParseResponseHeaders(payload));
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement prop)
                ? prop.GetString()
                : null;
        }

        private static bool IsAboutBlankUrl(string url)
        {
            return !string.IsNullOrEmpty(url)
                && url.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWebSocketUrl(string url)
            => !string.IsNullOrEmpty(url)
                && (url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase));

        private static bool GetBool(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out JsonElement prop)
                && prop.ValueKind == JsonValueKind.True;

        private static int GetInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement prop)
                && prop.TryGetInt32(out int value)
                ? value
                : 0;
        }

        private static void LogRouteHandlerError(Exception ex)
        {
            System.Console.Error.WriteLine($"[CRNetworkManager] Route handler error: {ex}");
        }

        private static object CreateCredentialResponse(string username, string password, bool hasCredentials)
        {
            if (!hasCredentials)
            {
                return new { response = "CancelAuth" };
            }

            return new
            {
                response = "ProvideCredentials",
                username,
                password = password ?? string.Empty,
            };
        }

        private Task EnableFetchAsync(CRSession session)
        {
            int routeCount;
            lock (_routes)
            {
                routeCount = _routes.Count;
            }

            bool interceptAll = routeCount > 0 || _handleAuthRequests;
            object patterns = interceptAll
                ? new[] { new { urlPattern = "*" } }
                : new[] { new { urlPattern = "*", resourceType = "WebSocket" } };
            return session.SendAsync("Fetch.enable", new
            {
                handleAuthRequests = _handleAuthRequests,
                patterns,
            });
        }

        private async Task EnableFetchBoundedAsync(CRSession session)
        {
            try
            {
                await EnableFetchAsync(session).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private async Task UpdateWebSocketInterceptionAsync()
        {
            bool needWs = !string.IsNullOrEmpty(_locale);
            if (needWs == _webSocketInterceptingEnabled)
            {
                return;
            }

            object patterns = needWs
                ? new object[]
                {
                    new { urlPattern = "*", resourceType = "WebSocket" },
                    new { urlPattern = "ws://*/*" },
                    new { urlPattern = "wss://*/*" },
                }
                : Array.Empty<object>();
            try
            {
                await _session.SendAsync("Network.setRequestInterception", new { patterns }).ConfigureAwait(false);
                foreach (WorkerSessionState worker in _workerSessions.Values)
                {
                    try
                    {
                        await worker.Session.SendAsync("Network.setRequestInterception", new { patterns }).ConfigureAwait(false);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }

                _webSocketInterceptingEnabled = needWs;
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private Task SetCacheDisabledAsync(CRSession session, bool disabled)
            => session.SendAsync("Network.setCacheDisabled", new { cacheDisabled = disabled });

        private void OnMessage(string method, JsonElement? parameters)
            => OnSessionMessage(_session, method, parameters);

        private void OnSessionMessage(CRSession session, string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "Network.requestWillBeSent":
                    OnRequestWillBeSent(parameters, session);
                    break;
                case "Network.requestWillBeSentExtraInfo":
                    OnRequestWillBeSentExtraInfo(parameters, session);
                    break;
                case "Network.responseReceived":
                    OnResponseReceived(parameters, session);
                    break;
                case "Network.responseReceivedExtraInfo":
                    OnResponseReceivedExtraInfo(parameters, session);
                    break;
                case "Network.loadingFinished":
                    OnLoadingFinished(parameters, session);
                    break;
                case "Network.requestServedFromCache":
                    OnRequestServedFromCache(parameters, session);
                    break;
                case "Network.loadingFailed":
                    OnLoadingFailed(parameters, session);
                    break;
                case "Network.requestIntercepted":
                    OnNetworkRequestIntercepted(parameters, session);
                    break;
                case "Fetch.requestPaused":
                    OnFetchRequestPaused(parameters, session);
                    break;
                case "Fetch.authRequired":
                    OnFetchAuthRequired(parameters, session);
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

            CRWebSocket socket = new(requestId, url, _page.PublicPage);
            if (!_webSockets.TryAdd(requestId, socket))
            {
                return;
            }

            if (_requestsById.TryGetValue(requestId, out CRRequest existing))
            {
                socket.Har.ApplyHandshakeRequest(
                    existing.Headers,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    0);
                existing.Frame?.OnInflightRequestFinished(requestId);
            }

            _page.OnWebSocketCreated(socket);
        }

        private void OnWebSocketHandshakeRequest(JsonElement? parameters)
        {
            string requestId = WebSocketProtocol.ReadRequestId(parameters);
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out CRWebSocket socket))
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
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out CRWebSocket socket))
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
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryRemove(requestId, out CRWebSocket socket))
            {
                return;
            }

            socket.NotifyClosed();
        }

        private void OnWebSocketFrame(JsonElement? parameters, bool sent)
        {
            string requestId = WebSocketProtocol.ReadRequestId(parameters);
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out CRWebSocket socket))
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
            if (string.IsNullOrEmpty(requestId) || !_webSockets.TryGetValue(requestId, out CRWebSocket socket))
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

        private void OnRequestWillBeSent(JsonElement? parameters, CRSession session, bool force = false)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string rawId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(rawId))
            {
                return;
            }

            string requestId = RequestKey(session, rawId);
            string holdType = GetString(p, "type");
            if (!force
                && (string.Equals(holdType, "Fetch", StringComparison.Ordinal) || string.Equals(holdType, "XHR", StringComparison.Ordinal))
                && !_networkIdToFetchRequestPaused.ContainsKey(rawId)
                && !_networkIdToFetchRequestPaused.ContainsKey(requestId)
                && HasUserRoutes())
            {
                _pendingRequestWillBeSent[rawId] = new BufferedWillBeSent(session, p);
                return;
            }

            string frameId = GetString(p, "frameId");
            Frame frame = ResolveFrame(session, frameId);

            // Handle redirect: if redirectResponse is present, the existing request was redirected.
            CRRequest redirectedFrom = null;
            if (p.TryGetProperty("redirectResponse", out JsonElement redirectResponse)
                && redirectResponse.ValueKind == JsonValueKind.Object)
            {
                if (_requestsById.TryRemove(requestId, out CRRequest existingRequest))
                {
                    ForgetRawRequest(rawId, existingRequest);

                    // Create a response for the redirected request.
                    string redirectUrl = GetString(redirectResponse, "url");
                    int redirectStatus = GetInt(redirectResponse, "status");
                    string redirectStatusText = GetString(redirectResponse, "statusText");
                    IDictionary<string, string> redirectHeaders = ParseResponseHeaders(redirectResponse);

                    if (redirectResponse.TryGetProperty("timing", out JsonElement redirectTiming)
                        && !existingRequest.ServedFromCache)
                    {
                        existingRequest.TimingRequestTime = ResourceTimingParser.ApplyResourceTiming(
                            existingRequest.Timing,
                            redirectTiming);
                    }

                    double redirectFinished = ResourceTimingParser.ReadDouble(p, "timestamp");
                    ResourceTimingParser.ApplyRequestFinished(
                        existingRequest.Timing,
                        existingRequest.TimestampSeconds,
                        redirectFinished);

                    CRResponse redirectResponseObj = new(
                        session ?? _session,
                        existingRequest,
                        redirectUrl,
                        redirectStatus,
                        redirectStatusText,
                        redirectHeaders,
                        ResponseNetworkInfo.ParseServerAddr(redirectResponse),
                        ResponseNetworkInfo.ParseSecurityDetails(redirectResponse),
                        ResponseNetworkInfo.ParseFromServiceWorker(redirectResponse),
                        ResponseNetworkInfo.ParseHttpVersion(redirectResponse));

                    _extraInfo.ResponseCreated(requestId, redirectResponseObj);
                    RaiseResponseReceived(redirectResponseObj);
                    RaiseRequestFinished(existingRequest);
                    existingRequest.Frame?.OnInflightRequestFinished(requestId);

                    redirectedFrom = existingRequest;
                }
            }

            // Parse the request payload.
            if (!p.TryGetProperty("request", out JsonElement requestPayload))
            {
                return;
            }

            string url = GetString(requestPayload, "url");
            string method = GetString(requestPayload, "method");
            byte[] postDataBuffer = RequestPostData.FromProtocol(requestPayload);
            string postData = RequestPostData.ToUtf8String(postDataBuffer);
            IDictionary<string, string> headers = ParseHeaders(requestPayload);

            string type = GetString(p, "type");
            if (p.TryGetProperty("initiator", out JsonElement initiator)
                && string.Equals(GetString(initiator, "type"), "preflight", StringComparison.OrdinalIgnoreCase))
            {
                type = "preflight";
            }

            bool isNavigationRequest = NetworkRequestEvents.IsDocumentNavigation(type);

            CRRequest request = new(
                requestId,
                url,
                method,
                headers,
                postData,
                type ?? string.Empty,
                isNavigationRequest,
                frame,
                redirectedFrom,
                postDataBuffer);
            request.ProtocolRequestId = rawId;
            request.NetworkSession = session ?? _session;
            request.FetchProtocolBody = () => GetProtocolBodyAsync(request);

            _extraInfo.RequestCreated(requestId, request);

            string loaderId = GetString(p, "loaderId");
            request.DocumentId = !string.IsNullOrEmpty(loaderId) ? loaderId : frame?.DocumentId;
            request.DocumentUrl = isNavigationRequest ? url : frame?.Url;
            request.TimestampSeconds = ResourceTimingParser.ReadDouble(p, "timestamp");
            double wallTime = ResourceTimingParser.ReadDouble(p, "wallTime");
            if (wallTime <= 0)
            {
                wallTime = request.TimestampSeconds;
            }

            ResourceTimingParser.ApplyWallTime(request.Timing, wallTime);
            if (_servedFromCache.TryRemove(requestId, out _) || _servedFromCache.TryRemove(rawId, out _))
            {
                request.ServedFromCache = true;
            }

            _requestsById[requestId] = request;
            _requestsByRawId[rawId] = request;
            RaiseRequestCreated(request);
            frame?.OnInflightRequestStarted(
                requestId,
                NetworkIdleRules.IsExcluded(request.Url, request.ResourceType));

            // Fetch.requestPaused for worker fetches arrives on the page session
            // (Chrome 130+), while Network.requestWillBeSent arrives on the worker
            // session. Correlate by the raw networkId, and continue/fulfill on the
            // Fetch session that paused the request.
            if (_networkIdToFetchRequestPaused.TryRemove(rawId, out BufferedFetch buffered)
                || _networkIdToFetchRequestPaused.TryRemove(requestId, out buffered))
            {
                string interceptionId = GetString(buffered.Parameters, "requestId");
                if (!string.IsNullOrEmpty(interceptionId)
                    && _handledFetchIds.TryAdd(interceptionId, 0))
                {
                    ApplyPausedRequestDetails(request, buffered.Parameters);
                    OnInterceptedRequest(interceptionId, request, buffered.Session ?? session);
                }
            }
        }

        private void OnResponseReceived(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string rawId = GetString(p, "requestId");
            string requestId = RequestKey(session, rawId);
            if (string.IsNullOrEmpty(rawId))
            {
                return;
            }

            if (!_requestsById.TryGetValue(requestId, out CRRequest request)
                && !_requestsByRawId.TryGetValue(rawId, out request))
            {
                JsonElement swResponse = default;
                string responseUrl = p.TryGetProperty("response", out swResponse)
                    ? GetString(swResponse, "url")
                    : null;
                if (swResponse.ValueKind == JsonValueKind.Object
                    && ResponseNetworkInfo.ParseFromServiceWorker(swResponse)
                    && TryReleaseHeldFetch(rawId, responseUrl))
                {
                    if (!_requestsById.TryGetValue(requestId, out request)
                        && !_requestsByRawId.TryGetValue(rawId, out request))
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            if (!p.TryGetProperty("response", out JsonElement responsePayload))
            {
                return;
            }

            string url = GetString(responsePayload, "url");
            int status = GetInt(responsePayload, "status");
            string statusText = GetString(responsePayload, "statusText");
            IDictionary<string, string> headers = ParseResponseHeaders(responsePayload);

            if (GetBool(responsePayload, "fromDiskCache")
                || GetBool(responsePayload, "fromPrefetchCache"))
            {
                request.ServedFromCache = true;
            }

            if (responsePayload.TryGetProperty("timing", out JsonElement resourceTiming)
                && !request.ServedFromCache)
            {
                request.TimingRequestTime = ResourceTimingParser.ApplyResourceTiming(request.Timing, resourceTiming);
            }

            CRResponse response = new(
                session ?? _session,
                request,
                url,
                status,
                statusText,
                headers,
                ResponseNetworkInfo.ParseServerAddr(responsePayload),
                ResponseNetworkInfo.ParseSecurityDetails(responsePayload),
                ResponseNetworkInfo.ParseFromServiceWorker(responsePayload),
                ResponseNetworkInfo.ParseHttpVersion(responsePayload));

            if (_pendingExtraHeaders.TryRemove(requestId, out IReadOnlyList<NameValueEntry> extra))
            {
                response.ApplyExtraHeaders(extra);
            }

            _extraInfo.ResponseCreated(requestId, response);
            MaybeUpdateRequestSession(session, request);
            RaiseResponseReceived(response);
        }

        private void OnRequestWillBeSentExtraInfo(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string rawId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(rawId))
            {
                return;
            }

            string requestId = RequestKey(session, rawId);
            _extraInfo.RequestExtraInfo(requestId, p);
        }

        private void OnResponseReceivedExtraInfo(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string rawId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(rawId))
            {
                return;
            }

            string requestId = RequestKey(session, rawId);
            IReadOnlyList<NameValueEntry> pairs = ParseExtraHeaders(p);
            if (_requestsById.TryGetValue(requestId, out CRRequest request) && request.Response != null)
            {
                request.Response.ApplyExtraHeaders(pairs);
            }
            else if (pairs.Count > 0)
            {
                _pendingExtraHeaders[requestId] = pairs;
            }

            _extraInfo.ResponseExtraInfo(requestId, p);
        }

        private void OnLoadingFinished(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string rawId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(rawId))
            {
                return;
            }

            if (TryTakeRequest(session, rawId, out CRRequest request))
            {
                MaybeUpdateRequestSession(session, request);
                ResourceTimingParser.ApplyRequestFinished(
                    request.Timing,
                    request.TimestampSeconds,
                    ResourceTimingParser.ReadDouble(p, "timestamp"));
                request.EncodedDataLength = (int)ResourceTimingParser.ReadDouble(p, "encodedDataLength");
                request.Finished = true;

                RaiseRequestFinished(request);
                request.Frame?.OnInflightRequestFinished(request.RequestId);
                _extraInfo.Finished(request.RequestId);
            }
        }

        private void OnRequestServedFromCache(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string rawId = GetString(parameters.Value, "requestId");
            if (string.IsNullOrEmpty(rawId))
            {
                return;
            }

            string requestId = RequestKey(session, rawId);
            if (_requestsById.TryGetValue(requestId, out CRRequest request)
                || _requestsByRawId.TryGetValue(rawId, out request))
            {
                request.ServedFromCache = true;
            }
            else
            {
                _servedFromCache[requestId] = 0;
                _servedFromCache[rawId] = 0;
            }
        }

        private void OnLoadingFailed(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            string rawId = GetString(p, "requestId");
            if (string.IsNullOrEmpty(rawId))
            {
                return;
            }

            string requestId = RequestKey(session, rawId);

            if (TryTakeRequest(session, rawId, out CRRequest request))
            {
                MaybeUpdateRequestSession(session, request);
                string errorText = GetString(p, "errorText");
                string blockedReason = GetString(p, "blockedReason");
                if (string.Equals(blockedReason, "mixed-content", StringComparison.OrdinalIgnoreCase))
                {
                    errorText = "mixed-content";
                }

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

                request.Frame?.OnInflightRequestFinished(requestId);
                _extraInfo.Finished(requestId);
            }

            if ((_webSockets.TryGetValue(rawId, out CRWebSocket socket)
                || _webSockets.TryGetValue(requestId, out socket))
                && !string.IsNullOrEmpty(GetString(p, "errorText")))
            {
                socket.NotifyError(GetString(p, "errorText"));
            }
        }

        private void OnFetchAuthRequired(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string requestId = GetString(payload, "requestId");
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            CRSession fetchSession = session ?? _session;

            string source = null;
            if (payload.TryGetProperty("authChallenge", out JsonElement challenge))
            {
                source = GetString(challenge, "source");
            }

            string requestUrl = null;
            if (payload.TryGetProperty("request", out JsonElement request))
            {
                requestUrl = GetString(request, "url");
            }

            object authChallengeResponse = BuildAuthChallengeResponse(requestId, source, requestUrl);
            _ = Task.Run(async () =>
            {
                try
                {
                    await fetchSession.SendAsync("Fetch.continueWithAuth", new
                    {
                        requestId,
                        authChallengeResponse,
                    }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogRouteHandlerError(ex);
                }
            });
        }

        private object BuildAuthChallengeResponse(string requestId, string source, string requestUrl)
        {
            if (!_attemptedAuthentications.TryAdd(requestId, 0))
            {
                return new { response = "CancelAuth" };
            }

            bool isProxy = string.Equals(source, "Proxy", StringComparison.OrdinalIgnoreCase);
            if (isProxy)
            {
                return CreateCredentialResponse(_proxy?.Username, _proxy?.Password, ProxySettings.HasCredentials(_proxy));
            }

            HttpCredentials picked = HttpBasicAuth.Pick(_httpCredentials, requestUrl);
            bool hasServerCredentials = HttpBasicAuth.HasCredentials(picked);
            bool isServer = string.Equals(source, "Server", StringComparison.OrdinalIgnoreCase);
            if (isServer || hasServerCredentials)
            {
                return CreateCredentialResponse(
                    picked?.Username,
                    picked?.Password,
                    hasServerCredentials);
            }

            return CreateCredentialResponse(_proxy?.Username, _proxy?.Password, ProxySettings.HasCredentials(_proxy));
        }

        private void OnNetworkRequestIntercepted(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;
            string interceptionId = GetString(p, "interceptionId");
            if (string.IsNullOrEmpty(interceptionId))
            {
                return;
            }

            IDictionary<string, string> headers = null;
            string url = string.Empty;
            string resourceType = GetString(p, "resourceType") ?? string.Empty;
            if (p.TryGetProperty("request", out JsonElement requestEl)
                && requestEl.ValueKind == JsonValueKind.Object)
            {
                url = GetString(requestEl, "url") ?? string.Empty;
                headers = ParseHeaders(requestEl);
            }

            bool isWebSocket = LocaleAcceptLanguage.IsWebSocket(resourceType) || IsWebSocketUrl(url);
            IDictionary<string, string> merged = LocaleAcceptLanguage.Merge(headers, _locale, isWebSocket);
            CRSession continueSession = session ?? _session;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (merged != null && !ReferenceEquals(merged, headers))
                    {
                        await continueSession.SendAsync(
                            "Network.continueInterceptedRequest",
                            new { interceptionId, headers = merged }).ConfigureAwait(false);
                    }
                    else
                    {
                        await continueSession.SendAsync(
                            "Network.continueInterceptedRequest",
                            new { interceptionId }).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    LogRouteHandlerError(ex);
                }
            });
        }

        private void OnFetchRequestPaused(JsonElement? parameters, CRSession session)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement p = parameters.Value;

            // The Fetch interception ID (used for Fetch.continueRequest / fulfillRequest / failRequest).
            string interceptionId = GetString(p, "requestId");

            // The network-layer ID that correlates with Network.requestWillBeSent.
            string networkId = GetString(p, "networkId");
            if (string.IsNullOrEmpty(interceptionId))
            {
                return;
            }

            string pausedUrl = p.TryGetProperty("request", out JsonElement pausedRequest)
                ? GetString(pausedRequest, "url")
                : null;
            if (TryReleaseHeldFetch(networkId, pausedUrl, session, p))
            {
                return;
            }

            if (!_handledFetchIds.TryAdd(interceptionId, 0))
            {
                return;
            }

            if (!string.IsNullOrEmpty(networkId) && TryGetRequestByNetworkId(session, networkId, out CRRequest request))
            {
                ApplyPausedRequestDetails(request, p);
                OnInterceptedRequest(interceptionId, request, session);
                return;
            }

            string url = string.Empty;
            string method = "GET";
            IDictionary<string, string> headers = new Dictionary<string, string>();
            byte[] postDataBuffer = null;

            if (p.TryGetProperty("request", out JsonElement fetchReq))
            {
                url = GetString(fetchReq, "url") ?? string.Empty;
                method = GetString(fetchReq, "method") ?? "GET";
                headers = ParseHeaders(fetchReq);
                postDataBuffer = RequestPostData.FromProtocol(fetchReq);
            }

            string resourceType = GetString(p, "resourceType") ?? string.Empty;

            // Blob/data worker scripts and Fetch events that omit networkId never
            // get a matching Network.requestWillBeSent. Intercept immediately.
            // WebSocket handshakes must continue before Chrome sends en-US.
            // Other requests wait so navigation type and headers stay accurate.
            int routeCount;
            lock (_routes)
            {
                routeCount = _routes.Count;
            }

            bool localeOnly = !string.IsNullOrEmpty(_locale)
                && routeCount == 0
                && !_handleAuthRequests;
            bool waitForNetwork = !localeOnly
                && !string.IsNullOrEmpty(networkId)
                && !url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                && !IsAboutBlankUrl(url)
                && !IsWebSocketUrl(url)
                && !LocaleAcceptLanguage.IsWebSocket(resourceType);
            if (waitForNetwork)
            {
                _handledFetchIds.TryRemove(interceptionId, out _);
                _networkIdToFetchRequestPaused[networkId] = new BufferedFetch(session, p);
                return;
            }

            CRRequest fetchRequest = new(
                interceptionId,
                url,
                method,
                headers,
                postData: RequestPostData.ToUtf8String(postDataBuffer),
                resourceType: resourceType,
                isNavigationRequest: false,
                frame: ResolveFrame(session, GetString(p, "frameId")),
                redirectedFrom: null,
                postDataBuffer: postDataBuffer);

            ApplyPausedRequestDetails(fetchRequest, p);
            OnInterceptedRequest(interceptionId, fetchRequest, session);
        }

        private void ApplyPausedRequestDetails(CRRequest request, JsonElement paused)
        {
            if (!paused.TryGetProperty("request", out JsonElement pausedRequest))
            {
                return;
            }

            request.UpdatePostData(RequestPostData.FromProtocol(pausedRequest));
            IReadOnlyList<NameValueEntry> pausedHeaders = RawNetworkHeaders.FromObject(
                pausedRequest.TryGetProperty("headers", out JsonElement pausedHeadersEl)
                    ? pausedHeadersEl
                    : default,
                "\n");
            request.ApplyInterceptedHeaders(ParseHeaders(pausedRequest), EffectiveExtraHeaders());
            ApplyChromiumRefererConcatenation(request);
            if (pausedHeaders.Count > 0)
            {
                request.SetRawRequestHeaders(pausedHeaders);
            }
        }

        private void ApplyChromiumRefererConcatenation(CRRequest request)
        {
            string extraReferer = HeaderMap.Value(EffectiveExtraHeaders(), "referer");
            string current = HeaderMap.Value(request.Headers, "referer");
            if (string.IsNullOrEmpty(extraReferer) && !string.IsNullOrEmpty(_navigateReferrer))
            {
                extraReferer = _navigateReferrer;
            }

            if (string.IsNullOrEmpty(extraReferer) && _extraHttpHeaders != null && _extraHttpHeaders.Count > 0)
            {
                extraReferer = current;
            }

            if (string.IsNullOrEmpty(extraReferer))
            {
                return;
            }

            if (!string.IsNullOrEmpty(current) && current.Contains(',', StringComparison.Ordinal))
            {
                return;
            }

            HeaderMap.Set(request.Headers, "referer", extraReferer + ", " + extraReferer);
        }

        private IReadOnlyDictionary<string, string> EffectiveExtraHeaders()
        {
            if (_extraHttpHeaders != null && _extraHttpHeaders.Count > 0)
            {
                return _extraHttpHeaders;
            }

            if (string.IsNullOrEmpty(_navigateReferrer))
            {
                return _extraHttpHeaders;
            }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["referer"] = _navigateReferrer,
            };
        }

        private void OnInterceptedRequest(string interceptionId, CRRequest request, CRSession session)
        {
            request.ApplyInterceptedHeaders(request.Headers, EffectiveExtraHeaders());
            ApplyChromiumRefererConcatenation(request);
            request.SetRawRequestHeaders(HeaderMap.Array(request.Headers));

            if (request.RedirectedFrom != null)
            {
                CRRoute redirectRoute = new(session ?? _session, interceptionId, request);
                IDictionary<string, string> continuedHeaders = request.RedirectedFrom.ContinuedHeaders;
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

            List<CRRouteEntry> matches = new();
            lock (_routes)
            {
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    CRRouteEntry entry = _routes[i];
                    if (entry.MatchesUrl(request.Url, NavigationUrl.ContextBase(_page.PublicPage?.Context)))
                    {
                        matches.Add(entry);
                    }
                }
            }

            List<CRRouteEntry> remaining = matches.Count > 1
                ? matches.GetRange(1, matches.Count - 1)
                : new List<CRRouteEntry>();
            CRRoute route = new(
                session ?? _session,
                interceptionId,
                request,
                remaining,
                IsRouteActive,
                InvokeRouteAsync,
                MatchingRoutes,
                NavigationUrl.ContextBase(_page.PublicPage?.Context));
            if (matches.Count > 0)
            {
                route.MarkInvoked(matches[0]);
            }

            if (NetworkRequestEvents.IsFaviconUrl(request.Url)
                || NetworkRequestEvents.IsPreflight(request.Method, request.ResourceType))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (NetworkRequestEvents.IsPreflight(request.Method, request.ResourceType)
                            && matches.Count > 0)
                        {
                            Dictionary<string, string> preflightHeaders = new(StringComparer.OrdinalIgnoreCase)
                            {
                                ["Access-Control-Allow-Origin"] = HeaderMap.Value(request.Headers, "origin") ?? "*",
                                ["Access-Control-Allow-Credentials"] = "true",
                                ["Access-Control-Allow-Methods"] = HeaderMap.Value(request.Headers, "access-control-request-method")
                                    ?? "GET, POST, PUT, OPTIONS, HEAD, DELETE, PATCH",
                            };
                            string requestHeaders = HeaderMap.Value(request.Headers, "access-control-request-headers");
                            if (!string.IsNullOrEmpty(requestHeaders))
                            {
                                preflightHeaders["Access-Control-Allow-Headers"] = requestHeaders;
                            }

                            await route.FulfillAsync(204, headers: preflightHeaders).ConfigureAwait(false);
                        }
                        else if (NetworkRequestEvents.IsFaviconUrl(request.Url))
                        {
                            await route.AbortAsync("Failed").ConfigureAwait(false);
                        }
                        else
                        {
                            await route.ContinueAsync().ConfigureAwait(false);
                        }
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
                        // Swallow the exception so one bad route handler doesn't hang other
                        // tests, but also surface it so future bugs do not fail silently —
                        // Debug.WriteLine alone is invisible in test runner output.
                        LogRouteHandlerError(ex);
                    }
                });
            }
            else
            {
                // No matching handler — continue the request unmodified. Wrap in Task.Run
                // with a catch (symmetric with the handler branch above) so that CDP
                // failures surface to LogRouteHandlerError instead of being silently dropped.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await route.ContinueAsync(headers: LocaleHeaders(request)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogRouteHandlerError(ex);
                    }
                });
            }
        }

        private IDictionary<string, string> LocaleHeaders(CRRequest request)
        {
            if (request == null)
            {
                return null;
            }

            bool isWebSocket = LocaleAcceptLanguage.IsWebSocket(request.ResourceType)
                || IsWebSocketUrl(request.Url);
            return LocaleAcceptLanguage.Merge(request.Headers, _locale, isWebSocket);
        }

        private void RaiseRequestCreated(CRRequest request)
        {
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            if (_page.Opener != null && IsAboutBlankUrl(request.Url))
            {
                return;
            }

            if (_page.Opener != null
                && request.IsNavigationRequest
                && !_popupMainRequestEmitted
                && request.Frame == _page.MainFrame)
            {
                request.FrameUnavailable = true;
                _popupMainRequestEmitted = true;
            }

            _page.OnRequestCreated(request);
        }

        private void RaiseResponseReceived(CRResponse response)
        {
            CRRequest request = response?.Request;
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            if (_webSockets.TryGetValue(request.RequestId, out CRWebSocket socket))
            {
                socket.Har.ApplyHandshakeResponse(
                    response.Status,
                    response.StatusText,
                    response.Headers,
                    overwrite: false);
            }

            _page.OnResponseReceived(response);
        }

        private void RaiseRequestFinished(CRRequest request)
        {
            request?.MarkFinished();
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            _page.OnRequestFinished(request);
        }

        private void RaiseRequestFailed(CRRequest request)
        {
            request?.MarkFinished();
            if (request == null
                || NetworkRequestEvents.IsHiddenFromPage(request.Url, request.Method, request.ResourceType))
            {
                return;
            }

            _page.OnRequestFailed(request);
        }

        private List<CRRouteEntry> MatchingRoutes(string url)
        {
            List<CRRouteEntry> matches = new();
            lock (_routes)
            {
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    CRRouteEntry entry = _routes[i];
                    if (entry.MatchesUrl(url, NavigationUrl.ContextBase(_page.PublicPage?.Context)))
                    {
                        matches.Add(entry);
                    }
                }
            }

            return matches;
        }

        private bool IsRouteActive(CRRouteEntry entry)
        {
            lock (_routes)
            {
                return _routes.Contains(entry);
            }
        }

        private Task InvokeRouteAsync(CRRouteEntry entry, CRRoute route)
            => BindTimes(entry)(route);

        private Func<CRRoute, Task> BindTimes(CRRouteEntry entry)
        {
            return async route =>
            {
                TaskCompletionSource<bool> invocation = entry.Lifetime.Begin();
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
                finally
                {
                    entry.Lifetime.End(invocation);
                }
            };
        }

        private string RequestKey(CRSession session, string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                return requestId;
            }

            if (session == null || string.Equals(session.SessionId, _session.SessionId, StringComparison.Ordinal))
            {
                return requestId;
            }

            return session.SessionId + ":" + requestId;
        }

        private bool TryGetRequestByNetworkId(CRSession session, string networkId, out CRRequest request)
        {
            if (_requestsByRawId.TryGetValue(networkId, out request))
            {
                return true;
            }

            return _requestsById.TryGetValue(RequestKey(session, networkId), out request);
        }

        private void MaybeUpdateRequestSession(CRSession session, CRRequest request)
        {
            if (session == null || request == null || session == request.NetworkSession || session == _session)
            {
                return;
            }

            bool isMainResource = string.Equals(request.DocumentId, request.ProtocolRequestId, StringComparison.Ordinal);
            bool isWorker = _workerSessions.TryGetValue(session.SessionId ?? string.Empty, out WorkerSessionState state)
                && state.IsWorker;
            if (isMainResource || isWorker)
            {
                request.NetworkSession = session;
            }
        }

        private bool TryTakeRequest(CRSession session, string rawId, out CRRequest request)
        {
            string requestId = RequestKey(session, rawId);
            if (_requestsById.TryRemove(requestId, out request))
            {
                ForgetRawRequest(rawId, request);
                return true;
            }

            if (_requestsByRawId.TryRemove(rawId, out request))
            {
                _requestsById.TryRemove(request.RequestId, out _);
                return true;
            }

            request = null;
            return false;
        }

        private void ForgetRawRequest(string rawId, CRRequest request)
        {
            if (string.IsNullOrEmpty(rawId) || request == null)
            {
                return;
            }

            if (_requestsByRawId.TryGetValue(rawId, out CRRequest current) && ReferenceEquals(current, request))
            {
                _requestsByRawId.TryRemove(rawId, out _);
            }
        }

        private Frame ResolveFrame(CRSession session, string frameId)
        {
            if (!string.IsNullOrEmpty(frameId))
            {
                Frame byId = _page.FrameManager.FrameById(frameId);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (session != null
                && _workerSessions.TryGetValue(session.SessionId ?? string.Empty, out WorkerSessionState state))
            {
                if (state.Frame != null && state.Frame.ParentFrame != null)
                {
                    return state.Frame;
                }

                if (!string.IsNullOrEmpty(state.ParentFrameId))
                {
                    Frame parent = _page.FrameManager.FrameById(state.ParentFrameId);
                    if (parent != null)
                    {
                        state.Frame = parent;
                        return parent;
                    }
                }

                Frame soleChild = SoleChildFrame();
                if (soleChild != null)
                {
                    state.Frame = soleChild;
                    return soleChild;
                }

                if (state.Frame != null)
                {
                    return state.Frame;
                }
            }

            return _page.MainFrame;
        }

        private Frame SoleChildFrame()
        {
            Frame child = null;
            Frame main = _page.MainFrame;
            if (main == null)
            {
                return null;
            }

            foreach (Frame frame in _page.FrameManager.Frames)
            {
                if (frame == null || frame == main)
                {
                    continue;
                }

                if (child != null)
                {
                    return null;
                }

                child = frame;
            }

            return child;
        }

        private bool HasUserRoutes()
        {
            if (_handleAuthRequests)
            {
                return true;
            }

            lock (_routes)
            {
                return _routes.Count > 0;
            }
        }

        private bool TryReleaseHeldFetch(string rawId, string url, CRSession session = null, JsonElement? paused = null)
        {
            BufferedWillBeSent pending = null;
            if (!string.IsNullOrEmpty(rawId))
            {
                _pendingRequestWillBeSent.TryRemove(rawId, out pending);
            }

            if (pending == null && !string.IsNullOrEmpty(url))
            {
                foreach (KeyValuePair<string, BufferedWillBeSent> pair in _pendingRequestWillBeSent)
                {
                    if (pair.Value.Parameters.TryGetProperty("request", out JsonElement request)
                        && string.Equals(GetString(request, "url"), url, StringComparison.Ordinal)
                        && _pendingRequestWillBeSent.TryRemove(pair.Key, out pending))
                    {
                        break;
                    }
                }
            }

            if (pending == null)
            {
                return false;
            }

            if (paused.HasValue && !string.IsNullOrEmpty(rawId))
            {
                _networkIdToFetchRequestPaused[rawId] = new BufferedFetch(session, paused.Value);
            }

            OnRequestWillBeSent(pending.Parameters, pending.Session, force: true);
            return true;
        }

        private sealed class BufferedFetch
        {
            internal BufferedFetch(CRSession session, JsonElement parameters)
            {
                Session = session;
                Parameters = parameters;
            }

            internal CRSession Session { get; }

            internal JsonElement Parameters { get; }
        }

        private sealed class BufferedWillBeSent
        {
            internal BufferedWillBeSent(CRSession session, JsonElement parameters)
            {
                Session = session;
                Parameters = parameters;
            }

            internal CRSession Session { get; }

            internal JsonElement Parameters { get; }
        }

        private sealed class WorkerSessionState
        {
            internal WorkerSessionState(CRSession session, Frame frame, bool isWorker)
            {
                Session = session;
                Frame = frame;
                IsWorker = isWorker;
            }

            internal CRSession Session { get; }

            internal Frame Frame { get; set; }

            internal string ParentFrameId { get; set; }

            internal bool IsWorker { get; }

            internal Action<string, JsonElement?> Handler { get; set; }
        }
    }
}
