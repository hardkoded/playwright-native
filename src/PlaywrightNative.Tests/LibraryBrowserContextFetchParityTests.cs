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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-fetch.spec.ts</c> parity.
    /// Do not edit leftover <c>ApiRequestTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextFetchParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
        private static string Hostname = "localhost";
        private static int ServerPort = TestConstants.Port;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19841;
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
                    Hostname = "localhost";
                    ServerPort = port;
                    await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
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
                Hostname = "localhost";
                ServerPort = TestConstants.Port;
                await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }

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
                    await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                }

                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }

            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            _ownedHttps?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "get should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetShouldWork()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/simple.json"));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.StatusText, Is.EqualTo("OK"));
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Headers["content-type"], Is.EqualTo("application/json; charset=utf-8"));
            Assert.That(response.HeadersArray, Does.Contain(new Header { Name = "Content-Type", Value = "application/json; charset=utf-8" }));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("{\"foo\": \"bar\"}\n"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "fetch should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldWork()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.FetchAsync(Prefix + "/simple.json").ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/simple.json"));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.StatusText, Is.EqualTo("OK"));
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Headers["content-type"], Is.EqualTo("application/json; charset=utf-8"));
            Assert.That(response.HeadersArray, Does.Contain(new Header { Name = "Content-Type", Value = "application/json; charset=utf-8" }));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("{\"foo\": \"bar\"}\n"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should return timing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnTiming()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            RawHttpServer hello = await StartRawHttpAsync(
                Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 5\r\nConnection: close\r\n\r\nHello")).ConfigureAwait(false);
            try
            {
                IAPIResponse response = await context.APIRequest.GetAsync("http://localhost:" + hello.Port.ToString(CultureInfo.InvariantCulture) + "/").ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
                RequestTimingResult timing = response.Timing;
                Assert.That(timing.StartTime, Is.EqualTo((float)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Within(10000));
                Assert.That(timing.DomainLookupStart, Is.EqualTo(0));
                Assert.That(timing.DomainLookupEnd, Is.GreaterThanOrEqualTo(timing.DomainLookupStart));
                Assert.That(timing.ConnectStart, Is.EqualTo(timing.DomainLookupEnd));
                Assert.That(timing.SecureConnectionStart, Is.EqualTo(-1));
                Assert.That(timing.ConnectEnd, Is.GreaterThanOrEqualTo(timing.ConnectStart));
                Assert.That(timing.RequestStart, Is.EqualTo(timing.ConnectEnd));
                Assert.That(timing.ResponseStart, Is.GreaterThanOrEqualTo(timing.RequestStart));
                Assert.That(timing.ResponseEnd, Is.GreaterThanOrEqualTo(timing.ResponseStart));
                Assert.That(timing.ResponseEnd, Is.LessThan(60_000));
            }
            finally
            {
                await hello.DisposeAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should return timing for https")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnTimingForHttps()
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            (SimpleServer https, int httpsPort) = await StartEphemeralHttpsAsync().ConfigureAwait(false);
            try
            {
                https.SetRoute("/", http => http.Response.WriteAsync("Hello"));
                string url = "https://localhost:" + httpsPort.ToString(CultureInfo.InvariantCulture) + "/";
                IAPIResponse response = await context.APIRequest.GetAsync(url, new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
                RequestTimingResult timing = response.Timing;
                Assert.That(timing.StartTime, Is.EqualTo((float)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Within(10000));
                Assert.That(timing.DomainLookupStart, Is.EqualTo(0));
                Assert.That(timing.DomainLookupEnd, Is.GreaterThanOrEqualTo(timing.DomainLookupStart));
                Assert.That(timing.ConnectStart, Is.EqualTo(timing.DomainLookupEnd));
                Assert.That(timing.SecureConnectionStart, Is.GreaterThanOrEqualTo(timing.ConnectStart));
                Assert.That(timing.ConnectEnd, Is.GreaterThanOrEqualTo(timing.SecureConnectionStart));
                Assert.That(timing.RequestStart, Is.EqualTo(timing.ConnectEnd));
                Assert.That(timing.ResponseStart, Is.GreaterThanOrEqualTo(timing.RequestStart));
                Assert.That(timing.ResponseEnd, Is.GreaterThanOrEqualTo(timing.ResponseStart));
            }
            finally
            {
                await https.StopAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw on network error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnNetworkError()
        {
            EnsureServer();
            Server.SetRoute("/test", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/test")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("apiRequestContext.get: socket hang up"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw on network error after redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnNetworkErrorAfterRedirect()
        {
            EnsureServer();
            Server.SetRedirect("/redirect", "/test");
            Server.SetRoute("/test", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/redirect")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("apiRequestContext.get: socket hang up"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw on network error when sending body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnNetworkErrorWhenSendingBody()
        {
            EnsureServer();
            Server.SetRoute("/test", http => WritePartialBodyThenAbortAsync(http));
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/test")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("apiRequestContext.get: aborted"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw on network error when sending body after redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnNetworkErrorWhenSendingBodyAfterRedirect()
        {
            EnsureServer();
            Server.SetRedirect("/redirect", "/test");
            Server.SetRoute("/test", http => WritePartialBodyThenAbortAsync(http));
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/redirect")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("apiRequestContext.get: aborted"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should add session cookies to request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddSessionCookiesToRequest()
        {
            Assert.Ignore("Node __testHookLookup DNS hook");
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should filter cookies by domain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterCookiesByDomain()
        {
            Assert.Ignore("Node __testHookLookup DNS hook");
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "fetch should support params passed as object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldSupportParamsPassedAsObject()
        {
            await ShouldSupportParamsPassedAsObjectAsync("fetch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "fetch should support params passed as URLSearchParams")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldSupportParamsPassedAsUrlSearchParams()
        {
            await ShouldSupportParamsPassedAsUrlSearchParamsAsync("fetch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "fetch should support params passed as string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldSupportParamsPassedAsString()
        {
            await ShouldSupportParamsPassedAsStringAsync("fetch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "fetch should support failOnStatusCode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldSupportFailOnStatusCode()
        {
            await ShouldSupportFailOnStatusCodeAsync("fetch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "fetchshould support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldSupportIgnoreHttpsErrorsOption()
        {
            await ShouldSupportIgnoreHttpsErrorsOptionAsync("fetch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "delete should support params passed as object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DeleteShouldSupportParamsPassedAsObject()
        {
            await ShouldSupportParamsPassedAsObjectAsync("delete").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "delete should support params passed as URLSearchParams")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DeleteShouldSupportParamsPassedAsUrlSearchParams()
        {
            await ShouldSupportParamsPassedAsUrlSearchParamsAsync("delete").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "delete should support params passed as string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DeleteShouldSupportParamsPassedAsString()
        {
            await ShouldSupportParamsPassedAsStringAsync("delete").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "delete should support failOnStatusCode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DeleteShouldSupportFailOnStatusCode()
        {
            await ShouldSupportFailOnStatusCodeAsync("delete").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "deleteshould support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DeleteShouldSupportIgnoreHttpsErrorsOption()
        {
            await ShouldSupportIgnoreHttpsErrorsOptionAsync("delete").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "get should support params passed as object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetShouldSupportParamsPassedAsObject()
        {
            await ShouldSupportParamsPassedAsObjectAsync("get").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "get should support params passed as URLSearchParams")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetShouldSupportParamsPassedAsUrlSearchParams()
        {
            await ShouldSupportParamsPassedAsUrlSearchParamsAsync("get").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "get should support params passed as string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetShouldSupportParamsPassedAsString()
        {
            await ShouldSupportParamsPassedAsStringAsync("get").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "get should support failOnStatusCode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetShouldSupportFailOnStatusCode()
        {
            await ShouldSupportFailOnStatusCodeAsync("get").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "getshould support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetShouldSupportIgnoreHttpsErrorsOption()
        {
            await ShouldSupportIgnoreHttpsErrorsOptionAsync("get").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "head should support params passed as object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HeadShouldSupportParamsPassedAsObject()
        {
            await ShouldSupportParamsPassedAsObjectAsync("head").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "head should support params passed as URLSearchParams")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HeadShouldSupportParamsPassedAsUrlSearchParams()
        {
            await ShouldSupportParamsPassedAsUrlSearchParamsAsync("head").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "head should support params passed as string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HeadShouldSupportParamsPassedAsString()
        {
            await ShouldSupportParamsPassedAsStringAsync("head").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "head should support failOnStatusCode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HeadShouldSupportFailOnStatusCode()
        {
            await ShouldSupportFailOnStatusCodeAsync("head").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "headshould support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HeadShouldSupportIgnoreHttpsErrorsOption()
        {
            await ShouldSupportIgnoreHttpsErrorsOptionAsync("head").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "patch should support params passed as object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PatchShouldSupportParamsPassedAsObject()
        {
            await ShouldSupportParamsPassedAsObjectAsync("patch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "patch should support params passed as URLSearchParams")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PatchShouldSupportParamsPassedAsUrlSearchParams()
        {
            await ShouldSupportParamsPassedAsUrlSearchParamsAsync("patch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "patch should support params passed as string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PatchShouldSupportParamsPassedAsString()
        {
            await ShouldSupportParamsPassedAsStringAsync("patch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "patch should support failOnStatusCode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PatchShouldSupportFailOnStatusCode()
        {
            await ShouldSupportFailOnStatusCodeAsync("patch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "patchshould support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PatchShouldSupportIgnoreHttpsErrorsOption()
        {
            await ShouldSupportIgnoreHttpsErrorsOptionAsync("patch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "post should support params passed as object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PostShouldSupportParamsPassedAsObject()
        {
            await ShouldSupportParamsPassedAsObjectAsync("post").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "post should support params passed as URLSearchParams")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PostShouldSupportParamsPassedAsUrlSearchParams()
        {
            await ShouldSupportParamsPassedAsUrlSearchParamsAsync("post").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "post should support params passed as string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PostShouldSupportParamsPassedAsString()
        {
            await ShouldSupportParamsPassedAsStringAsync("post").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "post should support failOnStatusCode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PostShouldSupportFailOnStatusCode()
        {
            await ShouldSupportFailOnStatusCodeAsync("post").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "postshould support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PostShouldSupportIgnoreHttpsErrorsOption()
        {
            await ShouldSupportIgnoreHttpsErrorsOptionAsync("post").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "put should support params passed as object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PutShouldSupportParamsPassedAsObject()
        {
            await ShouldSupportParamsPassedAsObjectAsync("put").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "put should support params passed as URLSearchParams")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PutShouldSupportParamsPassedAsUrlSearchParams()
        {
            await ShouldSupportParamsPassedAsUrlSearchParamsAsync("put").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "put should support params passed as string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PutShouldSupportParamsPassedAsString()
        {
            await ShouldSupportParamsPassedAsStringAsync("put").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "put should support failOnStatusCode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PutShouldSupportFailOnStatusCode()
        {
            await ShouldSupportFailOnStatusCodeAsync("put").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "putshould support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PutShouldSupportIgnoreHttpsErrorsOption()
        {
            await ShouldSupportIgnoreHttpsErrorsOptionAsync("put").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should not add context cookie if cookie header passed as a parameter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAddContextCookieIfCookieHeaderPassedAsAParameter()
        {
            Assert.Ignore("Node __testHookLookup DNS hook");
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should follow redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFollowRedirects()
        {
            Assert.Ignore("Node __testHookLookup DNS hook");
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should follow redirects correctly when Location header contains UTF-8 characters")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFollowRedirectsCorrectlyWhenLocationHeaderContainsUtf8Characters()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string location = Prefix + "/empty.html?message=マスクПривет";
            string raw = "HTTP/1.1 301 Moved Permanently\r\nLocation: " + location + "\r\nConnection: close\r\n\r\n";
            RawHttpServer rawServer = await StartRawHttpAsync(Encoding.UTF8.GetBytes(raw)).ConfigureAwait(false);
            try
            {
                IAPIResponse response = await context.APIRequest.GetAsync(
                    "http://localhost:" + rawServer.Port.ToString(CultureInfo.InvariantCulture) + "/redirect").ConfigureAwait(false);
                string expected = EmptyPage + "?" + "message=" + Uri.EscapeDataString("マスクПривет");
                Assert.That(response.Url, Is.EqualTo(expected));
            }
            finally
            {
                await rawServer.DisposeAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should add cookies from Set-Cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddCookiesFromSetCookieHeader()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "session=value");
                http.Response.Headers.Append("Set-Cookie", "foo=bar; max-age=3600");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            HashSet<string> pairs = new HashSet<string>(cookies.Select(c => c.Name + "=" + c.Value), StringComparer.Ordinal);
            Assert.That(pairs, Is.EquivalentTo(new[] { "session=value", "foo=bar" }));
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string documentCookie = await page.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false);
            string[] sorted = documentCookie.Split(';').Select(s => s.Trim()).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            Assert.That(sorted, Is.EqualTo(new[] { "foo=bar", "session=value" }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should preserve cookie order from Set-Cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreserveCookieOrderFromSetCookieHeader()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "cookie.0=foo");
                http.Response.Headers.Append("Set-Cookie", "cookie.1=bar");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Select(c => c.Name + "=" + c.Value).ToArray(), Is.EqualTo(new[] { "cookie.0=foo", "cookie.1=bar" }));
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false), Is.EqualTo("cookie.0=foo; cookie.1=bar"));
            Task<string> requestPromise = Server.WaitForRequest("/empty.html", req => req.Headers["Cookie"].ToString());
            await page.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            string cookieHeader = await requestPromise.ConfigureAwait(false);
            Assert.That(cookieHeader, Is.EqualTo("cookie.0=foo; cookie.1=bar"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support cookie with empty value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCookieWithEmptyValue()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "first=");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => document.cookie)()").ConfigureAwait(false), Is.EqualTo("first="));
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Select(c => c.Name + "=" + c.Value).ToArray(), Is.EqualTo(new[] { "first=" }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should not lose body while handling Set-Cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotLoseBodyWhileHandlingSetCookieHeader()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "session=value");
                http.Response.Headers.Append("Set-Cookie", "foo=bar; max-age=3600");
                await http.Response.WriteAsync("text content").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("text content"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should remove cookie with negative max-age")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookieWithNegativeMaxAge()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=v; max-age=100000");
                http.Response.Headers.Append("Set-Cookie", "b=v; max-age=100000");
                http.Response.Headers.Append("Set-Cookie", "c=v");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            Server.SetRoute("/removecookie.html", async http =>
            {
                long maxAge = -2L * DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                http.Response.Headers.Append("Set-Cookie", "a=v; max-age=" + maxAge.ToString(CultureInfo.InvariantCulture));
                http.Response.Headers.Append("Set-Cookie", "b=v; max-age=-1");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            await page.APIRequest.GetAsync(Prefix + "/removecookie.html").ConfigureAwait(false);
            Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["Cookie"].ToString());
            await page.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await serverRequest.ConfigureAwait(false), Is.EqualTo("c=v"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should remove cookie with expires far in the past")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookieWithExpiresFarInThePast()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=v; max-age=1000000");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            Server.SetRoute("/removecookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=v; expires=Wed, 01 Jan 1000 00:00:00 GMT");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            await page.APIRequest.GetAsync(Prefix + "/removecookie.html").ConfigureAwait(false);
            Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["Cookie"].ToString());
            await page.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(string.IsNullOrEmpty(await serverRequest.ConfigureAwait(false)), Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should handle cookies on redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleCookiesOnRedirects()
        {
            EnsureServer();
            Server.SetRoute("/redirect1", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "r1=v1;SameSite=Lax");
                http.Response.StatusCode = 301;
                http.Response.Headers["Location"] = "/a/b/redirect2";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            Server.SetRoute("/a/b/redirect2", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "r2=v2;SameSite=Lax");
                http.Response.StatusCode = 302;
                http.Response.Headers["Location"] = "/title.html";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            {
                Task<string> req1 = Server.WaitForRequest("/redirect1", req => req.Headers["Cookie"].ToString());
                Task<string> req2 = Server.WaitForRequest("/a/b/redirect2", req => req.Headers["Cookie"].ToString());
                Task<string> req3 = Server.WaitForRequest("/title.html", req => req.Headers["Cookie"].ToString());
                await context.APIRequest.GetAsync(Prefix + "/redirect1").ConfigureAwait(false);
                Assert.That(string.IsNullOrEmpty(await req1.ConfigureAwait(false)), Is.True);
                Assert.That(await req2.ConfigureAwait(false), Is.EqualTo("r1=v1"));
                Assert.That(await req3.ConfigureAwait(false), Is.EqualTo("r1=v1"));
            }

            {
                Task<string> req1 = Server.WaitForRequest("/redirect1", req => req.Headers["Cookie"].ToString());
                Task<string> req2 = Server.WaitForRequest("/a/b/redirect2", req => req.Headers["Cookie"].ToString());
                Task<string> req3 = Server.WaitForRequest("/title.html", req => req.Headers["Cookie"].ToString());
                await context.APIRequest.GetAsync(Prefix + "/redirect1").ConfigureAwait(false);
                Assert.That(await req1.ConfigureAwait(false), Is.EqualTo("r1=v1"));
                string[] second = (await req2.ConfigureAwait(false)).Split(';').Select(s => s.Trim()).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                Assert.That(second, Is.EqualTo(new[] { "r1=v1", "r2=v2" }));
                Assert.That(await req3.ConfigureAwait(false), Is.EqualTo("r1=v1"));
            }

            SameSiteAttribute sameSite = TestConstants.IsWebKit && OperatingSystem.IsWindows()
                ? SameSiteAttribute.None
                : SameSiteAttribute.Lax;
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            BrowserContextCookiesResult r2 = cookies.Single(c => c.Name == "r2");
            BrowserContextCookiesResult r1 = cookies.Single(c => c.Name == "r1");
            Assert.That(r2.SameSite, Is.EqualTo(sameSite));
            Assert.That(r2.Value, Is.EqualTo("v2"));
            Assert.That(r2.Domain, Is.EqualTo(Hostname));
            Assert.That(r2.Path, Is.EqualTo("/a/b"));
            Assert.That(r2.Expires, Is.EqualTo(-1));
            Assert.That(r2.HttpOnly, Is.False);
            Assert.That(r2.Secure, Is.False);
            Assert.That(r1.SameSite, Is.EqualTo(sameSite));
            Assert.That(r1.Value, Is.EqualTo("v1"));
            Assert.That(r1.Domain, Is.EqualTo(Hostname));
            Assert.That(r1.Path, Is.EqualTo("/"));
            Assert.That(r1.Expires, Is.EqualTo(-1));
            Assert.That(r1.HttpOnly, Is.False);
            Assert.That(r1.Secure, Is.False);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should return raw headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnRawHeaders()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string raw = "HTTP/1.1 200 OK\r\nName-A: v1\r\nname-b: v4\r\nName-a: v2\r\nname-A: v3\r\nConnection: close\r\n\r\n";
            RawHttpServer rawServer = await StartRawHttpAsync(Encoding.ASCII.GetBytes(raw)).ConfigureAwait(false);
            try
            {
                IAPIResponse response = await context.APIRequest.GetAsync(
                    "http://localhost:" + rawServer.Port.ToString(CultureInfo.InvariantCulture) + "/headers").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Header[] headers = response.HeadersArray
                    .Where(h => h.Name != null && h.Name.Contains("name-", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Assert.That(headers, Is.EqualTo(new[]
                {
                    new Header { Name = "Name-A", Value = "v1" },
                    new Header { Name = "name-b", Value = "v4" },
                    new Header { Name = "Name-a", Value = "v2" },
                    new Header { Name = "name-A", Value = "v3" },
                }));
                Assert.That(response.Headers["name-a"], Is.EqualTo("v1, v2, v3"));
                Assert.That(response.Headers["name-b"], Is.EqualTo("v4"));
            }
            finally
            {
                await rawServer.DisposeAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should work with http credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithHttpCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<string> request = Server.WaitForRequest("/empty.html", req => req.Path.Value);
            IAPIResponse response = await context.APIRequest.GetAsync(EmptyPage, new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass")),
                }
            }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await request.ConfigureAwait(false), Is.EqualTo("/empty.html"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should work with setHTTPCredentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithSetHttpCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response1 = await context.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(401));
            await context.SetHttpCredentialsAsync(new HttpCredentials { Username = "user", Password = "pass" }).ConfigureAwait(false);
            IAPIResponse response2 = await context.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should return error with wrong credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnErrorWithWrongCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new HttpCredentials { Username = "user", Password = "wrong" }).ConfigureAwait(false);
            IAPIResponse response2 = await context.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(401));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support multiple httpCredentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipleHttpCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user1", "pass1");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new[]
            {
                new HttpCredentials { Username = "user1", Password = "pass1", Origin = Prefix },
                new HttpCredentials { Username = "user2", Password = "pass2", Origin = CrossProcessPrefix },
            }).ConfigureAwait(false);
            IAPIResponse response1 = await context.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(200));
            IAPIResponse response2 = await context.APIRequest.GetAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(401));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support HTTPCredentials.send for newContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHttpCredentialsSendForNewContext()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "user",
                    Password = "pass",
                    Origin = Prefix.ToUpperInvariant(),
                    Send = HttpCredentialsSend.Always,
                }
            }).ConfigureAwait(false);
            {
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["Authorization"].ToString());
                IAPIResponse response = await context.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await serverRequest.ConfigureAwait(false), Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"))));
                Assert.That(response.Status, Is.EqualTo(200));
            }

            {
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["Authorization"].ToString());
                IAPIResponse response = await context.APIRequest.GetAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
                Assert.That(string.IsNullOrEmpty(await serverRequest.ConfigureAwait(false)), Is.True);
                Assert.That(response.Status, Is.EqualTo(200));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support HTTPCredentials.send for browser.newPage")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHttpCredentialsSendForBrowserNewPage()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "user",
                    Password = "pass",
                    Origin = Prefix.ToUpperInvariant(),
                    Send = HttpCredentialsSend.Always,
                }
            }).ConfigureAwait(false);
            {
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["Authorization"].ToString());
                IAPIResponse response = await page.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await serverRequest.ConfigureAwait(false), Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"))));
                Assert.That(response.Status, Is.EqualTo(200));
            }

            {
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["Authorization"].ToString());
                IAPIResponse response = await page.APIRequest.GetAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
                Assert.That(string.IsNullOrEmpty(await serverRequest.ConfigureAwait(false)), Is.True);
                Assert.That(response.Status, Is.EqualTo(200));
            }

            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "delete should support post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DeleteShouldSupportPostData()
        {
            await ShouldSupportPostDataAsync("delete").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "get should support post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetShouldSupportPostData()
        {
            await ShouldSupportPostDataAsync("get").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "head should support post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HeadShouldSupportPostData()
        {
            await ShouldSupportPostDataAsync("head").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "patch should support post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PatchShouldSupportPostData()
        {
            await ShouldSupportPostDataAsync("patch").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "post should support post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PostShouldSupportPostData()
        {
            await ShouldSupportPostDataAsync("post").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "put should support post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PutShouldSupportPostData()
        {
            await ShouldSupportPostDataAsync("put").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should add default headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddDefaultHeaders()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> request = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            HttpRequestSnapshot seen = await request.ConfigureAwait(false);
            Assert.That(seen.Accept, Is.EqualTo("*/*"));
            string userAgent = await page.EvaluateAsync<string>("(() => navigator.userAgent)()").ConfigureAwait(false);
            Assert.That(seen.UserAgent, Is.EqualTo(userAgent));
            Assert.That(seen.AcceptEncoding, Is.EqualTo("gzip,deflate,br"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should send content-length")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendContentLength()
        {
            EnsureServer();
            byte[] bytes = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                bytes[i] = (byte)i;
            }

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> request = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.PostAsync(EmptyPage, new() { DataByte = bytes }).ConfigureAwait(false);
            HttpRequestSnapshot seen = await request.ConfigureAwait(false);
            Assert.That(seen.ContentLength, Is.EqualTo("256"));
            Assert.That(seen.ContentType, Is.EqualTo("application/octet-stream"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should add default headers to redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddDefaultHeadersToRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/redirect", "/empty.html");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> request = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.GetAsync(Prefix + "/redirect").ConfigureAwait(false);
            HttpRequestSnapshot seen = await request.ConfigureAwait(false);
            Assert.That(seen.Accept, Is.EqualTo("*/*"));
            string userAgent = await page.EvaluateAsync<string>("(() => navigator.userAgent)()").ConfigureAwait(false);
            Assert.That(seen.UserAgent, Is.EqualTo(userAgent));
            Assert.That(seen.AcceptEncoding, Is.EqualTo("gzip,deflate,br"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should allow to override default headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowToOverrideDefaultHeaders()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> request = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.GetAsync(EmptyPage, new()
            {
                Headers = new Dictionary<string, string>
                {
                    ["User-Agent"] = "Playwright",
                    ["Accept"] = "text/html",
                    ["Accept-Encoding"] = "br",
                }
            }).ConfigureAwait(false);
            HttpRequestSnapshot seen = await request.ConfigureAwait(false);
            Assert.That(seen.Accept, Is.EqualTo("text/html"));
            Assert.That(seen.UserAgent, Is.EqualTo("Playwright"));
            Assert.That(seen.AcceptEncoding, Is.EqualTo("br"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should propagate custom headers with redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateCustomHeadersWithRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/simple.json");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<string> req1 = Server.WaitForRequest("/a/redirect1", req => req.Headers["foo"].ToString());
            Task<string> req2 = Server.WaitForRequest("/b/c/redirect2", req => req.Headers["foo"].ToString());
            Task<string> req3 = Server.WaitForRequest("/simple.json", req => req.Headers["foo"].ToString());
            await context.APIRequest.GetAsync(Prefix + "/a/redirect1", new() { Headers = new Dictionary<string, string> { ["foo"] = "bar" } }).ConfigureAwait(false);
            Assert.That(await req1.ConfigureAwait(false), Is.EqualTo("bar"));
            Assert.That(await req2.ConfigureAwait(false), Is.EqualTo("bar"));
            Assert.That(await req3.ConfigureAwait(false), Is.EqualTo("bar"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should propagate extra http headers with redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateExtraHttpHeadersWithRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/simple.json");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.SetExtraHttpHeadersAsync(new Dictionary<string, string> { ["My-Secret"] = "Value" }).ConfigureAwait(false);
            Task<string> req1 = Server.WaitForRequest("/a/redirect1", req => req.Headers["my-secret"].ToString());
            Task<string> req2 = Server.WaitForRequest("/b/c/redirect2", req => req.Headers["my-secret"].ToString());
            Task<string> req3 = Server.WaitForRequest("/simple.json", req => req.Headers["my-secret"].ToString());
            await context.APIRequest.GetAsync(Prefix + "/a/redirect1").ConfigureAwait(false);
            Assert.That(await req1.ConfigureAwait(false), Is.EqualTo("Value"));
            Assert.That(await req2.ConfigureAwait(false), Is.EqualTo("Value"));
            Assert.That(await req3.ConfigureAwait(false), Is.EqualTo("Value"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw on invalid header value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnInvalidHeaderValue()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/a/redirect1", new() { Headers = new Dictionary<string, string> { ["foo"] = "недопустимое значение" } })).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Invalid character in header content"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw on non-http(s) protocol")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnNonHttpProtocol()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error1 = await CatchAsync(() => context.APIRequest.GetAsync("data:text/plain,test")).ConfigureAwait(false);
            Assert.That(error1.Message, Does.Contain("Protocol \"data:\" not supported"));
            Exception error2 = await CatchAsync(() => context.APIRequest.GetAsync("file:///tmp/foo")).ConfigureAwait(false);
            Assert.That(error2.Message, Does.Contain("Protocol \"file:\" not supported"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support https")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHttps()
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(HttpsEmptyPage, new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should inherit ignoreHTTPSErrors from context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritIgnoreHttpsErrorsFromContext()
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(HttpsEmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should resolve url relative to baseURL")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveUrlRelativeToBaseUrl()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { BaseURL = Prefix }).ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync("/empty.html").ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(EmptyPage));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support gzip compression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportGzipCompression()
        {
            EnsureServer();
            Server.SetRoute("/compressed", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "gzip";
                http.Response.ContentType = "text/plain";
                await http.Response.Body.WriteAsync(CompressGzip(Encoding.UTF8.GetBytes("Hello, world!"))).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Hello, world!"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support case-insensitive content-encoding")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCaseInsensitiveContentEncoding()
        {
            EnsureServer();
            Server.SetRoute("/compressed-uppercase", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "GZIP";
                http.Response.ContentType = "text/plain";
                await http.Response.Body.WriteAsync(CompressGzip(Encoding.UTF8.GetBytes("Hello, world!"))).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed-uppercase").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Hello, world!"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw informative error on corrupted gzip body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowInformativeErrorOnCorruptedGzipBody()
        {
            EnsureServer();
            Server.SetRoute("/corrupted", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "gzip";
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("Hello, world!").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/corrupted")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("failed to decompress 'gzip' encoding"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support brotli compression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportBrotliCompression()
        {
            EnsureServer();
            Server.SetRoute("/compressed", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "br";
                http.Response.ContentType = "text/plain";
                await http.Response.Body.WriteAsync(CompressBrotli(Encoding.UTF8.GetBytes("Hello, world!"))).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Hello, world!"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw informative error on corrupted brotli body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowInformativeErrorOnCorruptedBrotliBody()
        {
            EnsureServer();
            Server.SetRoute("/corrupted", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "br";
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("Hello, world!").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/corrupted")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("failed to decompress 'br' encoding"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support deflate compression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportDeflateCompression()
        {
            EnsureServer();
            Server.SetRoute("/compressed", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "deflate";
                http.Response.ContentType = "text/plain";
                await http.Response.Body.WriteAsync(CompressDeflate(Encoding.UTF8.GetBytes("Hello, world!"))).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/compressed").ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Hello, world!"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw informative error on corrupted deflate body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowInformativeErrorOnCorruptedDeflateBody()
        {
            EnsureServer();
            Server.SetRoute("/corrupted", async http =>
            {
                http.Response.Headers["Content-Encoding"] = "deflate";
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("Hello, world!").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/corrupted")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("failed to decompress 'deflate' encoding"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support timeout option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTimeoutOption()
        {
            EnsureServer();
            Server.SetRoute("/slow", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentLength = 4096;
                http.Response.ContentType = "text/html";
                await http.Response.StartAsync().ConfigureAwait(false);
                await Task.Delay(Timeout.Infinite, http.RequestAborted).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/slow", new() { Timeout = 10 })).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("apiRequestContext.get: Timeout 10ms exceeded"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support a timeout of 0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportATimeoutOf0()
        {
            EnsureServer();
            Server.SetRoute("/slow", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentLength = 4;
                http.Response.ContentType = "text/html";
                await Task.Delay(50).ConfigureAwait(false);
                await http.Response.WriteAsync("done").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/slow", new() { Timeout = 0 }).ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("done"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should respect timeout after redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectTimeoutAfterRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/redirect", "/slow");
            Server.SetRoute("/slow", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentLength = 4096;
                http.Response.ContentType = "text/html";
                await http.Response.StartAsync().ConfigureAwait(false);
                await Task.Delay(Timeout.Infinite, http.RequestAborted).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            context.SetDefaultTimeout(100);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(Prefix + "/redirect")).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("apiRequestContext.get: Timeout 100ms exceeded"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should not hang on a brotli encoded Range request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangOnABrotliEncodedRangeRequest()
        {
            Assert.Ignore("Node-only HTTP parser");
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispose()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
            JsonElement? json = await response.JsonAsync().ConfigureAwait(false);
            Assert.That(json.HasValue, Is.True);
            Assert.That(json.Value.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            await response.DisposeAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => response.BodyAsync()).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Response has been disposed"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should dispose when context closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDisposeWhenContextCloses()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
            JsonElement? json = await response.JsonAsync().ConfigureAwait(false);
            Assert.That(json.HasValue, Is.True);
            Assert.That(json.Value.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            await context.CloseAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => response.BodyAsync()).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Response has been disposed"));
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should override request parameters")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverrideRequestParameters()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IRequest> pageReqTask = page.WaitForRequestAsync("**/*");
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IRequest pageReq = await pageReqTask.ConfigureAwait(false);
            Task<HttpRequestSnapshot> req = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.FetchAsync(
                pageReq,
                method: "POST",
                headers: new Dictionary<string, string> { ["foo"] = "bar" },
                data: "data").ConfigureAwait(false);
            HttpRequestSnapshot seen = await req.ConfigureAwait(false);
            Assert.That(seen.Method, Is.EqualTo("POST"));
            Assert.That(seen.Foo, Is.EqualTo("bar"));
            Assert.That(seen.Body, Is.EqualTo("data"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support application/x-www-form-urlencoded")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportApplicationXWwwFormUrlencoded()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IFormData form = context.APIRequest.CreateFormData();
            form.Set("firstName", "John").Set("lastName", "Doe").Set("file", "f.js");
            Task<HttpRequestSnapshot> req = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.PostAsync(EmptyPage, new() { Form = form }).ConfigureAwait(false);
            HttpRequestSnapshot seen = await req.ConfigureAwait(false);
            Assert.That(seen.Method, Is.EqualTo("POST"));
            Assert.That(seen.ContentType, Is.EqualTo("application/x-www-form-urlencoded"));
            Assert.That(seen.ContentLength, Is.EqualTo(seen.Body.Length.ToString(CultureInfo.InvariantCulture)));
            Assert.That(QueryValue(seen.Body, "firstName"), Is.EqualTo("John"));
            Assert.That(QueryValue(seen.Body, "lastName"), Is.EqualTo("Doe"));
            Assert.That(QueryValue(seen.Body, "file"), Is.EqualTo("f.js"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support application/x-www-form-urlencoded with param lists")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportApplicationXWwwFormUrlencodedWithParamLists()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IFormData form = context.APIRequest.CreateFormData();
            form.Append("foo", "1").Append("foo", "2");
            Task<HttpRequestSnapshot> req = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.PostAsync(EmptyPage, new() { Form = form }).ConfigureAwait(false);
            HttpRequestSnapshot seen = await req.ConfigureAwait(false);
            Assert.That(seen.Method, Is.EqualTo("POST"));
            Assert.That(seen.ContentType, Is.EqualTo("application/x-www-form-urlencoded"));
            Assert.That(seen.ContentLength, Is.EqualTo(seen.Body.Length.ToString(CultureInfo.InvariantCulture)));
            string[] values = ParseQueryAll(seen.Body, "foo");
            Assert.That(values, Is.EqualTo(new[] { "1", "2" }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should encode to application/json by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEncodeToApplicationJsonByDefault()
        {
            EnsureServer();
            var data = new
            {
                firstName = "John",
                lastName = "Doe",
                file = new { name = "f.js" },
            };
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> req = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.PostAsync(EmptyPage, new() { DataObject = data }).ConfigureAwait(false);
            HttpRequestSnapshot seen = await req.ConfigureAwait(false);
            Assert.That(seen.Method, Is.EqualTo("POST"));
            Assert.That(seen.ContentType, Is.EqualTo("application/json"));
            using JsonDocument parsed = JsonDocument.Parse(seen.Body);
            Assert.That(parsed.RootElement.GetProperty("firstName").GetString(), Is.EqualTo("John"));
            Assert.That(parsed.RootElement.GetProperty("lastName").GetString(), Is.EqualTo("Doe"));
            Assert.That(parsed.RootElement.GetProperty("file").GetProperty("name").GetString(), Is.EqualTo("f.js"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support multipart/form-data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipartFormData()
        {
            EnsureServer();
            TaskCompletionSource<HttpRequestSnapshot> formReceived = new TaskCompletionSource<HttpRequestSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/empty.html", async http =>
            {
                formReceived.TrySetResult(Capture(http.Request));
                http.Response.StatusCode = 200;
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            FilePayload file = new FilePayload
            {
                Name = "f.js",
                MimeType = "text/javascript",
                Buffer = Encoding.UTF8.GetBytes("var x = 10;\r\n;console.log(x);"),
            };
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IFormData multipart = context.APIRequest.CreateFormData();
            multipart.Set("firstName", "John").Set("middleName", string.Empty).Set("lastName", "Doe").Set("file", file);
            Task<IAPIResponse> responseTask = context.APIRequest.PostAsync(EmptyPage, new() { Multipart = multipart });
            HttpRequestSnapshot serverRequest = await formReceived.Task.ConfigureAwait(false);
            IAPIResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(serverRequest.Method, Is.EqualTo("POST"));
            Assert.That(serverRequest.ContentType, Does.Contain("multipart/form-data"));
            Assert.That(serverRequest.Body, Does.Contain("John"));
            Assert.That(serverRequest.Body, Does.Contain("Doe"));
            Assert.That(serverRequest.Body, Does.Contain("f.js"));
            Assert.That(serverRequest.Body, Does.Contain("text/javascript"));
            Assert.That(serverRequest.Body, Does.Contain("var x = 10;\r\n;console.log(x);"));
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support multipart/form-data with ReadStream values")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipartFormDataWithReadStreamValues()
        {
            EnsureServer();
            byte[] simpleZip = CreateSimpleZipBytes();
            TaskCompletionSource<HttpRequestSnapshot> formReceived = new TaskCompletionSource<HttpRequestSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/empty.html", async http =>
            {
                formReceived.TrySetResult(Capture(http.Request));
                http.Response.StatusCode = 200;
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IFormData multipart = context.APIRequest.CreateFormData();
            multipart.Set("firstName", "John").Set("lastName", "Doe");
            multipart.Set("readStream", new FilePayload
            {
                Name = "simplezip.json",
                MimeType = "application/json",
                Buffer = simpleZip,
            });
            Task<IAPIResponse> responseTask = context.APIRequest.PostAsync(EmptyPage, new() { Multipart = multipart });
            HttpRequestSnapshot serverRequest = await formReceived.Task.ConfigureAwait(false);
            IAPIResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(serverRequest.Method, Is.EqualTo("POST"));
            Assert.That(serverRequest.ContentType, Does.Contain("multipart/form-data"));
            Assert.That(serverRequest.ContentLength, Does.Contain("5498"));
            Assert.That(serverRequest.Body, Does.Contain("John"));
            Assert.That(serverRequest.Body, Does.Contain("Doe"));
            Assert.That(serverRequest.Body, Does.Contain("simplezip.json"));
            Assert.That(serverRequest.Body, Does.Contain("application/json"));
            Assert.That(serverRequest.Body, Does.Contain(Encoding.UTF8.GetString(simpleZip)));
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support multipart/form-data and keep the order")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipartFormDataAndKeepTheOrder()
        {
            EnsureServer();
            TaskCompletionSource<HttpRequestSnapshot> formReceived = new TaskCompletionSource<HttpRequestSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/empty.html", async http =>
            {
                formReceived.TrySetResult(Capture(http.Request));
                http.Response.StatusCode = 200;
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IFormData multipart = context.APIRequest.CreateFormData();
            multipart.Set("firstName", "John").Set("lastName", "Doe").Set("age", 27).Set("foo", "bar");
            Task<IAPIResponse> responseTask = context.APIRequest.PostAsync(EmptyPage, new() { Multipart = multipart });
            HttpRequestSnapshot serverRequest = await formReceived.Task.ConfigureAwait(false);
            IAPIResponse response = await responseTask.ConfigureAwait(false);
            string[] actualKeys = ParseMultipartNames(serverRequest.Body);
            Assert.That(actualKeys, Is.EqualTo(new[] { "firstName", "lastName", "age", "foo" }));
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support repeating names in multipart/form-data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportRepeatingNamesInMultipartFormData()
        {
            EnsureServer();
            TaskCompletionSource<string> postBodyPromise = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/empty.html", async http =>
            {
                postBodyPromise.TrySetResult(ReadBody(http.Request));
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("OK.").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IFormData formData = context.APIRequest.CreateFormData();
            formData.Set("name", "John");
            formData.Append("name", "Doe");
            formData.Append("file", new FilePayload
            {
                Name = "f1.js",
                MimeType = "text/javascript",
                Buffer = Encoding.UTF8.GetBytes("var x = 10;\r\n;console.log(x);"),
            });
            formData.Append("file", new FilePayload
            {
                Name = "custom_f2.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello"),
            });
            formData.Append("file", new FilePayload
            {
                Name = "blob",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("boo"),
            });
            Task<IAPIResponse> responseTask = context.APIRequest.PostAsync(EmptyPage, new() { Multipart = formData });
            string postBody = await postBodyPromise.Task.ConfigureAwait(false);
            IAPIResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(postBody, Does.Contain("content-disposition: form-data; name=\"name\"\r\n\r\nJohn"));
            Assert.That(postBody, Does.Contain("content-disposition: form-data; name=\"name\"\r\n\r\nDoe"));
            Assert.That(postBody, Does.Contain("content-disposition: form-data; name=\"file\"; filename=\"f1.js\"\r\ncontent-type: text/javascript\r\n\r\nvar x = 10;\r\n;console.log(x);"));
            Assert.That(postBody, Does.Contain("content-disposition: form-data; name=\"file\"; filename=\"custom_f2.txt\"\r\ncontent-type: text/plain\r\n\r\nhello"));
            Assert.That(postBody, Does.Contain("content-disposition: form-data; name=\"file\"; filename=\"blob\"\r\ncontent-type: text/plain\r\n\r\nboo"));
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should serialize data to json regardless of content-type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSerializeDataToJsonRegardlessOfContentType()
        {
            EnsureServer();
            var data = new
            {
                firstName = "John",
                lastName = "Doe",
            };
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> req = Server.WaitForRequest("/empty.html", Capture);
            await context.APIRequest.PostAsync(EmptyPage, new() { Headers = new Dictionary<string, string> { ["content-type"] = "unknown" }, DataObject = data }).ConfigureAwait(false);
            HttpRequestSnapshot seen = await req.ConfigureAwait(false);
            Assert.That(seen.Method, Is.EqualTo("POST"));
            Assert.That(seen.ContentType, Is.EqualTo("unknown"));
            Assert.That(seen.Body, Is.EqualTo(JsonSerializer.Serialize(data)));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should throw nice error on unsupported data type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowNiceErrorOnUnsupportedDataType()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            // Official: data: () => true. C# PostAsync `data` is string-only.
            // Unexpected payloads must still reject with Unexpected 'data' type.
            Exception error = await CatchAsync(() => context.APIRequest.PostAsync(EmptyPage, new() { Headers = new Dictionary<string, string> { ["content-type"] = "application/json" }, DataObject = (Func<bool>)(() => true) })).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Unexpected 'data' type"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "context request should export same storage state as context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextRequestShouldExportSameStorageStateAsContext()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=b");
                http.Response.Headers.Append("Set-Cookie", "c=d");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            string contextState = await context.StorageStateAsync().ConfigureAwait(false);
            using (JsonDocument doc = JsonDocument.Parse(contextState))
            {
                Assert.That(doc.RootElement.GetProperty("cookies").GetArrayLength(), Is.EqualTo(2));
            }

            string requestState = await context.APIRequest.StorageStateAsync().ConfigureAwait(false);
            Assert.That(requestState, Is.EqualTo(contextState));
            string pageState = await page.APIRequest.StorageStateAsync().ConfigureAwait(false);
            Assert.That(pageState, Is.EqualTo(contextState));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should send secure cookie over http for localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendSecureCookieOverHttpForLocalhost()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=v; secure");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.APIRequest.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["Cookie"].ToString());
            await page.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await serverRequest.ConfigureAwait(false), Is.EqualTo("a=v"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should accept bool and numeric params and filter out undefined")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptBoolAndNumericParamsAndFilterOutUndefined()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<string> request = Server.WaitForRequest("/empty.html", req => req.QueryString.Value ?? string.Empty);
            await page.APIRequest.GetAsync(EmptyPage, new APIRequestContextOptions
            {
                Params = new[]
                {
                    new KeyValuePair<string, object>("str", "s"),
                    new KeyValuePair<string, object>("num", "10"),
                    new KeyValuePair<string, object>("bool", "true"),
                    new KeyValuePair<string, object>("bool2", "false"),
                }
            }).ConfigureAwait(false);
            string query = await request.ConfigureAwait(false);
            Assert.That(QueryValue(query, "str"), Is.EqualTo("s"));
            Assert.That(QueryValue(query, "num"), Is.EqualTo("10"));
            Assert.That(QueryValue(query, "bool"), Is.EqualTo("true"));
            Assert.That(QueryValue(query, "bool2"), Is.EqualTo("false"));
            Assert.That(QueryValue(query, "none"), Is.Null);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should abort requests when browser context closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbortRequestsWhenBrowserContextCloses()
        {
            EnsureServer();
            TaskCompletionSource<object> connectionClosed = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/empty.html", http =>
            {
                http.RequestAborted.Register(() => connectionClosed.TrySetResult(null));
                return Task.Delay(Timeout.Infinite, http.RequestAborted);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<Exception> getError = CatchAsync(() => context.APIRequest.GetAsync(EmptyPage));
            Task<Exception> postError = CatchAsync(() => context.APIRequest.PostAsync(EmptyPage));
            Task closeAfterRequest = Server.WaitForRequest("/empty.html").ContinueWith(
                _ => context.CloseAsync(),
                TaskScheduler.Default).Unwrap();
            await Task.WhenAll(getError, postError, closeAfterRequest).ConfigureAwait(false);
            Exception error = await getError.ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(
                error.Message,
                Does.Match("Request context disposed|Target page, context or browser has been closed"));
            await connectionClosed.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should work with connectOverCDP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithConnectOverCdp()
        {
            Assert.Ignore("Node browserType.connectOverCDP");
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support SameSite cookie attribute over https")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportSameSiteCookieAttributeOverHttps()
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            foreach (string value in new[] { "None", "Lax", "Strict" })
            {
                HttpsServer.SetRoute("/empty.html", async http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "SID=2022; Path=/; Secure; SameSite=" + value);
                    await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
                });
                await page.APIRequest.GetAsync(HttpsEmptyPage).ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync().ConfigureAwait(false);
                BrowserContextCookiesResult cookie = cookies[0];
                if (TestConstants.IsWebKit && OperatingSystem.IsWindows())
                {
                    Assert.That(cookie.SameSite, Is.EqualTo(SameSiteAttribute.None));
                }
                else
                {
                    Assert.That(cookie.SameSite, Is.EqualTo(ParseSameSite(value)));
                }
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should set domain=localhost cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetDomainLocalhostCookie()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name=val; Domain=" + Hostname + "; Path=/;");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            BrowserContextCookiesResult cookie = cookies[0];
            Assert.That(cookie, Is.Not.Null);
            Assert.That(cookie.Name, Is.EqualTo("name"));
            Assert.That(cookie.Value, Is.EqualTo("val"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "fetch should not throw on long set-cookie value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldNotThrowOnLongSetCookieValue()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "foo=" + new string('a', 4100) + "; path=/;");
                http.Response.Headers.Append("Set-Cookie", "bar=val");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.APIRequest.GetAsync(EmptyPage, new() { Timeout = 5000 }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Select(c => c.Name), Does.Contain("bar"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should support set-cookie with SameSite and without Secure attribute over HTTP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportSetCookieWithSameSiteAndWithoutSecureAttributeOverHttp()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            foreach (string value in new[] { "None", "Lax", "Strict" })
            {
                Server.SetRoute("/empty.html", async http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "SID=2022; Path=/; SameSite=" + value);
                    await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
                });
                await page.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync().ConfigureAwait(false);
                BrowserContextCookiesResult cookie = cookies.Count > 0 ? cookies[0] : null;
                bool isLinux = !TestConstants.IsWindows && !TestConstants.IsMacOSX;
                if (TestConstants.IsChromium && value == "None")
                {
                    Assert.That(cookie, Is.Null);
                }
                else if (TestConstants.IsWebKit && isLinux && value == "None")
                {
                    Assert.That(cookie, Is.Null);
                }
                else if (TestConstants.IsWebKit && OperatingSystem.IsWindows())
                {
                    Assert.That(cookie.SameSite, Is.EqualTo(SameSiteAttribute.None));
                }
                else
                {
                    Assert.That(cookie.SameSite, Is.EqualTo(ParseSameSite(value)));
                }
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should update host header on redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUpdateHostHeaderOnRedirect()
        {
            EnsureServer();
            int redirectCount = 0;
            Server.SetRoute("/redirect", async http =>
            {
                redirectCount++;
                string path = string.Equals(http.Request.Host.Value, new Uri(Prefix).Authority, StringComparison.Ordinal)
                    ? "/redirect"
                    : "/test";
                http.Response.StatusCode = 302;
                http.Response.Headers["Location"] = CrossProcessPrefix + path;
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            Server.SetRoute("/test", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("Hello!").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<string> reqPromise = Server.WaitForRequest("/test", req => req.Host.Value);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/redirect", new() { Headers = new Dictionary<string, string> { ["HosT"] = new Uri(Prefix).Authority } }).ConfigureAwait(false);
            Assert.That(redirectCount, Is.EqualTo(2));
            await Assertions.Expect(response).ToBeOKAsync().ConfigureAwait(false);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Hello!"));
            Assert.That(await reqPromise.ConfigureAwait(false), Is.EqualTo(new Uri(CrossProcessPrefix).Authority));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should not work after dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotWorkAfterDispose()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.APIRequest.DisposeAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(EmptyPage)).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Target page, context or browser has been closed"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should not work after context dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotWorkAfterContextDispose()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.CloseAsync("Test ended.").ConfigureAwait(false);
            Exception error = await CatchAsync(() => context.APIRequest.GetAsync(EmptyPage)).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("Test ended."));
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should retry on ECONNRESET")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRetryOnEconnreset()
        {
            EnsureServer();
            int requestCount = 0;
            Server.SetRoute("/test", http =>
            {
                if (requestCount++ < 3)
                {
                    http.Abort();
                    return Task.CompletedTask;
                }

                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("Hello!");
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/test", new() { MaxRetries = 3 }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Hello!"));
            Assert.That(requestCount, Is.EqualTo(4));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should retry ECONNRESET on compressed response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRetryEconnresetOnCompressedResponse()
        {
            EnsureServer();
            int requestCount = 0;
            Server.SetRoute("/test-gzip", async http =>
            {
                if (requestCount++ < 2)
                {
                    http.Abort();
                    return;
                }

                http.Response.Headers["Content-Encoding"] = "gzip";
                http.Response.ContentType = "text/plain";
                await http.Response.Body.WriteAsync(CompressGzip(Encoding.UTF8.GetBytes("compressed-retry-ok"))).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/test-gzip", new() { MaxRetries = 3 }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("compressed-retry-ok"));
            Assert.That(requestCount, Is.EqualTo(3));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-fetch.spec.ts", "should retry ECONNRESET mid-stream during gzip decompression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRetryEconnresetMidStreamDuringGzipDecompression()
        {
            EnsureServer();
            int requestCount = 0;
            Server.SetRoute("/test-gzip-midstream", async http =>
            {
                requestCount++;
                if (requestCount <= 2)
                {
                    http.Response.Headers["Content-Encoding"] = "gzip";
                    http.Response.ContentType = "text/plain";
                    await http.Response.StartAsync().ConfigureAwait(false);
                    http.Abort();
                    return;
                }

                http.Response.Headers["Content-Encoding"] = "gzip";
                http.Response.ContentType = "text/plain";
                await http.Response.Body.WriteAsync(CompressGzip(Encoding.UTF8.GetBytes("midstream-retry-ok"))).ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(Prefix + "/test-gzip-midstream", new() { MaxRetries = 3 }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("midstream-retry-ok"));
            Assert.That(requestCount, Is.EqualTo(3));
            await context.CloseAsync().ConfigureAwait(false);
        }


        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new List<IBrowserContext>(_browser.Contexts))
            {
                try
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
                return;
            }

            string certPath = Path.Combine(contentRoot, "testCert.cer");
            if (!File.Exists(certPath))
            {
                return;
            }

            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19941;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
                    await https.StartAsync().ConfigureAwait(false);
                    _ownedHttps = https;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    HttpsPrefix = "https://localhost:" + portText;
                    HttpsEmptyPage = HttpsPrefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static async Task PollEqualAsync<T>(Func<Task<T>> getValue, T expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            T last = default;
            while (DateTime.UtcNow < deadline)
            {
                last = await getValue().ConfigureAwait(false);
                if (Equals(last, expected))
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(last, Is.EqualTo(expected));
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

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static SameSiteAttribute ParseSameSite(string value)
        {
            if (string.Equals(value, "None", StringComparison.Ordinal))
            {
                return SameSiteAttribute.None;
            }

            if (string.Equals(value, "Strict", StringComparison.Ordinal))
            {
                return SameSiteAttribute.Strict;
            }

            return SameSiteAttribute.Lax;
        }

        private async Task ShouldSupportParamsPassedAsObjectAsync(string method)
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> requestTask = Server.WaitForRequest("/empty.html", Capture);
            IAPIResponse response = await CallAsync(
                context.APIRequest,
                method,
                EmptyPage,
                queryParams: new[]
                {
                    new KeyValuePair<string, string>("param1", "value1"),
                    new KeyValuePair<string, string>("парам2", "знач2"),
                }.AsObjectPairs()).ConfigureAwait(false);
            HttpRequestSnapshot request = await requestTask.ConfigureAwait(false);
            Assert.That(QueryValue(request.Query, "param1"), Is.EqualTo("value1"));
            Assert.That(QueryValue(request.Query, "парам2"), Is.EqualTo("знач2"));
            Uri responseUri = new Uri(response.Url);
            Assert.That(QueryValue(responseUri.Query, "param1"), Is.EqualTo("value1"));
            Assert.That(QueryValue(responseUri.Query, "парам2"), Is.EqualTo("знач2"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldSupportParamsPassedAsUrlSearchParamsAsync(string method)
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> requestTask = Server.WaitForRequest("/empty.html", Capture);
            IAPIResponse response = await CallAsync(
                context.APIRequest,
                method,
                EmptyPage,
                queryParams: new[]
                {
                    new KeyValuePair<string, string>("param1", "value1"),
                    new KeyValuePair<string, string>("param1", "value2"),
                    new KeyValuePair<string, string>("парам2", "знач2"),
                }.AsObjectPairs()).ConfigureAwait(false);
            HttpRequestSnapshot request = await requestTask.ConfigureAwait(false);
            string[] requestParam1 = ParseQueryAll(request.Query ?? string.Empty, "param1");
            Assert.That(requestParam1, Is.EqualTo(new[] { "value1", "value2" }));
            Assert.That(QueryValue(request.Query, "парам2"), Is.EqualTo("знач2"));
            Uri responseUri = new Uri(response.Url);
            string[] responseParam1 = ParseQueryAll(responseUri.Query, "param1");
            Assert.That(responseParam1, Is.EqualTo(new[] { "value1", "value2" }));
            Assert.That(QueryValue(responseUri.Query, "парам2"), Is.EqualTo("знач2"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldSupportParamsPassedAsStringAsync(string method)
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            // Official: params as string '?param1=value1&param1=value2&парам2=знач2'.
            // IAPIRequestContext has no string params overload; pass the query on the URL.
            // Official encodeURI('?param1=value1&param1=value2&парам2=знач2') — encode names/values only.
            string encoded = "?param1=value1&param1=value2&" + Uri.EscapeDataString("парам2") + "=" + Uri.EscapeDataString("знач2");
            Task<HttpRequestSnapshot> requestTask = Server.WaitForRequest("/empty.html", Capture);
            IAPIResponse response = await CallAsync(context.APIRequest, method, EmptyPage + encoded).ConfigureAwait(false);
            HttpRequestSnapshot request = await requestTask.ConfigureAwait(false);
            string[] requestParam1 = ParseQueryAll(request.Query ?? string.Empty, "param1");
            Assert.That(requestParam1, Is.EqualTo(new[] { "value1", "value2" }));
            Assert.That(QueryValue(request.Query, "парам2"), Is.EqualTo("знач2"));
            Uri responseUri = new Uri(response.Url);
            string[] responseParam1 = ParseQueryAll(responseUri.Query, "param1");
            Assert.That(responseParam1, Is.EqualTo(new[] { "value1", "value2" }));
            Assert.That(QueryValue(responseUri.Query, "парам2"), Is.EqualTo("знач2"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldSupportFailOnStatusCodeAsync(string method)
        {
            EnsureServer();
            Server.SetRoute("/does-not-exist.html", http =>
            {
                http.Response.StatusCode = 404;
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("File not found: does-not-exist.html");
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => CallAsync(
                context.APIRequest,
                method,
                Prefix + "/does-not-exist.html",
                failOnStatusCode: true)).ConfigureAwait(false);
            Assert.That(error.Message, Does.Contain("404 Not Found"));
            if (method != "head")
            {
                Assert.That(error.Message, Does.Contain("Response text:\nFile not found:"));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldSupportIgnoreHttpsErrorsOptionAsync(string method)
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await CallAsync(
                context.APIRequest,
                method,
                HttpsEmptyPage,
                ignoreHTTPSErrors: true).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task ShouldSupportPostDataAsync(string method)
        {
            EnsureServer();
            Server.SetRoute("/simple.json", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "application/json";
                await http.Response.WriteAsync("{\"foo\": \"bar\"}\n").ConfigureAwait(false);
            });
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            Task<HttpRequestSnapshot> requestTask = Server.WaitForRequest("/simple.json", Capture);
            IAPIResponse response = await CallAsync(
                context.APIRequest,
                method,
                Prefix + "/simple.json",
                data: "My request").ConfigureAwait(false);
            HttpRequestSnapshot request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Method, Is.EqualTo(method.ToUpperInvariant()));
            Assert.That(request.Body, Is.EqualTo("My request"));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(request.Path, Is.EqualTo("/simple.json"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static Task<IAPIResponse> CallAsync(
            IAPIRequestContext request,
            string method,
            string url,
            IEnumerable<KeyValuePair<string, string>> headers = null,
            bool? failOnStatusCode = null,
            float? timeout = null,
            bool ignoreHTTPSErrors = false,
            IEnumerable<KeyValuePair<string, object>> queryParams = null,
            string data = null)
        {
            if (string.Equals(method, "get", StringComparison.Ordinal) && data != null)
            {
                return request.FetchAsync(url, new() { Method = "GET", Data = data, Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams });
            }

            if (string.Equals(method, "head", StringComparison.Ordinal) && data != null)
            {
                return request.FetchAsync(url, new() { Method = "HEAD", Data = data, Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams });
            }

            return method switch
            {
                "fetch" => request.FetchAsync(url, new() { Method = "GET", Data = data, Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams }),
                "delete" => request.DeleteAsync(url, new() { Data = data, Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams }),
                "get" => request.GetAsync(url, new() { Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams }),
                "head" => request.HeadAsync(url, new() { Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams }),
                "patch" => request.PatchAsync(url, new() { Data = data, Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams }),
                "post" => request.PostAsync(url, new() { Data = data, Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams }),
                "put" => request.PutAsync(url, new() { Data = data, Headers = headers, FailOnStatusCode = failOnStatusCode, Timeout = timeout, IgnoreHTTPSErrors = ignoreHTTPSErrors, Params = queryParams }),
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
            };
        }

        private static HttpRequestSnapshot Capture(HttpRequest request)
        {
            return new HttpRequestSnapshot
            {
                Method = request.Method,
                Path = request.Path.Value,
                Query = request.QueryString.Value,
                Accept = request.Headers["Accept"].ToString(),
                UserAgent = request.Headers["User-Agent"].ToString(),
                AcceptEncoding = request.Headers["Accept-Encoding"].ToString(),
                ContentLength = request.Headers["Content-Length"].ToString(),
                ContentType = request.Headers["Content-Type"].ToString(),
                Foo = request.Headers["foo"].ToString(),
                Body = ReadBody(request),
            };
        }

        private static string ReadBody(HttpRequest request)
        {
            if (request == null || request.Body == null || request.ContentLength.GetValueOrDefault() <= 0)
            {
                return string.Empty;
            }

            try
            {
                if (request.Body.CanSeek)
                {
                    request.Body.Position = 0;
                }

                using StreamReader reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                string body = reader.ReadToEnd();
                if (request.Body.CanSeek)
                {
                    request.Body.Position = 0;
                }

                return body;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string QueryValue(string query, string name)
        {
            string[] values = ParseQueryAll(query ?? string.Empty, name);
            return values.Length == 0 ? null : values[0];
        }

        private static string[] ParseQueryAll(string query, string name)
        {
            List<string> values = new List<string>();
            string trimmed = query.StartsWith('?') ? query.Substring(1) : query;
            foreach (string part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                string key = eq >= 0 ? part.Substring(0, eq) : part;
                string value = eq >= 0 ? part.Substring(eq + 1) : string.Empty;
                if (Uri.UnescapeDataString(key.Replace("+", " ", StringComparison.Ordinal)) == name)
                {
                    values.Add(Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal)));
                }
            }

            return values.ToArray();
        }

        private static string[] ParseMultipartNames(string body)
        {
            List<string> names = new List<string>();
            string marker = "name=\"";
            int index = 0;
            while (index < body.Length)
            {
                int start = body.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    break;
                }

                start += marker.Length;
                int end = body.IndexOf('"', start);
                if (end < 0)
                {
                    break;
                }

                names.Add(body.Substring(start, end - start));
                index = end + 1;
            }

            return names.ToArray();
        }

        private static byte[] CompressGzip(byte[] payload)
        {
            using MemoryStream stream = new MemoryStream();
            using (GZipStream gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(payload, 0, payload.Length);
            }

            return stream.ToArray();
        }

        private static byte[] CompressBrotli(byte[] payload)
        {
            using MemoryStream stream = new MemoryStream();
            using (BrotliStream brotli = new BrotliStream(stream, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotli.Write(payload, 0, payload.Length);
            }

            return stream.ToArray();
        }

        private static byte[] CompressDeflate(byte[] payload)
        {
            using MemoryStream stream = new MemoryStream();
            using (ZLibStream deflate = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(payload, 0, payload.Length);
            }

            return stream.ToArray();
        }

        private static async Task WritePartialBodyThenAbortAsync(HttpContext http)
        {
            http.Response.StatusCode = 200;
            http.Response.ContentLength = 4096;
            http.Response.ContentType = "text/html";
            await http.Response.StartAsync().ConfigureAwait(false);
            await http.Response.WriteAsync("<title>A").ConfigureAwait(false);
            await http.Response.Body.FlushAsync().ConfigureAwait(false);
            // Node's res.uncork() pushes the status line before socket.destroy().
            await Task.Delay(30).ConfigureAwait(false);
            http.Abort();
        }

        private static byte[] CreateSimpleZipBytes()
        {
            byte[] bytes = new byte[5100];
            byte[] line = Encoding.UTF8.GetBytes("{\"foo\": \"bar\"}\n");
            int offset = 0;
            while (offset < bytes.Length)
            {
                int copy = Math.Min(line.Length, bytes.Length - offset);
                Buffer.BlockCopy(line, 0, bytes, offset, copy);
                offset += copy;
            }

            return bytes;
        }

        private static async Task<RawHttpServer> StartRawHttpAsync(byte[] responseBytes)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            CancellationTokenSource cts = new CancellationTokenSource();
            Task loop = AcceptRawAsync(listener, responseBytes, cts.Token);
            return new RawHttpServer(listener, port, cts, loop);
        }

        private static async Task AcceptRawAsync(TcpListener listener, byte[] responseBytes, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    _ = Task.Run(
                        async () =>
                        {
                            try
                            {
                                using (client)
                                using (NetworkStream stream = client.GetStream())
                                {
                                    byte[] buffer = new byte[4096];
                                    await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                                    await stream.WriteAsync(responseBytes, token).ConfigureAwait(false);
                                    await stream.FlushAsync(token).ConfigureAwait(false);
                                }
                            }
                            catch (Exception)
                            {
                            }
                        },
                        token);
                }
            }
            catch (Exception)
            {
            }
        }

        private static async Task<(SimpleServer Server, int Port)> StartEphemeralHttpsAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            string certPath = Path.Combine(contentRoot, "testCert.cer");
            if (File.Exists(certPath))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
                {
                    Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
                }
            }

            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
            await https.StartAsync().ConfigureAwait(false);
            return (https, port);
        }

        private sealed class HttpRequestSnapshot
        {
            public string Method { get; set; }

            public string Path { get; set; }

            public string Query { get; set; }

            public string Accept { get; set; }

            public string UserAgent { get; set; }

            public string AcceptEncoding { get; set; }

            public string ContentLength { get; set; }

            public string ContentType { get; set; }

            public string Foo { get; set; }

            public string Body { get; set; }
        }

        private sealed class RawHttpServer : IAsyncDisposable
        {
            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _cts;
            private readonly Task _loop;

            public RawHttpServer(TcpListener listener, int port, CancellationTokenSource cts, Task loop)
            {
                _listener = listener;
                Port = port;
                _cts = cts;
                _loop = loop;
            }

            public int Port { get; }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                try
                {
                    _listener.Stop();
                }
                catch (Exception)
                {
                }

                try
                {
                    await _loop.ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _cts.Dispose();
            }
        }
    }
}
