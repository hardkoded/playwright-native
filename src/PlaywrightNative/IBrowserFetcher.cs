// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace PlaywrightNative
{
    /// <summary>
    /// Downloads and manages browser binaries from the Playwright CDN.
    /// </summary>
    public interface IBrowserFetcher
    {
        /// <summary>Gets or sets the CDN base URL. When <c>null</c>, the built-in mirrors are used.</summary>
        string BaseUrl { get; set; }

        /// <summary>Gets or sets the path to download browsers to.</summary>
        string CacheDir { get; set; }

        /// <summary>Gets or sets the target platform.</summary>
        Platform Platform { get; set; }

        /// <summary>Gets or sets the browser to download.</summary>
        SupportedBrowser Browser { get; set; }

        /// <summary>Gets or sets the proxy used by HTTP calls.</summary>
        IWebProxy WebProxy { get; set; }

        /// <summary>Returns <c>true</c> when the given build is reachable on the CDN.</summary>
        /// <param name="buildId">The build identifier to probe.</param>
        /// <returns>A task that resolves to <c>true</c> if the build is downloadable.</returns>
        Task<bool> CanDownloadAsync(string buildId);

        /// <summary>Downloads the default build for the configured browser+platform.</summary>
        /// <returns>A task that resolves to the installed browser descriptor.</returns>
        Task<InstalledBrowser> DownloadAsync();

        /// <summary>Downloads a specific build.</summary>
        /// <param name="buildId">The build identifier to download.</param>
        /// <returns>A task that resolves to the installed browser descriptor.</returns>
        Task<InstalledBrowser> DownloadAsync(string buildId);

        /// <summary>Returns every browser already present in <see cref="CacheDir"/>.</summary>
        /// <returns>An enumerable of installed browser descriptors.</returns>
        IEnumerable<InstalledBrowser> GetInstalledBrowsers();

        /// <summary>Removes the given build from the cache.</summary>
        /// <param name="buildId">The build identifier to remove.</param>
        void Uninstall(string buildId);

        /// <summary>Returns the expected executable path for the given build, without checking existence.</summary>
        /// <param name="buildId">The build identifier.</param>
        /// <returns>An absolute path to the browser executable.</returns>
        string GetExecutablePath(string buildId);
    }
}
