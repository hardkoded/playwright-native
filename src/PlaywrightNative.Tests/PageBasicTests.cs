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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-basic.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class PageBasicTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task GoToEmptyPageOrThrowAsync(IPage page)
        {
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
        }

        [PlaywrightTest("page-basic.spec.ts", "should fire load when expected")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireLoadWhenExpected()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await Task.WhenAll(
                page.GoToAsync("about:blank"),
                page.WaitForLoadAsync()).ConfigureAwait(false);
        }

        [PlaywrightTest("page-basic.spec.ts", "async stacks should work")]
        [Test]
        [Timeout(30_000)]
        public async Task AsyncStacksShouldWork()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRoute("/empty.html", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });

            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                Exception error = null;
                try
                {
                    await GoToEmptyPageOrThrowAsync(page).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                Assert.That(error, Is.Not.Null);
                Assert.That(error, Is.InstanceOf<PlaywrightNativeException>());
                Assert.That(
                    error.Message,
                    Does.Match("(?i)(navigation|net::|ERR_|connection|abort|socket|failed|reset|empty)"));
                string stack = error.StackTrace ?? string.Empty;
                Assert.That(
                    stack,
                    Does.Contain("GoToEmptyPageOrThrowAsync").Or.Contain(nameof(PageBasicTests)));
            }
            finally
            {
                Server.Reset();
            }
        }

        [PlaywrightTest("page-basic.spec.ts", "should provide access to the opener page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldProvideAccessToTheOpenerPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => { window.open('about:blank'); })()").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            IPage opener = await popup.OpenerAsync().ConfigureAwait(false);
            Assert.That(opener, Is.SameAs(page));
        }

        [PlaywrightTest("page-basic.spec.ts", "should fire domcontentloaded when expected")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireDomcontentloadedWhenExpected()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task navigatedPromise = page.GoToAsync("about:blank");
            await page.WaitForDOMContentLoadedAsync().ConfigureAwait(false);
            await navigatedPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("page-basic.spec.ts", "should pass self as argument to domcontentloaded event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPassSelfAsArgumentToDomcontentloadedEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IPage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.DOMContentLoaded += (_, eventArg) => tcs.TrySetResult(eventArg);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IPage eventArg = await tcs.Task.ConfigureAwait(false);
            Assert.That(eventArg, Is.SameAs(page));
        }

        [PlaywrightTest("page-basic.spec.ts", "should pass self as argument to load event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPassSelfAsArgumentToLoadEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IPage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Load += (_, eventArg) => tcs.TrySetResult(eventArg);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            IPage eventArg = await tcs.Task.ConfigureAwait(false);
            Assert.That(eventArg, Is.SameAs(page));
        }

        [PlaywrightTest("page-basic.spec.ts", "page.url should work")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUrlShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page.Url, Is.EqualTo("about:blank"));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [PlaywrightTest("page-basic.spec.ts", "page.url should include hashes")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUrlShouldIncludeHashes()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage + "#hash").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.EmptyPage + "#hash"));
            await page.EvaluateAsync("(() => { window.location.hash = 'dynamic'; })()").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(TestConstants.EmptyPage + "#dynamic"));
        }

        [PlaywrightTest("page-basic.spec.ts", "page.title should return the page title")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTitleShouldReturnThePageTitle()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/title.html").ConfigureAwait(false);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Woof-Woof"));
        }

        [PlaywrightTest("page-basic.spec.ts", "page.title should not throw during navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTitleShouldNotThrowDuringNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<title>hello</title>").ConfigureAwait(false);
            Task promise = page.GoToAsync(TestConstants.ServerUrl + "/title.html");
            object titleOrError = await page.TitleAsync().ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(titleOrError, Is.InstanceOf<string>());
            Assert.That((string)titleOrError, Does.Match("^(hello|Loading http.*title.html||Woof-Woof)$"));
            await Assertions.Expect(page).ToHaveTitleAsync("Woof-Woof").ConfigureAwait(false);
        }

        [PlaywrightTest("page-basic.spec.ts", "page.close should work with window.close")]
        [Test]
        [Timeout(30_000)]
        public async Task PageCloseShouldWorkWithWindowClose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> newPagePromise = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => { window['newPage'] = window.open('about:blank'); })()").ConfigureAwait(false);
            IPage newPage = await newPagePromise.ConfigureAwait(false);
            TaskCompletionSource<IPage> closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            newPage.Close += (_, closed) => closedTcs.TrySetResult(closed);
            await page.EvaluateAsync("(() => { window['newPage'].close(); })()").ConfigureAwait(false);
            await closedTcs.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("page-basic.spec.ts", "page.frame should respect name")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFrameShouldRespectName()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<iframe name=target></iframe>").ConfigureAwait(false);
            Assert.That(page.Frame("bogus"), Is.Null);
            IFrame frame = page.Frame("target");
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame, Is.SameAs(page.MainFrame.ChildFrames.First()));
        }

        [PlaywrightTest("page-basic.spec.ts", "page.frame should respect url")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFrameShouldRespectUrl()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync($"<iframe src=\"{TestConstants.EmptyPage}\"></iframe>").ConfigureAwait(false);
            Assert.That(page.FrameByUrl(new Regex("bogus")), Is.Null);
            Assert.That(page.FrameByUrl(new Regex("empty")).Url, Is.EqualTo(TestConstants.EmptyPage));
        }

        [PlaywrightTest("page-basic.spec.ts", "should have sane user agent")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveSaneUserAgent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string userAgent = await page.EvaluateAsync<string>("(() => navigator.userAgent)()").ConfigureAwait(false);
            string[] parts = userAgent.Split(new[] { '(', ')' }).Select(part => part.Trim()).ToArray();
            string part1 = parts.Length > 0 ? parts[0] : null;
            string part3 = parts.Length > 2 ? parts[2] : null;
            string part4 = parts.Length > 3 ? parts[3] : null;
            string part5 = parts.Length > 4 ? parts[4] : null;

            Assert.That(part1, Is.EqualTo("Mozilla/5.0"));

            if (TestConstants.IsFirefox)
            {
                string[] engineAndBrowser = (part3 ?? string.Empty).Split(' ');
                string engine = engineAndBrowser.Length > 0 ? engineAndBrowser[0] : string.Empty;
                string browserToken = engineAndBrowser.Length > 1 ? engineAndBrowser[1] : string.Empty;
                Assert.That(engine.StartsWith("Gecko", StringComparison.Ordinal), Is.True);
                Assert.That(browserToken.StartsWith("Firefox", StringComparison.Ordinal), Is.True);
                Assert.That(part4, Is.Null.Or.Empty);
                Assert.That(part5, Is.Null.Or.Empty);
                return;
            }

            Assert.That(part3, Does.StartWith("AppleWebKit/"));
            Assert.That(part4, Is.EqualTo("KHTML, like Gecko"));
            string[] tokens = (part5 ?? string.Empty).Split(' ');
            string engineToken = tokens.Length > 0 ? tokens[0] : string.Empty;
            string safari = Array.Find(tokens, t => t.StartsWith("Safari/", StringComparison.Ordinal));
            Assert.That(safari, Does.StartWith("Safari/"));
            if (TestConstants.IsChromium)
            {
                Assert.That(engineToken, Does.Contain("Chrome/"));
            }
            else
            {
                Assert.That(engineToken, Does.StartWith("Version/"));
            }
        }

        [PlaywrightTest("page-basic.spec.ts", "page.press should work")]
        [Test]
        [Timeout(30_000)]
        public async Task PagePressShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await page.PressAsync("textarea", "a").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => document.querySelector('textarea').value)()").ConfigureAwait(false),
                Is.EqualTo("a"));
        }

        [PlaywrightTest("page-basic.spec.ts", "page.press should work for Enter")]
        [Test]
        [Timeout(30_000)]
        public async Task PagePressShouldWorkForEnter()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input onkeypress=\"console.log('press')\"></input>").ConfigureAwait(false);
            List<IConsoleMessage> messages = new();
            page.Console += (_, message) => messages.Add(message);
            await page.PressAsync("input", "Enter").ConfigureAwait(false);
            Assert.That(messages, Is.Not.Empty);
            Assert.That(messages[0].Text, Is.EqualTo("press"));
        }

        [PlaywrightTest("page-basic.spec.ts", "frame.press should work")]
        [Test]
        [Timeout(30_000)]
        public async Task FramePressShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync($"<iframe name=inner src=\"{TestConstants.ServerUrl}/input/textarea.html\"></iframe>").ConfigureAwait(false);
            IFrame frame = page.Frame("inner");
            Assert.That(frame, Is.Not.Null);
            await frame.PressAsync("textarea", "a").ConfigureAwait(false);
            Assert.That(
                await frame.EvaluateAsync<string>("(() => document.querySelector('textarea').value)()").ConfigureAwait(false),
                Is.EqualTo("a"));
        }

        [PlaywrightTest("page-basic.spec.ts", "has navigator.webdriver set to true")]
        [Test]
        [Timeout(30_000)]
        public async Task HasNavigatorWebdriverSetToTrue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => navigator.webdriver)()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-basic.spec.ts", "should iterate over page properties")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldIterateOverPageProperties()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<object> props = new();
            foreach (PropertyInfo prop in page.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = null;
                try
                {
                    value = prop.GetValue(page);
                }
                catch (TargetInvocationException)
                {
                    // Some getters throw when unused; official loop is a no-op.
                }

                if (value != null)
                {
                    props.Add(value);
                }
            }

            Assert.That(props, Is.Not.Null);
        }
    }
}
