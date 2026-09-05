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
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Listens to <c>Network.*</c> and <c>Fetch.*</c> on a service-worker session so
    /// requests the worker issues surface as <see cref="IRequest"/> with
    /// <see cref="IRequest.ServiceWorker"/> set and can be routed.
    /// </summary>
    internal sealed class CRServiceWorkerNetwork : IDisposable
    {
        private readonly CRSession _session;
        private readonly IWorker _serviceWorker;
        private readonly ConcurrentDictionary<string, CRRequest> _requests = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, JsonElement> _pendingWillBeSent = new(StringComparer.Ordinal);
        private readonly object _routesLock = new();
        private List<CRRouteEntry> _routes = new();
        private bool _disposed;
        private bool _started;
        private bool _fetchEnabled;
        private bool _offline;
        private IDictionary<string, string> _extraHeaders;

        internal CRServiceWorkerNetwork(CRWorker worker, IWorker serviceWorker)
        {
            if (worker == null)
            {
                throw new ArgumentNullException(nameof(worker));
            }

            _session = worker.Session;
            _serviceWorker = serviceWorker ?? throw new ArgumentNullException(nameof(serviceWorker));
            worker.Closed += (_, _) => Dispose();
        }

        internal event EventHandler<CRRequest> Request;

        internal event EventHandler<CRResponse> Response;

        internal event EventHandler<CRRequest> RequestFinished;

        internal event EventHandler<CRRequest> RequestFailed;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _session.MessageReceived -= OnMessage;
        }

        internal async Task StartAsync()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _session.MessageReceived += OnMessage;
            try
            {
                // Official addSession enables Fetch with Network so the main
                // script request is paused. Enabling Network first reports the
                // already-in-flight script with no interception id.
                await UpdateInterceptionAsync().ConfigureAwait(false);
                await _session.SendAsync("Network.enable").ConfigureAwait(false);
                await ApplyEmulationAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                Dispose();
            }
        }

        internal Task SetOfflineAsync(bool offline)
        {
            _offline = offline;
            return ApplyEmulationAsync();
        }

        internal Task SetExtraHttpHeadersAsync(IDictionary<string, string> headers)
        {
            _extraHeaders = headers;
            return ApplyEmulationAsync();
        }

        internal Task SetRoutesAsync(IReadOnlyList<CRRouteEntry> routes)
        {
            lock (_routesLock)
            {
                _routes = routes == null ? new List<CRRouteEntry>() : new List<CRRouteEntry>(routes);
            }

            return UpdateInterceptionAsync();
        }

        private static bool IsReportableUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement property)
                ? property.GetString()
                : null;
        }

        private static int GetInt(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement property)
                && property.TryGetInt32(out int value)
                ? value
                : 0;
        }

        private static IDictionary<string, string> ParseHeaders(JsonElement payload, bool caseInsensitive)
        {
            Dictionary<string, string> result = caseInsensitive
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.Ordinal);

            if (payload.TryGetProperty("headers", out JsonElement headers)
                && headers.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in headers.EnumerateObject())
                {
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            return result;
        }

        private async Task ApplyEmulationAsync()
        {
            if (_disposed || !_started)
            {
                return;
            }

            try
            {
                await _session.SendAsync("Network.emulateNetworkConditions", new
                {
                    offline = _offline,
                    latency = 0,
                    downloadThroughput = -1,
                    uploadThroughput = -1,
                }).ConfigureAwait(false);

                if (_extraHeaders != null && _extraHeaders.Count > 0)
                {
                    await _session.SendAsync("Network.setExtraHTTPHeaders", new { headers = _extraHeaders }).ConfigureAwait(false);
                }
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private async Task UpdateInterceptionAsync()
        {
            if (_disposed || !_started)
            {
                return;
            }

            int routeCount;
            lock (_routesLock)
            {
                routeCount = _routes.Count;
            }

            try
            {
                if (routeCount > 0 && !_fetchEnabled)
                {
                    await _session.SendAsync("Fetch.enable", new
                    {
                        handleAuthRequests = true,
                        patterns = new[] { new { urlPattern = "*", requestStage = "Request" } },
                    }).ConfigureAwait(false);
                    await _session.SendAsync("Network.setCacheDisabled", new { cacheDisabled = true }).ConfigureAwait(false);
                    _fetchEnabled = true;
                }
                else if (routeCount == 0 && _fetchEnabled)
                {
                    await _session.SendAsync("Fetch.disable").ConfigureAwait(false);
                    await _session.SendAsync("Network.setCacheDisabled", new { cacheDisabled = false }).ConfigureAwait(false);
                    _fetchEnabled = false;
                }
            }
            catch (PlaywrightNativeException)
            {
            }
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
                case "Fetch.requestPaused":
                    OnFetchRequestPaused(parameters);
                    break;
            }
        }

        private CRRequest CreateRequest(string requestId, JsonElement requestPayload, string type)
        {
            CRRequest request = new CRRequest(
                requestId,
                GetString(requestPayload, "url"),
                GetString(requestPayload, "method"),
                ParseHeaders(requestPayload, caseInsensitive: false),
                GetString(requestPayload, "postData"),
                type ?? string.Empty,
                isNavigationRequest: string.Equals(type, "Document", StringComparison.Ordinal),
                frame: null,
                redirectedFrom: null)
            {
                ServiceWorker = _serviceWorker,
            };

            // Service-worker sessions do not emit requestWillBeSentExtraInfo.
            // Complete raw-header waiters with provisional headers so AllHeadersAsync resolves.
            request.EnsureRawRequestHeaders();
            return request;
        }

        private void OnRequestWillBeSent(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string requestId = GetString(payload, "requestId");
            if (string.IsNullOrEmpty(requestId)
                || !payload.TryGetProperty("request", out JsonElement requestPayload))
            {
                return;
            }

            if (_requests.ContainsKey(requestId))
            {
                return;
            }

            if (_fetchEnabled)
            {
                _pendingWillBeSent[requestId] = payload;
                return;
            }

            CRRequest request = CreateRequest(requestId, requestPayload, GetString(payload, "type"));
            _requests[requestId] = request;
            if (IsReportableUrl(request.Url))
            {
                Request?.Invoke(this, request);
            }
        }

        private void OnFetchRequestPaused(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string interceptionId = GetString(payload, "requestId");
            string networkId = GetString(payload, "networkId") ?? interceptionId;
            if (string.IsNullOrEmpty(interceptionId)
                || !payload.TryGetProperty("request", out JsonElement requestPayload))
            {
                return;
            }

            if (!_requests.TryGetValue(networkId, out CRRequest request))
            {
                if (_pendingWillBeSent.TryRemove(networkId, out JsonElement willBeSent)
                    && willBeSent.TryGetProperty("request", out JsonElement willBeSentRequest))
                {
                    request = CreateRequest(networkId, willBeSentRequest, GetString(willBeSent, "type") ?? GetString(payload, "resourceType"));
                }
                else
                {
                    request = CreateRequest(networkId, requestPayload, GetString(payload, "resourceType"));
                }

                _requests[networkId] = request;
                if (IsReportableUrl(request.Url))
                {
                    Request?.Invoke(this, request);
                }
            }

            List<CRRouteEntry> matches = new();
            lock (_routesLock)
            {
                for (int i = _routes.Count - 1; i >= 0; i--)
                {
                    CRRouteEntry entry = _routes[i];
                    if (entry.MatchesUrl(request.Url))
                    {
                        matches.Add(entry);
                    }
                }
            }

            CRRoute route = new(_session, interceptionId, request);
            _ = Task.Run(async () =>
            {
                try
                {
                    if (matches.Count == 0)
                    {
                        await route.ContinueAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        CRRouteEntry first = matches[0];
                        first.ConsumeAndShouldRemove();
                        await first.Handler(route).ConfigureAwait(false);
                    }

                    EmitSyntheticResponseIfNeeded(request);
                }
                catch (PlaywrightNativeException)
                {
                    try
                    {
                        await route.ContinueAsync().ConfigureAwait(false);
                        EmitSyntheticResponseIfNeeded(request);
                    }
                    catch (PlaywrightNativeException)
                    {
                    }
                }
            });
        }

        private void EmitSyntheticResponseIfNeeded(CRRequest request)
        {
            if (request == null || request.Response != null)
            {
                return;
            }

            int status = request.Fulfilled ? request.FulfilledStatus : 200;
            IDictionary<string, string> headers = request.FulfilledHeaders
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CRResponse response = new(
                _session,
                request,
                request.Url,
                status,
                HttpStatusText.For(status),
                headers,
                fromServiceWorker: false,
                httpVersion: "http/1.1");
            request.Response = response;
            Response?.Invoke(this, response);
            request.Finished = true;
            request.MarkFinished();
            RequestFinished?.Invoke(this, request);
        }

        private void OnResponseReceived(JsonElement? parameters)
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

            if (!_requests.TryGetValue(requestId, out CRRequest request))
            {
                if (!_pendingWillBeSent.TryRemove(requestId, out JsonElement willBeSent)
                    || !willBeSent.TryGetProperty("request", out JsonElement willBeSentRequest))
                {
                    return;
                }

                request = CreateRequest(requestId, willBeSentRequest, GetString(willBeSent, "type"));
                _requests[requestId] = request;
                if (IsReportableUrl(request.Url))
                {
                    Request?.Invoke(this, request);
                }
            }

            if (!payload.TryGetProperty("response", out JsonElement responsePayload))
            {
                return;
            }

            CRResponse response = new(
                _session,
                request,
                GetString(responsePayload, "url"),
                GetInt(responsePayload, "status"),
                GetString(responsePayload, "statusText"),
                ParseHeaders(responsePayload, caseInsensitive: true),
                ResponseNetworkInfo.ParseServerAddr(responsePayload),
                ResponseNetworkInfo.ParseSecurityDetails(responsePayload),
                ResponseNetworkInfo.ParseFromServiceWorker(responsePayload),
                ResponseNetworkInfo.ParseHttpVersion(responsePayload));

            request.Response = response;
            if (IsReportableUrl(request.Url))
            {
                Response?.Invoke(this, response);
            }
        }

        private void OnLoadingFinished(JsonElement? parameters)
        {
            string requestId = parameters.HasValue ? GetString(parameters.Value, "requestId") : null;
            if (string.IsNullOrEmpty(requestId) || !_requests.TryRemove(requestId, out CRRequest request))
            {
                return;
            }

            request.Finished = true;
            request.MarkFinished();
            RequestFinished?.Invoke(this, request);
        }

        private void OnLoadingFailed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string requestId = GetString(parameters.Value, "requestId");
            if (string.IsNullOrEmpty(requestId) || !_requests.TryRemove(requestId, out CRRequest request))
            {
                return;
            }

            request.FailureText = GetString(parameters.Value, "errorText") ?? "net::ERR_FAILED";
            request.Finished = true;
            request.MarkFinished();
            RequestFailed?.Invoke(this, request);
        }
    }
}
