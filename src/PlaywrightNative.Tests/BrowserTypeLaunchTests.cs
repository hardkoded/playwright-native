/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserType.launch()</c>.
    /// </summary>
    [TestFixture]
    public class BrowserTypeLaunchTests : PlaywrightTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "LaunchAsync starts a page")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchAsyncShouldStartAPage()
        {
            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = BrowserTypeInfo.Webkit;
                executablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                browserType = BrowserTypeInfo.Firefox;
                executablePath = BrowserExecutableFixture.FirefoxExecutablePath;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = BrowserTypeInfo.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            await using IBrowser browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = executablePath,
                Headless = true,
            }).ConfigureAwait(false);

            Assert.That(browser.BrowserType.Name, Is.EqualTo(browserType.Name));
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave456</body></html>").ConfigureAwait(false);
            string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Does.Contain("wave456"));
        }
    }
}
