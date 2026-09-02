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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-route.spec.ts</c> parity for <see cref="IPage.RouteAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android, BiDi-only):
    /// Android / Electron-only <c>it.skip</c> / <c>it.fixme</c> branches (no isolated
    /// context, no Electron browser-context management, Android CORS / OPTIONS / 1MB).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageRouteParityTests : PageTestEx
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

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19772;
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

        private static Task WriteTextAsync(HttpContext http, string body)
        {
            http.Response.ContentType = "text/plain";
            return http.Response.WriteAsync(body);
        }

        private static async Task AssertRouteMethodThrowsIfCalledTwiceAsync(Func<IRoute, Task> action)
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                TaskCompletionSource<IRoute> resolve = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await page.RouteAsync("**/*", route => resolve.TrySetResult(route)).ConfigureAwait(false);
                _ = page.GoToAsync(Prefix + "/empty.html").ContinueWith(_ => 0, TaskScheduler.Default);
                IRoute route = await resolve.Task.ConfigureAwait(false);
                await action(route).ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => action(route));
                Assert.That(error.Message, Does.Contain("Route is already handled!"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should intercept")]
        [PlaywrightTest("page-route.spec.ts", "should intercept @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIntercept()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                bool intercepted = false;
                await page.RouteAsync("**/empty.html", route =>
                {
                    IRequest request = route.Request;
                    Assert.That(request.Url, Does.Contain("empty.html"));
                    Assert.That(string.IsNullOrEmpty(request.GetHeaderValue("user-agent")), Is.False);
                    Assert.That(request.Method, Is.EqualTo("GET"));
                    Assert.That(request.PostData, Is.Null);
                    Assert.That(request.IsNavigationRequest, Is.True);
                    Assert.That(request.ResourceType, Is.EqualTo("document"));
                    Assert.That(request.Frame, Is.EqualTo(page.MainFrame));
                    Assert.That(request.Frame.Url, Is.EqualTo("about:blank"));
                    _ = route.ContinueAsync();
                    intercepted = true;
                }).ConfigureAwait(false);

                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
                Assert.That(intercepted, Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should unroute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUnroute()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<int> intercepted = new();
                await page.RouteAsync("**/*", route =>
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

                void Handler4(IRoute route)
                {
                    intercepted.Add(4);
                    _ = route.FallbackAsync();
                }

                await page.RouteAsync(new Regex("empty.html"), Handler4).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { 4, 3, 2, 1 }));

                intercepted.Clear();
                await page.UnrouteAsync(new Regex("empty.html"), Handler4).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));

                intercepted.Clear();
                await page.UnrouteAsync("**/empty.html").ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { 1 }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not support ? in glob pattern")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotSupportQuestionMarkInGlobPattern()
        {
            EnsureServer();
            Server.SetRoute("/index", http => http.Response.WriteAsync("index-no-hello"));
            Server.SetRoute("/index123hello", http => http.Response.WriteAsync("index123hello"));
            Server.SetRoute("/index?hello", http => http.Response.WriteAsync("index?hello"));
            Server.SetRoute("/index1hello", http => http.Response.WriteAsync("index1hello"));

            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/index?hello", route => route.FulfillAsync(new() { Body = "intercepted any character" })).ConfigureAwait(false);
                await page.RouteAsync("**/index\\?hello", route => route.FulfillAsync(new() { Body = "intercepted question mark" })).ConfigureAwait(false);

                await page.GoToAsync(Prefix + "/index?hello").ConfigureAwait(false);
                Assert.That(await page.ContentAsync().ConfigureAwait(false), Does.Contain("intercepted question mark"));

                await page.GoToAsync(Prefix + "/index").ConfigureAwait(false);
                Assert.That(await page.ContentAsync().ConfigureAwait(false), Does.Contain("index-no-hello"));

                await page.GoToAsync(Prefix + "/index1hello").ConfigureAwait(false);
                Assert.That(await page.ContentAsync().ConfigureAwait(false), Does.Contain("index1hello"));

                await page.GoToAsync(Prefix + "/index123hello").ConfigureAwait(false);
                Assert.That(await page.ContentAsync().ConfigureAwait(false), Does.Contain("index123hello"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work when POST is redirected with 302")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenPostIsRedirectedWith302()
        {
            EnsureServer();
            Server.SetRedirect("/rredirect", "/empty.html");
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                await page.SetContentAsync(@"
    <form action='/rredirect' method='post'>
      <input type=""hidden"" id=""foo"" name=""foo"" value=""FOOBAR"">
    </form>
  ").ConfigureAwait(false);
                await Task.WhenAll(
                    page.EvalOnSelectorAsync<object>("form", "form => form.submit()"),
                    page.WaitForNavigationAsync()).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work when header manipulation headers with redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenHeaderManipulationHeadersWithRedirect()
        {
            EnsureServer();
            Server.SetRedirect("/rrredirect", "/empty.html");
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["foo"] = "bar";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/rrredirect").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should be able to remove headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToRemoveHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers.Remove("foo");
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);

                Task<string> serverRequest = Server.WaitForRequest("/title.html", request => request.Headers["foo"].ToString());
                await Task.WhenAll(
                    serverRequest,
                    page.EvaluateAsync(
                        "url => fetch(url, { headers: { foo: 'bar' } })",
                        Prefix + "/title.html")).ConfigureAwait(false);
                Assert.That(string.IsNullOrEmpty(serverRequest.Result), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should contain referer header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldContainRefererHeader()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<IRequest> requests = new();
                await page.RouteAsync("**/*", route =>
                {
                    requests.Add(route.Request);
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(requests[1].Url, Does.Contain("/one-style.css"));
                Assert.That(requests[1].GetHeaderValue("referer"), Does.Contain("/one-style.html"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should properly return navigation response when URL has cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlyReturnNavigationResponseWhenURLHasCookies()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.Context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Url = EmptyPage,
                        Name = "foo",
                        Value = "bar",
                    },
                }).ConfigureAwait(false);

                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                IResponse response = await page.ReloadAsync().ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not override cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotOverrideCookieHeader()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.EvaluateAsync("() => { document.cookie = 'original=value'; }").ConfigureAwait(false);
                string cookieValueInRoute = null;
                await page.RouteAsync("**", async route =>
                {
                    Dictionary<string, string> headers = await route.Request.AllHeadersAsync().ConfigureAwait(false);
                    cookieValueInRoute = headers["cookie"];
                    headers["cookie"] = "overridden=value";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);

                Task<string> serverReq = Server.WaitForRequest("/empty.html", request => request.Headers["cookie"].ToString());
                await Task.WhenAll(serverReq, page.GoToAsync(EmptyPage)).ConfigureAwait(false);

                if (!TestConstants.IsWebKit)
                {
                    Assert.That(cookieValueInRoute, Is.EqualTo("original=value"));
                }

                Assert.That(serverReq.Result, Is.EqualTo("original=value"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should show custom HTTP headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldShowCustomHttpHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
                {
                    ["foo"] = "bar",
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    Assert.That(route.Request.GetHeaderValue("foo"), Is.EqualTo("bar"));
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work with redirect inside sync XHR")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithRedirectInsideSyncXhr()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("No Network.requestIntercepted for the request");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Server.SetRedirect("/logo.png", "/pptr.png");
                Task continuePromise = null;
                await page.RouteAsync("**/*", route =>
                {
                    continuePromise = route.ContinueAsync();
                }).ConfigureAwait(false);
                int status = await page.EvaluateAsync<int>(@"(() => {
    const request = new XMLHttpRequest();
    request.open('GET', '/logo.png', false);
    request.send(null);
    return request.status;
})()").ConfigureAwait(false);
                Assert.That(status, Is.EqualTo(200));
                Assert.That(continuePromise, Is.Not.Null);
                await continuePromise.ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should pause intercepted XHR until continue")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPauseInterceptedXhrUntilContinue()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Redirected request is not paused in WebKit");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                TaskCompletionSource<IRoute> routeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await page.RouteAsync("**/global-var.html", route => routeTcs.TrySetResult(route)).ConfigureAwait(false);
                bool xhrFinished = false;
                Task<int> statusPromise = page.EvaluateAsync<int>(@"(() => {
    const request = new XMLHttpRequest();
    request.open('GET', '/global-var.html', false);
    request.send(null);
    return request.status;
})()").ContinueWith(
                    t =>
                    {
                        xhrFinished = true;
                        return t.GetAwaiter().GetResult();
                    },
                    TaskScheduler.Default);
                IRoute route = await routeTcs.Task.ConfigureAwait(false);
                await Task.Delay(500).ConfigureAwait(false);
                Assert.That(xhrFinished, Is.False);
                Task continueTask = route.ContinueAsync();
                int status = await statusPromise.ConfigureAwait(false);
                await continueTask.ConfigureAwait(false);
                Assert.That(status, Is.EqualTo(200));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should pause intercepted fetch request until continue")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPauseInterceptedFetchRequestUntilContinue()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                TaskCompletionSource<IRoute> routeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await page.RouteAsync("**/global-var.html", route => routeTcs.TrySetResult(route)).ConfigureAwait(false);
                bool fetchFinished = false;
                Task<int> statusPromise = page.EvaluateAsync<int>(@"async () => {
    const response = await fetch('/global-var.html');
    return response.status;
}").ContinueWith(
                    t =>
                    {
                        fetchFinished = true;
                        return t.GetAwaiter().GetResult();
                    },
                    TaskScheduler.Default);
                IRoute route = await routeTcs.Task.ConfigureAwait(false);
                await Task.Delay(500).ConfigureAwait(false);
                Assert.That(fetchFinished, Is.False);
                Task continueTask = route.ContinueAsync();
                int status = await statusPromise.ConfigureAwait(false);
                await continueTask.ConfigureAwait(false);
                Assert.That(status, Is.EqualTo(200));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work with custom referer headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCustomRefererHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
                {
                    ["referer"] = EmptyPage,
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    if (TestConstants.IsChromium)
                    {
                        Assert.That(route.Request.GetHeaderValue("referer"), Is.EqualTo(EmptyPage + ", " + EmptyPage));
                    }
                    else
                    {
                        Assert.That(route.Request.GetHeaderValue("referer"), Is.EqualTo(EmptyPage));
                    }

                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should be abortable")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbortable()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync(new Regex("\\.css$"), route => route.AbortAsync()).ConfigureAwait(false);
                bool failed = false;
                page.RequestFailed += (_, request) =>
                {
                    if (request.Url.Contains(".css", StringComparison.Ordinal))
                    {
                        failed = true;
                    }
                };
                IResponse response = await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
                Assert.That(response.Request.Failure, Is.Null);
                Assert.That(failed, Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should be abortable with custom error codes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbortableWithCustomErrorCodes()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route => route.AbortAsync("internetdisconnected")).ConfigureAwait(false);
                IRequest failedRequest = null;
                page.RequestFailed += (_, request) => failedRequest = request;
                _ = page.GoToAsync(EmptyPage).ContinueWith(_ => 0, TaskScheduler.Default);
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                while (failedRequest == null && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }

                Assert.That(failedRequest, Is.Not.Null);
                if (TestConstants.IsWebKit)
                {
                    Assert.That(failedRequest.Failure, Is.EqualTo("Blocked by Web Inspector"));
                }
                else
                {
                    Assert.That(failedRequest.Failure, Is.EqualTo("net::ERR_INTERNET_DISCONNECTED"));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not throw if request was cancelled by the page")]
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
                Assert.That(cancelledRequest.Failure, Does.Match("cancelled|aborted").IgnoreCase);
                await route.AbortAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should send referer")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendReferer()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
                {
                    ["referer"] = "http://google.com/",
                }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                Task<string> request = Server.WaitForRequest("/grid.html", r => r.Headers["referer"].ToString());
                await Task.WhenAll(request, page.GoToAsync(Prefix + "/grid.html")).ConfigureAwait(false);
                Assert.That(request.Result, Is.EqualTo("http://google.com/"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should fail navigation when aborting main resource")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailNavigationWhenAbortingMainResource()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route => route.AbortAsync()).ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => page.GoToAsync(EmptyPage));
                Assert.That(error, Is.Not.Null);
                if (TestConstants.IsWebKit)
                {
                    Assert.That(error.Message, Does.Contain("Blocked by Web Inspector"));
                }
                else
                {
                    Assert.That(error.Message, Does.Contain("net::ERR_FAILED"));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not work with redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotWorkWithRedirects()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<IRequest> intercepted = new();
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.ContinueAsync();
                    intercepted.Add(route.Request);
                }).ConfigureAwait(false);
                Server.SetRedirect("/non-existing-page.html", "/non-existing-page-2.html");
                Server.SetRedirect("/non-existing-page-2.html", "/non-existing-page-3.html");
                Server.SetRedirect("/non-existing-page-3.html", "/non-existing-page-4.html");
                Server.SetRedirect("/non-existing-page-4.html", "/empty.html");

                IResponse response = await page.GoToAsync(Prefix + "/non-existing-page.html").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(response.Url, Does.Contain("empty.html"));

                Assert.That(intercepted, Has.Count.EqualTo(1));
                Assert.That(intercepted[0].ResourceType, Is.EqualTo("document"));
                Assert.That(intercepted[0].IsNavigationRequest, Is.True);
                Assert.That(intercepted[0].Url, Does.Contain("/non-existing-page.html"));

                List<IRequest> chain = new();
                for (IRequest current = response.Request; current != null; current = current.RedirectedFrom)
                {
                    chain.Add(current);
                    Assert.That(current.IsNavigationRequest, Is.True);
                }

                Assert.That(chain, Has.Count.EqualTo(5));
                Assert.That(chain[0].Url, Does.Contain("/empty.html"));
                Assert.That(chain[1].Url, Does.Contain("/non-existing-page-4.html"));
                Assert.That(chain[2].Url, Does.Contain("/non-existing-page-3.html"));
                Assert.That(chain[3].Url, Does.Contain("/non-existing-page-2.html"));
                Assert.That(chain[4].Url, Does.Contain("/non-existing-page.html"));
                for (int i = 0; i < chain.Count; i++)
                {
                    Assert.That(chain[i].RedirectedTo, Is.EqualTo(i > 0 ? chain[i - 1] : null));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should chain fallback w/ dynamic URL")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChainFallbackWithDynamicUrl()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<int> intercepted = new();
                await page.RouteAsync("**/bar", route =>
                {
                    intercepted.Add(1);
                    _ = route.FallbackAsync(new() { Url = EmptyPage });
                }).ConfigureAwait(false);
                await page.RouteAsync("**/foo", route =>
                {
                    intercepted.Add(2);
                    _ = route.FallbackAsync(new() { Url = "http://localhost/bar" });
                }).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route =>
                {
                    intercepted.Add(3);
                    _ = route.FallbackAsync(new() { Url = "http://localhost/foo" });
                }).ConfigureAwait(false);

                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work with redirects for subresources")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithRedirectsForSubresources()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<IRequest> intercepted = new();
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.ContinueAsync();
                    intercepted.Add(route.Request);
                }).ConfigureAwait(false);
                Server.SetRedirect("/one-style.css", "/two-style.css");
                Server.SetRedirect("/two-style.css", "/three-style.css");
                Server.SetRedirect("/three-style.css", "/four-style.css");
                Server.SetRoute("/four-style.css", http => http.Response.WriteAsync("body {box-sizing: border-box; }"));

                IResponse response = await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(response.Url, Does.Contain("one-style.html"));

                Assert.That(intercepted, Has.Count.EqualTo(2));
                Assert.That(intercepted[0].ResourceType, Is.EqualTo("document"));
                Assert.That(intercepted[0].Url, Does.Contain("one-style.html"));

                IRequest current = intercepted[1];
                foreach (string url in new[] { "/one-style.css", "/two-style.css", "/three-style.css", "/four-style.css" })
                {
                    Assert.That(current.ResourceType, Is.EqualTo("stylesheet"));
                    Assert.That(current.Url, Does.Contain(url));
                    current = current.RedirectedTo;
                }

                Assert.That(current, Is.Null);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work with equal requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithEqualRequests()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                int responseCount = 1;
                Server.SetRoute("/zzz", http => http.Response.WriteAsync((responseCount++ * 11).ToString(CultureInfo.InvariantCulture)));

                bool spinner = false;
                await page.RouteAsync("**/*", route =>
                {
                    _ = spinner ? route.AbortAsync() : route.ContinueAsync();
                    spinner = !spinner;
                }).ConfigureAwait(false);

                List<string> results = new();
                for (int i = 0; i < 3; i++)
                {
                    results.Add(await page.EvaluateAsync<string>("() => fetch('/zzz').then(response => response.text()).catch(() => 'FAILED')").ConfigureAwait(false));
                }

                Assert.That(results, Is.EqualTo(new[] { "11", "FAILED", "22" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should navigate to dataURL and not fire dataURL requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNavigateToDataUrlAndNotFireDataUrlRequests()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<IRequest> requests = new();
                await page.RouteAsync("**/*", route =>
                {
                    requests.Add(route.Request);
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                const string dataUrl = "data:text/html,<div>yo</div>";
                IResponse response = await page.GoToAsync(dataUrl).ConfigureAwait(false);
                Assert.That(response, Is.Null);
                Assert.That(requests, Is.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should be able to fetch dataURL and not fire dataURL requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToFetchDataUrlAndNotFireDataUrlRequests()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                List<IRequest> requests = new();
                await page.RouteAsync("**/*", route =>
                {
                    requests.Add(route.Request);
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                const string dataUrl = "data:text/html,<div>yo</div>";
                string text = await page.EvaluateAsync<string>("url => fetch(url).then(r => r.text())", dataUrl).ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("<div>yo</div>"));
                Assert.That(requests, Is.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should navigate to URL with hash and and fire requests without hash")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNavigateToUrlWithHashAndAndFireRequestsWithoutHash()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<IRequest> requests = new();
                await page.RouteAsync("**/*", route =>
                {
                    requests.Add(route.Request);
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(EmptyPage + "#hash").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(response.Url, Is.EqualTo(EmptyPage));
                Assert.That(requests, Has.Count.EqualTo(1));
                Assert.That(requests[0].Url, Is.EqualTo(EmptyPage));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work with encoded server")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithEncodedServer()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/some nonexisting page").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(404));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work with badly encoded server")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBadlyEncodedServer()
        {
            EnsureServer();
            Server.SetRoute("/malformed?rnd=%911", _ => Task.CompletedTask);
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/malformed?rnd=%911").ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work with encoded server - 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithEncodedServer2()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                List<IRequest> requests = new();
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.ContinueAsync();
                    requests.Add(route.Request);
                }).ConfigureAwait(false);
                await page.SetContentAsync($"<link rel=\"stylesheet\" href=\"{Prefix}/fonts?helvetica|arial\"/>").ConfigureAwait(false);
                Assert.That(requests, Has.Count.EqualTo(1));
                IResponse response = await requests[0].ResponseAsync().ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(404));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not throw \"Invalid Interception Id\" if the request was cancelled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowInvalidInterceptionIdIfTheRequestWasCancelled()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<iframe></iframe>").ConfigureAwait(false);
                IRoute route = null;
                await page.RouteAsync("**/*", r => route = r).ConfigureAwait(false);
                _ = page.EvalOnSelectorAsync<object>("iframe", "(frame, url) => { frame.src = url; }", EmptyPage);
                await page.WaitForEventAsync(PageEvent.Request).ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>("iframe", "frame => frame.remove()").ConfigureAwait(false);
                Exception error = null;
                try
                {
                    await route.ContinueAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                Assert.That(error, Is.Null);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should intercept main resource during cross-process navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptMainResourceDuringCrossProcessNavigation()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                bool intercepted = false;
                await page.RouteAsync(CrossProcessPrefix + "/empty.html", route =>
                {
                    intercepted = true;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
                Assert.That(intercepted, Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should fulfill with redirect status")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillWithRedirectStatus()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("in WebKit the redirects are handled by the network stack and we intercept before");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/title.html").ConfigureAwait(false);
                Server.SetRoute("/final", http => http.Response.WriteAsync("foo"));
                await page.RouteAsync("**/*", async route =>
                {
                    if (route.Request.Url != Prefix + "/redirect_this")
                    {
                        await route.ContinueAsync().ConfigureAwait(false);
                        return;
                    }

                    await route.FulfillAsync(301, headers: new Dictionary<string, string> { ["location"] = "/final" }).ConfigureAwait(false);
                }).ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url);
    return data.text();
}",
                    Prefix + "/redirect_this").ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("foo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not fulfill with redirect status")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFulfillWithRedirectStatus()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("we should support fulfill with redirect in webkit and delete this test");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                int status = 0;
                TaskCompletionSource<Exception> fulfill = null;
                await page.RouteAsync("**/*", async route =>
                {
                    if (route.Request.Url != Prefix + "/redirect_this")
                    {
                        await route.ContinueAsync().ConfigureAwait(false);
                        return;
                    }

                    try
                    {
                        await route.FulfillAsync(
                            status: status,
                            headers: new Dictionary<string, string> { ["location"] = "/empty.html" }).ConfigureAwait(false);
                        fulfill.TrySetException(new InvalidOperationException("fulfill didn't throw"));
                    }
                    catch (Exception ex)
                    {
                        fulfill.TrySetResult(ex);
                    }
                }).ConfigureAwait(false);

                for (status = 300; status < 310; status++)
                {
                    fulfill = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
                    Task navigation = page.EvaluateAsync("url => { location.href = url; }", Prefix + "/redirect_this");
                    Exception exception = await fulfill.Task.ConfigureAwait(false);
                    Assert.That(exception, Is.Not.Null);
                    Assert.That(exception.Message, Does.Contain("Cannot fulfill with redirect status"));
                    _ = navigation.ContinueWith(_ => 0, TaskScheduler.Default);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should support cors with GET")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCorsWithGet()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/cars*", async route =>
                {
                    Dictionary<string, string> headers = new()
                    {
                        ["access-control-allow-origin"] = route.Request.Url.EndsWith("allow", StringComparison.Ordinal) ? "*" : "none",
                    };
                    await route.FulfillAsync(new() { ContentType = "application/json", Headers = headers, Status = 200, Body = "[\"electric\", \"gas\"]" }).ConfigureAwait(false);
                }).ConfigureAwait(false);

                string[] resp = await page.EvaluateAsync<string[]>(@"async () => {
    const response = await fetch('https://example.com/cars?allow', { mode: 'cors' });
    return response.json();
}").ConfigureAwait(false);
                Assert.That(resp, Is.EqualTo(new[] { "electric", "gas" }));

                Exception error = Assert.CatchAsync(() => page.EvaluateAsync<string[]>(@"async () => {
    const response = await fetch('https://example.com/cars?reject', { mode: 'cors' });
    return response.json();
}"));
                if (TestConstants.IsChromium)
                {
                    Assert.That(error.Message, Does.Contain("Failed"));
                }

                if (TestConstants.IsWebKit)
                {
                    Assert.That(error.Message, Does.Contain("TypeError"));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should add Access-Control-Allow-Origin by default when fulfill")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddAccessControlAllowOriginByDefaultWhenFulfill()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/cars", async route =>
                {
                    await route.FulfillAsync(new() { ContentType = "application/json", Status = 200, Body = "[\"electric\", \"gas\"]" }).ConfigureAwait(false);
                }).ConfigureAwait(false);

                Task<IResponse> responseTask = page.WaitForResponseAsync("https://example.com/cars");
                Task<string[]> resultTask = page.EvaluateAsync<string[]>(@"async () => {
    const response = await fetch('https://example.com/cars', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        mode: 'cors',
        body: JSON.stringify({ 'number': 1 })
    });
    return response.json();
}");
                await Task.WhenAll(resultTask, responseTask).ConfigureAwait(false);
                Assert.That(resultTask.Result, Is.EqualTo(new[] { "electric", "gas" }));
                Assert.That(await responseTask.Result.HeaderValueAsync("Access-Control-Allow-Origin").ConfigureAwait(false), Is.EqualTo(Prefix));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should allow null origin for about:blank")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowNullOriginForAboutBlank()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/something", async route =>
                {
                    await route.FulfillAsync(new() { ContentType = "text/plain", Status = 200, Body = "done" }).ConfigureAwait(false);
                }).ConfigureAwait(false);

                Task<IResponse> responseTask = page.WaitForResponseAsync(CrossProcessPrefix + "/something");
                Task<string> textTask = page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, {
        method: 'GET',
        headers: { 'X-PINGOTHER': 'pingpong' }
    });
    return data.text();
}",
                    CrossProcessPrefix + "/something");
                await Task.WhenAll(responseTask, textTask).ConfigureAwait(false);
                Assert.That(textTask.Result, Is.EqualTo("done"));
                Assert.That(await responseTask.Result.HeaderValueAsync("Access-Control-Allow-Origin").ConfigureAwait(false), Is.EqualTo("null"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should respect cors overrides")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectCorsOverrides()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Server.SetRoute("/something", async http =>
                {
                    if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        http.Response.StatusCode = 204;
                        http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                        http.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS, DELETE";
                        http.Response.Headers["Access-Control-Allow-Headers"] = "*";
                        http.Response.Headers["Cache-Control"] = "no-cache";
                        return;
                    }

                    http.Response.StatusCode = 404;
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    await http.Response.WriteAsync("NOT FOUND").ConfigureAwait(false);
                });

                await page.RouteAsync("**/something", async route =>
                {
                    await route.FulfillAsync(
                        contentType: "text/plain",
                        status: 200,
                        headers: new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "http://non-existent" },
                        body: "done").ConfigureAwait(false);
                }).ConfigureAwait(false);

                Exception error = Assert.CatchAsync(() => page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, {
        method: 'GET',
        headers: { 'X-PINGOTHER': 'pingpong' }
    });
    return data.text();
}",
                    CrossProcessPrefix + "/something"));
                if (TestConstants.IsChromium)
                {
                    Assert.That(error.Message, Does.Contain("Failed to fetch"));
                }
                else if (TestConstants.IsWebKit)
                {
                    Assert.That(error.Message, Does.Contain("Load failed"));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not auto-intercept non-preflight OPTIONS without network interception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAutoInterceptNonPreflightOptionsWithoutNetworkInterception()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                List<string> requests = new();
                Server.SetRoute("/something", async http =>
                {
                    requests.Add(http.Request.Method + ":" + http.Request.Path);
                    if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        http.Response.StatusCode = 200;
                        http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                        http.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS, DELETE";
                        http.Response.Headers["Access-Control-Allow-Headers"] = "*";
                        http.Response.Headers["Cache-Control"] = "no-cache";
                        await http.Response.WriteAsync("Hello").ConfigureAwait(false);
                        return;
                    }

                    http.Response.StatusCode = 200;
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    await http.Response.WriteAsync("World").ConfigureAwait(false);
                });

                string[] texts = await page.EvaluateAsync<string[]>(
                    @"async url => {
    const response1 = await fetch(url, { method: 'OPTIONS' });
    const text1 = await response1.text();
    const response2 = await fetch(url, { method: 'GET' });
    const text2 = await response2.text();
    return [text1, text2];
}",
                    CrossProcessPrefix + "/something").ConfigureAwait(false);
                Assert.That(texts[0], Is.EqualTo("Hello"));
                Assert.That(texts[1], Is.EqualTo("World"));
                Assert.That(requests, Is.EqualTo(new[] { "OPTIONS:/something", "OPTIONS:/something", "GET:/something" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should not auto-intercept non-preflight OPTIONS with network interception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAutoInterceptNonPreflightOptionsWithNetworkInterception()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                List<string> requests = new();
                Server.SetRoute("/something", async http =>
                {
                    requests.Add(http.Request.Method + ":" + http.Request.Path);
                    if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        http.Response.StatusCode = 200;
                        http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                        http.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS, DELETE";
                        http.Response.Headers["Access-Control-Allow-Headers"] = "*";
                        http.Response.Headers["Cache-Control"] = "no-cache";
                        await http.Response.WriteAsync("Hello").ConfigureAwait(false);
                        return;
                    }

                    http.Response.StatusCode = 200;
                    http.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    await http.Response.WriteAsync("World").ConfigureAwait(false);
                });

                await page.RouteAsync("**/something", route => route.ContinueAsync()).ConfigureAwait(false);
                string[] texts = await page.EvaluateAsync<string[]>(
                    @"async url => {
    const response1 = await fetch(url, { method: 'OPTIONS' });
    const text1 = await response1.text();
    const response2 = await fetch(url, { method: 'GET' });
    const text2 = await response2.text();
    return [text1, text2];
}",
                    CrossProcessPrefix + "/something").ConfigureAwait(false);
                Assert.That(texts[0], Is.EqualTo("Hello"));
                Assert.That(texts[1], Is.EqualTo("World"));
                if (TestConstants.IsChromium)
                {
                    Assert.That(requests, Is.EqualTo(new[] { "OPTIONS:/something", "GET:/something" }));
                }
                else
                {
                    Assert.That(requests, Is.EqualTo(new[] { "OPTIONS:/something", "OPTIONS:/something", "GET:/something" }));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should support cors with POST")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCorsWithPost()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/cars", async route =>
                {
                    await route.FulfillAsync(
                        contentType: "application/json",
                        headers: new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                        status: 200,
                        body: "[\"electric\", \"gas\"]").ConfigureAwait(false);
                }).ConfigureAwait(false);
                string[] resp = await page.EvaluateAsync<string[]>(@"async () => {
    const response = await fetch('https://example.com/cars', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        mode: 'cors',
        body: JSON.stringify({ 'number': 1 })
    });
    return response.json();
}").ConfigureAwait(false);
                Assert.That(resp, Is.EqualTo(new[] { "electric", "gas" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should support cors with credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCorsWithCredentials()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/cars", async route =>
                {
                    await route.FulfillAsync(
                        contentType: "application/json",
                        headers: new Dictionary<string, string>
                        {
                            ["Access-Control-Allow-Origin"] = Prefix,
                            ["Access-Control-Allow-Credentials"] = "true",
                        },
                        status: 200,
                        body: "[\"electric\", \"gas\"]").ConfigureAwait(false);
                }).ConfigureAwait(false);
                string[] resp = await page.EvaluateAsync<string[]>(@"async () => {
    const response = await fetch('https://example.com/cars', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        mode: 'cors',
        body: JSON.stringify({ 'number': 1 }),
        credentials: 'include'
    });
    return response.json();
}").ConfigureAwait(false);
                Assert.That(resp, Is.EqualTo(new[] { "electric", "gas" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should reject cors with disallowed credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectCorsWithDisallowedCredentials()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/cars", async route =>
                {
                    await route.FulfillAsync(
                        contentType: "application/json",
                        headers: new Dictionary<string, string>
                        {
                            ["Access-Control-Allow-Origin"] = Prefix,
                        },
                        status: 200,
                        body: "[\"electric\", \"gas\"]").ConfigureAwait(false);
                }).ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => page.EvaluateAsync(@"async () => {
    const response = await fetch('https://example.com/cars', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        mode: 'cors',
        body: JSON.stringify({ 'number': 1 }),
        credentials: 'include'
    });
    return response.json();
}"));
                Assert.That(error, Is.Not.Null);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should support cors for different methods")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCorsForDifferentMethods()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/cars", async route =>
                {
                    await route.FulfillAsync(
                        contentType: "application/json",
                        headers: new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                        status: 200,
                        body: "[\"" + route.Request.Method + "\", \"electric\", \"gas\"]").ConfigureAwait(false);
                }).ConfigureAwait(false);

                string[] post = await page.EvaluateAsync<string[]>(@"async () => {
    const response = await fetch('https://example.com/cars', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        mode: 'cors',
        body: JSON.stringify({ 'number': 1 })
    });
    return response.json();
}").ConfigureAwait(false);
                Assert.That(post, Is.EqualTo(new[] { "POST", "electric", "gas" }));

                string[] delete = await page.EvaluateAsync<string[]>(@"async () => {
    const response = await fetch('https://example.com/cars', {
        method: 'DELETE',
        headers: {},
        mode: 'cors',
        body: ''
    });
    return response.json();
}").ConfigureAwait(false);
                Assert.That(delete, Is.EqualTo(new[] { "DELETE", "electric", "gas" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should support the times parameter with route matching")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTheTimesParameterWithRouteMatching()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<int> intercepted = new();
                await page.RouteAsync(
                    "**/empty.html",
                    route =>
                    {
                        intercepted.Add(1);
                        _ = route.ContinueAsync();
                    },
                    times: 1).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Has.Count.EqualTo(1));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should work if handler with times parameter was removed from another handler")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkIfHandlerWithTimesParameterWasRemovedFromAnotherHandler()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                List<string> intercepted = new();
                void Handler(IRoute route)
                {
                    intercepted.Add("first");
                    _ = route.ContinueAsync();
                }

                await page.RouteAsync("**/*", Handler, times: 1).ConfigureAwait(false);
                await page.RouteAsync("**/*", async route =>
                {
                    intercepted.Add("second");
                    await page.UnrouteAsync("**/*", Handler).ConfigureAwait(false);
                    await route.FallbackAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { "second" }));
                intercepted.Clear();
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { "second" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should support async handler w/ times")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportAsyncHandlerWithTimes()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync(
                    "**/empty.html",
                    async route =>
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                        await route.FulfillAsync(new() { Body = "<html>intercepted</html>", ContentType = "text/html" }).ConfigureAwait(false);
                    },
                    times: 1).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await Assertions.Expect(page.Locator("body")).ToHaveTextAsync("intercepted").ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await Assertions.Expect(page.Locator("body")).Not.ToHaveTextAsync("intercepted").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "route abort with times: 1 should not affect second sequential fetch")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RouteAbortWithTimes1ShouldNotAffectSecondSequentialFetch()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Chromium drops a request that is intercepted while Fetch.disable is being processed; fix is not rolled yet");
            }

            EnsureServer();
            Server.SetRoute("/data", http => WriteTextAsync(http, "ok"));
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/data", route => route.AbortAsync("timedout"), times: 1).ConfigureAwait(false);
                string[] results = await page.EvaluateAsync<string[]>(@"async () => {
    async function fetchOrHung(url) {
        const timeout = new Promise(resolve => setTimeout(() => resolve('hung'), 3000));
        const request = fetch(url).then(r => String(r.status)).catch(() => 'aborted');
        return Promise.race([request, timeout]);
    }
    const first = await fetchOrHung('/data');
    const second = await fetchOrHung('/data');
    return [first, second];
}").ConfigureAwait(false);
                Assert.That(results, Is.EqualTo(new[] { "aborted", "200" }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should contain raw request header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldContainRawRequestHeader()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Dictionary<string, string> headers = null;
                await page.RouteAsync("**/*", async route =>
                {
                    headers = await route.Request.AllHeadersAsync().ConfigureAwait(false);
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                Assert.That(headers["accept"], Is.Not.Null.And.Not.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should contain raw response header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldContainRawResponseHeader()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                IRequest request = null;
                await page.RouteAsync("**/*", route =>
                {
                    request = route.Request;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                IResponse response = await request.ResponseAsync().ConfigureAwait(false);
                Dictionary<string, string> headers = await response.AllHeadersAsync().ConfigureAwait(false);
                Assert.That(headers["content-type"], Is.Not.Null.And.Not.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should contain raw response header after fulfill")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldContainRawResponseHeaderAfterFulfill()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                IRequest request = null;
                await page.RouteAsync("**/*", async route =>
                {
                    request = route.Request;
                    await route.FulfillAsync(new() { Status = 200, Body = "Hello", ContentType = "text/html" }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                IResponse response = await request.ResponseAsync().ConfigureAwait(false);
                Dictionary<string, string> headers = await response.AllHeadersAsync().ConfigureAwait(false);
                Assert.That(headers["content-type"], Is.Not.Null.And.Not.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "route.fulfill should throw if called twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task RouteFulfillShouldThrowIfCalledTwice()
            => AssertRouteMethodThrowsIfCalledTwiceAsync(route => route.FulfillAsync());

        [PlaywrightTest("page-route.spec.ts", "route.continue should throw if called twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task RouteContinueShouldThrowIfCalledTwice()
            => AssertRouteMethodThrowsIfCalledTwiceAsync(route => route.ContinueAsync());

        [PlaywrightTest("page-route.spec.ts", "route.fallback should throw if called twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task RouteFallbackShouldThrowIfCalledTwice()
            => AssertRouteMethodThrowsIfCalledTwiceAsync(route => route.FallbackAsync());

        [PlaywrightTest("page-route.spec.ts", "route.abort should throw if called twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task RouteAbortShouldThrowIfCalledTwice()
            => AssertRouteMethodThrowsIfCalledTwiceAsync(route => route.AbortAsync());

        [PlaywrightTest("page-route.spec.ts", "should intercept when postData is more than 1MB")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptWhenPostDataIsMoreThan1Mb()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                TaskCompletionSource<string> interception = new(TaskCreationOptions.RunContinuationsAsynchronously);
                string large = new string('0', 2 * 1024 * 1024);
                await page.RouteAsync("**/404.html", async route =>
                {
                    await route.AbortAsync().ConfigureAwait(false);
                    interception.TrySetResult(route.Request.PostData);
                }).ConfigureAwait(false);
                await page.EvaluateAsync("POST_BODY => fetch('/404.html', { method: 'POST', body: POST_BODY }).catch(() => {})", large).ConfigureAwait(false);
                Assert.That(await interception.Task.ConfigureAwait(false), Is.EqualTo(large));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-route.spec.ts", "should be able to intercept every navigation to a page controlled by service worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToInterceptEveryNavigationToAPageControlledByServiceWorker()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                int interceptions = 0;
                string url = Prefix + "/serviceworkers/bug-33561/index.html";
                await page.RouteAsync(url, async route =>
                {
                    interceptions++;
                    await route.ContinueAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

                await page.GoToAsync(url).ConfigureAwait(false);
                await page.EvaluateAsync<object>("(() => window['activationPromise'])()").ConfigureAwait(false);
                await page.GoToAsync(url).ConfigureAwait(false);
                Assert.That(interceptions, Is.EqualTo(2));
            }).ConfigureAwait(false);
        }
    }
}

