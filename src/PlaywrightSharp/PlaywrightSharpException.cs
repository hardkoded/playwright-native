using System;
using System.Runtime.Serialization;

namespace PlaywrightSharp
{
    /// <summary>
    /// Base exception used to identify any exception thrown by PlaywrightSharp.
    /// </summary>
    public class PlaywrightSharpException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightSharpException"/> class.
        /// </summary>
        public PlaywrightSharpException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightSharpException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public PlaywrightSharpException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightSharpException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Inner exception.</param>
        public PlaywrightSharpException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightSharpException"/> class.
        /// </summary>
        /// <param name="info">Info.</param>
        /// <param name="context">Context.</param>
#if NET8_0_OR_GREATER
        [Obsolete("Formatter-based serialization is obsolete", DiagnosticId = "SYSLIB0051")]
#endif
        protected PlaywrightSharpException(SerializationInfo info, StreamingContext context) : base(info, context)
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
