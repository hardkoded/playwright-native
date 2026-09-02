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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
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
