/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Synthetic page WebSocket for <see cref="IWebSocketRoute.ConnectToServer"/>
    /// so HAR records the native server-side wire traffic.
    /// </summary>
    internal sealed partial class RoutedHarWebSocket : IWebSocket, IHasHarWebSocket
    {
        internal RoutedHarWebSocket(string url)
        {
            Url = url ?? string.Empty;
        }

        /// <inheritdoc/>
        public event EventHandler<IWebSocket> Close;

        /// <inheritdoc/>
        public event EventHandler<IWebSocketFrame> FrameReceived;

        /// <inheritdoc/>
        public event EventHandler<IWebSocketFrame> FrameSent;

        /// <inheritdoc/>
        public event EventHandler<string> SocketError;

        /// <inheritdoc/>
        public bool IsClosed { get; private set; }

        /// <inheritdoc/>
        public string Url { get; }

        /// <inheritdoc/>
        HarWebSocketState IHasHarWebSocket.Har => Har;

        internal HarWebSocketState Har { get; } = new HarWebSocketState();

        /// <inheritdoc/>
        public Task<object> WaitForEventAsync(string @event, float? timeout = default)
            => WebSocketWaitForEventHelper.WaitAsync(this, @event, timeout);

        /// <inheritdoc/>
        public Task<T> WaitForEventAsync<T>(PlaywrightEvent<T> webSocketEvent, Func<T, bool> predicate = null, float? timeout = null)
            => WebSocketWaitForEventHelper.WaitAsync(this, webSocketEvent, predicate, timeout);

        /// <inheritdoc/>
        public Task<IWebSocketFrame> WaitForFrameReceivedAsync(Func<IWebSocketFrame, bool> predicate = default, float? timeout = default)
            => WebSocketWaitHelper.WaitForFrameReceivedAsync(this, predicate, timeout);

        /// <inheritdoc/>
        public Task<IWebSocketFrame> WaitForFrameSentAsync(Func<IWebSocketFrame, bool> predicate = default, float? timeout = default)
            => WebSocketWaitHelper.WaitForFrameSentAsync(this, predicate, timeout);

        internal void MarkConnected()
        {
            double wall = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Har.ApplyHandshakeRequest(Array.Empty<KeyValuePair<string, string>>(), wall, 0);
            Har.ApplyHandshakeResponse(101, "Switching Protocols", Array.Empty<KeyValuePair<string, string>>());
        }

        internal void NotifyFrameSent(IWebSocketFrame frame)
            => FrameSent?.Invoke(this, frame);

        internal void NotifyFrameReceived(IWebSocketFrame frame)
            => FrameReceived?.Invoke(this, frame);

        internal void NotifyError(string message)
        {
            Har.ApplyFailure(message);
            SocketError?.Invoke(this, message ?? string.Empty);
        }

        internal void NotifyClosed()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            Close?.Invoke(this, this);
        }
    }
}
