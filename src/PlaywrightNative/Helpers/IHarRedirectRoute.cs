/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official HAR navigation redirect: restart the document request at the
    /// final URL so <c>page.goto</c> observes the redirected location.
    /// </summary>
    internal interface IHarRedirectRoute
    {
        /// <summary>
        /// Restarts this navigation at <paramref name="url"/>.
        /// </summary>
        /// <param name="url">The final document URL from the HAR.</param>
        /// <returns>A task that completes when the redirect has been issued.</returns>
        Task RedirectNavigationAsync(string url);
    }
}
