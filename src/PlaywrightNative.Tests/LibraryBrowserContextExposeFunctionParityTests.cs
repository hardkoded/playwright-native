/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-expose-function.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextExposeTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextExposeFunctionParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19840;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = Prefix + "/empty.html";
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
                    await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                }

                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }

            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "expose binding should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ExposeBindingShouldWork()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            BindingSource bindingSource = null;
            await context.ExposeBindingAsync("add", (BindingSource source, int a, int b) =>
            {
                bindingSource = source;
                return a + b;
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(
                "(async function() { return window['add'](5, 6); })()").ConfigureAwait(false);
            Assert.That(bindingSource, Is.Not.Null);
            Assert.That(bindingSource.Context, Is.SameAs(context));
            Assert.That(bindingSource.Page, Is.SameAs(page));
            Assert.That(bindingSource.Frame, Is.SameAs(page.MainFrame));
            Assert.That(result, Is.EqualTo(11));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.ExposeFunctionAsync("add", (int a, int b) => a + b).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.ExposeFunctionAsync("mul", (int a, int b) => a * b).ConfigureAwait(false);
            await context.ExposeFunctionAsync("sub", (int a, int b) => a - b).ConfigureAwait(false);
            await context.ExposeBindingAsync("addHandle", async (BindingSource source, int a, int b) =>
            {
                IJSHandle handle = await source.Frame.EvaluateHandleAsync(
                    "(([x, y]) => x + y)",
                    new[] { a, b }).ConfigureAwait(false);
                return handle;
            }).ConfigureAwait(false);
            JsonElement result = await page.EvaluateAsync<JsonElement>(
                "(async () => ({ mul: await mul(9, 4), add: await add(9, 4), sub: await sub(9, 4), addHandle: await addHandle(5, 6) }))()").ConfigureAwait(false);
            Assert.That(result.GetProperty("mul").GetInt32(), Is.EqualTo(36));
            Assert.That(result.GetProperty("add").GetInt32(), Is.EqualTo(13));
            Assert.That(result.GetProperty("sub").GetInt32(), Is.EqualTo(5));
            Assert.That(result.GetProperty("addHandle").GetInt32(), Is.EqualTo(11));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "should dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispose()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAsyncDisposable binding = await context.ExposeFunctionAsync("compute", (int a, int b) => a * b).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(
                "(async function() { return await window['compute'](9, 4); })()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(36));
            await binding.DisposeAsync().ConfigureAwait(false);

            PlaywrightNativeException exception = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await page.EvaluateAsync<int>(
                    "(async function() { return await window['compute'](9, 4); })()").ConfigureAwait(false));
            Assert.That(exception.Message, Does.Contain("is not a function"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "should throw for duplicate registrations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForDuplicateRegistrations()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.ExposeFunctionAsync("foo", () => { }).ConfigureAwait(false);
            await context.ExposeFunctionAsync("bar", () => { }).ConfigureAwait(false);
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await context.ExposeFunctionAsync("foo", () => { }).ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("Function \"foo\" has been already registered"));
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await page.ExposeFunctionAsync("foo", () => { }).ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("Function \"foo\" has been already registered in the browser context"));
            await page.ExposeFunctionAsync("baz", () => { }).ConfigureAwait(false);
            error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await context.ExposeFunctionAsync("baz", () => { }).ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("Function \"baz\" has been already registered in one of the pages"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "should be callable from-inside addInitScript")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeCallableFromInsideAddInitScript()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            List<string> args = new List<string>();
            await context.ExposeFunctionAsync("woof", (string arg) => { args.Add(arg); }).ConfigureAwait(false);
            await context.AddInitScriptAsync("window[\"woof\"](\"context\")").ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.EvaluateAsync("undefined").ConfigureAwait(false);
            await PollEqualAsync(() => Task.FromResult(string.Join(",", args)), "context").ConfigureAwait(false);
            args.Clear();
            await page.AddInitScriptAsync("window[\"woof\"](\"page\")").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            await PollEqualAsync(() => Task.FromResult(string.Join(",", args)), "context,page").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-expose-function.spec.ts", "should work with CSP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCsp()
        {
            EnsureServer();
            Server.SetCSP("/empty.html", "default-src \"self\"");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            bool called = false;
            await context.ExposeBindingAsync("hi", () => { called = true; }).ConfigureAwait(false);
            await page.EvaluateAsync("(() => window.hi())()").ConfigureAwait(false);
            Assert.That(called, Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new List<IBrowserContext>(_browser.Contexts))
            {
                try
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task PollEqualAsync<T>(Func<Task<T>> getValue, T expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            T last = default;
            while (DateTime.UtcNow < deadline)
            {
                last = await getValue().ConfigureAwait(false);
                if (Equals(last, expected))
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(last, Is.EqualTo(expected));
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
