// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Manual
{
    /// <summary>
    /// Manual verification of BrowserFetcher's download + extract pipeline.
    /// Marked [Explicit] so it does not run in normal test invocations.
    /// </summary>
    [TestFixture]
    [Explicit("Manual verification — downloads real browser binaries (~150 MB each).")]
    public class BrowserFetcherManualTest
    {
        [PlaywrightTest("browsers-path.spec.ts", "Download and launch chromium")]
        [Test]
        public async Task DownloadAndLaunchChromium()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "pwsharp-manual-" + Guid.NewGuid().ToString("N"));
            try
            {
                BrowserFetcher fetcher = new(new BrowserFetcherOptions
                {
                    Browser = SupportedBrowser.Chromium,
                    Path = tempCache,
                });

                Console.WriteLine($"Downloading Chromium to {tempCache}");
                InstalledBrowser installed = await fetcher.DownloadAsync().ConfigureAwait(false);
                Console.WriteLine($"Installed at: {installed.GetExecutablePath()}");

                Assert.That(File.Exists(installed.GetExecutablePath()), Is.True, "executable should exist on disk");

                IBrowser browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
                {
                    ExecutablePath = installed.GetExecutablePath(),
                    Headless = true,
                }).ConfigureAwait(false);

                await browser.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                if (Directory.Exists(tempCache))
                {
                    Directory.Delete(tempCache, recursive: true);
                }
            }
        }

        [PlaywrightTest("browsers-path.spec.ts", "Download and launch firefox")]
        [Test]
        public async Task DownloadAndLaunchFirefox()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "pwsharp-manual-" + Guid.NewGuid().ToString("N"));
            try
            {
                BrowserFetcher fetcher = new(new BrowserFetcherOptions
                {
                    Browser = SupportedBrowser.Firefox,
                    Path = tempCache,
                });

                Console.WriteLine($"Downloading Firefox to {tempCache}");
                InstalledBrowser installed = await fetcher.DownloadAsync().ConfigureAwait(false);
                Console.WriteLine($"Installed at: {installed.GetExecutablePath()}");

                Assert.That(File.Exists(installed.GetExecutablePath()), Is.True, "executable should exist on disk");

                IBrowser browser = await Playwright.LaunchFirefoxAsync(new BrowserTypeLaunchOptions
                {
                    ExecutablePath = installed.GetExecutablePath(),
                    Headless = true,
                }).ConfigureAwait(false);

                await browser.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                if (Directory.Exists(tempCache))
                {
                    Directory.Delete(tempCache, recursive: true);
                }
            }
        }
    }
}
