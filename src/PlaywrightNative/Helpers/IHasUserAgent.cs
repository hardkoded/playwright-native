/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Exposes the User-Agent override stored on a browser context.
    /// </summary>
    internal interface IHasUserAgent
    {
        /// <summary>
        /// Override from <c>NewContextAsync(userAgent)</c>, or <see langword="null"/>.
        /// </summary>
        string UserAgent { get; }
    }
}
