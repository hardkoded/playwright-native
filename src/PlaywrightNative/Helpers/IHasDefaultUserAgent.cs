/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Exposes the browser's default User-Agent string.
    /// </summary>
    internal interface IHasDefaultUserAgent
    {
        /// <summary>
        /// Value from <c>Browser.getVersion</c> (Chromium) or the first page.
        /// </summary>
        string DefaultUserAgent { get; }
    }
}
