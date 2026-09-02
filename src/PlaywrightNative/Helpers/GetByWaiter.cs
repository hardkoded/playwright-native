/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Polls a query until a matching element handle appears or the timeout elapses.
    /// </summary>
    internal static class GetByWaiter
    {
        /// <summary>
        /// Repeatedly invokes <paramref name="queryOnceAsync"/> until it returns a non-null handle.
        /// </summary>
        /// <typeparam name="THandle">The element-handle type.</typeparam>
        /// <param name="queryOnceAsync">A single-shot query that returns null when nothing matches.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <returns>The first non-null handle.</returns>
        internal static async Task<THandle> WaitAsync<THandle>(
            Func<Task<THandle>> queryOnceAsync,
            float? timeout,
            string apiName)
            where THandle : class
        {
            if (queryOnceAsync == null)
            {
                throw new ArgumentNullException(nameof(queryOnceAsync));
            }

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();

            while (true)
            {
                THandle handle;
                try
                {
                    handle = await queryOnceAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (PlaywrightNativeException.IsDestroyedContext(ex) || ClickAction.IsRetryable(ex))
                {
                    handle = null;
                }

                if (handle != null)
                {
                    return handle;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException($"{apiName}: Timeout {timeoutMs}ms exceeded.");
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }
    }
}
