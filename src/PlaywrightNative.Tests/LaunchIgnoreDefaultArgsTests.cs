/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.IgnoreDefaultArgs"/>.
    /// </summary>
    [TestFixture]
    public class LaunchIgnoreDefaultArgsTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "GetDefaultArgs skips built-in flags")]
        [Test]
        public void GetDefaultArgsShouldSkipBuiltInFlagsWhenIgnored()
        {
            List<string> ignored = ChromiumBrowserType.GetDefaultArgs(
                additionalArgs: new[] { "--no-sandbox", "--headless" },
                ignoreDefaultArgs: true);
            Assert.That(ignored, Does.Contain("--no-sandbox"));
            Assert.That(ignored, Does.Contain("--headless"));
            Assert.That(ignored, Does.Not.Contain("--disable-extensions"));
            Assert.That(ignored, Does.Not.Contain("--no-first-run"));

            List<string> included = ChromiumBrowserType.GetDefaultArgs(ignoreDefaultArgs: false);
            Assert.That(included, Does.Contain("--disable-extensions"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "GetDefaultArgs list omits --mute-audio")]
        [Test]
        public void GetDefaultArgsShouldOmitNamedDefaultFlags()
        {
            List<string> omitted = ChromiumBrowserType.GetDefaultArgs(
                ignoreDefaultArgsList: new[] { "--mute-audio" });
            Assert.That(omitted, Does.Not.Contain("--mute-audio"));
            Assert.That(omitted, Does.Contain("--disable-extensions"));
            Assert.That(omitted, Does.Contain("--headless"));

            List<string> boolWins = ChromiumBrowserType.GetDefaultArgs(
                additionalArgs: new[] { "--no-sandbox" },
                ignoreDefaultArgs: true,
                ignoreDefaultArgsList: new[] { "--mute-audio" });
            Assert.That(boolWins, Does.Not.Contain("--disable-extensions"));
            Assert.That(boolWins, Does.Contain("--no-sandbox"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch IgnoreDefaultArgs omits --disable-extensions")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchShouldOmitDefaultFlagsWhenIgnored()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("IgnoreDefaultArgs is a Chromium launch option.");
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            await using IBrowser browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath,
                IgnoreDefaultArgs = true,
                Args = new[] { "--no-sandbox", "--headless", "--disable-gpu" },
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("chrome://version").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.body.innerText").ConfigureAwait(false);
            Assert.That(text, Does.Contain("--no-sandbox"));
            Assert.That(text, Does.Not.Contain("--disable-extensions"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch IgnoreDefaultArgsList omits --mute-audio")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchShouldOmitNamedDefaultFlags()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("IgnoreDefaultArgsList is a Chromium launch option.");
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            await using IBrowser browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath,
                IgnoreDefaultArgsList = new[] { "--mute-audio" },
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("chrome://version").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.body.innerText").ConfigureAwait(false);
            Assert.That(text, Does.Not.Contain("--mute-audio"));
            Assert.That(text, Does.Contain("--disable-extensions"));
        }
    }
}
