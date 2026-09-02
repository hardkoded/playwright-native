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
    /// IPage.Reload / GoBack / GoForward on Chromium and WebKit.
    /// </summary>
    [TestFixture]
    public class PageHistoryTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-history.spec.ts", "reload clears page state")]
        [Test]
        [Timeout(30_000)]
        public async Task ReloadShouldCreateANewDocument()
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
            await page.EvaluateAsync("window.__wave64 = 64").ConfigureAwait(false);

            IResponse response = await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await page.EvaluateAsync<int>("window.__wave64 || 0").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(page.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("page-history.spec.ts", "goBack and goForward traverse history")]
        [Test]
        [Timeout(30_000)]
        public async Task GoBackAndGoForwardShouldTraverseHistory()
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
            await page.GoToAsync($"{TestConstants.ServerUrl}/title.html").ConfigureAwait(false);
            Assert.That(page.Url, Does.Contain("title.html"));

            IResponse back = await page.GoBackAsync().ConfigureAwait(false);
            Assert.That(back, Is.Not.Null);
            Assert.That(page.Url, Does.Contain("empty.html"));

            IResponse forward = await page.GoForwardAsync().ConfigureAwait(false);
            Assert.That(forward, Is.Not.Null);
            Assert.That(page.Url, Does.Contain("title.html"));
        }

        [PlaywrightTest("page-history.spec.ts", "goBack with no history returns null")]
        [Test]
        [Timeout(30_000)]
        public async Task GoBackWithoutHistoryShouldReturnNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoBackAsync().ConfigureAwait(false);
            Assert.That(response, Is.Null);
        }
    }
}
