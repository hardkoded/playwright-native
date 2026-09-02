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
    /// Official <c>page-add-script-tag.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageAddScriptTagTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19180;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = origin + "/empty.html";
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

        [PlaywrightTest("page-add-script-tag.spec.ts", "should throw an error if no options are provided")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowAnErrorIfNoOptionsAreProvided()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.AddScriptTagAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Provide an object with a `url`, `path` or `content` property"));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should work with a url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle scriptHandle = await page.AddScriptTagAsync(new() { Url = "/injectedfile.js" }).ConfigureAwait(false);
            Assert.That(scriptHandle.AsElement(), Is.Not.Null);
            int injected = await page.EvaluateAsync<int>("(() => window['__injected'])()").ConfigureAwait(false);
            Assert.That(injected, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should work with a url and type=module")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAUrlAndTypeModule()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.AddScriptTagAsync(new() { Url = "/es6/es6import.js", Type = "module" }).ConfigureAwait(false);
            int injected = await page.EvaluateAsync<int>("(() => window['__es6injected'])()").ConfigureAwait(false);
            Assert.That(injected, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should work with a path and type=module")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAPathAndTypeModule()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.AddScriptTagAsync(new() { Path = TestUtils.GetWebServerFile("es6/es6pathimport.js"), Type = "module" }).ConfigureAwait(false);
            await page.WaitForFunctionAsync("window.__es6injected").ConfigureAwait(false);
            int injected = await page.EvaluateAsync<int>("(() => window['__es6injected'])()").ConfigureAwait(false);
            Assert.That(injected, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should work with a content and type=module")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAContentAndTypeModule()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.AddScriptTagAsync(new() { Content = "import num from '/es6/es6module.js';window.__es6injected = num;", Type = "module" }).ConfigureAwait(false);
            await page.WaitForFunctionAsync("window.__es6injected").ConfigureAwait(false);
            int injected = await page.EvaluateAsync<int>("(() => window['__es6injected'])()").ConfigureAwait(false);
            Assert.That(injected, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should throw an error if loading from url fail")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowAnErrorIfLoadingFromUrlFail()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.AddScriptTagAsync(new() { Url = "/nonexistfile.js" }));
            Assert.That(error, Is.Not.Null);
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should work with a path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle scriptHandle = await page.AddScriptTagAsync(new() { Path = TestUtils.GetWebServerFile("injectedfile.js") }).ConfigureAwait(false);
            Assert.That(scriptHandle.AsElement(), Is.Not.Null);
            int injected = await page.EvaluateAsync<int>("(() => window['__injected'])()").ConfigureAwait(false);
            Assert.That(injected, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should include sourceURL when path is provided")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldIncludeSourceURLWhenPathIsProvided()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Upstream skips WebKit for sourceURL.");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string assetPath = TestUtils.GetWebServerFile("injectedfile.js");
            await page.AddScriptTagAsync(new() { Path = assetPath }).ConfigureAwait(false);
            string result = await page.EvaluateAsync<string>("(() => window['__injectedError'].stack)()").ConfigureAwait(false);
            Assert.That(result, Does.Contain(assetPath));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should work with content")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle scriptHandle = await page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 35;" }).ConfigureAwait(false);
            Assert.That(scriptHandle.AsElement(), Is.Not.Null);
            int injected = await page.EvaluateAsync<int>("(() => window['__injected'])()").ConfigureAwait(false);
            Assert.That(injected, Is.EqualTo(35));
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should throw when added with content to the CSP page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenAddedWithContentToTheCSPPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.AddScriptTagAsync(new() { Content = "window[\"__injected\"] = 35;" }));
            Assert.That(error, Is.Not.Null);
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should throw when added with URL to the CSP page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenAddedWithURLToTheCSPPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/csp.html").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.AddScriptTagAsync(new() { Url = CrossProcessPrefix + "/injectedfile.js" }));
            Assert.That(error, Is.Not.Null);
        }

        [PlaywrightTest("page-add-script-tag.spec.ts", "should throw a nice error when the request fails")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowANiceErrorWhenTheRequestFails()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string url = Prefix + "/this_does_not_exist.js";
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.AddScriptTagAsync(new() { Url = url }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(url));
        }
    }
}
