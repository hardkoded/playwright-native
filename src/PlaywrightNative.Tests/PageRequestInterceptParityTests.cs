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
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-request-intercept.spec.ts</c> parity for intercepted
    /// <see cref="IRoute.FetchAsync"/> / <see cref="IRoute.FulfillAsync(IAPIResponse, int?, IEnumerable{KeyValuePair{string, string}}, string, string, byte[])"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android, BiDi-only):
    /// Android / Electron-only <c>it.skip</c> / <c>it.fixme</c> branches.
    /// Skipped (<c>it.fixme(browserName !== 'firefox')</c>):
    /// <c>should intercept multipart/form-data request body</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageRequestInterceptParityTests : PageTestEx
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

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19776;
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

        [PlaywrightTest("page-request-intercept.spec.ts", "should fulfill intercepted response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillInterceptedResponse()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", async route =>
                {
                    IAPIResponse apiResponse = await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);
                    await route.FulfillAsync(
                        apiResponse,
                        status: 201,
                        headers: new Dictionary<string, string> { ["foo"] = "bar" },
                        contentType: "text/plain",
                        body: "Yo, page!").ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(201));
                Assert.That(response.GetHeaderValue("foo"), Is.EqualTo("bar"));
                Assert.That(response.GetHeaderValue("content-type"), Is.EqualTo("text/plain"));
                Assert.That(
                    await page.EvaluateAsync<string>("() => document.body.textContent").ConfigureAwait(false),
                    Is.EqualTo("Yo, page!"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should fulfill response with empty body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillResponseWithEmptyBody()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", async route =>
                {
                    IAPIResponse apiResponse = await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);
                    await route.FulfillAsync(
                        apiResponse,
                        headers: new Dictionary<string, string> { ["content-length"] = "0" },
                        status: 201,
                        body: string.Empty).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/title.html").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(201));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should override with defaults when intercepted response not provided")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverrideWithDefaultsWhenInterceptedResponseNotProvided()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/empty.html", async http =>
                {
                    http.Response.Headers["foo"] = "bar";
                    await http.Response.WriteAsync("my content").ConfigureAwait(false);
                });
                await page.RouteAsync("**/*", async route =>
                {
                    await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);
                    await route.FulfillAsync(new() { Status = 201 }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(201));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
                Dictionary<string, string> headers = HeaderMap.All(response.Headers);
                if (TestConstants.IsWebKit)
                {
                    Assert.That(headers, Is.EqualTo(new Dictionary<string, string> { ["content-type"] = "text/plain" }));
                }
                else
                {
                    Assert.That(headers, Is.EqualTo(new Dictionary<string, string>()));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should fulfill with any response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithAnyResponse()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/sample", async http =>
                {
                    http.Response.Headers["foo"] = "bar";
                    await http.Response.WriteAsync("Woo-hoo").ConfigureAwait(false);
                });
                IAPIResponse sampleResponse = await page.APIRequest.GetAsync(Prefix + "/sample").ConfigureAwait(false);

                await page.RouteAsync("**/*", async route =>
                {
                    await route.FulfillAsync(
                        sampleResponse,
                        status: 201,
                        contentType: "text/plain").ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(201));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("Woo-hoo"));
                Assert.That(response.GetHeaderValue("foo"), Is.EqualTo("bar"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should support fulfill after intercept")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportFulfillAfterIntercept()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> requestPromise = Server.WaitForRequest("/title.html", request => request.Path.ToString());
                await page.RouteAsync("**", async route =>
                {
                    IAPIResponse apiResponse = await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);
                    await route.FulfillAsync(apiResponse).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/title.html").ConfigureAwait(false);
                string requestPath = await requestPromise.ConfigureAwait(false);
                Assert.That(requestPath, Is.EqualTo("/title.html"));
                string original = File.ReadAllText(Path.Combine(
                    TestUtils.FindParentDirectory("PlaywrightNative.TestServer"),
                    "wwwroot",
                    "title.html"));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(original));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should give access to the intercepted response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGiveAccessToTheInterceptedResponse()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);

                TaskCompletionSource<IRoute> routeTcs = new();
                await page.RouteAsync("**/title.html", route =>
                {
                    routeTcs.TrySetResult(route);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                Task evalPromise = page.EvaluateAsync("url => fetch(url)", Prefix + "/title.html");

                IRoute route = await routeTcs.Task.ConfigureAwait(false);
                IAPIResponse response = await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);

                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(response.StatusText, Is.EqualTo("OK"));
                Assert.That(response.Ok, Is.True);
                Assert.That(response.Url.EndsWith("/title.html", StringComparison.Ordinal), Is.True);
                Assert.That(response.Headers["content-type"], Is.EqualTo("text/html; charset=utf-8"));
                List<Header> contentType = response.HeadersArray
                    .Where(entry => string.Equals(entry.Name, "content-type", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                Assert.That(contentType, Has.Exactly(1).Items);
                Assert.That(contentType[0].Name, Is.EqualTo("Content-Type"));
                Assert.That(contentType[0].Value, Is.EqualTo("text/html; charset=utf-8"));

                await Task.WhenAll(route.FulfillAsync(response), evalPromise).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should give access to the intercepted response body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGiveAccessToTheInterceptedResponseBody()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);

                TaskCompletionSource<IRoute> routeTcs = new();
                await page.RouteAsync("**/simple.json", route =>
                {
                    routeTcs.TrySetResult(route);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                Task evalPromise = IgnoreAsync(() => page.EvaluateAsync("url => fetch(url)", Prefix + "/simple.json"));

                IRoute route = await routeTcs.Task.ConfigureAwait(false);
                IAPIResponse response = await page.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);

                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("{\"foo\": \"bar\"}\n"));

                await Task.WhenAll(route.FulfillAsync(response), evalPromise).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should intercept multipart/form-data request body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldInterceptMultipartFormDataRequestBody()
        {
            Assert.Ignore("it.fixme(browserName !== 'firefox')");
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should fulfill intercepted response using alias")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillInterceptedResponseUsingAlias()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", async route =>
                {
                    RouteFetchResult fetched = await route.FetchResultAsync().ConfigureAwait(false);
                    await route.FulfillAsync(fetched).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(response.GetHeaderValue("content-type"), Does.Contain("text/html"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should support timeout option in route.fetch")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTimeoutOptionInRouteFetch()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/slow", async http =>
                {
                    http.Response.StatusCode = 200;
                    http.Response.ContentLength = 4096;
                    http.Response.ContentType = "text/html";
                    await http.Response.StartAsync().ConfigureAwait(false);
                    await Task.Delay(60_000).ConfigureAwait(false);
                });
                await page.RouteAsync("**/*", async route =>
                {
                    Exception error = await CatchAsync(() => route.FetchAsync(new() { Timeout = 1000 })).ConfigureAwait(false);
                    Assert.That(error, Is.Not.Null);
                    Assert.That(error.Message, Does.Contain("route.fetch: Timeout 1000ms exceeded"));
                }).ConfigureAwait(false);
                TimeoutException gotoError = Assert.ThrowsAsync<TimeoutException>(
                    () => page.GoToAsync(Prefix + "/slow", timeout: 2000));
                Assert.That(gotoError.Message, Does.Contain("Timeout 2000ms exceeded"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should not follow redirects when maxRedirects is set to 0 in route.fetch")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFollowRedirectsWhenMaxRedirectsIsSetTo0InRouteFetch()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRedirect("/foo", "/empty.html");
                await page.RouteAsync("**/*", async route =>
                {
                    RouteFetchResult fetched = await route.FetchResultAsync(new() { MaxRedirects = 0 }).ConfigureAwait(false);
                    Assert.That(fetched.Headers["location"], Is.EqualTo("/empty.html"));
                    Assert.That(fetched.Status, Is.EqualTo(302));
                    await route.FulfillAsync(new() { Body = "hello" }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/foo").ConfigureAwait(false);
                Assert.That(await page.ContentAsync().ConfigureAwait(false), Does.Contain("hello"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should intercept with url override")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptWithUrlOverride()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*.html", async route =>
                {
                    RouteFetchResult fetched = await route.FetchResultAsync(new() { Url = Prefix + "/one-style.html" }).ConfigureAwait(false);
                    await route.FulfillAsync(fetched).ConfigureAwait(false);
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(await response.BodyAsync().ConfigureAwait(false)), Does.Contain("one-style.css"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should intercept with post data override")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptWithPostDataOverride()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> requestPromise = Server.WaitForRequest("/empty.html", request =>
                {
                    using StreamReader reader = new(request.Body);
                    return reader.ReadToEnd();
                });
                await page.RouteAsync("**/*.html", async route =>
                {
                    RouteFetchResult fetched = await route.FetchResultAsync(
                        new() { PostData = System.Text.Encoding.UTF8.GetBytes("{\"foo\":\"bar\"}") }).ConfigureAwait(false);
                    await route.FulfillAsync(fetched).ConfigureAwait(false);
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                string postBody = await requestPromise.ConfigureAwait(false);
                Assert.That(postBody, Is.EqualTo("{\"foo\":\"bar\"}"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "request.postData is not null when fetching FormData with a Blob")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RequestPostDataIsNotNullWhenFetchingFormDataWithABlob()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("it.fixme(webkit): The body is empty in WebKit when intercepting");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync(
                    "<script>\n" +
                    "  function doStuff() {\n" +
                    "    const formData = new FormData();\n" +
                    "    formData.append('file', new Blob([\"hello\"], { type: \"text/plain\" }));\n" +
                    "    fetch('/upload', {\n" +
                    "      method: 'POST',\n" +
                    "      body: formData\n" +
                    "    });\n" +
                    "  }\n" +
                    "</script>\n" +
                    "<body>\n" +
                    "<button onclick=\"doStuff()\" data-testid=\"click-me\">Click me!</button>\n" +
                    "</body>").ConfigureAwait(false);
                TaskCompletionSource<string> postDataTcs = new();
                await page.RouteAsync(Prefix + "/upload", async route =>
                {
                    Assert.That(route.Request.Method, Is.EqualTo("POST"));
                    postDataTcs.TrySetResult(route.Request.PostData);
                    await route.FulfillAsync(new() { Status = 200, Body = "ok" }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                await page.GetByTestId("click-me").ClickAsync().ConfigureAwait(false);
                string postData = await postDataTcs.Task.ConfigureAwait(false);
                Assert.That(postData, Does.Contain("Content-Disposition: form-data; name=\"file\"; filename=\"blob\""));
                Assert.That(postData, Does.Contain("\r\nhello\r\n"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-intercept.spec.ts", "should abort favicon requests if interception is enabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbortFaviconRequestsIfInterceptionIsEnabled()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                int requestCount = 0;
                Server.SetRoute("/favicon.ico", http =>
                {
                    requestCount++;
                    http.Response.ContentType = "text/plain";
                    return http.Response.WriteAsync("my content");
                });
                await page.RouteAsync("**/*", route => route.FulfillAsync(new() { Status = 200, Body = "Hello, world!" })).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                string response = await page.EvaluateAsync<string>(
                    "() => fetch('/favicon.ico').then(r => r.text()).catch(e => 'load failed')").ConfigureAwait(false);
                Assert.That(response, Is.EqualTo("load failed"));
                await Task.Delay(1000).ConfigureAwait(false);
                Assert.That(requestCount, Is.EqualTo(0));
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

        private static async Task IgnoreAsync(Func<Task> body)
        {
            try
            {
                await body().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static async Task<Exception> CatchAsync(Func<Task> body)
        {
            try
            {
                await body().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
