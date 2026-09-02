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
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>interception.spec.ts</c> parity for page.route glob matching,
    /// worker interception, service-worker ordering, and memory-cache disable.
    /// Skipped (Android / Node-only internals):
    /// Android-only <c>it.skip</c> branches are not applied here.
    /// <c>should work with regular expression passed from a different context</c>
    /// uses a normal <see cref="Regex"/> — C# has no Node <c>vm</c> module.
    /// <c>should intercept blob url requests</c> is WebKit-only
    /// (<c>it.skip(browserName !== 'webkit')</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class InterceptionParityTests : PageTestEx
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
            int basePort = 19782;
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

        [PlaywrightTest("interception.spec.ts", "should work with navigation")]
        [PlaywrightTest("interception.spec.ts", "should work with navigation @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNavigation()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Dictionary<string, IRequest> requests = new(StringComparer.Ordinal);
                await page.RouteAsync("**/*", route =>
                {
                    string[] parts = route.Request.Url.Split('/');
                    requests[parts[parts.Length - 1]] = route.Request;
                    _ = route.ContinueAsync();
                }).ConfigureAwait(false);
                Server.SetRedirect("/rrredirect", "/frames/one-frame.html");
                await page.GoToAsync(Prefix + "/rrredirect").ConfigureAwait(false);
                Assert.That(requests["rrredirect"].IsNavigationRequest, Is.True);
                Assert.That(requests["frame.html"].IsNavigationRequest, Is.True);
                Assert.That(requests["script.js"].IsNavigationRequest, Is.False);
                Assert.That(requests["style.css"].IsNavigationRequest, Is.False);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should intercept after a service worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptAfterAServiceWorker()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(Prefix + "/serviceworkers/fetchdummy/sw.html").ConfigureAwait(false);
                await page.EvaluateAsync("() => window['activationPromise']").ConfigureAwait(false);

                string swResponse = await page.EvaluateAsync<string>("() => window['fetchDummy']('foo')").ConfigureAwait(false);
                Assert.That(swResponse, Is.EqualTo("responseFromServiceWorker:foo"));

                await page.RouteAsync("**/foo", route =>
                {
                    int slash = route.Request.Url.LastIndexOf('/');
                    string name = route.Request.Url.Substring(slash + 1);
                    _ = route.FulfillAsync(new() { Status = 200, ContentType = "text/css", Body = "responseFromInterception:" + name });
                }).ConfigureAwait(false);

                string swResponse2 = await page.EvaluateAsync<string>("() => window['fetchDummy']('foo')").ConfigureAwait(false);
                Assert.That(swResponse2, Is.EqualTo("responseFromServiceWorker:foo"));

                string nonInterceptedResponse = await page.EvaluateAsync<string>("() => window['fetchDummy']('passthrough')").ConfigureAwait(false);
                Assert.That(nonInterceptedResponse, Is.EqualTo("FAILURE: Not Found"));

                if (!TestConstants.IsFirefox)
                {
                    Server.SetRedirect("/serviceworkers/fetchdummy/passthrough", "/simple.json");
                    string redirectedResponse = await page.EvaluateAsync<string>("() => window['fetchDummy']('passthrough')").ConfigureAwait(false);
                    Assert.That(redirectedResponse, Is.EqualTo("{\"foo\": \"bar\"}\n"));
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should work with glob")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldWorkWithGlob()
        {
            Regex GlobToRegex(string glob) => new Regex(UrlMatcher.GlobToRegexPattern(glob));

            Assert.That(GlobToRegex("**/*.js").IsMatch("https://localhost:8080/foo.js"), Is.True);
            Assert.That(GlobToRegex("**/*.css").IsMatch("https://localhost:8080/foo.js"), Is.False);
            Assert.That(GlobToRegex("*.js").IsMatch("https://localhost:8080/foo.js"), Is.False);
            Assert.That(GlobToRegex("https://**/*.js").IsMatch("https://localhost:8080/foo.js"), Is.True);
            Assert.That(GlobToRegex("http://localhost:8080/simple/path.js").IsMatch("http://localhost:8080/simple/path.js"), Is.True);
            Assert.That(GlobToRegex("**/{a,b}.js").IsMatch("https://localhost:8080/a.js"), Is.True);
            Assert.That(GlobToRegex("**/{a,b}.js").IsMatch("https://localhost:8080/b.js"), Is.True);
            Assert.That(GlobToRegex("**/{a,b}.js").IsMatch("https://localhost:8080/c.js"), Is.False);

            Assert.That(GlobToRegex("**/*.{png,jpg,jpeg}").IsMatch("https://localhost:8080/c.jpg"), Is.True);
            Assert.That(GlobToRegex("**/*.{png,jpg,jpeg}").IsMatch("https://localhost:8080/c.jpeg"), Is.True);
            Assert.That(GlobToRegex("**/*.{png,jpg,jpeg}").IsMatch("https://localhost:8080/c.png"), Is.True);
            Assert.That(GlobToRegex("**/*.{png,jpg,jpeg}").IsMatch("https://localhost:8080/c.css"), Is.False);
            Assert.That(GlobToRegex("foo*").IsMatch("foo.js"), Is.True);
            Assert.That(GlobToRegex("foo*").IsMatch("foo/bar.js"), Is.False);
            Assert.That(GlobToRegex("http://localhost:3000/signin-oidc*").IsMatch("http://localhost:3000/signin-oidc/foo"), Is.False);
            Assert.That(GlobToRegex("http://localhost:3000/signin-oidc*").IsMatch("http://localhost:3000/signin-oidcnice"), Is.True);

            Assert.That(GlobToRegex("**/*.js").IsMatch("/foo.js"), Is.True);
            Assert.That(GlobToRegex("asd/**.js").IsMatch("/foo.js"), Is.False);
            Assert.That(GlobToRegex("**/*.js").IsMatch("bar_foo.js"), Is.False);

            Assert.That(GlobToRegex("**/api/v[0-9]").IsMatch("http://example.com/api/v[0-9]"), Is.True);
            Assert.That(GlobToRegex("**/api/v[0-9]").IsMatch("http://example.com/api/version"), Is.False);

            Assert.That(GlobToRegex("**/api\\?param").IsMatch("http://example.com/api?param"), Is.True);
            Assert.That(GlobToRegex("**/api\\?param").IsMatch("http://example.com/api-param"), Is.False);
            Assert.That(
                GlobToRegex("**/three-columns/settings.html\\?**id=settings-**")
                    .IsMatch("http://mydomain:8080/blah/blah/three-columns/settings.html?id=settings-e3c58efe-02e9-44b0-97ac-dd138100cf7c&blah"),
                Is.True);

            Assert.That(UrlMatcher.GlobToRegexPattern("\\?"), Is.EqualTo("^\\?$"));
            Assert.That(UrlMatcher.GlobToRegexPattern("\\"), Is.EqualTo("^\\\\$"));
            Assert.That(UrlMatcher.GlobToRegexPattern("\\\\"), Is.EqualTo("^\\\\$"));
            Assert.That(UrlMatcher.GlobToRegexPattern("\\["), Is.EqualTo("^\\[$"));
            Assert.That(UrlMatcher.GlobToRegexPattern("[a-z]"), Is.EqualTo("^\\[a-z\\]$"));
            Assert.That(UrlMatcher.GlobToRegexPattern("$^+.\\*()|\\?\\{\\}\\[\\]"), Is.EqualTo("^\\$\\^\\+\\.\\*\\(\\)\\|\\?\\{\\}\\[\\]$"));

            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/", "http://playwright.dev"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/?a=b", "http://playwright.dev?a=b"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/", "h*://playwright.dev"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://api.playwright.dev/?x=y", "http://*.playwright.dev?x=y"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/foo/bar", "**/foo/**"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://playwright.dev", "http://playwright.dev/?x=y", "?x=y"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://playwright.dev/foo/", "http://playwright.dev/foo/bar?x=y", "./bar?x=y"), Is.True);

            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/foo$$bar", "http://playwright.dev/foo$$bar"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/a$&b", "http://playwright.dev/a$&b"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://playwright.dev", "http://playwright.dev/p$$q", "./p$$q"), Is.True);

            Assert.That(UrlMatcher.UrlMatches(null, "https://playwright.dev/fooBAR", "HtTpS://pLaYwRiGhT.dEv/fooBAR"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://ignored", "https://playwright.dev/fooBAR", "HtTpS://pLaYwRiGhT.dEv/fooBAR"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "https://playwright.dev/foobar", "https://playwright.dev/fooBAR"), Is.False);
            Assert.That(UrlMatcher.UrlMatches(null, "https://playwright.dev/foobar?a=b", "https://playwright.dev/foobar?A=B"), Is.False);

            Assert.That(UrlMatcher.UrlMatches(null, "http://example.com/path", "http://example.com:80/path"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "https://example.com/path", "https://example.com:443/path"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://example.com:8080/path", "http://example.com:8080/path"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://localhost/", "http://localhost:80/**"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://example.com/foo%20bar", "http://example.com/foo bar"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://xn--mnchen-3ya.de/", "http://münchen.de/"), Is.True);

            Assert.That(UrlMatcher.UrlMatches(null, "https://localhost:3000/?a=b", "**/?a=b"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "https://localhost:3000/?a=b", "**?a=b"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "https://localhost:3000/?a=b", "**=b"), Is.True);

            Assert.That(UrlMatcher.UrlMatches(null, "my.custom.protocol://foo", "my.custom.protocol://**"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "my.p://foo", "my.{p,y}://**"), Is.False);
            Assert.That(UrlMatcher.UrlMatches(null, "my.p://foo/", "my.{p,y}://**"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "file:///foo/", "f*e://**"), Is.True);

            Assert.That(GlobToRegex("http://localhost:8080/?imple/path.js").IsMatch("http://localhost:8080/Simple/path.js"), Is.False);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/", "http://playwright.?ev"), Is.False);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright./?ev", "http://playwright.?ev"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/foo", "http://playwright.dev/f??"), Is.False);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/f??", "http://playwright.dev/f??"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/?x=y", "http://playwright.dev\\?x=y"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "http://playwright.dev/?x=y", "http://playwright.dev/\\?x=y"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://playwright.dev/foo", "http://playwright.dev/foo?bar", "?bar"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://playwright.dev/foo", "http://playwright.dev/foo?bar", "\\\\?bar"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://first.host/", "http://second.host/foo", "**/foo"), Is.True);
            Assert.That(UrlMatcher.UrlMatches("http://playwright.dev/", "http://localhost/", "*//localhost/"), Is.True);

            Assert.That(UrlMatcher.UrlMatches(null, "https://foo/bar.js", "https://foo/**/bar.js"), Is.True);
            Assert.That(UrlMatcher.UrlMatches(null, "https://foo/bar.js", "https://foo/**/**/bar.js"), Is.True);

            string[] customPrefixes = new[] { "about", "data", "chrome", "edge", "file" };
            foreach (string prefix in customPrefixes)
            {
                Assert.That(UrlMatcher.UrlMatches("http://playwright.dev/", prefix + ":blank", prefix + ":blank"), Is.True);
                Assert.That(UrlMatcher.UrlMatches("http://playwright.dev/", prefix + ":blank", "http://playwright.dev/"), Is.False);
                Assert.That(UrlMatcher.UrlMatches(null, prefix + ":blank", prefix + ":blank"), Is.True);
                Assert.That(UrlMatcher.UrlMatches(null, prefix + ":blank", prefix + ":*"), Is.True);
                Assert.That(UrlMatcher.UrlMatches(null, "not" + prefix + ":blank", prefix + ":*"), Is.False);
            }
        }

        [PlaywrightTest("interception.spec.ts", "should throw on unbalanced glob braces")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowOnUnbalancedGlobBraces()
        {
            Exception unmatchedOpen = Assert.Throws<PlaywrightNativeException>(() => UrlMatcher.GlobToRegexPattern("{foo"));
            Assert.That(unmatchedOpen.Message, Does.Contain("Invalid glob pattern \"{foo\": unmatched '{'"));
            Exception unmatchedClose = Assert.Throws<PlaywrightNativeException>(() => UrlMatcher.GlobToRegexPattern("}foo"));
            Assert.That(unmatchedClose.Message, Does.Contain("Invalid glob pattern \"}foo\": unmatched '}'"));
            Assert.That(
                Assert.Throws<PlaywrightNativeException>(() => UrlMatcher.GlobToRegexPattern("http://*/foo{")).Message,
                Does.Contain("unmatched '{'"));
            Assert.That(
                Assert.Throws<PlaywrightNativeException>(() => UrlMatcher.GlobToRegexPattern("**/*.png?{")).Message,
                Does.Contain("unmatched '{'"));
            Assert.That(
                Assert.Throws<PlaywrightNativeException>(() => UrlMatcher.GlobToRegexPattern("https://example.com/{a")).Message,
                Does.Contain("unmatched '{'"));
            Assert.That(
                Assert.Throws<PlaywrightNativeException>(() => UrlMatcher.GlobToRegexPattern("{{foo}")).Message,
                Does.Contain("nested '{' is not supported"));
            Assert.DoesNotThrow(() => UrlMatcher.GlobToRegexPattern("\\{foo"));
            Assert.DoesNotThrow(() => UrlMatcher.GlobToRegexPattern("foo\\}"));
        }

        [PlaywrightTest("interception.spec.ts", "should throw on page.route with invalid glob")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnPageRouteWithInvalidGlob()
        {
            await WithPageAsync(async page =>
            {
                Exception error = Assert.CatchAsync(() => page.RouteAsync("http://*/foo{", route => route.ContinueAsync()));
                Assert.That(error.Message, Does.Contain("unmatched '{'"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should intercept by glob")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptByGlob()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("http://localhos**?*oo", async route =>
                {
                    await route.FulfillAsync(new() { Status = 200, Body = "intercepted" }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                string result = await page.EvaluateAsync<string>(
                    "url => fetch(url).then(r => r.text())",
                    Prefix + "/?foo").ConfigureAwait(false);
                Assert.That(result, Is.EqualTo("intercepted"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should intercept network activity from worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptNetworkActivityFromWorker()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Server.SetRoute("/data_for_worker", http => http.Response.WriteAsync("failed to intercept"));
                string url = Prefix + "/data_for_worker";
                await page.RouteAsync(url, route => route.FulfillAsync(new() { Status = 200, Body = "intercepted" })).ConfigureAwait(false);
                Task<IConsoleMessage> consoleTask = page.WaitForConsoleMessageAsync();
                await page.EvaluateAsync<object>(
                    "url => new Worker(URL.createObjectURL(new Blob([`" +
                    "fetch(\"${url}\").then(response => response.text()).then(console.log);" +
                    "`], { type: 'application/javascript' })))",
                    url).ConfigureAwait(false);
                IConsoleMessage msg = await consoleTask.ConfigureAwait(false);
                Assert.That(msg.Text, Is.EqualTo("intercepted"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should intercept worker requests when enabled after worker creation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptWorkerRequestsWhenEnabledAfterWorkerCreation()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Server.SetRoute("/data_for_worker", http => http.Response.WriteAsync("failed to intercept"));
                string url = Prefix + "/data_for_worker";
                Task<IWorker> workerTask = page.WaitForEventAsync(PageEvent.Worker);
                await page.EvaluateAsync<object>(
                    "url => { window.w = new Worker(URL.createObjectURL(new Blob([`" +
                    "onmessage = function(e) {" +
                    "  fetch(\"${url}\").then(response => response.text()).then(console.log);" +
                    "};" +
                    "`], { type: 'application/javascript' }))); }",
                    url).ConfigureAwait(false);
                await workerTask.ConfigureAwait(false);
                await page.RouteAsync(url, route => route.FulfillAsync(new() { Status = 200, Body = "intercepted" })).ConfigureAwait(false);
                Task<IConsoleMessage> consoleTask = page.WaitForConsoleMessageAsync();
                await page.EvaluateAsync("() => window.w.postMessage('')").ConfigureAwait(false);
                IConsoleMessage msg = await consoleTask.ConfigureAwait(false);
                Assert.That(msg.Text, Is.EqualTo("intercepted"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should intercept network activity from worker 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptNetworkActivityFromWorker2()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                string url = Prefix + "/worker/worker.js";
                await page.RouteAsync(url, route => route.FulfillAsync(new() { Status = 200, Body = "console.log(\"intercepted\");", ContentType = "application/javascript" })).ConfigureAwait(false);
                Task<IConsoleMessage> consoleTask = page.WaitForConsoleMessageAsync();
                await page.GoToAsync(Prefix + "/worker/worker.html").ConfigureAwait(false);
                IConsoleMessage msg = await consoleTask.ConfigureAwait(false);
                Assert.That(msg.Text, Is.EqualTo("intercepted"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should work with regular expression passed from a different context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithRegularExpressionPassedFromADifferentContext()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                Regex regexp = new Regex("empty\\.html");
                bool intercepted = false;
                await page.RouteAsync(regexp, (route) =>
                {
                    IRequest request = route.Request;
                    Assert.That(route.Request, Is.EqualTo(request));
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

        [PlaywrightTest("interception.spec.ts", "should intercept every request matching a global regexp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptEveryRequestMatchingAGlobalRegexp()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                int intercepted = 0;
                await page.RouteAsync(new Regex("/intercept-me"), async route =>
                {
                    intercepted++;
                    await route.FulfillAsync(new() { Body = "intercepted" }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                string url = Prefix + "/intercept-me";
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(
                        await page.EvaluateAsync<string>(
                            "u => fetch(u, { cache: 'no-store' }).then(r => r.text())",
                            url).ConfigureAwait(false),
                        Is.EqualTo("intercepted"));
                }

                Assert.That(intercepted, Is.EqualTo(3));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should not break remote worker importScripts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotBreakRemoteWorkerImportScripts()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.RouteAsync("**", async route =>
                {
                    await route.ContinueAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/worker/worker-http-import.html").ConfigureAwait(false);
                await page.WaitForSelectorAsync("#status:has-text('finished')").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should disable memory cache when intercepting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDisableMemoryCacheWhenIntercepting()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                int intercepted = 0;
                await page.RouteAsync("**/page.html", route =>
                {
                    intercepted++;
                    _ = route.FulfillAsync(new() { Body = "success" });
                }).ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/page.html").ConfigureAwait(false);
                Assert.That(await page.Locator("body").TextContentAsync().ConfigureAwait(false), Does.Contain("success"));
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await Assertions.Expect(page).ToHaveURLAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(1));
                await page.GoBackAsync().ConfigureAwait(false);
                await Assertions.Expect(page).ToHaveURLAsync(Prefix + "/page.html").ConfigureAwait(false);
                Assert.That(intercepted, Is.EqualTo(2));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("interception.spec.ts", "should intercept blob url requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterceptBlobUrlRequests()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("upstream it.skip(browserName !== 'webkit')");
            }

            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await page.RouteAsync("**/*", route =>
                {
                    _ = route.FulfillAsync(new() { Status = 200, Body = "intercepted" });
                }).ConfigureAwait(false);
                string response = await page.EvaluateAsync<string>(
                    @"async () => {
                        const blobUrl = URL.createObjectURL(new Blob(['failed to intercept'], { type: 'text/plain' }));
                        return await fetch(blobUrl).then(response => response.text());
                    }").ConfigureAwait(false);
                Assert.That(response, Is.EqualTo("intercepted"));
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
    }
}
