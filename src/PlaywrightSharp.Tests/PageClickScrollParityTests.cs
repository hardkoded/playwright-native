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
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-click-scroll.spec.ts</c> parity for click/hover scroll
    /// behavior. Skipped: none (Android-only <c>it.fixme</c> omitted).
    /// </summary>
    [TestFixture]
    public class PageClickScrollParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19788;
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
                    EmptyPage = origin + "/empty.html";
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("page-click-scroll.spec.ts", "should not hit scroll bar")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHitScrollBar()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      .categories { width: 180px; display: flex; overflow-x: scroll; }
      button { flex: none; height: 28px; }
    </style>
    <div class=""categories"">
      <button>One</button>
      <button>Two</button>
      <button>Three</button>
      <button>Story</button>
      <button>More</button>
      <button>Items</button>
      <button>Here</button>
    </div>
    ").ConfigureAwait(false);
            await page.ClickAsync("text=Story", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should scroll into view display:contents")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollIntoViewDisplayContents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div style=""background:red;height:2000px"">filler</div>
    <div>
      Example text, and button here:
      <button style=""display: contents"" onclick=""window._clicked=true;"">click me</button>
    </div>
  ").ConfigureAwait(false);
            await page.ClickAsync("text=click me", new() { Timeout = 5000 }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should scroll into view display:contents with a child")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollIntoViewDisplayContentsWithAChild()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div style=""background:red;height:2000px"">filler</div>
    Example text, and button here:
    <button style=""display: contents"" onclick=""window._clicked=true;""><div>click me</div></button>
  ").ConfigureAwait(false);
            await page.ClickAsync("text=click me", new() { Timeout = 5000 }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should scroll into view display:contents with position")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollIntoViewDisplayContentsWithPosition()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("DOM.getBoxModel does not work for display:contents");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div style=""background:red;height:2000px"">filler</div>
    <div>
      Example text, and button here:
      <button style=""display: contents"" onclick=""window._clicked=true;"">click me</button>
    </div>
  ").ConfigureAwait(false);
            await page.ClickAsync("text=click me", new() { Position = new Position { X = 5, Y = 5 }, Timeout = 5000 })
                .ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should not crash when force-clicking hidden input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhenForceClickingHiddenInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=hidden>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(
                () => page.Locator("input").ClickAsync(new() { Force = true, Timeout = 2000 }));
            Assert.That(error.Message, Does.Contain("Element is not visible"));
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should scroll into view span element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollIntoViewSpanElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=big style=""height: 10000px;""></div>
    <span id=small>foo</span>
  ").ConfigureAwait(false);
            await page.Locator("#small").ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<double>("() => window.scrollY").ConfigureAwait(false), Is.GreaterThan(9000));
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should scroll into view element in iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollIntoViewElementInIframe()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=big style=""height: 10000px;""></div>
    <iframe src='" + CrossProcessPrefix + @"/input/button.html'></iframe>
  ").ConfigureAwait(false);
            await page.FrameLocator("iframe").GetByRole("button").ClickAsync(new() { Timeout = 5000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should not scroll the page when scroll is \"none\"")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotScrollThePageWhenScrollIsNone()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div style=""height: 2000px;""></div>
    <button onclick=""window._clicked=true"">click me</button>
  ").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(
                () => page.Locator("button").ClickAsync(new LocatorClickOptions { Scroll = ActionScroll.None, Timeout = 2000 }));
            Assert.That(error.Message, Does.Contain("element is outside of the viewport"));
            Assert.That(await page.EvaluateAsync<object>("window._clicked").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvaluateAsync<double>("() => window.scrollY").ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should click in-viewport element when scroll is \"none\"")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickInViewportElementWhenScrollIsNone()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button onclick=""window._clicked=true"">click me</button>
    <div style=""height: 2000px;""></div>
  ").ConfigureAwait(false);
            await page.Locator("button").ClickAsync(new LocatorClickOptions { Scroll = ActionScroll.None, Timeout = 2000 }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<double>("() => window.scrollY").ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should not scroll nested container when scroll is \"none\"")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotScrollNestedContainerWhenScrollIsNone()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div style=""height: 100px; width: 100px; overflow-y: scroll;"">
      <div style=""height: 50px;"">A</div>
      <div style=""height: 50px;"">B</div>
      <button style=""height: 50px; width: 100px;"" onclick=""window._clicked=true"">C</button>
    </div>
  ").ConfigureAwait(false);
            ILocator button = page.Locator("button");
            Exception error = Assert.CatchAsync(
                () => button.ClickAsync(new LocatorClickOptions { Scroll = ActionScroll.None, Timeout = 2000 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(await page.EvaluateAsync<object>("window._clicked").ConfigureAwait(false), Is.Null);
            await button.ClickAsync(new() { Timeout = 2000 }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "should not scroll on hover when scroll is \"none\"")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotScrollOnHoverWhenScrollIsNone()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div style=""height: 2000px;""></div>
    <div onmouseover=""window._hovered=true"" style=""width: 50px; height: 50px;"">hover me</div>
  ").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(
                () => page.Locator("div >> text=hover me").HoverAsync(new LocatorHoverOptions { Scroll = ActionScroll.None, Timeout = 2000 }));
            Assert.That(error.Message, Does.Contain("element is outside of the viewport"));
            Assert.That(await page.EvaluateAsync<object>("window._hovered").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvaluateAsync<double>("() => window.scrollY").ConfigureAwait(false), Is.EqualTo(0));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<bool> FixtureReachableAsync(string origin)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(1),
                };
                using System.Net.Http.HttpResponseMessage response = await client.GetAsync(origin + "/empty.html")
                    .ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
