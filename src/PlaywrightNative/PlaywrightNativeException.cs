using System;
using System.Runtime.Serialization;

namespace PlaywrightNative
{
    /// <summary>
    /// Base exception used to identify any exception thrown by PlaywrightNative.
    /// </summary>
    public class PlaywrightNativeException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightNativeException"/> class.
        /// </summary>
        public PlaywrightNativeException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightNativeException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public PlaywrightNativeException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightNativeException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Inner exception.</param>
        public PlaywrightNativeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightNativeException"/> class.
        /// </summary>
        /// <param name="info">Info.</param>
        /// <param name="context">Context.</param>
#if NET8_0_OR_GREATER
        [Obsolete("Formatter-based serialization is obsolete", DiagnosticId = "SYSLIB0051")]
#endif
        protected PlaywrightNativeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        internal static string RewriteErrorMeesage(string message)
            => message.Contains("Cannot find context with specified id") || message.Contains("Inspected target navigated or close")
                ? "Execution context was destroyed, most likely because of a navigation."
                : message;

        /// <summary>
        /// Returns whether <paramref name="ex"/> is a destroyed-context race
        /// that selector waits and clicks should retry.
        /// </summary>
        /// <param name="ex">The exception from a protocol evaluate or query.</param>
        /// <returns><see langword="true"/> when the caller should poll again.</returns>
        internal static bool IsDestroyedContext(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            string message = ex.Message ?? string.Empty;
            return message.Contains("Cannot find context", StringComparison.Ordinal)
                || message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                || message.Contains("Inspected target navigated", StringComparison.Ordinal);
        }
    }
}
