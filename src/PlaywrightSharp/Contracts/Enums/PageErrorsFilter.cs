/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp
{
    /// <summary>
    /// Official Playwright <c>page.pageErrors({ filter })</c>
    /// (v1.59): <c>"all"</c> or <c>"since-navigation"</c>.
    /// </summary>
    public enum PageErrorsFilter
    {
        /// <summary>
        /// Default. Same as <see cref="SinceNavigation"/>.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Return every stored page error.
        /// </summary>
        All,

        /// <summary>
        /// Return only errors thrown after the last committed
        /// main-frame navigation.
        /// </summary>
        SinceNavigation,
    }
}
