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
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.ChromiumSandbox"/>.
    /// </summary>
    [TestFixture]
    public class LaunchChromiumSandboxTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "GetDefaultArgs toggles --no-sandbox")]
        [Test]
        public void GetDefaultArgsShouldHonorChromiumSandbox()
        {
            List<string> withoutSandbox = ChromiumBrowserType.GetDefaultArgs(chromiumSandbox: false);
            Assert.That(withoutSandbox, Does.Contain("--no-sandbox"));

            List<string> withSandbox = ChromiumBrowserType.GetDefaultArgs(chromiumSandbox: true);
            Assert.That(withSandbox, Does.Not.Contain("--no-sandbox"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch ChromiumSandbox false adds --no-sandbox")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchShouldAddNoSandboxWhenDisabled()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("ChromiumSandbox is a Chromium launch option.");
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            await using IBrowser browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath,
                ChromiumSandbox = false,
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("chrome://version").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.body.innerText").ConfigureAwait(false);
            Assert.That(text, Does.Contain("--no-sandbox"));
        }
    }
}
