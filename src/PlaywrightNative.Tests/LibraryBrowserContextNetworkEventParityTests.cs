/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-network-event.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextNetworkEventTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextNetworkEventParityTests : PageTestEx
    {
        private const string OpenPopupLink =
            "<a target=_blank rel=noopener href=\"/one-style.html\">yo</a>";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19844;
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

        [PlaywrightTest("browsercontext-network-event.spec.ts", "BrowserContext.Events.Request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserContextEventsRequest()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<IRequest> requests = new();
            context.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(OpenPopupLink).ConfigureAwait(false);
            Task<IPage> pageTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.ClickAsync("a").ConfigureAwait(false);
            IPage page1 = await pageTask.ConfigureAwait(false);
            await page1.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(
                requests.Select(request => request.Url).ToArray(),
                Is.EqualTo(new[]
                {
                    EmptyPage,
                    Prefix + "/one-style.html",
                    Prefix + "/one-style.css",
                }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "BrowserContext.Events.Response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserContextEventsResponse()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<IResponse> responses = new();
            context.Response += (_, response) => responses.Add(response);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(OpenPopupLink).ConfigureAwait(false);
            Task<IPage> pageTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.ClickAsync("a").ConfigureAwait(false);
            IPage page1 = await pageTask.ConfigureAwait(false);
            await page1.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(
                responses.Select(response => response.Url).ToArray(),
                Is.EqualTo(new[]
                {
                    EmptyPage,
                    Prefix + "/one-style.html",
                    Prefix + "/one-style.css",
                }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "BrowserContext.Events.RequestFailed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserContextEventsRequestFailed()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", async http =>
            {
                http.Response.ContentType = "text/css";
                http.Response.ContentLength = 64;
                await http.Response.StartAsync().ConfigureAwait(false);
                http.Abort();
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<IRequest> failedRequests = new();
            context.RequestFailed += (_, request) => failedRequests.Add(request);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            Assert.That(failedRequests.Count, Is.EqualTo(1));
            Assert.That(failedRequests[0].Url, Does.Contain("one-style.css"));
            Assert.That(await failedRequests[0].ResponseAsync().ConfigureAwait(false), Is.Null);
            Assert.That(failedRequests[0].ResourceType, Is.EqualTo("stylesheet"));
            Assert.That(failedRequests[0].Frame, Is.Not.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "BrowserContext.Events.RequestFinished")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserContextEventsRequestFinished()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IRequest> finishedTask = context.WaitForEventAsync(BrowserContextEvent.RequestFinished);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await finishedTask.ConfigureAwait(false);
            IRequest request = response.Request;
            Assert.That(request.Url, Is.EqualTo(EmptyPage));
            Assert.That(await request.ResponseAsync().ConfigureAwait(false), Is.Not.Null);
            Assert.That(request.Frame, Is.SameAs(page.MainFrame));
            Assert.That(request.Frame.Url, Is.EqualTo(EmptyPage));
            Assert.That(request.Failure, Is.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "should fire events in proper order")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireEventsInProperOrder()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<string> events = new();
            context.Request += (_, _) => events.Add("request");
            context.Response += (_, _) => events.Add("response");
            context.RequestFinished += (_, _) => events.Add("requestfinished");
            Task<IRequest> finishedTask = context.WaitForEventAsync(BrowserContextEvent.RequestFinished);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await finishedTask.ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "request", "response", "requestfinished" }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "should not fire events for favicon or favicon redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFireEventsForFaviconOrFaviconRedirects()
        {
            // Official: it.skip(headless && browserName !== 'firefox',
            // 'headless browsers, except firefox, do not request favicons')
            Assert.Ignore("headless browsers, except firefox, do not request favicons");
            await Task.CompletedTask.ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-network-event.spec.ts", "should reject response.finished if context closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectResponseFinishedIfContextCloses()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/get", async http =>
            {
                http.Response.ContentType = "text/plain; charset=utf-8";
                await http.Response.WriteAsync("hello ").ConfigureAwait(false);
                await Task.Delay(-1).ConfigureAwait(false);
            });
            Task<IResponse> responseTask = page.WaitForEventAsync(PageEvent.Response);
            _ = page.EvaluateAsync("() => fetch('./get', { method: 'GET' })");
            IResponse pageResponse = await responseTask.ConfigureAwait(false);
            Task<string> finishTask = pageResponse.FinishedAsync();
            await context.CloseAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(finishTask).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("closed"));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<Exception> CatchAsync<T>(Task<T> task)
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
