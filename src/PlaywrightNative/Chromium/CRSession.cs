/*
 * Copyright (c) 2020 Darío Kondratiuk
 * Copyright (c) 2020 Meir Blachman
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
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Represents a single Chrome DevTools Protocol session. Each session is identified
    /// by a unique session ID and tracks its own set of pending request callbacks.
    /// </summary>
    internal class CRSession : IDisposable
    {
        /// <summary>
        /// Error code returned by Chromium when a message is sent to a session that
        /// has already been closed. These responses are silently ignored.
        /// </summary>
        private const int ClosedSessionErrorCode = -32001;

        private readonly CRConnection _connection;
        private readonly ConcurrentDictionary<int, PendingCallback> _callbacks = new();
        private bool _closed;
        private bool _crashed;
        private string _closeReason;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRSession"/> class.
        /// </summary>
        /// <param name="connection">The parent <see cref="CRConnection"/> that owns this session.</param>
        /// <param name="sessionId">The CDP session identifier (empty string for the root session).</param>
        public CRSession(CRConnection connection, string sessionId)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            SessionId = sessionId;
        }

        /// <summary>
        /// Occurs when a CDP event (a message without an ID) is received on this session.
        /// The first parameter is the method name, the second is the event parameters.
        /// </summary>
        internal event Action<string, JsonElement?> MessageReceived;

        /// <summary>
        /// Occurs when this session is disposed or the connection drops it.
        /// </summary>
        internal event EventHandler Closed;

        /// <summary>
        /// Gets the CDP session identifier. The root session uses an empty string.
        /// </summary>
        internal string SessionId { get; }

        /// <summary>
        /// Gets a value indicating whether this session has been closed.
        /// </summary>
        internal bool IsClosed => _closed;

        /// <summary>
        /// Gets or sets a value indicating whether this session has crashed.
        /// </summary>
        internal bool IsCrashed
        {
            get => _crashed;
            set => _crashed = value;
        }

        /// <summary>
        /// Gets or sets the reason recorded when the owning page was closed.
        /// </summary>
        internal string CloseReason
        {
            get => _closeReason;
            set => _closeReason = value;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _connection.Sessions.TryRemove(SessionId, out _);

            foreach (KeyValuePair<int, PendingCallback> kvp in _callbacks)
            {
                kvp.Value.Completion.TrySetException(ClosedSessionException(
                    DriverMessages.BrowserOrContextClosedExceptionMessage));
            }

            _callbacks.Clear();
            Closed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sends a CDP command and waits for the response.
        /// </summary>
        /// <param name="method">The CDP method name (e.g. "Page.navigate").</param>
        /// <param name="parameters">Optional method parameters, serialized to <see cref="JsonElement"/>.</param>
        /// <returns>A task that resolves with the result of the CDP command.</returns>
        /// <exception cref="TargetClosedException">Thrown when the session has been closed or crashed.</exception>
        internal Task<JsonElement?> SendAsync(string method, object parameters = null)
        {
            if (_closed)
            {
                throw ClosedSessionException(
                    DriverMessages.BrowserOrContextClosedExceptionMessage);
            }

            if (_crashed)
            {
                throw new TargetClosedException($"Protocol error ({method}): Session crashed.");
            }

            JsonElement? jsonParams = null;
            if (parameters != null)
            {
                jsonParams = JsonSerializer.SerializeToElement(parameters);
            }

            // Register the callback before sending. A reply that arrives between
            // RawSend and TryAdd was previously dropped, leaving SendAsync hung
            // forever (flaky NewPage / setDeviceMetricsOverride stalls).
            int id = _connection.NextMessageId();
            PendingCallback pending = new PendingCallback
            {
                Method = method,
                Completion = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously),
            };
            _callbacks.TryAdd(id, pending);
            _connection.RawSend(id, SessionId, method, jsonParams);

            return pending.Completion.Task;
        }

        /// <summary>
        /// Creates a child session registered under the given session ID.
        /// </summary>
        /// <param name="sessionId">The CDP session identifier for the child session.</param>
        /// <returns>The newly created <see cref="CRSession"/>.</returns>
        internal CRSession CreateChildSession(string sessionId)
        {
            if (_connection.Sessions.TryGetValue(sessionId, out CRSession existing))
            {
                return existing;
            }

            CRSession session = new CRSession(_connection, sessionId);
            if (_connection.Sessions.TryAdd(sessionId, session))
            {
                return session;
            }

            return _connection.Sessions.TryGetValue(sessionId, out existing)
                ? existing
                : session;
        }

        /// <summary>
        /// Processes an incoming protocol response for this session.
        /// </summary>
        /// <param name="message">The protocol response to process.</param>
        internal void OnMessage(ProtocolResponse message)
        {
            if (message.Id.HasValue)
            {
                int id = message.Id.Value;

                if (_callbacks.TryRemove(id, out PendingCallback callback))
                {
                    if (message.Error != null)
                    {
                        string method = !string.IsNullOrEmpty(message.Method)
                            ? message.Method
                            : callback.Method;
                        callback.Completion.TrySetException(new PlaywrightNativeException(
                            $"Protocol error ({method}): {message.Error.Message}"));
                    }
                    else
                    {
                        callback.Completion.TrySetResult(message.Result);
                    }
                }
                else if (message.Error?.Code == ClosedSessionErrorCode)
                {
                    // Silently ignore errors for messages sent to sessions that have already closed.
                }

                return;
            }

            // No ID means this is a CDP event — invoke the event listener.
            MessageReceived?.Invoke(message.Method, message.Params);
        }

        private TargetClosedException ClosedSessionException(string message)
            => ClosedTarget.Exception(message, _closeReason);

        private sealed class PendingCallback
        {
            internal string Method { get; set; }

            internal TaskCompletionSource<JsonElement?> Completion { get; set; }
        }
    }
}
