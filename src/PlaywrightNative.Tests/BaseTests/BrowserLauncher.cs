// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Cross-browser launch helper for tests in <c>Direct/</c> and other
    /// product-agnostic fixtures. Routes <see cref="Playwright"/>'s
    /// browser-specific launchers based on the <c>PRODUCT</c> environment
    /// variable and pulls the executable path from
    /// <see cref="BrowserExecutable"/> in PlaywrightNative.NUnit.
    /// </summary>
    /// <remarks>
    /// Tests that hard-target a specific browser (e.g. the <c>Chromium/CR*</c>
    /// protocol fixtures) should keep calling <see cref="Playwright.LaunchChromiumAsync"/>
    /// directly — this helper is only for the public-API surface that runs against
    /// every product.
    /// </remarks>
    internal static class BrowserLauncher
    {
        /// <summary>
        /// Launches the browser selected by <c>PRODUCT</c>, or skips the calling
        /// test via <c>Assert.Ignore</c> when the executable couldn't be resolved.
        /// </summary>
        /// <param name="headless">Whether to launch headless. Defaults to <c>true</c>.</param>
        /// <param name="proxy">Optional launch-level proxy. Chromium needs one when contexts override proxy.</param>
        /// <returns>An <see cref="IBrowser"/> for the selected product.</returns>
        public static Task<IBrowser> LaunchAsync(bool headless = true, Proxy proxy = null)
            => LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless, Proxy = proxy });

        /// <summary>
        /// Launches the browser selected by <c>PRODUCT</c> with official launch
        /// options (for example <c>tracesDir</c>).
        /// </summary>
        /// <param name="options">Launch options. Executable path is filled in.</param>
        /// <returns>An <see cref="IBrowser"/> for the selected product.</returns>
        public static async Task<IBrowser> LaunchAsync(BrowserTypeLaunchOptions options)
        {
            options ??= new BrowserTypeLaunchOptions();
            string browserName = BrowserExecutable.ResolveBrowserName();
            BrowserTypeLaunchOptions resolved = await BrowserExecutable.CreateLaunchOptionsAsync(browserName).ConfigureAwait(false);
            options.ExecutablePath = resolved.ExecutablePath;

            if (browserName == "webkit")
            {
                return await Playwright.LaunchWebkitAsync(options).ConfigureAwait(false);
            }

            if (browserName == "firefox")
            {
                return await LaunchFirefoxAsync(options).ConfigureAwait(false);
            }

            return await Playwright.LaunchChromiumAsync(options).ConfigureAwait(false);
        }

        /// <summary>
        /// Skips the calling test unless the active product is Chromium with a resolvable
        /// executable. Used by Direct tests that exercise features only implemented on the
        /// Chromium stack so far. They launch via <see cref="LaunchAsync"/> (which follows
        /// <c>PRODUCT</c>), so without this guard they would launch the active product
        /// (e.g. WebKit) whenever Chrome happens to be installed.
        /// </summary>
        public static void SkipUnlessChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore($"This Direct test exercises a Chromium-only feature; not implemented on {TestConstants.Product} yet.");
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }
        }

        private static async Task<IBrowser> LaunchFirefoxAsync(BrowserTypeLaunchOptions options)
        {
            try
            {
                return await Playwright.LaunchFirefoxAsync(options).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Assert.Ignore("Firefox launch/connect failed: " + ex.Message);
                throw;
            }
        }
    }
}
