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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForURLAsync"/>.
    /// Mirrors a first-match subset of upstream <c>page-wait-for-url.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class WaitForUrlTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-url.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyWhenUrlAlreadyMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            const string url = "data:text/html,<div>already-matched</div>";
            await page.GoToAsync(url).ConfigureAwait(false);
            await page.WaitForURLAsync(url).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(url));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilPredicateMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task waitTask = page.WaitForURLAsync(url => url.Contains("wait-for-url-pred", StringComparison.Ordinal));
            await page.GoToAsync("data:text/html,<div>wait-for-url-pred</div>").ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
            Assert.That(page.Url, Does.Contain("wait-for-url-pred"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilRegexMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task waitTask = page.WaitForURLAsync(new Regex("wait-for-url-re"));
            await page.GoToAsync("data:text/html,<div>wait-for-url-re</div>").ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
            Assert.That(page.Url, Does.Match("wait-for-url-re"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with glob")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilGlobMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            // `*` does not cross `/`; keep the data URL slash-free so the glob can match.
            Task waitTask = page.WaitForURLAsync("data:text/html*");
            await page.GoToAsync("data:text/html,wait-for-url-glob").ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
            Assert.That(page.Url, Does.StartWith("data:text/html"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForURLAsync("**/never-this-url", new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForURL"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }
    }
}
