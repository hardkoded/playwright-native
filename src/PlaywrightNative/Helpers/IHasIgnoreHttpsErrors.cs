/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Exposes the ignore-HTTPS-errors flag stored on a browser context.
    /// </summary>
    internal interface IHasIgnoreHttpsErrors
    {
        /// <summary>
        /// Whether <c>NewContextAsync(ignoreHTTPSErrors: true)</c> is set.
        /// </summary>
        bool IgnoreHttpsErrors { get; }
    }
}
