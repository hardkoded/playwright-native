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
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.Crash"/>.
    /// </summary>
    [TestFixture]
    public class PageCrashTests : PageTestEx
    {
        [PlaywrightTest("page-event-crash.spec.ts", "should fire Crash on Chromium chrome://crash")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireCrashOnChromiumChromeCrashAsync()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("chrome://crash is Chromium-only");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IPage> waitTask = page.WaitForCrashAsync();
            _ = page.GoToAsync("chrome://crash");
            IPage crashed = await waitTask.ConfigureAwait(false);
            Assert.That(crashed, Is.SameAs(page));
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should fire Crash after Page.crash on WebKit")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireCrashAfterPageCrashOnWebKitAsync()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("Page.crash probe is WebKit-only");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            WKPage wk = (WKPage)page;
            Task<IPage> waitTask = page.WaitForCrashAsync();
            await wk.CrashForTestsAsync().ConfigureAwait(false);
            IPage crashed = await waitTask.ConfigureAwait(false);
            Assert.That(crashed, Is.SameAs(page));
        }

        [PlaywrightTest("page-event-crash.spec.ts", "WaitForCrashAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForCrash()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForCrashAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }
    }
}
