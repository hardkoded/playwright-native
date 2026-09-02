/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.Transport;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>handleSIGHUP</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchHandleSighupTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "BrowserProcessManager honors HandleSIGHUP")]
        [Test]
        public void BrowserProcessManagerShouldHonorHandleSIGHUP()
        {
            using BrowserProcessManager enabled = new(
                "/bin/true",
                Array.Empty<string>(),
                handleSIGHUP: true);
            Assert.That(enabled.HandlesSIGHUP, Is.True);

            using BrowserProcessManager disabled = new(
                "/bin/true",
                Array.Empty<string>(),
                handleSIGHUP: false);
            Assert.That(disabled.HandlesSIGHUP, Is.False);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "LaunchAsync HandleSIGHUP false starts a page")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchAsyncHandleSIGHUPFalseShouldStartAPage()
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
                HandleSIGHUP = false,
            }).ConfigureAwait(false);

            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave465</body></html>").ConfigureAwait(false);
            string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Does.Contain("wave465"));
        }
    }
}
