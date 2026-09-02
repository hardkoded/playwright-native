/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>await using page</c> closes the page and ignores a
    /// second close after the browser or context is already gone.
    /// </summary>
    internal static class PageDispose
    {
        /// <summary>
        /// Closes <paramref name="page"/> for <see cref="IAsyncDisposable"/>.
        /// </summary>
        /// <param name="page">The page to close.</param>
        /// <returns>A task that completes when close has been attempted.</returns>
        internal static async ValueTask RunAsync(IPage page)
        {
            try
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }
        }
    }
}
