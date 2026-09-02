/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightNative
{
    /// <summary>
    /// Browsers that can be downloaded via BrowserFetcher.
    /// </summary>
    public enum SupportedBrowser
    {
        /// <summary>Chromium (open-source Chromium builds).</summary>
        Chromium,

        /// <summary>Mozilla Firefox.</summary>
        Firefox,

        /// <summary>Apple WebKit (used by Safari).</summary>
        Webkit,
    }
}
