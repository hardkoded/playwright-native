/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
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
    /// Official <c>page-wait-for-navigation.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageWaitForNavigationParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static readonly string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19669;
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
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
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
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
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
        }

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        private static IFrame FrameAt(IPage page, int index)
        {
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(index));
            return frames[index];
        }

        private static bool HasQueryFooBar(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            string query = uri.Query;
            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                query = query.Substring(1);
            }

            foreach (string part in query.Split('&'))
            {
                string[] pair = part.Split(new[] { '=' }, 2);
                if (pair.Length == 2
                    && string.Equals(Uri.UnescapeDataString(pair[0].Replace('+', ' ')), "foo", StringComparison.Ordinal)
                    && string.Equals(Uri.UnescapeDataString(pair[1].Replace('+', ' ')), "bar", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertSslError(string message)
        {
            if (TestConstants.IsChromium)
            {
                Assert.That(message, Does.Contain("net::ERR_CERT_AUTHORITY_INVALID"));
                return;
            }

            if (TestConstants.IsWebKit)
            {
                if (TestConstants.IsWindows)
                {
                    Assert.That(message, Does.Contain("SSL peer certificate or SSH remote key was not OK"));
                    return;
                }

                if (TestConstants.IsMacOSX)
                {
                    Assert.That(message, Does.Contain("The certificate for this server is invalid"));
                    return;
                }

                Assert.That(message, Does.Contain("Unacceptable TLS certificate"));
                return;
            }

            Assert.That(message, Does.Contain("SSL_ERROR_UNKNOWN"));
        }

        private static async Task AssignLocationAsync(IPage page, string url)
        {
            await page.EvaluateAsync<object>(
                "(() => { window.location.href = " + JsonSerializer.Serialize(url) + "; })()").ConfigureAwait(false);
        }

        private static async Task AssignLocationAsync(IFrame frame, string url)
        {
            await frame.EvaluateAsync<object>(
                "(() => { window.location.href = " + JsonSerializer.Serialize(url) + "; })()").ConfigureAwait(false);
        }

        private static async Task RemoveFrameAfterCssAsync(IPage page)
        {
            await Server.WaitForRequest("/one-style.css").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("iframe", "frame => { setTimeout(() => frame.remove(), 0); }").ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync();
            Task evalTask = AssignLocationAsync(page, Prefix + "/grid.html");
            IResponse response = await waitTask.ConfigureAwait(false);
            await evalTask.ConfigureAwait(false);

            Assert.That(response.Ok, Is.True);
            Assert.That(response.Url, Does.Contain("grid.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectTimeout()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> promise = page.WaitForNavigationAsync("**/frame.html", timeout: 5000);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(async () => await promise.ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("page.waitForNavigation: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("waiting for navigation to \"**/frame.html\" until \"load\""));
            Assert.That(error.Message, Does.Contain("navigated to \"" + EmptyPage + "\""));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with both domcontentloaded and load")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithBothDomcontentloadedAndLoad()
        {
            EnsureServer();
            TaskCompletionSource<bool> releaseCss = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task waitForCss = Server.WaitForRequest("/one-style.css");
            Server.SetRoute("/one-style.css", async http =>
            {
                await releaseCss.Task.ConfigureAwait(false);
                http.Response.ContentType = "text/css";
                await http.Response.WriteAsync("body { background: green; }").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> navigationPromise = page.GoToAsync(Prefix + "/one-style.html");
            Task<IResponse> domContentLoadedPromise = page.WaitForNavigationAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            bool bothFired = false;
            Task bothFiredPromise = Task.WhenAll(
                page.WaitForNavigationAsync(new() { WaitUntil = WaitUntilState.Load }),
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

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with commit")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCommit()
        {
            EnsureServer();
            Server.SetRoute("/script.js", _ => Task.Delay(Timeout.Infinite));
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<title>Hello</title><script src=\"script.js\"></script>").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            _ = page.GoToAsync(EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            await page.WaitForNavigationAsync(new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Hello"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with clicking on anchor links")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithClickingOnAnchorLinks()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href='#foobar'>foobar</a>").ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync();
            Task clickTask = page.ClickAsync("a");
            IResponse response = await waitTask.ConfigureAwait(false);
            await clickTask.ConfigureAwait(false);

            Assert.That(response, Is.Null);
            Assert.That(page.Url, Is.EqualTo(EmptyPage + "#foobar"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with clicking on links which do not commit navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithClickingOnLinksWhichDoNotCommitNavigation()
        {
            EnsureServer();
            if (TestServerSetup.HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href='" + HttpsEmptyPage + "'>foobar</a>").ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync();
            Task clickTask = page.ClickAsync("a");
            Exception error = await waitTask.ContinueWith(
                t => t.Exception?.GetBaseException(),
                TaskScheduler.Default).ConfigureAwait(false);
            try
            {
                await clickTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error ??= ex;
            }

            if (error == null)
            {
                error = Assert.CatchAsync(async () => await waitTask.ConfigureAwait(false));
            }

            Assert.That(error, Is.Not.Null);
            AssertSslError(error.Message);
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with history.pushState()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithHistoryPushState()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <a onclick='javascript:pushState()'>SPA</a>
    <script>
      function pushState() { history.pushState({}, '', 'wow.html') }
    </script>
  ").ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync();
            Task clickTask = page.ClickAsync("a");
            IResponse response = await waitTask.ConfigureAwait(false);
            await clickTask.ConfigureAwait(false);

            Assert.That(response, Is.Null);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/wow.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with history.replaceState()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithHistoryReplaceState()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <a onclick='javascript:replaceState()'>SPA</a>
    <script>
      function replaceState() { history.replaceState({}, '', '/replaced.html') }
    </script>
  ").ConfigureAwait(false);

            Task<IResponse> waitTask = page.WaitForNavigationAsync();
            Task clickTask = page.ClickAsync("a");
            IResponse response = await waitTask.ConfigureAwait(false);
            await clickTask.ConfigureAwait(false);

            Assert.That(response, Is.Null);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/replaced.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with DOM history.back()/history.forward()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDOMHistoryBackHistoryForward()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <a id=back onclick='javascript:goBack()'>back</a>
    <a id=forward onclick='javascript:goForward()'>forward</a>
    <script>
      function goBack() { history.back(); }
      function goForward() { history.forward(); }
      history.pushState({}, '', '/first.html');
      history.pushState({}, '', '/second.html');
    </script>
  ").ConfigureAwait(false);

            Assert.That(page.Url, Is.EqualTo(Prefix + "/second.html"));

            Task<IResponse> backWait = page.WaitForNavigationAsync();
            Task backClick = page.ClickAsync("a#back");
            IResponse backResponse = await backWait.ConfigureAwait(false);
            await backClick.ConfigureAwait(false);
            Assert.That(backResponse, Is.Null);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/first.html"));

            Task<IResponse> forwardWait = page.WaitForNavigationAsync();
            Task forwardClick = page.ClickAsync("a#forward");
            IResponse forwardResponse = await forwardWait.ConfigureAwait(false);
            await forwardClick.ConfigureAwait(false);
            Assert.That(forwardResponse, Is.Null);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/second.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work when subframe issues window.stop()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenSubframeIssuesWindowStop()
        {
            EnsureServer();
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit issues load event in some cases, but not always");
            }

            Server.SetRoute("/frames/style.css", _ => Task.Delay(Timeout.Infinite));

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IFrame> attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnAttached(object sender, IFrame frame) => attached.TrySetResult(frame);
            page.FrameAttached += OnAttached;

            bool done = false;
            Task gotoTask = page.GoToAsync(Prefix + "/frames/one-frame.html").ContinueWith(
                t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        done = true;
                    }
                },
                TaskScheduler.Default);

            IFrame frame;
            try
            {
                frame = await attached.Task.ConfigureAwait(false);
            }
            finally
            {
                page.FrameAttached -= OnAttached;
            }

            TaskCompletionSource<bool> navigated = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnNavigated(object sender, IFrame navigatedFrame)
            {
                if (ReferenceEquals(navigatedFrame, frame))
                {
                    navigated.TrySetResult(true);
                }
            }

            page.FrameNavigated += OnNavigated;
            try
            {
                await navigated.Task.ConfigureAwait(false);
            }
            finally
            {
                page.FrameNavigated -= OnNavigated;
            }

            await frame.EvaluateAsync<object>("(() => { window.stop(); })()").ConfigureAwait(false);
            await gotoTask.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with url match")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithUrlMatch()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response1 = null;
            Task response1Promise = page.WaitForNavigationAsync(new Regex("one-style\\.html")).ContinueWith(
                t =>
                {
                    response1 = t.Result;
                },
                TaskScheduler.Default);
            IResponse response2 = null;
            Task response2Promise = page.WaitForNavigationAsync(new Regex("/frame.html")).ContinueWith(
                t =>
                {
                    response2 = t.Result;
                },
                TaskScheduler.Default);
            IResponse response3 = null;
            Task response3Promise = page.WaitForNavigationAsync(HasQueryFooBar).ContinueWith(
                t =>
                {
                    response3 = t.Result;
                },
                TaskScheduler.Default);

            Assert.That(response1, Is.Null);
            Assert.That(response2, Is.Null);
            Assert.That(response3, Is.Null);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1, Is.Null);
            Assert.That(response2, Is.Null);
            Assert.That(response3, Is.Null);
            await page.GoToAsync(Prefix + "/frame.html").ConfigureAwait(false);
            Assert.That(response1, Is.Null);
            await response2Promise.ConfigureAwait(false);
            Assert.That(response2, Is.Not.Null);
            Assert.That(response3, Is.Null);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            await response1Promise.ConfigureAwait(false);
            Assert.That(response1, Is.Not.Null);
            Assert.That(response2, Is.Not.Null);
            Assert.That(response3, Is.Null);
            await page.GoToAsync(Prefix + "/frame.html?foo=bar").ConfigureAwait(false);
            await response3Promise.ConfigureAwait(false);
            Assert.That(response1, Is.Not.Null);
            Assert.That(response2, Is.Not.Null);
            Assert.That(response3, Is.Not.Null);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response1.Url, Is.EqualTo(Prefix + "/one-style.html"));
            Assert.That(response2.Url, Is.EqualTo(Prefix + "/frame.html"));
            Assert.That(response3.Url, Is.EqualTo(Prefix + "/frame.html?foo=bar"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work with url match for same document navigations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithUrlMatchForSameDocumentNavigations()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            bool resolved = false;
            Task waitPromise = page.WaitForNavigationAsync(new Regex("third\\.html")).ContinueWith(
                _ =>
                {
                    resolved = true;
                },
                TaskScheduler.Default);
            Assert.That(resolved, Is.False);

            await page.EvaluateAsync<object>("(() => { history.pushState({}, '', '/first.html'); })()").ConfigureAwait(false);
            Assert.That(resolved, Is.False);
            await page.EvaluateAsync<object>("(() => { history.pushState({}, '', '/second.html'); })()").ConfigureAwait(false);
            Assert.That(resolved, Is.False);
            await page.EvaluateAsync<object>("(() => { history.pushState({}, '', '/third.html'); })()").ConfigureAwait(false);
            await waitPromise.ConfigureAwait(false);
            Assert.That(resolved, Is.True);
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work for cross-process navigations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForCrossProcessNavigations()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IResponse> waitPromise = page.WaitForNavigationAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            string url = CrossProcessPrefix + "/empty.html";
            Task<IResponse> gotoPromise = page.GoToAsync(url);
            IResponse response = await waitPromise.ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(url));
            Assert.That(page.Url, Is.EqualTo(url));
            Assert.That(await page.EvaluateAsync<string>("document.location.href").ConfigureAwait(false), Is.EqualTo(url));
            await gotoPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should work on frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkOnFrame()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IFrame frame = FrameAt(page, 1);

            Task<IResponse> waitTask = frame.WaitForNavigationAsync();
            Task evalTask = AssignLocationAsync(frame, Prefix + "/grid.html");
            IResponse response = await waitTask.ConfigureAwait(false);
            await evalTask.ConfigureAwait(false);

            Assert.That(response.Ok, Is.True);
            Assert.That(response.Url, Does.Contain("grid.html"));
            Assert.That(response.Frame, Is.SameAs(frame));
            Assert.That(page.Url, Does.Contain("/frames/one-frame.html"));
        }

        [PlaywrightTest("page-wait-for-navigation.spec.ts", "should fail when frame detaches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenFrameDetaches()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IFrame frame = FrameAt(page, 1);

            Server.SetRoute("/empty.html", _ => Task.Delay(Timeout.Infinite));
            Server.SetRoute("/one-style.css", _ => Task.Delay(Timeout.Infinite));

            Task<IResponse> waitTask = frame.WaitForNavigationAsync();
            Task navEval = page.EvalOnSelectorAsync<object>("iframe", "frame => { frame.contentWindow.location.href = '/one-style.html'; }");
            Task removeAfterCss = RemoveFrameAfterCssAsync(page);
            Exception error = Assert.CatchAsync(async () => await waitTask.ConfigureAwait(false));
            try
            {
                await navEval.ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
            }

            try
            {
                await removeAfterCss.ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("waiting for navigation until \"load\""));
            Assert.That(error.Message, Does.Contain("frame was detached"));
        }
    }
}
