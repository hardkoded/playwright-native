/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
    /// Official <c>page-dispatchevent.spec.ts</c> parity for <see cref="IPage.DispatchEventAsync"/>,
    /// <see cref="IFrame.DispatchEventAsync"/>, and <see cref="IElementHandle.DispatchEventAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageDispatchEventParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossPrefix = TestConstants.CrossProcessHttpPrefix;

        private static IFrame ChildFrame(IPage page)
        {
            foreach (IFrame frame in page.Frames)
            {
                if (frame != page.MainFrame)
                {
                    return frame;
                }
            }

            Assert.Fail("Expected a child frame.");
            return null;
        }

        private static void IgnoreWebKitDeviceEvent(Exception ex)
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore(ex.Message);
            }

            throw ex;
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                CrossPrefix = TestConstants.CrossProcessHttpPrefix;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19113;
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
                    CrossPrefix = "http://127.0.0.1:" + portText;
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

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click event")]
        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click event @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.DispatchEventAsync("button", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click event properties")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickEventProperties()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.DispatchEventAsync("button", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("bubbles").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("cancelable").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("composed").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click svg")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickSvg()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <svg height=""100"" width=""100"">
      <circle onclick=""javascript:window.__CLICKED=42"" cx=""50"" cy=""50"" r=""40"" stroke=""black"" stroke-width=""3"" fill=""red"" />
    </svg>
  ").ConfigureAwait(false);
            await page.DispatchEventAsync("circle", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window['__CLICKED']").ConfigureAwait(false), Is.EqualTo(42));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click on a span with an inline element inside")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickOnASpanWithAnInlineElementInside()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
    span::before {
      content: 'q';
    }
    </style>
    <span onclick='javascript:window.CLICKED=42'></span>
  ").ConfigureAwait(false);
            await page.DispatchEventAsync("span", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window['CLICKED']").ConfigureAwait(false), Is.EqualTo(42));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click after navigation ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickAfterNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.DispatchEventAsync("button", "click").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.DispatchEventAsync("button", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click after a cross origin navigation ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickAfterACrossOriginNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.DispatchEventAsync("button", "click").ConfigureAwait(false);
            await page.GoToAsync(CrossPrefix + "/input/button.html").ConfigureAwait(false);
            await page.DispatchEventAsync("button", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should not fail when element is blocked on hover")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotFailWhenElementIsBlockedOnHover()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<style>
    container { display: block; position: relative; width: 200px; height: 50px; }
    div, button { position: absolute; left: 0; top: 0; bottom: 0; right: 0; }
    div { pointer-events: none; }
    container:hover div { pointer-events: auto; background: red; }
  </style>
  <container>
    <button onclick=""window.clicked=true"">Click me</button>
    <div></div>
  </container>").ConfigureAwait(false);
            await page.DispatchEventAsync("button", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window['clicked']").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click when node is added in shadow dom")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickWhenNodeIsAddedInShadowDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Task watchdog = page.DispatchEventAsync("span", "click");
            await page.EvaluateAsync<object>(@"(() => {
    const div = document.createElement('div');
    div.attachShadow({ mode: 'open' });
    document.body.appendChild(div);
  })()").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(100).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
    const span = document.createElement('span');
    span.textContent = 'Hello from shadow';
    span.addEventListener('click', () => window['clicked'] = true);
    document.querySelector('div').shadowRoot.appendChild(span);
  })()").ConfigureAwait(false);
            await watchdog.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window['clicked']").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should be atomic")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAtomic()
        {
            const string createDummySelector = @"{
  query(root, selector) {
    const result = root.querySelector(selector);
    if (result)
      void Promise.resolve().then(() => result.onclick = '');
    return result;
  },
  queryAll(root, selector) {
    const result = Array.from(root.querySelectorAll(selector));
    for (const e of result)
      void Promise.resolve().then(() => e.onclick = null);
    return result;
  }
}";
            try
            {
                await Playwright.Selectors.RegisterAsync("dispatchEvent713", createDummySelector).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex) when (ex.Message.Contains("already registered", StringComparison.Ordinal))
            {
            }
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<div onclick=\"window._clicked=true\">Hello</div>").ConfigureAwait(false);
            await page.DispatchEventAsync("dispatchEvent713=div", "click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window['_clicked']").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch drag drop events")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchDragDropEvents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/drag-n-drop.html").ConfigureAwait(false);
            IJSHandle dataTransfer = await page.EvaluateHandleAsync("() => new DataTransfer()").ConfigureAwait(false);
            await page.DispatchEventAsync("#source", "dragstart", new { dataTransfer }).ConfigureAwait(false);
            await page.DispatchEventAsync("#target", "drop", new { dataTransfer }).ConfigureAwait(false);
            IElementHandle source = await page.QuerySelectorAsync("#source").ConfigureAwait(false);
            IElementHandle target = await page.QuerySelectorAsync("#target").ConfigureAwait(false);
            bool moved = await source.EvaluateAsync<bool>("(s, t) => s.parentElement === t", target).ConfigureAwait(false);
            Assert.That(moved, Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch drag drop events via ElementHandles")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchDragDropEventsViaElementHandles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/drag-n-drop.html").ConfigureAwait(false);
            IJSHandle dataTransfer = await page.EvaluateHandleAsync("() => new DataTransfer()").ConfigureAwait(false);
            IElementHandle source = await page.QuerySelectorAsync("#source").ConfigureAwait(false);
            await source.DispatchEventAsync("dragstart", new { dataTransfer }).ConfigureAwait(false);
            IElementHandle target = await page.QuerySelectorAsync("#target").ConfigureAwait(false);
            await target.DispatchEventAsync("drop", new { dataTransfer }).ConfigureAwait(false);
            bool moved = await source.EvaluateAsync<bool>("(s, t) => s.parentElement === t", target).ConfigureAwait(false);
            Assert.That(moved, Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch click event via ElementHandles")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchClickEventViaElementHandles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.DispatchEventAsync("click").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window['result']").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch wheel event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchWheelEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            IJSHandle eventsHandle = await page.Locator("body").EvaluateHandleAsync(@"e => {
    const events = [];
    e.addEventListener('wheel', event => {
      events.push(event);
    });
    return events;
  }").ConfigureAwait(false);
            await page.Locator("body").DispatchEventAsync("wheel", new { deltaX = 100, deltaY = 200 }).ConfigureAwait(false);
            Assert.That(await eventsHandle.EvaluateAsync<int>("e => e.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await eventsHandle.EvaluateAsync<bool>("e => e[0] instanceof WheelEvent").ConfigureAwait(false), Is.True);
            int deltaX = await eventsHandle.EvaluateAsync<int>("e => e[0].deltaX").ConfigureAwait(false);
            int deltaY = await eventsHandle.EvaluateAsync<int>("e => e[0].deltaY").ConfigureAwait(false);
            Assert.That(deltaX, Is.EqualTo(100));
            Assert.That(deltaY, Is.EqualTo(200));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch device orientation event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchDeviceOrientationEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/device-orientation.html").ConfigureAwait(false);
            try
            {
                await page.Locator("html").DispatchEventAsync("deviceorientation", new { alpha = 10, beta = 20, gamma = 30 }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                IgnoreWebKitDeviceEvent(ex);
            }

            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Oriented"));
            Assert.That(await page.EvaluateAsync<double>("alpha").ConfigureAwait(false), Is.EqualTo(10));
            Assert.That(await page.EvaluateAsync<double>("beta").ConfigureAwait(false), Is.EqualTo(20));
            Assert.That(await page.EvaluateAsync<double>("gamma").ConfigureAwait(false), Is.EqualTo(30));
            Assert.That(await page.EvaluateAsync<bool>("absolute").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch absolute device orientation event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchAbsoluteDeviceOrientationEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/device-orientation.html").ConfigureAwait(false);
            try
            {
                await page.Locator("html").DispatchEventAsync(
                    "deviceorientationabsolute",
                    new { alpha = 10, beta = 20, gamma = 30, absolute = true }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                IgnoreWebKitDeviceEvent(ex);
            }

            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Oriented"));
            Assert.That(await page.EvaluateAsync<double>("alpha").ConfigureAwait(false), Is.EqualTo(10));
            Assert.That(await page.EvaluateAsync<double>("beta").ConfigureAwait(false), Is.EqualTo(20));
            Assert.That(await page.EvaluateAsync<double>("gamma").ConfigureAwait(false), Is.EqualTo(30));
            Assert.That(await page.EvaluateAsync<bool>("absolute").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should dispatch device motion event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchDeviceMotionEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/device-motion.html").ConfigureAwait(false);
            try
            {
                await page.Locator("html").DispatchEventAsync("devicemotion", new
                {
                    acceleration = new { x = 10, y = 20, z = 30 },
                    accelerationIncludingGravity = new { x = 15, y = 25, z = 35 },
                    rotationRate = new { alpha = 5, beta = 10, gamma = 15 },
                    interval = 16,
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                IgnoreWebKitDeviceEvent(ex);
            }

            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Moved"));
            Assert.That(await page.EvaluateAsync<double>("acceleration.x").ConfigureAwait(false), Is.EqualTo(10));
            Assert.That(await page.EvaluateAsync<double>("acceleration.y").ConfigureAwait(false), Is.EqualTo(20));
            Assert.That(await page.EvaluateAsync<double>("acceleration.z").ConfigureAwait(false), Is.EqualTo(30));
            Assert.That(await page.EvaluateAsync<double>("accelerationIncludingGravity.x").ConfigureAwait(false), Is.EqualTo(15));
            Assert.That(await page.EvaluateAsync<double>("accelerationIncludingGravity.y").ConfigureAwait(false), Is.EqualTo(25));
            Assert.That(await page.EvaluateAsync<double>("accelerationIncludingGravity.z").ConfigureAwait(false), Is.EqualTo(35));
            Assert.That(await page.EvaluateAsync<double>("rotationRate.alpha").ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(await page.EvaluateAsync<double>("rotationRate.beta").ConfigureAwait(false), Is.EqualTo(10));
            Assert.That(await page.EvaluateAsync<double>("rotationRate.gamma").ConfigureAwait(false), Is.EqualTo(15));
            Assert.That(await page.EvaluateAsync<double>("interval").ConfigureAwait(false), Is.EqualTo(16));
        }

        [PlaywrightTest("page-dispatchevent.spec.ts", "should throw if argument is from different frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowIfArgumentIsFromDifferentFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);

            IJSHandle sameFrameTransfer = await ChildFrame(page).EvaluateHandleAsync("() => new DataTransfer()").ConfigureAwait(false);
            await page.FrameLocator("iframe").Locator("div").DispatchEventAsync("drop", new { dataTransfer = sameFrameTransfer }).ConfigureAwait(false);

            IJSHandle otherFrameTransfer = await page.EvaluateHandleAsync("() => new DataTransfer()").ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.FrameLocator("iframe").Locator("div").DispatchEventAsync("drop", new { dataTransfer = otherFrameTransfer }));
            Assert.That(ex, Is.Not.Null);
        }
    }
}
