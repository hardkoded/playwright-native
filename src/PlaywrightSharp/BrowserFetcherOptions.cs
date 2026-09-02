/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightSharp
{
    /// <summary>
    /// Options for constructing a BrowserFetcher.
    /// </summary>
    public class BrowserFetcherOptions
    {
        /// <summary>
        /// Gets or sets the browser to download. Defaults to <see cref="SupportedBrowser.Chromium"/>.
        /// </summary>
        public SupportedBrowser Browser { get; set; } = SupportedBrowser.Chromium;

        /// <summary>
        /// Gets or sets the target platform. When <c>null</c> (default) the current
        /// OS and architecture are auto-detected.
        /// </summary>
        public Platform? Platform { get; set; }

        /// <summary>
        /// Gets or sets an override for the browser cache root directory.
        /// When <c>null</c> the same default location as the <c>playwright</c> CLI is used.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets an override for the CDN base URL.
        /// When <c>null</c> the built-in Playwright CDN mirrors are used.
        /// </summary>
        public string Host { get; set; }
    }
}
