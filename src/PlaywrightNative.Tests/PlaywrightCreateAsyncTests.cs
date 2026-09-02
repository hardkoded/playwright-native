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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>Playwright.CreateAsync()</c> entry point.
    /// </summary>
    [TestFixture]
    public class PlaywrightCreateAsyncTests : PlaywrightTestEx
    {
        [PlaywrightTest("browsertype-basic.spec.ts", "browserType.launch should work")]
        [Test]
        [Timeout(30_000)]
        public async Task CreateAsyncShouldExposeBrowserTypesAndLaunch()
        {
            using IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            Assert.That(playwright.Chromium.Name, Is.EqualTo("chromium"));
            Assert.That(playwright.Firefox.Name, Is.EqualTo("firefox"));
            Assert.That(playwright.Webkit.Name, Is.EqualTo("webkit"));
            Assert.That(playwright[Microsoft.Playwright.BrowserType.Chromium], Is.SameAs(playwright.Chromium));
            Assert.That(playwright.Devices.ContainsKey("iPhone 13"), Is.True);
            Assert.That(playwright.APIRequest, Is.Not.Null);
            Assert.That(playwright.Selectors, Is.Not.Null);

            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = playwright.Webkit;
                executablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                browserType = playwright.Firefox;
                executablePath = BrowserExecutableFixture.FirefoxExecutablePath;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = playwright.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            await using IBrowser browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = executablePath,
                Headless = true,
            }).ConfigureAwait(false);

            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>create-async</body></html>").ConfigureAwait(false);
            string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Does.Contain("create-async"));
        }
    }
}
