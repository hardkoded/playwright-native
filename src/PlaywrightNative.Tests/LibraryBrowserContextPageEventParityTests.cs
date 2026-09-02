/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-page-event.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextPageEventTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextPageEventParityTests : PageTestEx
    {
        private const string OpenUrl =
            "(url) => window.open(url)";

        private const string OpenBlank =
            "() => window.open()";

        private const string HelloWorld =
            "() => ['Hello', 'world'].join(' ')";

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
            int basePort = 19845;
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

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveUrl()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> otherTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.EvaluateAsync(OpenUrl, EmptyPage).ConfigureAwait(false);
            IPage otherPage = await otherTask.ConfigureAwait(false);
            Assert.That(otherPage.Url, Is.EqualTo(EmptyPage));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have url after domcontentloaded")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveUrlAfterDomcontentloaded()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> otherTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.EvaluateAsync(OpenUrl, EmptyPage).ConfigureAwait(false);
            IPage otherPage = await otherTask.ConfigureAwait(false);
            await otherPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(otherPage.Url, Is.EqualTo(EmptyPage));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have about:blank url with domcontentloaded")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveAboutBlankUrlWithDomcontentloaded()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> otherTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.EvaluateAsync(OpenUrl, "about:blank").ConfigureAwait(false);
            IPage otherPage = await otherTask.ConfigureAwait(false);
            await otherPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(otherPage.Url, Is.EqualTo("about:blank"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have about:blank for empty url with domcontentloaded")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveAboutBlankForEmptyUrlWithDomcontentloaded()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> otherTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.EvaluateAsync(OpenBlank).ConfigureAwait(false);
            IPage otherPage = await otherTask.ConfigureAwait(false);
            await otherPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(otherPage.Url, Is.EqualTo("about:blank"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should report when a new page is created and closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportWhenANewPageIsCreatedAndClosed()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> otherTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.EvaluateAsync(OpenUrl, CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            IPage otherPage = await otherTask.ConfigureAwait(false);
            Assert.That(otherPage.Url, Does.Contain(CrossProcessPrefix));
            Assert.That(await otherPage.EvaluateAsync<string>(HelloWorld).ConfigureAwait(false), Is.EqualTo("Hello world"));
            Assert.That(await otherPage.QuerySelectorAsync("body").ConfigureAwait(false), Is.Not.Null);

            IReadOnlyCollection<IPage> allPages = context.Pages;
            Assert.That(allPages, Does.Contain(page));
            Assert.That(allPages, Does.Contain(otherPage));

            bool closeEventReceived = false;
            otherPage.Close += (_, _) => closeEventReceived = true;
            await otherPage.CloseAsync().ConfigureAwait(false);
            Assert.That(closeEventReceived, Is.True);

            allPages = context.Pages;
            Assert.That(allPages, Does.Contain(page));
            Assert.That(allPages, Does.Not.Contain(otherPage));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should report initialized pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportInitializedPages()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<IPage> pageTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            _ = context.NewPageAsync();
            IPage newPage = await pageTask.ConfigureAwait(false);
            Assert.That(newPage.Url, Is.EqualTo("about:blank"));

            Task<IPage> popupTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            Task evaluateTask = newPage.EvaluateAsync("() => window.open('about:blank')");
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(popup.Url, Is.EqualTo("about:blank"));
            await evaluateTask.ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should not crash while redirecting of original request was missed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhileRedirectingOfOriginalRequestWasMissed()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<HttpContext> css = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/one-style.css", http =>
            {
                css.TrySetResult(http);
                return Task.Delay(-1);
            });
            Task<IPage> newPageTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            _ = page.EvaluateAsync(OpenUrl, Prefix + "/one-style.html");
            IPage newPage = await newPageTask.ConfigureAwait(false);
            HttpContext cssContext = await css.Task.ConfigureAwait(false);
            cssContext.Response.StatusCode = 302;
            cssContext.Response.Headers["Location"] = "/injectedstyle.css";
            await cssContext.Response.CompleteAsync().ConfigureAwait(false);
            await newPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(newPage.Url, Is.EqualTo(Prefix + "/one-style.html"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should have an opener")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveAnOpener()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.GoToAsync(Prefix + "/popup/window-open.html").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(popup.Url, Is.EqualTo(Prefix + "/popup/popup.html"));
            Assert.That(await popup.OpenerAsync().ConfigureAwait(false), Is.SameAs(page));
            Assert.That(await page.OpenerAsync().ConfigureAwait(false), Is.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should fire page lifecycle events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFirePageLifecycleEvents()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            List<string> events = new();
            context.Page += (_, opened) =>
            {
                events.Add("CREATED: " + opened.Url);
                opened.Close += (_, closed) => events.Add("DESTROYED: " + closed.Url);
            };
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[]
            {
                "CREATED: about:blank",
                "DESTROYED: " + EmptyPage,
            }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should work with Shift-clicking")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithShiftClicking()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"/one-style.html\">yo</a>").ConfigureAwait(false);
            Task<IPage> popupTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.ClickAsync("a", new() { Modifiers = new[] { KeyboardModifier.Shift } }).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(await popup.OpenerAsync().ConfigureAwait(false), Is.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "should work with Ctrl-clicking")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCtrlClicking()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"/one-style.html\">yo</a>").ConfigureAwait(false);
            Task<IPage> popupTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.ClickAsync("a", new() { Modifiers = new[] { KeyboardModifier.ControlOrMeta } }).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Assert.That(await popup.OpenerAsync().ConfigureAwait(false), Is.Null);
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
