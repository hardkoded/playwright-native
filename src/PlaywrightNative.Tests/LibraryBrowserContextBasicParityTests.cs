/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-basic.spec.ts</c> parity.
    /// Skipped (official <c>it.skip</c> / Node-only): default user agent
    /// (<c>_channel.defaultUserAgentForTest</c>).
    /// Official <c>it.fixme</c> Chromium: should emulate navigator.onLine
    /// across navigations.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextBasicParityTests : PageTestEx
    {
        private const string TargetClosed = "Target page, context or browser has been closed";

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
            int basePort = 19834;
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

        [PlaywrightTest("browsercontext-basic.spec.ts", "should create new context @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateNewContext()
        {
            Assert.That(_browser.Contexts.Count, Is.EqualTo(0));
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Assert.That(_browser.Contexts, Is.EqualTo(new[] { context }));
            Assert.That(context.Browser, Is.SameAs(_browser));
            IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
            Assert.That(_browser.Contexts, Is.EqualTo(new[] { context, context2 }));
            await context.CloseAsync().ConfigureAwait(false);
            Assert.That(_browser.Contexts, Is.EqualTo(new[] { context2 }));
            Assert.That(context2.Browser, Is.SameAs(_browser));
            await context2.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should be able to click across browser contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToClickAcrossBrowserContexts()
        {
            Assert.That(_browser.Contexts.Count, Is.EqualTo(0));

            IPage page1 = await CreateClickPageAsync().ConfigureAwait(false);
            IPage page2 = await CreateClickPageAsync().ConfigureAwait(false);

            const int clickCount = 20;
            await Task.WhenAll(
                ClickInPageAsync(page1, clickCount),
                ClickInPageAsync(page2, clickCount)).ConfigureAwait(false);

            Assert.That(
                await page1.EvaluateAsync<int>("(() => window['clicks'])()").ConfigureAwait(false),
                Is.EqualTo(clickCount));
            Assert.That(
                await page2.EvaluateAsync<int>("(() => window['clicks'])()").ConfigureAwait(false),
                Is.EqualTo(clickCount));

            await page1.CloseAsync().ConfigureAwait(false);
            await page2.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should be able to hover across browser contexts in parallel")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToHoverAcrossBrowserContextsInParallel()
        {
            const string html = @"
    <style>
      [role=""tooltip""] { display: none; }
      [role=""tooltip""].visible { display: block; }
    </style>
    <button>Hover me</button>
    <div role=""tooltip"">Tooltip content</div>
    <script>
      const button = document.querySelector('button');
      const tooltip = document.querySelector('[role=""tooltip""]');
      button.addEventListener('pointerenter', () => tooltip.classList.add('visible'));
      button.addEventListener('pointerleave', () => tooltip.classList.remove('visible'));
    </script>
  ";

            IPage[] pages = await Task.WhenAll(
                CreateHoverPageAsync(html),
                CreateHoverPageAsync(html),
                CreateHoverPageAsync(html),
                CreateHoverPageAsync(html),
                CreateHoverPageAsync(html)).ConfigureAwait(false);

            await Task.WhenAll(HoverAndAssertTooltipAsync(pages[0], 1),
                HoverAndAssertTooltipAsync(pages[1], 2),
                HoverAndAssertTooltipAsync(pages[2], 3),
                HoverAndAssertTooltipAsync(pages[3], 4),
                HoverAndAssertTooltipAsync(pages[4], 5)).ConfigureAwait(false);

            await Task.WhenAll(
                pages[0].Context.CloseAsync(),
                pages[1].Context.CloseAsync(),
                pages[2].Context.CloseAsync(),
                pages[3].Context.CloseAsync(),
                pages[4].Context.CloseAsync()).ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "window.open should use parent tab context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WindowOpenShouldUseParentTabContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("url => window.open(url)", EmptyPage).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(popup.Context, Is.SameAs(context));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should isolate localStorage and cookies @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateLocalStorageAndCookies()
        {
            EnsureServer();
            IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
            IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
            Assert.That(context1.Pages.Count, Is.EqualTo(0));
            Assert.That(context2.Pages.Count, Is.EqualTo(0));

            IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
            await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page1.EvaluateAsync(@"(() => {
                localStorage.setItem('name', 'page1');
                document.cookie = 'name=page1';
            })()").ConfigureAwait(false);

            Assert.That(context1.Pages.Count, Is.EqualTo(1));
            Assert.That(context2.Pages.Count, Is.EqualTo(0));

            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page2.EvaluateAsync(@"(() => {
                localStorage.setItem('name', 'page2');
                document.cookie = 'name=page2';
            })()").ConfigureAwait(false);

            Assert.That(context1.Pages.Count, Is.EqualTo(1));
            Assert.That(context2.Pages.Count, Is.EqualTo(1));
            Assert.That(context1.Pages, Does.Contain(page1));
            Assert.That(context2.Pages, Does.Contain(page2));

            Assert.That(
                await page1.EvaluateAsync<string>("(() => localStorage.getItem('name'))()").ConfigureAwait(false),
                Is.EqualTo("page1"));
            Assert.That(
                await page1.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false),
                Is.EqualTo("name=page1"));
            Assert.That(
                await page2.EvaluateAsync<string>("(() => localStorage.getItem('name'))()").ConfigureAwait(false),
                Is.EqualTo("page2"));
            Assert.That(
                await page2.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false),
                Is.EqualTo("name=page2"));

            await Task.WhenAll(context1.CloseAsync(), context2.CloseAsync()).ConfigureAwait(false);
            Assert.That(_browser.Contexts.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should propagate default viewport to the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateDefaultViewportToThePage()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 456, Height = 789 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await VerifyViewportAsync(page, 456, 789).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should make a copy of default viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMakeACopyOfDefaultViewport()
        {
            ViewportSize viewport = new ViewportSize { Width = 456, Height = 789 };
            IBrowserContext context = await _browser.NewContextAsync(new() { ViewportSize = viewport }).ConfigureAwait(false);
            viewport.Width = 567;
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await VerifyViewportAsync(page, 456, 789).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should respect deviceScaleFactor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectDeviceScaleFactor()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { DeviceScaleFactor = 3 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.devicePixelRatio").ConfigureAwait(false), Is.EqualTo(3));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should not allow deviceScaleFactor with null viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNotAllowDeviceScaleFactorWithNullViewport()
        {
            Exception error = Assert.CatchAsync(
                () => _browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport, DeviceScaleFactor = 1 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("\"deviceScaleFactor\" option is not supported with null \"viewport\""));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should not allow isMobile with null viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNotAllowIsMobileWithNullViewport()
        {
            Exception error = Assert.CatchAsync(
                () => _browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport, IsMobile = true }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("\"isMobile\" option is not supported with null \"viewport\""));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "close() should work for empty context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CloseShouldWorkForEmptyContext()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "close() should abort waitForEvent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CloseShouldAbortWaitForEvent()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<IPage> waitTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await context.CloseAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(waitTask).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(TargetClosed));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "close() should be callable twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CloseShouldBeCallableTwice()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should pass self to close event")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassSelfToCloseEvent()
        {
            IBrowserContext newContext = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<IBrowserContext> closedTask = newContext.WaitForEventAsync(BrowserContextEvent.Close);
            await newContext.CloseAsync().ConfigureAwait(false);
            IBrowserContext closedContext = await closedTask.ConfigureAwait(false);
            Assert.That(closedContext, Is.SameAs(newContext));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should not report frameless pages on error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotReportFramelessPagesOnError()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Server.SetRoute("/empty.html", http =>
            {
                return http.Response.WriteAsync($"<a href=\"{EmptyPage}\" target=\"_blank\">Click me</a>");
            });
            IPage popup = null;
            context.Page += (_, p) => popup = p;
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.ClickAsync("\"Click me\"").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
            if (popup != null)
            {
                Assert.That(popup.IsClosed, Is.True);
                Assert.That(popup.MainFrame, Is.Not.Null);
            }
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should return all of the pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnAllOfThePages()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IPage second = await context.NewPageAsync().ConfigureAwait(false);
            IReadOnlyCollection<IPage> allPages = context.Pages;
            Assert.That(allPages.Count, Is.EqualTo(2));
            Assert.That(allPages, Does.Contain(page));
            Assert.That(allPages, Does.Contain(second));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should close all belonging pages once closing context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCloseAllBelongingPagesOnceClosingContext()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(context.Pages.Count, Is.EqualTo(1));

            await context.CloseAsync().ConfigureAwait(false);
            Assert.That(context.Pages.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should disable javascript")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDisableJavascript()
        {
            {
                IBrowserContext context = await _browser.NewContextAsync(new() { JavaScriptEnabled = false }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("data:text/html, <script>var something = \"forbidden\"</script>").ConfigureAwait(false);
                Exception error = await CatchAsync(page.EvaluateAsync("something")).ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                if (TestConstants.IsWebKit)
                {
                    Assert.That(error.Message, Does.Contain("Can't find variable: something"));
                }
                else
                {
                    Assert.That(error.Message, Does.Contain("something is not defined"));
                }

                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("data:text/html, <script>var something = \"forbidden\"</script>").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("something").ConfigureAwait(false), Is.EqualTo("forbidden"));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should be able to navigate after disabling javascript")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToNavigateAfterDisablingJavascript()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { JavaScriptEnabled = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should not hang on promises after disabling javascript")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangOnPromisesAfterDisablingJavascript()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { JavaScriptEnabled = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("(() => 1)()").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvaluateAsync<int>("(async () => 2)()").ConfigureAwait(false), Is.EqualTo(2));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "setContent should work after disabling javascript")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetContentShouldWorkAfterDisablingJavascript()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { JavaScriptEnabled = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Hello</h1>").ConfigureAwait(false);
            Assert.That(await page.Locator("h1").InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should work with offline option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithOfflineOption()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { Offline = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(page.GoToAsync(EmptyPage)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            await context.SetOfflineAsync(false).ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "fetch with keepalive should throw when offline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchWithKeepaliveShouldThrowWhenOffline()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string url = Prefix + "/fetch";
            Server.SetRoute("/fetch", http =>
            {
                http.Response.ContentType = "application/json";
                return http.Response.WriteAsync(JsonSerializer.Serialize("hello"));
            });

            JsonElement okResponse = await page.EvaluateAsync<JsonElement>(
                "url => fetch(url, { cache: 'no-store', keepalive: true }).then(response => response.json())",
                url).ConfigureAwait(false);
            Assert.That(okResponse.GetString(), Is.EqualTo("hello"));

            await context.SetOfflineAsync(true).ConfigureAwait(false);
            string offlineResponse = await page.EvaluateAsync<string>(
                @"async url => {
                    try {
                      const response = await fetch(url, { cache: 'no-store', keepalive: true });
                      return await response.json();
                    } catch {
                      return 'error';
                    }
                }",
                url).ConfigureAwait(false);
            Assert.That(offlineResponse, Is.EqualTo("error"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should emulate navigator.onLine")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateNavigatorOnLine()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.True);
            await context.SetOfflineAsync(true).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.False);
            await context.SetOfflineAsync(false).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should emulate navigator.onLine across navigations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateNavigatorOnLineAcrossNavigations()
        {
            EnsureServer();
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("does not survive cross-process navgiation");
            }

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.True);
            await context.SetOfflineAsync(true).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.False);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.False);
            await page.GoToAsync("data:text/html,<title>offline</title>").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.False);

            await context.SetOfflineAsync(false).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("(() => window.navigator.onLine)()").ConfigureAwait(false), Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should emulate offline event")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateOfflineEvent()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IJSHandle events = await page.EvaluateHandleAsync(@"(() => {
                const events = [];
                window.addEventListener('offline', () => events.push('offline'));
                window.addEventListener('online', () => events.push('online'));
                return events;
            })()").ConfigureAwait(false);
            await context.SetOfflineAsync(true).ConfigureAwait(false);
            await PollEqualAsync(() => events.JsonValueAsync<string[]>(), new[] { "offline" }).ConfigureAwait(false);
            await context.SetOfflineAsync(false).ConfigureAwait(false);
            await PollEqualAsync(() => events.JsonValueAsync<string[]>(), new[] { "offline", "online" }).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should emulate media in popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateMediaInPopup()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForPopupAsync();
                await page.EvaluateAsync("url => { window.open(url); }", EmptyPage).ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                Assert.That(
                    await popup.EvaluateAsync<bool>("(() => matchMedia('(prefers-color-scheme: light)').matches)()").ConfigureAwait(false),
                    Is.False);
                Assert.That(
                    await popup.EvaluateAsync<bool>("(() => matchMedia('(prefers-color-scheme: dark)').matches)()").ConfigureAwait(false),
                    Is.True);
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IPage page = await _browser.NewPageAsync(new() { ColorScheme = ColorScheme.Light }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForPopupAsync();
                await page.EvaluateAsync("url => { window.open(url); }", EmptyPage).ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                Assert.That(
                    await popup.EvaluateAsync<bool>("(() => matchMedia('(prefers-color-scheme: light)').matches)()").ConfigureAwait(false),
                    Is.True);
                Assert.That(
                    await popup.EvaluateAsync<bool>("(() => matchMedia('(prefers-color-scheme: dark)').matches)()").ConfigureAwait(false),
                    Is.False);
                await page.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should emulate media in cross-process iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateMediaInCrossProcessIframe()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { ColorScheme = ColorScheme.Dark }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "frame1", CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(
                await frame.EvaluateAsync<bool>("(() => matchMedia('(prefers-color-scheme: dark)').matches)()").ConfigureAwait(false),
                Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "should create two pages in parallel in various contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateTwoPagesInParallelInVariousContexts()
        {
            IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
            IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
            await Task.WhenAll(
                context1.NewPageAsync(),
                context1.NewPageAsync(),
                context2.NewPageAsync(),
                context2.NewPageAsync()).ConfigureAwait(false);
            await context1.CloseAsync().ConfigureAwait(false);
            await context2.CloseAsync().ConfigureAwait(false);
            IBrowserContext context3 = await _browser.NewContextAsync().ConfigureAwait(false);
            await Task.WhenAll(
                context3.NewPageAsync(),
                context3.NewPageAsync()).ConfigureAwait(false);
            await context3.CloseAsync().ConfigureAwait(false);
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            List<IBrowserContext> leftover = new List<IBrowserContext>(_browser.Contexts);
            foreach (IBrowserContext context in leftover)
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

        private async Task<IPage> CreateClickPageAsync()
        {
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>Click me</button>").ConfigureAwait(false);
            await page.Locator("button").EvaluateAsync<object>(@"(button) => {
                window['clicks'] = 0;
                button.addEventListener('click', () => ++window['clicks'], false);
            }").ConfigureAwait(false);
            return page;
        }

        private static async Task ClickInPageAsync(IPage page, int count)
        {
            for (int i = 0; i < count; i++)
            {
                await page.Locator("button").ClickAsync().ConfigureAwait(false);
            }
        }

        private async Task<IPage> CreateHoverPageAsync(string html)
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(html).ConfigureAwait(false);
            return page;
        }

        private static async Task HoverAndAssertTooltipAsync(IPage page, int index)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Hover me" }).HoverAsync().ConfigureAwait(false);
            Assert.That(
                await page.GetByRole(AriaRole.Tooltip).IsVisibleAsync().ConfigureAwait(false),
                Is.True,
                $"Tooltip for page {index} should be visible");
        }

        private static async Task VerifyViewportAsync(IPage page, int width, int height)
        {
            Assert.That(page.ViewportSize, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(width));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(height));
            Assert.That(await page.EvaluateAsync<int>("window.innerWidth").ConfigureAwait(false), Is.EqualTo(width));
            Assert.That(await page.EvaluateAsync<int>("window.innerHeight").ConfigureAwait(false), Is.EqualTo(height));
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson + "; f.id = " + nameJson + "; f.src = " + urlJson +
                "; document.body.appendChild(f); })()").ConfigureAwait(false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
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

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        private static async Task PollEqualAsync<T>(Func<Task<T>> getValue, T expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            T last = default;
            while (DateTime.UtcNow < deadline)
            {
                last = await getValue().ConfigureAwait(false);
                if (EqualsSequence(last, expected))
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(last, Is.EqualTo(expected));
        }

        private static bool EqualsSequence<T>(T actual, T expected)
        {
            if (actual is IEnumerable<string> actualItems && expected is IEnumerable<string> expectedItems)
            {
                return new List<string>(actualItems).Count == new List<string>(expectedItems).Count
                    && string.Join("\0", actualItems) == string.Join("\0", expectedItems);
            }

            return Equals(actual, expected);
        }

        private static async Task<Exception> CatchAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
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
