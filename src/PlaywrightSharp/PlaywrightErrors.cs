using System;

namespace PlaywrightSharp
{
    /// <summary>
    /// Official <c>playwright.errors</c>.
    /// </summary>
    public sealed class PlaywrightErrors
    {
        /// <summary>
        /// Official <c>playwright.errors.TimeoutError</c> constructor.
        /// <c>String(playwright.errors.TimeoutError)</c> contains
        /// <c>TimeoutError</c>.
        /// </summary>
        public Type TimeoutError { get; } = typeof(TimeoutError);
    }
}
