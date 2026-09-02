/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightSharp.Transport;
using PlaywrightSharp.Transport.Protocol;

namespace PlaywrightSharp.Firefox
{
    /// <summary>
    /// Firefox Juggler protocol connection that wraps an <see cref="IConnectionTransport"/>
    /// and routes messages by session ID to <see cref="FFSession"/> instances.
    /// </summary>
    internal class FFConnection : IDisposable
    {
        /// <summary>
        /// Special message ID used for Browser.close. Responses with this ID are ignored.
        /// </summary>
        internal const int KBrowserCloseMessageId = -9999;

        private readonly IConnectionTransport _transport;
        private readonly ILogger<FFConnection> _logger;
        private readonly ConcurrentDictionary<string, FFSession> _sessions = new();
        private int _lastId;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFConnection"/> class.
        /// </summary>
        /// <param name="transport">The underlying connection transport.</param>
        /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
        public FFConnection(IConnectionTransport transport, ILoggerFactory loggerFactory = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _logger = loggerFactory?.CreateLogger<FFConnection>();

            RootSession = new FFSession(this, sessionId: string.Empty);
            _sessions.TryAdd(string.Empty, RootSession);

            _transport.OnMessage = OnMessage;
            _transport.OnClose = OnClose;
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        ~FFConnection() => Dispose(false);

        /// <summary>
        /// Occurs when the underlying transport connection is closed.
        /// </summary>
        internal event EventHandler Disconnected;

        /// <summary>
        /// Gets the root Juggler session (sessionId=""), used for browser-level commands.
        /// </summary>
        internal FFSession RootSession { get; }

        /// <summary>
        /// Gets a value indicating whether the connection has been closed.
        /// </summary>
        internal bool IsClosed { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Allocates the next protocol message id. Callers must register their
        /// response waiter before <see cref="RawSend"/>.
        /// </summary>
        /// <returns>The new message id.</returns>
        internal int AllocateId() => Interlocked.Increment(ref _lastId);

        /// <summary>
        /// Sends a raw Juggler protocol message through the transport.
        /// </summary>
        /// <param name="id">The message id from <see cref="AllocateId"/>.</param>
        /// <param name="sessionId">The target session ID (empty string for the root session).</param>
        /// <param name="method">The protocol method name.</param>
        /// <param name="parameters">Optional method parameters.</param>
        internal void RawSend(int id, string sessionId, string method, JsonElement? parameters)
        {
            var request = new ProtocolRequest
            {
                Id = id,
                Method = method,
                Params = parameters,
                SessionId = string.IsNullOrEmpty(sessionId) ? null : sessionId,
            };

            _logger?.LogInformation("Send ► {SessionId} {Method} {Id}", sessionId, method, id);

            _ = _transport.SendAsync(request);
        }

        /// <summary>
        /// Creates a new child session for a page target.
        /// </summary>
        /// <param name="sessionId">The session ID from the Browser.attachedToTarget event.</param>
        /// <returns>A new <see cref="FFSession"/> registered in the sessions map.</returns>
        internal FFSession CreateSession(string sessionId)
        {
            var session = new FFSession(this, sessionId);
            _sessions.TryAdd(sessionId, session);
            return session;
        }

        private void OnMessage(ProtocolResponse message)
        {
            _logger?.LogInformation("Recv ◀ {Method} id={Id} sessionId={SessionId}", message.Method, message.Id, message.SessionId);

            if (message.Id == KBrowserCloseMessageId)
            {
                return;
            }

            string sessionId = message.SessionId ?? string.Empty;
            if (_sessions.TryGetValue(sessionId, out FFSession session))
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

            foreach (KeyValuePair<string, FFSession> kvp in _sessions)
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
