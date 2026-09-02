/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>Exposes strict-selector mode on a browser context instance.</summary>
    internal interface IHasStrictSelectors
    {
        /// <summary>Whether strict selector mode is enabled.</summary>
        bool StrictSelectors { get; }
    }
}
