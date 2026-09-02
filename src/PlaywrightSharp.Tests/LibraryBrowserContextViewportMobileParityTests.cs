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
    /// Official <c>library/browsercontext-viewport-mobile.spec.ts</c> parity.
    /// Official skips the suite on Firefox (non-BiDi). Do not edit leftover
    /// <c>ContextEmulationTests</c> or leftover device tests.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextViewportMobileParityTests : PageTestEx
    {
        private const string DispatchTouch = @"() => {
            let fulfill;
            const promise = new Promise(x => fulfill = x);
            window.ontouchstart = function(e) {
                fulfill('Received touch');
            };
            window.dispatchEvent(new Event('touchstart'));
            fulfill('Did not receive touch');
            return promise;
        }";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19857;
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

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should support mobile emulation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMobileEmulation()
        {
            EnsureServer();
            BrowserContextOptions iPhone = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(iPhone).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false), Is.EqualTo(375));
            await page.SetViewportSizeAsync(400, 300).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false), Is.EqualTo(400));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should support touch emulation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTouchEmulation()
        {
            EnsureServer();
            BrowserContextOptions iPhone = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(iPhone).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => 'ontouchstart' in window").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<string>(DispatchTouch).ConfigureAwait(false), Is.EqualTo("Received touch"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should be detectable")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeDetectable()
        {
            BrowserContextOptions iPhone = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(iPhone).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<bool>("() => 'ontouchstart' in window || !!window.TouchEvent").ConfigureAwait(false),
                Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should detect touch when applying viewport with touches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectTouchWhenApplyingViewportWithTouches()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 800, Height = 600 }, HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<bool>("() => 'ontouchstart' in window || !!window.TouchEvent").ConfigureAwait(false),
                Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should support landscape emulation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportLandscapeEmulation()
        {
            EnsureServer();
            IBrowserContext context1 = await _browser.NewContextAsync(Playwright.Devices["iPhone 6"]).ConfigureAwait(false);
            IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
            await page1.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page1.EvaluateAsync<bool>("() => matchMedia('(orientation: landscape)').matches").ConfigureAwait(false), Is.False);
            IBrowserContext context2 = await _browser.NewContextAsync(Playwright.Devices["iPhone 6 landscape"]).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            Assert.That(await page2.EvaluateAsync<bool>("() => matchMedia('(orientation: landscape)').matches").ConfigureAwait(false), Is.True);
            await context1.CloseAsync().ConfigureAwait(false);
            await context2.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should support window.orientation emulation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportWindowOrientationEmulation()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 300, Height = 400 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.orientation").ConfigureAwait(false), Is.EqualTo(0));
            await page.SetViewportSizeAsync(400, 300).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.orientation").ConfigureAwait(false), Is.EqualTo(90));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should preserve window.orientation override after navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreserveWindowOrientationOverrideAfterNavigation()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 300 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.orientation").ConfigureAwait(false), Is.EqualTo(90));
            await page.GoToAsync(CrossProcessPrefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.orientation").ConfigureAwait(false), Is.EqualTo(90));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should preserve screen.orientation.type override after navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreserveScreenOrientationTypeOverrideAfterNavigation()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 300, Height = 400 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("() => window.screen.orientation.type").ConfigureAwait(false),
                Is.EqualTo("portrait-primary"));
            await page.GoToAsync(CrossProcessPrefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("() => window.screen.orientation.type").ConfigureAwait(false),
                Is.EqualTo("portrait-primary"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should fire orientationchange event")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireOrientationchangeEvent()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 300, Height = 400 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
                let counter = 0;
                window.addEventListener('orientationchange', () => console.log(++counter));
            }").ConfigureAwait(false);

            Task<IConsoleMessage> event1 = page.WaitForEventAsync(PageEvent.Console);
            await page.SetViewportSizeAsync(400, 300).ConfigureAwait(false);
            Assert.That((await event1.ConfigureAwait(false)).Text, Is.EqualTo("1"));

            Task<IConsoleMessage> event2 = page.WaitForEventAsync(PageEvent.Console);
            await page.SetViewportSizeAsync(300, 400).ConfigureAwait(false);
            Assert.That((await event2.ConfigureAwait(false)).Text, Is.EqualTo("2"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "default mobile viewports to 980 width")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DefaultMobileViewportsTo980Width()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false), Is.EqualTo(980));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "respect meta viewport tag")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RespectMetaViewportTag()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false), Is.EqualTo(320));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should emulate the hover media feature")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateTheHoverMediaFeature()
        {
            IBrowserContext mobileContext = await _browser.NewContextAsync(Playwright.Devices["iPhone 6"]).ConfigureAwait(false);
            IPage mobilepage = await mobileContext.NewPageAsync().ConfigureAwait(false);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(hover: hover)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(hover: none)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(any-hover: hover)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(any-hover: none)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(pointer: coarse)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(pointer: fine)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(any-pointer: coarse)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await mobilepage.EvaluateAsync<bool>("() => matchMedia('(any-pointer: fine)').matches").ConfigureAwait(false), Is.False);
            await mobileContext.CloseAsync().ConfigureAwait(false);

            IPage desktopPage = await _browser.NewPageAsync().ConfigureAwait(false);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(hover: none)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(hover: hover)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(any-hover: none)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(any-hover: hover)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(pointer: coarse)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(pointer: fine)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(any-pointer: coarse)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await desktopPage.EvaluateAsync<bool>("() => matchMedia('(any-pointer: fine)').matches").ConfigureAwait(false), Is.True);
            await desktopPage.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "mouse should work with mobile viewports and cross process navigations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MouseShouldWorkWithMobileViewportsAndCrossProcessNavigations()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 360, Height = 640 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.GoToAsync(CrossProcessPrefix + "/mobile.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
                document.addEventListener('click', event => {
                    window['result'] = { x: event.clientX, y: event.clientY };
                });
            }").ConfigureAwait(false);
            await page.Mouse.ClickAsync(30, 40).ConfigureAwait(false);
            JsonElement result = await page.EvaluateAsync<JsonElement>("result").ConfigureAwait(false);
            Assert.That(result.GetProperty("x").GetInt32(), Is.EqualTo(30));
            Assert.That(result.GetProperty("y").GetInt32(), Is.EqualTo(40));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should scroll when emulating a mobile viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollWhenEmulatingAMobileViewport()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 1000, Height = 600 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.Mouse.MoveAsync(50, 60).ConfigureAwait(false);
            if (TestConstants.IsWebKit)
            {
                PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                    () => page.Mouse.WheelAsync(0, 100));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Mouse wheel is not supported in mobile WebKit"));
            }
            else
            {
                await page.Mouse.WheelAsync(0, 100).ConfigureAwait(false);
                await page.WaitForFunctionAsync("() => window.scrollY === 100").ConfigureAwait(false);
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "should scroll mobile page with background-attachment: fixed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollMobilePageWithBackgroundAttachmentFixed()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(Playwright.Devices["iPhone 12"]).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/background-fixed.html").ConfigureAwait(false);
            await page.GetByRole(AriaRole.Button).ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.scrollY").ConfigureAwait(false), Is.GreaterThan(1000));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport-mobile.spec.ts", "view scale should reset after navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ViewScaleShouldResetAfterNavigation()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 390, Height = 664 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<meta name='viewport' content='device-width, initial-scale=1'><button>Mobile Viewport</button>").ConfigureAwait(false);
            await page.RouteAsync("**/button.html", route => route.FulfillAsync(new() { Body = @"<body>
          <button>Click me</button>
          <script>
            window.clicks = [];
            document.addEventListener('click', e => {
              const dot = document.createElement('div');
              dot.style.position = 'absolute';
              dot.style.width = '10px';
              dot.style.height = '10px';
              dot.style.borderRadius = '5px';
              dot.style.backgroundColor = 'red';
              dot.style.left = e.pageX + 'px';
              dot.style.top = e.pageY + 'px';
              dot.textContent = 'x: ' + e.pageX + ' y: ' + e.pageY;
              document.body.appendChild(dot);
              window.clicks.push({ x: e.pageX, y: e.pageY });
            });
          </script>
        </body>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.GoToAsync("http://localhost/button.html").ConfigureAwait(false);
            await page.GetByText("Click me").ClickAsync(new() { Force = true }).ConfigureAwait(false);
            var box = await page.Locator("button").BoundingBoxAsync().ConfigureAwait(false);
            JsonElement clicks = await page.EvaluateAsync<JsonElement>("() => window.clicks").ConfigureAwait(false);
            Assert.That(clicks.GetArrayLength(), Is.EqualTo(1));
            JsonElement click = clicks[0];
            float x = click.GetProperty("x").GetSingle();
            float y = click.GetProperty("y").GetSingle();
            bool isClickInsideButton = box.X <= x && x <= box.X + box.Width && box.Y <= y && y <= box.Y + box.Height;
            Assert.That(isClickInsideButton, Is.True);
            await context.CloseAsync().ConfigureAwait(false);
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
