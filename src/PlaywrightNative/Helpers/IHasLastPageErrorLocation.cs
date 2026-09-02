/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Page implementations that stash the location of the last
    /// <see cref="IPage.PageError"/> so context <see cref="IWebError"/>
    /// can expose official <see cref="IWebError.Location"/>.
    /// </summary>
    internal interface IHasLastPageErrorLocation
    {
        /// <summary>
        /// Location of the most recently raised page error.
        /// </summary>
        WebErrorLocation LastPageErrorLocation { get; }
    }
}
