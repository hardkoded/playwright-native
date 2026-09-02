/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Polls a predicate until it returns true or the timeout elapses.
    /// </summary>
    internal static class ExpectWaiter
    {
        /// <summary>
        /// Repeatedly invokes <paramref name="predicateAsync"/> until it returns
        /// <see langword="true"/>.
        /// </summary>
        /// <param name="predicateAsync">A single-shot check.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="extraMessage">Optional suffix appended to the timeout message.</param>
        /// <returns>A task that completes when the predicate is true.</returns>
        internal static async Task WaitUntilAsync(
            Func<Task<bool>> predicateAsync,
            float? timeout,
            string apiName,
            Func<string> extraMessage = null)
        {
            if (predicateAsync == null)
            {
                throw new ArgumentNullException(nameof(predicateAsync));
            }

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();

            while (true)
            {
                // Always finish the in-flight probe so timeout: 1 still
                // succeeds when the locator already matches (official
                // expect(locator).toBeVisible({ timeout: 1 })).
                if (await predicateAsync().ConfigureAwait(false))
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    string extra = extraMessage == null ? string.Empty : extraMessage();
                    throw new TimeoutException(
                        apiName +
                        ": Timeout " +
                        timeoutMs.ToString(CultureInfo.InvariantCulture) +
                        "ms exceeded." +
                        extra);
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }
    }
}
