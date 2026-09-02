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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-set-content.spec.ts</c>.
    /// Skipped (not implemented):
    /// <c>should handle timeout properly</c> (needs toImpl utility-world console.debug tamper);
    /// <c>should handle timeout properly 2</c> (needs toImpl document.close infinite loop).
    /// </summary>
    [TestFixture]
    public class PageSetContentTests : PageTestEx
    {
        private const string ExpectedHtml = "<html><head></head><body><div>hello</div></body></html>";
        private const string EmptyHtml = "<html><head></head><body></body></html>";

        private static SimpleServer Server => TestServerSetup.Server;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<string> ContentOrErrorAsync(IPage page)
        {
            try
            {
                return await page.ContentAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work")]
        [PlaywrightTest("page-set-content.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            string result = await page.ContentAsync().ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(ExpectedHtml));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with domcontentloaded")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDomcontentloaded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>hello</div>", new() { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
            string result = await page.ContentAsync().ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(ExpectedHtml));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with commit")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCommit()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>hello</div>", new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            string result = await page.ContentAsync().ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(ExpectedHtml));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with doctype")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDoctype()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            const string doctype = "<!DOCTYPE html>";
            await page.SetContentAsync(doctype + "<div>hello</div>").ConfigureAwait(false);
            string result = await page.ContentAsync().ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(doctype + ExpectedHtml));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with HTML 4 doctype")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithHtml4Doctype()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            const string doctype = "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" " +
                "\"http://www.w3.org/TR/html4/strict.dtd\">";
            await page.SetContentAsync(doctype + "<div>hello</div>").ConfigureAwait(false);
            string result = await page.ContentAsync().ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(doctype + ExpectedHtml));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectTimeout()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRoute("/img.png", _ => Task.Delay(-1));

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.ThrowsAsync<TimeoutException>(
                async () => await page.SetContentAsync($"<img src=\"{TestConstants.ServerUrl}/img.png\"></img>", new() { Timeout = 1 }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should respect default navigation timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectDefaultNavigationTimeout()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRoute("/img.png", _ => Task.Delay(-1));

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.SetDefaultNavigationTimeout(1);
            Assert.ThrowsAsync<TimeoutException>(
                async () => await page.SetContentAsync(
                    $"<img src=\"{TestConstants.ServerUrl}/img.png\"></img>").ConfigureAwait(false));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should await resources to load")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAwaitResourcesToLoad()
        {
            EnsureServer();
            Server.Reset();
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/img.png", async http =>
            {
                await release.Task.ConfigureAwait(false);
                http.Response.StatusCode = 200;
                http.Response.ContentType = "image/png";
                await http.Response.CompleteAsync().ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool loaded = false;
            async Task SetContentAndMarkAsync()
            {
                await page.SetContentAsync($"<img src=\"{TestConstants.ServerUrl}/img.png\"></img>").ConfigureAwait(false);
                loaded = true;
            }

            Task waitRequest = Server.WaitForRequest("/img.png");
            Task contentTask = SetContentAndMarkAsync();
            await waitRequest.ConfigureAwait(false);
            Assert.That(loaded, Is.False);
            release.TrySetResult(true);
            await contentTask.ConfigureAwait(false);
            Assert.That(loaded, Is.True);
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work fast enough")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkFastEnough()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            for (int i = 0; i < 20; i++)
            {
                await page.SetContentAsync("<div>yo</div>").ConfigureAwait(false);
            }
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with tricky content")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithTrickyContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>hello world</div>" + "\u007F").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("div", "div => div.textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello world"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with accents")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAccents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>aberración</div>").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("div", "div => div.textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("aberración"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with emojis")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithEmojis()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>🐥</div>").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("div", "div => div.textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("🐥"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "should work with newline")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNewline()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>\n</div>").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("div", "div => div.textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("\n"));
        }

        [PlaywrightTest("page-set-content.spec.ts", "content() should throw nice error during navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ContentShouldThrowNiceErrorDuringNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            for (int timeout = 0; timeout < 200; timeout += 20)
            {
                await page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
                Task<IResponse> navigation = page.GoToAsync(TestConstants.EmptyPage);
                await page.WaitForTimeoutAsync(timeout).ConfigureAwait(false);
                Task<string> contentTask = ContentOrErrorAsync(page);
                await Task.WhenAll(contentTask, navigation).ConfigureAwait(false);
                string contentOrError = await contentTask.ConfigureAwait(false);
                if (contentOrError != ExpectedHtml && contentOrError != EmptyHtml)
                {
                    Assert.That(
                        contentOrError,
                        Does.Contain("Unable to retrieve content because the page is navigating and changing the content."));
                }
            }
        }

        [PlaywrightTest("page-set-content.spec.ts", "should return empty content there is no iframe src")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyContentThereIsNoIframeSrc()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("there is no utility context");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<iframe src=\"javascript:console.log(1)\"></iframe>").ConfigureAwait(false);
            Assert.That(page.Frames.Count, Is.EqualTo(2));
            IFrame child = page.MainFrame.ChildFrames.First();
            string content = await child.ContentAsync().ConfigureAwait(false);
            Assert.That(content, Is.EqualTo(EmptyHtml));
        }
    }
}
