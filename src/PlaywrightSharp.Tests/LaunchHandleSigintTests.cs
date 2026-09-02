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
    /// Official <c>handleSIGINT</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchHandleSigintTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "BrowserProcessManager honors HandleSIGINT")]
        [Test]
        public void BrowserProcessManagerShouldHonorHandleSIGINT()
        {
            using BrowserProcessManager enabled = new(
                "/bin/true",
                Array.Empty<string>(),
                handleSIGINT: true);
            Assert.That(enabled.HandlesSIGINT, Is.True);

            using BrowserProcessManager disabled = new(
                "/bin/true",
                Array.Empty<string>(),
                handleSIGINT: false);
            Assert.That(disabled.HandlesSIGINT, Is.False);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "LaunchAsync HandleSIGINT false starts a page")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchAsyncHandleSIGINTFalseShouldStartAPage()
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
                HandleSIGINT = false,
            }).ConfigureAwait(false);

            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave463</body></html>").ConfigureAwait(false);
            string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Does.Contain("wave463"));
        }
    }
}
