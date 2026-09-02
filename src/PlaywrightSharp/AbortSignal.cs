// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Globalization;
using System.Threading.Tasks;

namespace PlaywrightSharp
{
    /// <summary>
    /// Official Playwright <c>AbortSignal</c> for cancelling actions and
    /// web-first assertions.
    /// </summary>
    public sealed class AbortSignal
    {
        private readonly object _gate = new object();
        private readonly TaskCompletionSource<bool> _abortedSource =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets a value indicating whether <see cref="AbortController.Abort(object)"/>
        /// has already been called.
        /// </summary>
        public bool Aborted { get; private set; }

        /// <summary>
        /// Gets the reason passed to <see cref="AbortController.Abort(object)"/>,
        /// or <see langword="null"/> when the signal is still live.
        /// </summary>
        public object Reason { get; private set; }

        /// <summary>
        /// Official abort reason text: an <see cref="Exception"/> uses
        /// <see cref="Exception.Message"/>; any other value is stringified.
        /// </summary>
        internal string ReasonText => FormatReason(Reason);

        /// <summary>
        /// Formats an official abort reason for error messages.
        /// </summary>
        /// <param name="reason">The abort reason.</param>
        /// <returns>The official reason text.</returns>
        internal static string FormatReason(object reason)
        {
            if (reason is Exception exception)
            {
                return string.IsNullOrEmpty(exception.Message) ? "Error" : exception.Message;
            }

            if (reason == null)
            {
                return "This operation was aborted";
            }

            return Convert.ToString(reason, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>
        /// Completes when this signal is aborted.
        /// </summary>
        /// <returns>A completed task when <see cref="Aborted"/> is true.</returns>
        internal Task WhenAbortedAsync() => _abortedSource.Task;

        /// <summary>
        /// Marks this signal aborted. Subsequent calls are ignored.
        /// </summary>
        /// <param name="reason">Official abort reason (error or string).</param>
        internal void Abort(object reason)
        {
            lock (_gate)
            {
                if (Aborted)
                {
                    return;
                }

                Aborted = true;
                Reason = reason;
            }

            _abortedSource.TrySetResult(true);
        }
    }
}
