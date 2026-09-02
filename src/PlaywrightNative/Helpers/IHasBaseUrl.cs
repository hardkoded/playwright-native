/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Exposes the <c>baseURL</c> stored on a browser context.
    /// </summary>
    internal interface IHasBaseUrl
    {
        /// <summary>
        /// Prefix used to resolve relative navigation URLs, or <see langword="null"/>.
        /// </summary>
        string BaseURL { get; set; }
    }
}
