namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Stores the official Playwright <c>logger</c> for API-call wrapping.
    /// </summary>
    internal interface IHasPlaywrightLogger
    {
        /// <summary>
        /// Official logger, or <see langword="null"/>.
        /// </summary>
        IPlaywrightLogger Logger { get; set; }
    }
}
