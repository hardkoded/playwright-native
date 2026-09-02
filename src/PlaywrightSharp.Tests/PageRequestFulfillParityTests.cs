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
    /// Official <c>page-request-fulfill.spec.ts</c> parity for <see cref="IRoute.FulfillAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android, BiDi-only):
    /// Android / Electron-only <c>it.skip</c> / <c>it.fixme</c> branches.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageRequestFulfillParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19774;
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
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("page-request-fulfill.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(201, headers: new Dictionary<string, string> { ["foo"] = "bar" },
                        contentType: "text/html",
                        body: "Yo, page!");
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(201));
                Assert.That(response.GetHeaderValue("foo"), Is.EqualTo("bar"));
                Assert.That(await page.EvaluateAsync<string>("() => document.body.textContent").ConfigureAwait(false), Is.EqualTo("Yo, page!"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should work with buffer as body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBufferAsBody()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(new() { Status = 200, ContentType = "text/plain", BodyBytes = Encoding.UTF8.GetBytes("Yo, page!") });
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(await page.EvaluateAsync<string>("() => document.body.textContent").ConfigureAwait(false), Is.EqualTo("Yo, page!"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should work with status code 422")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithStatusCode422()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(new() { Status = 422, Body = "Yo, page!" });
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(422));
                Assert.That(response.StatusText, Is.EqualTo("Unprocessable Entity"));
                Assert.That(await page.EvaluateAsync<string>("() => document.body.textContent").ConfigureAwait(false), Is.EqualTo("Yo, page!"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with unuassigned status codes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithUnuassignedStatusCodes()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                TaskCompletionSource<Exception> fulfillTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await page.RouteAsync("**/data.json", route =>
                {
                    route.FulfillAsync(new() { Status = 430, Body = "Yo, page!" }).ContinueWith(
                        t => fulfillTcs.TrySetResult(t.Exception?.InnerException ?? (t.IsFaulted ? t.Exception : null)),
                        TaskScheduler.Default);
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                JsonElement response = await page.EvaluateAsync<JsonElement>(
                    "async url => { const { status, statusText } = await fetch(url); return { status, statusText }; }",
                    Prefix + "/data.json").ConfigureAwait(false);
                Exception error = await fulfillTcs.Task.ConfigureAwait(false);
                Assert.That(error, Is.Null);
                Assert.That(response.GetProperty("status").GetInt32(), Is.EqualTo(430));
                Assert.That(response.GetProperty("statusText").GetString(), Is.EqualTo("Unknown"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should not throw if request was cancelled by the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowIfRequestWasCancelledByThePage()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                TaskCompletionSource<IRoute> intercept = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await page.RouteAsync("**/data.json", route => intercept.TrySetResult(route)).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                _ = page.EvaluateAsync(
                    "url => { globalThis.controller = new AbortController(); return fetch(url, { signal: globalThis.controller.signal }); }",
                    Prefix + "/data.json").ContinueWith(_ => 0, TaskScheduler.Default);
                IRoute route = await intercept.Task.ConfigureAwait(false);
                Task<IRequest> failurePromise = page.WaitForEventAsync(PageEvent.RequestFailed);
                await page.EvaluateAsync("() => globalThis.controller.abort()").ConfigureAwait(false);
                IRequest cancelledRequest = await failurePromise.ConfigureAwait(false);
                Assert.That(cancelledRequest.Failure, Is.Not.Null);
                Assert.That(cancelledRequest.Failure, Does.Match("cancelled|aborted").IgnoreCase);
                await route.FulfillAsync(new() { Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should allow mocking binary responses")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowMockingBinaryResponses()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                byte[] imageBuffer = File.ReadAllBytes(TestUtils.GetWebServerFile("pptr.png"));
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(new() { ContentType = "image/png", BodyBytes = imageBuffer });
                }).ConfigureAwait(false);
                await page.EvaluateAsync(
                    "PREFIX => { const img = document.createElement('img'); img.src = PREFIX + '/does-not-exist.png'; document.body.appendChild(img); return new Promise(fulfill => img.onload = fulfill); }",
                    Prefix).ConfigureAwait(false);
                IElementHandle img = await page.QuerySelectorAsync("img").ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("mock-binary-response.png", await img.ScreenshotAsync().ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should allow mocking svg with charset")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowMockingSvgWithCharset()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(new() { ContentType = "image/svg+xml ; charset=utf-8", Body = "<svg width=\"50\" height=\"50\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"10\" y=\"10\" width=\"30\" height=\"30\" stroke=\"black\" fill=\"transparent\" stroke-width=\"5\"/></svg>" });
                }).ConfigureAwait(false);
                await page.EvaluateAsync(
                    "PREFIX => { const img = document.createElement('img'); img.src = PREFIX + '/does-not-exist.svg'; document.body.appendChild(img); return new Promise((f, r) => { img.onload = f; img.onerror = r; }); }",
                    Prefix).ConfigureAwait(false);
                IElementHandle img = await page.QuerySelectorAsync("img").ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("mock-svg.png", await img.ScreenshotAsync().ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should work with file path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithFilePath()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route => route.FulfillAsync(new() { ContentType = "shouldBeIgnored", Path = TestUtils.GetWebServerFile("pptr.png") })).ConfigureAwait(false);
                await page.EvaluateAsync(
                    "PREFIX => { const img = document.createElement('img'); img.src = PREFIX + '/does-not-exist.png'; document.body.appendChild(img); return new Promise(fulfill => img.onload = fulfill); }",
                    Prefix).ConfigureAwait(false);
                IElementHandle img = await page.QuerySelectorAsync("img").ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("mock-binary-response.png", await img.ScreenshotAsync().ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should stringify intercepted request response headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStringifyInterceptedRequestResponseHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(200, headers: new Dictionary<string, string> { ["foo"] = "true" },
                        body: "Yo, page!");
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(response.GetHeaderValue("foo"), Is.EqualTo("true"));
                Assert.That(await page.EvaluateAsync<string>("() => document.body.textContent").ConfigureAwait(false), Is.EqualTo("Yo, page!"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should not modify the headers sent to the server")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotModifyTheHeadersSentToTheServer()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                List<Dictionary<string, string>> interceptedRequests = new();
                await page.RouteAsync(Prefix + "/unused", _ => Task.CompletedTask).ConfigureAwait(false);
                Server.SetRoute("/something", async http =>
                {
                    interceptedRequests.Add(SnapshotHeaders(http.Request));
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    await http.Response.WriteAsync("done").ConfigureAwait(false);
                });

                string text = await page.EvaluateAsync<string>(
                    "async url => { const data = await fetch(url); return data.text(); }",
                    CrossProcessPrefix + "/something").ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("done"));

                await page.RouteAsync(CrossProcessPrefix + "/something", route =>
                {
                    _ = route.ContinueAsync(new() { Headers = HeadersOf(route.Request) });
                }).ConfigureAwait(false);

                string textAfterRoute = await page.EvaluateAsync<string>(
                    "async url => { const data = await fetch(url); return data.text(); }",
                    CrossProcessPrefix + "/something").ConfigureAwait(false);
                Assert.That(textAfterRoute, Is.EqualTo("done"));
                Assert.That(interceptedRequests, Has.Count.EqualTo(2));
                Assert.That(interceptedRequests[1], Is.EqualTo(interceptedRequests[0]));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should include the origin header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeTheOriginHeader()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                IRequest interceptedRequest = null;
                await page.RouteAsync(CrossProcessPrefix + "/something", route =>
                {
                    interceptedRequest = route.Request;
                    _ = route.FulfillAsync(
                        headers: new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                        contentType: "text/plain",
                        body: "done");
                }).ConfigureAwait(false);
                string text = await page.EvaluateAsync<string>(
                    "async url => { const data = await fetch(url); return data.text(); }",
                    CrossProcessPrefix + "/something").ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("done"));
                Assert.That(interceptedRequest.GetHeaderValue("origin"), Is.EqualTo(Prefix));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with global fetch result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithGlobalFetchResult()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", async route =>
                {
                    await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
                    IAPIResponse apiResponse = await request.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
                    await route.FulfillAsync(apiResponse).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                JsonDocument json = await response.GetJsonAsync().ConfigureAwait(false);
                Assert.That(json.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with fetch result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithFetchResult()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", async route =>
                {
                    IAPIResponse apiResponse = await page.APIRequest.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
                    await route.FulfillAsync(apiResponse).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                JsonDocument json = await response.GetJsonAsync().ConfigureAwait(false);
                Assert.That(json.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with fetch result and overrides")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithFetchResultAndOverrides()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", async route =>
                {
                    IAPIResponse apiResponse = await page.APIRequest.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
                    await route.FulfillAsync(
                        apiResponse,
                        status: 201,
                        headers: new Dictionary<string, string>
                        {
                            ["Content-Type"] = "application/json",
                            ["foo"] = "bar",
                        }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(201));
                Dictionary<string, string> all = await response.AllHeadersAsync().ConfigureAwait(false);
                Assert.That(all["foo"], Is.EqualTo("bar"));
                JsonDocument json = await response.GetJsonAsync().ConfigureAwait(false);
                Assert.That(json.RootElement.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fetch original request and fulfill")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFetchOriginalRequestAndFulfill()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", async route =>
                {
                    IAPIResponse apiResponse = await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);
                    await route.FulfillAsync(apiResponse).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/title.html").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Woof-Woof"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with multiple set-cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithMultipleSetCookie()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                string[] cookies = { "a=b", "c=d" };
                await page.RouteAsync("**/multiple-set-cookie.html", route =>
                {
                    _ = route.FulfillAsync(200, headers: new Dictionary<string, string>
                    {
                        ["X-Header-1"] = "v1",
                        ["Set-Cookie"] = string.Join("\n", cookies),
                        ["X-Header-2"] = "v2",
                    },
                        body: string.Empty);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/multiple-set-cookie.html").ConfigureAwait(false);
                string cookie = await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false);
                List<string> parsed = new();
                foreach (string part in cookie.Split(';'))
                {
                    parsed.Add(part.Trim());
                }

                parsed.Sort(StringComparer.Ordinal);
                Assert.That(parsed, Is.EqualTo(cookies));
                Assert.That(await response.HeaderValueAsync("X-Header-1").ConfigureAwait(false), Is.EqualTo("v1"));
                Assert.That(await response.HeaderValueAsync("X-Header-2").ConfigureAwait(false), Is.EqualTo("v2"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with fetch response that has multiple set-cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithFetchResponseThatHasMultipleSetCookie()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/empty.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=b");
                    http.Response.Headers.Append("Set-Cookie", "c=d");
                    http.Response.ContentType = "text/html";
                    return Task.CompletedTask;
                });
                await page.RouteAsync("**/empty.html", async route =>
                {
                    IAPIResponse apiResponse = await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);
                    await route.FulfillAsync(apiResponse).ConfigureAwait(false);
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                string cookie = await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false);
                List<string> parsed = new();
                foreach (string part in cookie.Split(';'))
                {
                    parsed.Add(part.Trim());
                }

                parsed.Sort(StringComparer.Ordinal);
                Assert.That(parsed, Is.EqualTo(new[] { "a=b", "c=d" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "headerValue should return set-cookie from intercepted response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HeaderValueShouldReturnSetCookieFromInterceptedResponse()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Set-Cookie is missing in response after interception");
            }

            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Set-Cookie with \\n in intercepted response does not pass validation in WebCore, see also https://github.com/microsoft/playwright/pull/9273");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/empty.html", route =>
                {
                    _ = route.FulfillAsync(200, headers: new Dictionary<string, string> { ["Set-Cookie"] = "a=b" },
                        body: string.Empty);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await response.HeaderValueAsync("Set-Cookie").ConfigureAwait(false), Is.EqualTo("a=b"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with har response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithHarResponse()
        {
            await WithPageAsync(async page =>
            {
                string harPath = Path.Combine(TestUtils.FindParentDirectory("PlaywrightSharp.Tests"), "Assets", "har-fulfill.har");
                JsonDocument har = JsonDocument.Parse(await File.ReadAllTextAsync(harPath).ConfigureAwait(false));
                await page.RouteAsync("**/*", async route =>
                {
                    JsonElement response = FindHarResponse(har, route.Request.Url);
                    Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
                    if (response.TryGetProperty("headers", out JsonElement headerList))
                    {
                        foreach (JsonElement header in headerList.EnumerateArray())
                        {
                            headers[header.GetProperty("name").GetString()] = header.GetProperty("value").GetString();
                        }
                    }

                    JsonElement content = response.GetProperty("content");
                    string text = content.TryGetProperty("text", out JsonElement textElement)
                        ? textElement.GetString() ?? string.Empty
                        : string.Empty;
                    string encoding = content.TryGetProperty("encoding", out JsonElement encodingElement)
                        ? encodingElement.GetString()
                        : null;
                    byte[] body = string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase)
                        ? Convert.FromBase64String(text)
                        : Encoding.UTF8.GetBytes(text);
                    await route.FulfillAsync(new() { Status = response.GetProperty("status").GetInt32(), Headers = headers, BodyBytes = body }).ConfigureAwait(false);
                }).ConfigureAwait(false);

                await page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
                await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(0, 255, 255)").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill preload link requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillPreloadLinkRequests()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                bool intercepted = false;
                await page.RouteAsync("**/one-style.css", route =>
                {
                    intercepted = true;
                    _ = route.FulfillAsync(200, headers: new Dictionary<string, string>
                    {
                        ["content-type"] = "text/css; charset=utf-8",
                        ["cache-control"] = "no-cache, no-store",
                        ["custom"] = "value",
                    },
                        body: "body { background-color: green; }");
                }).ConfigureAwait(false);
                Task<IResponse> response = page.WaitForResponseAsync("**/one-style.css");
                await Task.WhenAll(response, page.GoToAsync(Prefix + "/preload.html")).ConfigureAwait(false);
                Assert.That(await response.Result.HeaderValueAsync("custom").ConfigureAwait(false), Is.EqualTo("value"));
                await page.WaitForFunctionAsync("() => window['preloadedStyles']", null, polling: "raf").ConfigureAwait(false);
                Assert.That(intercepted, Is.True);
                string color = await page.EvaluateAsync<string>("() => window.getComputedStyle(document.body).backgroundColor").ConfigureAwait(false);
                Assert.That(color, Is.EqualTo("rgb(0, 128, 0)"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill json")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillJson()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/data.json", route =>
                {
                    _ = route.FulfillAsync(201, headers: new Dictionary<string, string> { ["foo"] = "bar" },
                        json: new Dictionary<string, string> { ["bar"] = "baz" });
                }).ConfigureAwait(false);
                Task<IResponse> response = page.WaitForResponseAsync("**/*");
                Task<string> body = page.EvaluateAsync<string>("() => fetch('./data.json').then(r => r.text())");
                await Task.WhenAll(response, body).ConfigureAwait(false);
                Assert.That(response.Result.Status, Is.EqualTo(201));
                Assert.That(response.Result.GetHeaderValue("content-type"), Is.EqualTo("application/json"));
                Assert.That(body.Result, Is.EqualTo(JsonSerializer.Serialize(new Dictionary<string, string> { ["bar"] = "baz" })));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should fulfill with gzip and readback")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithGzipAndReadback()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.EnableGzip("/one-style.html");
                await page.RouteAsync("**/one-style.html", async route =>
                {
                    RouteFetchResult fetched = await route.FetchResultAsync().ConfigureAwait(false);
                    Assert.That(HeaderValue(fetched.Headers, "content-encoding"), Is.EqualTo("gzip"));
                    await route.FulfillAsync(fetched).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(await page.Locator("div").TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello, world!"));
                Assert.That(
                    await page.EvaluateAsync<string>("() => window.getComputedStyle(document.body).backgroundColor").ConfigureAwait(false),
                    Is.EqualTo("rgb(255, 192, 203)"));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("<div>hello, world!</div>"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should not go to the network for fulfilled requests body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotGoToTheNetworkForFulfilledRequestsBody()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/one-style.css", route =>
                {
                    return route.FulfillAsync(new() { Status = 404, ContentType = "text/plain", Body = "Not Found! (mocked)" });
                }).ConfigureAwait(false);

                bool serverHit = false;
                Server.SetRoute("/one-style.css", async http =>
                {
                    serverHit = true;
                    http.Response.ContentType = "text/css";
                    await http.Response.WriteAsync("body { background-color: green; }").ConfigureAwait(false);
                });

                Task<IResponse> responsePromise = page.WaitForResponseAsync("**/one-style.css");
                await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                IResponse response = await responsePromise.ConfigureAwait(false);
                byte[] body = await response.BodyAsync().ConfigureAwait(false);
                Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo("Not Found! (mocked)"));
                Assert.That(serverHit, Is.False);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-fulfill.spec.ts", "should return body for fulfilled responses")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnBodyForFulfilledResponses()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                foreach (int status in new[] { 100, 200, 404, 500 })
                {
                    string bodyOverride = "Custom body " + status.ToString(CultureInfo.InvariantCulture);
                    await page.RouteAsync("**/one-style.css", route =>
                    {
                        return route.FulfillAsync(new() { Status = status, ContentType = "text/plain", Body = bodyOverride });
                    }).ConfigureAwait(false);

                    Task<IResponse> responsePromise = page.WaitForResponseAsync("**/one-style.css");
                    await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                    IResponse response = await responsePromise.ConfigureAwait(false);
                    byte[] body = await response.BodyAsync().ConfigureAwait(false);
                    Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo(bodyOverride));
                    await page.UnrouteAllAsync().ConfigureAwait(false);
                }
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

        private static JsonElement FindHarResponse(JsonDocument har, string url)
        {
            JsonElement entries = har.RootElement.GetProperty("log").GetProperty("entries");
            string current = url;
            JsonElement? found = null;
            while (!string.IsNullOrWhiteSpace(current))
            {
                found = null;
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    if (string.Equals(entry.GetProperty("request").GetProperty("url").GetString(), current, StringComparison.Ordinal))
                    {
                        found = entry;
                        break;
                    }
                }

                Assert.That(found.HasValue, Is.True, url);
                current = found.Value.GetProperty("response").GetProperty("redirectURL").GetString();
                if (string.IsNullOrWhiteSpace(current))
                {
                    return found.Value.GetProperty("response");
                }
            }

            Assert.Fail(url);
            return default;
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
    }
}
