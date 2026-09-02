/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Shared wait helpers for <see cref="IWebSocket"/>.
    /// </summary>
    internal static class WebSocketWaitHelper
    {
        /// <summary>
        /// Waits for the next received frame.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="predicate">Optional filter.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>The matching frame.</returns>
        internal static Task<IWebSocketFrame> WaitForFrameReceivedAsync(
            IWebSocket socket,
            Func<IWebSocketFrame, bool> predicate,
            float? timeout)
            => WaitForEventHelper.WaitAsync(
                h => socket.FrameReceived += h,
                h => socket.FrameReceived -= h,
                predicate ?? (_ => true),
                timeout,
                "webSocket.waitForEvent");

        /// <summary>
        /// Waits for the next sent frame.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="predicate">Optional filter.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>The matching frame.</returns>
        internal static Task<IWebSocketFrame> WaitForFrameSentAsync(
            IWebSocket socket,
            Func<IWebSocketFrame, bool> predicate,
            float? timeout)
            => WaitForEventHelper.WaitAsync(
                h => socket.FrameSent += h,
                h => socket.FrameSent -= h,
                predicate ?? (_ => true),
                timeout,
                "webSocket.waitForEvent");
    }
}
