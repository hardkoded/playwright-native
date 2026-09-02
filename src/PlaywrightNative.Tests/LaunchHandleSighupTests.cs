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
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.Transport;

namespace PlaywrightNative.Tests
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
