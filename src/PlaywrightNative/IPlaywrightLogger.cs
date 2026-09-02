namespace PlaywrightNative
{
    /// <summary>
    /// Official Playwright <c>logger</c> sink for
    /// <c>browserType.launch</c> and <c>browser.newContext</c>.
    /// </summary>
    public interface IPlaywrightLogger
    {
        /// <summary>
        /// Returns whether <paramref name="name"/> at <paramref name="severity"/>
        /// should be recorded.
        /// </summary>
        /// <param name="name">Official log channel, for example <c>api</c>.</param>
        /// <param name="severity">Official severity.</param>
        /// <returns><see langword="true"/> when <see cref="Log"/> should run.</returns>
        bool IsEnabled(string name, PlaywrightLogSeverity severity);

        /// <summary>
        /// Records one official log line.
        /// </summary>
        /// <param name="name">Official log channel.</param>
        /// <param name="severity">Official severity.</param>
        /// <param name="message">Official message, for example <c>browser.newContext started</c>.</param>
        void Log(string name, PlaywrightLogSeverity severity, string message);
    }
}
