using System;

namespace PlaywrightSharp
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
