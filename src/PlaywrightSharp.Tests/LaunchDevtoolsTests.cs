/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.Devtools"/>.
    /// </summary>
    [TestFixture]
    public class LaunchDevtoolsTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "GetDefaultArgs adds --auto-open-devtools-for-tabs")]
        [Test]
        public void GetDefaultArgsShouldHonorDevtools()
        {
            List<string> off = ChromiumBrowserType.GetDefaultArgs(devtools: false);
            Assert.That(off, Does.Not.Contain("--auto-open-devtools-for-tabs"));

            List<string> on = ChromiumBrowserType.GetDefaultArgs(devtools: true);
            Assert.That(on, Does.Contain("--auto-open-devtools-for-tabs"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch Devtools true adds --auto-open-devtools-for-tabs")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchShouldAddDevtoolsFlag()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Devtools is a Chromium launch option.");
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            await using IBrowser browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath,
                Devtools = true,
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("chrome://version").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.body.innerText").ConfigureAwait(false);
            Assert.That(text, Does.Contain("--auto-open-devtools-for-tabs"));
        }
    }
}
