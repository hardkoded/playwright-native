namespace PlaywrightSharp
{
    /// <summary>
    /// Official Playwright logger severity.
    /// </summary>
    public enum PlaywrightLogSeverity
    {
        /// <summary>Verbose protocol or internal detail.</summary>
        Verbose,

        /// <summary>API call start and success.</summary>
        Info,

        /// <summary>Recoverable warning.</summary>
        Warning,

        /// <summary>Failure.</summary>
        Error,
    }
}
