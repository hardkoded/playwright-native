/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-network-idle.spec.ts</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageNetworkIdleTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static void InstallSseRoute()
        {
            Server.SetRoute("/sse", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/event-stream";
                http.Response.Headers["Cache-Control"] = "no-cache";
                http.Response.Headers["Connection"] = "keep-alive";
                await http.Response.WriteAsync("data: hello\n\n").ConfigureAwait(false);
                await http.Response.Body.FlushAsync().ConfigureAwait(false);
                await Task.Delay(-1).ConfigureAwait(false);
            });
        }

        private static async Task NetworkIdleTestAsync(IFrame frame, Func<Task<IResponse>> action, bool isSetContent = false)
        {
            Server.Reset();
            IPage page = frame.Page;

            Task WaitForFetchAsync(string suffix)
            {
                return Task.WhenAll(
                    Server.WaitForRequest(suffix),
                    page.WaitForRequestAsync(request => request.Url.Contains(suffix, StringComparison.Ordinal)));
            }

            TaskCompletionSource<bool> releaseA = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseB = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/fetch-request-a.js", http => HoldThenNotFoundAsync(http, releaseA));
            Task firstFetchResourceRequested = WaitForFetchAsync("/fetch-request-a.js");
            Server.SetRoute("/fetch-request-b.js", http => HoldThenNotFoundAsync(http, releaseB));
            Task secondFetchResourceRequested = WaitForFetchAsync("/fetch-request-b.js");

            Task waitForLoadPromise = isSetContent
                ? Task.CompletedTask
                : frame.WaitForNavigationAsync(new() { WaitUntil = WaitUntilState.Load });

            Task<IResponse> actionPromise = action();
            bool actionFinished = false;
            _ = actionPromise.ContinueWith(
                _ =>
                {
                    actionFinished = true;
                    return Task.CompletedTask;
                },
                TaskScheduler.Default);

            await waitForLoadPromise.ConfigureAwait(false);
            Assert.That(actionFinished, Is.False);

            await firstFetchResourceRequested.ConfigureAwait(false);
            Assert.That(actionFinished, Is.False);

            await page.EvaluateAsync("(() => window['fetchSecond']())()").ConfigureAwait(false);
            releaseA.TrySetResult(true);

            await secondFetchResourceRequested.ConfigureAwait(false);
            Assert.That(actionFinished, Is.False);

            Stopwatch idleWatch = Stopwatch.StartNew();
            releaseB.TrySetResult(true);

            IResponse response = await actionPromise.ConfigureAwait(false);
            Assert.That(idleWatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(500));
            if (!isSetContent)
            {
                Assert.That(response.Ok, Is.True);
            }
        }

        private static async Task HoldThenNotFoundAsync(HttpContext http, TaskCompletionSource<bool> release)
        {
            await release.Task.ConfigureAwait(false);
            http.Response.StatusCode = 404;
            await http.Response.WriteAsync("File not found").ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should navigate to empty page with networkidle")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNavigateToEmptyPageWithNetworkidle()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage, waitUntil: WaitUntilState.NetworkIdle).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle to succeed navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleToSucceedNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await NetworkIdleTestAsync(
                page.MainFrame,
                () => page.GoToAsync(TestConstants.ServerUrl + "/networkidle.html", waitUntil: WaitUntilState.NetworkIdle)).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle to succeed navigation with request from previous navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleToSucceedNavigationWithRequestFromPreviousNavigation()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/foo.js", _ => Task.Delay(-1));
            await page.SetContentAsync("<script>fetch('foo.js');</script>").ConfigureAwait(false);
            await NetworkIdleTestAsync(
                page.MainFrame,
                () => page.GoToAsync(TestConstants.ServerUrl + "/networkidle.html", waitUntil: WaitUntilState.NetworkIdle)).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle in waitForNavigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleInWaitForNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await NetworkIdleTestAsync(
                page.MainFrame,
                () =>
                {
                    Task<IResponse> promise = page.WaitForNavigationAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
                    _ = page.GoToAsync(TestConstants.ServerUrl + "/networkidle.html");
                    return promise;
                }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle in setContent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleInSetContent()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await NetworkIdleTestAsync(
                page.MainFrame,
                async () =>
                {
                    await page.SetContentAsync("<script src='networkidle.js'></script>", new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
                    return null;
                },
                isSetContent: true).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle in setContent with request from previous navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleInSetContentWithRequestFromPreviousNavigation()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/foo.js", _ => Task.Delay(-1));
            await page.SetContentAsync("<script>fetch('foo.js');</script>").ConfigureAwait(false);
            await NetworkIdleTestAsync(
                page.MainFrame,
                async () =>
                {
                    await page.SetContentAsync("<script src='networkidle.js'></script>", new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
                    return null;
                },
                isSetContent: true).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle when navigating iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleWhenNavigatingIframe()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/frames/one-frame.html").ConfigureAwait(false);
            List<IFrame> children = new List<IFrame>(page.MainFrame.ChildFrames);
            IFrame frame = children[0];
            await NetworkIdleTestAsync(
                frame,
                () => frame.GoToAsync(TestConstants.ServerUrl + "/networkidle.html", waitUntil: WaitUntilState.NetworkIdle)).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle in setContent from the child frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleInSetContentFromTheChildFrame()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await NetworkIdleTestAsync(
                page.MainFrame,
                async () =>
                {
                    await page.SetContentAsync("<iframe src='networkidle.html'></iframe>", new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
                    return null;
                },
                isSetContent: true).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle from the child frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleFromTheChildFrame()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await NetworkIdleTestAsync(
                page.MainFrame,
                () => page.GoToAsync(TestConstants.ServerUrl + "/networkidle-frame.html", waitUntil: WaitUntilState.NetworkIdle)).ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle from the popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleFromThePopup()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button id=box1 onclick=""window.open('./popup/popup.html')"">Button1</button>
    <button id=box2 onclick=""window.open('./popup/popup.html')"">Button2</button>
    <button id=box3 onclick=""window.open('./popup/popup.html')"">Button3</button>
    <button id=box4 onclick=""window.open('./popup/popup.html')"">Button4</button>
    <button id=box5 onclick=""window.open('./popup/popup.html')"">Button5</button>
  ").ConfigureAwait(false);

            for (int i = 1; i < 6; i++)
            {
                Task<IPage> popupTask = page.WaitForPopupAsync();
                await page.ClickAsync("#box" + i).ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                await popup.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should wait for networkidle when iframe attaches and detaches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForNetworkidleWhenIframeAttachesAndDetaches()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Server.SetRoute("/empty.html", _ => Task.Delay(-1));
            bool done = false;
            Task promise = page.SetContentAsync($@"
    <body>
      <script>
        const iframe = document.createElement('iframe');
        iframe.src = {System.Text.Json.JsonSerializer.Serialize(TestConstants.EmptyPage)};
        document.body.appendChild(iframe);
      </script>
    </body>
  ", new() { WaitUntil = WaitUntilState.NetworkIdle }).ContinueWith(
                _ =>
                {
                    done = true;
                    return Task.CompletedTask;
                },
                TaskScheduler.Default);
            await Task.Delay(600).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await page.EvaluateAsync("(() => document.querySelector('iframe').remove())()").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should work after repeated navigations in the same page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkAfterRepeatedNavigationsInTheSamePage()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            int requestCount = 0;
            await page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { ContentType = "text/html", Body = @"
        <script>
          fetch('http://localhost:8000/sample').then(res => console.log(res.json()))
        </script>" })).ConfigureAwait(false);

            await page.RouteAsync("**/sample", route =>
            {
                requestCount++;
                return route.FulfillAsync(new() { ContentType = "application/json", Body = "{\"content\":\"sample\"}" });
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage, waitUntil: WaitUntilState.NetworkIdle).ConfigureAwait(false);
            Assert.That(requestCount, Is.EqualTo(1));
            await page.GoToAsync(TestConstants.EmptyPage, waitUntil: WaitUntilState.NetworkIdle).ConfigureAwait(false);
            Assert.That(requestCount, Is.EqualTo(2));
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should not wait for an open EventSource connection")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotWaitForAnOpenEventSourceConnection()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            InstallSseRoute();
            Server.SetRoute("/sse-page.html", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<script>new EventSource('/sse');</script>").ConfigureAwait(false);
            });

            IResponse response = await page.GoToAsync(TestConstants.ServerUrl + "/sse-page.html", waitUntil: WaitUntilState.NetworkIdle).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-network-idle.spec.ts", "should not wait for an open EventSource connection in setContent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotWaitForAnOpenEventSourceConnectionInSetContent()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            InstallSseRoute();
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"<script>
    window.__sseOpened = new Promise(resolve => {
      const es = new EventSource('/sse');
      es.onmessage = () => resolve();
    });
  </script>", new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
            await page.EvaluateAsync("window['__sseOpened']").ConfigureAwait(false);
        }
    }
}
