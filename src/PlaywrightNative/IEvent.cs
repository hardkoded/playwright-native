using System;

namespace PlaywrightNative
{
    /// <summary>
    /// Events for WaitForEventAsync.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// Event Name.
        /// </summary>
        string Name { get; }
    }
}
