/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.Transport;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>handleSIGTERM</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchHandleSigtermTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "BrowserProcessManager honors HandleSIGTERM")]
        [Test]
        public void BrowserProcessManagerShouldHonorHandleSIGTERM()
        {
            using BrowserProcessManager enabled = new(
                "/bin/true",
                Array.Empty<string>(),
                handleSIGTERM: true);
            Assert.That(enabled.HandlesSIGTERM, Is.True);

            using BrowserProcessManager disabled = new(
                "/bin/true",
                Array.Empty<string>(),
                handleSIGTERM: false);
            Assert.That(disabled.HandlesSIGTERM, Is.False);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "LaunchAsync HandleSIGTERM false starts a page")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchAsyncHandleSIGTERMFalseShouldStartAPage()
        {
            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                executablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                browserType = Playwright.Firefox;
                executablePath = BrowserExecutableFixture.FirefoxExecutablePath;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            await using IBrowser browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = executablePath,
                Headless = true,
                HandleSIGTERM = false,
            }).ConfigureAwait(false);

            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave464</body></html>").ConfigureAwait(false);
            string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Does.Contain("wave464"));
        }
    }
}
