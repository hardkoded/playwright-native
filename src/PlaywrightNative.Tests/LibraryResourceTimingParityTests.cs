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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/resource-timing.spec.ts</c> parity. Official
    /// <c>it.fixme</c> on WebKit Windows subresource and all-WebKit
    /// redirect timing. Do not edit leftover
    /// <c>ApiResponseTimingTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryResourceTimingParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static OfficialHttpsTargetServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19889;
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
                    _ownedHttps = OfficialHttpsTargetServer.Start();
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
                _ownedHttps = OfficialHttpsTargetServer.Start();
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedHttps != null)
            {
                await _ownedHttps.DisposeAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }

            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            Server?.Reset();
            await DisposeBrowserAsync().ConfigureAwait(false);
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            await DisposeBrowserAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("resource-timing.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkSmoke()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IRequest> finished = page.WaitForEventAsync(PageEvent.RequestFinished);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IRequest request = await finished.ConfigureAwait(false);
            RequestTimingResult timing = request.Timing;
            VerifyConnectionTimingConsistency(timing);
            Assert.That(timing.RequestStart, Is.GreaterThanOrEqualTo(timing.ConnectEnd));
            Assert.That(timing.ResponseStart, Is.GreaterThanOrEqualTo(timing.RequestStart));
            Assert.That(timing.ResponseEnd, Is.GreaterThanOrEqualTo(timing.ResponseStart));
            Assert.That(timing.ResponseEnd, Is.LessThan(10000));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("resource-timing.spec.ts", "should work for subresource")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForSubresource()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("responseStart is wrong due upstream webkit/libcurl bug");
            }

            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<IRequest> requests = new();
            page.RequestFinished += (_, request) => requests.Add(request);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            Assert.That(requests.Count, Is.EqualTo(2));
            RequestTimingResult timing = requests[1].Timing;
            VerifyConnectionTimingConsistency(timing);
            Assert.That(timing.RequestStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(timing.ResponseStart, Is.GreaterThan(timing.RequestStart));
            Assert.That(timing.ResponseEnd, Is.GreaterThanOrEqualTo(timing.ResponseStart));
            Assert.That(timing.ResponseEnd, Is.LessThan(10000));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("resource-timing.spec.ts", "should work for SSL")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForSsl()
        {
            if (_ownedHttps == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }

            IPage page = await _browser.NewPageAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            try
            {
                string url = "https://localhost:" + _ownedHttps.Port.ToString(CultureInfo.InvariantCulture) + "/empty.html";
                Task<IRequest> finished = page.WaitForEventAsync(PageEvent.RequestFinished);
                await page.GoToAsync(url).ConfigureAwait(false);
                IRequest request = await finished.ConfigureAwait(false);
                RequestTimingResult timing = request.Timing;
                VerifyConnectionTimingConsistency(timing);
                Assert.That(timing.RequestStart, Is.GreaterThanOrEqualTo(timing.ConnectEnd));
                Assert.That(timing.ResponseStart, Is.GreaterThan(timing.RequestStart));
                Assert.That(timing.ResponseEnd, Is.GreaterThanOrEqualTo(timing.ResponseStart));
                Assert.That(timing.ResponseEnd, Is.LessThan(10000));
            }
            finally
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("resource-timing.spec.ts", "should work for redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForRedirect()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("In WebKit, redirects don't carry the timing info");
            }

            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Server.SetRedirect("/foo.html", "/empty.html");
            List<IResponse> responses = new();
            page.Response += (_, response) => responses.Add(response);
            await page.GoToAsync(Prefix + "/foo.html").ConfigureAwait(false);
            foreach (IResponse response in responses)
            {
                await response.FinishedAsync().ConfigureAwait(false);
            }

            Assert.That(responses.Count, Is.EqualTo(2));
            Assert.That(responses[0].Url, Is.EqualTo(Prefix + "/foo.html"));
            Assert.That(responses[1].Url, Is.EqualTo(Prefix + "/empty.html"));

            RequestTimingResult timing1 = responses[0].Request.Timing;
            VerifyConnectionTimingConsistency(timing1);
            Assert.That(timing1.RequestStart, Is.GreaterThanOrEqualTo(timing1.ConnectEnd));
            Assert.That(timing1.ResponseStart, Is.GreaterThan(timing1.RequestStart));
            Assert.That(timing1.ResponseEnd, Is.GreaterThanOrEqualTo(timing1.ResponseStart));
            Assert.That(timing1.ResponseEnd, Is.LessThan(10000));

            RequestTimingResult timing2 = responses[1].Request.Timing;
            VerifyConnectionTimingConsistency(timing2);
            Assert.That(timing2.RequestStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(timing2.ResponseStart, Is.GreaterThan(timing2.RequestStart));
            Assert.That(timing2.ResponseEnd, Is.GreaterThanOrEqualTo(timing2.ResponseStart));
            Assert.That(timing2.ResponseEnd, Is.LessThan(10000));

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("resource-timing.spec.ts", "should work when serving from memory cache")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenServingFromMemoryCache()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", async http =>
            {
                http.Response.ContentType = "text/css";
                http.Response.Headers["Cache-Control"] = "public, max-age=10031518";
                await http.Response.WriteAsync("body { background: red }").ConfigureAwait(false);
            });

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            Task<IResponse> wait = page.WaitForResponseAsync("**/one-style.css");
            await page.ReloadAsync().ConfigureAwait(false);
            IResponse response = await wait.ConfigureAwait(false);
            await response.FinishedAsync().ConfigureAwait(false);

            RequestTimingResult timing = response.Request.Timing;
            VerifyConnectionTimingConsistency(timing);
            Assert.That(timing.ResponseStart, Is.EqualTo(timing.ResponseEnd));
            Assert.That(timing.ResponseEnd, Is.LessThan(1000));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void VerifyTimingValue(float value, float previous)
        {
            Assert.That(value == -1 || (value > 0 && value >= previous), Is.True);
        }

        private static void VerifyConnectionTimingConsistency(RequestTimingResult timing)
        {
            VerifyTimingValue(timing.DomainLookupStart, -1);
            VerifyTimingValue(timing.DomainLookupEnd, timing.DomainLookupStart);
            VerifyTimingValue(timing.ConnectStart, timing.DomainLookupEnd);
            VerifyTimingValue(timing.SecureConnectionStart, timing.ConnectStart);
            VerifyTimingValue(timing.ConnectEnd, timing.SecureConnectionStart);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private async Task DisposeBrowserAsync()
        {
            if (_browser != null)
            {
                try
                {
                    await _browser.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _browser = null;
            }
        }
    }
}
