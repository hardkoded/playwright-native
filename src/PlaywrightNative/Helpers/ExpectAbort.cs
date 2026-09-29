// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official expect <c>signal</c> abort: already-aborted fails immediately;
    /// mid-assertion abort fails like a timeout.
    /// </summary>
    internal static class ExpectAbort
    {
        /// <summary>
        /// Throws the official already-aborted expect error when
        /// <paramref name="signal"/> is already aborted.
        /// </summary>
        /// <param name="signal">Optional expect signal.</param>
        /// <param name="header">Official <c>expect(...).method failed</c> line.</param>
        /// <param name="details">Lines after the header, including a trailing newline.</param>
        internal static void ThrowIfAlreadyAborted(AbortSignal signal, string header, string details)
        {
            if (signal == null || !signal.Aborted)
            {
                return;
            }

            throw AlreadyAborted(header, details, signal);
        }

        /// <summary>
        /// Official already-aborted expect failure.
        /// </summary>
        /// <param name="header">Official <c>expect(...).method failed</c> line.</param>
        /// <param name="details">Lines after the header, including a trailing newline.</param>
        /// <param name="signal">The aborted signal.</param>
        /// <returns>The expect exception to throw.</returns>
        internal static ExpectException AlreadyAborted(string header, string details, AbortSignal signal)
        {
            string message = header + "\n\n" + details + "Error: The assertion was aborted: " + signal.ReasonText + "\n";
            return ExpectException.Fail(
                message,
                actual: null,
                expected: null,
                name: string.Empty,
                pass: false,
                timeoutMs: 0,
                ariaSnapshot: null);
        }

        /// <summary>
        /// Whether <paramref name="signal"/> was aborted after the assertion started.
        /// </summary>
        /// <param name="signal">Optional expect signal.</param>
        /// <param name="reason">Official reason text when aborted.</param>
        /// <returns><see langword="true"/> when the assertion should fail like a timeout.</returns>
        internal static bool TryMidAbort(AbortSignal signal, out string reason)
        {
            if (signal == null || !signal.Aborted)
            {
                reason = null;
                return false;
            }

            reason = signal.ReasonText;
            return true;
        }

        /// <summary>
        /// Poll delay that wakes early when <paramref name="signal"/> aborts.
        /// </summary>
        /// <param name="signal">Optional expect signal.</param>
        /// <returns>A task that completes after 50ms or when aborted.</returns>
        internal static Task DelayOrAbortAsync(AbortSignal signal)
            => DelayOrAbortAsync(signal, 50);

        /// <summary>
        /// Upstream <c>retryWithProgressAndBackoff</c> delays:
        /// <c>[20, 50, 100, 100, 500]</c>, each capped by <c>timeout/5</c>.
        /// Dense fixed 50ms polls starve page timers under Windows suite load
        /// (ShouldNotMissElementThatAppearsBetweenRetriesBeforeTheDeadline).
        /// </summary>
        /// <param name="attempt">Zero-based poll index after a failed probe.</param>
        /// <param name="timeoutMs">Expect timeout budget in milliseconds.</param>
        /// <returns>Delay in milliseconds for this attempt.</returns>
        internal static int BackoffDelayMs(int attempt, int timeoutMs)
        {
            int[] delays = { 20, 50, 100, 100, 500 };
            int index = attempt < 0 ? 0 : (attempt >= delays.Length ? delays.Length - 1 : attempt);
            int delay = delays[index];
            if (timeoutMs > 0 && timeoutMs != int.MaxValue)
            {
                int cap = Math.Max(20, timeoutMs / 5);
                if (delay > cap)
                {
                    delay = cap;
                }
            }

            return delay;
        }

        /// <summary>
        /// Backoff poll delay that wakes early when <paramref name="signal"/> aborts.
        /// </summary>
        /// <param name="signal">Optional expect signal.</param>
        /// <param name="attempt">Zero-based poll index after a failed probe.</param>
        /// <param name="timeoutMs">Expect timeout budget in milliseconds.</param>
        /// <returns>A task that completes after the backoff delay or when aborted.</returns>
        internal static Task DelayOrAbortWithBackoffAsync(AbortSignal signal, int attempt, int timeoutMs)
            => DelayOrAbortAsync(signal, BackoffDelayMs(attempt, timeoutMs));

        /// <summary>
        /// Poll delay that wakes early when <paramref name="signal"/> aborts.
        /// </summary>
        /// <param name="signal">Optional expect signal.</param>
        /// <param name="millisecondsDelay">Poll interval in milliseconds.</param>
        /// <returns>A task that completes after the delay or when aborted.</returns>
        internal static Task DelayOrAbortAsync(AbortSignal signal, int millisecondsDelay)
        {
            if (signal == null)
            {
                return Task.Delay(millisecondsDelay);
            }

            if (signal.Aborted)
            {
                return Task.CompletedTask;
            }

            return Task.WhenAny(Task.Delay(millisecondsDelay), signal.WhenAbortedAsync());
        }
    }
}
