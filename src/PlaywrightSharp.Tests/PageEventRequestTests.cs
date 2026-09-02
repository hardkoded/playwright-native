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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-event-request.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageEventRequestTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task WaitForServiceWorkerActivationAsync(IPage page)
        {
            await page.EvaluateAsync<object>(@"(async () => {
                if (navigator.serviceWorker.controller)
                    return;
                await window.activationPromise;
            })()").ConfigureAwait(false);
        }

        private static Task FetchUrlAsync(IPage page, string url)
        {
            string script = "(() => fetch(" + JsonSerializer.Serialize(url) + "))()";
            return page.EvaluateAsync<object>(script);
        }

        private static async Task AttachFrameAsync(IPage page, string name, string url)
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
        }

        private static async Task<string> FetchCorsAsync(IPage page, string url)
        {
            try
            {
                return await page.EvaluateAsync<string>(
                    @"(async (target) => {
                        const response = await fetch(target, {
                            method: 'POST',
                            body: '',
                            headers: {
                                'Content-Type': 'application/json',
                                'X-Custom-Header': 'test-value'
                            }
                        });
                        return await response.text();
                    })",
                    url).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void InstallCorsRoute(List<string> serverRequests)
        {
            Server.SetRoute("/cors", async http =>
            {
                serverRequests.Add(http.Request.Method + " " + http.Request.Path);
                if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    http.Response.StatusCode = 204;
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    http.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, OPTIONS";
                    http.Response.Headers["Access-Control-Allow-Headers"] = "*";
                    return;
                }

                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/plain";
                http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                await http.Response.WriteAsync("Hello there!").ConfigureAwait(false);
            });
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19380;
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

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
        }

        [PlaywrightTest("page-event-request.spec.ts", "should fire for navigation requests")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireForNavigationRequests()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(requests, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should fire for iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireForIframes()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            Assert.That(requests, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should fire for fetches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireForFetches()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => fetch('/empty.html'))()").ConfigureAwait(false);
            Assert.That(requests, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should fire for fetches with keepalive: true")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireForFetchesWithKeepaliveTrue()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => fetch('/empty.html', { keepalive: true }))()").ConfigureAwait(false);
            Assert.That(requests, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should report requests and responses handled by service worker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportRequestsAndResponsesHandledByServiceWorker()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/serviceworkers/fetchdummy/sw.html").ConfigureAwait(false);
            await WaitForServiceWorkerActivationAsync(page).ConfigureAwait(false);

            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.Request);
            Task<string> swTask = page.EvaluateAsync<string>("(() => window['fetchDummy']('foo'))()");
            await Task.WhenAll(requestTask, swTask).ConfigureAwait(false);

            IRequest request = await requestTask.ConfigureAwait(false);
            string swResponse = await swTask.ConfigureAwait(false);
            Assert.That(swResponse, Is.EqualTo("responseFromServiceWorker:foo"));
            Assert.That(request.Url, Is.EqualTo(Prefix + "/serviceworkers/fetchdummy/foo"));
            Assert.That(request.ServiceWorker(), Is.Null);
            IResponse response = await request.ResponseAsync().ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/serviceworkers/fetchdummy/foo"));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("responseFromServiceWorker:foo"));
            Assert.That(response.FromServiceWorker, Is.True);

            Task<IRequest> failedTask = page.WaitForEventAsync(PageEvent.RequestFailed);
            Task failEval = page.EvaluateAsync<object>("(() => window['fetchDummy']('error'))()")
                .ContinueWith(_ => { }, TaskScheduler.Default);
            await Task.WhenAll(failedTask, failEval).ConfigureAwait(false);

            IRequest failedRequest = await failedTask.ConfigureAwait(false);
            Assert.That(failedRequest.Url, Is.EqualTo(Prefix + "/serviceworkers/fetchdummy/error"));
            Assert.That(failedRequest.Failure, Is.Not.Null);
            Assert.That(failedRequest.ServiceWorker(), Is.Null);
            Assert.That(await failedRequest.ResponseAsync().ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-event-request.spec.ts", "should report requests and responses handled by service worker with routing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportRequestsAndResponsesHandledByServiceWorkerWithRouting()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<string> interceptedUrls = new List<string>();
            await page.RouteAsync("**/*", route =>
            {
                interceptedUrls.Add(route.Request.Url);
                return route.ContinueAsync();
            }).ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/serviceworkers/fetchdummy/sw.html").ConfigureAwait(false);
            await WaitForServiceWorkerActivationAsync(page).ConfigureAwait(false);

            Task<string> swTask = page.EvaluateAsync<string>("(() => window['fetchDummy']('foo'))()");
            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.Request);
            await Task.WhenAll(swTask, requestTask).ConfigureAwait(false);

            string swResponse = await swTask.ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(swResponse, Is.EqualTo("responseFromServiceWorker:foo"));
            Assert.That(request.Url, Is.EqualTo(Prefix + "/serviceworkers/fetchdummy/foo"));
            Assert.That(request.ServiceWorker(), Is.Null);
            IResponse response = await request.ResponseAsync().ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/serviceworkers/fetchdummy/foo"));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("responseFromServiceWorker:foo"));

            Task<IRequest> failedTask = page.WaitForEventAsync(PageEvent.RequestFailed);
            Task failEval = page.EvaluateAsync<object>("(() => window['fetchDummy']('error'))()")
                .ContinueWith(_ => { }, TaskScheduler.Default);
            await Task.WhenAll(failedTask, failEval).ConfigureAwait(false);

            IRequest failedRequest = await failedTask.ConfigureAwait(false);
            Assert.That(failedRequest.Url, Is.EqualTo(Prefix + "/serviceworkers/fetchdummy/error"));
            Assert.That(failedRequest.Failure, Is.Not.Null);
            Assert.That(failedRequest.ServiceWorker(), Is.Null);
            Assert.That(await failedRequest.ResponseAsync().ConfigureAwait(false), Is.Null);

            List<string> expectedUrls = new List<string> { Prefix + "/serviceworkers/fetchdummy/sw.html" };
            if (TestConstants.IsWebKit)
            {
                expectedUrls.Add(Prefix + "/serviceworkers/fetchdummy/sw.js");
            }

            Assert.That(interceptedUrls, Is.EqualTo(expectedUrls));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should report navigation requests and responses handled by service worker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportNavigationRequestsAndResponsesHandledByServiceWorker()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/serviceworkers/stub/sw.html").ConfigureAwait(false);
            await WaitForServiceWorkerActivationAsync(page).ConfigureAwait(false);

            IResponse reloadResponse = await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.fromSW").ConfigureAwait(false), Is.True);
            Assert.That(reloadResponse.Url, Is.EqualTo(Prefix + "/serviceworkers/stub/sw.html"));
            await WaitForServiceWorkerActivationAsync(page).ConfigureAwait(false);

            if (!TestConstants.IsFirefox)
            {
                Task<IRequest> failedTask = page.WaitForEventAsync(PageEvent.RequestFailed);
                Task navTask = page.EvaluateAsync<object>(@"(() => {
                        window.location.href = '/serviceworkers/stub/error.html';
                    })()").ContinueWith(_ => { }, TaskScheduler.Default);
                await Task.WhenAll(navTask, failedTask).ConfigureAwait(false);

                IRequest failedRequest = await failedTask.ConfigureAwait(false);
                Assert.That(failedRequest.Url, Is.EqualTo(Prefix + "/serviceworkers/stub/error.html"));
                Assert.That(
                    failedRequest.Failure,
                    Does.Contain(TestConstants.IsChromium ? "net::ERR_FAILED" : "uh oh"));
                Assert.That(failedRequest.ServiceWorker(), Is.Null);
                Assert.That(await failedRequest.ResponseAsync().ConfigureAwait(false), Is.Null);
            }
        }

        [PlaywrightTest("page-event-request.spec.ts", "should report navigation requests and responses handled by service worker with routing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportNavigationRequestsAndResponsesHandledByServiceWorkerWithRouting()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/serviceworkers/stub/sw.html").ConfigureAwait(false);
            await WaitForServiceWorkerActivationAsync(page).ConfigureAwait(false);

            IResponse reloadResponse = await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.fromSW").ConfigureAwait(false), Is.True);
            Assert.That(reloadResponse.Url, Is.EqualTo(Prefix + "/serviceworkers/stub/sw.html"));
            await WaitForServiceWorkerActivationAsync(page).ConfigureAwait(false);

            if (!TestConstants.IsFirefox)
            {
                Task<IRequest> failedTask = page.WaitForEventAsync(PageEvent.RequestFailed);
                Task navTask = page.EvaluateAsync<object>(@"(() => {
                        window.location.href = '/serviceworkers/stub/error.html';
                        undefined
                    })()").ContinueWith(_ => { }, TaskScheduler.Default);
                await Task.WhenAll(navTask, failedTask).ConfigureAwait(false);

                IRequest failedRequest = await failedTask.ConfigureAwait(false);
                Assert.That(failedRequest.Url, Is.EqualTo(Prefix + "/serviceworkers/stub/error.html"));
                Assert.That(
                    failedRequest.Failure,
                    Does.Contain(TestConstants.IsChromium ? "net::ERR_FAILED" : "uh oh"));
                Assert.That(failedRequest.ServiceWorker(), Is.Null);
                Assert.That(await failedRequest.ResponseAsync().ConfigureAwait(false), Is.Null);
            }
        }

        [PlaywrightTest("page-event-request.spec.ts", "should return response body when Cross-Origin-Opener-Policy is set")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnResponseBodyWhenCrossOriginOpenerPolicyIsSet()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                await http.Response.WriteAsync(
                    "\n      <div>Hello there!</div>\n      <script>window.onload = () => console.log('onload')</script>\n    ").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
            await response.FinishedAsync().ConfigureAwait(false);
            Assert.That(response.Request.Failure, Is.Null);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("Hello there!"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should fire requestfailed when intercepting race")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireRequestfailedWhenInterceptingRace()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("This test is specifically testing Chromium race");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int counter = 0;
            HashSet<IRequest> failures = new HashSet<IRequest>();
            HashSet<IRequest> alive = new HashSet<IRequest>();
            page.Request += (_, request) =>
            {
                Assert.That(alive.Contains(request), Is.False);
                Assert.That(failures.Contains(request), Is.False);
                alive.Add(request);
            };
            page.RequestFailed += (_, request) =>
            {
                Assert.That(failures.Contains(request), Is.False);
                Assert.That(alive.Contains(request), Is.True);
                alive.Remove(request);
                failures.Add(request);
                if (++counter == 10)
                {
                    done.TrySetResult(true);
                }
            };

            await page.RouteAsync("**", _ => { }).ConfigureAwait(false);

            await page.SetContentAsync(
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <iframe src=\"" + EmptyPage + "\"></iframe>\n" +
                "    <script>\n" +
                "      function abortAll() {\n" +
                "        const frames = document.querySelectorAll(\"iframe\");\n" +
                "        for (const frame of frames)\n" +
                "          frame.src = \"about:blank\";\n" +
                "      }\n" +
                "      abortAll();\n" +
                "    </script>\n").ConfigureAwait(false);

            await done.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("page-event-request.spec.ts", "main resource xhr should have type xhr")]
        [Test]
        [Timeout(30_000)]
        public async Task MainResourceXhrShouldHaveTypeXhr()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.Request);
            await Task.WhenAll(
                requestTask,
                page.EvaluateAsync<object>(@"(() => {
                    const x = new XMLHttpRequest();
                    x.open('GET', location.href, false);
                    x.send();
                })()")).ConfigureAwait(false);

            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.IsNavigationRequest, Is.False);
            Assert.That(request.ResourceType, Is.EqualTo("xhr"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should finish 204 request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFinish204Request()
        {
            EnsureServer();
            Server.SetRoute("/204", http =>
            {
                http.Response.StatusCode = 204;
                http.Response.ContentType = "text/plain";
                return Task.CompletedTask;
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> failedTask = page.WaitForRequestFailedAsync(
                r => r.Url.EndsWith("/204", StringComparison.Ordinal));
            Task<IRequest> finishedTask = page.WaitForRequestFinishedAsync(
                r => r.Url.EndsWith("/204", StringComparison.Ordinal));
            _ = page.EvaluateAsync<object>("(async (url) => { await fetch(url); })", Prefix + "/204")
                .ContinueWith(_ => { }, TaskScheduler.Default);

            Task<IRequest> winner = await Task.WhenAny(failedTask, finishedTask).ConfigureAwait(false);
            await winner.ConfigureAwait(false);
            string name = winner == finishedTask ? "requestfinished" : "requestfailed";
            Assert.That(name, Is.EqualTo("requestfinished"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "<picture> resource should have type image")]
        [Test]
        [Timeout(30_000)]
        public async Task PictureResourceShouldHaveTypeImage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> requestTask = page.WaitForEventAsync(PageEvent.Request);
            await Task.WhenAll(
                requestTask,
                page.SetContentAsync(
                    "      <picture>\n" +
                    "        <source>\n" +
                    "          <img src=\"https://www.wikipedia.org/portal/wikipedia.org/assets/img/Wikipedia-logo-v2@2x.png\">\n" +
                    "        </source>\n" +
                    "      </picture>\n")).ConfigureAwait(false);

            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.ResourceType, Is.EqualTo("image"));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should not expose preflight OPTIONS request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotExposePreflightOptionsRequest()
        {
            EnsureServer();
            List<string> serverRequests = new List<string>();
            InstallCorsRoute(serverRequests);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<string> clientRequests = new List<string>();
            page.Request += (_, request) => clientRequests.Add(request.Method + " " + request.Url);

            string response = await FetchCorsAsync(page, CrossProcessPrefix + "/cors").ConfigureAwait(false);
            Assert.That(response, Is.EqualTo("Hello there!"));
            Assert.That(serverRequests, Is.EqualTo(new[] { "OPTIONS /cors", "POST /cors" }));
            Assert.That(clientRequests, Is.EqualTo(new[] { "POST " + CrossProcessPrefix + "/cors" }));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should not expose preflight OPTIONS request with network interception")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotExposePreflightOptionsRequestWithNetworkInterception()
        {
            EnsureServer();
            List<string> serverRequests = new List<string>();
            InstallCorsRoute(serverRequests);

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            List<string> clientRequests = new List<string>();
            page.Request += (_, request) => clientRequests.Add(request.Method + " " + request.Url);

            string response = await FetchCorsAsync(page, CrossProcessPrefix + "/cors").ConfigureAwait(false);
            Assert.That(response, Is.EqualTo("Hello there!"));

            List<string> expectedServer = new List<string>();
            if (!TestConstants.IsChromium)
            {
                expectedServer.Add("OPTIONS /cors");
            }

            expectedServer.Add("POST /cors");
            Assert.That(serverRequests, Is.EqualTo(expectedServer));
            Assert.That(clientRequests, Is.EqualTo(new[] { "POST " + CrossProcessPrefix + "/cors" }));
        }

        [PlaywrightTest("page-event-request.spec.ts", "should return last requests")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnLastRequests()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/title.html").ConfigureAwait(false);
            for (int i = 0; i < 200; ++i)
            {
                int index = i;
                Server.SetRoute("/fetch?" + index.ToString(CultureInfo.InvariantCulture), http =>
                {
                    http.Response.StatusCode = 200;
                    string path = http.Request.Path.Value + http.Request.QueryString.Value;
                    return http.Response.WriteAsync("url:" + Prefix + path);
                });
            }

            for (int i = 0; i < 99; ++i)
            {
                await FetchUrlAsync(page, Prefix + "/fetch?" + i.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            }

            List<IRequest> first99Requests = new List<IRequest>(await page.RequestsAsync().ConfigureAwait(false));
            first99Requests.RemoveAt(0);
            for (int i = 99; i < 199; ++i)
            {
                await FetchUrlAsync(page, Prefix + "/fetch?" + i.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            }

            IReadOnlyList<IRequest> last100Requests = await page.RequestsAsync().ConfigureAwait(false);
            List<IRequest> allRequests = new List<IRequest>(first99Requests);
            allRequests.AddRange(last100Requests);

            List<(string Text, string Url)> received = new List<(string Text, string Url)>();
            foreach (IRequest request in allRequests)
            {
                IResponse response = await request.ResponseAsync().ConfigureAwait(false);
                received.Add((await response.TextAsync().ConfigureAwait(false), request.Url));
            }

            List<(string Text, string Url)> expected = new List<(string Text, string Url)>();
            for (int i = 0; i < 199; ++i)
            {
                string url = Prefix + "/fetch?" + i.ToString(CultureInfo.InvariantCulture);
                expected.Add(("url:" + url, url));
            }

            Assert.That(received, Is.EqualTo(expected));
        }
    }
}
