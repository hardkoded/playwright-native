/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative
{
    /// <summary>
    /// Official action <c>scroll</c> option (<c>auto</c> or <c>none</c>).
    /// </summary>
    public enum ActionScroll
    {
        /// <summary>
        /// Default. Same as <see cref="Auto"/>.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Scroll the element into view before the action.
        /// </summary>
        Auto,

        /// <summary>
        /// Do not scroll the element into view.
        /// </summary>
        None,
    }
}
