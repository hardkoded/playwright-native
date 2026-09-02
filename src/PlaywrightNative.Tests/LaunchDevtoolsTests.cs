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
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
