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
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForNavigationAsync()"/>.
    /// </summary>
    [TestFixture]
    public class WaitForNavigationTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "wait then GoTo empty returns the document response")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnDocumentResponseForGoTo()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync();
            IResponse gotoResponse = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IResponse waited = await waitTask.ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(page.Url, Does.Contain("empty.html"));
            Assert.That(gotoResponse?.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "wait then click a link")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForClickNavigation()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync($"<a href=\"{TestConstants.ServerUrl}/title.html\">next</a>").ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync();
            await page.ClickAsync("a", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IResponse waited = await waitTask.ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(page.Url, Does.Contain("title.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "RunAndWaitForNavigationAsync waits for a click")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForNavigationAsyncShouldReturnTheResponse()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync($"<a href=\"{TestConstants.ServerUrl}/title.html\">next</a>").ConfigureAwait(false);

            IResponse waited = await page.RunAndWaitForNavigationAsync(
                () => page.ClickAsync("a", new() { NoWaitAfter = true })).ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(page.Url, Does.Contain("title.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "url glob ignores a non-matching GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterNavigationByGlob()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync("**/title.html");
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(waitTask.IsCompleted, Is.False);

            await page.GoToAsync($"{TestConstants.ServerUrl}/title.html").ConfigureAwait(false);
            IResponse waited = await waitTask.ConfigureAwait(false);

            Assert.That(waited, Is.Not.Null);
            Assert.That(waited.Status, Is.EqualTo(200));
            Assert.That(page.Url, Does.Contain("title.html"));
        }
    }
}
