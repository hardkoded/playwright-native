/*
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Threading.Tasks;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Tracks network requests for a Firefox page and supports route-based interception
    /// using the Juggler <c>Network.*</c> protocol events and commands.
    /// </summary>
    internal class FFNetworkManager
    {
        private readonly FFSession _session;
        private readonly FFPage _page;
        private readonly ConcurrentDictionary<string, FFRequest> _requests = new();
        private readonly List<(string Pattern, Func<FFRoute, Task> Handler)> _routes = new();
        private bool _interceptionEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFNetworkManager"/> class.
        /// </summary>
        /// <param name="session">The Juggler page session.</param>
        /// <param name="page">The owning <see cref="FFPage"/>.</param>
        public FFNetworkManager(FFSession session, FFPage page)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _page = page ?? throw new ArgumentNullException(nameof(page));

            _session.MessageReceived += OnMessage;
        }

        /// <summary>
        /// Adds a route handler for the given URL glob pattern.
        /// </summary>
        /// <param name="pattern">A glob-style URL pattern (e.g. <c>**/api/**</c>).</param>
        /// <param name="handler">The handler to invoke when a request matches.</param>
        internal void AddRoute(string pattern, Func<FFRoute, Task> handler)
        {
            lock (_routes)
            {
                _routes.Add((pattern, handler));
            }
        }

        /// <summary>
        /// Enables or disables request interception based on whether any routes are registered.
        /// </summary>
        internal async Task UpdateInterceptionAsync()
        {
            bool shouldEnable;
            lock (_routes)
            {
                shouldEnable = _routes.Count > 0;
            }

            if (shouldEnable == _interceptionEnabled)
            {
                return;
            }

            _interceptionEnabled = shouldEnable;
            await _session.SendAsync("Network.setRequestInterception", new { enabled = shouldEnable })
                .ConfigureAwait(false);
        }

        private static bool UrlMatchesPattern(string url, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
            {
                return true;
            }

            // Convert glob pattern to regex.
            string regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*", ".*", StringComparison.Ordinal)
                .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
                + "$";

            return Regex.IsMatch(url, regexPattern, RegexOptions.IgnoreCase);
        }

        private static string GetString(JsonElement element, string property)
            => element.TryGetProperty(property, out JsonElement el) ? el.GetString() ?? string.Empty : string.Empty;

        private static void LogRouteHandlerError(Exception ex, string url)
        {
            System.Console.Error.WriteLine($"[FFNetworkManager] Route handler for '{url}' threw: {ex.Message}");
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
                case "Network.requestFinished":
                    OnRequestFinished(parameters);
                    break;
                case "Network.requestFailed":
                    OnRequestFailed(parameters);
                    break;
            }
        }

        private void OnRequestWillBeSent(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;

            string requestId = GetString(payload, "requestId");
            bool isIntercepted = payload.TryGetProperty("isIntercepted", out JsonElement isInterceptedEl) &&
                isInterceptedEl.GetBoolean();

            FFRequest request = new(payload, requestId);
            _requests.TryAdd(requestId, request);

            _page.OnRequestCreated(request);

            if (isIntercepted)
            {
                _ = HandleInterceptedRequestAsync(request);
            }
        }

        private async Task HandleInterceptedRequestAsync(FFRequest request)
        {
            List<(string Pattern, Func<FFRoute, Task> Handler)> routesSnapshot;
            lock (_routes)
            {
                routesSnapshot = new List<(string, Func<FFRoute, Task>)>(_routes);
            }

            string url = request.Url;
            foreach ((string pattern, Func<FFRoute, Task> handler) in routesSnapshot)
            {
                if (UrlMatchesPattern(url, pattern))
                {
                    FFRoute route = new(_session, request);
                    try
                    {
                        await handler(route).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogRouteHandlerError(ex, url);
                        await route.ContinueAsync().ConfigureAwait(false);
                    }

                    return;
                }
            }

            // No matching handler — continue the request.
            await _session.SendAsync("Network.resumeInterceptedRequest", new
            {
                requestId = request.RequestId,
            }).ConfigureAwait(false);
        }

        private void OnResponseReceived(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string requestId = GetString(payload, "requestId");

            if (!_requests.TryGetValue(requestId, out FFRequest request))
            {
                return;
            }

            FFResponse response = new(payload, request);
            request.Response = response;
            _page.OnResponseReceived(response);
        }

        private void OnRequestFinished(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            string requestId = GetString(parameters.Value, "requestId");

            if (_requests.TryRemove(requestId, out FFRequest request))
            {
                _page.OnRequestFinished(request);
            }
        }

        private void OnRequestFailed(JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            JsonElement payload = parameters.Value;
            string requestId = GetString(payload, "requestId");

            if (_requests.TryRemove(requestId, out FFRequest request))
            {
                string errorText = GetString(payload, "errorCode");
                request.FailureText = errorText;
                _page.OnRequestFailed(request);
            }
        }
    }
}
