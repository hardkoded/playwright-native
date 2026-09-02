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
using System.Threading;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Transport;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Chromium DevTools Protocol connection that wraps an <see cref="IConnectionTransport"/>
    /// and routes messages by session ID to <see cref="CRSession"/> instances.
    /// </summary>
    internal class CRConnection : IDisposable
    {
        /// <summary>
        /// Special message ID used for Browser.close commands. Messages with this ID
        /// are ignored when received as responses.
        /// </summary>
        internal const int KBrowserCloseMessageId = -9999;

        private readonly IConnectionTransport _transport;
        private readonly ILogger<CRConnection> _logger;
        private readonly ConcurrentDictionary<string, CRSession> _sessions = new();
        private int _lastId;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRConnection"/> class.
        /// </summary>
        /// <param name="transport">The underlying connection transport.</param>
        /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
        public CRConnection(IConnectionTransport transport, ILoggerFactory loggerFactory = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _logger = loggerFactory?.CreateLogger<CRConnection>();

            RootSession = new CRSession(this, sessionId: string.Empty);
            _sessions.TryAdd(string.Empty, RootSession);

            _transport.OnMessage = OnMessage;
            _transport.OnClose = OnClose;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        ~CRConnection() => Dispose(false);

        /// <summary>
        /// Occurs when the underlying transport connection is closed.
        /// </summary>
        internal event EventHandler Disconnected;

        /// <summary>
        /// Gets the root CDP session (sessionId=""), used for browser-level commands.
        /// </summary>
        internal CRSession RootSession { get; }

        /// <summary>
        /// Gets a value indicating whether the connection has been closed.
        /// </summary>
        internal bool IsClosed { get; private set; }

        /// <summary>
        /// Reason from the last transport close, including a custom WebSocket
        /// close description such as official <c>Oh my!</c>.
        /// </summary>
        internal string CloseReason { get; private set; }

        /// <summary>
        /// Gets the sessions dictionary for child session registration.
        /// </summary>
        internal ConcurrentDictionary<string, CRSession> Sessions => _sessions;

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Sends a raw CDP message through the transport and returns the message ID.
        /// </summary>
        /// <param name="sessionId">The target session ID (empty string for the root session).</param>
        /// <param name="method">The CDP method name.</param>
        /// <param name="parameters">Optional method parameters.</param>
        /// <returns>The unique message ID assigned to this request.</returns>
        internal int RawSend(string sessionId, string method, JsonElement? parameters)
        {
            int id = Interlocked.Increment(ref _lastId);

            var request = new ProtocolRequest
            {
                Id = id,
                Method = method,
                Params = parameters,
                SessionId = sessionId,
            };

            _logger?.LogInformation("Send ► {SessionId} {Method} {Id}", sessionId, method, id);

            // Fire-and-forget: the transport send is not awaited because callers
            // track completion through the callback dictionary on CRSession.
            _ = _transport.SendAsync(request);

            return id;
        }

        private void OnMessage(ProtocolResponse message)
        {
            _logger?.LogInformation("Recv ◀ {Method} id={Id} sessionId={SessionId}", message.Method, message.Id, message.SessionId);

            if (message.Id == KBrowserCloseMessageId)
            {
                return;
            }

            string sessionId = message.SessionId ?? string.Empty;
            if (_sessions.TryGetValue(sessionId, out CRSession session))
            {
                session.OnMessage(message);
            }
            else
            {
                _logger?.LogWarning("Unknown session {SessionId}", sessionId);
            }
        }

        private void OnClose(string reason)
        {
            _logger?.LogInformation("Connection closed: {Reason}", reason);

            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            CloseReason = reason;

            foreach (KeyValuePair<string, CRSession> kvp in _sessions)
            {
                kvp.Value.Dispose();
            }

            _sessions.Clear();

            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!disposing)
            {
                return;
            }

            if (!IsClosed)
            {
                OnClose("Connection disposed");
            }

            _transport.Dispose();
        }
    }
}
