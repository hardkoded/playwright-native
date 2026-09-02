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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-request-fallback.spec.ts</c> parity for <see cref="IRoute.FallbackAsync"/>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageRequestFallbackParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19775;
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

        [SetUp]
        public void SkipFirefox()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("RouteAsync is Chromium/WebKit until Firefox interception is wired.");
            }
        }

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route => route.FallbackAsync()).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should fall back")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFallBack()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<int> intercepted = new();
                await page.RouteAsync("**/empty.html", route =>
                {
                    intercepted.Add(1);
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route =>
                {
                    intercepted.Add(2);
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route =>
                {
                    intercepted.Add(3);
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should fall back async")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFallBackAsync()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<int> intercepted = new();
                await page.RouteAsync("**/empty.html", async route =>
                {
                    intercepted.Add(1);
                    await Task.Delay(100).ConfigureAwait(false);
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", async route =>
                {
                    intercepted.Add(2);
                    await Task.Delay(100).ConfigureAwait(false);
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", async route =>
                {
                    intercepted.Add(3);
                    await Task.Delay(100).ConfigureAwait(false);
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should not chain fulfill")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotChainFulfill()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                bool failed = false;
                await page.RouteAsync("**/empty.html", _ =>
                {
                    failed = true;
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route =>
                {
                    _ = route.FulfillAsync(new() { Status = 200, Body = "fulfilled" });
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route =>
                {
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                byte[] body = await response.BodyAsync().ConfigureAwait(false);
                Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo("fulfilled"));
                Assert.That(failed, Is.False);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should not chain abort")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotChainAbort()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                bool failed = false;
                await page.RouteAsync("**/empty.html", _ =>
                {
                    failed = true;
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route =>
                {
                    _ = route.AbortAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route =>
                {
                    _ = route.FallbackAsync();
                }).ConfigureAwait(false);
                Exception error = Assert.CatchAsync(async () => await page.GoToAsync(EmptyPage).ConfigureAwait(false));
                Assert.That(error, Is.Not.Null);
                Assert.That(failed, Is.False);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should fall back after exception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFallBackAfterException()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/empty.html", route =>
                {
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", async route =>
                {
                    try
                    {
                        await route.FulfillAsync((IAPIResponse)null).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        _ = route.FallbackAsync();
                    }
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should chain once")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChainOnce()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync(
                    "**/empty.html",
                    route =>
                    {
                        _ = route.FulfillAsync(new() { Status = 200, Body = "fulfilled one" });
                    },
                    times: 1).ConfigureAwait(false);
                await page.RouteAsync(
                    "**/empty.html",
                    route =>
                    {
                        _ = route.FallbackAsync();
                    },
                    times: 1).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                byte[] body = await response.BodyAsync().ConfigureAwait(false);
                Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo("fulfilled one"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should amend HTTP headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendHTTPHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<string> values = new();
                await page.RouteAsync("**/sleep.zzz", async route =>
                {
                    values.Add(route.Request.GetHeaderValue("foo"));
                    values.Add(await route.Request.HeaderValueAsync("FOO").ConfigureAwait(false));
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["FOO"] = "bar";
                    _ = route.FallbackAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<string> request = Server.WaitForRequest("/sleep.zzz", r => (string)r.Headers["foo"]);
                await Task.WhenAll(request, page.EvaluateAsync("() => fetch('/sleep.zzz')")).ConfigureAwait(false);
                values.Add(request.Result);
                Assert.That(values, Is.EqualTo(new[] { "bar", "bar", "bar" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should delete header with undefined value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDeleteHeaderWithUndefinedValue()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                Server.SetRoute("/something", async http =>
                {
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    await http.Response.WriteAsync("done").ConfigureAwait(false);
                });
                IRequest interceptedRequest = null;
                await page.RouteAsync("**/*", route =>
                {
                    interceptedRequest = route.Request;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync(Prefix + "/something", async route =>
                {
                    Dictionary<string, string> headers = await route.Request.AllHeadersAsync().ConfigureAwait(false);
                    headers["foo"] = null;
                    _ = route.FallbackAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<string> text = page.EvaluateAsync<string>(
                    "async url => { const data = await fetch(url, { headers: { foo: 'a', bar: 'b' } }); return data.text(); }",
                    Prefix + "/something");
                Task<Dictionary<string, string>> serverRequest = Server.WaitForRequest("/something", SnapshotHeaders);
                await Task.WhenAll(text, serverRequest).ConfigureAwait(false);
                Assert.That(text.Result, Is.EqualTo("done"));
                Assert.That(interceptedRequest.GetHeaderValue("foo"), Is.Null);
                Assert.That(interceptedRequest.GetHeaderValue("bar"), Is.EqualTo("b"));
                Assert.That(HeaderValue(serverRequest.Result, "foo"), Is.Null.Or.Empty);
                Assert.That(HeaderValue(serverRequest.Result, "bar"), Is.EqualTo("b"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should amend method")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendMethod()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> sRequest = Server.WaitForRequest("/sleep.zzz", r => r.Method);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);

                string method = null;
                await page.RouteAsync("**/*", route =>
                {
                    method = route.Request.Method;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.FallbackAsync(new() { Method = "POST" })).ConfigureAwait(false);

                Task<IRequest> request = page.WaitForRequestAsync("**/sleep.zzz");
                await Task.WhenAll(request, page.EvaluateAsync("() => fetch('/sleep.zzz')")).ConfigureAwait(false);
                Assert.That(method, Is.EqualTo("POST"));
                Assert.That(request.Result.Method, Is.EqualTo("POST"));
                Assert.That(await sRequest.ConfigureAwait(false), Is.EqualTo("POST"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should override request url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverrideRequestUrl()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> serverRequest = Server.WaitForRequest("/global-var.html", r => r.Method);

                string url = null;
                await page.RouteAsync("**/global-var.html", route =>
                {
                    url = route.Request.Url;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);

                await page.RouteAsync("**/foo", route => route.FallbackAsync(new() { Url = Prefix + "/global-var.html" })).ConfigureAwait(false);

                IResponse response = await page.GoToAsync(Prefix + "/foo").ConfigureAwait(false);
                Assert.That(url, Is.EqualTo(Prefix + "/global-var.html"));
                Assert.That(response.Request.Url, Is.EqualTo(Prefix + "/global-var.html"));
                Assert.That(response.Url, Is.EqualTo(Prefix + "/global-var.html"));
                Assert.That(await page.EvaluateAsync<int>("() => window['globalVar']").ConfigureAwait(false), Is.EqualTo(123));
                Assert.That(await serverRequest.ConfigureAwait(false), Is.EqualTo("GET"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should amend post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                string postData = null;
                await page.RouteAsync("**/*", route =>
                {
                    postData = route.Request.PostData;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FallbackAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes("doggo") });
                }).ConfigureAwait(false);
                Task<string> serverRequest = WaitForBodyAsync("/sleep.zzz");
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'POST', body: 'birdy' })")).ConfigureAwait(false);
                Assert.That(postData, Is.EqualTo("doggo"));
                Assert.That(serverRequest.Result, Is.EqualTo("doggo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should amend binary post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendBinaryPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                byte[] arr = new byte[256];
                for (int i = 0; i < arr.Length; i++)
                {
                    arr[i] = (byte)i;
                }

                byte[] postDataBuffer = null;
                await page.RouteAsync("**/*", route =>
                {
                    postDataBuffer = route.Request.PostDataBuffer;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FallbackAsync(new RouteFallbackOptions { PostData = arr });
                }).ConfigureAwait(false);
                Task<byte[]> serverRequest = Server.WaitForRequest("/sleep.zzz", ReadBodyBytes);
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'POST', body: 'birdy' })")).ConfigureAwait(false);
                byte[] buffer = serverRequest.Result;
                Assert.That(postDataBuffer, Is.Not.Null);
                Assert.That(postDataBuffer.Length, Is.EqualTo(arr.Length));
                Assert.That(buffer.Length, Is.EqualTo(arr.Length));
                for (int i = 0; i < arr.Length; i++)
                {
                    Assert.That(buffer[i], Is.EqualTo(arr[i]));
                    Assert.That(postDataBuffer[i], Is.EqualTo(arr[i]));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fallback.spec.ts", "should amend json post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendJsonPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                JsonDocument postData = null;
                await page.RouteAsync("**/*", route =>
                {
                    postData = route.Request.GetPayloadAsJson();
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FallbackAsync(postDataJson: new Dictionary<string, string> { ["foo"] = "bar" });
                }).ConfigureAwait(false);
                Task<string> serverRequest = WaitForBodyAsync("/sleep.zzz");
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'POST', body: 'birdy' })")).ConfigureAwait(false);
                Assert.That(postData, Is.Not.Null);
                Assert.That(postData.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
                Assert.That(serverRequest.Result, Is.EqualTo("{\"foo\":\"bar\"}"));
            }).ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        private static async Task WithPageAsync(Func<IPage, Task> body)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await body(page).ConfigureAwait(false);
        }

        private static Dictionary<string, string> HeadersOf(IRequest request)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                map[header.Key] = header.Value;
            }

            return map;
        }

        private static Dictionary<string, string> SnapshotHeaders(HttpRequest request)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in request.Headers)
            {
                map[header.Key] = header.Value.ToString();
            }

            return map;
        }

        private static string HeaderValue(IEnumerable<KeyValuePair<string, string>> headers, string name)
        {
            if (headers == null)
            {
                return null;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }

        private static Task<string> WaitForBodyAsync(string path)
            => Server.WaitForRequest(path, r =>
            {
                using StreamReader reader = new(r.Body);
                return reader.ReadToEnd();
            });

        private static byte[] ReadBodyBytes(HttpRequest request)
        {
            using MemoryStream buffer = new();
            request.Body.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
