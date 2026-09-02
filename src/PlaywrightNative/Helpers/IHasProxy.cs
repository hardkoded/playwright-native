/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Exposes the proxy configured on a browser context.
    /// </summary>
    internal interface IHasProxy
    {
        /// <summary>
        /// Proxy from <c>NewContextAsync(proxy)</c>, or <see langword="null"/>.
        /// </summary>
        Proxy Proxy { get; }
    }
}
