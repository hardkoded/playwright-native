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
    /// Official <c>page-evaluate-callback.spec.ts</c> parity.
    /// Skipped playbook Node-only: <c>page-evaluate-no-stall.spec.ts</c> (toImpl),
    /// <c>page-leaks.spec.ts</c> (toImpl).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageEvaluateCallbackParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19875;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    EmptyPage = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/empty.html";
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

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson +
                "; f.id = " +
                nameJson +
                "; f.src = " +
                urlJson +
                "; document.body.appendChild(f); })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named != null && !named.IsDetached)
                {
                    return named;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should throw without the exposeFunctions option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWithoutTheExposeFunctionsOption()
        {
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => Page.EvaluateAsync("({ cb }) => cb()", new { cb = (Action)(() => { }) }));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Match(@"Attempting to serialize unexpected value at position ""cb"": \(\) => \{\}"));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should call a function passed as an argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCallAFunctionPassedAsAnArgument()
        {
            List<int> received = new List<int>();
            await Page.EvaluateExposingFunctionsAsync<object>(@"async ({ cb }) => {
    await cb(1);
    await cb(2);
  }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should accept a function as the whole argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptAFunctionAsTheWholeArgument()
        {
            List<string> received = new List<string>();
            await Page.EvaluateExposingFunctionsAsync<object>(@"async cb => {
    await cb('a');
    await cb('b');
  }", (Func<string, Task>)(s => { received.Add(s); return Task.CompletedTask; })).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { "a", "b" }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should pass arguments to the callback")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassArgumentsToTheCallback()
        {
            TaskCompletionSource<object[]> tcs = new TaskCompletionSource<object[]>();
            _ = Page.EvaluateExposingFunctionsAsync<object>(@"({ cb }) => cb(1, 'two', { three: 3 }, [4])", new
            {
                cb = (Func<int, string, JsonElement, JsonElement, object>)((n, s, o, a) =>
            {
                tcs.TrySetResult(new object[] { n, s, o, a });
                return null;
            })
            });
            object[] args = await tcs.Task.ConfigureAwait(false);
            Assert.That((int)args[0], Is.EqualTo(1));
            Assert.That((string)args[1], Is.EqualTo("two"));
            Assert.That(((JsonElement)args[2]).GetProperty("three").GetInt32(), Is.EqualTo(3));
            Assert.That(((JsonElement)args[3])[0].GetInt32(), Is.EqualTo(4));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should return the callback result to the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnTheCallbackResultToThePage()
        {
            int doubled = await Page.EvaluateExposingFunctionsAsync<int>(@"async ({ cb }) => await cb(21)", new { cb = (Func<int, Task<int>>)(n => Task.FromResult(n * 2)) }).ConfigureAwait(false);
            Assert.That(doubled, Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should support handle as a callback result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHandleAsACallbackResult()
        {
            int result = await Page.EvaluateExposingFunctionsAsync<int>(@"async cb => {
    const value = await cb(42);
    return value + 17;
  }", (Func<int, Task<IJSHandle>>)(n => Page.EvaluateHandleAsync("x => 2 * x", n))).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(101));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should support nested handles in the callback result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportNestedHandlesInTheCallbackResult()
        {
            int result = await Page.EvaluateExposingFunctionsAsync<int>(@"async cb => {
    const res = await cb(42);
    return res.mul[0] + res.mul[1] + res.add;
  }", (Func<int, Task<object>>)(async n =>
                {
                    IJSHandle doubled = await Page.EvaluateHandleAsync("x => 2 * x", n).ConfigureAwait(false);
                    IJSHandle triple = await Page.EvaluateHandleAsync("x => 3 * x", n).ConfigureAwait(false);
                    return new { mul = new[] { doubled, triple }, add = 17 };
                })).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(227));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should await an async callback result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitAnAsyncCallbackResult()
        {
            int value = await Page.EvaluateExposingFunctionsAsync<int>(@"async ({ cb }) => await cb(20)", new
            {
                cb = (Func<int, Task<int>>)(async n =>
            {
                await Task.Delay(10).ConfigureAwait(false);
                return n + 1;
            })
            }).ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(21));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should propagate callback errors to the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateCallbackErrorsToThePage()
        {
            string message = await Page.EvaluateExposingFunctionsAsync<string>(@"async ({ cb }) => {
    try {
      await cb();
      return 'no error';
    } catch (e) {
      return e.message;
    }
  }", new { cb = (Func<Task>)(() => throw new InvalidOperationException("boom")) }).ConfigureAwait(false);
            Assert.That(message, Does.Contain("boom"));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should work with a fire-and-forget setTimeout callback")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAFireAndForgetSetTimeoutCallback()
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            _ = Page.EvaluateExposingFunctionsAsync<object>(@"({ cb }) => { setTimeout(() => cb(5), 0); }", new { cb = (Func<int, object>)(n => { tcs.TrySetResult(n); return null; }) });
            Assert.That(await tcs.Task.ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should support multiple callbacks")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipleCallbacks()
        {
            int result = await Page.EvaluateExposingFunctionsAsync<int>(@"async ({ add, mul }) => {
    return (await add(2, 3)) + (await mul(2, 3));
  }", new
            {
                add = (Func<int, int, Task<int>>)((a, b) => Task.FromResult(a + b)),
                mul = (Func<int, int, Task<int>>)((a, b) => Task.FromResult(a * b)),
            }).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(11));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should work with evaluateHandle")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithEvaluateHandle()
        {
            List<int> received = new List<int>();
            IJSHandle handle = await Page.EvaluateHandleExposingFunctionsAsync(@"async ({ cb }) => {
    await cb(7);
    return { done: true };
  }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(await handle.JsonValueAsync<Dictionary<string, bool>>().ConfigureAwait(false), Is.EqualTo(new Dictionary<string, bool> { ["done"] = true }));
            Assert.That(received, Is.EqualTo(new[] { 7 }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should work in a child frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkInAChildFrame()
        {
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(Page, "frame1", EmptyPage).ConfigureAwait(false);
            List<int> received = new List<int>();
            await frame.EvaluateExposingFunctionsAsync<object>(@"async ({ cb }) => { await cb(42); }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { 42 }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should route callbacks back to the calling frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRouteCallbacksBackToTheCallingFrame()
        {
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(Page, "frame1", EmptyPage).ConfigureAwait(false);
            Func<string, Task<string>> greet = where => Task.FromResult("hello " + where);
            Task<string> fromMain = Page.EvaluateExposingFunctionsAsync<string>(@"async ({ cb }) => await cb('main')", new { cb = greet });
            Task<string> fromChild = frame.EvaluateExposingFunctionsAsync<string>(@"async ({ cb }) => await cb('child')", new { cb = greet });
            Assert.That(await fromMain.ConfigureAwait(false), Is.EqualTo("hello main"));
            Assert.That(await fromChild.ConfigureAwait(false), Is.EqualTo("hello child"));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should work with jsHandle.evaluate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithJsHandleEvaluate()
        {
            IJSHandle handle = await Page.EvaluateHandleAsync("() => window").ConfigureAwait(false);
            List<int> received = new List<int>();
            await handle.EvaluateExposingFunctionsAsync<object>(@"async (win, { cb }) => { await cb(99); }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { 99 }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should work with locator.evaluate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLocatorEvaluate()
        {
            await Page.SetContentAsync("<div id=target>hello</div>").ConfigureAwait(false);
            List<string> received = new List<string>();
            await Page.Locator("#target").EvaluateExposingFunctionsAsync<object>(@"async (element, { cb }) => { await cb(element.id); }", new { cb = (Func<string, Task>)(s => { received.Add(s); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { "target" }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should return the callback result with locator.evaluate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnTheCallbackResultWithLocatorEvaluate()
        {
            await Page.SetContentAsync("<div id=target>7</div>").ConfigureAwait(false);
            int result = await Page.Locator("#target").EvaluateExposingFunctionsAsync<int>(@"async (element, { double }) => {
    return await double(+element.textContent);
  }", new { @double = (Func<int, Task<int>>)(n => Task.FromResult(n * 2)) }).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(14));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should propagate callback errors with locator.evaluate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateCallbackErrorsWithLocatorEvaluate()
        {
            await Page.SetContentAsync("<div id=target></div>").ConfigureAwait(false);
            string message = await Page.Locator("#target").EvaluateExposingFunctionsAsync<string>(@"async (element, { cb }) => {
    try {
      await cb();
      return 'no error';
    } catch (e) {
      return e.message;
    }
  }", new { cb = (Func<Task>)(() => throw new InvalidOperationException("boom")) }).ConfigureAwait(false);
            Assert.That(message, Does.Contain("boom"));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should work with locator.evaluateHandle")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLocatorEvaluateHandle()
        {
            await Page.SetContentAsync("<div id=target>hello</div>").ConfigureAwait(false);
            List<string> received = new List<string>();
            IJSHandle handle = await Page.Locator("#target").EvaluateHandleExposingFunctionsAsync(@"async (element, { cb }) => {
    await cb(element.id);
    return element;
  }", new { cb = (Func<string, Task>)(s => { received.Add(s); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { "target" }));
            Assert.That(await handle.EvaluateAsync<string>("element => element.id").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should work with locator.evaluate inside an iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLocatorEvaluateInsideAnIframe()
        {
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(Page, "frame1", EmptyPage).ConfigureAwait(false);
            await frame.EvaluateAsync("() => { document.body.innerHTML = '<div id=target>in-frame</div>'; }").ConfigureAwait(false);
            List<string> received = new List<string>();
            await Page.FrameLocator("#frame1").Locator("#target").EvaluateExposingFunctionsAsync<object>(@"async (element, { cb }) => {
    await cb(element.textContent);
  }", new { cb = (Func<string, Task>)(text => { received.Add(text); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { "in-frame" }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should survive a navigation and keep working")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveANavigationAndKeepWorking()
        {
            List<int> received = new List<int>();
            await Page.EvaluateExposingFunctionsAsync<object>(@"async ({ cb }) => { await cb(1); }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.EvaluateExposingFunctionsAsync<object>(@"async ({ cb }) => { await cb(2); }", new { cb = (Func<int, Task>)(n => { received.Add(n); return Task.CompletedTask; }) }).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo(new[] { 1, 2 }));
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should not register the callback on the global object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRegisterTheCallbackOnTheGlobalObject()
        {
            string[] result = await Page.EvaluateExposingFunctionsAsync<string[]>(@"async ({ cb }) => {
    await cb();
    return Object.getOwnPropertyNames(globalThis).filter(name => name.startsWith('__pw_fn_'));
  }", new { cb = (Func<Task>)(() => Task.CompletedTask) }).ConfigureAwait(false);
            Assert.That(result, Is.Empty);
        }

        [PlaywrightTest("page-evaluate-callback.spec.ts", "should scope the page-side callback to the execution context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScopeThePageSideCallbackToTheExecutionContext()
        {
            await Page.EvaluateExposingFunctionsAsync<object>(@"({ cb }) => { window.__cb = cb; }", new { cb = (Action)(() => { }) }).ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<string>("() => typeof window.__cb").ConfigureAwait(false), Is.EqualTo("function"));
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<string>("() => typeof window.__cb").ConfigureAwait(false), Is.EqualTo("undefined"));
        }
    }
}
