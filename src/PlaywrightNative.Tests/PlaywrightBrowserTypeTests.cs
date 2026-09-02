/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>playwright.chromium</c> / <c>firefox</c> / <c>webkit</c>.
    /// </summary>
    [TestFixture]
    public class PlaywrightBrowserTypeTests : PlaywrightTestEx
    {
        [PlaywrightTest("browsertype-basic.spec.ts", "Playwright.Chromium launches Chromium")]
        [Test]
        [Timeout(30_000)]
        public async Task PlaywrightBrowserTypeShouldLaunchTheSelectedEngine()
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
                Assert.That(browserType.Name, Is.EqualTo("webkit"));
            }
            else if (TestConstants.IsFirefox)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                browserType = Playwright.Firefox;
                executablePath = BrowserExecutableFixture.FirefoxExecutablePath;
                Assert.That(browserType.Name, Is.EqualTo("firefox"));
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
                Assert.That(browserType.Name, Is.EqualTo("chromium"));
            }

            Assert.That(Playwright.Firefox.Name, Is.EqualTo("firefox"));
            await using IBrowser browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = executablePath,
                Headless = true,
            }).ConfigureAwait(false);

            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave457</body></html>").ConfigureAwait(false);
            string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(body, Does.Contain("wave457"));
        }
    }
}
