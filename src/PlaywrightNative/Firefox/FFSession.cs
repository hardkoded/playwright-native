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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Transport;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Represents a single Juggler protocol session. Messages are routed here from
    /// <see cref="FFConnection"/> by session ID.
    /// </summary>
    internal class FFSession : IDisposable
    {
        private readonly FFConnection _connection;
        private readonly string _sessionId;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>> _callbacks = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFSession"/> class.
        /// </summary>
        /// <param name="connection">The parent <see cref="FFConnection"/>.</param>
        /// <param name="sessionId">The session ID (empty for the root session).</param>
        public FFSession(FFConnection connection, string sessionId)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _sessionId = sessionId;
        }

        /// <summary>
        /// Occurs when an event message (no id) is received for this session.
        /// Provides the event method name and parameters.
        /// </summary>
        internal event Action<string, JsonElement?> MessageReceived;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (System.Collections.Generic.KeyValuePair<int, TaskCompletionSource<JsonElement?>> kvp in _callbacks)
            {
                kvp.Value.TrySetException(new TargetClosedException("Session disposed"));
            }

            _callbacks.Clear();
        }

        /// <summary>
        /// Creates a child session routed by the given session ID.
        /// </summary>
        /// <param name="sessionId">The child session ID.</param>
        /// <returns>A new <see cref="FFSession"/>.</returns>
        internal FFSession CreateChildSession(string sessionId) => _connection.CreateSession(sessionId);

        /// <summary>
        /// Sends a Juggler protocol command and waits for the response.
        /// </summary>
        /// <param name="method">The protocol method (e.g. "Page.navigate").</param>
        /// <param name="parameters">Optional parameters object.</param>
        /// <returns>The response result element, or <c>null</c> if the response has no result.</returns>
        internal Task<JsonElement?> SendAsync(string method, object parameters = null)
        {
            if (_disposed || _connection.IsClosed)
            {
                return Task.FromException<JsonElement?>(
                    new TargetClosedException($"Session {_sessionId} is closed"));
            }

            var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);

            JsonElement? paramsElement = null;
            if (parameters != null)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(parameters);
                paramsElement = System.Text.Json.JsonDocument.Parse(json).RootElement;
            }

            // Register the callback before writing so a fast pipe reply cannot
            // arrive before the waiter is in the map.
            int id = _connection.AllocateId();
            _callbacks.TryAdd(id, tcs);
            _connection.RawSend(id, _sessionId, method, paramsElement);

            return tcs.Task;
        }

        /// <summary>
        /// Dispatches an incoming message — either resolving/rejecting a pending callback
        /// (if it has an id) or emitting it as an event (if it is a notification).
        /// </summary>
        /// <param name="message">The incoming protocol response.</param>
        internal void OnMessage(ProtocolResponse message)
        {
            if (message.Id.HasValue)
            {
                if (_callbacks.TryRemove(message.Id.Value, out TaskCompletionSource<JsonElement?> tcs))
                {
                    if (message.Error != null)
                    {
                        tcs.TrySetException(new PlaywrightNativeException(message.Error.Message));
                    }
                    else
                    {
                        tcs.TrySetResult(message.Result);
                    }
                }
            }
            else if (message.Method != null)
            {
                MessageReceived?.Invoke(message.Method, message.Params);
            }
        }
    }
}
