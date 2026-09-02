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
#pragma warning disable CA1062
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy WebSocket helpers.
    /// </summary>
    public static class WebSocketCompatExtensions
    {
        /// <summary>Wait for a WebSocket event.</summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        public static Task<T> WaitForEventAsync<T>(
            this IWebSocket webSocket,
            PlaywrightEvent<T> webSocketEvent,
            Func<T, bool> predicate = null,
            float? timeout = null)
            => WebSocketWaitForEventHelper.WaitAsync(webSocket, webSocketEvent, predicate, timeout);

        /// <summary>Wait for the next received frame.</summary>
        public static Task<IWebSocketFrame> WaitForFrameReceivedAsync(this IWebSocket webSocket, float? timeout = default)
            => webSocket.WaitForEventAsync(WebSocketEvent.FrameReceived, timeout: timeout);

        /// <summary>Removes a page-level WebSocket route.</summary>
        public static Task UnrouteWebSocketAsync(this IPage page, string url, Action<IWebSocketRoute> handler = null)
            => WebSocketRouter.UnrouteAsync(page, url, handler);

        /// <summary>Removes a page-level WebSocket route.</summary>
        public static Task UnrouteWebSocketAsync(this IPage page, string url, Func<IWebSocketRoute, Task> handler)
            => WebSocketRouter.UnrouteAsync(page, url, handler);

        /// <summary>Removes a context-level WebSocket route.</summary>
        public static Task UnrouteWebSocketAsync(this IBrowserContext context, string url, Action<IWebSocketRoute> handler = null)
            => WebSocketRouter.UnrouteAsync(context, url, handler);

        /// <summary>Removes a context-level WebSocket route.</summary>
        public static Task UnrouteWebSocketAsync(this IBrowserContext context, string url, Func<IWebSocketRoute, Task> handler)
            => WebSocketRouter.UnrouteAsync(context, url, handler);

        /// <summary>Removes a page-level WebSocket route registered with a regex.</summary>
        public static Task UnrouteWebSocketAsync(this IPage page, Regex url, Action<IWebSocketRoute> handler = null)
            => WebSocketRouter.UnrouteAsync(page, url, handler);

        /// <summary>Removes a context-level WebSocket route registered with a regex.</summary>
        public static Task UnrouteWebSocketAsync(this IBrowserContext context, Regex url, Action<IWebSocketRoute> handler = null)
            => WebSocketRouter.UnrouteAsync(context, url, handler);

        /// <summary>Removes a page-level WebSocket route registered with a predicate.</summary>
        public static Task UnrouteWebSocketAsync(this IPage page, Func<string, bool> url, Action<IWebSocketRoute> handler = null)
            => WebSocketRouter.UnrouteAsync(page, url, handler);

        /// <summary>Removes a context-level WebSocket route registered with a predicate.</summary>
        public static Task UnrouteWebSocketAsync(this IBrowserContext context, Func<string, bool> url, Action<IWebSocketRoute> handler = null)
            => WebSocketRouter.UnrouteAsync(context, url, handler);

        /// <summary>Wait for a WebSocket event by name.</summary>
        public static Task<object> WaitForEventAsync(this IWebSocket webSocket, string eventName, float? timeout = null)
        {
            switch (eventName.ToUpperInvariant())
            {
                case "CLOSE":
                    return WaitForEventAsync<EventArgs>(webSocket, WebSocketEvent.Close, timeout: timeout)
                        .ContinueWith(t => (object)t.Result, TaskScheduler.Default);
                case "FRAMERECEIVED":
                    return WaitForEventAsync<IWebSocketFrame>(webSocket, WebSocketEvent.FrameReceived, timeout: timeout)
                        .ContinueWith(t => (object)t.Result, TaskScheduler.Default);
                case "FRAMESENT":
                    return WaitForEventAsync<IWebSocketFrame>(webSocket, WebSocketEvent.FrameSent, timeout: timeout)
                        .ContinueWith(t => (object)t.Result, TaskScheduler.Default);
                case "SOCKETERROR":
                    return WaitForEventAsync<string>(webSocket, WebSocketEvent.SocketError, timeout: timeout)
                        .ContinueWith(t => (object)t.Result, TaskScheduler.Default);
                default:
                    throw new System.ArgumentException($"Unknown webSocket event '{eventName}'.");
            }
        }

        /// <summary>Legacy WebSocket route close with code and reason.</summary>
        public static Task CloseAsync(this IWebSocketRoute route, int? code = default, string reason = default)
            => route is Helpers.WebSocketRoute concrete
                ? concrete.CloseAsync(code, reason)
                : route.CloseAsync(new WebSocketRouteCloseOptions { Code = code, Reason = reason });

        /// <summary>Wait for a WebSocket event by name.</summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static Task<T> WaitForEventAsync<T>(
            this IWebSocket webSocket,
            string eventName,
            float? timeout = null)
        {
            PlaywrightEvent<T> webSocketEvent = eventName switch
            {
                "close" or "Close" => (PlaywrightEvent<T>)(object)WebSocketEvent.Close,
                "framereceived" or "FrameReceived" => (PlaywrightEvent<T>)(object)WebSocketEvent.FrameReceived,
                "framesent" or "FrameSent" => (PlaywrightEvent<T>)(object)WebSocketEvent.FrameSent,
                "socketerror" or "SocketError" => (PlaywrightEvent<T>)(object)WebSocketEvent.SocketError,
                _ => throw new System.ArgumentException($"Unknown webSocket event '{eventName}'."),
            };

            return webSocket.WaitForEventAsync(webSocketEvent, timeout: timeout);
        }
    }
}
