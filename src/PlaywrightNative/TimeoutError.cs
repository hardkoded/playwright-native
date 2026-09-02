using System;

namespace PlaywrightNative
{
    /// <summary>
    /// Official <c>playwright.errors.TimeoutError</c>. Emitted when an
    /// operation exceeds its timeout.
    /// </summary>
    public class TimeoutError : TimeoutException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutError"/> class.
        /// </summary>
        public TimeoutError()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutError"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public TimeoutError(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutError"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Inner exception.</param>
        public TimeoutError(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
