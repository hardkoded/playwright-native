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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared waiter for <c>page.waitForEvent</c>. Subscribes to the matching
    /// <see cref="IPage"/> event until the optional predicate matches or the
    /// timeout elapses.
    /// </summary>
    internal static class PageWaitForEventHelper
    {
        /// <summary>
        /// Waits for the next <paramref name="pageEvent"/> on <paramref name="page"/>.
        /// </summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        /// <param name="page">The page that raises the event.</param>
        /// <param name="pageEvent">The event to wait for, from <see cref="PageEvent"/>.</param>
        /// <param name="predicate">Optional filter. When omitted, the first event resolves the wait.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>The matching event payload.</returns>
        internal static Task<T> WaitAsync<T>(
            IPage page,
            PlaywrightEvent<T> pageEvent,
            Func<T, Task<bool>> predicate,
            float? timeout)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (pageEvent == null)
            {
                throw new ArgumentNullException(nameof(pageEvent));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            if (string.Equals(pageEvent.Name, "Response", StringComparison.Ordinal))
            {
                return WaitResponseAsync(page, predicate, timeout);
            }

            throw new ArgumentException("Async waitForEvent predicates are supported for Response.");
        }

        internal static Task<T> WaitAsync<T>(
            IPage page,
            PlaywrightEvent<T> pageEvent,
            Func<T, bool> predicate,
            float? timeout)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (pageEvent == null)
            {
                throw new ArgumentNullException(nameof(pageEvent));
            }

            Func<T, bool> matches = predicate ?? (_ => true);
            string name = pageEvent.Name;
            timeout ??= page.DefaultTimeout();

            switch (name)
            {
                case "Console":
                    // Subscribe before ActionTrace yields. evaluate() also yields before
                    // its body; if waitForEvent delayed subscribe until after that yield,
                    // console.log from a racing evaluate could fire with no listener
                    // (tracing "should not emit after w/o before" hangs 30s).
                    Task<T> consoleWait = WaitTypedAsync<T, IConsoleMessage>(
                        page,
                        h => page.Console += h,
                        h => page.Console -= h,
                        matches,
                        timeout);
                    return ActionTrace.RunAsync(
                        page.Context,
                        "Wait for event \"console\"",
                        "Page",
                        "waitForEvent",
                        () => consoleWait);
                case "Dialog":
                    // Resolve inline so waitForEvent('dialog') completes during
                    // RaiseDialog (same as Load). Deferred predicate ContinueWith
                    // can starve under macOS suite load while alert blocks evaluate
                    // (ContextWaitForDialogShouldResolveOnAlert 30s empty stack).
                    // Replay OpenDialog after subscribe when ScheduleOpen deferred
                    // the raise past the waiter attach.
                    return WaitTypedAsync<T, IDialog>(
                        page,
                        h => page.Dialog += h,
                        h => page.Dialog -= h,
                        matches,
                        timeout,
                        existingAfterSubscribe: () =>
                        {
                            if (page is IHasPageExtras extras)
                            {
                                IDialog open = extras.TryGetOpenDialog();
                                if (open != null)
                                {
                                    extras.TryMarkOpenDialogEmitted();
                                    return Task.FromResult<IReadOnlyList<T>>(
                                        (IReadOnlyList<T>)(object)new IDialog[] { open });
                                }
                            }

                            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
                        },
                        deferPredicateEvaluation: false);
                case "DialogClosed":
                    if (page is not IHasPageExtras extras)
                    {
                        throw new NotSupportedException("DialogClosed events require a PlaywrightNative page.");
                    }

                    return WaitTypedAsync<T, IDialog>(
                        page,
                        h => extras.DialogClosed += h,
                        h => extras.DialogClosed -= h,
                        matches,
                        timeout);
                case "Download":
                    return WaitTypedAsync<T, IDownload>(
                        page,
                        h => page.Download += h,
                        h => page.Download -= h,
                        matches,
                        timeout);
                case "FileChooser":
                    return WaitTypedAsync<T, IFileChooser>(
                        page,
                        h => page.FileChooser += h,
                        h => page.FileChooser -= h,
                        matches,
                        timeout);
                case "Request":
                    return WaitTypedAsync<T, IRequest>(
                        page,
                        h => page.Request += h,
                        h => page.Request -= h,
                        matches,
                        timeout,
                        "request");
                case "RequestFinished":
                    return WaitTypedAsync<T, IRequest>(
                        page,
                        h => page.RequestFinished += h,
                        h => page.RequestFinished -= h,
                        matches,
                        timeout);
                case "RequestFailed":
                    return WaitTypedAsync<T, IRequest>(
                        page,
                        h => page.RequestFailed += h,
                        h => page.RequestFailed -= h,
                        matches,
                        timeout);
                case "Response":
                    return WaitTypedAsync<T, IResponse>(
                        page,
                        h => page.Response += h,
                        h => page.Response -= h,
                        matches,
                        timeout);
                case "Close":
                    return WaitTypedAsync<T, IPage>(
                        page,
                        h => page.Close += h,
                        h => page.Close -= h,
                        matches,
                        timeout,
                        abortOnClose: false);
                case "Popup":
                    return WaitTypedAsync<T, IPage>(
                        page,
                        h => page.Popup += h,
                        h => page.Popup -= h,
                        matches,
                        timeout);
                case "FrameNavigated":
                    return WaitTypedAsync<T, IFrame>(
                        page,
                        h => page.FrameNavigated += h,
                        h => page.FrameNavigated -= h,
                        matches,
                        timeout);
                case "FrameAttached":
                    return WaitTypedAsync<T, IFrame>(
                        page,
                        h => page.FrameAttached += h,
                        h => page.FrameAttached -= h,
                        matches,
                        timeout);
                case "FrameDetached":
                    return WaitTypedAsync<T, IFrame>(
                        page,
                        h => page.FrameDetached += h,
                        h => page.FrameDetached -= h,
                        matches,
                        timeout);
                case "PageError":
                    // IPage.PageError is EventHandler<string> (Microsoft API), while
                    // PageEvent.PageError is typed as PageErrorEventArgs for callers.
                    if (typeof(T) == typeof(string))
                    {
                        return WaitTypedAsync<T, string>(
                            page,
                            h => page.PageError += h,
                            h => page.PageError -= h,
                            matches,
                            timeout);
                    }

                    if (typeof(T) != typeof(PageErrorEventArgs))
                    {
                        throw new ArgumentException(
                            $"Page event payload type is String, not {typeof(T).Name}.");
                    }

                    return WaitPageErrorAsArgsAsync(page, matches, timeout);
                case "Load":
                    // Resolve inline so waitForEvent('load') posts its RCA continuation
                    // before waitForLoadState's LifecycleChanged handler (autowait
                    // route|load|clickload). Continuations stay RCA so protocol I/O
                    // from the awaiter cannot block the CDP/WebKit read loop.
                    return WaitTypedAsync<T, IPage>(
                        page,
                        h => page.Load += h,
                        h => page.Load -= h,
                        matches,
                        timeout,
                        deferPredicateEvaluation: false);
                case "DOMContentLoaded":
                    return WaitTypedAsync<T, IPage>(
                        page,
                        h => page.DOMContentLoaded += h,
                        h => page.DOMContentLoaded -= h,
                        matches,
                        timeout,
                        deferPredicateEvaluation: false);
                case "Worker":
                    return WaitTypedAsync<T, IWorker>(
                        page,
                        h => page.Worker += h,
                        h => page.Worker -= h,
                        matches,
                        timeout);
                case "WebSocket":
                    return WaitTypedAsync<T, IWebSocket>(
                        page,
                        h => page.WebSocket += h,
                        h => page.WebSocket -= h,
                        matches,
                        timeout);
                case "Crash":
                    // crash() then waitForEvent('crash') races: WebKit can raise Crash
                    // before the waiter subscribes. Replay after subscribe when already
                    // crashed (same pattern as other existingAfterSubscribe waits).
                    return WaitTypedAsync<T, IPage>(
                        page,
                        h => page.Crash += h,
                        h => page.Crash -= h,
                        matches,
                        timeout,
                        abortOnPageCrash: false,
                        existingAfterSubscribe: () =>
                        {
                            if (page is IHasPageExtras extras && extras.HasCrashed)
                            {
                                return Task.FromResult<IReadOnlyList<T>>(
                                    (IReadOnlyList<T>)(object)new IPage[] { page });
                            }

                            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
                        });
                default:
                    throw new ArgumentException($"Unknown page event '{name}'.");
            }
        }

        private static async Task<T> WaitPageErrorAsArgsAsync<T>(
            IPage page,
            Func<T, bool> matches,
            float? timeout)
        {
            string message = await WaitForEventHelper.WaitAsync<string>(
                h => page.PageError += h,
                h => page.PageError -= h,
                raw =>
                {
                    PageErrorEventArgs args = PageErrorText.Parse(raw);
                    return matches == null || matches((T)(object)args);
                },
                timeout,
                "page.waitForEvent",
                waitForEventName: "PageError",
                abortOnPageClose: page,
                abortOnPageCrash: true).ConfigureAwait(false);
            return (T)(object)PageErrorText.Parse(message);
        }

        private static async Task<T> WaitTypedAsync<T, TEvent>(
            IPage page,
            Action<EventHandler<TEvent>> addHandler,
            Action<EventHandler<TEvent>> removeHandler,
            Func<T, bool> matches,
            float? timeout,
            string waitForEventName = null,
            bool abortOnClose = true,
            bool abortOnPageCrash = true,
            Func<Task<IReadOnlyList<T>>> existingAfterSubscribe = null,
            bool deferPredicateEvaluation = true)
        {
            if (typeof(T) != typeof(TEvent))
            {
                throw new ArgumentException($"Page event payload type is {typeof(TEvent).Name}, not {typeof(T).Name}.");
            }

            Func<Task<IReadOnlyList<TEvent>>> existing = null;
            if (existingAfterSubscribe != null)
            {
                existing = async () =>
                {
                    IReadOnlyList<T> items = await existingAfterSubscribe().ConfigureAwait(false);
                    return (IReadOnlyList<TEvent>)items;
                };
            }

            TEvent result = await WaitForEventHelper.WaitAsync(
                addHandler,
                removeHandler,
                e => matches((T)(object)e),
                timeout,
                "page.waitForEvent",
                waitForEventName: waitForEventName,
                abortOnPageClose: abortOnClose ? page : null,
                abortOnPageCrash: abortOnPageCrash,
                existingAfterSubscribe: existing,
                deferPredicateEvaluation: deferPredicateEvaluation).ConfigureAwait(false);
            return (T)(object)result;
        }

        private static async Task<T> WaitResponseAsync<T>(IPage page, Func<T, Task<bool>> predicate, float? timeout)
        {
            IResponse result = await WaitForEventHelper.WaitAsync<IResponse>(
                h => page.Response += h,
                h => page.Response -= h,
                response => predicate((T)(object)response),
                timeout ?? page.DefaultTimeout(),
                "page.waitForEvent",
                abortOnPageClose: page,
                abortOnPageCrash: true).ConfigureAwait(false);
            return (T)(object)result;
        }
    }
}
