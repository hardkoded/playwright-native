/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
    /// Official <c>library/browsercontext-viewport.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextEmulationTests</c> or
    /// <c>LaunchPersistentViewportTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextViewportParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19856;
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

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should get the proper default viewport size")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGetTheProperDefaultViewportSize()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await VerifyViewportAsync(page, 1280, 720).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should set the proper viewport size")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetTheProperViewportSize()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await VerifyViewportAsync(page, 1280, 720).ConfigureAwait(false);
            await page.SetViewportSizeAsync(345, 456).ConfigureAwait(false);
            await VerifyViewportAsync(page, 345, 456).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should return correct outerWidth and outerHeight")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnCorrectOuterWidthAndOuterHeight()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(410, 420).ConfigureAwait(false);
            JsonElement size = await page.EvaluateAsync<JsonElement>(
                @"() => ({
                    innerWidth: window.innerWidth,
                    innerHeight: window.innerHeight,
                    outerWidth: window.outerWidth,
                    outerHeight: window.outerHeight,
                })").ConfigureAwait(false);
            Assert.That(size.GetProperty("innerWidth").GetInt32(), Is.EqualTo(410));
            Assert.That(size.GetProperty("innerHeight").GetInt32(), Is.EqualTo(420));
            Assert.That(size.GetProperty("outerWidth").GetInt32(), Is.GreaterThanOrEqualTo(size.GetProperty("innerWidth").GetInt32()));
            Assert.That(size.GetProperty("outerHeight").GetInt32(), Is.GreaterThanOrEqualTo(size.GetProperty("innerHeight").GetInt32()));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "landscape viewport should have width larger than height")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void LandscapeViewportShouldHaveWidthLargerThanHeight()
        {
            foreach (KeyValuePair<string, BrowserContextOptions> device in Playwright.Devices)
            {
                if (!device.Key.Contains("landscape", StringComparison.Ordinal)
                    && !device.Key.Contains("Landscape", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.That(device.Value.Viewport, Is.Not.Null);
                Assert.That(device.Value.Viewport.Width, Is.GreaterThan(device.Value.Viewport.Height), device.Key);
            }
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should emulate device width")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateDeviceWidth()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page.ViewportSize, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(1280));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(720));
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.width").ConfigureAwait(false), Is.EqualTo(300));
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-width: 200px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-width: 400px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-width: 200px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-width: 400px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-width: 600px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-width: 300px)').matches").ConfigureAwait(false), Is.True);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.width").ConfigureAwait(false), Is.EqualTo(500));
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-width: 400px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-width: 600px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-width: 400px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-width: 600px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-width: 200px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-width: 500px)').matches").ConfigureAwait(false), Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should emulate device height")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateDeviceHeight()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page.ViewportSize, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(1280));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(720));
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.height").ConfigureAwait(false), Is.EqualTo(300));
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-height: 200px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-height: 400px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-height: 200px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-height: 400px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-height: 600px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-height: 300px)').matches").ConfigureAwait(false), Is.True);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.height").ConfigureAwait(false), Is.EqualTo(500));
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-height: 400px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(min-device-height: 600px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-height: 400px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(max-device-height: 600px)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-height: 200px)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await page.EvaluateAsync<bool>("() => matchMedia('(device-height: 500px)').matches").ConfigureAwait(false), Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should emulate availWidth and availHeight")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateAvailWidthAndAvailHeight()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 600).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.availWidth").ConfigureAwait(false), Is.EqualTo(500));
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.availHeight").ConfigureAwait(false), Is.EqualTo(600));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should not have touch by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHaveTouchByDefault()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => 'ontouchstart' in window").ConfigureAwait(false), Is.False);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should throw on tap if hasTouch is not enabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnTapIfHasTouchIsNotEnabled()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>a</div>").ConfigureAwait(false);
            PlaywrightNativeException pageError = Assert.CatchAsync<PlaywrightNativeException>(() => page.TapAsync("div"));
            Assert.That(pageError, Is.Not.Null);
            Assert.That(pageError.Message, Does.Contain("The page does not support tap"));
            PlaywrightNativeException locatorError = Assert.CatchAsync<PlaywrightNativeException>(() => page.Locator("div").TapAsync());
            Assert.That(locatorError, Is.Not.Null);
            Assert.That(locatorError.Message, Does.Contain("The page does not support tap"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should support touch with null viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTouchWithNullViewport()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport, HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("() => 'ontouchstart' in window").ConfigureAwait(false), Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should set both screen and viewport options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetBothScreenAndViewportOptions()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { ScreenSize = new ScreenSize { Width = 1280, Height = 720 }, ViewportSize = new ViewportSize { Width = 1000, Height = 600 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            JsonElement screen = await page.EvaluateAsync<JsonElement>(
                "() => ({ w: screen.width, h: screen.height })").ConfigureAwait(false);
            Assert.That(screen.GetProperty("w").GetInt32(), Is.EqualTo(1280));
            Assert.That(screen.GetProperty("h").GetInt32(), Is.EqualTo(720));
            JsonElement inner = await page.EvaluateAsync<JsonElement>(
                "() => ({ w: window.innerWidth, h: window.innerHeight })").ConfigureAwait(false);
            Assert.That(inner.GetProperty("w").GetInt32(), Is.EqualTo(1000));
            Assert.That(inner.GetProperty("h").GetInt32(), Is.EqualTo(600));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should report null viewportSize when given null viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportNullViewportSizeWhenGivenNullViewport()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page.ViewportSize, Is.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should drag with high dpi")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDragWithHighDpi()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { DeviceScaleFactor = 2 }).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/drag-n-drop.html").ConfigureAwait(false);
            await page.HoverAsync("#source").ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.HoverAsync("#target").ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);
            Assert.That(
                await page.EvalOnSelectorAsync<bool>(
                    "#target",
                    "target => target.contains(document.querySelector('#source'))").ConfigureAwait(false),
                Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "WebKit Windows headed should have a minimal viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void WebKitWindowsHeadedShouldHaveAMinimalViewport()
        {
            Assert.Ignore("Not relevant for this browser");
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should be able to get correct orientation angle on non-mobile devices")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToGetCorrectOrientationAngleOnNonMobileDevices()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Desktop webkit dont support orientation API");
            }

            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 300, Height = 400 }, IsMobile = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/index.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.orientation.angle").ConfigureAwait(false), Is.EqualTo(0));
            await page.SetViewportSizeAsync(400, 300).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.screen.orientation.angle").ConfigureAwait(false), Is.EqualTo(0));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should set window.screen.orientation.type for mobile devices")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetWindowScreenOrientationTypeForMobileDevices()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(Playwright.Devices["iPhone 14"]).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/index.html").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("() => window.screen.orientation.type").ConfigureAwait(false),
                Is.EqualTo("portrait-primary"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task VerifyViewportAsync(IPage page, int width, int height)
        {
            Assert.That(page.ViewportSize, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(width));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(height));
            Assert.That(await page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false), Is.EqualTo(width));
            Assert.That(await page.EvaluateAsync<int>("() => window.innerHeight").ConfigureAwait(false), Is.EqualTo(height));
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
