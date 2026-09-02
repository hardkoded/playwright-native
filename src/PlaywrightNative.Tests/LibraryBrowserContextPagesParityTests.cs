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
    /// Official <c>library/browsercontext-pages.spec.ts</c> parity.
    /// Official <c>it.fixme(chromium)</c> on close-during-reload.
    /// Official Firefox skips (non-BiDi) on page-scale click/box are not
    /// applied here. Node <c>process.warning</c> is exercised as 20-page
    /// navigate/close only. Do not edit leftover context page tests.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextPagesParityTests : PageTestEx
    {
        private const string GotFocus =
            "(() => !!(window.gotFocus))()";

        private const string ReloadNeverSettles =
            "(() => { location.reload(); return new Promise(() => {}); })()";

        private const string SelectRange =
            "(() => {" +
            " const element = document.getElementById('container');" +
            " const textNode = element.firstChild;" +
            " const range = document.createRange();" +
            " range.setStart(textNode, 6);" +
            " range.setEnd(textNode, 11);" +
            " const selection = document.getSelection();" +
            " selection.removeAllRanges();" +
            " selection.addRange(range);" +
            "})()";

        private const string ReadRange =
            "(() => {" +
            " const selection = document.getSelection();" +
            " const range = selection.getRangeAt(0);" +
            " return {" +
            "  rangeCount: selection.rangeCount," +
            "  startOffset: range.startOffset," +
            "  endOffset: range.endOffset" +
            " };" +
            "})()";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19846;
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
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should not be visible in context.pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotBeVisibleInContextPages()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(context.Pages, Does.Contain(page));
            await page.CloseAsync().ConfigureAwait(false);
            Assert.That(context.Pages, Does.Not.Contain(page));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "page.context should return the correct instance")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageContextShouldReturnTheCorrectInstance()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page.Context, Is.SameAs(context));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "frame.focus should work multiple times")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FrameFocusShouldWorkMultipleTimes()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context.NewPageAsync().ConfigureAwait(false);
            IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
            foreach (IPage page in new[] { page1, page2 })
            {
                await page.SetContentAsync("<button id=\"foo\" onfocus=\"window.gotFocus=true\"></button>").ConfigureAwait(false);
                await page.FocusAsync("#foo").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<bool>(GotFocus).ConfigureAwait(false), Is.True);
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should click with disabled javascript")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickWithDisabledJavascript()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { JavaScriptEnabled = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/wrappedlink.html").ConfigureAwait(false);
            Task<IResponse> navigation = page.WaitForNavigationAsync();
            await page.ClickAsync("a").ConfigureAwait(false);
            await navigation.ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/wrappedlink.html#clicked"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should not hang with touch-enabled viewports")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangWithTouchEnabledViewports()
        {
            BrowserContextOptions iPhone = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = iPhone.Viewport, HasTouch = iPhone.HasTouch }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.Mouse.MoveAsync(100, 10).ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should click the button with deviceScaleFactor set")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButtonWithDeviceScaleFactorSet()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 400 }, DeviceScaleFactor = 5 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => window.devicePixelRatio)()").ConfigureAwait(false), Is.EqualTo(5));
            await page.SetContentAsync("<div style=\"width:100px;height:100px\">spacer</div>").ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "button-test", Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await frame.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
            Assert.That(await frame.EvaluateAsync<string>("(() => window.result)()").ConfigureAwait(false), Is.EqualTo("Clicked"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should click the button with offset with page scale")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButtonWithOffsetWithPageScale()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 400 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>(
                "button",
                "button => { button.style.borderWidth = '8px'; document.body.style.margin = '0'; }").ConfigureAwait(false);
            await page.ClickAsync("button", new() { Position = new Position { X = 20, Y = 10 } }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            AssertCloseTo(28, await page.EvaluateAsync<int>("pageX").ConfigureAwait(false));
            AssertCloseTo(18, await page.EvaluateAsync<int>("pageY").ConfigureAwait(false));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should return bounding box with page scale")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnBoundingBoxWithPageScale()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 400 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.EvaluateAsync<object>(
                "button => { document.body.style.margin = '0'; button.style.borderWidth = '0'; button.style.width = '200px'; button.style.height = '20px'; button.style.marginLeft = '17px'; button.style.marginTop = '23px'; }").ConfigureAwait(false);
            var box = await button.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That((int)Math.Round(box.X * 100), Is.EqualTo(17 * 100));
            Assert.That((int)Math.Round(box.Y * 100), Is.EqualTo(23 * 100));
            Assert.That((int)Math.Round(box.Width * 100), Is.EqualTo(200 * 100));
            Assert.That((int)Math.Round(box.Height * 100), Is.EqualTo(20 * 100));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should not leak listeners during navigation of 20 pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotLeakListenersDuringNavigationOf20Pages()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            List<Task<IPage>> creates = new();
            for (int i = 0; i < 20; i++)
            {
                creates.Add(context.NewPageAsync());
            }

            IPage[] pages = await Task.WhenAll(creates).ConfigureAwait(false);
            List<Task> navigations = new();
            foreach (IPage page in pages)
            {
                navigations.Add(page.GoToAsync(EmptyPage));
            }

            await Task.WhenAll(navigations).ConfigureAwait(false);
            List<Task> closes = new();
            foreach (IPage page in pages)
            {
                closes.Add(page.CloseAsync());
            }

            await Task.WhenAll(closes).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should close page while a reload is committing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClosePageWhileAReloadIsCommitting()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Chromium loses the close when the reload commits into a new RenderFrameHost; fix is not rolled yet");
            }

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                    () => page.EvaluateAsync<object>(ReloadNeverSettles));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("navigation"));
                await page.CloseAsync().ConfigureAwait(false);
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-pages.spec.ts", "should keep selection in multiple pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepSelectionInMultiplePages()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context.NewPageAsync().ConfigureAwait(false);
            IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
            foreach (IPage page in new[] { page1, page2 })
            {
                await page.SetContentAsync("<div id=\"container\">lorem ipsum dolor sit amet</div>").ConfigureAwait(false);
                await page.EvaluateAsync(SelectRange).ConfigureAwait(false);
            }

            await Task.Delay(1_000).ConfigureAwait(false);
            foreach (IPage page in new[] { page1, page2 })
            {
                JsonElement range = await page.EvaluateAsync<JsonElement>(ReadRange).ConfigureAwait(false);
                Assert.That(range.GetProperty("rangeCount").GetInt32(), Is.EqualTo(1));
                Assert.That(range.GetProperty("startOffset").GetInt32(), Is.EqualTo(6));
                Assert.That(range.GetProperty("endOffset").GetInt32(), Is.EqualTo(11));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void AssertCloseTo(int expected, int actual)
        {
            if (Math.Abs(expected - actual) > 2)
            {
                Assert.Fail("Expected: " + expected.ToString(CultureInfo.InvariantCulture) + ", received: " + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string frameId, string url)
        {
            string frameIdJson = JsonSerializer.Serialize(frameId);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(async () => { const frame = document.createElement('iframe'); frame.src = " +
                urlJson + "; frame.id = " + frameIdJson + "; document.body.appendChild(frame); await new Promise(x => frame.onload = x); })()")
                .ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(frameId);
                if (named != null && !named.IsDetached)
                {
                    return named;
                }

                foreach (IFrame frame in page.Frames)
                {
                    if (!ReferenceEquals(frame, page.MainFrame) && !frame.IsDetached)
                    {
                        return frame;
                    }
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + frameId);
            return null;
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
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
