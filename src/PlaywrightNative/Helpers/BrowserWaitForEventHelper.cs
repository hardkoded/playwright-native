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
    /// Shared waiter for <c>browser.waitForEvent</c>.
    /// </summary>
    internal static class BrowserWaitForEventHelper
    {
        /// <summary>
        /// Waits for the next <paramref name="browserEvent"/> on <paramref name="browser"/>.
        /// </summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        /// <param name="browser">The browser that raises the event.</param>
        /// <param name="browserEvent">The event to wait for, from <see cref="BrowserEvent"/>.</param>
        /// <param name="predicate">Optional filter. When omitted, the first event resolves the wait.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>The matching event payload.</returns>
        internal static Task<T> WaitAsync<T>(
            IBrowser browser,
            PlaywrightEvent<T> browserEvent,
            Func<T, bool> predicate,
            float? timeout)
        {
            if (browser == null)
            {
                throw new ArgumentNullException(nameof(browser));
            }

            if (browserEvent == null)
            {
                throw new ArgumentNullException(nameof(browserEvent));
            }

            Func<T, bool> matches = predicate ?? (_ => true);
            string name = browserEvent.Name;

            switch (name)
            {
                case "Disconnected":
                    return WaitTypedAsync<T, IBrowser>(
                        h => browser.Disconnected += h,
                        h => browser.Disconnected -= h,
                        matches,
                        timeout);
                case "Context":
                    return WaitTypedAsync<T, IBrowserContext>(
                        h => browser.Context += h,
                        h => browser.Context -= h,
                        matches,
                        timeout);
                default:
                    throw new ArgumentException($"Unknown browser event '{name}'.");
            }
        }

        private static async Task<T> WaitTypedAsync<T, TEvent>(
            Action<EventHandler<TEvent>> addHandler,
            Action<EventHandler<TEvent>> removeHandler,
            Func<T, bool> matches,
            float? timeout)
        {
            if (typeof(T) != typeof(TEvent))
            {
                throw new ArgumentException($"Browser event payload type is {typeof(TEvent).Name}, not {typeof(T).Name}.");
            }

            TEvent result = await WaitForEventHelper.WaitAsync(
                addHandler,
                removeHandler,
                e => matches((T)(object)e),
                timeout,
                "browser.waitForEvent").ConfigureAwait(false);
            return (T)(object)result;
        }
    }
}
