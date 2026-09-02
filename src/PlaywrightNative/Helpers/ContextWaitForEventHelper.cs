/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared waiter for <c>browserContext.waitForEvent</c>.
    /// </summary>
    internal static class ContextWaitForEventHelper
    {
        /// <summary>
        /// Waits for the next <paramref name="contextEvent"/> on <paramref name="context"/>.
        /// </summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        /// <param name="context">The context that raises the event.</param>
        /// <param name="contextEvent">The event to wait for, from <see cref="BrowserContextEvent"/>.</param>
        /// <param name="predicate">Optional filter. When omitted, the first event resolves the wait.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>The matching event payload.</returns>
        internal static Task<T> WaitAsync<T>(
            IBrowserContext context,
            PlaywrightEvent<T> contextEvent,
            Func<T, bool> predicate,
            float? timeout)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (contextEvent == null)
            {
                throw new ArgumentNullException(nameof(contextEvent));
            }

            Func<T, bool> matches = predicate ?? (_ => true);
            string name = contextEvent.Name;

            switch (name)
            {
                case "Page":
                    return WaitTypedAsync<T, IPage>(
                        h => context.Page += h,
                        h => context.Page -= h,
                        matches,
                        timeout,
                        context);
                case "Close":
                    return WaitTypedAsync<T, IBrowserContext>(
                        h => context.Close += h,
                        h => context.Close -= h,
                        matches,
                        timeout);
                case "Request":
                    return WaitTypedAsync<T, IRequest>(
                        h => context.Request += h,
                        h => context.Request -= h,
                        matches,
                        timeout);
                case "Response":
                    return WaitTypedAsync<T, IResponse>(
                        h => context.Response += h,
                        h => context.Response -= h,
                        matches,
                        timeout);
                case "RequestFailed":
                    return WaitTypedAsync<T, IRequest>(
                        h => context.RequestFailed += h,
                        h => context.RequestFailed -= h,
                        matches,
                        timeout);
                case "RequestFinished":
                    return WaitTypedAsync<T, IRequest>(
                        h => context.RequestFinished += h,
                        h => context.RequestFinished -= h,
                        matches,
                        timeout);
                case "ServiceWorker":
                    if (context is not IHasBrowserContextExtras extrasServiceWorker)
                    {
                        throw new NotSupportedException("ServiceWorker events require a PlaywrightNative browser context.");
                    }

                    return WaitTypedAsync<T, IWorker>(
                        h => extrasServiceWorker.ServiceWorker += h,
                        h => extrasServiceWorker.ServiceWorker -= h,
                        matches,
                        timeout);
                case "Console":
                    return WaitTypedAsync<T, IConsoleMessage>(
                        h => context.Console += h,
                        h => context.Console -= h,
                        matches,
                        timeout);
                case "Download":
                    return WaitTypedAsync<T, IDownload>(
                        h => context.Download += h,
                        h => context.Download -= h,
                        matches,
                        timeout);
                case "Dialog":
                    return WaitTypedAsync<T, IDialog>(
                        h => context.Dialog += h,
                        h => context.Dialog -= h,
                        matches,
                        timeout);
                case "DialogClosed":
                    if (context is not IHasBrowserContextExtras extrasDialogClosed)
                    {
                        throw new NotSupportedException("DialogClosed events require a PlaywrightNative browser context.");
                    }

                    return WaitTypedAsync<T, IDialog>(
                        h => extrasDialogClosed.DialogClosed += h,
                        h => extrasDialogClosed.DialogClosed -= h,
                        matches,
                        timeout);
                case "PageClose":
                    return WaitTypedAsync<T, IPage>(
                        h => context.PageClose += h,
                        h => context.PageClose -= h,
                        matches,
                        timeout);
                case "PageLoad":
                    return WaitTypedAsync<T, IPage>(
                        h => context.PageLoad += h,
                        h => context.PageLoad -= h,
                        matches,
                        timeout);
                case "FrameAttached":
                    return WaitTypedAsync<T, IFrame>(
                        h => context.FrameAttached += h,
                        h => context.FrameAttached -= h,
                        matches,
                        timeout);
                case "FrameDetached":
                    return WaitTypedAsync<T, IFrame>(
                        h => context.FrameDetached += h,
                        h => context.FrameDetached -= h,
                        matches,
                        timeout);
                case "FrameNavigated":
                    return WaitTypedAsync<T, IFrame>(
                        h => context.FrameNavigated += h,
                        h => context.FrameNavigated -= h,
                        matches,
                        timeout);
                case "WebError":
                    return WaitTypedAsync<T, IWebError>(
                        h => context.WebError += h,
                        h => context.WebError -= h,
                        matches,
                        timeout);
                case "BackgroundPage":
                    return WaitTypedAsync<T, IPage>(
                        h => context.BackgroundPage += h,
                        h => context.BackgroundPage -= h,
                        matches,
                        timeout);
                default:
                    throw new ArgumentException($"Unknown context event '{name}'.");
            }
        }

        private static async Task<T> WaitTypedAsync<T, TEvent>(
            Action<EventHandler<TEvent>> addHandler,
            Action<EventHandler<TEvent>> removeHandler,
            Func<T, bool> matches,
            float? timeout,
            IBrowserContext abortOnClose = null)
        {
            if (typeof(T) != typeof(TEvent))
            {
                throw new ArgumentException($"Context event payload type is {typeof(TEvent).Name}, not {typeof(T).Name}.");
            }

            if (abortOnClose != null && abortOnClose.IsClosed)
            {
                throw new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage);
            }

            TaskCompletionSource<TEvent> closed = null;
            EventHandler<IBrowserContext> onClose = null;
            if (abortOnClose != null)
            {
                closed = new TaskCompletionSource<TEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
                onClose = (_, _) => closed.TrySetException(
                    new TargetClosedException(DriverMessages.BrowserOrContextClosedExceptionMessage));
                abortOnClose.Close += onClose;
            }

            try
            {
                Task<TEvent> waitTask = WaitForEventHelper.WaitAsync(
                    addHandler,
                    removeHandler,
                    e => matches((T)(object)e),
                    timeout,
                    "browserContext.waitForEvent");
                if (closed == null)
                {
                    TEvent result = await waitTask.ConfigureAwait(false);
                    return (T)(object)result;
                }

                Task finished = await Task.WhenAny(waitTask, closed.Task).ConfigureAwait(false);
                if (ReferenceEquals(finished, closed.Task))
                {
                    await closed.Task.ConfigureAwait(false);
                }

                TEvent value = await waitTask.ConfigureAwait(false);
                return (T)(object)value;
            }
            finally
            {
                if (onClose != null)
                {
                    abortOnClose.Close -= onClose;
                }
            }
        }
    }
}
