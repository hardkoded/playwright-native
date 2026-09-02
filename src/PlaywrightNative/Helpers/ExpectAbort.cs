// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

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
        {
            if (signal == null)
            {
                return Task.Delay(50);
            }

            if (signal.Aborted)
            {
                return Task.CompletedTask;
            }

            return Task.WhenAny(Task.Delay(50), signal.WhenAbortedAsync());
        }
    }
}
