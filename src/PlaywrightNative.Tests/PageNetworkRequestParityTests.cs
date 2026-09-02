/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-network-request.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android, BiDi-only):
    /// Electron <c>This needs Chromium &gt;= 99</c> guards on several header tests
    /// (not applicable here); Android <c>Playwright does not get CORS pre-flight on Android</c>
    /// is <see cref="Assert.Ignore(string)"/> at runtime.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageNetworkRequestParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19760;
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

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
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
            List<IFrame> frames = new List<IFrame>(page.Frames);
            return frames[frames.Count - 1];
        }

        private static Dictionary<string, string> AdjustServerHeaders(IHeaderDictionary headers)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

#pragma warning disable CA1308 // Node IncomingMessage.headers lower-cases names.
                string key = header.Key.ToLowerInvariant();
#pragma warning restore CA1308
                if (TestConstants.IsFirefox && string.Equals(key, "priority", StringComparison.Ordinal))
                {
                    continue;
                }

                map[key] = string.Join(", ", header.Value.ToArray());
            }

            return NormalizeConnection(map);
        }

        private static Dictionary<string, string> NormalizeConnection(Dictionary<string, string> headers)
        {
            if (headers == null)
            {
                return null;
            }

            Dictionary<string, string> map = new Dictionary<string, string>(headers, StringComparer.Ordinal);
            if (map.TryGetValue("connection", out string connection) && connection != null)
            {
                map["connection"] = connection.ToLowerInvariant();
            }

            return map;
        }

        private static string HeaderFromEnumerable(IEnumerable<KeyValuePair<string, string>> headers, string name)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }

        private static void IgnoreWebKitWin32RawHeaders()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("Curl does not show accept-encoding and accept-language");
            }
        }

        [PlaywrightTest("page-network-request.spec.ts", "should work for main frame navigation request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForMainFrameNavigationRequest()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Frame, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should work for subframe navigation request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForSubframeNavigationRequest()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            Assert.That(requests.Count, Is.EqualTo(1));
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(requests[0].Frame, Is.SameAs(frames[1]));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should work for fetch requests")]
        [PlaywrightTest("page-network-request.spec.ts", "should work for fetch requests @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForFetchRequests()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.EvaluateAsync<object>("(() => fetch('/digits/1.png'))()").ConfigureAwait(false);
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Frame, Is.SameAs(page.MainFrame));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should work for a redirect")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForARedirect()
        {
            EnsureServer();
            Server.SetRedirect("/foo.html", "/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(Prefix + "/foo.html").ConfigureAwait(false);

            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[0].Url, Is.EqualTo(Prefix + "/foo.html"));
            Assert.That(requests[1].Url, Is.EqualTo(Prefix + "/empty.html"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should not work for a redirect and interception")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotWorkForARedirectAndInterception()
        {
            EnsureServer();
            Server.SetRedirect("/foo.html", "/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            await page.RouteAsync("**", route =>
            {
                requests.Add(route.Request);
                return route.ContinueAsync();
            }).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/foo.html").ConfigureAwait(false);

            Assert.That(page.Url, Is.EqualTo(Prefix + "/empty.html"));
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Url, Is.EqualTo(Prefix + "/foo.html"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should return headers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnHeaders()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string userAgent = HeaderFromEnumerable(response.Request.Headers, "user-agent");
            if (TestConstants.IsChromium)
            {
                Assert.That(userAgent, Does.Contain("Chrome"));
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.That(userAgent, Does.Contain("Firefox"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(userAgent, Does.Contain("WebKit"));
            }
        }

        [PlaywrightTest("page-network-request.spec.ts", "should get the same headers as the server")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldGetTheSameHeadersAsTheServer()
        {
            EnsureServer();
            IgnoreWebKitWin32RawHeaders();
            Dictionary<string, string> serverHeaders = null;
            Server.SetRoute("/empty.html", http =>
            {
                serverHeaders = AdjustServerHeaders(http.Request.Headers);
                return http.Response.WriteAsync("done");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Dictionary<string, string> headers = await response.Request.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(NormalizeConnection(headers), Is.EqualTo(NormalizeConnection(serverHeaders)));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should not return allHeaders() until they are available")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotReturnAllHeadersUntilTheyAreAvailable()
        {
            EnsureServer();
            IgnoreWebKitWin32RawHeaders();
            Task<Dictionary<string, string>> requestHeadersPromise = null;
            Task<Dictionary<string, string>> responseHeadersPromise = null;
            Dictionary<string, string> serverHeaders = null;
            Server.SetRoute("/empty.html", async http =>
            {
                serverHeaders = AdjustServerHeaders(http.Request.Headers);
                http.Response.StatusCode = 200;
                http.Response.Headers["foo"] = "bar";
                await http.Response.StartAsync().ConfigureAwait(false);
                await Task.Delay(3000).ConfigureAwait(false);
                await http.Response.WriteAsync("done").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Request += (_, request) => requestHeadersPromise = request.AllHeadersAsync();
            page.Response += (_, response) => responseHeadersPromise = response.AllHeadersAsync();
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);

            Dictionary<string, string> requestHeaders = await requestHeadersPromise.ConfigureAwait(false);
            Assert.That(NormalizeConnection(requestHeaders), Is.EqualTo(NormalizeConnection(serverHeaders)));
            Dictionary<string, string> responseHeaders = await responseHeadersPromise.ConfigureAwait(false);
            Assert.That(responseHeaders["foo"], Is.EqualTo("bar"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should get the same headers as the server CORS")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldGetTheSameHeadersAsTheServerCors()
        {
            EnsureServer();
            IgnoreWebKitWin32RawHeaders();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Dictionary<string, string> serverHeaders = null;
            Server.SetRoute("/something", http =>
            {
                serverHeaders = AdjustServerHeaders(http.Request.Headers);
                http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                return http.Response.WriteAsync("done");
            });

            Task<IResponse> responsePromise = page.WaitForEventAsync(PageEvent.Response);
            string text = await page.EvaluateAsync<string>(
                @"async url => {
                    const data = await fetch(url);
                    return data.text();
                }",
                CrossProcessPrefix + "/something").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("done"));
            IResponse response = await responsePromise.ConfigureAwait(false);
            Dictionary<string, string> headers = await response.Request.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(NormalizeConnection(headers), Is.EqualTo(NormalizeConnection(serverHeaders)));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should not get preflight CORS requests when intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotGetPreflightCorsRequestsWhenIntercepting()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("Playwright does not get CORS pre-flight on Android");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            List<string> requests = new List<string>();
            Server.SetRoute("/something", http =>
            {
                requests.Add(http.Request.Method);
                if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    http.Response.StatusCode = 204;
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    http.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS, DELETE";
                    http.Response.Headers["Access-Control-Allow-Headers"] = "*";
                    http.Response.Headers["Cache-Control"] = "no-cache";
                    return Task.CompletedTask;
                }

                http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                return http.Response.WriteAsync("done");
            });

            string text = await page.EvaluateAsync<string>(
                @"async url => {
                    const data = await fetch(url, {
                        method: 'DELETE',
                        headers: { 'X-PINGOTHER': 'pingpong' }
                    });
                    return data.text();
                }",
                CrossProcessPrefix + "/something").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("done"));
            Assert.That(requests, Is.EqualTo(new[] { "OPTIONS", "DELETE" }));

            requests.Clear();
            List<string> routed = new List<string>();
            await page.RouteAsync("**/something", route =>
            {
                routed.Add(route.Request.Method);
                return route.ContinueAsync();
            }).ConfigureAwait(false);

            text = await page.EvaluateAsync<string>(
                @"async url => {
                    const data = await fetch(url, {
                        method: 'DELETE',
                        headers: { 'X-PINGOTHER': 'pingpong' }
                    });
                    return data.text();
                }",
                CrossProcessPrefix + "/something").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("done"));
            Assert.That(routed, Is.EqualTo(new[] { "DELETE" }));
            if (TestConstants.IsFirefox)
            {
                Assert.That(requests, Is.EqualTo(new[] { "OPTIONS", "DELETE" }));
            }
            else
            {
                Assert.That(requests, Is.EqualTo(new[] { "DELETE" }));
            }
        }

        [PlaywrightTest("page-network-request.spec.ts", "should return postData")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnPostData()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/post", _ => Task.CompletedTask);
            IRequest request = null;
            page.Request += (_, r) => request = r;
            await page.EvaluateAsync<object>(
                "(() => fetch('./post', { method: 'POST', body: JSON.stringify({ foo: 'bar' }) }))()").ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.PostData, Is.EqualTo("{\"foo\":\"bar\"}"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should work with binary post data")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithBinaryPostData()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/post", _ => Task.CompletedTask);
            IRequest request = null;
            page.Request += (_, r) => request = r;
            await page.EvaluateAsync<object>(@"(async () => {
                await fetch('./post', { method: 'POST', body: new Uint8Array(Array.from(Array(256).keys())) });
            })()").ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            byte[] buffer = request.PostDataBuffer;
            Assert.That(buffer.Length, Is.EqualTo(256));
            for (int i = 0; i < 256; ++i)
            {
                Assert.That((int)buffer[i], Is.EqualTo(i));
            }
        }

        [PlaywrightTest("page-network-request.spec.ts", "should work with binary post data and interception")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithBinaryPostDataAndInterception()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/post", _ => Task.CompletedTask);
            IRequest request = null;
            await page.RouteAsync("/post", route => route.ContinueAsync()).ConfigureAwait(false);
            page.Request += (_, r) => request = r;
            await page.EvaluateAsync<object>(@"(async () => {
                await fetch('./post', { method: 'POST', body: new Uint8Array(Array.from(Array(256).keys())) });
            })()").ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            byte[] buffer = request.PostDataBuffer;
            Assert.That(buffer.Length, Is.EqualTo(256));
            for (int i = 0; i < 256; ++i)
            {
                Assert.That((int)buffer[i], Is.EqualTo(i));
            }
        }

        [PlaywrightTest("page-network-request.spec.ts", "should override post data content type")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldOverridePostDataContentType()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string serverContentType = null;
            Server.SetRoute("/post", http =>
            {
                serverContentType = http.Request.Headers["content-type"].ToString();
                return Task.CompletedTask;
            });
            await page.RouteAsync("**/post", route =>
            {
                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> header in route.Request.Headers)
                {
                    headers[header.Key] = header.Value;
                }

                headers["content-type"] = "application/x-www-form-urlencoded; charset=UTF-8";
                return route.ContinueAsync(new() { Headers = headers, PostData = System.Text.Encoding.UTF8.GetBytes(route.Request.PostData) });
            }).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(async () => {
                await fetch('./post', { method: 'POST', body: 'foo=bar' });
            })()").ConfigureAwait(false);
            Assert.That(serverContentType, Is.EqualTo("application/x-www-form-urlencoded; charset=UTF-8"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should get |undefined| with postData() when there is no post data")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldGetUndefinedWithPostDataWhenThereIsNoPostData()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Request.PostData, Is.Null);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should parse the json post data")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldParseTheJsonPostData()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/post", _ => Task.CompletedTask);
            IRequest request = null;
            page.Request += (_, r) => request = r;
            await page.EvaluateAsync<object>(
                "(() => fetch('./post', { method: 'POST', body: JSON.stringify({ foo: 'bar' }) }))()").ConfigureAwait(false);
            Assert.That(request, Is.Not.Null);
            using JsonDocument json = request.GetPayloadAsJson();
            Assert.That(json.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should parse the data if content-type is application/x-www-form-urlencoded")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldParseTheDataIfContentTypeIsApplicationXWwwFormUrlencoded()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/post", _ => Task.CompletedTask);
            Task<IRequest> requestTask = page.WaitForRequestAsync("**/post");
            await page.SetContentAsync("<form method='POST' action='/post'><input type='text' name='foo' value='bar'><input type='number' name='baz' value='123'><input type='submit'></form>").ConfigureAwait(false);
            await page.ClickAsync("input[type=submit]").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            using JsonDocument json = request.GetPayloadAsJson();
            Assert.That(json.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            Assert.That(json.RootElement.GetProperty("baz").GetString(), Is.EqualTo("123"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should parse the data if content-type is application/x-www-form-urlencoded; charset=UTF-8")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldParseTheDataIfContentTypeIsApplicationXWwwFormUrlencodedCharsetUtf8()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestPromise = page.WaitForRequestAsync("**/post");
            await page.EvaluateAsync<object>(@"(() => fetch('./post', {
                method: 'POST',
                headers: {
                    'content-type': 'application/x-www-form-urlencoded; charset=UTF-8',
                },
                body: 'foo=bar&baz=123'
            }))()").ConfigureAwait(false);
            IRequest request = await requestPromise.ConfigureAwait(false);
            using JsonDocument json = request.GetPayloadAsJson();
            Assert.That(json.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            Assert.That(json.RootElement.GetProperty("baz").GetString(), Is.EqualTo("123"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should get |undefined| with postDataJSON() when there is no post data")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldGetUndefinedWithPostDataJsonWhenThereIsNoPostData()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Request.GetPayloadAsJson(), Is.Null);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should return multipart/form-data")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnMultipartFormData()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("File content is missing in WebKit");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/post", _ => Task.CompletedTask);
            await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            Task<IRequest> requestPromise = page.WaitForRequestAsync("**/post");
            await page.EvaluateAsync<object>(@"(async () => {
                const body = new FormData();
                body.set('name1', 'value1');
                body.set('file', new File(['file-value'], 'foo.txt'));
                body.set('name2', 'value2');
                body.append('name2', 'another-value2');
                await fetch('/post', { method: 'POST', body });
            })()").ConfigureAwait(false);
            IRequest request = await requestPromise.ConfigureAwait(false);
            string contentType = await request.HeaderValueAsync("Content-Type").ConfigureAwait(false);
            Assert.That(contentType, Does.Match(new Regex("^multipart/form-data; boundary=(.*)$")));
            Match match = Regex.Match(contentType, "^multipart/form-data; boundary=(.*)$");
            string boundary = match.Groups[1].Value;
            string expected =
                "--" + boundary + "\r\nContent-Disposition: form-data; name=\"name1\"\r\n\r\nvalue1\r\n--" +
                boundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"foo.txt\"\r\nContent-Type: application/octet-stream\r\n\r\nfile-value\r\n--" +
                boundary + "\r\nContent-Disposition: form-data; name=\"name2\"\r\n\r\nvalue2\r\n--" +
                boundary + "\r\nContent-Disposition: form-data; name=\"name2\"\r\n\r\nanother-value2\r\n--" +
                boundary + "--\r\n";
            Assert.That(System.Text.Encoding.UTF8.GetString(request.PostDataBuffer), Is.EqualTo(expected));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should return event source")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEventSource()
        {
            EnsureServer();
            const string sseMessage = "{\"foo\": \"bar\"}";
            Server.SetRoute("/sse", async http =>
            {
                http.Response.Headers["content-type"] = "text/event-stream";
                http.Response.Headers["connection"] = "keep-alive";
                http.Response.Headers["cache-control"] = "no-cache";
                await http.Response.StartAsync().ConfigureAwait(false);
                await http.Response.WriteAsync("data: " + sseMessage + "\n\n").ConfigureAwait(false);
                await http.Response.Body.FlushAsync().ConfigureAwait(false);
                await Task.Delay(60_000).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            using JsonDocument message = await page.EvaluateAsync<JsonDocument>(@"(() => {
                const eventSource = new EventSource('/sse');
                return new Promise(resolve => {
                    eventSource.onmessage = e => resolve(JSON.parse(e.data));
                });
            })()").ConfigureAwait(false);
            Assert.That(message.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            Assert.That(requests[0].ResourceType, Is.EqualTo("eventsource"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should return navigation bit")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNavigationBit()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Dictionary<string, IRequest> requests = new Dictionary<string, IRequest>(StringComparer.Ordinal);
            page.Request += (_, request) => requests[request.Url.Split('/').Last()] = request;
            Server.SetRedirect("/rrredirect", "/frames/one-frame.html");
            await page.GoToAsync(Prefix + "/rrredirect").ConfigureAwait(false);
            Assert.That(requests["rrredirect"].IsNavigationRequest, Is.True);
            Assert.That(requests["one-frame.html"].IsNavigationRequest, Is.True);
            Assert.That(requests["frame.html"].IsNavigationRequest, Is.True);
            Assert.That(requests["script.js"].IsNavigationRequest, Is.False);
            Assert.That(requests["style.css"].IsNavigationRequest, Is.False);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should return navigation bit when navigating to image")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNavigationBitWhenNavigatingToImage()
        {
            EnsureServer();
            Server.SetRoute("/pptr.png", http =>
            {
                http.Response.ContentType = "image/png";
                return http.Response.Body.WriteAsync(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            await page.GoToAsync(Prefix + "/pptr.png").ConfigureAwait(false);
            Assert.That(requests[0].IsNavigationRequest, Is.True);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report raw headers")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportRawHeaders()
        {
            EnsureServer();
            List<Header> expectedHeaders = new List<Header>();
            Server.SetRoute("/headers", http =>
            {
                expectedHeaders.Clear();
                foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in http.Request.Headers)
                {
                    foreach (string value in header.Value)
                    {
                        expectedHeaders.Add(new Header { Name = header.Key, Value = value });
                    }
                }

                if (TestConstants.IsWebKit && TestConstants.IsWindows)
                {
                    expectedHeaders = expectedHeaders
                        .Where(h => !string.Equals(h.Name, "accept-encoding", StringComparison.OrdinalIgnoreCase))
                        .Select(e =>
                        {
                            if (!string.Equals(e.Name, "accept-language", StringComparison.OrdinalIgnoreCase))
                            {
                                return e;
                            }

                            string[] values = e.Value.Split(',').Select(v => v.Trim()).ToArray();
                            if (values.Length == 1 || values[0] != values[1])
                            {
                                return e;
                            }

                            return new Header { Name = e.Name, Value = values[0] };
                        })
                        .ToList();
                }

                if (TestConstants.IsFirefox)
                {
                    expectedHeaders = expectedHeaders
                        .Where(h => !string.Equals(h.Name, "priority", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return Task.CompletedTask;
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForRequestAsync("**/*");
            await page.EvaluateAsync<object>(@"(() => fetch('/headers', {
                headers: [
                    ['header-a', 'value-a'],
                    ['header-b', 'value-b'],
                    ['header-a', 'value-a-1'],
                    ['header-a', 'value-a-2'],
                ]
            }))()").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            List<Header> headers = new List<Header>(await request.HeadersArrayAsync().ConfigureAwait(false));
            expectedHeaders = expectedHeaders
                .Select(e => string.Equals(e.Name, "connection", StringComparison.OrdinalIgnoreCase)
                    ? new Header { Name = "connection", Value = e.Value.ToLowerInvariant() }
                    : e)
                .ToList();
            headers = headers
                .Select(e => string.Equals(e.Name, "connection", StringComparison.OrdinalIgnoreCase)
                    ? new Header { Name = "connection", Value = e.Value.ToLowerInvariant() }
                    : e)
                .ToList();
            expectedHeaders.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            headers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            Assert.That(JsonSerializer.Serialize(headers), Is.EqualTo(JsonSerializer.Serialize(expectedHeaders)));
            Assert.That(await request.HeaderValueAsync("header-a").ConfigureAwait(false), Is.EqualTo("value-a, value-a-1, value-a-2"));
            Assert.That(await request.HeaderValueAsync("not-there").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report raw response headers in redirects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportRawResponseHeadersInRedirects()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit won't give us raw headers for redirects");
            }

            EnsureServer();
            Server.SetRoute("/redirect/1.html", http =>
            {
                http.Response.Headers["sec-test-header"] = "1.html";
                http.Response.Redirect("/redirect/2.html");
                return Task.CompletedTask;
            });
            Server.SetRoute("/redirect/2.html", http =>
            {
                http.Response.Headers["sec-test-header"] = "2.html";
                http.Response.Redirect("/empty.html");
                return Task.CompletedTask;
            });
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers["sec-test-header"] = "empty.html";
                return http.Response.WriteAsync("done");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string[] expectedUrls = new[]
            {
                Prefix + "/redirect/1.html",
                Prefix + "/redirect/2.html",
                Prefix + "/empty.html",
            };
            string[] expectedHeaders = new[] { "1.html", "2.html", "empty.html" };

            IResponse response = await page.GoToAsync(Prefix + "/redirect/1.html").ConfigureAwait(false);
            List<string> redirectChain = new List<string>();
            List<string> headersChain = new List<string>();
            for (IRequest req = response.Request; req != null; req = req.RedirectedFrom)
            {
                redirectChain.Insert(0, req.Url);
                IResponse res = await req.ResponseAsync().ConfigureAwait(false);
                Dictionary<string, string> headers = await res.AllHeadersAsync().ConfigureAwait(false);
                headersChain.Insert(0, headers["sec-test-header"]);
            }

            Assert.That(redirectChain, Is.EqualTo(expectedUrls));
            Assert.That(headersChain, Is.EqualTo(expectedHeaders));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should report all cookies in one header")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportAllCookiesInOneHeader()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(() => {
                document.cookie = 'myCookie=myValue';
                document.cookie = 'myOtherCookie=myOtherValue';
            })()").ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string cookie = (await response.Request.AllHeadersAsync().ConfigureAwait(false))["cookie"];
            Assert.That(cookie, Is.EqualTo("myCookie=myValue; myOtherCookie=myOtherValue"));
        }

        [PlaywrightTest("page-network-request.spec.ts", "should not allow to access frame on popup main request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotAllowToAccessFrameOnPopupMainRequest()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<a href=\"" + EmptyPage + "\" target=\"_blank\">click me</a>").ConfigureAwait(false);
            TaskCompletionSource<IRequest> requestPromise = new TaskCompletionSource<IRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Request += (_, e) => requestPromise.TrySetResult(e);
            Task<IPage> popupPromise = context.WaitForPageAsync();
            Task clicked = page.GetByText("click me").ClickAsync();
            IRequest request = await requestPromise.Task.ConfigureAwait(false);

            Assert.That(request.IsNavigationRequest, Is.True);
            Exception exception = Assert.Catch(() =>
            {
                IFrame unused = request.Frame;
            });
            Assert.That(exception.Message, Does.Contain("Frame for this navigation request is not available"));

            IResponse response = await request.ResponseAsync().ConfigureAwait(false);
            await response.FinishedAsync().ConfigureAwait(false);
            await popupPromise.ConfigureAwait(false);
            await clicked.ConfigureAwait(false);
        }

        [PlaywrightTest("page-network-request.spec.ts", "page.reload return 304 status code")]
        [Test]
        [Timeout(30_000)]
        public async Task PageReloadReturn304StatusCode()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Does not send second request");
            }

            EnsureServer();
            int requestNumber = 0;
            Server.SetRoute("/test.html", http =>
            {
                ++requestNumber;
                http.Response.Headers["cf-cache-status"] = "DYNAMIC";
                http.Response.Headers["Content-Type"] = "text/html;charset=UTF-8";
                http.Response.Headers["Last-Modified"] = "Fri, 05 Jan 2024 01:56:20 GMT";
                http.Response.Headers["Vary"] = "Access-Control-Request-Headers";
                if (requestNumber == 1)
                {
                    http.Response.StatusCode = 200;
                }
                else
                {
                    http.Response.StatusCode = 304;
                    http.Response.Headers["Status"] = "Not Modified";
                }

                return http.Response.WriteAsync("<div>Test</div>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response1 = await page.GoToAsync(Prefix + "/test.html").ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(200));
            IResponse response2 = await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(requestNumber, Is.EqualTo(2));
            if (TestConstants.IsChromium)
            {
                Assert.That(response2.Status, Is.EqualTo(200));
                Assert.That(response2.StatusText, Is.EqualTo("OK"));
                Assert.That(await response2.TextAsync().ConfigureAwait(false), Is.EqualTo("<div>Test</div>"));
            }
            else
            {
                Assert.That(response2.Status, Is.EqualTo(304));
                Assert.That(response2.StatusText, Is.EqualTo("Not Modified"));
            }
        }

        [PlaywrightTest("page-network-request.spec.ts", "should handle mixed-content blocked requests")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleMixedContentBlockedRequests()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("FF and WK actually succeed with the request, and block afterwards");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/mixedcontent.html", route => route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Body = "<html><head><style>@font-face { font-family: 'pwtest-iconfont'; src: url('http://another.com/iconfont.woff2') format('woff2'); } body { font-family: 'pwtest-iconfont'; }</style></head><body>+-</body></html>" })).ConfigureAwait(false);
            await page.RouteAsync("**/iconfont.woff2", route => route.FulfillAsync(new() { BodyBytes = new byte[] { 0 } })).ConfigureAwait(false);

            Task<IRequest> failedTask = page.WaitForRequestFailedAsync(r => r.Url.Contains("iconfont.woff2", StringComparison.Ordinal));
            Task gotoTask = page.GoToAsync("https://example.com/mixedcontent.html");
            IRequest request = await failedTask.ConfigureAwait(false);
            await gotoTask.ConfigureAwait(false);
            Dictionary<string, string> headers = await request.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(headers["origin"], Is.Not.Null.And.Not.Empty);
            Assert.That(request.Failure, Is.EqualTo("mixed-content"));
        }
    }
}
