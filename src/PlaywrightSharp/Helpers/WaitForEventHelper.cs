/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Waits for the next event that satisfies a predicate. Used by
    /// <c>page.waitForRequest</c> / <c>page.waitForResponse</c>.
    /// </summary>
    internal static class WaitForEventHelper
    {
        /// <summary>
        /// Subscribes to <paramref name="addHandler"/> until <paramref name="matches"/>
        /// returns true or the timeout elapses.
        /// </summary>
        /// <typeparam name="T">The event argument type.</typeparam>
        /// <param name="addHandler">Adds the event handler.</param>
        /// <param name="removeHandler">Removes the event handler.</param>
        /// <param name="matches">Predicate for the desired event payload.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="waitingLog">Optional official <c>waiting for …</c> timeout line.</param>
        /// <param name="waitForEventName">
        /// Official lowercase event name. When set, the timeout text is
        /// <c>Timeout Nms exceeded while waiting for event "name"</c>.
        /// </param>
        /// <param name="abortOnPageClose">
        /// When set, page close rejects the wait with the official target-closed
        /// error (Node <c>rejectOnEvent(page, 'close')</c>).
        /// </param>
        /// <param name="abortOnPageCrash">
        /// When set, page crash rejects the wait with official <c>Page crashed</c>.
        /// </param>
        /// <param name="existingAfterSubscribe">
        /// Optional snapshot of events that may have arrived before the
        /// handler was attached. Replayed after subscribe so
        /// <c>evaluate</c>-then-<c>waitForEvent</c> matches Node's event loop.
        /// </param>
        /// <param name="cancellationToken">Cancels the wait (Node <c>signal</c>).</param>
        /// <returns>The matching event payload.</returns>
        internal static Task<T> WaitAsync<T>(
            Action<EventHandler<T>> addHandler,
            Action<EventHandler<T>> removeHandler,
            Func<T, bool> matches,
            float? timeout,
            string apiName,
            string waitingLog = null,
            string waitForEventName = null,
            IPage abortOnPageClose = null,
            bool abortOnPageCrash = false,
            Func<Task<IReadOnlyList<T>>> existingAfterSubscribe = null,
            CancellationToken cancellationToken = default)
        {
            if (addHandler == null)
            {
                throw new ArgumentNullException(nameof(addHandler));
            }

            if (removeHandler == null)
            {
                throw new ArgumentNullException(nameof(removeHandler));
            }

            if (matches == null)
            {
                throw new ArgumentNullException(nameof(matches));
            }

            TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object sender, T payload)
            {
                if (tcs.Task.IsCompleted)
                {
                    return;
                }

                if (!matches(payload))
                {
                    return;
                }

                removeHandler(Handler);
                tcs.TrySetResult(payload);
            }

            addHandler(Handler);
            return ReplayAndWaitAsync(
                tcs,
                Handler,
                removeHandler,
                timeout,
                apiName,
                waitingLog,
                waitForEventName,
                abortOnPageClose,
                abortOnPageCrash,
                existingAfterSubscribe,
                cancellationToken);
        }

        /// <summary>
        /// Subscribes until an async <paramref name="matches"/> returns true or the timeout elapses.
        /// The handler is removed as soon as a match is accepted so later events do not
        /// re-enter the predicate (official <c>sync predicate should be only called once</c>).
        /// </summary>
        /// <typeparam name="T">The event argument type.</typeparam>
        /// <param name="addHandler">Adds the event handler.</param>
        /// <param name="removeHandler">Removes the event handler.</param>
        /// <param name="matches">Async predicate for the desired event payload.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="waitingLog">Optional official <c>waiting for …</c> timeout line.</param>
        /// <param name="waitForEventName">
        /// Official lowercase event name. When set, the timeout text is
        /// <c>Timeout Nms exceeded while waiting for event "name"</c>.
        /// </param>
        /// <param name="abortOnPageClose">
        /// When set, page close rejects the wait with the official target-closed
        /// error (Node <c>rejectOnEvent(page, 'close')</c>).
        /// </param>
        /// <param name="abortOnPageCrash">
        /// When set, page crash rejects the wait with official <c>Page crashed</c>.
        /// </param>
        /// <param name="cancellationToken">Cancels the wait (Node <c>signal</c>).</param>
        /// <returns>The matching event payload.</returns>
        internal static Task<T> WaitAsync<T>(
            Action<EventHandler<T>> addHandler,
            Action<EventHandler<T>> removeHandler,
            Func<T, Task<bool>> matches,
            float? timeout,
            string apiName,
            string waitingLog = null,
            string waitForEventName = null,
            IPage abortOnPageClose = null,
            bool abortOnPageCrash = false,
            CancellationToken cancellationToken = default)
        {
            if (addHandler == null)
            {
                throw new ArgumentNullException(nameof(addHandler));
            }

            if (removeHandler == null)
            {
                throw new ArgumentNullException(nameof(removeHandler));
            }

            if (matches == null)
            {
                throw new ArgumentNullException(nameof(matches));
            }

            TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object sender, T payload)
            {
                if (tcs.Task.IsCompleted)
                {
                    return;
                }

                _ = MatchAsync(payload);
            }

            async Task MatchAsync(T payload)
            {
                try
                {
                    if (tcs.Task.IsCompleted)
                    {
                        return;
                    }

                    if (!await matches(payload).ConfigureAwait(false))
                    {
                        return;
                    }

                    removeHandler(Handler);
                    tcs.TrySetResult(payload);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            addHandler(Handler);
            return AwaitMatchAsync(tcs, removeHandler, Handler, timeout, apiName, waitingLog, waitForEventName, abortOnPageClose, abortOnPageCrash, cancellationToken);
        }

        /// <summary>
        /// Official <c>waiting for response …</c> timeout log line.
        /// </summary>
        /// <param name="urlString">Glob or exact URL.</param>
        /// <param name="urlRegex">Regular expression matcher.</param>
        /// <returns>The log line, or <see langword="null"/> for a predicate-only wait.</returns>
        internal static string ResponseWaitingLog(string urlString, Regex urlRegex)
            => WaitingLog("response", urlString, urlRegex);

        /// <summary>
        /// Official <c>waiting for request …</c> timeout log line
        /// (<c>trimUrl</c> / <c>trimStringWithEllipsis(..., 50)</c>).
        /// </summary>
        /// <param name="urlString">Glob or exact URL.</param>
        /// <param name="urlRegex">Regular expression matcher.</param>
        /// <returns>The log line, or <see langword="null"/> for a predicate-only wait.</returns>
        internal static string RequestWaitingLog(string urlString, Regex urlRegex)
            => WaitingLog("request", urlString, urlRegex);

        /// <summary>
        /// Official <c>trimStringWithEllipsis</c>: cap includes the ellipsis.
        /// </summary>
        /// <param name="input">The string to trim.</param>
        /// <param name="cap">Maximum length including the ellipsis.</param>
        /// <returns>The original string, or a prefix plus <c>…</c>.</returns>
        internal static string TrimStringWithEllipsis(string input, int cap)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= cap)
            {
                return input ?? string.Empty;
            }

            return string.Concat(input.AsSpan(0, cap - 1), "\u2026");
        }

        private static string WaitingLog(string kind, string urlString, Regex urlRegex)
        {
            if (!string.IsNullOrEmpty(urlString))
            {
                return "waiting for " + kind + " \"" + TrimStringWithEllipsis(urlString, 50) + "\"";
            }

            if (urlRegex != null)
            {
                string flags = (urlRegex.Options & RegexOptions.IgnoreCase) != 0 ? "i" : string.Empty;
                return "waiting for " + kind + " /" + TrimStringWithEllipsis(urlRegex.ToString(), 50) + "/" + flags;
            }

            return null;
        }

        private static async Task<T> ReplayAndWaitAsync<T>(
            TaskCompletionSource<T> tcs,
            EventHandler<T> handler,
            Action<EventHandler<T>> removeHandler,
            float? timeout,
            string apiName,
            string waitingLog,
            string waitForEventName,
            IPage abortOnPageClose,
            bool abortOnPageCrash,
            Func<Task<IReadOnlyList<T>>> existingAfterSubscribe,
            CancellationToken cancellationToken)
        {
            if (existingAfterSubscribe != null)
            {
                IReadOnlyList<T> existing = await existingAfterSubscribe().ConfigureAwait(false);
                if (existing != null)
                {
                    foreach (T item in existing)
                    {
                        handler(null, item);
                        if (tcs.Task.IsCompleted)
                        {
                            break;
                        }
                    }
                }
            }

            return await AwaitMatchAsync(
                tcs,
                removeHandler,
                handler,
                timeout,
                apiName,
                waitingLog,
                waitForEventName,
                abortOnPageClose,
                abortOnPageCrash,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<T> AwaitMatchAsync<T>(
            TaskCompletionSource<T> tcs,
            Action<EventHandler<T>> removeHandler,
            EventHandler<T> handler,
            float? timeout,
            string apiName,
            string waitingLog,
            string waitForEventName,
            IPage abortOnPageClose,
            bool abortOnPageCrash,
            CancellationToken cancellationToken)
        {
            CancellationTokenRegistration registration = default;
            EventHandler<IPage> closeHandler = null;
            EventHandler<IPage> crashHandler = null;
            try
            {
                if (abortOnPageClose != null)
                {
                    if (abortOnPageClose.IsClosed)
                    {
                        throw TargetClosedOnWait();
                    }

                    closeHandler = (_, _) => tcs.TrySetException(TargetClosedOnWait());
                    abortOnPageClose.Close += closeHandler;
                }

                if (abortOnPageCrash && abortOnPageClose != null)
                {
                    crashHandler = (_, _) => tcs.TrySetException(new PlaywrightSharpException("Page crashed"));
                    abortOnPageClose.Crash += crashHandler;
                }

                if (cancellationToken.CanBeCanceled)
                {
                    registration = cancellationToken.Register(
                        () => tcs.TrySetException(new OperationCanceledException(cancellationToken)));
                    cancellationToken.ThrowIfCancellationRequested();
                }

                int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
                if (timeoutMs == Timeout.Infinite)
                {
                    return await tcs.Task.ConfigureAwait(false);
                }

                Task delay = Task.Delay(timeoutMs, cancellationToken);
                Task completed = await Task.WhenAny(tcs.Task, delay).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    if (cancellationToken.IsCancellationRequested || delay.IsCanceled)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    await delay.ConfigureAwait(false);
                    throw TimeoutError(apiName, timeoutMs, waitingLog, waitForEventName);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            finally
            {
                if (closeHandler != null && abortOnPageClose != null)
                {
                    abortOnPageClose.Close -= closeHandler;
                }

                if (crashHandler != null && abortOnPageClose != null)
                {
                    abortOnPageClose.Crash -= crashHandler;
                }

                await registration.DisposeAsync().ConfigureAwait(false);
                removeHandler(handler);
            }
        }

        private static TargetClosedException TargetClosedOnWait()
            => new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage);

        private static TimeoutException TimeoutError(string apiName, int timeoutMs, string waitingLog, string waitForEventName)
        {
            string timeoutText = timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string message;
            if (!string.IsNullOrEmpty(waitForEventName))
            {
                message = apiName + ": Timeout " + timeoutText + "ms exceeded while waiting for event \"" + waitForEventName + "\"";
            }
            else
            {
                message = apiName + ": Timeout " + timeoutText + "ms exceeded.";
            }

            if (!string.IsNullOrEmpty(waitingLog))
            {
                message += System.Environment.NewLine + waitingLog;
            }

            return new TimeoutException(message);
        }
    }
}
