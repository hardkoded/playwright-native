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
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-select-text.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleSelectTextParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19865;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "should select textarea")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectTextarea()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.EvaluateAsync("textarea => textarea.value = 'some value'").ConfigureAwait(false);
            await textarea.SelectTextAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window.getSelection().toString())()").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "should select input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            await input.EvaluateAsync("input => input.value = 'some value'").ConfigureAwait(false);
            await input.SelectTextAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window.getSelection().toString())()").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "should select plain div")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectPlainDiv()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div.plain").ConfigureAwait(false);
            await div.SelectTextAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window.getSelection().toString())()").ConfigureAwait(false), Is.EqualTo("Plain div"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "should timeout waiting for invisible element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForInvisibleElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.EvaluateAsync("e => e.style.display = 'none'").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => textarea.SelectTextAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("element is not visible"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "should wait for visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            await textarea.EvaluateAsync("textarea => textarea.value = 'some value'").ConfigureAwait(false);
            await textarea.EvaluateAsync("e => e.style.display = 'none'").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await textarea.EvaluateAsync("e => e.style.display = 'block'").ConfigureAwait(false);
            await promise.ConfigureAwait(false);

            async Task MarkDoneAsync()
            {
                await textarea.SelectTextAsync(new() { Timeout = 3000 }).ConfigureAwait(false);
                done = true;
            }
        }
    }
}
