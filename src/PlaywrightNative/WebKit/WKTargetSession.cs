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
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Per-page WIP session targeted at a WebKit inner Target. Outbound commands are
    /// wrapped in <c>Target.sendMessageToTarget</c> on the owning page-proxy session;
    /// inbound responses and events arrive pre-unwrapped from <see cref="WKPage"/> via
    /// <see cref="DispatchInboundMessage(string)"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="WKSession"/>, this session does not put a message on the wire
    /// directly — it serializes an inner protocol envelope and asks the parent session
    /// to deliver it. Message ids are allocated on the shared
    /// <see cref="WKConnection"/> counter so they remain unique across every session.
    /// </remarks>
    internal class WKTargetSession : IDisposable
    {
        private readonly WKSession _parentSession;
        private readonly WKConnection _connection;
        private readonly string _targetId;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>> _callbacks = new();
        private bool _disposed;
        private string _closeReason;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKTargetSession"/> class.
        /// </summary>
        /// <param name="parentSession">The page-proxy session this target session is multiplexed onto.</param>
        /// <param name="connection">The owning connection (used to allocate shared message ids).</param>
        /// <param name="targetId">The WebKit inner target id this session is bound to.</param>
        public WKTargetSession(WKSession parentSession, WKConnection connection, string targetId)
        {
            _parentSession = parentSession ?? throw new ArgumentNullException(nameof(parentSession));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _targetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        }

        /// <summary>
        /// Fired when an event (a message without an <c>id</c>) is received on this session.
        /// Provides the event method name and its <c>params</c>.
        /// </summary>
        internal event Action<string, JsonElement?> MessageReceived;

        /// <summary>
        /// Gets the WebKit inner target id this session is bound to.
        /// </summary>
        internal string TargetId => _targetId;

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
        /// Sends a WIP command targeted at the inner Target. Builds the inner envelope,
        /// wraps it in <c>Target.sendMessageToTarget</c>, and dispatches via the parent
        /// page-proxy session. Returns a task that completes when the inner response
        /// arrives via <see cref="DispatchInboundMessage(string)"/>.
        /// </summary>
        /// <param name="method">The protocol method (e.g. <c>"Page.enable"</c>).</param>
        /// <param name="parameters">Optional parameters object — serialized as the inner <c>params</c>.</param>
        /// <returns>The inner <c>result</c> element from the response, or <see langword="null"/>.</returns>
        internal virtual Task<JsonElement?> SendAsync(string method, object parameters = null)
        {
            if (_disposed || _connection.IsClosed)
            {
                return Task.FromException<JsonElement?>(ClosedSessionException());
            }

            (int id, TaskCompletionSource<JsonElement?> tcs) = EnqueueCommand();
            string innerJson = SerializeInnerMessage(id, method, parameters);

            // Fire-and-forget the outer wrap. We never await the parent's ack — if the wrap
            // fails (e.g. unknown targetId), the inner TCS will be drained by the connection
            // close handler. Mirrors upstream which uses send() without awaiting.
            _ = _parentSession.SendAsync(
                "Target.sendMessageToTarget",
                new { targetId = _targetId, message = innerJson });

            return tcs.Task;
        }

        /// <summary>
        /// Dispatches a raw inner-protocol JSON string (the <c>message</c> field of a
        /// <c>Target.dispatchMessageFromTarget</c> envelope) to this session. The caller —
        /// <see cref="WKPage"/> — is responsible for routing by <c>targetId</c>.
        /// </summary>
        /// <param name="rawJson">The inner JSON message as received on the wire.</param>
        internal void DispatchInboundMessage(string rawJson)
        {
            if (_disposed || string.IsNullOrEmpty(rawJson))
            {
                return;
            }

            ProtocolResponse message = JsonSerializer.Deserialize<ProtocolResponse>(rawJson);

            if (message.Id.HasValue)
            {
                if (_callbacks.TryRemove(message.Id.Value, out TaskCompletionSource<JsonElement?> tcs))
                {
                    if (message.Error != null)
                    {
                        tcs.TrySetException(new PlaywrightNativeException(message.Error.Message ?? "Unknown WebKit protocol error"));
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

        /// <summary>
        /// Allocates a shared connection message id and registers the response waiter.
        /// </summary>
        /// <returns>The id and the task source completed by <see cref="DispatchInboundMessage"/>.</returns>
        protected (int Id, TaskCompletionSource<JsonElement?> Tcs) EnqueueCommand()
        {
            int id = _connection.NextMessageId();
            TaskCompletionSource<JsonElement?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _callbacks.TryAdd(id, tcs);
            return (id, tcs);
        }

        /// <summary>
        /// Serializes an inner Inspector-protocol command envelope.
        /// </summary>
        /// <param name="id">The message id.</param>
        /// <param name="method">The protocol method.</param>
        /// <param name="parameters">Optional parameters.</param>
        /// <returns>The JSON string.</returns>
        protected string CreateInnerMessage(int id, string method, object parameters)
            => SerializeInnerMessage(id, method, parameters);

        private static string SerializeInnerMessage(int id, string method, object parameters)
            => JsonSerializer.Serialize(new InnerMessage
            {
                Id = id,
                Method = method,
                Params = parameters,
            });

        private TargetClosedException ClosedSessionException()
            => ClosedTarget.Exception(DriverMessages.BrowserOrContextClosedExceptionMessage, _closeReason);

        private sealed class InnerMessage
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public int Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("method")]
            public string Method { get; set; }

            // Omit when null so WebKit sees no params field at all (the wire format
            // prefers absence to a literal `params: null`).
            [System.Text.Json.Serialization.JsonPropertyName("params")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public object Params { get; set; }
        }
    }
}
