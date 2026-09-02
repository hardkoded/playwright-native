/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
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
