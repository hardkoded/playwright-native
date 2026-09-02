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
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-add-init-script-callback.spec.ts</c> parity.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageAddInitScriptCallbackParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

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
            int basePort = 19920;
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

        [SetUp]
        public async Task SetUpAsync()
        {
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            try
            {
                if (_context != null)
                {
                    await _context.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                if (_browser != null)
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private IPage Page => _page;

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

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should drop functions without the exposeFunctions option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDropFunctionsWithoutTheExposeFunctionsOption()
        {
            await Page.AddInitScriptAsync(
                @"({ cb }) => { window.cbType = typeof cb; }",
                new { cb = (Action)(() => { }) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<string>("() => window.cbType").ConfigureAwait(false), Is.EqualTo("undefined"));
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should throw when the script is not a function")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenTheScriptIsNotAFunction()
        {
            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.AddInitScriptExposingFunctionsAsync("window.foo = 1;", null));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("Passing functions requires the init script to be a function"));
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should call a function passed as an argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCallAFunctionPassedAsAnArgument()
        {
            List<int> received = new List<int>();
            await Page.AddInitScriptExposingFunctionsAsync(@"async ({ cb }) => {
    await cb(1);
    await cb(2);
  }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(
                () => Task.FromResult(received.Count == 2),
                "timed out waiting for init-script callbacks").ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should accept a function as the whole argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptAFunctionAsTheWholeArgument()
        {
            await Page.AddInitScriptExposingFunctionsAsync(@"async cb => { window.result = await cb('a'); }", (Func<string, Task<string>>)(s => Task.FromResult(s + "b"))).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await Page.EvaluateAsync<string>("() => window.result").ConfigureAwait(false)) == "ab",
                "timed out waiting for init-script result").ConfigureAwait(false);
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should pass arguments to the callback")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassArgumentsToTheCallback()
        {
            TaskCompletionSource<object[]> tcs = new TaskCompletionSource<object[]>();
            await Page.AddInitScriptExposingFunctionsAsync(@"({ cb }) => cb(1, 'two', { three: 3 }, [4])", new
            {
                cb = (Func<int, string, JsonElement, JsonElement, object>)((n, s, o, a) =>
            {
                tcs.TrySetResult(new object[] { n, s, o, a });
                return null;
            })
            }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            object[] args = await tcs.Task.ConfigureAwait(false);
            Assert.That((int)args[0], Is.EqualTo(1));
            Assert.That((string)args[1], Is.EqualTo("two"));
            Assert.That(((JsonElement)args[2]).GetProperty("three").GetInt32(), Is.EqualTo(3));
            Assert.That(((JsonElement)args[3])[0].GetInt32(), Is.EqualTo(4));
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should return the callback result to the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnTheCallbackResultToThePage()
        {
            await Page.AddInitScriptExposingFunctionsAsync(@"async ({ double }) => { window.result = await double(21); }", new { @double = (Func<int, Task<int>>)(n => Task.FromResult(n * 2)) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(
                async () => await Page.EvaluateAsync<int>("() => window.result").ConfigureAwait(false) == 42,
                "timed out waiting for doubled result").ConfigureAwait(false);
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should propagate callback errors to the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateCallbackErrorsToThePage()
        {
            await Page.AddInitScriptExposingFunctionsAsync(@"async ({ cb }) => {
    try {
      await cb();
      window.result = 'no error';
    } catch (e) {
      window.result = e.message;
    }
  }", new { cb = (Func<Task>)(() => throw new InvalidOperationException("boom")) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(
                async () =>
                {
                    string message = await Page.EvaluateAsync<string>("() => window.result").ConfigureAwait(false);
                    return message != null && message.Contains("boom", StringComparison.Ordinal);
                },
                "timed out waiting for callback error").ConfigureAwait(false);
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should support multiple callbacks")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipleCallbacks()
        {
            await Page.AddInitScriptExposingFunctionsAsync(@"async ({ add, mul }) => {
    window.result = (await add(2, 3)) + (await mul(2, 3));
  }", new
            {
                add = (Func<int, int, Task<int>>)((a, b) => Task.FromResult(a + b)),
                mul = (Func<int, int, Task<int>>)((a, b) => Task.FromResult(a * b)),
            }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(
                async () => await Page.EvaluateAsync<int>("() => window.result").ConfigureAwait(false) == 11,
                "timed out waiting for combined result").ConfigureAwait(false);
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should survive a navigation and keep working")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveANavigationAndKeepWorking()
        {
            List<int> received = new List<int>();
            await Page.AddInitScriptExposingFunctionsAsync(@"({ cb }) => { window.cb = cb; }", new { cb = (Func<int, int>)(n => { received.Add(n); return n * 2; }) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<int>("() => window.cb(1)").ConfigureAwait(false), Is.EqualTo(2));
            await Page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<int>("() => window.cb(2)").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should work in a child frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkInAChildFrame()
        {
            List<int> received = new List<int>();
            await Page.AddInitScriptExposingFunctionsAsync(@"async ({ cb }) => { await cb(42); }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            await Page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            await PollUntilAsync(
                () => Task.FromResult(received.Count == 2),
                "timed out waiting for main and child frame callbacks").ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { 42, 42 }));
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should not register the callback on the global object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRegisterTheCallbackOnTheGlobalObject()
        {
            await Page.AddInitScriptExposingFunctionsAsync(@"async ({ cb }) => {
    await cb();
    window.result = Object.getOwnPropertyNames(globalThis).filter(name => name.startsWith('__pw_fn_'));
  }", new { cb = (Func<Task>)(() => Task.CompletedTask) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(
                async () =>
                {
                    string[] names = await Page.EvaluateAsync<string[]>("() => window.result").ConfigureAwait(false);
                    return names != null && names.Length == 0;
                },
                "timed out waiting for empty global names").ConfigureAwait(false);
        }

        [PlaywrightTest("page-add-init-script-callback.spec.ts", "should remove exposed functions after dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveExposedFunctionsAfterDispose()
        {
            List<int> received = new List<int>();
            IAsyncDisposable disposable = await Page.AddInitScriptExposingFunctionsAsync(@"({ cb }) => { window.cb = cb; }", new { cb = (Func<int, object>)(n => { received.Add(n); return null; }) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.EvaluateAsync("() => window.cb(1)").ConfigureAwait(false);
            await disposable.DisposeAsync().ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<string>("() => typeof window.cb").ConfigureAwait(false), Is.EqualTo("undefined"));
            Assert.That(received, Is.EqualTo(new[] { 1 }));
        }
    }
}
