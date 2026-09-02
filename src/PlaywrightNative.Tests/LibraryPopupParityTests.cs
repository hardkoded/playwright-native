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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/popup.spec.ts</c> parity.
    /// Do not edit leftover <c>PageEventPopupTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryPopupParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19965;
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
            Server?.Reset();
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
            Server?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("popup.spec.ts", "should inherit user agent from browser context @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritUserAgentFromBrowserContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { UserAgent = "hey" }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<a target=_blank rel=noopener href=\"/popup/popup.html\">link</a>").ConfigureAwait(false);
                Task<string> requestTask = Server.WaitForRequest("/popup/popup.html", request => request.Headers["user-agent"].ToString());
                Task<IPage> popupTask = context.WaitForEventAsync(BrowserContextEvent.Page);
                Task clickTask = page.ClickAsync("a");
                IPage popup = await popupTask.ConfigureAwait(false);
                await clickTask.ConfigureAwait(false);
                await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
                string userAgent = await popup.EvaluateAsync<string>("() => window.initialUserAgent").ConfigureAwait(false);
                string requestUa = await requestTask.ConfigureAwait(false);
                Assert.That(userAgent, Is.EqualTo("hey"));
                Assert.That(requestUa, Is.EqualTo("hey"));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should respect routes from browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectRoutesFromBrowserContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<a target=_blank rel=noopener href=\"empty.html\">link</a>").ConfigureAwait(false);
                bool intercepted = false;
                await context.RouteAsync("**/empty.html", route =>
                {
                    intercepted = true;
                    return route.ContinueAsync();
                }).ConfigureAwait(false);
                await Task.WhenAll(
                    context.WaitForEventAsync(BrowserContextEvent.Page),
                    page.ClickAsync("a")).ConfigureAwait(false);
                Assert.That(intercepted, Is.True);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should inherit extra headers from browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritExtraHeadersFromBrowserContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ExtraHTTPHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["foo"] = "bar" } }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<string> requestTask = Server.WaitForRequest("/dummy.html", request => request.Headers["foo"].ToString());
                await page.EvaluateAsync("url => window._popup = window.open(url)", Prefix + "/dummy.html").ConfigureAwait(false);
                Assert.That(await requestTask.ConfigureAwait(false), Is.EqualTo("bar"));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should inherit offline from browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritOfflineFromBrowserContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await context.SetOfflineAsync(true).ConfigureAwait(false);
                bool online = await page.EvaluateAsync<bool>(
                    "url => { const win = window.open(url); return win.navigator.onLine; }",
                    Prefix + "/dummy.html").ConfigureAwait(false);
                Assert.That(online, Is.False);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should inherit http credentials from browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritHttpCredentialsFromBrowserContext()
        {
            EnsureServer();
            Server.SetAuth("/title.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" } }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                Task evaluateTask = page.EvaluateAsync(
                    "url => window._popup = window.open(url)",
                    Prefix + "/title.html");
                IPage popup = await popupTask.ConfigureAwait(false);
                await evaluateTask.ConfigureAwait(false);
                await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
                Assert.That(await popup.TitleAsync().ConfigureAwait(false), Is.EqualTo("Woof-Woof"));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should inherit touch support from browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritTouchSupportFromBrowserContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 500 }, HasTouch = true }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                bool hasTouch = await page.EvaluateAsync<bool>(
                    "() => { const win = window.open(''); return 'ontouchstart' in win; }").ConfigureAwait(false);
                Assert.That(hasTouch, Is.True);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should inherit viewport size from browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritViewportSizeFromBrowserContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 400, Height = 500 } }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                ViewportSizeDto size = await page.EvaluateAsync<ViewportSizeDto>(
                    "() => { const win = window.open('about:blank'); return { width: win.innerWidth, height: win.innerHeight }; }").ConfigureAwait(false);
                Assert.That(size.Width, Is.EqualTo(400));
                Assert.That(size.Height, Is.EqualTo(500));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should use viewport size from window features")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseViewportSizeFromWindowFeatures()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 700, Height = 700 } }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                Task<ViewportSizeDto> sizeTask = page.EvaluateAsync<ViewportSizeDto>(
                    @"async () => {
                        const win = window.open(window.location.href, 'Title', 'toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=yes,resizable=yes,width=600,height=300,top=0,left=0');
                        await new Promise(resolve => {
                            const interval = setInterval(() => {
                                if (win.innerWidth === 600 && win.innerHeight === 300) {
                                    clearInterval(interval);
                                    resolve();
                                }
                            }, 10);
                        });
                        return { width: win.innerWidth, height: win.innerHeight };
                    }");
                ViewportSizeDto size = await sizeTask.ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                await popup.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
                await popup.WaitForLoadStateAsync().ConfigureAwait(false);
                ViewportSizeDto resized = await popup.EvaluateAsync<ViewportSizeDto>(
                    "() => ({ width: window.innerWidth, height: window.innerHeight })").ConfigureAwait(false);
                Assert.That(size.Width, Is.EqualTo(600));
                Assert.That(size.Height, Is.EqualTo(300));
                Assert.That(resized.Width, Is.EqualTo(500));
                Assert.That(resized.Height, Is.EqualTo(400));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should respect routes from browser context when using window.open")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectRoutesFromBrowserContextWhenUsingWindowOpen()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                bool intercepted = false;
                await context.RouteAsync("**/empty.html", route =>
                {
                    intercepted = true;
                    return route.ContinueAsync();
                }).ConfigureAwait(false);
                await Task.WhenAll(
                    page.WaitForEventAsync(PageEvent.Popup),
                    page.EvaluateAsync("url => window.__popup = window.open(url)", EmptyPage)).ConfigureAwait(false);
                Assert.That(intercepted, Is.True);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "BrowserContext.addInitScript should apply to an in-process popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserContextAddInitScriptShouldApplyToAnInProcessPopup()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                await context.AddInitScriptAsync("() => window.injected = 123").ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                int injected = await page.EvaluateAsync<int>(
                    "() => { const win = window.open('about:blank'); return win.injected; }").ConfigureAwait(false);
                Assert.That(injected, Is.EqualTo(123));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "BrowserContext.addInitScript should apply to a cross-process popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserContextAddInitScriptShouldApplyToACrossProcessPopup()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                await context.AddInitScriptAsync("() => window.injected = 123").ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                Task evaluateTask = page.EvaluateAsync("url => window.open(url)", CrossProcessPrefix + "/title.html");
                IPage popup = await popupTask.ConfigureAwait(false);
                await evaluateTask.ConfigureAwait(false);
                Assert.That(await popup.EvaluateAsync<int>("injected").ConfigureAwait(false), Is.EqualTo(123));
                await popup.ReloadAsync().ConfigureAwait(false);
                Assert.That(await popup.EvaluateAsync<int>("injected").ConfigureAwait(false), Is.EqualTo(123));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should expose function from browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeFunctionFromBrowserContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                List<string> messages = new List<string>();
                await context.ExposeFunctionAsync("add", (int a, int b) =>
                {
                    messages.Add("binding");
                    return a + b;
                }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                context.Page += (_, _) => messages.Add("page");
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                int added = await page.EvaluateAsync<int>(
                    "async () => { const win = window.open('about:blank'); return win.add(9, 4); }").ConfigureAwait(false);
                Assert.That(added, Is.EqualTo(13));
                Assert.That(string.Join("|", messages), Is.EqualTo("page|binding"));
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should not dispatch binding on a closed page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotDispatchBindingOnAClosedPage()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                bool? wasClosed = null;
                await context.ExposeBindingAsync("add", (BindingSource source, int a, int b) =>
                {
                    wasClosed = source.Page.IsClosed;
                    return a + b;
                }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await Task.WhenAll(
                    page.WaitForEventAsync(PageEvent.Popup),
                    page.EvaluateAsync(
                        @"async () => {
                            const win = window.open('about:blank');
                            win.add(9, 4);
                            win.close();
                        }")).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
                Assert.That(wasClosed, Is.Not.True);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should not throttle rAF in the opener page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrottleRafInTheOpenerPage()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                Task evaluateTask = page.EvaluateAsync("() => { window.open('about:blank'); }");
                IPage popup = await popupTask.ConfigureAwait(false);
                await evaluateTask.ConfigureAwait(false);
                await Task.WhenAll(WaitForRafsAsync(page, 30), WaitForRafsAsync(popup, 30)).ConfigureAwait(false);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("popup.spec.ts", "should not throw when click closes popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenClickClosesPopup()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                Task evaluateTask = page.EvaluateAsync(
                    @"async () => { const w = window.open('about:blank'); w.document.body.innerHTML = '<button onclick=""window.close()"">close</button>'; }");
                IPage popup = await popupTask.ConfigureAwait(false);
                await evaluateTask.ConfigureAwait(false);
                await popup.GetByRole("button").ClickAsync().ConfigureAwait(false);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        private static Task WaitForRafsAsync(IPage page, int count)
            => page.EvaluateAsync(
                @"count => new Promise(resolve => {
                    const onRaf = () => {
                        --count;
                        if (!count)
                            resolve();
                        else
                            requestAnimationFrame(onRaf);
                    };
                    requestAnimationFrame(onRaf);
                })",
                count);

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

        private sealed class ViewportSizeDto
        {
            [JsonPropertyName("width")]
            public int Width { get; set; }

            [JsonPropertyName("height")]
            public int Height { get; set; }
        }
    }
}
