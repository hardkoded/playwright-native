/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-wait-for-selector-1.spec.ts</c> parity for
    /// <see cref="IPage.WaitForSelectorAsync"/> attach logs, shadow/innerHTML,
    /// and element-handle waits.
    /// Official <c>options.waitFor</c> is
    /// <see cref="IPage.WaitForSelectorAsync(string, WaitForSelectorState, float?, bool?, string, string)"/>.
    /// Do not edit leftover <c>WaitForSelectorTests.cs</c>.
    /// </summary>
    [TestFixture]
    public class PageWaitForSelector1ParityTests : PageTestEx
    {
        private const string AddElement = "tag => document.body.appendChild(document.createElement(tag))";

        private static SimpleServer _ownedServer;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19794;
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

        [SetUp]
        public void ResetOwnedRoutes()
        {
            _ownedServer?.Reset();
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should throw on waitFor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnWaitFor()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.WaitForSelectorAsync("*", new PageWaitForSelectorOptions { WaitFor = "attached" }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("options.waitFor is not supported, did you mean options.state?"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should tolerate waitFor=visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTolerateWaitForVisible()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            bool error = false;
            try
            {
                await page.WaitForSelectorAsync("*", new PageWaitForSelectorOptions { WaitFor = "visible" }).ConfigureAwait(false);
            }
            catch (Exception)
            {
                error = true;
            }

            Assert.That(error, Is.False);
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should immediately resolve promise if node exists")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldImmediatelyResolvePromiseIfNodeExists()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            await frame.WaitForSelectorAsync("*").ConfigureAwait(false);
            await frame.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            await frame.WaitForSelectorAsync("div", WaitForSelectorState.Attached).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "elementHandle.waitForSelector should immediately resolve if node exists")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementHandleWaitForSelectorShouldImmediatelyResolveIfNodeExists()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<span>extra</span><div><span>target</span></div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            IElementHandle span = await div.WaitForSelectorAsync("span", WaitForSelectorState.Attached).ConfigureAwait(false);
            Assert.That(await span.EvaluateAsync<string>("e => e.textContent").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "elementHandle.waitForSelector should wait")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementHandleWaitForSelectorShouldWait()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Task<IElementHandle> promise = div.WaitForSelectorAsync("span", WaitForSelectorState.Attached);
            await div.EvaluateAsync<object>("div => div.innerHTML = '<span>target</span>'").ConfigureAwait(false);
            IElementHandle span = await promise.ConfigureAwait(false);
            Assert.That(await span.EvaluateAsync<string>("e => e.textContent").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "elementHandle.waitForSelector should timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementHandleWaitForSelectorShouldTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => div.WaitForSelectorAsync("span", new() { Timeout = 100 }));
            Assert.That(error.Message, Does.Contain("Timeout 100ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "elementHandle.waitForSelector should throw on navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementHandleWaitForSelectorShouldThrowOnNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Task<Exception> promise = div.WaitForSelectorAsync("span").ContinueWith(
                t => t.Exception?.GetBaseException(),
                TaskScheduler.Default);
            for (int i = 0; i < 10; i++)
            {
                await page.EvaluateAsync<object>("() => 1").ConfigureAwait(false);
            }

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Exception error = await promise.ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("waiting for locator('span') to be visible"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should work with removed MutationObserver")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithRemovedMutationObserver()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>("() => { delete window.MutationObserver; }").ConfigureAwait(false);
            Task<IElementHandle> waitForSelector = page.WaitForSelectorAsync(".zombo");
            await Task.WhenAll(
                waitForSelector,
                page.SetContentAsync("<div class='zombo'>anything</div>")).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("x => x.textContent", await waitForSelector.ConfigureAwait(false)).ConfigureAwait(false),
                Is.EqualTo("anything"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should resolve promise when node is added")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolvePromiseWhenNodeIsAdded()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            Task<IElementHandle> watchdog = frame.WaitForSelectorAsync("div", WaitForSelectorState.Attached);
            await frame.EvaluateAsync<object>(AddElement, "br").ConfigureAwait(false);
            await frame.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            IElementHandle eHandle = await watchdog.ConfigureAwait(false);
            IJSHandle tagProperty = await eHandle.GetPropertyAsync("tagName").ConfigureAwait(false);
            string tagName = await tagProperty.JsonValueAsync<string>().ConfigureAwait(false);
            Assert.That(tagName, Is.EqualTo("DIV"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should report logs while waiting for visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportLogsWhileWaitingForVisible()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            Task<IElementHandle> watchdog = frame.WaitForSelectorAsync("div", new() { Timeout = 5000 });

            await frame.EvaluateAsync<object>(@"() => {
              const div = document.createElement('div');
              div.className = 'foo bar';
              div.id = 'mydiv';
              div.setAttribute('style', 'display: none');
              div.setAttribute('foo', '123456789012345678901234567890123456789012345678901234567890');
              div.textContent = 'abcdefghijklmnopqrstuvwyxzabcdefghijklmnopqrstuvwyxzabcdefghijklmnopqrstuvwyxz';
              document.body.appendChild(div);
            }").ConfigureAwait(false);
            await GiveItTimeToLogAsync(frame).ConfigureAwait(false);

            await frame.EvaluateAsync<object>("() => document.querySelector('div').remove()").ConfigureAwait(false);
            await GiveItTimeToLogAsync(frame).ConfigureAwait(false);

            await frame.EvaluateAsync<object>(@"() => {
              const div = document.createElement('div');
              div.className = 'another';
              div.style.display = 'none';
              document.body.appendChild(div);
            }").ConfigureAwait(false);
            await GiveItTimeToLogAsync(frame).ConfigureAwait(false);

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(() => watchdog);
            Assert.That(error.Message, Does.Contain("frame.waitForSelector: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("waiting for locator('div') to be visible"));
            Assert.That(
                error.Message,
                Does.Contain("locator resolved to hidden <div id=\"mydiv\" class=\"foo bar\" foo=\"123456789012345678901234567890123456789012345678901234567890\">abcdefghijklmnopqrstuvwyxzabcdefghijklmnopqrstuvw…</div>"));
            Assert.That(error.Message, Does.Contain("locator resolved to hidden <div class=\"another\"></div>"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should report logs while waiting for hidden")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportLogsWhileWaitingForHidden()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            await frame.EvaluateAsync<object>(@"() => {
              const div = document.createElement('div');
              div.className = 'foo bar';
              div.id = 'mydiv';
              div.textContent = 'hello';
              document.body.appendChild(div);
            }").ConfigureAwait(false);

            Task<IElementHandle> watchdog = frame.WaitForSelectorAsync("div", WaitForSelectorState.Hidden, 5000);
            await GiveItTimeToLogAsync(frame).ConfigureAwait(false);

            await frame.EvaluateAsync<object>(@"() => {
              document.querySelector('div').remove();
              const div = document.createElement('div');
              div.className = 'another';
              div.textContent = 'hello';
              document.body.appendChild(div);
            }").ConfigureAwait(false);
            await GiveItTimeToLogAsync(frame).ConfigureAwait(false);

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(() => watchdog);
            Assert.That(error.Message, Does.Contain("frame.waitForSelector: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("waiting for locator('div') to be hidden"));
            Assert.That(error.Message, Does.Contain("locator resolved to visible <div id=\"mydiv\" class=\"foo bar\">hello</div>"));
            Assert.That(error.Message, Does.Contain("locator resolved to visible <div class=\"another\">hello</div>"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should report logs when the selector resolves to multiple elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportLogsWhenTheSelectorResolvesToMultipleElements()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button style=""display: none; position: absolute; top: 0px; left: 0px; width: 100%;"">Reset</button>
    <button>Reset</button>
  ").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.ClickAsync("text=Reset", new() { Timeout = 1000 }));
            Assert.That(
                error.ToString(),
                Does.Contain("locator resolved to 2 elements. Proceeding with the first one: <button>Reset</button>"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should resolve promise when node is added in shadow dom")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolvePromiseWhenNodeIsAddedInShadowDom()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IElementHandle> watchdog = page.WaitForSelectorAsync("span");
            await page.EvaluateAsync<object>(@"() => {
              const div = document.createElement('div');
              div.attachShadow({ mode: 'open' });
              document.body.appendChild(div);
            }").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(100).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
              const span = document.createElement('span');
              span.textContent = 'Hello from shadow';
              document.querySelector('div').shadowRoot.appendChild(span);
            }").ConfigureAwait(false);
            IElementHandle handle = await watchdog.ConfigureAwait(false);
            Assert.That(await handle.EvaluateAsync<string>("e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from shadow"));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should work when node is added through innerHTML")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenNodeIsAddedThroughInnerHTML()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IElementHandle> watchdog = page.WaitForSelectorAsync("h3 div", WaitForSelectorState.Attached);
            await page.EvaluateAsync<object>(AddElement, "span").ConfigureAwait(false);
            await page.EvaluateAsync<object>("() => document.querySelector('span').innerHTML = '<h3><div></div></h3>'").ConfigureAwait(false);
            await watchdog.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "page.waitForSelector is shortcut for main frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageWaitForSelectorIsShortcutForMainFrame()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame otherFrame = page.Frame("frame1");
            Task<IElementHandle> watchdog = page.WaitForSelectorAsync("div", WaitForSelectorState.Attached);
            await otherFrame.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            await page.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            IElementHandle eHandle = await watchdog.ConfigureAwait(false);
            Assert.That(await eHandle.OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should run in specified frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunInSpecifiedFrame()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame2", EmptyPage).ConfigureAwait(false);
            IFrame frame1 = page.Frame("frame1");
            IFrame frame2 = page.Frame("frame2");
            Task<IElementHandle> waitForSelectorPromise = frame2.WaitForSelectorAsync("div", WaitForSelectorState.Attached);
            await frame1.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            await frame2.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            IElementHandle eHandle = await waitForSelectorPromise.ConfigureAwait(false);
            Assert.That(await eHandle.OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(frame2));
        }

        [PlaywrightTest("page-wait-for-selector-1.spec.ts", "should throw when frame is detached")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenFrameIsDetached()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = page.Frame("frame1");
            Exception waitError = null;
            Task waitPromise = frame.WaitForSelectorAsync(".box").ContinueWith(
                t =>
                {
                    waitError = t.Exception?.GetBaseException();
                    return t.Exception;
                },
                TaskScheduler.Default);
            await DetachFrameAsync(page, "frame1").ConfigureAwait(false);
            await waitPromise.ConfigureAwait(false);
            Assert.That(waitError, Is.Not.Null);
            Assert.That(waitError.Message, Does.Contain("frame.waitForSelector: Frame was detached"));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task GiveItTimeToLogAsync(IFrame frame)
        {
            await frame.EvaluateAsync<object>("() => new Promise(f => requestAnimationFrame(() => requestAnimationFrame(f)))").ConfigureAwait(false);
            await frame.EvaluateAsync<object>("() => new Promise(f => requestAnimationFrame(() => requestAnimationFrame(f)))").ConfigureAwait(false);
        }

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
                if (named != null)
                {
                    try
                    {
                        await named.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        private static async Task DetachFrameAsync(IPage page, string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.getElementById(" + nameJson + "); if (f) f.remove(); })()")
                .ConfigureAwait(false);
        }
    }
}
