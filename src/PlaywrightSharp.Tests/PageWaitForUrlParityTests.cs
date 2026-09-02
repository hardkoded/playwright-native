/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-wait-for-url.spec.ts</c> parity for
    /// <see cref="IPage.WaitForURLAsync(string, float?, WaitUntilState)"/> and
    /// <see cref="IFrame.WaitForURLAsync(string, float?, WaitUntilState)"/>.
    /// Do not edit leftover <c>WaitForUrlTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageWaitForUrlParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19822;
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
            if (_browser == null)
            {
                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
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
            Server?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        private IPage Page => _page;

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.EvaluateAsync<object>("url => window.location.href = url", Prefix + "/grid.html").ConfigureAwait(false);
            await Page.WaitForURLAsync("**/grid.html").ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectTimeout()
        {
            EnsureServer();
            Task waitTask = Page.WaitForURLAsync("**/frame.html", new() { Timeout = 2500 });
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            TimeoutException error = Assert.CatchAsync<TimeoutException>(async () => await waitTask.ConfigureAwait(false));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("page.waitForURL: Timeout 2500ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with both domcontentloaded and load")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBothDomcontentloadedAndLoad()
        {
            EnsureServer();
            TaskCompletionSource<bool> releaseCss = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task waitForCss = Server.WaitForRequest("/one-style.css");
            Server.SetRoute("/one-style.css", async http =>
            {
                await releaseCss.Task.ConfigureAwait(false);
                http.Response.StatusCode = 404;
                await http.Response.WriteAsync("Not found").ConfigureAwait(false);
            });

            Task<IResponse> navigationPromise = Page.GoToAsync(Prefix + "/one-style.html");
            Task domContentLoadedPromise = Page.WaitForURLAsync("**/one-style.html", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            bool bothFired = false;
            Task bothFiredPromise = Task.WhenAll(
                Page.WaitForURLAsync("**/one-style.html", new() { WaitUntil = WaitUntilState.Load }),
                domContentLoadedPromise).ContinueWith(
                _ =>
                {
                    bothFired = true;
                },
                TaskScheduler.Default);

            await waitForCss.ConfigureAwait(false);
            await domContentLoadedPromise.ConfigureAwait(false);
            Assert.That(bothFired, Is.False);
            releaseCss.TrySetResult(true);
            await bothFiredPromise.ConfigureAwait(false);
            await navigationPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with commit")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCommit()
        {
            EnsureServer();
            Server.SetRoute("/script.js", _ => Task.Delay(Timeout.Infinite));
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<title>Hello</title><script src=\"script.js\"></script>").ConfigureAwait(false);
            });

            _ = Page.GoToAsync(EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            await Page.WaitForURLAsync("**/empty.html", new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            Assert.That(await Page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Hello"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with commit and about:blank")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCommitAndAboutBlank()
        {
            await Page.WaitForURLAsync("about:blank", new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with clicking on anchor links")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithClickingOnAnchorLinks()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync("<a href='#foobar'>foobar</a>").ConfigureAwait(false);
            await Page.ClickAsync("a").ConfigureAwait(false);
            await Page.WaitForURLAsync("**/*#foobar").ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with history.pushState()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithHistoryPushState()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync(@"
    <a onclick='javascript:pushState()'>SPA</a>
    <script>
      function pushState() { history.pushState({}, '', 'wow.html') }
    </script>
  ").ConfigureAwait(false);
            await Page.ClickAsync("a").ConfigureAwait(false);
            await Page.WaitForURLAsync("**/wow.html").ConfigureAwait(false);
            Assert.That(Page.Url, Is.EqualTo(Prefix + "/wow.html"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with history.replaceState()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithHistoryReplaceState()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync(@"
    <a onclick='javascript:replaceState()'>SPA</a>
    <script>
      function replaceState() { history.replaceState({}, '', '/replaced.html') }
    </script>
  ").ConfigureAwait(false);
            await Page.ClickAsync("a").ConfigureAwait(false);
            await Page.WaitForURLAsync("**/replaced.html").ConfigureAwait(false);
            Assert.That(Page.Url, Is.EqualTo(Prefix + "/replaced.html"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with DOM history.back()/history.forward()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithDomHistoryBackHistoryForward()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync(@"
    <a id=back onclick='javascript:goBack()'>back</a>
    <a id=forward onclick='javascript:goForward()'>forward</a>
    <script>
      function goBack() { history.back(); }
      function goForward() { history.forward(); }
      history.pushState({}, '', '/first.html');
      history.pushState({}, '', '/second.html');
    </script>
  ").ConfigureAwait(false);
            Assert.That(Page.Url, Is.EqualTo(Prefix + "/second.html"));

            await Page.ClickAsync("a#back").ConfigureAwait(false);
            await Page.WaitForURLAsync("**/first.html").ConfigureAwait(false);
            Assert.That(Page.Url, Is.EqualTo(Prefix + "/first.html"));

            await Page.ClickAsync("a#forward").ConfigureAwait(false);
            await Page.WaitForURLAsync("**/second.html").ConfigureAwait(false);
            Assert.That(Page.Url, Is.EqualTo(Prefix + "/second.html"));
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work with url match for same document navigations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithUrlMatchForSameDocumentNavigations()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            bool resolved = false;
            Task waitPromise = Page.WaitForURLAsync(new Regex("third\\.html")).ContinueWith(
                _ =>
                {
                    resolved = true;
                },
                TaskScheduler.Default);
            Assert.That(resolved, Is.False);
            await Page.EvaluateAsync("() => { history.pushState({}, '', '/first.html'); }").ConfigureAwait(false);
            Assert.That(resolved, Is.False);
            await Page.EvaluateAsync("() => { history.pushState({}, '', '/second.html'); }").ConfigureAwait(false);
            Assert.That(resolved, Is.False);
            await Page.EvaluateAsync("() => { history.pushState({}, '', '/third.html'); }").ConfigureAwait(false);
            await waitPromise.ConfigureAwait(false);
            Assert.That(resolved, Is.True);
        }

        [PlaywrightTest("page-wait-for-url.spec.ts", "should work on frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkOnFrame()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IFrame frame = FrameAt(1);
            await frame.EvaluateAsync<object>("url => window.location.href = url", Prefix + "/grid.html").ConfigureAwait(false);
            await frame.WaitForURLAsync("**/grid.html").ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private IFrame FrameAt(int index)
        {
            System.Collections.Generic.List<IFrame> frames = new System.Collections.Generic.List<IFrame>(Page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(index));
            return frames[index];
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
