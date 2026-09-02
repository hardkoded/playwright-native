/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Maps <see cref="WaitUntilState"/> to the lifecycle event name recorded
    /// by the browser frame trackers.
    /// </summary>
    internal static class WaitUntilMapping
    {
        /// <summary>
        /// Returns the lifecycle event to wait for.
        /// </summary>
        /// <param name="waitUntil">The public wait-until option.</param>
        /// <returns>
        /// <c>commit</c>, <c>DOMContentLoaded</c>, <c>networkidle</c>, or <c>load</c>.
        /// </returns>
        internal static string ToLifecycleEvent(WaitUntilState waitUntil)
        {
            return waitUntil switch
            {
                WaitUntilState.Commit => "commit",
                WaitUntilState.DOMContentLoaded => "DOMContentLoaded",
                WaitUntilState.NetworkIdle => "networkidle",
                _ => "load",
            };
        }
    }
}
