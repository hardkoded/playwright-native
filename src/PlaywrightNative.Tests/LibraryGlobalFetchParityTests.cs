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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/global-fetch.spec.ts</c> parity. Leftover
    /// <c>ApiResponse*</c> already covers server address / security
    /// details. Skip Node-only <c>should set playwright as user-agent</c>
    /// (<c>getPlaywrightVersion</c> / <c>node/X.X</c>) and
    /// <c>should be able to construct with context options</c>
    /// (<c>_instrumentation.runBeforeCreateRequestContext</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryGlobalFetchParityTests : PageTestEx
    {
        private static readonly string[] HttpMethods = { "GET", "PUT", "POST", "OPTIONS", "HEAD", "PATCH" };

        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
        private static int ServerPort = TestConstants.Port;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19877;
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
        }

        [TearDown]
        public void TearDown()
        {
            _ownedServer?.Reset();
            _ownedHttps?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
        }

        [PlaywrightTest("global-fetch.spec.ts", "fetch should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task FetchShouldWork() => AssertMethodWorksAsync("fetch");

        [PlaywrightTest("global-fetch.spec.ts", "delete should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task DeleteShouldWork() => AssertMethodWorksAsync("delete");

        [PlaywrightTest("global-fetch.spec.ts", "get should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task GetShouldWork() => AssertMethodWorksAsync("get");

        [PlaywrightTest("global-fetch.spec.ts", "head should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task HeadShouldWork() => AssertMethodWorksAsync("head");

        [PlaywrightTest("global-fetch.spec.ts", "patch should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task PatchShouldWork() => AssertMethodWorksAsync("patch");

        [PlaywrightTest("global-fetch.spec.ts", "post should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task PostShouldWork() => AssertMethodWorksAsync("post");

        [PlaywrightTest("global-fetch.spec.ts", "put should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task PutShouldWork() => AssertMethodWorksAsync("put");

        [PlaywrightTest("global-fetch.spec.ts", "should dispose global request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDisposeGlobalRequest()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
            JsonElement? json = await response.JsonAsync().ConfigureAwait(false);
            Assert.That(json.HasValue, Is.True);
            Assert.That(json.Value.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            await request.DisposeAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => response.BodyAsync());
            Assert.That(error.Message, Does.Contain("Response has been disposed"));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support global userAgent option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportGlobalUserAgentOption()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { UserAgent = "My Agent" }).ConfigureAwait(false);
            try
            {
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers["user-agent"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(responseTask.Result.Ok, Is.True);
                Assert.That(responseTask.Result.Url, Is.EqualTo(EmptyPage));
                Assert.That(serverRequest.Result, Is.EqualTo("My Agent"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support global timeout option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportGlobalTimeoutOption()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { Timeout = 100 }).ConfigureAwait(false);
            try
            {
                Server.SetRoute("/empty.html", _ => Task.Delay(Timeout.Infinite));
                Exception error = Assert.CatchAsync(() => request.GetAsync(EmptyPage));
                Assert.That(error.Message, Does.Contain("apiRequestContext.get: Timeout 100ms exceeded"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "should propagate extra http headers with redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateExtraHttpHeadersWithRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/simple.json");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { ExtraHTTPHeaders = new Dictionary<string, string> { ["My-Secret"] = "Value" } }).ConfigureAwait(false);
            try
            {
                Task<string> req1 = Server.WaitForRequest("/a/redirect1", req => req.Headers["my-secret"].ToString());
                Task<string> req2 = Server.WaitForRequest("/b/c/redirect2", req => req.Headers["my-secret"].ToString());
                Task<string> req3 = Server.WaitForRequest("/simple.json", req => req.Headers["my-secret"].ToString());
                await Task.WhenAll(req1, req2, req3, request.GetAsync(Prefix + "/a/redirect1")).ConfigureAwait(false);
                Assert.That(req1.Result, Is.EqualTo("Value"));
                Assert.That(req2.Result, Is.EqualTo("Value"));
                Assert.That(req3.Result, Is.EqualTo("Value"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "should preserve authorization on same-origin redirect but strip on cross-origin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreserveAuthorizationOnSameOriginRedirectButStripOnCrossOrigin()
        {
            EnsureServer();
            Server.SetRedirect("/same/redirect", "/same/dest");
            Server.SetRedirect("/cross/redirect", CrossProcessPrefix + "/cross/dest");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { ExtraHTTPHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer secret" } }).ConfigureAwait(false);
            try
            {
                Task<string> sameDest = Server.WaitForRequest("/same/dest", req => req.Headers["authorization"].ToString());
                await Task.WhenAll(sameDest, request.GetAsync(Prefix + "/same/redirect")).ConfigureAwait(false);
                Assert.That(sameDest.Result, Is.EqualTo("Bearer secret"));

                Task<string> crossDest = Server.WaitForRequest("/cross/dest", req => req.Headers["authorization"].ToString());
                await Task.WhenAll(crossDest, request.GetAsync(Prefix + "/cross/redirect")).ConfigureAwait(false);
                Assert.That(crossDest.Result, Is.Null.Or.Empty);
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support global httpCredentials option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportGlobalHttpCredentialsOption()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IAPIRequestContext request1 = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response1 = await request1.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(401));
            await request1.DisposeAsync().ConfigureAwait(false);

            IAPIRequestContext request2 = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" } }).ConfigureAwait(false);
            IAPIResponse response2 = await request2.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(200));
            await request2.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return error with wrong credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnErrorWithWrongCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "wrong" } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should work with correct credentials and matching origin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCorrectCredentialsAndMatchingOrigin()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = Prefix } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should work with correct credentials and matching origin case insensitive")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCorrectCredentialsAndMatchingOriginCaseInsensitive()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = Prefix.ToUpperInvariant() } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return error with correct credentials and mismatching scheme")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnErrorWithCorrectCredentialsAndMismatchingScheme()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "user",
                    Password = "pass",
                    Origin = Prefix.Replace("http://", "https://", StringComparison.Ordinal),
                }
            }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return error with correct credentials and mismatching hostname")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnErrorWithCorrectCredentialsAndMismatchingHostname()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            string hostname = new Uri(Prefix).Host;
            string origin = Prefix.Replace(hostname, "mismatching-hostname", StringComparison.Ordinal);
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = origin } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return error with correct credentials and mismatching port")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnErrorWithCorrectCredentialsAndMismatchingPort()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            string origin = Prefix.Replace(
                ServerPort.ToString(CultureInfo.InvariantCulture),
                (ServerPort + 1).ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = origin } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support WWW-Authenticate: Basic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportWwwAuthenticateBasic()
        {
            EnsureServer();
            string credentials = null;
            Server.SetRoute("/empty.html", async http =>
            {
                string header = http.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(header))
                {
                    http.Response.StatusCode = 401;
                    http.Response.Headers["WWW-Authenticate"] = "Basic";
                    await http.Response.WriteAsync("HTTP Error 401 Unauthorized: Access is denied").ConfigureAwait(false);
                    return;
                }

                credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header.Split(' ').Last()));
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" } }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(credentials, Is.EqualTo("user:pass"));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support HTTPCredentials.send")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHttpCredentialsSend()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "user",
                    Password = "pass",
                    Origin = Prefix.ToUpperInvariant(),
                    Send = HttpCredentialsSend.Always,
                }
            }).ConfigureAwait(false);
            try
            {
                Task<string> first = Server.WaitForRequest("/empty.html", req => req.Headers.Authorization.ToString());
                Task<IAPIResponse> firstResponse = request.GetAsync(EmptyPage);
                await Task.WhenAll(first, firstResponse).ConfigureAwait(false);
                Assert.That(first.Result, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"))));
                Assert.That(firstResponse.Result.Status, Is.EqualTo(200));

                Task<string> second = Server.WaitForRequest("/empty.html", req => req.Headers.Authorization.ToString());
                Task<IAPIResponse> secondResponse = request.GetAsync(CrossProcessPrefix + "/empty.html");
                await Task.WhenAll(second, secondResponse).ConfigureAwait(false);
                Assert.That(second.Result, Is.Null.Or.Empty);
                Assert.That(secondResponse.Result.Status, Is.EqualTo(200));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support multiple httpCredentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipleHttpCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user1", "pass1");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
            {
                HttpCredentials = new[]
                {
                    new HttpCredentials { Username = "user1", Password = "pass1", Origin = Prefix },
                    new HttpCredentials { Username = "user2", Password = "pass2", Origin = CrossProcessPrefix },
                }
            }).ConfigureAwait(false);
            IAPIResponse response1 = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(200));
            IAPIResponse response2 = await request.GetAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(401));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support HTTPCredentials.send with multiple httpCredentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHttpCredentialsSendWithMultipleHttpCredentials()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
            {
                HttpCredentials = new[]
                {
                    new HttpCredentials { Username = "user1", Password = "pass1", Origin = Prefix, Send = HttpCredentialsSend.Always },
                    new HttpCredentials { Username = "user2", Password = "pass2", Origin = CrossProcessPrefix, Send = HttpCredentialsSend.Unauthorized },
                }
            }).ConfigureAwait(false);
            try
            {
                Task<string> first = Server.WaitForRequest("/empty.html", req => req.Headers.Authorization.ToString());
                Task<IAPIResponse> firstResponse = request.GetAsync(EmptyPage);
                await Task.WhenAll(first, firstResponse).ConfigureAwait(false);
                Assert.That(first.Result, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user1:pass1"))));
                Assert.That(firstResponse.Result.Status, Is.EqualTo(200));

                Task<string> second = Server.WaitForRequest("/empty.html", req => req.Headers.Authorization.ToString());
                Task<IAPIResponse> secondResponse = request.GetAsync(CrossProcessPrefix + "/empty.html");
                await Task.WhenAll(second, secondResponse).ConfigureAwait(false);
                Assert.That(second.Result, Is.Null.Or.Empty);
                Assert.That(secondResponse.Result.Status, Is.EqualTo(200));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "should support global ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportGlobalIgnoreHttpsErrorsOption()
        {
            EnsureHttps();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(HttpsEmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should propagate ignoreHTTPSErrors on redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateIgnoreHttpsErrorsOnRedirects()
        {
            EnsureHttps();
            HttpsServer.SetRedirect("/redir", "/empty.html");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(HttpsPrefix + "/redir", new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return security details for a resumed TLS session")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnSecurityDetailsForAResumedTlsSession()
        {
            EnsureHttps();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IAPIResponse first = await request.GetAsync(HttpsEmptyPage, new() { Headers = new Dictionary<string, string> { ["connection"] = "close" } }).ConfigureAwait(false);
            ResponseSecurityDetailsResult expected = await first.SecurityDetailsAsync().ConfigureAwait(false);
            Assert.That(expected, Is.Not.Null);
            IAPIResponse second = await request.GetAsync(HttpsEmptyPage).ConfigureAwait(false);
            ResponseSecurityDetailsResult again = await second.SecurityDetailsAsync().ConfigureAwait(false);
            Assert.That(again, Is.Not.Null);
            Assert.That(again.SubjectName, Is.EqualTo(expected.SubjectName));
            Assert.That(again.Issuer, Is.EqualTo(expected.Issuer));
            Assert.That(again.Protocol, Is.EqualTo(expected.Protocol));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return security details for certificate with multiple CN attributes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnSecurityDetailsForCertificateWithMultipleCnAttributes()
        {
            await using MultiCnHttpsServer server = MultiCnHttpsServer.Start();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(
                "https://localhost:" + server.Port.ToString(CultureInfo.InvariantCulture) + "/").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            ResponseSecurityDetailsResult details = await response.SecurityDetailsAsync().ConfigureAwait(false);
            Assert.That(details.SubjectName, Is.EqualTo("localhost"));
            Assert.That(details.Issuer, Is.EqualTo("localhost"));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should resolve url relative to global baseURL option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveUrlRelativeToGlobalBaseUrlOption()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = Prefix }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync("/empty.html").ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(EmptyPage));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should fallback to given URL if baseURL is bogus")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFallbackToGivenUrlIfBaseUrlIsBogus()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = "bogus" }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(EmptyPage));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return empty body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnEmptyBody()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
            byte[] body = await response.BodyAsync().ConfigureAwait(false);
            Assert.That(body.Length, Is.EqualTo(0));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should abort requests when context is disposed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbortRequestsWhenContextIsDisposed()
        {
            EnsureServer();
            TaskCompletionSource<object> connectionClosed = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/empty.html", http =>
            {
                http.Request.HttpContext.RequestAborted.Register(() => connectionClosed.TrySetResult(null));
                return Task.Delay(Timeout.Infinite, http.Request.HttpContext.RequestAborted);
            });
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            Task<IAPIResponse> get = request.GetAsync(EmptyPage);
            Task<IAPIResponse> post = request.PostAsync(EmptyPage);
            Task<IAPIResponse> delete = request.DeleteAsync(EmptyPage);
            await Server.WaitForRequest("/empty.html").ConfigureAwait(false);
            await request.DisposeAsync().ConfigureAwait(false);
            Exception getError = Assert.CatchAsync(() => get);
            Exception postError = Assert.CatchAsync(() => post);
            Exception deleteError = Assert.CatchAsync(() => delete);
            Assert.That(getError.Message, Does.Contain("Request context disposed."));
            Assert.That(postError.Message, Does.Contain("Request context disposed."));
            Assert.That(deleteError.Message, Does.Contain("Request context disposed."));
            await connectionClosed.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should abort redirected requests when context is disposed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbortRedirectedRequestsWhenContextIsDisposed()
        {
            EnsureServer();
            Server.SetRedirect("/redirect", "/test");
            TaskCompletionSource<object> connectionClosed = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/test", http =>
            {
                http.Request.HttpContext.RequestAborted.Register(() => connectionClosed.TrySetResult(null));
                return Task.Delay(Timeout.Infinite, http.Request.HttpContext.RequestAborted);
            });
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            Task<IAPIResponse> get = request.GetAsync(Prefix + "/redirect");
            await Server.WaitForRequest("/test").ConfigureAwait(false);
            await request.DisposeAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => get);
            Assert.That(error.Message, Does.Match("Request context disposed|Target page, context or browser has been closed"));
            await connectionClosed.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should remove content-length from redirected post requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveContentLengthFromRedirectedPostRequests()
        {
            EnsureServer();
            Server.SetRedirect("/redirect", "/empty.html");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            Task<string> req1 = Server.WaitForRequest("/redirect", req => req.Headers.ContentLength.ToString());
            Task<string> req2 = Server.WaitForRequest("/empty.html", req => req.Headers.ContentLength.ToString());
            Task<IAPIResponse> result = request.PostAsync(Prefix + "/redirect", new() { DataObject = new Dictionary<string, string> { ["foo"] = "bar" } });
            await Task.WhenAll(result, req1, req2).ConfigureAwait(false);
            Assert.That(result.Result.Status, Is.EqualTo(200));
            Assert.That(req1.Result, Is.EqualTo("13"));
            Assert.That(req2.Result, Is.Null.Or.Empty.Or.EqualTo("0"));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify object body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyObjectBody()
            => AssertJsonBodyAsync("{\"foo\":\"bar\"}", json: new Dictionary<string, string> { ["foo"] = "bar" });

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify object body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyObjectBody()
            => AssertJsonBodyAsync("{\"foo\":\"bar\"}", data: "{\"foo\":\"bar\"}");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify array body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyArrayBody()
            => AssertJsonBodyAsync("[\"foo\",\"bar\",2021]", json: new object[] { "foo", "bar", 2021 });

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify array body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyArrayBody()
            => AssertJsonBodyAsync("[\"foo\",\"bar\",2021]", data: "[\"foo\",\"bar\",2021]");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify string body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyStringBody()
            => AssertJsonBodyAsync("\"foo\"", json: "foo");

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify string body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyStringBody()
            => AssertJsonBodyAsync("\"foo\"", data: "\"foo\"");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify string (falsey) body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyFalseyStringBody()
            => AssertJsonBodyAsync("\"\"", json: string.Empty);

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify string (falsey) body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyFalseyStringBody()
            => AssertJsonBodyAsync("\"\"", data: "\"\"");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify bool body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyBoolBody()
            => AssertJsonBodyAsync("true", json: true);

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify bool body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyBoolBody()
            => AssertJsonBodyAsync("true", data: "true");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify bool (false) body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyFalseBoolBody()
            => AssertJsonBodyAsync("false", json: false);

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify bool (false) body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyFalseBoolBody()
            => AssertJsonBodyAsync("false", data: "false");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify number body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyNumberBody()
            => AssertJsonBodyAsync("2021", json: 2021);

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify number body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyNumberBody()
            => AssertJsonBodyAsync("2021", data: "2021");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify number (falsey) body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyFalseyNumberBody()
            => AssertJsonBodyAsync("0", json: 0);

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify number (falsey) body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyFalseyNumberBody()
            => AssertJsonBodyAsync("0", data: "0");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify null body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyNullBody()
            => AssertJsonBodyAsync("null", json: new JsonNull());

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify null body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyNullBody()
            => AssertJsonBodyAsync("null", data: "null");

        [PlaywrightTest("global-fetch.spec.ts", "should json stringify literal string undefined body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldJsonStringifyLiteralUndefinedBody()
            => AssertJsonBodyAsync("\"undefined\"", json: "undefined");

        [PlaywrightTest("global-fetch.spec.ts", "should not double stringify literal string undefined body when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotDoubleStringifyLiteralUndefinedBody()
            => AssertJsonBodyAsync("\"undefined\"", data: "\"undefined\"");

        [PlaywrightTest("global-fetch.spec.ts", "should accept already serialized data as Buffer when content-type is application/json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldAcceptAlreadySerializedDataAsBuffer()
        {
            string value = JsonSerializer.Serialize(JsonSerializer.Serialize(new Dictionary<string, string> { ["foo"] = "bar" }));
            return AssertJsonBodyAsync(value, dataBytes: Encoding.UTF8.GetBytes(value));
        }

        [PlaywrightTest("global-fetch.spec.ts", "should have nice toString")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveNiceToString()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.PostAsync(EmptyPage, new() { Headers = new Dictionary<string, string> { ["content-type"] = "application/json" }, Data = "My post data" }).ConfigureAwait(false);
            string text = response.ToString();
            Assert.That(text, Does.Contain("APIResponse: 200 OK"));
            foreach (Header header in response.HeadersArray)
            {
                Assert.That(text, Does.Contain(" " + header.Name + ": " + header.Value));
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should not fail on empty body with encoding")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFailOnEmptyBodyWithEncoding()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            foreach (string method in new[] { "head", "put" })
            {
                foreach (string encoding in new[] { "br", "gzip", "deflate" })
                {
                    Server.SetRoute("/empty.html", async http =>
                    {
                        http.Response.Headers["Content-Encoding"] = encoding;
                        http.Response.ContentType = "text/plain";
                        await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
                    });
                    IAPIResponse response = await FetchAsync(request, method, EmptyPage).ConfigureAwait(false);
                    Assert.That(response.Status, Is.EqualTo(200));
                    Assert.That((await response.BodyAsync().ConfigureAwait(false)).Length, Is.EqualTo(0));
                }
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return body for failing requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnBodyForFailingRequests()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            foreach (string method in new[] { "head", "put", "trace" })
            {
                Server.SetRoute("/empty.html", async http =>
                {
                    http.Response.StatusCode = 404;
                    http.Response.ContentType = "text/plain";
                    await http.Response.WriteAsync("Not found.").ConfigureAwait(false);
                });
                IAPIResponse response = await request.FetchAsync(EmptyPage, new() { Method = method }).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(404));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(method == "head" ? string.Empty : "Not found."));
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should throw an error when maxRedirects is exceeded")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowAnErrorWhenMaxRedirectsIsExceeded()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/b/c/redirect3");
            Server.SetRedirect("/b/c/redirect3", "/b/c/redirect4");
            Server.SetRedirect("/b/c/redirect4", "/simple.json");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            foreach (string method in HttpMethods)
            {
                foreach (int maxRedirects in new[] { 1, 2, 3 })
                {
                    Exception error = Assert.CatchAsync(() => request.FetchAsync(Prefix + "/a/redirect1", new() { Method = method, MaxRedirects = maxRedirects }));
                    Assert.That(error.Message, Does.Contain("Max redirect count exceeded"));
                }
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should not follow redirects when maxRedirects is set to 0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFollowRedirectsWhenMaxRedirectsIsSetTo0()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/simple.json");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            foreach (string method in HttpMethods)
            {
                IAPIResponse response = await request.FetchAsync(Prefix + "/a/redirect1", new() { Method = method, MaxRedirects = 0 }).ConfigureAwait(false);
                Assert.That(response.Headers["location"], Is.EqualTo("/b/c/redirect2"));
                Assert.That(response.Status, Is.EqualTo(302));
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should throw an error when maxRedirects is less than 0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowAnErrorWhenMaxRedirectsIsLessThan0()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/simple.json");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            foreach (string method in HttpMethods)
            {
                Exception error = Assert.CatchAsync(() => request.FetchAsync(Prefix + "/a/redirect1", new() { Method = method, MaxRedirects = -1 }));
                Assert.That(error.Message, Does.Contain("'maxRedirects' must be greater than or equal to '0'"));
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should not follow redirects when maxRedirects is set to 0 in newContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFollowRedirectsWhenMaxRedirectsIsSetTo0InNewContext()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/simple.json");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { MaxRedirects = 0 }).ConfigureAwait(false);
            foreach (string method in HttpMethods)
            {
                IAPIResponse response = await request.FetchAsync(Prefix + "/a/redirect1", new() { Method = method }).ConfigureAwait(false);
                Assert.That(response.Headers["location"], Is.EqualTo("/b/c/redirect2"));
                Assert.That(response.Status, Is.EqualTo(302));
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should follow redirects up to maxRedirects limit set in newContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFollowRedirectsUpToMaxRedirectsLimitSetInNewContext()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/b/c/redirect3");
            Server.SetRedirect("/b/c/redirect3", "/b/c/redirect4");
            Server.SetRedirect("/b/c/redirect4", "/simple.json");
            foreach (int maxRedirects in new[] { 1, 2, 3, 4 })
            {
                IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { MaxRedirects = maxRedirects }).ConfigureAwait(false);
                foreach (string method in HttpMethods)
                {
                    if (maxRedirects < 4)
                    {
                        Exception error = Assert.CatchAsync(() => request.FetchAsync(Prefix + "/a/redirect1", new() { Method = method }));
                        Assert.That(error.Message, Does.Contain("Max redirect count exceeded"));
                    }
                    else
                    {
                        IAPIResponse response = await request.FetchAsync(Prefix + "/a/redirect1", new() { Method = method }).ConfigureAwait(false);
                        Assert.That(response.Status, Is.EqualTo(200));
                    }
                }

                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "should use maxRedirects from fetch when provided, overriding newContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseMaxRedirectsFromFetchWhenProvidedOverridingNewContext()
        {
            EnsureServer();
            Server.SetRedirect("/a/redirect1", "/b/c/redirect2");
            Server.SetRedirect("/b/c/redirect2", "/b/c/redirect3");
            Server.SetRedirect("/b/c/redirect3", "/b/c/redirect4");
            Server.SetRedirect("/b/c/redirect4", "/simple.json");
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { MaxRedirects = 1 }).ConfigureAwait(false);
            foreach (string method in HttpMethods)
            {
                IAPIResponse response = await request.FetchAsync(Prefix + "/a/redirect1", new() { Method = method, MaxRedirects = 4 }).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should keep headers capitalization")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepHeadersCapitalization()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            Task<string[]> serverRequest = Server.WaitForRequest("/empty.html", req => req.Headers.Select(h => h.Key).ToArray());
            Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage, new() { Headers = new Dictionary<string, string> { ["X-fOo"] = "vaLUE" } });
            await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
            Assert.That(responseTask.Result.Ok, Is.True);
            Assert.That(serverRequest.Result, Does.Contain("X-fOo").IgnoreCase);
            Assert.That(await responseTask.Result.TextAsync().ConfigureAwait(false), Is.Not.Null);
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should serialize post data on the client")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSerializePostDataOnTheClient()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            Task<string> serverReq = Server.WaitForRequest("/empty.html", ReadBody);
            bool onStack = true;
            Task<IAPIResponse> postReq = request.PostAsync(EmptyPage, new()
            {
                DataObject = new ClientToJson
                {
                    ToJson = () =>
                    {
                        if (!onStack)
                        {
                            throw new InvalidOperationException("Should not be called on the server");
                        }

                        return new Dictionary<string, string> { ["foo"] = "bar" };
                    },
                }
            });
            onStack = false;
            await postReq.ConfigureAwait(false);
            Assert.That(await serverReq.ConfigureAwait(false), Is.EqualTo("{\"foo\":\"bar\"}"));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should throw after dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowAfterDispose()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            await request.DisposeAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => request.GetAsync(EmptyPage));
            Assert.That(error.Message, Does.Contain("Target page, context or browser has been closed"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "should retry ECONNRESET")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRetryEconnreset()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            int requestCount = 0;
            Server.SetRoute("/test", http =>
            {
                if (requestCount++ < 3)
                {
                    http.Abort();
                    return Task.CompletedTask;
                }

                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("Hello!");
            });
            IAPIResponse response = await request.FetchAsync(Prefix + "/test", new() { MaxRetries = 3 }).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Hello!"));
            Assert.That(requestCount, Is.EqualTo(4));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should not crash when server refuses body before reading it")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhenServerRefusesBodyBeforeReadingIt()
        {
            EnsureServer();
            Server.SetRoute("/refuse", async http =>
            {
                await Task.Delay(50).ConfigureAwait(false);
                http.Response.StatusCode = 413;
                http.Response.ContentType = "application/json";
                await http.Response.WriteAsync(JsonSerializer.Serialize(new Dictionary<string, string> { ["error"] = "too large" })).ConfigureAwait(false);
                http.Abort();
            });
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            try
            {
                IAPIResponse response = await request.PostAsync(
                    CrossProcessPrefix + "/refuse",
                    dataBytes: new byte[20 * 1024 * 1024],
                    headers: new Dictionary<string, string> { ["content-type"] = "text/plain" },
                    maxRetries: 0).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(413));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(JsonSerializer.Serialize(new Dictionary<string, string> { ["error"] = "too large" })));
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message, Does.Match("apiRequestContext\\.post|ECONNRESET|EPIPE|ECONNABORTED|socket"));
            }

            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should throw when failOnStatusCode is set to true inside APIRequest context options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenFailOnStatusCodeIsSetToTrueInsideApiRequestContextOptions()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { FailOnStatusCode = true }).ConfigureAwait(false);
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.StatusCode = 404;
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("Not found.").ConfigureAwait(false);
            });
            Exception error = Assert.CatchAsync(() => request.FetchAsync(EmptyPage));
            Assert.That(error.Message, Does.Contain("404 Not Found"));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should not throw when failOnStatusCode is set to false inside APIRequest context options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenFailOnStatusCodeIsSetToFalseInsideApiRequestContextOptions()
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { FailOnStatusCode = false }).ConfigureAwait(false);
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.StatusCode = 404;
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("Not found.").ConfigureAwait(false);
            });
            IAPIResponse response = await request.FetchAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(404));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        private static async Task AssertMethodWorksAsync(string method)
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await FetchAsync(request, method, Prefix + "/simple.json").ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/simple.json"));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.StatusText, Is.EqualTo("OK"));
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Headers["content-type"], Is.EqualTo("application/json; charset=utf-8"));
            Assert.That(response.HeadersArray, Does.Contain(new Header { Name = "Content-Type", Value = "application/json; charset=utf-8" }));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(method == "head" ? string.Empty : "{\"foo\": \"bar\"}\n"));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        private static async Task AssertJsonBodyAsync(string expected, object json = null, string data = null, byte[] dataBytes = null)
        {
            EnsureServer();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            Task<string> req = Server.WaitForRequest("/empty.html", ReadBody);
            await request.PostAsync(
                EmptyPage,
                headers: new Dictionary<string, string> { ["content-type"] = "application/json" },
                json: json,
                data: data,
                dataBytes: dataBytes).ConfigureAwait(false);
            Assert.That(await req.ConfigureAwait(false), Is.EqualTo(expected));
            await request.DisposeAsync().ConfigureAwait(false);
        }

        private static Task<IAPIResponse> FetchAsync(IAPIRequestContext request, string method, string url)
        {
            return method switch
            {
                "fetch" => request.FetchAsync(url),
                "delete" => request.DeleteAsync(url),
                "get" => request.GetAsync(url),
                "head" => request.HeadAsync(url),
                "patch" => request.PatchAsync(url),
                "post" => request.PostAsync(url),
                "put" => request.PutAsync(url),
                _ => request.FetchAsync(url, new() { Method = method }),
            };
        }

        private static string ReadBody(HttpRequest request)
        {
            if (request?.Body == null)
            {
                return string.Empty;
            }

            using StreamReader reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            request.Body.Position = 0;
            return reader.ReadToEnd();
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

            int basePort = 19977;
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

        [JsonConverter(typeof(JsonNullConverter))]
        private sealed class JsonNull
        {
        }

        private sealed class JsonNullConverter : JsonConverter<JsonNull>
        {
            public override JsonNull Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => null;

            public override void Write(Utf8JsonWriter writer, JsonNull value, JsonSerializerOptions options)
                => writer.WriteNullValue();
        }

        [JsonConverter(typeof(ClientToJsonConverter))]
        private sealed class ClientToJson
        {
            internal Func<object> ToJson { get; set; }
        }

        private sealed class ClientToJsonConverter : JsonConverter<ClientToJson>
        {
            public override ClientToJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotSupportedException();

            public override void Write(Utf8JsonWriter writer, ClientToJson value, JsonSerializerOptions options)
            {
                object payload = value.ToJson();
                JsonSerializer.Serialize(writer, payload, payload.GetType(), options);
            }
        }

        private sealed class MultiCnHttpsServer : IAsyncDisposable
        {
            private readonly TcpListener _listener;
            private readonly X509Certificate2 _cert;
            private readonly CancellationTokenSource _cts = new();
            private readonly Task _loop;

            private MultiCnHttpsServer(TcpListener listener, X509Certificate2 cert)
            {
                _listener = listener;
                _cert = cert;
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                _loop = AcceptLoopAsync();
            }

            internal int Port { get; }

            internal static MultiCnHttpsServer Start()
            {
                using RSA key = RSA.Create(2048);
                CertificateRequest request = new CertificateRequest(
                    "CN=localhost, CN=localhost",
                    key,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                SubjectAlternativeNameBuilder san = new SubjectAlternativeNameBuilder();
                san.AddDnsName("localhost");
                request.CertificateExtensions.Add(san.Build());
                using X509Certificate2 created = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddDays(7));
                X509Certificate2 cert = X509CertificateLoader.LoadPkcs12(
                    created.Export(X509ContentType.Pfx),
                    string.Empty,
                    X509KeyStorageFlags.Exportable);
                TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                return new MultiCnHttpsServer(listener, cert);
            }

            public async ValueTask DisposeAsync()
            {
                await _cts.CancelAsync().ConfigureAwait(false);
                try
                {
                    _listener.Stop();
                }
                catch (ObjectDisposedException)
                {
                }

                _cert.Dispose();
                try
                {
                    await _loop.ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _cts.Dispose();
            }

            private async Task AcceptLoopAsync()
            {
                while (!_cts.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    _ = HandleAsync(client);
                }
            }

            private async Task HandleAsync(TcpClient client)
            {
                try
                {
                    using (client)
                    using (System.Net.Security.SslStream ssl = new System.Net.Security.SslStream(client.GetStream(), false))
                    {
                        await ssl.AuthenticateAsServerAsync(_cert).ConfigureAwait(false);
                        byte[] buffer = new byte[4096];
                        MemoryStream acc = new MemoryStream();
                        while (acc.Length < 64 * 1024)
                        {
                            int n = await ssl.ReadAsync(buffer).ConfigureAwait(false);
                            if (n == 0)
                            {
                                break;
                            }

                            acc.Write(buffer, 0, n);
                            byte[] data = acc.ToArray();
                            if (data.AsSpan().IndexOf("\r\n\r\n"u8) >= 0)
                            {
                                break;
                            }
                        }

                        byte[] body = Encoding.UTF8.GetBytes("{\"ok\":true}");
                        string header =
                            "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                            body.Length.ToString(CultureInfo.InvariantCulture) +
                            "\r\nConnection: close\r\n\r\n";
                        await ssl.WriteAsync(Encoding.ASCII.GetBytes(header)).ConfigureAwait(false);
                        await ssl.WriteAsync(body).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
