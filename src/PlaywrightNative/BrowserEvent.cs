namespace PlaywrightNative
{
    /// <summary>
    /// Browser events for <see cref="IBrowser.WaitForEventAsync{T}"/>.
    /// </summary>
    public static class BrowserEvent
    {
        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowser.Disconnected"/>.
        /// </summary>
        public static PlaywrightEvent<IBrowser> Disconnected { get; } = new PlaywrightEvent<IBrowser>() { Name = "Disconnected" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowser.Context"/>.
        /// </summary>
        public static PlaywrightEvent<IBrowserContext> Context { get; } = new PlaywrightEvent<IBrowserContext>() { Name = "Context" };
    }
}
