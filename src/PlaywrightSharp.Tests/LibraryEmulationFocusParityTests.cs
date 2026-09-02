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
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/emulation-focus.spec.ts</c> parity.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryEmulationFocusParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19874;
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
            if (_browser == null || !_browser.IsConnected)
            {
                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }

            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should think that it is focused by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThinkThatItIsFocusedByDefault()
        {
            Assert.That(await _page.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should think that all pages are focused @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThinkThatAllPagesAreFocused()
        {
            IPage page2 = await _page.Context.NewPageAsync().ConfigureAwait(false);
            Assert.That(await _page.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false), Is.True);
            Assert.That(await page2.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false), Is.True);
            await page2.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should focus popups by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFocusPopupsByDefault()
        {
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = _page.WaitForPopupAsync();
            await _page.EvaluateAsync("url => { window.open(url); }", EmptyPage).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(await popup.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false), Is.True);
            Assert.That(await _page.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should provide target for keyboard events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProvideTargetForKeyboardEvents()
        {
            IPage page2 = await _page.Context.NewPageAsync().ConfigureAwait(false);
            await Task.WhenAll(
                _page.GoToAsync(Prefix + "/input/textarea.html"),
                page2.GoToAsync(Prefix + "/input/textarea.html")).ConfigureAwait(false);
            await Task.WhenAll(
                _page.FocusAsync("input"),
                page2.FocusAsync("input")).ConfigureAwait(false);
            await Task.WhenAll(
                _page.Keyboard.TypeAsync("first"),
                page2.Keyboard.TypeAsync("second")).ConfigureAwait(false);
            string first = await _page.EvaluateAsync<string>("result").ConfigureAwait(false);
            string second = await page2.EvaluateAsync<string>("result").ConfigureAwait(false);
            Assert.That(new[] { first, second }, Is.EqualTo(new[] { "first", "second" }));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should not affect mouse event target page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAffectMouseEventTargetPage()
        {
            IPage page2 = await _page.Context.NewPageAsync().ConfigureAwait(false);
            const string clickCounter =
                "() => { document.onclick = () => window.clickCount = (window.clickCount || 0) + 1; }";
            await Task.WhenAll(
                _page.EvaluateAsync(clickCounter),
                page2.EvaluateAsync(clickCounter),
                _page.FocusAsync("body"),
                page2.FocusAsync("body")).ConfigureAwait(false);
            await Task.WhenAll(
                _page.Mouse.ClickAsync(1, 1),
                page2.Mouse.ClickAsync(1, 1)).ConfigureAwait(false);
            int first = await _page.EvaluateAsync<int>("window.clickCount").ConfigureAwait(false);
            int second = await page2.EvaluateAsync<int>("window.clickCount").ConfigureAwait(false);
            Assert.That(new[] { first, second }, Is.EqualTo(new[] { 1, 1 }));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should change document.activeElement")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChangeDocumentActiveElement()
        {
            IPage page2 = await _page.Context.NewPageAsync().ConfigureAwait(false);
            await Task.WhenAll(
                _page.GoToAsync(Prefix + "/input/textarea.html"),
                page2.GoToAsync(Prefix + "/input/textarea.html")).ConfigureAwait(false);
            await Task.WhenAll(
                _page.FocusAsync("input"),
                page2.FocusAsync("textarea")).ConfigureAwait(false);
            string first = await _page.EvaluateAsync<string>("document.activeElement.tagName").ConfigureAwait(false);
            string second = await page2.EvaluateAsync<string>("document.activeElement.tagName").ConfigureAwait(false);
            Assert.That(new[] { first, second }, Is.EqualTo(new[] { "INPUT", "TEXTAREA" }));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should not affect screenshots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAffectScreenshots()
        {
            IPage page2 = await _page.Context.NewPageAsync().ConfigureAwait(false);
            await Task.WhenAll(
                _page.SetViewportSizeAsync(500, 500),
                _page.GoToAsync(Prefix + "/grid.html"),
                page2.SetViewportSizeAsync(50, 50),
                page2.GoToAsync(Prefix + "/grid.html")).ConfigureAwait(false);
            await Task.WhenAll(
                _page.FocusAsync("body"),
                page2.FocusAsync("body")).ConfigureAwait(false);
            byte[][] screenshots = await Task.WhenAll(
                _page.ScreenshotAsync(),
                page2.ScreenshotAsync()).ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-sanity.png", screenshots[0]);
            OfficialSnapshot.ToMatchSnapshot("grid-cell-0.png", screenshots[1]);
            await page2.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should change focused iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChangeFocusedIframe()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("official skip: browserName === 'firefox' && !headless");
            }

            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IFrame> frame1Task = AttachFrameAsync(_page, "frame1", Prefix + "/input/textarea.html");
            Task<IFrame> frame2Task = AttachFrameAsync(_page, "frame2", Prefix + "/input/textarea.html");
            await Task.WhenAll(frame1Task, frame2Task).ConfigureAwait(false);
            IFrame frame1 = await frame1Task.ConfigureAwait(false);
            IFrame frame2 = await frame2Task.ConfigureAwait(false);
            const string logger =
                "() => { self._events = []; const element = document.querySelector('input'); element.onfocus = element.onblur = e => self._events.push(e.type); }";
            await Task.WhenAll(frame1.EvaluateAsync(logger), frame2.EvaluateAsync(logger)).ConfigureAwait(false);
            bool focus1 = await frame1.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false);
            bool focus2 = await frame2.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false);
            Assert.That(new[] { focus1, focus2 }, Is.EqualTo(new[] { false, false }));

            await frame1.FocusAsync("input").ConfigureAwait(false);
            JsonElement events1 = await frame1.EvaluateAsync<JsonElement>("self._events").ConfigureAwait(false);
            JsonElement events2 = await frame2.EvaluateAsync<JsonElement>("self._events").ConfigureAwait(false);
            Assert.That(JsonSerializer.Serialize(events1), Is.EqualTo("[\"focus\"]"));
            Assert.That(JsonSerializer.Serialize(events2), Is.EqualTo("[]"));
            focus1 = await frame1.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false);
            focus2 = await frame2.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false);
            Assert.That(new[] { focus1, focus2 }, Is.EqualTo(new[] { true, false }));

            await frame2.FocusAsync("input").ConfigureAwait(false);
            events1 = await frame1.EvaluateAsync<JsonElement>("self._events").ConfigureAwait(false);
            events2 = await frame2.EvaluateAsync<JsonElement>("self._events").ConfigureAwait(false);
            Assert.That(JsonSerializer.Serialize(events1), Is.EqualTo("[\"focus\",\"blur\"]"));
            Assert.That(JsonSerializer.Serialize(events2), Is.EqualTo("[\"focus\"]"));
            focus1 = await frame1.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false);
            focus2 = await frame2.EvaluateAsync<bool>("document.hasFocus()").ConfigureAwait(false);
            Assert.That(new[] { focus1, focus2 }, Is.EqualTo(new[] { false, true }));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should focus with more than one page/context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFocusWithMoreThanOnePageContext()
        {
            IPage page1 = await (await _browser.NewContextAsync().ConfigureAwait(false)).NewPageAsync().ConfigureAwait(false);
            IPage page2 = await (await _browser.NewContextAsync().ConfigureAwait(false)).NewPageAsync().ConfigureAwait(false);
            await page1.SetContentAsync("<button id=\"foo\" onfocus=\"window.gotFocus=true\">foo</button>").ConfigureAwait(false);
            await page2.SetContentAsync("<button id=\"foo\" onfocus=\"window.gotFocus=true\">foo</button>").ConfigureAwait(false);
            await page1.FocusAsync("#foo").ConfigureAwait(false);
            await page2.FocusAsync("#foo").ConfigureAwait(false);
            Assert.That(await page1.EvaluateAsync<bool>("() => !!window.gotFocus").ConfigureAwait(false), Is.True);
            Assert.That(await page2.EvaluateAsync<bool>("() => !!window.gotFocus").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should not fire blur events when interacting with more than one page/context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFireBlurEventsWhenInteractingWithMoreThanOnePageContext()
        {
            IPage page1 = await (await _browser.NewContextAsync().ConfigureAwait(false)).NewPageAsync().ConfigureAwait(false);
            IPage page2 = await (await _browser.NewContextAsync().ConfigureAwait(false)).NewPageAsync().ConfigureAwait(false);
            await page1.SetContentAsync("<button id=\"foo\" onblur=\"window.gotBlur=true\">foo</button>").ConfigureAwait(false);
            await page2.SetContentAsync("<button id=\"foo\" onblur=\"window.gotBlur=true\">foo</button>").ConfigureAwait(false);
            await page1.ClickAsync("#foo").ConfigureAwait(false);
            await page2.ClickAsync("#foo").ConfigureAwait(false);
            Assert.That(await page1.EvaluateAsync<bool>("() => !!window.gotBlur").ConfigureAwait(false), Is.False);
            Assert.That(await page2.EvaluateAsync<bool>("() => !!window.gotBlur").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "should trigger hover state concurrently")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerHoverStateConcurrently()
        {
            IBrowser browser1 = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context1 = await browser1.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
            IPage page2 = await context1.NewPageAsync().ConfigureAwait(false);
            IBrowser browser2 = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page3 = await browser2.NewPageAsync().ConfigureAwait(false);
            const string html =
                "<style>button { display: none; } div:hover button { display: inline }</style>" +
                "<div><span>hover me</span><button onclick=\"window.clicked=1+(window.clicked || 0)\">click me</button></div>";
            IPage[] pages = { page1, page2, page3 };
            foreach (IPage page in pages)
            {
                await page.SetContentAsync(html).ConfigureAwait(false);
            }

            foreach (IPage page in pages)
            {
                await page.HoverAsync("span").ConfigureAwait(false);
            }

            foreach (IPage page in pages)
            {
                await page.ClickAsync("button").ConfigureAwait(false);
            }

            foreach (IPage page in pages)
            {
                Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(1));
            }

            foreach (IPage page in pages)
            {
                await page.ClickAsync("button").ConfigureAwait(false);
            }

            foreach (IPage page in pages)
            {
                Assert.That(await page.EvaluateAsync<int>("window.clicked").ConfigureAwait(false), Is.EqualTo(2));
            }

            await DisposeQuietlyAsync(browser1).ConfigureAwait(false);
            await DisposeQuietlyAsync(browser2).ConfigureAwait(false);
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

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + frameId);
            return null;
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }
        }
    }
}
