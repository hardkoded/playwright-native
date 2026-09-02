/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Maps legacy <see cref="ActionScroll"/> to official <c>scroll</c> option bags.
    /// </summary>
    internal static class ActionScrollBridge
    {
        /// <summary>
        /// Converts <see cref="ActionScroll"/> to the official <see cref="ScrollMode"/> option.
        /// </summary>
        /// <param name="scroll">Legacy scroll option.</param>
        /// <returns><see cref="ScrollMode.None"/> for <see cref="ActionScroll.None"/>; otherwise <see langword="null"/>.</returns>
        internal static ScrollMode? ToScrollOption(ActionScroll scroll)
            => scroll == ActionScroll.None ? ScrollMode.None : null;
    }
}
