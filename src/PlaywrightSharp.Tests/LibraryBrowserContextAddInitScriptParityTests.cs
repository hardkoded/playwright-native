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
    /// Official <c>library/browsercontext-add-init-script.spec.ts</c> parity for
    /// <see cref="IBrowserContext.AddInitScriptAsync(string, string, object)"/>.
    /// Skipped (official <c>it.skip</c>): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextAddInitScriptParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19832;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    Prefix = "http://localhost:" + portText;
                    EmptyPage = Prefix + "/empty.html";
                    CrossProcessPrefix = "http://127.0.0.1:" + portText;
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                return;
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

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser == null || !_browser.IsConnected)
            {
                if (_browser != null)
                {
                    await RecycleBrowserAsync().ConfigureAwait(false);
                }
                else
                {
                    _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                }
            }

            try
            {
                _context = await NewContextOrRecycleAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                _context = await _browser.NewContextAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should work with browser context scripts @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBrowserContextScripts()
        {
            EnsureServer();
            await _context.AddInitScriptAsync("() => { window['temp'] = 123; }").ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.AddInitScriptAsync("() => { window['injected'] = window['temp']; }").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false),
                Is.EqualTo(123));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should work without navigation, after all bindings")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithoutNavigationAfterAllBindings()
        {
            EnsureServer();
            TaskCompletionSource<string> callback = new TaskCompletionSource<string>();
            await _context.ExposeFunctionAsync("woof", (string arg) => { callback.TrySetResult(arg); })
                .ConfigureAwait(false);
            await _context.AddInitScriptAsync(
                "() => { window['woof']('hey'); window['temp'] = 123; }").ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window['temp'])()").ConfigureAwait(false),
                Is.EqualTo(123));
            Assert.That(await callback.Task.ConfigureAwait(false), Is.EqualTo("hey"));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should work without navigation in popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithoutNavigationInPopup()
        {
            EnsureServer();
            await _context.AddInitScriptAsync("() => { window['temp'] = 123; }").ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("() => { window['win'] = window.open(); }").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(
                await popup.EvaluateAsync<int>("(() => window['temp'])()").ConfigureAwait(false),
                Is.EqualTo(123));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should work with browser context scripts with a path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBrowserContextScriptsWithAPath()
        {
            EnsureServer();
            await _context.AddInitScriptAsync(scriptPath: TestUtils.GetWebServerFile("injectedfile.js"))
                .ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false),
                Is.EqualTo(123));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should work with browser context scripts for already created pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBrowserContextScriptsForAlreadyCreatedPages()
        {
            EnsureServer();
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await _context.AddInitScriptAsync("() => { window['temp'] = 123; }").ConfigureAwait(false);
            await page.AddInitScriptAsync("() => { window['injected'] = window['temp']; }").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window['result'])()").ConfigureAwait(false),
                Is.EqualTo(123));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should remove context init script after dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveContextInitScriptAfterDispose()
        {
            EnsureServer();
            IAsyncDisposable disposable = await _context.AddInitScriptAsync("() => { window['temp'] = 123; }")
                .ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window['temp'])()").ConfigureAwait(false),
                Is.EqualTo(123));

            await disposable.DisposeAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => typeof window['temp'])()").ConfigureAwait(false),
                Is.EqualTo("undefined"));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should remove context init script and keep working in new pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveContextInitScriptAndKeepWorkingInNewPages()
        {
            EnsureServer();
            IAsyncDisposable disposable = await _context.AddInitScriptAsync("() => { window['temp'] = 123; }")
                .ConfigureAwait(false);
            await disposable.DisposeAsync().ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/tamperable.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => typeof window['temp'])()").ConfigureAwait(false),
                Is.EqualTo("undefined"));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should expose functions passed as arguments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeFunctionsPassedAsArguments()
        {
            EnsureServer();
            List<string> received = new List<string>();
            await _context.AddInitScriptExposingFunctionsAsync(@"async ({ cb }) => {
    await cb(location.href);
  }", new { cb = (Func<string, Task>)(href => { received.Add(href); return Task.CompletedTask; }) }).ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(
                () => Task.FromResult(received.IndexOf(EmptyPage) >= 0),
                "timed out waiting for init-script callback").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should expose functions that survive navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeFunctionsThatSurviveNavigation()
        {
            EnsureServer();
            List<int> received = new List<int>();
            await _context.AddInitScriptExposingFunctionsAsync(@"({ cb }) => { window.cb = cb; }", new { cb = (Func<int, int>)(n => { received.Add(n); return n * 2; }) }).ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window.cb(1))()").ConfigureAwait(false),
                Is.EqualTo(2));
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window.cb(2))()").ConfigureAwait(false),
                Is.EqualTo(4));
            Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should expose functions in popups")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeFunctionsInPopups()
        {
            EnsureServer();
            await _context.AddInitScriptExposingFunctionsAsync(@"({ mul }) => { window.mul = mul; }", new { mul = (Func<int, int, int>)((a, b) => a * b) }).ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("() => window.open('about:blank')").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(
                await popup.EvaluateAsync<int>("(() => window.mul(6, 7))()").ConfigureAwait(false),
                Is.EqualTo(42));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "should remove exposed functions after dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveExposedFunctionsAfterDispose()
        {
            EnsureServer();
            IAsyncDisposable disposable = await _context.AddInitScriptExposingFunctionsAsync(@"({ cb }) => { window.cb = cb; }", new { cb = (Func<int, int>)(n => n * 2) }).ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<int>("(() => window.cb(21))()").ConfigureAwait(false),
                Is.EqualTo(42));
            await disposable.DisposeAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => typeof window.cb)()").ConfigureAwait(false),
                Is.EqualTo("undefined"));
        }

        [PlaywrightTest("browsercontext-add-init-script.spec.ts", "init script should run only once in popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InitScriptShouldRunOnlyOnceInPopup()
        {
            EnsureServer();
            await _context.AddInitScriptAsync(
                "() => { window['callCount'] = (window['callCount'] || 0) + 1; }").ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("() => window.open('about:blank')").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(
                await popup.EvaluateAsync<int>("callCount").ConfigureAwait(false),
                Is.EqualTo(1));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task PollUntilAsync(Func<Task<bool>> ready, string message)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (await ready().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail(message);
        }

        private async Task<IBrowserContext> NewContextOrRecycleAsync()
        {
            Task<IBrowserContext> create = _browser.NewContextAsync();
            Task finished = await Task.WhenAny(create, Task.Delay(5000)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, create))
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                return await _browser.NewContextAsync().ConfigureAwait(false);
            }

            return await create.ConfigureAwait(false);
        }

        private async Task RecycleBrowserAsync()
        {
            IBrowser previous = _browser;
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (previous != null)
            {
                await DisposeQuietlyAsync(previous).ConfigureAwait(false);
            }
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
