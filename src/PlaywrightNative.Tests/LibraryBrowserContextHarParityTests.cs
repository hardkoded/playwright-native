/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-har.spec.ts</c> parity.
    /// Do not edit leftover <c>RouteFromHarTests</c>,
    /// <c>ContextHarTests</c>, or <c>RouteFromHarUpdate*</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextHarParityTests : PageTestEx
    {
        private const string FetchEcho =
            "(body) => fetch('/echo', { method: 'POST', body }).then(r => r.text())";

        private const string FetchEchoCatch =
            "(body) => fetch('/echo', { method: 'POST', body }).then(r => r.text()).catch(e => e && (e.message || String(e)))";

        private const string FetchEchoRedir =
            "(arg) => fetch(arg.path, { method: 'POST', body: arg.body }).then(r => r.text())";

        private const string FetchEchoHeader =
            "(bazValue) => fetch('/echo', { method: 'POST', body: '', headers: { foo: 'foo-value', bar: 'bar-value', baz: bazValue } }).then(r => r.text())";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19842;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("browsercontext-har.spec.ts", "should context.routeFromHAR, matching the method and following redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldContextRouteFromHarMatchingTheMethodAndFollowingRedirects()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-fulfill.har")).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 0, 0)").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should page.routeFromHAR, matching the method and following redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPageRouteFromHarMatchingTheMethodAndFollowingRedirects()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteFromHARAsync(Asset("har-fulfill.har")).ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 0, 0)").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "fallback:continue should continue when not found in har")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FallbackContinueShouldContinueWhenNotFoundInHar()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-fulfill.har"), new() { NotFound = HarNotFound.Fallback }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "by default should abort requests not found in har")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ByDefaultShouldAbortRequestsNotFoundInHar()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-fulfill.har")).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => page.GoToAsync(EmptyPage)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "fallback:continue should continue requests on bad har")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FallbackContinueShouldContinueRequestsOnBadHar()
        {
            EnsureServer();
            string path = OutputPath("test.har");
            try
            {
                File.WriteAllText(path, "{\"log\":{}}");
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path, new() { NotFound = HarNotFound.Fallback }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should only handle requests matching url filter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOnlyHandleRequestsMatchingUrlFilter()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-fulfill.har"), "**/*.js", notFound: HarNotFound.Fallback).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("http://no.playwright/", async route =>
            {
                Assert.That(route.Request.Url, Is.EqualTo("http://no.playwright/"));
                await route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Body = "<script src=\"./script.js\"></script><div>hello</div>" }).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0)").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should only context.routeFromHAR requests matching url filter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOnlyContextRouteFromHarRequestsMatchingUrlFilter()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-fulfill.har"), "**/*.js").ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("http://no.playwright/", async route =>
            {
                Assert.That(route.Request.Url, Is.EqualTo("http://no.playwright/"));
                await route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Body = "<script src=\"./script.js\"></script><div>hello</div>" }).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0)").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should only page.routeFromHAR requests matching url filter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOnlyPageRouteFromHarRequestsMatchingUrlFilter()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteFromHARAsync(Asset("har-fulfill.har"), "**/*.js").ConfigureAwait(false);
            await context.RouteAsync("http://no.playwright/", async route =>
            {
                Assert.That(route.Request.Url, Is.EqualTo("http://no.playwright/"));
                await route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Body = "<script src=\"./script.js\"></script><div>hello</div>" }).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0)").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should apply overrides before routing from har")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldApplyOverridesBeforeRoutingFromHar()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-fulfill.har"), "**/*.js").ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("http://no.playwright/my-script.js", route =>
                route.FallbackAsync(new() { Url = "http://no.playwright/script2.js" })).ConfigureAwait(false);
            await context.RouteAsync("http://test.example/", route =>
                route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Body = "<script src=\"http://no.playwright/my-script.js\"></script><div>hello</div>" })).ConfigureAwait(false);
            await page.GoToAsync("http://test.example/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should support regex filter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportRegexFilter()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-fulfill.har"), new Regex(@".*(\.js|.*\.css|no.playwright/)$")).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 0, 0)").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "newPage should fulfill from har, matching the method and following redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NewPageShouldFulfillFromHarMatchingTheMethodAndFollowingRedirects()
        {
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.RouteFromHARAsync(Asset("har-fulfill.har")).ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 0, 0)").ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should change document URL after redirected navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChangeDocumentUrlAfterRedirectedNavigation()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-redirect.har")).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IResponse> navigation = page.WaitForNavigationAsync();
            Task waitUrl = page.WaitForURLAsync("https://www.theverge.com/");
            Task<IResponse> go = page.GoToAsync("https://theverge.com/");
            await Task.WhenAll(navigation, waitUrl, go).ConfigureAwait(false);
            IResponse response = await navigation.ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("https://www.theverge.com/").ConfigureAwait(false);
            Assert.That(response.Request.Url, Is.EqualTo("https://www.theverge.com/"));
            Assert.That(await page.EvaluateAsync<string>("(() => location.href)()").ConfigureAwait(false), Is.EqualTo("https://www.theverge.com/"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should change document URL after redirected navigation on click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChangeDocumentUrlAfterRedirectedNavigationOnClick()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-redirect.har"), new() { UrlRegex = new Regex(".*theverge.*") }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"https://theverge.com/\">click me</a>").ConfigureAwait(false);
            Task<IResponse> navigation = page.WaitForNavigationAsync();
            Task click = page.ClickAsync("text=click me");
            await Task.WhenAll(navigation, click).ConfigureAwait(false);
            IResponse response = await navigation.ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("https://www.theverge.com/").ConfigureAwait(false);
            Assert.That(response.Request.Url, Is.EqualTo("https://www.theverge.com/"));
            Assert.That(await page.EvaluateAsync<string>("(() => location.href)()").ConfigureAwait(false), Is.EqualTo("https://www.theverge.com/"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should goBack to redirected navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGoBackToRedirectedNavigation()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-redirect.har"), new() { UrlRegex = new Regex(".*theverge.*") }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("https://theverge.com/").ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(EmptyPage).ConfigureAwait(false);
            IResponse response = await page.GoBackAsync().ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("https://www.theverge.com/").ConfigureAwait(false);
            Assert.That(response.Request.Url, Is.EqualTo("https://www.theverge.com/"));
            Assert.That(await page.EvaluateAsync<string>("(() => location.href)()").ConfigureAwait(false), Is.EqualTo("https://www.theverge.com/"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should goForward to redirected navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGoForwardToRedirectedNavigation()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-redirect.har"), new() { UrlRegex = new Regex(".*theverge.*") }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(EmptyPage).ConfigureAwait(false);
            await page.GoToAsync("https://theverge.com/").ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("https://www.theverge.com/").ConfigureAwait(false);
            await page.GoBackAsync().ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(EmptyPage).ConfigureAwait(false);
            IResponse response = await page.GoForwardAsync().ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("https://www.theverge.com/").ConfigureAwait(false);
            Assert.That(response.Request.Url, Is.EqualTo("https://www.theverge.com/"));
            Assert.That(await page.EvaluateAsync<string>("(() => location.href)()").ConfigureAwait(false), Is.EqualTo("https://www.theverge.com/"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should reload redirected navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReloadRedirectedNavigation()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-redirect.har"), new() { UrlRegex = new Regex(".*theverge.*") }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("https://theverge.com/").ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("https://www.theverge.com/").ConfigureAwait(false);
            IResponse response = await page.ReloadAsync().ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync("https://www.theverge.com/").ConfigureAwait(false);
            Assert.That(response.Request.Url, Is.EqualTo("https://www.theverge.com/"));
            Assert.That(await page.EvaluateAsync<string>("(() => location.href)()").ConfigureAwait(false), Is.EqualTo("https://www.theverge.com/"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should fulfill from har with content in a file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillFromHarWithContentInAFile()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteFromHARAsync(Asset("har-sha1.har")).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await page.ContentAsync().ConfigureAwait(false), Is.EqualTo("<html><head></head><body>Hello, world</body></html>"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should round-trip har.zip")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundTripHarZip()
        {
            EnsureServer();
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarMode = HarMode.Minimal }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page2.ContentAsync().ConfigureAwait(false), Does.Contain("hello, world!"));
                await Assertions.Expect(page2.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should produce extracted zip")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProduceExtractedZip()
        {
            EnsureServer();
            string harPath = OutputPath("har.har");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarMode = HarMode.Minimal, RecordHarContent = HarContentPolicy.Attach }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context1.CloseAsync().ConfigureAwait(false);

                Assert.That(File.Exists(harPath), Is.True);
                string har = File.ReadAllText(harPath);
                Assert.That(har, Does.Not.Contain("background-color"));

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page2.ContentAsync().ConfigureAwait(false), Does.Contain("hello, world!"));
                await Assertions.Expect(page2.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDeleteAttached(harPath);
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should round-trip extracted har.zip")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundTripExtractedHarZip()
        {
            EnsureServer();
            string harPath = OutputPath("har.zip");
            string harDir = OutputPath("hardir");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarMode = HarMode.Minimal }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context1.CloseAsync().ConfigureAwait(false);

                Directory.CreateDirectory(harDir);
                ZipFile.ExtractToDirectory(harPath, harDir);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(Path.Combine(harDir, "har.har")).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page2.ContentAsync().ConfigureAwait(false), Does.Contain("hello, world!"));
                await Assertions.Expect(page2.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
                TryDeleteDir(harDir);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should round-trip har with postData")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundTripHarWithPostData()
        {
            EnsureServer();
            SetEchoRoute();
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarMode = HarMode.Minimal }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page1.EvaluateAsync<string>(FetchEcho, "1").ConfigureAwait(false), Is.EqualTo("1"));
                Assert.That(await page1.EvaluateAsync<string>(FetchEcho, "2").ConfigureAwait(false), Is.EqualTo("2"));
                Assert.That(await page1.EvaluateAsync<string>(FetchEcho, "3").ConfigureAwait(false), Is.EqualTo("3"));
                await context1.CloseAsync().ConfigureAwait(false);

                Server.Reset();
                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page2.EvaluateAsync<string>(FetchEcho, "1").ConfigureAwait(false), Is.EqualTo("1"));
                Assert.That(await page2.EvaluateAsync<string>(FetchEcho, "2").ConfigureAwait(false), Is.EqualTo("2"));
                Assert.That(await page2.EvaluateAsync<string>(FetchEcho, "3").ConfigureAwait(false), Is.EqualTo("3"));
                object failed = await page2.EvaluateAsync<string>(FetchEchoCatch, "4").ConfigureAwait(false);
                Assert.That(failed, Is.Not.Null);
                Assert.That(failed as string, Is.Not.Empty);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should record overridden requests to har")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordOverriddenRequestsToHar()
        {
            EnsureServer();
            SetEchoRoute();
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarMode = HarMode.Minimal }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page1.RouteAsync("**/echo_redir", async route =>
                {
                    int value = int.Parse(route.Request.PostData, CultureInfo.InvariantCulture);
                    await route.FallbackAsync(new() { Url = Prefix + "/echo", PostData = System.Text.Encoding.UTF8.GetBytes((value + 10).ToString(CultureInfo.InvariantCulture)) }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                Assert.That(
                    await page1.EvaluateAsync<string>(FetchEchoRedir, new Dictionary<string, string> { ["path"] = "/echo_redir", ["body"] = "1" }).ConfigureAwait(false),
                    Is.EqualTo("11"));
                Assert.That(
                    await page1.EvaluateAsync<string>(FetchEchoRedir, new Dictionary<string, string> { ["path"] = "/echo_redir", ["body"] = "2" }).ConfigureAwait(false),
                    Is.EqualTo("12"));
                await context1.CloseAsync().ConfigureAwait(false);

                Server.Reset();
                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(
                    await page2.EvaluateAsync<string>(FetchEchoRedir, new Dictionary<string, string> { ["path"] = "/echo", ["body"] = "11" }).ConfigureAwait(false),
                    Is.EqualTo("11"));
                Assert.That(
                    await page2.EvaluateAsync<string>(FetchEchoRedir, new Dictionary<string, string> { ["path"] = "/echo", ["body"] = "12" }).ConfigureAwait(false),
                    Is.EqualTo("12"));
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should disambiguate by header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDisambiguateByHeader()
        {
            EnsureServer();
            Server.SetRoute("/echo", http =>
            {
                string baz = http.Request.Headers["baz"].ToString();
                return http.Response.WriteAsync(baz);
            });
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarMode = HarMode.Minimal }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page1.EvaluateAsync<string>(FetchEchoHeader, "baz1").ConfigureAwait(false), Is.EqualTo("baz1"));
                Assert.That(await page1.EvaluateAsync<string>(FetchEchoHeader, "baz2").ConfigureAwait(false), Is.EqualTo("baz2"));
                Assert.That(await page1.EvaluateAsync<string>(FetchEchoHeader, "baz3").ConfigureAwait(false), Is.EqualTo("baz3"));
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page2.EvaluateAsync<string>(FetchEchoHeader, "baz1").ConfigureAwait(false), Is.EqualTo("baz1"));
                Assert.That(await page2.EvaluateAsync<string>(FetchEchoHeader, "baz2").ConfigureAwait(false), Is.EqualTo("baz2"));
                Assert.That(await page2.EvaluateAsync<string>(FetchEchoHeader, "baz3").ConfigureAwait(false), Is.EqualTo("baz3"));
                Assert.That(await page2.EvaluateAsync<string>(FetchEchoHeader, "baz4").ConfigureAwait(false), Is.EqualTo("baz1"));
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should update har.zip for context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUpdateHarZipForContext()
        {
            EnsureServer();
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context1.RouteFromHARAsync(harPath, new() { Update = true }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page2.ContentAsync().ConfigureAwait(false), Does.Contain("hello, world!"));
                await Assertions.Expect(page2.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should ignore boundary when matching multipart/form-data body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreBoundaryWhenMatchingMultipartFormDataBody()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(@"
      <form id=""form"" action=""form.html"" enctype=""multipart/form-data"" method=""POST"">
      <input id=""file"" type=""file"" multiple />
      <button type=""submit"">Upload</button>
      </form>");
            });
            string capturedBody = null;
            Server.SetRoute("/form.html", async http =>
            {
                using StreamReader reader = new(http.Request.Body);
                capturedBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<div>done</div>").ConfigureAwait(false);
            });

            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context1.RouteFromHARAsync(harPath, new() { Update = true }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                Task reqPromise = Server.WaitForRequest("/form.html");
                await page1.Locator("button").ClickAsync().ConfigureAwait(false);
                await Assertions.Expect(page1.Locator("div")).ToHaveTextAsync("done").ConfigureAwait(false);
                await reqPromise.ConfigureAwait(false);
                Assert.That(capturedBody, Does.Contain("---"));
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                Task<IRequest> requestPromise = page2.WaitForRequestAsync(new Regex(".*form.html"));
                await page2.Locator("button").ClickAsync().ConfigureAwait(false);
                IRequest request = await requestPromise.ConfigureAwait(false);
                Assert.That(await request.GetResponseAsync().ConfigureAwait(false), Is.Not.Null);
                Assert.That(request.Failure, Is.Null);
                await Assertions.Expect(page2.Locator("div")).ToHaveTextAsync("done").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should record single set-cookie headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordSingleSetCookieHeaders()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.ContentType = "text/html";
                http.Response.Headers.Append("set-cookie", "first=foo");
                return http.Response.WriteAsync(string.Empty);
            });
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context1.RouteFromHARAsync(harPath, new() { Update = true }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page1.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false), Is.EqualTo("first=foo"));
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page2.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false), Is.EqualTo("first=foo"));
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should record multiple set-cookie headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordMultipleSetCookieHeaders()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.ContentType = "text/html";
                http.Response.Headers.Append("set-cookie", "first=foo");
                http.Response.Headers.Append("set-cookie", "second=bar");
                return http.Response.WriteAsync(string.Empty);
            });
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context1.RouteFromHARAsync(harPath, new() { Update = true }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
                string cookie1 = await page1.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false);
                Assert.That(SortCookies(cookie1), Is.EqualTo("first=foo; second=bar"));
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
                string cookie2 = await page2.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false);
                Assert.That(SortCookies(cookie2), Is.EqualTo("first=foo; second=bar"));
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should update har.zip for page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUpdateHarZipForPage()
        {
            EnsureServer();
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.RouteFromHARAsync(harPath, new() { Update = true }).ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page2.ContentAsync().ConfigureAwait(false), Does.Contain("hello, world!"));
                await Assertions.Expect(page2.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should update har.zip for page with different options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUpdateHarZipForPageWithDifferentOptions()
        {
            EnsureServer();
            string harPath = OutputPath("har.zip");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.RouteFromHARAsync(harPath, new() { Update = true, UpdateMode = HarMode.Full, UpdateContent = RouteFromHarUpdateContentPolicy.Embed }).ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page2.ContentAsync().ConfigureAwait(false), Does.Contain("hello, world!"));
                await Assertions.Expect(page2.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should update extracted har.zip for page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUpdateExtractedHarZipForPage()
        {
            EnsureServer();
            string harPath = OutputPath("har.har");
            try
            {
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.RouteFromHARAsync(harPath, new() { Update = true }).ConfigureAwait(false);
                await page1.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context1.CloseAsync().ConfigureAwait(false);

                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.RouteFromHARAsync(harPath, new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page2.ContentAsync().ConfigureAwait(false), Does.Contain("hello, world!"));
                await Assertions.Expect(page2.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)").ConfigureAwait(false);
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDeleteAttached(harPath);
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "page.unrouteAll should stop page.routeFromHAR")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageUnrouteAllShouldStopPageRouteFromHar()
        {
            EnsureServer();
            IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
            await page1.RouteFromHARAsync(Asset("har-fulfill.har"), new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
            Exception error = await CatchAsync(() => page1.GoToAsync(EmptyPage)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            await page1.UnrouteAllAsync(UnrouteBehavior.Wait).ConfigureAwait(false);
            IResponse response = await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            await context1.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "context.unrouteAll should stop context.routeFromHAR")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextUnrouteAllShouldStopContextRouteFromHar()
        {
            EnsureServer();
            IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
            await context1.RouteFromHARAsync(Asset("har-fulfill.har"), new() { NotFound = HarNotFound.Abort }).ConfigureAwait(false);
            Exception error = await CatchAsync(() => page1.GoToAsync(EmptyPage)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            await context1.UnrouteAllAsync(UnrouteBehavior.Wait).ConfigureAwait(false);
            IResponse response = await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            await context1.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "should ignore aborted requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreAbortedRequests()
        {
            EnsureServer();
            string path = OutputPath("test.har");
            try
            {
                Server.SetRoute("/x", http =>
                {
                    http.Abort();
                    return Task.CompletedTask;
                });
                IBrowserContext context1 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context1.RouteFromHARAsync(path, new() { Update = true }).ConfigureAwait(false);
                IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                await page1.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task reqPromise = Server.WaitForRequest("/x");
                Task<string> evalPromise = page1.EvaluateAsync<string>(
                    "(url) => fetch(url).catch(e => 'cancelled')",
                    Prefix + "/x");
                await reqPromise.ConfigureAwait(false);
                Assert.That(await evalPromise.ConfigureAwait(false), Is.EqualTo("cancelled"));
                await context1.CloseAsync().ConfigureAwait(false);

                Server.Reset();
                Server.SetRoute("/x", http =>
                {
                    http.Response.ContentType = "text/plain";
                    return http.Response.WriteAsync("test");
                });
                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                await context2.RouteFromHARAsync(path).ConfigureAwait(false);
                IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<string> eval2 = page2.EvaluateAsync<string>(
                    "(url) => fetch(url).catch(e => 'cancelled')",
                    Prefix + "/x");
                Task timeout = page2.WaitForTimeoutAsync(1000);
                Task winner = await Task.WhenAny(eval2, timeout).ConfigureAwait(false);
                string result = winner == timeout ? "timeout" : await eval2.ConfigureAwait(false);
                Assert.That(result, Is.EqualTo("timeout"));
                await context2.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(path);
            }
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new List<IBrowserContext>(_browser.Contexts))
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

        private void SetEchoRoute()
        {
            Server.SetRoute("/echo", async http =>
            {
                using StreamReader reader = new(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                await http.Response.WriteAsync(body).ConfigureAwait(false);
            });
        }

        private static string Asset(string name)
        {
            string fromSource = Path.Combine(TestUtils.FindParentDirectory("PlaywrightNative.Tests"), "Assets", name);
            if (File.Exists(fromSource))
            {
                return fromSource;
            }

            return Path.Combine(AppContext.BaseDirectory, "Assets", name);
        }

        private static string OutputPath(string name)
            => Path.Combine(Path.GetTempPath(), "pw-wave842-" + Guid.NewGuid().ToString("N") + "-" + name);

        private static string SortCookies(string cookie)
        {
            if (string.IsNullOrEmpty(cookie))
            {
                return string.Empty;
            }

            string[] parts = cookie.Split(new[] { "; " }, StringSplitOptions.None);
            Array.Sort(parts, StringComparer.Ordinal);
            return string.Join("; ", parts);
        }

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }

        private static void TryDeleteAttached(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string dir = Path.GetDirectoryName(path);
            string folder = Path.GetFileNameWithoutExtension(path) + "-files";
            string attachDir = string.IsNullOrEmpty(dir) ? folder : Path.Combine(dir, folder);
            TryDeleteDir(attachDir);
        }

        private static void TryDeleteDir(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
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
