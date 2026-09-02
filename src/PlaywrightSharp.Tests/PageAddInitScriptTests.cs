/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-add-init-script.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageAddInitScriptTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

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

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19223;
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

        [PlaywrightTest("page-add-init-script.spec.ts", "should evaluate before anything else on the page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEvaluateBeforeAnythingElseOnThePage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync("window['injected'] = 123;").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(123));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should work with a path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync(scriptPath: TestUtils.GetWebServerFile("injectedfile.js")).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(123));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should work with content")]
        [PlaywrightTest("page-add-init-script.spec.ts", "should work with content @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync("window[\"injected\"] = 123").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(123));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should throw without path and content")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWithoutPathAndContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(() => page.AddInitScriptAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Either path or content property must be present"));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should work with trailing comments")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithTrailingComments()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync("// comment").ConfigureAwait(false);
            await page.AddInitScriptAsync("window.secret = 42;").ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html></html>").ConfigureAwait(false);
            int secret = await page.EvaluateAsync<int>("secret").ConfigureAwait(false);
            Assert.That(secret, Is.EqualTo(42));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should support multiple scripts")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportMultipleScripts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync("window['script1'] = 1;").ConfigureAwait(false);
            await page.AddInitScriptAsync("window['script2'] = 2;").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            int script1 = await page.EvaluateAsync<int>("(() => window['script1'])()").ConfigureAwait(false);
            int script2 = await page.EvaluateAsync<int>("(() => window['script2'])()").ConfigureAwait(false);
            Assert.That(script1, Is.EqualTo(1));
            Assert.That(script2, Is.EqualTo(2));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should work with CSP")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCsp()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            try
            {
                Server.SetCSP("/empty.html", "script-src " + Prefix);
            }
            catch (ArgumentException)
            {
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.AddInitScriptAsync("window['injected'] = 123;").ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            int injected = await page.EvaluateAsync<int>("(() => window['injected'])()").ConfigureAwait(false);
            Assert.That(injected, Is.EqualTo(123));

            Assert.CatchAsync<PlaywrightSharpException>(() => page.AddScriptTagAsync(new() { Content = "window.e = 10;" }));
            object e = await page.EvaluateAsync<object>("(() => window['e'])()").ConfigureAwait(false);
            Assert.That(e, Is.Null);
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should work after a cross origin navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkAfterACrossOriginNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            await page.AddInitScriptAsync("window['injected'] = 123;").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(123));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should remove init script after dispose")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRemoveInitScriptAfterDispose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IAsyncDisposable disposable = await page.AddInitScriptAsync("window['injected'] = 123;").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(123));

            await disposable.DisposeAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            object after = await page.EvaluateAsync<object>("(() => window['result'])()").ConfigureAwait(false);
            Assert.That(after, Is.Null);
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should remove one of multiple init scripts after dispose")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRemoveOneOfMultipleInitScriptsAfterDispose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IAsyncDisposable disposable1 = await page.AddInitScriptAsync("window['script1'] = 1;").ConfigureAwait(false);
            await page.AddInitScriptAsync("window['script2'] = 2;").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            int script1 = await page.EvaluateAsync<int>("(() => window['script1'])()").ConfigureAwait(false);
            int script2 = await page.EvaluateAsync<int>("(() => window['script2'])()").ConfigureAwait(false);
            Assert.That(script1, Is.EqualTo(1));
            Assert.That(script2, Is.EqualTo(2));

            await disposable1.DisposeAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            object after1 = await page.EvaluateAsync<object>("(() => window['script1'])()").ConfigureAwait(false);
            int after2 = await page.EvaluateAsync<int>("(() => window['script2'])()").ConfigureAwait(false);
            Assert.That(after1, Is.Null);
            Assert.That(after2, Is.EqualTo(2));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "init script should run only once in iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task InitScriptShouldRunOnlyOnceInIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<string> messages = new List<string>();
            page.Console += (_, received) =>
            {
                string text = received.Text;
                if (text != null && text.StartsWith("init script:", StringComparison.Ordinal))
                {
                    messages.Add(text);
                }
            };

            await page.AddInitScriptAsync("() => console.log('init script:', location.pathname || 'no url yet')").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);

            string framePath = TestConstants.IsFirefox ? "no url yet" : "/frames/frame.html";
            Assert.That(messages, Is.EqualTo(new[]
            {
                "init script: /frames/one-frame.html",
                "init script: " + framePath,
            }));
        }
    }
}
