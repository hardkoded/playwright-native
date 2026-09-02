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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Transport;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// WebKit Inspector Protocol connection over a pipe transport. Owns a single
    /// browser-level <see cref="WKSession"/> and dispatches inbound messages either
    /// to that session (browser-level events) or to <see cref="PageProxyMessageReceived"/>
    /// subscribers when a <c>pageProxyId</c> is present at the top level.
    /// </summary>
    internal class WKConnection : IDisposable
    {
        /// <summary>
        /// Sentinel id used for the <c>Playwright.close</c> command during shutdown.
        /// Responses with this id are discarded — the transport will be torn down
        /// before any reply would be acted on.
        /// </summary>
        internal const int BrowserCloseMessageId = -9999;

        private readonly IConnectionTransport _transport;
        private readonly ILogger<WKConnection> _logger;
        private int _lastId;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKConnection"/> class.
        /// </summary>
        /// <param name="transport">The underlying pipe transport.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public WKConnection(IConnectionTransport transport, ILoggerFactory loggerFactory = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _logger = loggerFactory?.CreateLogger<WKConnection>();

            BrowserSession = new WKSession(this, sessionId: string.Empty);

            _transport.OnMessage = DispatchTransportMessage;
            _transport.OnClose = OnTransportClose;
        }

        /// <summary>
        /// Occurs when the underlying transport reports the connection has closed.
        /// </summary>
        internal event EventHandler Disconnected;

        /// <summary>
        /// Fired for every inbound protocol message that carried a top-level
        /// <c>pageProxyId</c>. The page-routing layer (<see cref="WKBrowser"/>) subscribes
        /// to fan these out to the matching <see cref="WKPage"/> session — typed delivery,
        /// no JSON round-trip.
        /// </summary>
        internal event Action<string, ProtocolResponse> PageProxyMessageReceived;

        /// <summary>
        /// Gets the browser-level <see cref="WKSession"/>. All inbound messages that lack
        /// a <c>pageProxyId</c> are dispatched here.
        /// </summary>
        internal WKSession BrowserSession { get; }

        /// <summary>
        /// Gets a value indicating whether the connection has been closed.
        /// </summary>
        internal bool IsClosed { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!IsClosed)
            {
                OnTransportClose("Connection disposed");
            }

            _transport.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Allocates a fresh message id on the shared counter. Used by inner-target
        /// sessions whose outbound messages are wrapped in <c>Target.sendMessageToTarget</c>
        /// rather than sent as top-level protocol requests — they still need globally
        /// unique ids so inbound callbacks can be routed back.
        /// </summary>
        /// <returns>A new monotonically increasing message id.</returns>
        internal int NextMessageId() => Interlocked.Increment(ref _lastId);

        /// <summary>
        /// Sends a raw WIP message on the transport. Builds a <see cref="ProtocolRequest"/>
        /// with the optional <paramref name="pageProxyId"/> stamped at the top level so
        /// WebKit routes the message to the matching page proxy.
        /// </summary>
        /// <param name="sessionId">The session id (always empty for WebKit in practice).</param>
        /// <param name="pageProxyId">The page proxy id, or null for browser-level messages.</param>
        /// <param name="method">The protocol method name.</param>
        /// <param name="parameters">Optional parameters element.</param>
        /// <param name="messageId">Optional explicit message id (used for the close sentinel). When null, the connection auto-allocates.</param>
        /// <returns>The allocated message id.</returns>
        internal int RawSend(
            string sessionId,
            string pageProxyId,
            string method,
            JsonElement? parameters,
            int? messageId = null)
        {
            int id = messageId ?? Interlocked.Increment(ref _lastId);

            ProtocolRequest request = new()
            {
                Id = id,
                Method = method,
                Params = parameters,
                SessionId = sessionId,
                PageProxyId = pageProxyId,
            };

            _logger?.LogInformation(
                "Send ► pageProxyId={PageProxyId} {Method} id={Id}",
                pageProxyId ?? "<browser>",
                method,
                id);

            _ = _transport.SendAsync(request);
            return id;
        }

        private void DispatchTransportMessage(ProtocolResponse message)
        {
            _logger?.LogInformation(
                "Recv ◀ pageProxyId={PageProxyId} method={Method} id={Id}",
                message.PageProxyId ?? "<browser>",
                message.Method,
                message.Id);

            // Discard responses to the close sentinel — we already abandoned that call.
            if (message.Id == BrowserCloseMessageId)
            {
                return;
            }

            // Messages carrying a pageProxyId are routed to subscribers via the typed
            // PageProxyMessageReceived event — no JSON round-trip.
            if (!string.IsNullOrEmpty(message.PageProxyId))
            {
                PageProxyMessageReceived?.Invoke(message.PageProxyId, message);
                return;
            }

            BrowserSession.OnMessage(message);
        }

        private void OnTransportClose(string reason)
        {
            _logger?.LogInformation("Connection closed: {Reason}", reason);

            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            BrowserSession.Dispose();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }
}
