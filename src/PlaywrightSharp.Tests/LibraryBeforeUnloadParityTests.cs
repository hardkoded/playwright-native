/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/beforeunload.spec.ts</c> parity for
    /// <c>beforeunload</c> close and navigation.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBeforeUnloadParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string HelloWorld = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19827;
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
                    HelloWorld = Prefix + "/empty.html";
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
                HelloWorld = TestConstants.EmptyPage;
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
                    await RecycleBrowserAsync().ConfigureAwait(false);
                }
                else
                {
                    _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                }
            }

            try
            {
                _context = await NewContextOrRecycleAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                _context = await _browser.NewContextAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
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

        private IPage Page => _page;

        [PlaywrightTest("beforeunload.spec.ts", "should close browser with beforeunload page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCloseBrowserWithBeforeunloadPage()
        {
            EnsureServer();
            IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
                await page.ClickAsync("body").ConfigureAwait(false);
                await browser.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("beforeunload.spec.ts", "should close browsercontext with beforeunload page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCloseBrowsercontextWithBeforeunloadPage()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await Page.ClickAsync("body").ConfigureAwait(false);
            await _context.CloseAsync().ConfigureAwait(false);
            _context = null;
            _page = null;
        }

        [PlaywrightTest("beforeunload.spec.ts", "should be able to navigate away from page with beforeunload")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToNavigateAwayFromPageWithBeforeunload()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await Page.ClickAsync("body").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
        }

        [PlaywrightTest("beforeunload.spec.ts", "should close page with beforeunload listener")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClosePageWithBeforeunloadListener()
        {
            EnsureServer();
            IPage newPage = await _context.NewPageAsync().ConfigureAwait(false);
            await newPage.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await newPage.ClickAsync("body").ConfigureAwait(false);
            await newPage.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("beforeunload.spec.ts", "should run beforeunload if asked for @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunBeforeunloadIfAskedFor()
        {
            EnsureServer();
            IPage newPage = await _context.NewPageAsync().ConfigureAwait(false);
            await newPage.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await newPage.ClickAsync("body").ConfigureAwait(false);
            Task<IDialog> dialogTask = newPage.WaitForEventAsync(PageEvent.Dialog);
            Task closeTask = newPage.CloseAsync(new() { RunBeforeUnload = true });
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            Assert.That(dialog.Type, Is.EqualTo(DialogType.BeforeUnload));
            Assert.That(dialog.DefaultValue, Is.EqualTo(string.Empty));
            if (TestConstants.IsChromium)
            {
                Assert.That(dialog.Message, Is.EqualTo(string.Empty));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(dialog.Message, Is.EqualTo("Leave?"));
            }
            else
            {
                Assert.That(
                    dialog.Message,
                    Does.Contain("This page is asking you to confirm that you want to leave"));
            }

            Task<IPage> closedTask = newPage.WaitForEventAsync(PageEvent.Close);
            await dialog.AcceptAsync().ConfigureAwait(false);
            await closedTask.ConfigureAwait(false);
            try
            {
                await closeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("beforeunload.spec.ts", "should access page after beforeunload")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAccessPageAfterBeforeunload()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await Page.ClickAsync("body").ConfigureAwait(false);
            Task<IDialog> dialogTask = Page.WaitForEventAsync(PageEvent.Dialog);
            Task closeTask = Page.CloseAsync(new() { RunBeforeUnload = true });
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            await dialog.DismissAsync().ConfigureAwait(false);
            await Page.EvaluateAsync("() => document.title").ConfigureAwait(false);
            try
            {
                await closeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("beforeunload.spec.ts", "should not stall on evaluate when dismissing beforeunload")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotStallOnEvaluateWhenDismissingBeforeunload()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await Page.ClickAsync("body").ConfigureAwait(false);
            await Task.WhenAll(
                Page.WaitForEventAsync(PageEvent.Dialog).ContinueWith(
                    t => t.Result.DismissAsync(),
                    TaskScheduler.Default).Unwrap(),
                Page.EvaluateAsync("() => { window.location.reload(); }")).ConfigureAwait(false);
        }

        [PlaywrightTest("beforeunload.spec.ts", "should not stall on click when dismissing beforeunload")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotStallOnClickWhenDismissingBeforeunload()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync("<a href=\"" + Prefix + "/frames/one-frame.html\">click me</a>")
                .ConfigureAwait(false);
            await Page.EvaluateAsync("() => { window.onbeforeunload = () => false; }").ConfigureAwait(false);
            Page.Dialog += async (_, dialog) =>
            {
                await dialog.DismissAsync().ConfigureAwait(false);
            };
            await Page.GetByRole("link").ClickAsync(new() { NoWaitAfter = true }).ConfigureAwait(false);
            await Page.EvaluateAsync("() => { window.onbeforeunload = null; }").ConfigureAwait(false);
            await Page.GetByRole("link").ClickAsync(new() { Timeout = 5000 }).ConfigureAwait(false);
            await Assertions.Expect(Page).ToHaveURLAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
        }

        [PlaywrightTest("beforeunload.spec.ts", "should support dismissing the dialog multiple times")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportDismissingTheDialogMultipleTimes()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await Page.ClickAsync("body").ConfigureAwait(false);
            Task<IDialog> dialogTask = Page.WaitForEventAsync(PageEvent.Dialog);
            Task closeTask = Page.CloseAsync(new() { RunBeforeUnload = true });
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            await dialog.DismissAsync().ConfigureAwait(false);
            try
            {
                await closeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            Task<IDialog> dialog2Task = Page.WaitForEventAsync(PageEvent.Dialog);
            Task close2Task = Page.CloseAsync(new() { RunBeforeUnload = true });
            IDialog dialog2 = await dialog2Task.ConfigureAwait(false);
            await dialog2.DismissAsync().ConfigureAwait(false);
            try
            {
                await close2Task.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("beforeunload.spec.ts", "should support closing the page after a previous dismiss")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportClosingThePageAfterAPreviousDismiss()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await Page.ClickAsync("body").ConfigureAwait(false);
            Task<IDialog> dialogTask = Page.WaitForEventAsync(PageEvent.Dialog);
            Task closeTask = Page.CloseAsync(new() { RunBeforeUnload = true });
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            await dialog.DismissAsync().ConfigureAwait(false);
            try
            {
                await closeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            await Page.CloseAsync().ConfigureAwait(false);
            Assert.That(Page.IsClosed, Is.True);
        }

        [PlaywrightTest("beforeunload.spec.ts", "should support closing the page via a subsequent onbeforeunload dialog")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportClosingThePageViaASubsequentOnbeforeunloadDialog()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/beforeunload.html").ConfigureAwait(false);
            await Page.ClickAsync("body").ConfigureAwait(false);
            Task<IDialog> dialogTask = Page.WaitForEventAsync(PageEvent.Dialog);
            Task closeTask = Page.CloseAsync(new() { RunBeforeUnload = true });
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            Task<IPage> closedTask = Page.WaitForEventAsync(PageEvent.Close);
            await dialog.AcceptAsync().ConfigureAwait(false);
            await closedTask.ConfigureAwait(false);
            Assert.That(Page.IsClosed, Is.True);
            try
            {
                await closeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("beforeunload.spec.ts", "does not get stalled by beforeUnload")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DoesNotGetStalledByBeforeUnload()
        {
            EnsureServer();
            await Page.GoToAsync(HelloWorld).ConfigureAwait(false);
            await Page.EvaluateAsync(@"() => {
    window.addEventListener('beforeunload', event => {
      event.preventDefault();
    });
  }").ConfigureAwait(false);
            Page.Dialog += (_, dialog) =>
            {
                _ = dialog.DismissAsync();
            };
            await Page.ClickAsync("body").ConfigureAwait(false);
            await Page.RouteAsync("**/api", route => route.FulfillAsync(HttpStatusCode.OK, body: "ok"))
                .ConfigureAwait(false);
            await Page.EvaluateAsync("async () => fetch(new URL('/api', window.location.href))")
                .ConfigureAwait(false);
            await Page.CloseAsync(new() { RunBeforeUnload = true }).ConfigureAwait(false);
            await Page.EvaluateAsync("async () => fetch(new URL('/api', window.location.href))")
                .ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private async Task<IBrowserContext> NewContextOrRecycleAsync()
        {
            Task<IBrowserContext> create = _browser.NewContextAsync();
            Task finished = await Task.WhenAny(create, Task.Delay(5000)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, create))
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                return await _browser.NewContextAsync().ConfigureAwait(false);
            }

            return await create.ConfigureAwait(false);
        }

        private async Task RecycleBrowserAsync()
        {
            IBrowser previous = _browser;
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (previous != null)
            {
                await DisposeQuietlyAsync(previous).ConfigureAwait(false);
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
