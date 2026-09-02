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
    /// Official <c>library/browsercontext-device.spec.ts</c> parity.
    /// Official skips the suite on Firefox (non-BiDi). Official WebKit skips
    /// two scroll-position titles. Do not edit leftover device tests.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextDeviceParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19837;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
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

        [PlaywrightTest("browsercontext-device.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            BrowserContextOptions iPhone = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(iPhone).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => window.innerWidth)()").ConfigureAwait(false), Is.EqualTo(375));
            Assert.That(await page.EvaluateAsync<string>("(() => navigator.userAgent)()").ConfigureAwait(false), Does.Contain("iPhone"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "should support clicking")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportClicking()
        {
            EnsureServer();
            BrowserContextOptions iPhone = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(iPhone).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", "button => { button.style.marginTop = '200px'; }").ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "should scroll to click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollToClick()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 400 }, DeviceScaleFactor = 1, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("#button-91").ConfigureAwait(false);
            await element.ClickAsync().ConfigureAwait(false);
            Assert.That(await element.TextContentAsync().ConfigureAwait(false), Is.EqualTo("clicked"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "should scroll twice when emulated")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollTwiceWhenEmulated()
        {
            BrowserContextOptions device = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(device).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<meta name=\"viewport\" content=\"width=device-width, user-scalable=no\" />" +
                " Lorem ipsum dolor sit amet consectetur adipiscing elit proin, integer curabitur imperdiet rhoncus cursus tincidunt bibendum, consequat sed magnis laoreet luctus mollis tellus. Nisl parturient mus accumsan feugiat sem laoreet magnis nisi, aptent per sollicitudin gravida orci ac blandit, viverra eros praesent auctor vivamus semper bibendum. Consequat sed habitasse luctus dictumst gravida platea semper phasellus, nascetur ridiculus purus est varius quisque et scelerisque, id vehicula eleifend montes sollicitudin dis velit. Pellentesque ridiculus per natoque et eleifend taciti nunc, laoreet auctor at condimentum imperdiet ante, conubia mi cubilia scelerisque sociosqu sem.</p> <p>Curabitur magna per felis primis mauris non dapibus luctus ultricies eros, quis et egestas condimentum lobortis eget semper montes litora purus, ridiculus elementum sollicitudin imperdiet dictum lacinia parturient cras eu. Risus cum varius rhoncus eros torquent pretium taciti id erat dis egestas, nibh tristique montes convallis metus lacus phasellus blandit ut auctor bibendum semper, facilisis mi integer eget ultrices lobortis odio viverra duis dui. Risus ullamcorper lacinia in venenatis sodales fusce tortor potenti volutpat quis, dictum vulputate suspendisse velit mollis torquent sociis aptent morbi, senectus nascetur justo maecenas conubia magnis viverra gravida fames. Phasellus sed nec gravida nibh class augue lectus, blandit quis turpis orci diam nam pellentesque, ultricies metus imperdiet hendrerit lacinia lacus.</p> <p>Inceptos facilisi montes cum hendrerit, pulvinar ut tellus eget velit, arcu nulla aenean. Phasellus augue urna nostra molestie interdum vehicula, posuere fames cum euismod massa curabitur donec, inceptos cubilia tellus facilisis fermentum. Lacus laoreet facilisis ultrices cursus quisque at ad porta vestibulum massa inceptos, curae class aliquet maecenas cum ullamcorper pulvinar erat mus vitae. Cum in aenean convallis dis quam tincidunt justo sed quisque, imperdiet faucibus hendrerit felis commodo scelerisque magnis vehicula etiam leo, eros varius platea lobortis maecenas condimentum nisi phasellus. Turpis vulputate mus himenaeos sociosqu facilisis dignissim leo quam, ultricies habitasse commodo molestie est tortor vitae et, porttitor risus erat cursus phasellus facilisi litora.</p> <p>Nostra habitasse egestas magnis velit pellentesque parturient cum lectus viverra, vestibulum sociosqu nunc vel urna consequat lacinia phasellus at sapien, aenean pretium dictum sed montes interdum imperdiet iaculis. Leo hac eros arcu senectus maecenas, tortor pulvinar venenatis lacinia volutpat, mattis platea ut facilisi. Aenean condimentum at et donec sociosqu fermentum luctus potenti semper vulputate, sapien justo non est auctor gravida ultricies fames per commodo, sed habitasse facilisi nulla quisque hendrerit aliquet viverra bibendum.</p> <p>Interdum nisl quam etiam montes porttitor laoreet nullam senectus velit, mauris proin tellus imperdiet litora venenatis fames massa quis, sollicitudin justo vivamus curae in sociis suscipit facilisi. Platea inceptos lacus elementum pellentesque quam euismod dictumst sociis tincidunt vulputate porttitor eros, turpis netus ut ad tempor sapien aliquet sodales molestie consequat nostra. Cum augue in quisque primis ut nunc sodales, sem orci tempus posuere cubilia suspendisse lacinia ligula, magna sed ridiculus at maecenas habitant.</p> <p>Natoque magna ac feugiat tellus bibendum diam, metus lobortis nisl ornare varius praesent, dictumst gravida lacus parturient semper. Pellentesque faucibus congue fusce posuere placerat dictum vitae, dui vestibulum eu sociis tempus aliquam ultricies malesuada, potenti laoreet lacus sem gravida nisi. Nostra platea sagittis hendrerit congue conubia senectus bibendum quis sapien pharetra, scelerisque nam imperdiet fermentum feugiat suspendisse viverra luctus at, semper ac consequat vitae mi gravida parturient mollis nascetur. Vel taciti justo consequat primis et blandit convallis sed, felis purus fusce a venenatis etiam aenean scelerisque, fringilla volutpat sagittis egestas rutrum id dis.</p> <p>Feugiat fermentum tortor ante ac iaculis sollicitudin ut interdum, cras orci ullamcorper potenti tristique vehicula. Molestie tortor ullamcorper rutrum turpis malesuada phasellus sem ultricies praesent mattis lobortis porta, senectus venenatis diam nostra laoreet volutpat per aptent justo elementum cum. Urna cursus vel felis cras eleifend arcu enim magnis, duis rutrum nibh nascetur cubilia interdum ultrices curae, id lacus aliquam dictumst diam fringilla lacinia.</p> <p>Luctus diam morbi eget tellus libero taciti faucibus inceptos, natoque facilisis lectus maecenas risus dapibus suscipit nibh, vel curae conubia orci imperdiet metus fusce. Condimentum massa donec luctus pharetra cum, in viverra placerat nisl litora facilisis, neque nascetur sociis dictumst. Suscipit accumsan eget rhoncus pharetra justo malesuada aliquet, suspendisse metus eleifend tincidunt varius ridiculus, convallis primis vitae curabitur quis mus.</p> <p>Gravida donec lacus molestie tortor aenean ultricies blandit per tempor, nostra penatibus orci vestibulum semper lectus vel a, montes potenti cum dapibus natoque eu volutpat nulla. Himenaeos purus nam malesuada habitasse nisl pharetra laoreet feugiat mi non, ultrices ultricies a cras ante eu venenatis ligula. Suscipit ut mus habitasse at aliquet sodales commodo justo, feugiat platea sagittis phasellus eleifend pellentesque interdum iaculis, integer cubilia montes metus hendrerit tincidunt purus.</p> <p>Vel posuere tellus dapibus eget duis cubilia, nec class vehicula libero gravida ligula, tempus urna taciti donec congue. Facilisis ridiculus congue cum dui per augue natoque, molestie hac etiam pellentesque dignissim urna class, feugiat aenean massa himenaeos penatibus ut eu, convallis purus et fusce tempus mattis. At mattis suscipit porta nostra nec facilisis sodales turpis, integer et lectus conubia justo nam congue taciti odio, fermentum semper cubilia fusce nunc purus velit." +
                "<button>hi</button>").ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window.scroll(0, 100))()").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => window.scrollY)()").ConfigureAwait(false), Is.EqualTo(100));
            await page.EvaluateAsync<object>("(() => window.scroll(0, 200))()").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => window.scrollY)()").ConfigureAwait(false), Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "should reset scroll top after a navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResetScrollTopAfterANavigation()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("webkit");
            }

            EnsureServer();
            BrowserContextOptions device = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(device).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window.scroll(0, 100))()").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable2.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => window.scrollY)()").ConfigureAwait(false), Is.EqualTo(0));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "should scroll to a precise position with mobile scale")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollToAPrecisePositionWithMobileScale()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("webkit");
            }

            EnsureServer();
            BrowserContextOptions device = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(device).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/scrollable.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => document.body.scrollHeight)()").ConfigureAwait(false), Is.GreaterThan(1000));
            await page.EvaluateAsync<object>("(() => window.scroll(0, 100))()").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => window.scrollY)()").ConfigureAwait(false), Is.EqualTo(100));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "should emulate viewport and screen size")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateViewportAndScreenSize()
        {
            BrowserContextOptions device = Playwright.Devices["iPhone 12"];
            IBrowserContext context = await _browser.NewContextAsync(device).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<meta name=\"viewport\" content=\"width=device-width, user-scalable=no\" />").ConfigureAwait(false);
            JsonElement screen = (await page.EvaluateAsync<JsonElement>("(() => ({ width: window.screen.width, height: window.screen.height }))()").ConfigureAwait(false));
            Assert.That(screen.GetProperty("width").GetInt32(), Is.EqualTo(390));
            Assert.That(screen.GetProperty("height").GetInt32(), Is.EqualTo(844));
            JsonElement inner = (await page.EvaluateAsync<JsonElement>("(() => ({ width: window.innerWidth, height: window.innerHeight }))()").ConfigureAwait(false));
            Assert.That(inner.GetProperty("width").GetInt32(), Is.EqualTo(390));
            Assert.That(inner.GetProperty("height").GetInt32(), Is.EqualTo(664));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-device.spec.ts", "should emulate viewport without screen size")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateViewportWithoutScreenSize()
        {
            BrowserContextOptions device = Playwright.Devices["iPhone 6"];
            IBrowserContext context = await _browser.NewContextAsync(device).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<meta name=\"viewport\" content=\"width=device-width, user-scalable=no\" />").ConfigureAwait(false);
            JsonElement screen = (await page.EvaluateAsync<JsonElement>("(() => ({ width: window.screen.width, height: window.screen.height }))()").ConfigureAwait(false));
            Assert.That(screen.GetProperty("width").GetInt32(), Is.EqualTo(375));
            Assert.That(screen.GetProperty("height").GetInt32(), Is.EqualTo(667));
            JsonElement inner = (await page.EvaluateAsync<JsonElement>("(() => ({ width: window.innerWidth, height: window.innerHeight }))()").ConfigureAwait(false));
            Assert.That(inner.GetProperty("width").GetInt32(), Is.EqualTo(375));
            Assert.That(inner.GetProperty("height").GetInt32(), Is.EqualTo(667));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new System.Collections.Generic.List<IBrowserContext>(_browser.Contexts))
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
