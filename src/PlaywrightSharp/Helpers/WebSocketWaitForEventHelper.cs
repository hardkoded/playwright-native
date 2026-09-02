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
    /// Shared waiter for <c>webSocket.waitForEvent</c>.
    /// </summary>
    internal static class WebSocketWaitForEventHelper
    {
        /// <summary>
        /// Waits for the named WebSocket event and boxes the payload.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="eventName">Event name from <see cref="WebSocketEvent"/>.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>The event payload.</returns>
        internal static async Task<object> WaitAsync(IWebSocket socket, string eventName, float? timeout)
        {
            if (string.IsNullOrEmpty(eventName) ||
                !WebSocketEvent.Events.TryGetValue(eventName, out IEvent ev))
            {
                throw new ArgumentException($"WebSocket event '{eventName}' is not supported.");
            }

            switch (ev)
            {
                case PlaywrightEvent<EventArgs> close:
                    return await WaitAsync(socket, close, null, timeout).ConfigureAwait(false);
                case PlaywrightEvent<IWebSocketFrame> frame:
                    return await WaitAsync(socket, frame, null, timeout).ConfigureAwait(false);
                case PlaywrightEvent<string> error:
                    return await WaitAsync(socket, error, null, timeout).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"WebSocket event '{eventName}' is not supported.");
            }
        }

        /// <summary>
        /// Waits for <paramref name="webSocketEvent"/> on <paramref name="socket"/>.
        /// </summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        /// <param name="socket">The socket.</param>
        /// <param name="webSocketEvent">The event to wait for, from <see cref="WebSocketEvent"/>.</param>
        /// <param name="predicate">Optional filter.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>The matching event payload.</returns>
        internal static Task<T> WaitAsync<T>(
            IWebSocket socket,
            PlaywrightEvent<T> webSocketEvent,
            Func<T, bool> predicate,
            float? timeout)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            if (webSocketEvent == null)
            {
                throw new ArgumentNullException(nameof(webSocketEvent));
            }

            Func<T, bool> matches = predicate ?? (_ => true);
            string name = webSocketEvent.Name;

            switch (name)
            {
                case "Close":
                    return WaitMappedAsync<T, IWebSocket>(
                        socket,
                        h => socket.Close += h,
                        h => socket.Close -= h,
                        _ => matches((T)(object)EventArgs.Empty),
                        _ => (T)(object)EventArgs.Empty,
                        timeout,
                        abortOnSocketClose: false);
                case "FrameReceived":
                    return WaitTypedAsync<T, IWebSocketFrame>(
                        socket,
                        h => socket.FrameReceived += h,
                        h => socket.FrameReceived -= h,
                        matches,
                        timeout);
                case "FrameSent":
                    return WaitTypedAsync<T, IWebSocketFrame>(
                        socket,
                        h => socket.FrameSent += h,
                        h => socket.FrameSent -= h,
                        matches,
                        timeout);
                case "SocketError":
                    return WaitTypedAsync<T, string>(
                        socket,
                        h => socket.SocketError += h,
                        h => socket.SocketError -= h,
                        matches,
                        timeout);
                default:
                    throw new ArgumentException($"WebSocket event '{name}' is not supported.");
            }
        }

        private static Task<T> WaitTypedAsync<T, TEvent>(
            IWebSocket socket,
            Action<EventHandler<TEvent>> addHandler,
            Action<EventHandler<TEvent>> removeHandler,
            Func<T, bool> matches,
            float? timeout)
        {
            if (typeof(T) != typeof(TEvent))
            {
                throw new ArgumentException($"WebSocket event payload type is {typeof(TEvent).Name}, not {typeof(T).Name}.");
            }

            return WaitWithAbortAsync(
                socket,
                async () =>
                {
                    TEvent result = await WaitForEventHelper.WaitAsync(
                        addHandler,
                        removeHandler,
                        e => matches((T)(object)e),
                        timeout,
                        "webSocket.waitForEvent",
                        abortOnPageClose: OwnerPage(socket),
                        abortOnPageCrash: true).ConfigureAwait(false);
                    return (T)(object)result;
                },
                abortOnSocketClose: true);
        }

        private static Task<T> WaitMappedAsync<T, TEvent>(
            IWebSocket socket,
            Action<EventHandler<TEvent>> addHandler,
            Action<EventHandler<TEvent>> removeHandler,
            Func<TEvent, bool> matches,
            Func<TEvent, T> map,
            float? timeout,
            bool abortOnSocketClose)
        {
            return WaitWithAbortAsync(
                socket,
                async () =>
                {
                    TEvent result = await WaitForEventHelper.WaitAsync(
                        addHandler,
                        removeHandler,
                        matches,
                        timeout,
                        "webSocket.waitForEvent",
                        abortOnPageClose: OwnerPage(socket),
                        abortOnPageCrash: true).ConfigureAwait(false);
                    return map(result);
                },
                abortOnSocketClose);
        }

        private static async Task<T> WaitWithAbortAsync<T>(
            IWebSocket socket,
            Func<Task<T>> wait,
            bool abortOnSocketClose)
        {
            if (abortOnSocketClose && socket.IsClosed)
            {
                throw new PlaywrightSharpException("Socket closed");
            }

            TaskCompletionSource<T> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnClose(object sender, IWebSocket closedSocket)
            {
                _ = closedSocket;
                IPage page = OwnerPage(socket);
                if (page != null && page.IsClosed)
                {
                    closed.TrySetException(new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage));
                }
                else
                {
                    closed.TrySetException(new PlaywrightSharpException("Socket closed"));
                }
            }

            if (abortOnSocketClose)
            {
                socket.Close += OnClose;
            }

            try
            {
                Task<T> waitTask = wait();
                if (!abortOnSocketClose)
                {
                    return await waitTask.ConfigureAwait(false);
                }

                Task completed = await Task.WhenAny(waitTask, closed.Task).ConfigureAwait(false);
                if (completed == closed.Task)
                {
                    return await closed.Task.ConfigureAwait(false);
                }

                return await waitTask.ConfigureAwait(false);
            }
            finally
            {
                if (abortOnSocketClose)
                {
                    socket.Close -= OnClose;
                }
            }
        }

        private static IPage OwnerPage(IWebSocket socket)
            => socket is IHasOwnerPage host ? host.OwnerPage : null;
    }
}
