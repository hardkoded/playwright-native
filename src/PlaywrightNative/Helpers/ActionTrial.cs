/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Playwright <c>trial</c> actions run actionability checks without
    /// dispatching the pointer or keyboard event.
    /// </summary>
    internal static class ActionTrial
    {
        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="trial"/> is set.
        /// </summary>
        /// <param name="trial">The trial option.</param>
        /// <returns>Whether this is a dry-run action.</returns>
        internal static bool IsTrial(bool? trial) => trial == true;
    }
}
