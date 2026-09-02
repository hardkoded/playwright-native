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
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.Transport.Protocol;

namespace PlaywrightSharp.WebKit
{
    /// <summary>
    /// Represents a single WebKit Inspector Protocol session. Routed from
    /// <see cref="WKConnection"/> either by the browser-level top-level message
    /// stream (empty sessionId) or by a synthetic <c>kPageProxyMessageReceived</c>
    /// envelope for messages targeted at a specific page proxy.
    /// </summary>
    internal class WKSession : IDisposable
    {
        // A protocol command normally responds in milliseconds. If a response is ever lost,
        // bound the wait so the command throws a fast, labelled timeout naming the stuck
        // method instead of hanging until the caller's (much longer) timeout. Above real
        // latency, below typical navigation/test timeouts.
        private const int CommandTimeoutMs = 20_000;

        private readonly WKConnection _connection;
        private readonly string _sessionId;
        private readonly string _pageProxyId;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>> _callbacks = new();
        private string _closeReason;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKSession"/> class as a browser-level
        /// session with no associated page proxy.
        /// </summary>
        /// <param name="connection">The owning <see cref="WKConnection"/>.</param>
        /// <param name="sessionId">The session ID (empty string for the browser session).</param>
        public WKSession(WKConnection connection, string sessionId)
            : this(connection, sessionId, pageProxyId: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WKSession"/> class targeting a specific
        /// WebKit page proxy. Outbound messages have <c>pageProxyId</c> injected at the top level
        /// so the browser routes them to the matching page-proxy process.
        /// </summary>
        /// <param name="connection">The owning <see cref="WKConnection"/>.</param>
        /// <param name="sessionId">The session ID (empty string is fine — page proxies share the browser session).</param>
        /// <param name="pageProxyId">The WebKit pageProxyId this session is bound to. Null for browser session.</param>
        public WKSession(WKConnection connection, string sessionId, string pageProxyId)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _sessionId = sessionId ?? string.Empty;
            _pageProxyId = pageProxyId;
        }

        /// <summary>
        /// Fired when an event (a message without an <c>id</c>) is received on this session.
        /// Provides the event method name and its <c>params</c>.
        /// </summary>
        internal event Action<string, JsonElement?> MessageReceived;

        /// <summary>
        /// Gets the session ID this session is bound to.
        /// </summary>
        internal string SessionId => _sessionId;

        /// <summary>
        /// Gets the page proxy ID this session is bound to, if any.
        /// </summary>
        internal string PageProxyId => _pageProxyId;

        /// <summary>
        /// Gets a value indicating whether this session has been disposed.
        /// </summary>
        internal bool IsDisposed => _disposed;

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
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (System.Collections.Generic.KeyValuePair<int, TaskCompletionSource<JsonElement?>> kvp in _callbacks)
            {
                kvp.Value.TrySetException(ClosedSessionException());
            }

            _callbacks.Clear();
        }

        /// <summary>
        /// Sends a WIP command and awaits the result. Throws <see cref="TargetClosedException"/>
        /// if the session or connection has been closed.
        /// </summary>
        /// <param name="method">The protocol method (e.g. <c>"Browser.createPage"</c>).</param>
        /// <param name="parameters">Optional parameters object — serialized via <see cref="JsonSerializer"/>.</param>
        /// <returns>The <c>result</c> element from the response, or <see langword="null"/> if absent.</returns>
        internal Task<JsonElement?> SendAsync(string method, object parameters = null)
            => SendAsync(method, parameters, messageId: null);

        /// <summary>
        /// Sends a WIP command with an optional explicit message id. The explicit id is used
        /// for the <see cref="WKConnection.BrowserCloseMessageId"/> sentinel so the response
        /// can be discarded during shutdown.
        /// </summary>
        /// <param name="method">The protocol method.</param>
        /// <param name="parameters">Optional parameters object.</param>
        /// <param name="messageId">Optional explicit message id. When null, the connection auto-allocates.</param>
        /// <returns>The <c>result</c> element from the response, or <see langword="null"/>. Completes immediately for sentinel ids.</returns>
        internal Task<JsonElement?> SendAsync(string method, object parameters, int? messageId)
        {
            if (_disposed || _connection.IsClosed)
            {
                return Task.FromException<JsonElement?>(ClosedSessionException());
            }

            JsonElement? paramsElement = null;
            if (parameters != null)
            {
                string json = JsonSerializer.Serialize(parameters);
                paramsElement = JsonDocument.Parse(json).RootElement;
            }

            // Sentinel ids (e.g. Playwright.close) are fire-and-forget — the connection
            // discards responses with those ids, so callers must not wait for one. Send
            // directly without registering a callback.
            if (messageId == WKConnection.BrowserCloseMessageId)
            {
                _connection.RawSend(_sessionId, _pageProxyId, method, paramsElement, messageId);
                return Task.FromResult<JsonElement?>(null);
            }

            // Register the callback BEFORE the message goes on the wire. The transport reader
            // runs on its own thread; if we sent first, a fast response (common on Linux CI's
            // quick IPC) could be dispatched to OnMessage and dropped — TryRemove finding no
            // callback — before TryAdd registered it. That lost response left the command
            // hanging until the timeout below. Allocate the id, register, then send.
            int id = messageId ?? _connection.NextMessageId();
            TaskCompletionSource<JsonElement?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _callbacks.TryAdd(id, tcs);

            // Bound the wait: a lost response faults the command with a labelled timeout
            // (naming the method) rather than hanging. Disposed when the response settles.
#pragma warning disable CA2000 // Disposed in the ContinueWith below once the response task settles.
            System.Threading.CancellationTokenSource timeoutCts = new(CommandTimeoutMs);
#pragma warning restore CA2000
            timeoutCts.Token.Register(() =>
            {
                if (_callbacks.TryRemove(id, out TaskCompletionSource<JsonElement?> timedOut))
                {
                    timedOut.TrySetException(new TimeoutException(
                        $"WebKit command '{method}' (id {id}) on session '{_sessionId}' did not respond within {CommandTimeoutMs}ms."));
                }
            });
            _ = tcs.Task.ContinueWith(_ => timeoutCts.Dispose(), TaskScheduler.Default);

            _connection.RawSend(_sessionId, _pageProxyId, method, paramsElement, id);

            return tcs.Task;
        }

        /// <summary>
        /// Dispatches a protocol message to this session: either resolves/rejects a pending
        /// callback (when <c>id</c> is present) or raises <see cref="MessageReceived"/>
        /// (when only <c>method</c> is present).
        /// </summary>
        /// <param name="message">The protocol response or event.</param>
        internal void OnMessage(ProtocolResponse message)
        {
            if (message.Id.HasValue)
            {
                if (_callbacks.TryRemove(message.Id.Value, out TaskCompletionSource<JsonElement?> tcs))
                {
                    if (message.Error != null)
                    {
                        tcs.TrySetException(new PlaywrightSharpException(message.Error.Message ?? "Unknown WebKit protocol error"));
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

        private TargetClosedException ClosedSessionException()
            => ClosedTarget.Exception(DriverMessages.BrowserOrContextClosedExceptionMessage, _closeReason);
    }
}
