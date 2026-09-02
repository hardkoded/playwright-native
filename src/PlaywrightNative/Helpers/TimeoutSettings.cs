/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Resolves Playwright-style timeouts: an explicit option wins, otherwise 30s.
    /// Passing <c>0</c> disables the timeout (infinite wait).
    /// </summary>
    internal static class TimeoutSettings
    {
        internal const int DefaultTimeoutMs = 30_000;

        private static int _expectTimeoutMs = DefaultTimeoutMs;

        /// <summary>
        /// Sets the default used by <see cref="ExpectTimeoutMs"/> when no per-call
        /// timeout is provided. <paramref name="timeout"/> of <c>0</c> or less
        /// disables the timeout.
        /// </summary>
        /// <param name="timeout">Default expect timeout in milliseconds.</param>
        internal static void SetExpectTimeout(float timeout)
        {
            _expectTimeoutMs = timeout <= 0 ? Timeout.Infinite : (int)timeout;
        }

        /// <summary>
        /// Resolves a timeout option to milliseconds suitable for
        /// <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="timeout">Explicit timeout in milliseconds, or <see langword="null"/> for the default.</param>
        /// <returns>
        /// The resolved timeout. <see cref="Timeout.Infinite"/> when <paramref name="timeout"/> is 0.
        /// </returns>
        internal static int TimeoutMs(float? timeout)
        {
            if (!timeout.HasValue)
            {
                return DefaultTimeoutMs;
            }

            if (timeout.Value <= 0)
            {
                return Timeout.Infinite;
            }

            return (int)timeout.Value;
        }

        /// <summary>
        /// Resolves an expect timeout. The configured expect default is used when
        /// <paramref name="timeout"/> is omitted.
        /// </summary>
        /// <param name="timeout">Explicit timeout in milliseconds, or <see langword="null"/> for the expect default.</param>
        /// <returns>
        /// The resolved timeout. <see cref="Timeout.Infinite"/> when the value is 0.
        /// </returns>
        internal static int ExpectTimeoutMs(float? timeout)
        {
            if (!timeout.HasValue)
            {
                return _expectTimeoutMs;
            }

            if (timeout.Value <= 0)
            {
                return Timeout.Infinite;
            }

            return (int)timeout.Value;
        }
    }
}
