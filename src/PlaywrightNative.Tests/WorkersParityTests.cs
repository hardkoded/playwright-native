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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>workers.spec.ts</c> parity for dedicated workers.
    /// Skipped (Node-only internals):
    /// <c>should have timestamp on worker console messages</c> (<c>isAndroid</c>,
    /// <c>channel === 'webkit-wsl'</c> clock skew).
    /// Chromium <c>browserMajorVersion</c> gates (&lt; 143 / 149 / 151) are not
    /// applied here — this Chrome is modern.
    /// Firefox-only version skips are omitted (Firefox is not a gate).
    /// </summary>
    [TestFixture]
    public class WorkersParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19847;
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

        [PlaywrightTest("workers.spec.ts", "Page.workers")]
        [PlaywrightTest("workers.spec.ts", "Page.workers @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task PageWorkers()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.GoToAsync(Prefix + "/worker/worker.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            Assert.That(worker.Url, Does.Contain("worker.js"));

            string result = await worker.EvaluateAsync<string>("(() => self['workerFunction']())()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("worker function result"));

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Workers, Is.Empty);
        }

        [PlaywrightTest("workers.spec.ts", "should emit created and destroyed events")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitCreatedAndDestroyedEvents()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> workerCreated = page.WaitForEventAsync(PageEvent.Worker);
            IJSHandle workerObj = await page.EvaluateHandleAsync(
                "(() => new Worker(URL.createObjectURL(new Blob(['1'], { type: 'application/javascript' }))))()").ConfigureAwait(false);
            IWorker worker = await workerCreated.ConfigureAwait(false);
            IJSHandle workerThisObj = await worker.EvaluateHandleAsync("this").ConfigureAwait(false);
            Task<IWorker> workerDestroyed = worker.WaitForCloseAsync();
            await page.EvaluateAsync<object>("workerObj => workerObj.terminate()", workerObj).ConfigureAwait(false);
            IWorker closed = await workerDestroyed.ConfigureAwait(false);
            Assert.That(closed, Is.SameAs(worker));

            Exception error = null;
            try
            {
                await workerThisObj.GetPropertyAsync("self").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("jsHandle.getProperty"));
            Assert.That(error.Message, Does.Contain("closed").IgnoreCase);
        }

        [PlaywrightTest("workers.spec.ts", "should report console logs")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportConsoleLogs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IConsoleMessage> consoleTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>(CreateBlobWorkerScript("console.log(1)")).ConfigureAwait(false);
            IConsoleMessage message = await consoleTask.ConfigureAwait(false);
            Assert.That(message.Text, Is.EqualTo("1"));
            Assert.That(page.Url, Does.Not.Contain("blob"));
        }

        [PlaywrightTest("workers.spec.ts", "should have timestamp on worker console messages")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveTimestampOnWorkerConsoleMessages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            double before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1;
            Task<IConsoleMessage> consoleTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>(CreateBlobWorkerScript("console.log(\"ts\")")).ConfigureAwait(false);
            IConsoleMessage message = await consoleTask.ConfigureAwait(false);
            double after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1;
            Assert.That(message.Text, Is.EqualTo("ts"));
            Assert.That(message.Timestamp, Is.GreaterThanOrEqualTo(before));
            Assert.That(message.Timestamp, Is.LessThanOrEqualTo(after));
        }

        [PlaywrightTest("workers.spec.ts", "should not report console logs from workers twice")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotReportConsoleLogsFromWorkersTwice()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            List<string> messages = new List<string>();
            page.Console += (_, msg) => messages.Add(msg.Text);
            Task<IConsoleMessage> first = page.WaitForConsoleMessageAsync(msg => msg.Text == "1");
            Task<IConsoleMessage> second = page.WaitForConsoleMessageAsync(msg => msg.Text == "2");
            await Task.WhenAll(
                page.EvaluateAsync<object>(CreateBlobWorkerScript("console.log(1); console.log(2);")),
                first,
                second).ConfigureAwait(false);
            Assert.That(messages, Is.EqualTo(new[] { "1", "2" }));
            Assert.That(page.Url, Does.Not.Contain("blob"));
        }

        [PlaywrightTest("workers.spec.ts", "should have JSHandles for console logs")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveJSHandlesForConsoleLogs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            TaskCompletionSource<IConsoleMessage> logTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Console += (_, msg) => logTcs.TrySetResult(msg);
            await page.EvaluateAsync<object>(CreateBlobWorkerScript("console.log(1,2,3,this)")).ConfigureAwait(false);
            IConsoleMessage log = await logTcs.Task.ConfigureAwait(false);
            Assert.That(log.Text, Is.EqualTo("1 2 3 DedicatedWorkerGlobalScope"));
            Assert.That(log.Args, Has.Exactly(4).Items);
            IJSHandle origin = await GetArg(log, 3).GetPropertyAsync("origin").ConfigureAwait(false);
            Assert.That(await origin.JsonValueAsync<string>().ConfigureAwait(false), Is.EqualTo("null"));
        }

        [PlaywrightTest("workers.spec.ts", "should evaluate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEvaluate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> workerCreated = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(CreateBlobWorkerScript("console.log(1)")).ConfigureAwait(false);
            IWorker worker = await workerCreated.ConfigureAwait(false);
            Assert.That(await worker.EvaluateAsync<int>("1+1").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("workers.spec.ts", "should report console event on the worker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportConsoleEventOnTheWorker()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(
                "(() => { window.worker = new Worker(URL.createObjectURL(new Blob(['42'], { type: 'application/javascript' }))); })()").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);

            Task<IConsoleMessage> workerConsole = worker.WaitForConsoleMessageAsync();
            Task<IConsoleMessage> pageConsole = page.WaitForConsoleMessageAsync();
            Task<IConsoleMessage> contextConsole = context.WaitForConsoleMessageAsync();
            await worker.EvaluateAsync<object>("(() => { console.log('hello from worker'); })()").ConfigureAwait(false);
            IConsoleMessage message1 = await workerConsole.ConfigureAwait(false);
            IConsoleMessage message2 = await pageConsole.ConfigureAwait(false);
            IConsoleMessage message3 = await contextConsole.ConfigureAwait(false);
            Assert.That(message1.Text, Is.EqualTo("hello from worker"));
            Assert.That(message1, Is.SameAs(message2));
            Assert.That(message1, Is.SameAs(message3));
        }

        [PlaywrightTest("workers.spec.ts", "should report console event on the worker when not listening on page or context")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportConsoleEventOnTheWorkerWhenNotListeningOnPageOrContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(
                "(() => { window.worker = new Worker(URL.createObjectURL(new Blob(['42'], { type: 'application/javascript' }))); })()").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);

            Task<IConsoleMessage> workerConsole = worker.WaitForConsoleMessageAsync();
            await worker.EvaluateAsync<object>("(() => { console.log('hello from worker'); })()").ConfigureAwait(false);
            IConsoleMessage message = await workerConsole.ConfigureAwait(false);
            Assert.That(message.Text, Is.EqualTo("hello from worker"));
        }

        [PlaywrightTest("workers.spec.ts", "should report errors")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            TaskCompletionSource<PageErrorEventArgs> errorTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.PageError += (_, error) => errorTcs.TrySetResult(PageErrorText.Parse(error));
            await page.EvaluateAsync<object>(CreateBlobWorkerScript(
                "setTimeout(() => { console.log('hey'); throw new Error('this is my error'); })")).ConfigureAwait(false);
            PageErrorEventArgs errorLog = await errorTcs.Task.ConfigureAwait(false);
            Assert.That(errorLog.Message, Does.Contain("this is my error"));
        }

        [PlaywrightTest("workers.spec.ts", "should clear upon navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClearUponNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IWorker> workerCreated = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(CreateBlobWorkerScript("console.log(1)")).ConfigureAwait(false);
            IWorker worker = await workerCreated.ConfigureAwait(false);
            Assert.That(page.Workers, Has.Exactly(1).Items);
            bool destroyed = false;
            worker.Close += (_, _) => destroyed = true;
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            Assert.That(destroyed, Is.True);
            Assert.That(page.Workers, Is.Empty);
        }

        [PlaywrightTest("workers.spec.ts", "should clear upon cross-process navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClearUponCrossProcessNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IWorker> workerCreated = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(CreateBlobWorkerScript("console.log(1)")).ConfigureAwait(false);
            IWorker worker = await workerCreated.ConfigureAwait(false);
            Assert.That(page.Workers, Has.Exactly(1).Items);
            bool destroyed = false;
            worker.Close += (_, _) => destroyed = true;
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(destroyed, Is.True);
            Assert.That(page.Workers, Is.Empty);
        }

        [PlaywrightTest("workers.spec.ts", "should attribute network activity for worker inside iframe to the iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAttributeNetworkActivityForWorkerInsideIframeToTheIframe()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);

            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            Task<IFrame> frameTask = AttachFrameAsync(page, "frame1", Prefix + "/worker/worker.html");
            IWorker worker = await workerTask.ConfigureAwait(false);
            IFrame frame = await frameTask.ConfigureAwait(false);

            string url = Prefix + "/one-style.css";
            Task<IRequest> requestTask = page.WaitForRequestAsync(url);
            await worker.EvaluateAsync<object>(
                "url => fetch(url).then(response => response.text()).then(console.log)",
                url).ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(url));
            Assert.That(request.Frame, Is.SameAs(frame));
        }

        [PlaywrightTest("workers.spec.ts", "should report network activity")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportNetworkActivity()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.GoToAsync(Prefix + "/worker/worker.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            string url = Prefix + "/one-style.css";
            Task<IRequest> requestTask = page.WaitForRequestAsync(url);
            Task<IResponse> responseTask = page.WaitForResponseAsync(url);
            await worker.EvaluateAsync<object>(
                "url => fetch(url).then(response => response.text()).then(console.log)",
                url).ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(url));
            Assert.That(response.Request, Is.SameAs(request));
            Assert.That(response.Ok, Is.True);
            string expected = await File.ReadAllTextAsync(TestUtils.GetWebServerFile("one-style.css")).ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(expected));
        }

        [PlaywrightTest("workers.spec.ts", "should report network activity on worker creation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportNetworkActivityOnWorkerCreation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string url = Prefix + "/one-style.css";
            Task<IRequest> requestTask = page.WaitForRequestAsync(url);
            Task<IResponse> responseTask = page.WaitForResponseAsync(url);
            await page.EvaluateAsync<object>(
                @"url => new Worker(URL.createObjectURL(new Blob([`
    fetch(""${url}"").then(response => response.text()).then(console.log);
  `], { type: 'application/javascript' })))",
                url).ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(url));
            Assert.That(response.Request, Is.SameAs(request));
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("workers.spec.ts", "should report worker script as network request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportWorkerScriptAsNetworkRequest()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IRequest> request1Task = page.WaitForEventAsync(PageEvent.Request, r => r.Url.Contains("worker.js", StringComparison.Ordinal));
            Task<IRequest> request2Task = page.WaitForEventAsync(PageEvent.RequestFinished, r => r.Url.Contains("worker.js", StringComparison.Ordinal));
            await page.EvaluateAsync<object>("(() => { window.w = new Worker('/worker/worker.js'); })()").ConfigureAwait(false);
            IRequest request1 = await request1Task.ConfigureAwait(false);
            IRequest request2 = await request2Task.ConfigureAwait(false);
            Assert.That(request1.Url, Is.EqualTo(Prefix + "/worker/worker.js"));
            Assert.That(request1, Is.SameAs(request2));
            IResponse response = await request1.ResponseAsync().ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("console.log('hello from the worker');"));
        }

        [PlaywrightTest("workers.spec.ts", "should report worker script as network request after redirect")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportWorkerScriptAsNetworkRequestAfterRedirect()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Chromium does not report the redirect because it is not plumbed to the worker target");
            }

            EnsureServer();
            Server.Reset();
            Server.SetRedirect("/worker.js", "/worker2.js");
            Server.SetRoute("/worker2.js", async http =>
            {
                http.Response.ContentType = "text/javascript";
                await http.Response.WriteAsync("console.log('hello from the worker');").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.Request, r => r.Url.Contains("worker.js", StringComparison.Ordinal));
            Task<IConsoleMessage> consoleTask = page.WaitForConsoleMessageAsync(msg => msg.Text.Contains("hello from the worker", StringComparison.Ordinal));
            await page.EvaluateAsync<object>("(() => { window.w = new Worker('/worker.js'); })()").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            await consoleTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(Prefix + "/worker.js"));
            IRequest redirect = request.RedirectedTo;
            Assert.That(redirect, Is.Not.Null);
            Assert.That(redirect.Url, Is.EqualTo(Prefix + "/worker2.js"));
            IResponse response = await redirect.ResponseAsync().ConfigureAwait(false);
            string text = await response.TextAsync().ConfigureAwait(false);
            Assert.That(text, Does.Contain("console.log('hello from the worker');"));
        }

        [PlaywrightTest("workers.spec.ts", "should dispatch console messages when page has workers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDispatchConsoleMessagesWhenPageHasWorkers()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IWorker> idleWorker = page.WaitForEventAsync(PageEvent.Worker);
            await Task.WhenAll(
                idleWorker,
                page.EvaluateAsync<object>(CreateBlobWorkerScript("const x = 1;"))).ConfigureAwait(false);

            Task<IConsoleMessage> consoleTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>("(() => console.log('foo'))()").ConfigureAwait(false);
            IConsoleMessage message = await consoleTask.ConfigureAwait(false);
            Assert.That(message.Text, Is.EqualTo("foo"));
        }

        [PlaywrightTest("workers.spec.ts", "should report and intercept network from nested worker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportAndInterceptNetworkFromNestedWorker()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/27376");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/simple.json", route => route.FulfillAsync(new() { Json = new { foo = "not bar" } })).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string url = Prefix + "/simple.json";
            List<IWorker> workers = new List<IWorker>();
            List<string> messages = new List<string>();
            page.Worker += (_, worker) => workers.Add(worker);
            page.Console += (_, msg) => messages.Add(msg.Text);

            await page.EvaluateAsync<object>(
                @"url => new Worker(URL.createObjectURL(new Blob([`
    fetch(""${url}"").then(response => response.text()).then(t => console.log(t.trim()));
  `], { type: 'application/javascript' })))",
                url).ConfigureAwait(false);
            await WaitUntilAsync(() => workers.Count == 1).ConfigureAwait(false);

            await workers[0].EvaluateAsync<object>(
                @"url => new Worker(URL.createObjectURL(new Blob([`
    fetch(""${url}"").then(response => response.text()).then(t => console.log(t.trim()));
  `], { type: 'application/javascript' })))",
                url).ConfigureAwait(false);
            await WaitUntilAsync(() => workers.Count == 2).ConfigureAwait(false);
            await WaitUntilAsync(() => messages.Count >= 2).ConfigureAwait(false);
            Assert.That(messages, Is.EqualTo(new[] { "{\"foo\":\"not bar\"}", "{\"foo\":\"not bar\"}" }));
        }

        [PlaywrightTest("workers.spec.ts", "should support extra http headers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportExtraHttpHeaders()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string> { ["foo"] = "bar" }).ConfigureAwait(false);

            Task<IHeaderDictionary> request1Task = Server.WaitForRequest("/worker/worker.js", r => r.Headers);
            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.GoToAsync(Prefix + "/worker/worker.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            IHeaderDictionary request1 = await request1Task.ConfigureAwait(false);

            Task<IHeaderDictionary> request2Task = Server.WaitForRequest("/one-style.css", r => r.Headers);
            await worker.EvaluateAsync<object>("url => fetch(url)", Prefix + "/one-style.css").ConfigureAwait(false);
            IHeaderDictionary request2 = await request2Task.ConfigureAwait(false);
            Assert.That(request1["foo"].ToString(), Is.EqualTo("bar"));
            Assert.That(request2["foo"].ToString(), Is.EqualTo("bar"));
        }

        [PlaywrightTest("workers.spec.ts", "should support offline")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportOffline()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("flaky on all platforms");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            Task<IConsoleMessage> hello = page.WaitForConsoleMessageAsync(msg => msg.Text.Contains("hello from the worker", StringComparison.Ordinal));
            await page.GoToAsync(Prefix + "/worker/worker.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            await hello.ConfigureAwait(false);

            await context.SetOfflineAsync(true).ConfigureAwait(false);
            await WaitUntilAsync(async () => !await worker.EvaluateAsync<bool>("(() => navigator.onLine)()").ConfigureAwait(false)).ConfigureAwait(false);
            object fetchResult = await worker.EvaluateAsync<object>("(() => fetch('/one-style.css').catch(e => 'error'))()").ConfigureAwait(false);
            Assert.That(fetchResult?.ToString(), Is.EqualTo("error"));
            await context.SetOfflineAsync(false).ConfigureAwait(false);
            await WaitUntilAsync(async () => await worker.EvaluateAsync<bool>("(() => navigator.onLine)()").ConfigureAwait(false)).ConfigureAwait(false);
        }

        [PlaywrightTest("workers.spec.ts", "should resolve worker script allHeaders in main frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWorkerScriptAllHeadersInMainFrame()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string workerUrl = Prefix + "/worker/worker.js";
            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.RequestFinished, request => request.Url == workerUrl);
            await page.GoToAsync(Prefix + "/worker/worker.html").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            IResponse response = await request.ResponseAsync().ConfigureAwait(false);
            Dictionary<string, string> requestHeaders = await request.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(requestHeaders["host"], Is.Not.Null.And.Not.Empty);
            Dictionary<string, string> responseHeaders = await response.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(responseHeaders["content-type"], Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("workers.spec.ts", "should resolve worker script allHeaders in iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWorkerScriptAllHeadersInIframe()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string workerUrl = Prefix + "/worker/worker.js";
            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.RequestFinished, request => request.Url == workerUrl);
            await AttachFrameAsync(page, "frame1", Prefix + "/worker/worker.html").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            IResponse response = await request.ResponseAsync().ConfigureAwait(false);
            Dictionary<string, string> requestHeaders = await request.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(requestHeaders["host"], Is.Not.Null.And.Not.Empty);
            Dictionary<string, string> responseHeaders = await response.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(responseHeaders["content-type"], Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("workers.spec.ts", "should resolve worker script allHeaders in nested worker inside iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWorkerScriptAllHeadersInNestedWorkerInsideIframe()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("cannot evaluate in nested worker");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string url = Prefix + "/worker/worker.js";
            Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
            Task<IRequest> firstFinished = page.WaitForEventAsync(PageEvent.RequestFinished, request => request.Url == url);
            await AttachFrameAsync(page, "frame1", Prefix + "/worker/worker.html").ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            await firstFinished.ConfigureAwait(false);

            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.RequestFinished, request => request.Url == url);
            await worker.EvaluateAsync<object>("url => { self.w = new Worker(url); }", url).ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            IResponse response = await request.ResponseAsync().ConfigureAwait(false);
            Dictionary<string, string> headers = await response.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(headers["content-type"], Is.Not.Null.And.Not.Empty);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<bool> FixtureReachableAsync(string prefix)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2),
                };
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(prefix + "/empty.html").ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(async () => { const f = document.createElement('iframe'); f.name = " +
                nameJson +
                "; f.id = " +
                nameJson +
                "; f.src = " +
                urlJson +
                "; const done = new Promise(r => f.onload = r); document.body.appendChild(f); await done; })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named == null)
                {
                    foreach (IFrame frame in page.Frames)
                    {
                        if (!ReferenceEquals(frame, page.MainFrame))
                        {
                            named = frame;
                            break;
                        }
                    }
                }

                if (named != null)
                {
                    try
                    {
                        await named.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        private static string CreateBlobWorkerScript(string body)
        {
            return "(() => new Worker(URL.createObjectURL(new Blob([" +
                JsonSerializer.Serialize(body) +
                "], { type: 'application/javascript' }))))()";
        }

        private static IJSHandle GetArg(IConsoleMessage message, int index)
        {
            int i = 0;
            foreach (IJSHandle arg in message.Args)
            {
                if (i == index)
                {
                    return arg;
                }

                i++;
            }

            Assert.Fail("Console message has no argument at index " + index.ToString(CultureInfo.InvariantCulture));
            return null;
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Condition was not met.");
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Condition was not met.");
        }
    }
}
