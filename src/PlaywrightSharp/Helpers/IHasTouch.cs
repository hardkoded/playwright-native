/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Exposes context <c>hasTouch</c> for official tap gating.
    /// </summary>
    internal interface IHasTouch
    {
        /// <summary>
        /// True when <c>NewContextAsync(hasTouch: true)</c> was set.
        /// </summary>
        bool HasTouch { get; }
    }
}
