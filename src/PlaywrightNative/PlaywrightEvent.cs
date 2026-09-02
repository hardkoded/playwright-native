using System;

namespace PlaywrightNative
{
    /// <summary>
    /// Events for WaitForEventAsync.
    /// </summary>
    /// <typeparam name="T"><see cref="EventArgs"/> returned by the event.</typeparam>
    public class PlaywrightEvent<T> : IEvent
    {
        /// <inheritdoc/>
        public string Name { get; set; }
    }
}
