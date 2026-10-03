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
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Public <see cref="ICDPSession"/> wrapper around a child <see cref="CRSession"/>.
    /// </summary>
    internal sealed partial class CRCDPSession : ICDPSession
    {
        private readonly CRSession _session;
        private readonly CRSession _rootSession;
        private readonly Dictionary<string, CRCDPSessionEvent> _eventSubscriptions = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> _suppressedFaviconRequestIds = new(StringComparer.Ordinal);
        private bool _detached;

        internal CRCDPSession(CRSession session, CRSession rootSession)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _rootSession = rootSession ?? throw new ArgumentNullException(nameof(rootSession));
            _session.MessageReceived += OnMessageReceived;
            _session.Closed += OnSessionClosed;
        }

        /// <inheritdoc/>
        public event EventHandler<ICDPSession> Close;

        /// <inheritdoc/>
        public Task<JsonElement?> SendAsync(string method, object args = null)
            => _session.SendAsync(method, args);

        /// <inheritdoc/>
        public ICDPSessionEvent Event(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentException("eventName must be non-empty.", nameof(eventName));
            }

            if (!_eventSubscriptions.TryGetValue(eventName, out CRCDPSessionEvent subscription))
            {
                subscription = new CRCDPSessionEvent(eventName);
                _eventSubscriptions[eventName] = subscription;
            }

            return subscription;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await DetachAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DetachAsync()
        {
            if (_session.IsClosed)
            {
                throw new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage);
            }

            if (_detached)
            {
                return;
            }

            _detached = true;
            try
            {
                await _rootSession.SendAsync("Target.detachFromTarget", new
                {
                    sessionId = _session.SessionId,
                }).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Target already gone.
            }

            _session.Dispose();
        }

        /// <summary>
        /// Official: page close detaches user CDP sessions so later
        /// <see cref="DetachAsync"/> / <see cref="SendAsync"/> throw.
        /// </summary>
        internal void NotifyTargetClosed()
        {
            _session.Dispose();
        }

        private void OnMessageReceived(string method, JsonElement? parameters)
        {
            // Match page-level NetworkRequestEvents: Chromium still emits favicon
            // housekeeping on a raw Network.enable session after document load.
            // page.goto can return before or after that frame depending on
            // continuation scheduling, so session.spec.ts "should send events"
            // (expects exactly one EmptyPage requestWillBeSent) would flake.
            // Suppress favicon Network.* like page.Request does.
            if (IsFaviconNetworkEvent(method, parameters))
            {
                return;
            }

            if (_eventSubscriptions.TryGetValue(method, out CRCDPSessionEvent subscription))
            {
                subscription.Raise(parameters);
            }
        }

        private bool IsFaviconNetworkEvent(string method, JsonElement? parameters)
        {
            if (string.IsNullOrEmpty(method)
                || !method.StartsWith("Network.", StringComparison.Ordinal)
                || !parameters.HasValue)
            {
                return false;
            }

            JsonElement payload = parameters.Value;
            string requestId = payload.TryGetProperty("requestId", out JsonElement idEl)
                ? idEl.GetString()
                : null;

            if (string.Equals(method, "Network.requestWillBeSent", StringComparison.Ordinal))
            {
                string url = null;
                if (payload.TryGetProperty("request", out JsonElement request)
                    && request.ValueKind == JsonValueKind.Object
                    && request.TryGetProperty("url", out JsonElement urlEl))
                {
                    url = urlEl.GetString();
                }

                if (!NetworkRequestEvents.IsFaviconUrl(url))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(requestId))
                {
                    _suppressedFaviconRequestIds[requestId] = 0;
                }

                return true;
            }

            if (string.IsNullOrEmpty(requestId)
                || !_suppressedFaviconRequestIds.ContainsKey(requestId))
            {
                return false;
            }

            if (string.Equals(method, "Network.loadingFinished", StringComparison.Ordinal)
                || string.Equals(method, "Network.loadingFailed", StringComparison.Ordinal))
            {
                _suppressedFaviconRequestIds.TryRemove(requestId, out _);
            }

            return true;
        }

        private void OnSessionClosed(object sender, EventArgs e)
        {
            _session.MessageReceived -= OnMessageReceived;
            _session.Closed -= OnSessionClosed;
            _detached = true;
            Close?.Invoke(this, this);
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<JsonElement?> ICDPSession.SendAsync(string method, Dictionary<string, object> args)
            => SendAsync(method, (object)args);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
