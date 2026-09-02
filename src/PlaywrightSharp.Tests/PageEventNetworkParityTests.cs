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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-event-network.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageEventNetworkParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19556;
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

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.Request")]
        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.Request @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task PageEventsRequest()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Url, Is.EqualTo(EmptyPage));
            Assert.That(requests[0].ResourceType, Is.EqualTo("document"));
            Assert.That(requests[0].Method, Is.EqualTo("GET"));
            Assert.That(await requests[0].ResponseAsync().ConfigureAwait(false), Is.Not.Null);
            Assert.That(requests[0].Frame, Is.SameAs(page.MainFrame));
            Assert.That(requests[0].Frame.Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.Response")]
        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.Response @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task PageEventsResponse()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IResponse> responses = new List<IResponse>();
            page.Response += (_, response) => responses.Add(response);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(responses.Count, Is.EqualTo(1));
            Assert.That(responses[0].Url, Is.EqualTo(EmptyPage));
            Assert.That(responses[0].Status, Is.EqualTo(200));
            Assert.That(responses[0].Ok, Is.True);
            Assert.That(responses[0].Request, Is.Not.Null);
        }

        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.RequestFailed")]
        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.RequestFailed @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task PageEventsRequestFailed()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", async http =>
            {
                http.Response.ContentType = "text/css";
                http.Response.ContentLength = 64;
                await http.Response.StartAsync().ConfigureAwait(false);
                http.Abort();
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> failedRequests = new List<IRequest>();
            page.RequestFailed += (_, request) => failedRequests.Add(request);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            Assert.That(failedRequests.Count, Is.EqualTo(1));
            Assert.That(failedRequests[0].Url, Does.Contain("one-style.css"));
            Assert.That(await failedRequests[0].ResponseAsync().ConfigureAwait(false), Is.Null);
            Assert.That(failedRequests[0].ResourceType, Is.EqualTo("stylesheet"));
            if (TestConstants.IsChromium)
            {
                Assert.That(
                    failedRequests[0].Failure,
                    Is.EqualTo("net::ERR_EMPTY_RESPONSE").Or.EqualTo("net::ERR_CONNECTION_RESET"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(
                    failedRequests[0].Failure,
                    Does.Match(new Regex("(Message Corrupt)|(Connection terminated unexpectedly)|(The network connection was lost.)|(Server returned nothing)|(Connection reset by peer)", RegexOptions.IgnoreCase)));
            }
            else
            {
                Assert.That(failedRequests[0].Failure, Is.EqualTo("NS_ERROR_NET_RESET"));
            }

            Assert.That(failedRequests[0].Frame, Is.Not.Null);
        }

        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.RequestFinished")]
        [PlaywrightTest("page-event-network.spec.ts", "Page.Events.RequestFinished @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task PageEventsRequestFinished()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IRequest> finishedTask = page.WaitForRequestFinishedAsync();
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await finishedTask.ConfigureAwait(false);
            IRequest request = response.Request;
            Assert.That(request.Url, Is.EqualTo(EmptyPage));
            Assert.That(await request.ResponseAsync().ConfigureAwait(false), Is.Not.Null);
            Assert.That(request.Frame, Is.SameAs(page.MainFrame));
            Assert.That(request.Frame.Url, Is.EqualTo(EmptyPage));
            Assert.That(request.Failure, Is.Null);
        }

        [PlaywrightTest("page-event-network.spec.ts", "should fire events in proper order")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireEventsInProperOrder()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<string> events = new List<string>();
            page.Request += (_, _) => events.Add("request");
            page.Response += (_, _) => events.Add("response");
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await response.FinishedAsync().ConfigureAwait(false), Is.Null);
            events.Add("requestfinished");
            Assert.That(events, Is.EqualTo(new[] { "request", "response", "requestfinished" }));
        }

        [PlaywrightTest("page-event-network.spec.ts", "should support redirects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportRedirects()
        {
            EnsureServer();
            string fooUrl = Prefix + "/foo.html";
            Dictionary<string, List<string>> events = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                [EmptyPage] = new List<string>(),
                [fooUrl] = new List<string>(),
            };

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Request += (_, request) => events[request.Url].Add(request.Method);
            page.Response += (_, response) => events[response.Url].Add(response.Status.ToString(CultureInfo.InvariantCulture));
            page.RequestFinished += (_, request) => events[request.Url].Add("DONE");
            page.RequestFailed += (_, request) => events[request.Url].Add("FAIL");

            Server.SetRedirect("/foo.html", "/empty.html");
            IResponse response = await page.GoToAsync(fooUrl).ConfigureAwait(false);
            await response.FinishedAsync().ConfigureAwait(false);

            Dictionary<string, List<string>> expected = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                [fooUrl] = new List<string> { "GET", "302", "DONE" },
                [EmptyPage] = new List<string> { "GET", "200", "DONE" },
            };
            Assert.That(events, Is.EqualTo(expected));
            IRequest redirectedFrom = response.Request.RedirectedFrom;
            Assert.That(redirectedFrom.Url, Does.Contain("/foo.html"));
            Assert.That(redirectedFrom.RedirectedFrom, Is.Null);
            Assert.That(redirectedFrom.RedirectedTo, Is.SameAs(response.Request));
        }

        [PlaywrightTest("page-event-network.spec.ts", "should resolve responses after a navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveResponsesAfterANavigation()
        {
            EnsureServer();
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("upstream test.fixme(chromium)");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<HttpContext> responseFromServer = new TaskCompletionSource<HttpContext>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/foo", http =>
            {
                responseFromServer.TrySetResult(http);
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            });

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForRequestAsync(_ => true);
            string fooUrl = Prefix + "/foo";
            await page.EvaluateAsync<object>(
                "url => { void fetch(url); }",
                fooUrl).ConfigureAwait(false);
            HttpContext serverResponse = await responseFromServer.Task.ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Task<IResponse> responseTask = request.ResponseAsync();
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            serverResponse.Abort();
            Assert.That(await responseTask.ConfigureAwait(false), Is.Null);
        }
    }
}
