// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;

namespace PlaywrightNative
{
    /// <summary>
    /// Official Node <c>AbortError</c> thrown when an action is cancelled
    /// through <see cref="AbortSignal"/>.
    /// </summary>
    public sealed class AbortError : PlaywrightNativeException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AbortError"/> class.
        /// </summary>
        public AbortError()
            : this("The operation was aborted", cause: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbortError"/> class.
        /// </summary>
        /// <param name="message">Official error message.</param>
        public AbortError(string message)
            : this(message, cause: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbortError"/> class.
        /// </summary>
        /// <param name="message">Official error message.</param>
        /// <param name="cause">
        /// Official <c>error.cause</c>: the abort reason (an
        /// <see cref="System.Exception"/> or a string).
        /// </param>
        public AbortError(string message, object cause)
            : base(message)
        {
            Cause = cause;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbortError"/> class.
        /// </summary>
        /// <param name="message">Official error message.</param>
        /// <param name="innerException">The exception that caused this abort.</param>
        public AbortError(string message, Exception innerException)
            : base(message, innerException)
        {
            Cause = innerException;
        }

        /// <summary>
        /// Official <c>error.cause</c> from <see cref="AbortController.Abort(object)"/>.
        /// </summary>
        public object Cause { get; }

        /// <inheritdoc />
        public override string ToString() => "AbortError: " + Message;

        /// <summary>
        /// Already-aborted action: generic DOM message, reason in
        /// <see cref="Cause"/>.
        /// </summary>
        /// <param name="reason">The abort reason.</param>
        /// <returns>The exception to throw.</returns>
        internal static AbortError AlreadyAborted(object reason)
            => new AbortError("The operation was aborted", reason);

        /// <summary>
        /// Mid-action abort with official <c>apiName: reason</c> and call log.
        /// </summary>
        /// <param name="apiName">Official API name such as <c>locator.click</c>.</param>
        /// <param name="signal">The aborted signal.</param>
        /// <param name="callLog">Optional official call log, including <c>Call log:</c>.</param>
        /// <returns>The exception to throw.</returns>
        internal static AbortError InFlight(string apiName, AbortSignal signal, string callLog = null)
        {
            string reason = AbortSignal.FormatReason(signal?.Reason);
            string message = apiName + ": " + reason;
            if (!string.IsNullOrEmpty(callLog))
            {
                message = message + "\n" + callLog.TrimEnd('\n') + "\n  - operation was aborted: " + reason + "\n";
            }

            return new AbortError(message, signal?.Reason);
        }
    }
}
