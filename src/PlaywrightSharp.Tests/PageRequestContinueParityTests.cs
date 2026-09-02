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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-request-continue.spec.ts</c> parity for <see cref="IRoute.ContinueAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android, BiDi-only):
    /// Android / Electron-only <c>it.skip</c> / <c>it.fixme</c> branches.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageRequestContinueParityTests : PageTestEx
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
            int basePort = 19773;
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

        [PlaywrightTest("page-request-continue.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend HTTP headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendHttpHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**/*", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["FOO"] = "bar";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<string> request = Server.WaitForRequest("/sleep.zzz", r => (string)r.Headers["foo"]);
                await Task.WhenAll(request, page.EvaluateAsync("() => fetch('/sleep.zzz')")).ConfigureAwait(false);
                Assert.That(request.Result, Is.EqualTo("bar"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should not allow to override unsafe HTTP headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowToOverrideUnsafeHttpHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                TaskCompletionSource<IRoute> routeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await page.RouteAsync("**/*", route => routeTcs.TrySetResult(route)).ConfigureAwait(false);
                Task<Dictionary<string, string>> serverRequest = Server.WaitForRequest("/empty.html", SnapshotHeaders);
                _ = page.GoToAsync(EmptyPage).ContinueWith(_ => 0, TaskScheduler.Default);
                IRoute route = await routeTcs.Task.ConfigureAwait(false);
                Dictionary<string, string> headers = HeadersOf(route.Request);
                headers["host"] = "bar";
                headers["trailer"] = "baz";
                await route.ContinueAsync(new() { Headers = headers }).ConfigureAwait(false);
                Dictionary<string, string> received = await serverRequest.ConfigureAwait(false);
                Assert.That(received.TryGetValue("trailer", out string trailer) ? trailer : null, Is.Null.Or.Empty);
                Assert.That(received["host"], Is.EqualTo(new Uri(EmptyPage).Authority));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should delete header with undefined value")]
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
                await page.RouteAsync(Prefix + "/something", async route =>
                {
                    interceptedRequest = route.Request;
                    Dictionary<string, string> headers = await route.Request.AllHeadersAsync().ConfigureAwait(false);
                    headers.Remove("foo");
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);

                Task<string> text = page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, { headers: { foo: 'a', bar: 'b' } });
    return data.text();
}",
                    Prefix + "/something");
                Task<Dictionary<string, string>> serverRequest = Server.WaitForRequest("/something", SnapshotHeaders);
                await Task.WhenAll(text, serverRequest).ConfigureAwait(false);
                Assert.That(text.Result, Is.EqualTo("done"));
                Assert.That(interceptedRequest.GetHeaderValue("foo"), Is.Null);
                Assert.That(serverRequest.Result.TryGetValue("foo", out string foo) ? foo : null, Is.Null.Or.Empty);
                Assert.That(serverRequest.Result["bar"], Is.EqualTo("b"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend method")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendMethod()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> sRequest = Server.WaitForRequest("/sleep.zzz", r => r.Method);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync(new() { Method = "POST" })).ConfigureAwait(false);
                await Task.WhenAll(sRequest, page.EvaluateAsync("() => fetch('/sleep.zzz')")).ConfigureAwait(false);
                Assert.That(sRequest.Result, Is.EqualTo("POST"));
                Assert.That(await sRequest.ConfigureAwait(false), Is.EqualTo("POST"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should override request url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverrideRequestUrl()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> serverRequest = Server.WaitForRequest("/global-var.html", r => r.Method);
                await page.RouteAsync("**/foo", route =>
                {
                    _ = route.ContinueAsync(new() { Url = Prefix + "/global-var.html" });
                }).ConfigureAwait(false);
                IResponse response = await page.GoToAsync(Prefix + "/foo").ConfigureAwait(false);
                Assert.That(response.Request.Url, Is.EqualTo(Prefix + "/global-var.html"));
                Assert.That(response.Url, Is.EqualTo(Prefix + "/global-var.html"));
                Assert.That(await page.EvaluateAsync<int>("() => window['globalVar']").ConfigureAwait(false), Is.EqualTo(123));
                Assert.That(await serverRequest.ConfigureAwait(false), Is.EqualTo("GET"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should not allow changing protocol when overriding url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowChangingProtocolWhenOverridingUrl()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                TaskCompletionSource<Exception> errorTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await page.RouteAsync("**/*", async route =>
                {
                    try
                    {
                        await route.ContinueAsync(new() { Url = "file:///tmp/foo" }).ConfigureAwait(false);
                        errorTcs.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        errorTcs.TrySetResult(ex);
                    }
                }).ConfigureAwait(false);
                _ = page.GoToAsync(EmptyPage).ContinueWith(_ => 0, TaskScheduler.Default);
                Exception error = await errorTcs.Task.ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("New URL must have same protocol as overridden URL"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should not throw if request was cancelled by the page")]
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
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should override method along with url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverrideMethodAlongWithUrl()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> request = Server.WaitForRequest("/empty.html", r => r.Method);
                await page.RouteAsync("**/foo", route =>
                {
                    _ = route.ContinueAsync(new() { Url = EmptyPage, Method = "POST" });
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/foo").ConfigureAwait(false);
                Assert.That(await request.ConfigureAwait(false), Is.EqualTo("POST"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend method on main request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendMethodOnMainRequest()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Task<string> request = Server.WaitForRequest("/empty.html", r => r.Method);
                await page.RouteAsync("**/*", route => route.ContinueAsync(new() { Method = "POST" })).ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await request.ConfigureAwait(false), Is.EqualTo("POST"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes("doggo") })).ConfigureAwait(false);
                Task<string> serverRequest = WaitForBodyAsync("/sleep.zzz");
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'POST', body: 'birdy' })")).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("doggo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should compute content-length from post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldComputeContentLengthFromPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                string data = new string('a', 7500);
                await page.RouteAsync("**/*", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["content-type"] = "application/json";
                    _ = route.ContinueAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes(data), Headers = headers });
                }).ConfigureAwait(false);
                Task<(string Body, string Length, string Type)> serverRequest = Server.WaitForRequest(
                    "/sleep.zzz",
                    r =>
                    {
                        using StreamReader reader = new(r.Body);
                        return (reader.ReadToEnd(), (string)r.Headers["content-length"], (string)r.Headers["content-type"]);
                    });
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'PATCH', body: 'birdy' })")).ConfigureAwait(false);
                Assert.That(serverRequest.Result.Body, Is.EqualTo(data));
                Assert.That(serverRequest.Result.Length, Is.EqualTo(data.Length.ToString(CultureInfo.InvariantCulture)));
                Assert.That(serverRequest.Result.Type, Is.EqualTo("application/json"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend method and post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendMethodAndPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync(new() { Method = "POST", PostData = System.Text.Encoding.UTF8.GetBytes("doggo") })).ConfigureAwait(false);
                Task<(string Method, string Body)> serverRequest = Server.WaitForRequest(
                    "/sleep.zzz",
                    r =>
                    {
                        using StreamReader reader = new(r.Body);
                        return (r.Method, reader.ReadToEnd());
                    });
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'GET' })")).ConfigureAwait(false);
                Assert.That(serverRequest.Result.Method, Is.EqualTo("POST"));
                Assert.That(serverRequest.Result.Body, Is.EqualTo("doggo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend utf8 post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendUtf8PostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes("пушкин") })).ConfigureAwait(false);
                Task<(string Method, string Body)> serverRequest = Server.WaitForRequest(
                    "/sleep.zzz",
                    r =>
                    {
                        using StreamReader reader = new(r.Body, Encoding.UTF8);
                        return (r.Method, reader.ReadToEnd());
                    });
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'POST', body: 'birdy' })")).ConfigureAwait(false);
                Assert.That(serverRequest.Result.Method, Is.EqualTo("POST"));
                Assert.That(serverRequest.Result.Body, Is.EqualTo("пушкин"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend longer post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAmendLongerPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes("doggo-is-longer-than-birdy") })).ConfigureAwait(false);
                Task<(string Method, string Body)> serverRequest = Server.WaitForRequest(
                    "/sleep.zzz",
                    r =>
                    {
                        using StreamReader reader = new(r.Body);
                        return (r.Method, reader.ReadToEnd());
                    });
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'POST', body: 'birdy' })")).ConfigureAwait(false);
                Assert.That(serverRequest.Result.Method, Is.EqualTo("POST"));
                Assert.That(serverRequest.Result.Body, Is.EqualTo("doggo-is-longer-than-birdy"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should amend binary post data")]
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

                await page.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOptions { PostData = arr })).ConfigureAwait(false);
                Task<(string Method, byte[] Body)> serverRequest = Server.WaitForRequest(
                    "/sleep.zzz",
                    r =>
                    {
                        using MemoryStream ms = new();
                        r.Body.CopyTo(ms);
                        return (r.Method, ms.ToArray());
                    });
                await Task.WhenAll(serverRequest, page.EvaluateAsync("() => fetch('/sleep.zzz', { method: 'POST', body: 'birdy' })")).ConfigureAwait(false);
                Assert.That(serverRequest.Result.Method, Is.EqualTo("POST"));
                Assert.That(serverRequest.Result.Body.Length, Is.EqualTo(arr.Length));
                Assert.That(serverRequest.Result.Body, Is.EqualTo(arr));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should use content-type from original request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseContentTypeFromOriginalRequest()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync(Prefix + "/title.html", route => route.ContinueAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes("{\"b\":2}") })).ConfigureAwait(false);
                Task<(string Type, string Body)> request = Server.WaitForRequest(
                    "/title.html",
                    r =>
                    {
                        using StreamReader reader = new(r.Body);
                        return ((string)r.Headers["content-type"], reader.ReadToEnd());
                    });
                await Task.WhenAll(
                    request,
                    page.EvaluateAsync(
                        "async url => { await fetch(url, { method: 'POST', body: '{\"a\":1}', headers: { 'content-type': 'application/json' } }); }",
                        Prefix + "/title.html")).ConfigureAwait(false);
                Assert.That(request.Result.Type, Is.EqualTo("application/json"));
                Assert.That(request.Result.Body, Is.EqualTo("{\"b\":2}"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should work with Cross-Origin-Opener-Policy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCrossOriginOpenerPolicy()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/8796");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                string[] serverHeadersFoo = { null };
                List<string> serverRequests = new();
                Server.SetRoute("/empty.html", async http =>
                {
                    serverRequests.Add(http.Request.Path);
                    serverHeadersFoo[0] ??= (string)http.Request.Headers["foo"];
                    http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                    await http.Response.WriteAsync(
                        "<div>Hello there!</div><script>window.onload = () => console.log('onload')</script>").ConfigureAwait(false);
                });

                List<string> intercepted = new();
                await page.RouteAsync("**/*", route =>
                {
                    intercepted.Add(route.Request.Url);
                    _ = route.ContinueAsync(headers: new Dictionary<string, string> { ["foo"] = "bar" });
                }).ConfigureAwait(false);

                HashSet<IRequest> requests = new();
                List<string> events = new();
                page.Request += (_, r) =>
                {
                    events.Add("request");
                    requests.Add(r);
                };
                page.RequestFailed += (_, r) =>
                {
                    events.Add("requestfailed");
                    requests.Add(r);
                };
                page.RequestFinished += (_, r) =>
                {
                    events.Add("requestfinished");
                    requests.Add(r);
                };
                page.Response += (_, r) =>
                {
                    events.Add("response");
                    requests.Add(r.Request);
                };
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(new[] { EmptyPage }));
                Assert.That(serverRequests, Is.EqualTo(new[] { "/empty.html" }));
                Assert.That(serverHeadersFoo[0], Is.EqualTo("bar"));
                Assert.That(page.Url, Is.EqualTo(EmptyPage));
                await response.FinishedAsync().ConfigureAwait(false);
                Assert.That(events, Is.EqualTo(new[] { "request", "response", "requestfinished" }));
                Assert.That(requests, Has.Count.EqualTo(1));
                Assert.That(response.Request.Failure, Is.Null);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should not delete the origin header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotDeleteTheOriginHeader()
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
                string interceptedOrigin = null;
                await page.RouteAsync(CrossProcessPrefix + "/something", async route =>
                {
                    Dictionary<string, string> headers = await route.Request.AllHeadersAsync().ConfigureAwait(false);
                    interceptedOrigin = headers.TryGetValue("origin", out string origin) ? origin : null;
                    headers.Remove("origin");
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);

                Task<string> text = page.EvaluateAsync<string>(
                    "async url => { const data = await fetch(url); return data.text(); }",
                    CrossProcessPrefix + "/something");
                Task<string> serverRequest = Server.WaitForRequest("/something", r => (string)r.Headers["origin"]);
                await Task.WhenAll(text, serverRequest).ConfigureAwait(false);
                Assert.That(text.Result, Is.EqualTo("done"));
                Assert.That(interceptedOrigin, Is.EqualTo(Prefix));
                Assert.That(serverRequest.Result, Is.EqualTo(Prefix));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should continue preload link requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldContinuePreloadLinkRequests()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                bool intercepted = false;
                await page.RouteAsync("**/one-style.css", route =>
                {
                    intercepted = true;
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["custom"] = "value";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/one-style.css", r => (string)r.Headers["custom"]);
                await Task.WhenAll(serverRequest, page.GoToAsync(Prefix + "/preload.html")).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("value"));
                await page.WaitForFunctionAsync("() => window['preloadedStyles']", null, polling: "raf").ConfigureAwait(false);
                Assert.That(intercepted, Is.True);
                string color = await page.EvaluateAsync<string>("() => window.getComputedStyle(document.body).backgroundColor").ConfigureAwait(false);
                Assert.That(color, Is.EqualTo("rgb(255, 192, 203)"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should respect set-cookie in redirect response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectSetCookieInRedirectResponse()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"/set-cookie-redirect\">Set cookie</a>").ConfigureAwait(false);
                Server.SetRoute("/set-cookie-redirect", http =>
                {
                    http.Response.StatusCode = 302;
                    http.Response.Headers["set-cookie"] = "foo=bar;  max-age=36000";
                    http.Response.Headers["location"] = "/empty.html";
                    return Task.CompletedTask;
                });
                await page.RouteAsync("**/set-cookie-redirect", route =>
                {
                    _ = route.ContinueAsync(new() { Headers = HeadersOf(route.Request) });
                }).ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", r => (string)r.Headers["cookie"]);
                await page.GoToAsync(Prefix + "/set-cookie-redirect").ConfigureAwait(false);
                Assert.That(await serverRequest.ConfigureAwait(false), Is.EqualTo("foo=bar"));
                Assert.That(await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false), Is.EqualTo("foo=bar"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue should not propagate cookie override to redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueShouldNotPropagateCookieOverrideToRedirects()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/set-cookie", http =>
                {
                    http.Response.Headers["Set-Cookie"] = "foo=bar;";
                    return Task.CompletedTask;
                });
                await page.GoToAsync(Prefix + "/set-cookie").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false), Is.EqualTo("foo=bar"));
                Server.SetRedirect("/redirect", Prefix + "/empty.html");
                await page.RouteAsync("**/redirect", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["cookie"] = "override";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", r => (string)r.Headers["cookie"]);
                await Task.WhenAll(serverRequest, page.GoToAsync(Prefix + "/redirect")).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("foo=bar"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue should not override cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueShouldNotOverrideCookie()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/set-cookie", http =>
                {
                    http.Response.Headers["Set-Cookie"] = "foo=bar;";
                    return Task.CompletedTask;
                });
                await page.GoToAsync(Prefix + "/set-cookie").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false), Is.EqualTo("foo=bar"));
                await page.RouteAsync("**", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["cookie"] = "override";
                    headers["custom"] = "value";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<(string Cookie, string Custom)> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    r => ((string)r.Headers["cookie"], (string)r.Headers["custom"]));
                await Task.WhenAll(serverRequest, page.GoToAsync(EmptyPage)).ConfigureAwait(false);
                Assert.That(serverRequest.Result.Cookie, Is.EqualTo("foo=bar"));
                Assert.That(serverRequest.Result.Custom, Is.EqualTo("value"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue with headers should send fresh cookie from the browser cookie store")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueWithHeadersShouldSendFreshCookieFromTheBrowserCookieStore()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/set-cookie", http =>
                {
                    http.Response.Headers["Set-Cookie"] = "foo=v1;";
                    return Task.CompletedTask;
                });
                await page.GoToAsync(Prefix + "/set-cookie").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false), Is.EqualTo("foo=v1"));
                await page.RouteAsync("**/empty.html", async route =>
                {
                    await page.Context.AddCookiesAsync(new[]
                    {
                        new Cookie { Name = "foo", Value = "v2", Url = Prefix },
                    }).ConfigureAwait(false);
                    await route.ContinueAsync(new() { Headers = HeadersOf(route.Request) }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", r => (string)r.Headers["cookie"]);
                await Task.WhenAll(serverRequest, page.GoToAsync(EmptyPage)).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("foo=v2"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue with headers should send fresh cookie after a redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueWithHeadersShouldSendFreshCookieAfterARedirect()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/set-cookie", http =>
                {
                    http.Response.Headers["Set-Cookie"] = "foo=v1;";
                    return Task.CompletedTask;
                });
                await page.GoToAsync(Prefix + "/set-cookie").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false), Is.EqualTo("foo=v1"));
                Server.SetRoute("/redirect", http =>
                {
                    http.Response.StatusCode = 302;
                    http.Response.Headers["Set-Cookie"] = "foo=v2;";
                    http.Response.Headers["location"] = Prefix + "/empty.html";
                    return Task.CompletedTask;
                });
                await page.RouteAsync("**/redirect", route =>
                {
                    _ = route.ContinueAsync(new() { Headers = HeadersOf(route.Request) });
                }).ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", r => (string)r.Headers["cookie"]);
                await Task.WhenAll(serverRequest, page.GoToAsync(Prefix + "/redirect")).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("foo=v2"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "redirect after continue should be able to delete cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RedirectAfterContinueShouldBeAbleToDeleteCookie()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/set-cookie", http =>
                {
                    http.Response.Headers["Set-Cookie"] = "foo=bar;";
                    return Task.CompletedTask;
                });
                await page.GoToAsync(Prefix + "/set-cookie").ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false), Is.EqualTo("foo=bar"));
                Server.SetRoute("/delete-cookie", http =>
                {
                    http.Response.Headers["Set-Cookie"] = "foo=bar; expires=Thu, 01 Jan 1970 00:00:00 GMT";
                    return Task.CompletedTask;
                });
                Server.SetRedirect("/redirect", "/delete-cookie");
                await page.RouteAsync("**/redirect", route =>
                {
                    _ = route.ContinueAsync(new() { Headers = HeadersOf(route.Request) });
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/redirect").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", r => (string)r.Headers["cookie"]);
                await Task.WhenAll(serverRequest, page.GoToAsync(EmptyPage)).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.Null.Or.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue should propagate headers to redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueShouldPropagateHeadersToRedirects()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRedirect("/redirect", "/empty.html");
                await page.RouteAsync("**/redirect", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["custom"] = "value";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", r => (string)r.Headers["custom"]);
                await Task.WhenAll(serverRequest, page.GoToAsync(Prefix + "/redirect")).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("value"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue should drop content-length on redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueShouldDropContentLengthOnRedirects()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Server.SetRedirect("/redirect", "/empty.html");
                await page.RouteAsync("**/redirect", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["custom"] = "value";
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<(string Method, string Length, string Type, string Custom)> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    r => (r.Method, (string)r.Headers["content-length"], (string)r.Headers["content-type"], (string)r.Headers["custom"]));
                await Task.WhenAll(
                    serverRequest,
                    page.EvaluateAsync("url => fetch(url, { method: 'POST', body: 'foo' })", Prefix + "/redirect")).ConfigureAwait(false);
                Assert.That(serverRequest.Result.Method, Is.EqualTo("GET"));
                Assert.That(serverRequest.Result.Length, Is.Null.Or.Empty);
                Assert.That(serverRequest.Result.Type, Is.Null.Or.Empty);
                Assert.That(serverRequest.Result.Custom, Is.EqualTo("value"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "redirected requests should report overridden headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RedirectedRequestsShouldReportOverriddenHeaders()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRedirect("/redirect", "/empty.html");
                await page.RouteAsync("**/redirect", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["custom"] = "value";
                    _ = route.FallbackAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest("/empty.html", r => (string)r.Headers["custom"]);
                Task<IResponse> response = page.GoToAsync(Prefix + "/redirect");
                await Task.WhenAll(serverRequest, response).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("value"));
                Assert.That(response.Result.Request.Url, Is.EqualTo(EmptyPage));
                Assert.That(response.Result.Request.GetHeaderValue("custom"), Is.EqualTo("value"));
                Dictionary<string, string> all = await response.Result.Request.AllHeadersAsync().ConfigureAwait(false);
                Assert.That(all["custom"], Is.EqualTo("value"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue should delete headers on redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueShouldDeleteHeadersOnRedirects()
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
                Server.SetRedirect("/redirect", "/something");
                await page.RouteAsync("**/redirect", route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers.Remove("foo");
                    _ = route.ContinueAsync(new() { Headers = headers });
                }).ConfigureAwait(false);
                Task<string> text = page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, { headers: { foo: 'a' } });
    return data.text();
}",
                    Prefix + "/redirect");
                Task<string> serverRequest = Server.WaitForRequest("/something", r => (string)r.Headers["foo"]);
                await Task.WhenAll(text, serverRequest).ConfigureAwait(false);
                Assert.That(text.Result, Is.EqualTo("done"));
                Assert.That(serverRequest.Result, Is.Null.Or.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "propagate headers same origin redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PropagateHeadersSameOriginRedirect()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                TaskCompletionSource<Dictionary<string, string>> serverRequestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Server.SetRoute("/something", async http =>
                {
                    if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteCorsPreflight(http, Prefix, "authorization,cookie,custom");
                        return;
                    }

                    serverRequestTcs.TrySetResult(SnapshotHeaders(http.Request));
                    await http.Response.WriteAsync("done").ConfigureAwait(false);
                });
                Server.SetRedirect("/redirect", "/something");
                await page.EvaluateAsync("() => document.cookie = 'a=b'").ConfigureAwait(false);
                string text = await page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, {
        headers: {
            authorization: 'credentials',
            custom: 'foo'
        },
        credentials: 'include',
    });
    return data.text();
}",
                    Prefix + "/redirect").ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("done"));
                Dictionary<string, string> serverRequest = await serverRequestTcs.Task.ConfigureAwait(false);
                Assert.That(serverRequest["authorization"], Is.EqualTo("credentials"));
                Assert.That(serverRequest["cookie"], Is.EqualTo("a=b"));
                Assert.That(serverRequest["custom"], Is.EqualTo("foo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "propagate headers cross origin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PropagateHeadersCrossOrigin()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                TaskCompletionSource<Dictionary<string, string>> serverRequestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Server.SetRoute("/something", async http =>
                {
                    if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteCorsPreflight(http, Prefix, "authorization,custom");
                        return;
                    }

                    serverRequestTcs.TrySetResult(SnapshotHeaders(http.Request));
                    http.Response.Headers["Access-Control-Allow-Origin"] = Prefix;
                    http.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                    await http.Response.WriteAsync("done").ConfigureAwait(false);
                });
                string text = await page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, {
        headers: {
            authorization: 'credentials',
            custom: 'foo'
        },
        credentials: 'include',
    });
    return data.text();
}",
                    CrossProcessPrefix + "/something").ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("done"));
                Dictionary<string, string> serverRequest = await serverRequestTcs.Task.ConfigureAwait(false);
                Assert.That(serverRequest["authorization"], Is.EqualTo("credentials"));
                Assert.That(serverRequest["custom"], Is.EqualTo("foo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "propagate headers cross origin redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PropagateHeadersCrossOriginRedirect()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                TaskCompletionSource<Dictionary<string, string>> serverRequestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Server.SetRoute("/something", async http =>
                {
                    if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteCorsPreflight(http, Prefix, "authorization,cookie,custom");
                        return;
                    }

                    serverRequestTcs.TrySetResult(SnapshotHeaders(http.Request));
                    http.Response.Headers["Access-Control-Allow-Origin"] = Prefix;
                    http.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                    await http.Response.WriteAsync("done").ConfigureAwait(false);
                });
                Server.SetRoute("/redirect", http =>
                {
                    http.Response.StatusCode = 301;
                    http.Response.Headers["location"] = CrossProcessPrefix + "/something";
                    return Task.CompletedTask;
                });
                await page.EvaluateAsync("() => document.cookie = 'a=b'").ConfigureAwait(false);
                string text = await page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, {
        headers: {
            authorization: 'credentials',
            custom: 'foo'
        },
        credentials: 'include',
    });
    return data.text();
}",
                    Prefix + "/redirect").ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("done"));
                Dictionary<string, string> serverRequest = await serverRequestTcs.Task.ConfigureAwait(false);
                Assert.That(serverRequest.TryGetValue("authorization", out string authorization) ? authorization : null, Is.Null.Or.Empty);
                Assert.That(serverRequest.TryGetValue("cookie", out string cookie) ? cookie : null, Is.Null.Or.Empty);
                Assert.That(serverRequest["custom"], Is.EqualTo("foo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "propagate headers cross origin redirect after interception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PropagateHeadersCrossOriginRedirectAfterInterception()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                TaskCompletionSource<Dictionary<string, string>> serverRequestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Server.SetRoute("/something", async http =>
                {
                    if (string.Equals(http.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteCorsPreflight(http, Prefix, "authorization,cookie,custom");
                        return;
                    }

                    serverRequestTcs.TrySetResult(SnapshotHeaders(http.Request));
                    http.Response.Headers["Access-Control-Allow-Origin"] = Prefix;
                    http.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                    await http.Response.WriteAsync("done").ConfigureAwait(false);
                });
                Server.SetRoute("/redirect", http =>
                {
                    http.Response.StatusCode = 301;
                    http.Response.Headers["location"] = CrossProcessPrefix + "/something";
                    return Task.CompletedTask;
                });
                await page.EvaluateAsync("() => document.cookie = 'a=b'").ConfigureAwait(false);
                await page.RouteAsync("**/redirect", async route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    headers["authorization"] = "credentials";
                    headers["custom"] = "foo";
                    await route.ContinueAsync(new() { Headers = headers }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                string text = await page.EvaluateAsync<string>(
                    @"async url => {
    const data = await fetch(url, {
        headers: {
            authorization: 'none',
        },
        credentials: 'include',
    });
    return data.text();
}",
                    Prefix + "/redirect").ConfigureAwait(false);
                Assert.That(text, Is.EqualTo("done"));
                Dictionary<string, string> serverRequest = await serverRequestTcs.Task.ConfigureAwait(false);
                if (TestConstants.IsWebKit)
                {
                    Assert.That(serverRequest.TryGetValue("authorization", out string authorization) ? authorization : null, Is.Null.Or.Empty);
                }
                else
                {
                    Assert.That(serverRequest["authorization"], Is.EqualTo("credentials"));
                }

                Assert.That(serverRequest.TryGetValue("cookie", out string cookie) ? cookie : null, Is.Null.Or.Empty);
                Assert.That(serverRequest["custom"], Is.EqualTo("foo"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue should pass on 307 cross-origin redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueShouldPassOn307CrossOriginRedirect()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/final", async http =>
                {
                    http.Response.ContentType = "text/html";
                    await http.Response.WriteAsync("<!doctype html><title>final</title><p>ok</p>").ConfigureAwait(false);
                });
                Server.SetRoute("/redirect307", http =>
                {
                    http.Response.StatusCode = 307;
                    http.Response.Headers["location"] = Prefix + "/final";
                    return Task.CompletedTask;
                });

                await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                await page.SetContentAsync(
                    "<form id=\"f\" method=\"POST\" action=\"" + CrossProcessPrefix + "/redirect307\">\n      <input type=\"submit\">\n    </form>").ConfigureAwait(false);

                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                await Task.WhenAll(
                    page.WaitForURLAsync(Prefix + "/final"),
                    page.Locator("input").ClickAsync()).ConfigureAwait(false);
                Assert.That(await page.Locator("p").TextContentAsync().ConfigureAwait(false), Is.EqualTo("ok"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should intercept css variable with background url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptCssVariableWithBackgroundUrl()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Server.SetRoute("/test.html", async http =>
                {
                    http.Response.ContentType = "text/html";
                    await http.Response.WriteAsync(
                        @"
    <style>
      @keyframes JNDzq {
        0% { background-position: 0 0 }
        to { background-position: 100 0 }
      }
      div {
        --background: url(/pptr.png);
        background-image: var(--background);
        animation: JNDzq 1s linear infinite;
      }
    </style>
    <div>Yo!</div>").ConfigureAwait(false);
                });
                TaskCompletionSource<bool> interceptTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                int interceptedRequests = 0;
                await page.RouteAsync(Prefix + "/pptr.png", route =>
                {
                    interceptedRequests++;
                    interceptTcs.TrySetResult(true);
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/test.html").ConfigureAwait(false);
                Assert.That(await page.Locator("div").TextContentAsync().ConfigureAwait(false), Is.EqualTo("Yo!"));
                await interceptTcs.Task.ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
                Assert.That(interceptedRequests, Is.EqualTo(1));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "continue should not change multipart/form-data body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContinueShouldNotChangeMultipartFormDataBody()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Server.SetRoute("/upload", async http =>
                {
                    http.Response.ContentType = "text/plain";
                    await http.Response.WriteAsync("done").ConfigureAwait(false);
                });

                async Task<string> SendFormDataAsync()
                {
                    Task<string> requestPostBody = WaitForBodyAsync("/upload");
                    int status = await page.EvaluateAsync<int>(@"async () => {
    const newFile = new File(['file content'], 'file.txt');
    const formData = new FormData();
    formData.append('file', newFile);
    const response = await fetch('/upload', {
        method: 'POST',
        credentials: 'include',
        body: formData,
    });
    return response.status;
}").ConfigureAwait(false);
                    Assert.That(status, Is.EqualTo(200));
                    return await requestPostBody.ConfigureAwait(false);
                }

                string reqBefore = await SendFormDataAsync().ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
                string reqAfter = await SendFormDataAsync().ConfigureAwait(false);
                string fileContent = string.Join(
                    "\r\n",
                    new[]
                    {
                        "Content-Disposition: form-data; name=\"file\"; filename=\"file.txt\"",
                        "Content-Type: application/octet-stream",
                        string.Empty,
                        "file content",
                        "------",
                    });
                Assert.That(reqBefore, Does.Contain(fileContent));
                Assert.That(reqAfter, Does.Contain(fileContent));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "should not forward Host header on cross-origin redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotForwardHostHeaderOnCrossOriginRedirect()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                const string redirectTargetPath = "/final";
                const string redirectSourcePath = "/redirect";

                string redirectedHost = null;
                Server.SetRoute(redirectTargetPath, async http =>
                {
                    redirectedHost = http.Request.Headers["host"].ToString();
                    await http.Response.WriteAsync("OK").ConfigureAwait(false);
                });

                string firstHost = null;
                Server.SetRoute(redirectSourcePath, http =>
                {
                    firstHost = http.Request.Headers["host"].ToString();
                    http.Response.StatusCode = 302;
                    http.Response.Headers["location"] = CrossProcessPrefix + redirectTargetPath;
                    return Task.CompletedTask;
                });

                await page.RouteAsync("**/*", async route =>
                {
                    Dictionary<string, string> headers = HeadersOf(route.Request);
                    if (TestConstants.IsFirefox)
                    {
                        Assert.That(headers.ContainsKey("host"), Is.True);
                    }
                    else
                    {
                        Assert.That(headers.ContainsKey("host"), Is.False);
                    }

                    await route.ContinueAsync(new() { Headers = headers }).ConfigureAwait(false);
                }).ConfigureAwait(false);

                IResponse response = await page.GoToAsync(Prefix + redirectSourcePath).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(firstHost, Is.EqualTo(new Uri(Prefix).Authority));
                Assert.That(redirectedHost, Is.EqualTo(new Uri(CrossProcessPrefix).Authority));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-request-continue.spec.ts", "postData should return empty string when overriding body with empty string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PostDataShouldReturnEmptyStringWhenOverridingBodyWithEmptyString()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.ContinueAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes(string.Empty) })).ConfigureAwait(false);
                Task<IRequest> request = page.WaitForRequestAsync("**");
                await Task.WhenAll(
                    request,
                    page.EvaluateAsync(
                        "({ url }) => fetch(url, { method: 'POST', body: 'original' })",
                        new Dictionary<string, string> { ["url"] = Prefix + "/sleep.zzz" })).ConfigureAwait(false);
                Assert.That(request.Result.PostData, Is.EqualTo(string.Empty));
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

        private static Task<string> WaitForBodyAsync(string path)
            => Server.WaitForRequest(path, r =>
            {
                using StreamReader reader = new(r.Body);
                return reader.ReadToEnd();
            });

        private static void WriteCorsPreflight(HttpContext http, string allowOrigin, string allowHeaders)
        {
            http.Response.StatusCode = 204;
            http.Response.Headers["Access-Control-Allow-Origin"] = allowOrigin;
            http.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            http.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS, DELETE";
            http.Response.Headers["Access-Control-Allow-Headers"] = allowHeaders;
        }
    }
}
