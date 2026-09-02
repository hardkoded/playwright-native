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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-route.spec.ts</c> parity.
    /// Electron / Android skips do not apply. Do not edit leftover
    /// <c>ContextRouteTimesTests</c> or leftover route tests.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextRouteParityTests : PageTestEx
    {
        private const string FetchStatus =
            "(() => fetch('/api').then(r => r.status))()";

        private static SimpleServer _ownedServer;
        private static OfficialHttpsTargetServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string Hostname = "localhost";

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19848;
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
                    Hostname = "localhost";
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
                Hostname = "localhost";
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

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should intercept")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIntercept()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            bool intercepted = false;
            IPage page = null;
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted = true;
                IRequest request = route.Request;
                Assert.That(request.Url, Does.Contain("empty.html"));
                Assert.That(string.IsNullOrEmpty(request.GetHeaderValue("user-agent")), Is.False);
                Assert.That(request.Method, Is.EqualTo("GET"));
                Assert.That(request.PostData, Is.Null);
                Assert.That(request.IsNavigationRequest, Is.True);
                Assert.That(request.ResourceType, Is.EqualTo("document"));
                Assert.That(request.Frame, Is.SameAs(page.MainFrame));
                Assert.That(request.Frame.Url, Is.EqualTo("about:blank"));
                _ = route.ContinueAsync();
            }).ConfigureAwait(false);
            page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(intercepted, Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should unroute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUnroute()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<int> intercepted = new();
            await context.RouteAsync("**/*", route =>
            {
                intercepted.Add(1);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(2);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(3);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);

            void Handler4(IRoute route)
            {
                intercepted.Add(4);
                _ = route.FallbackAsync();
            }

            await context.RouteAsync(new Regex("empty.html"), Handler4).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { 4, 3, 2, 1 }));

            intercepted.Clear();
            await context.UnrouteAsync(new Regex("empty.html"), Handler4).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));

            intercepted.Clear();
            await context.UnrouteAsync("**/empty.html").ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { 1 }));

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should yield to page.route")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldYieldToPageRoute()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.FulfillAsync(new() { Status = 200, Body = "context" });
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/empty.html", route =>
            {
                _ = route.FulfillAsync(new() { Status = 200, Body = "page" });
            }).ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.GetTextAsync().ConfigureAwait(false), Is.EqualTo("page"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should fall back to context.route")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFallBackToContextRoute()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.FulfillAsync(new() { Status = 200, Body = "context" });
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/non-empty.html", route =>
            {
                _ = route.FulfillAsync(new() { Status = 200, Body = "page" });
            }).ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.GetTextAsync().ConfigureAwait(false), Is.EqualTo("context"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should support Set-Cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportSetCookieHeader()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("https://example.com/", route =>
            {
                _ = route.FulfillAsync(new() { Headers = new[] { new KeyValuePair<string, string>("Set-Cookie", "name=value; domain=.example.com; Path=/") }, ContentType = "text/html", Body = "done" });
            }).ConfigureAwait(false);
            await page.GoToAsync("https://example.com").ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            AssertCookie(cookies[0], "name", "value", ".example.com", "/", -1, false, false, DefaultSameSite());
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should ignore secure Set-Cookie header for insecure requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreSecureSetCookieHeaderForInsecureRequests()
        {
            if (TestConstants.IsWebKit && !TestConstants.IsMacOSX)
            {
                Assert.Ignore("official it.fixme(webkit && !isMac)");
            }

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("http://example.com/", route =>
            {
                _ = route.FulfillAsync(new() { Headers = new[] { new KeyValuePair<string, string>("Set-Cookie", "name=value; domain=.example.com; Path=/; Secure") }, ContentType = "text/html", Body = "done" });
            }).ConfigureAwait(false);
            await page.GoToAsync("http://example.com").ConfigureAwait(false);
            Assert.That(await context.CookiesAsync().ConfigureAwait(false), Is.Empty);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should use Set-Cookie header in future requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseSetCookieHeaderInFutureRequests()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync(EmptyPage, route =>
            {
                _ = route.FulfillAsync(new() { Headers = new[] { new KeyValuePair<string, string>("Set-Cookie", "name=value") }, ContentType = "text/html", Body = "done" });
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            AssertCookie(cookies[0], "name", "value", Hostname, "/", -1, false, false, DefaultSameSite());

            string cookie = null;
            Server.SetRoute("/foo.html", async http =>
            {
                cookie = http.Request.Headers["Cookie"].ToString();
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await page.GoToAsync(Prefix + "/foo.html").ConfigureAwait(false);
            Assert.That(cookie, Is.EqualTo("name=value"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should work with ignoreHTTPSErrors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithIgnoreHttpsErrors()
        {
            if (_ownedHttps == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }

            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            string url = "https://localhost:" + _ownedHttps.Port.ToString(CultureInfo.InvariantCulture) + "/empty.html";
            IResponse response = await page.GoToAsync(url).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should support the times parameter with route matching")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTheTimesParameterWithRouteMatching()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<int> intercepted = new();
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(1);
                _ = route.ContinueAsync();
            }, times: 1).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Has.Count.EqualTo(1));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should work if handler with times parameter was removed from another handler")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkIfHandlerWithTimesParameterWasRemovedFromAnotherHandler()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<string> intercepted = new();

            void Handler(IRoute route)
            {
                intercepted.Add("first");
                _ = route.ContinueAsync();
            }

            await context.RouteAsync("**/*", Handler, times: 1).ConfigureAwait(false);
            await context.RouteAsync("**/*", async route =>
            {
                intercepted.Add("second");
                await context.UnrouteAsync("**/*", Handler).ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { "second" }));
            intercepted.Clear();
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { "second" }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should support async handler w/ times")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportAsyncHandlerWTimes()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", async route =>
            {
                await Task.Delay(100).ConfigureAwait(false);
                await route.FulfillAsync(new() { Body = "<html>intercepted</html>", ContentType = "text/html" }).ConfigureAwait(false);
            }, times: 1).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("body")).ToHaveTextAsync("intercepted").ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("body")).Not.ToHaveTextAsync("intercepted").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should overwrite post body with empty string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverwritePostBodyWithEmptyString()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.ContinueAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes(string.Empty) });
            }).ConfigureAwait(false);

            Task<string> req = Server.WaitForRequest("/empty.html", http =>
            {
                if (http.ContentLength.GetValueOrDefault() == 0)
                {
                    return string.Empty;
                }

                try
                {
                    if (http.Body.CanSeek)
                    {
                        http.Body.Position = 0;
                    }

                    using StreamReader reader = new StreamReader(http.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                    return reader.ReadToEnd();
                }
                catch (Exception)
                {
                    return http.ContentLength.GetValueOrDefault() == 0 ? string.Empty : "unreadable";
                }
            });
            await page.SetContentAsync(
                "<script>" +
                "(async () => {" +
                " await fetch('" + EmptyPage + "', { method: 'POST', body: 'original' });" +
                "})()" +
                "</script>").ConfigureAwait(false);
            string body = await req.ConfigureAwait(false);
            Assert.That(body, Is.EqualTo(string.Empty));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should chain fallback")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChainFallback()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<int> intercepted = new();
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(1);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(2);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(3);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should chain fallback w/ dynamic URL")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChainFallbackWDynamicUrl()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<int> intercepted = new();
            await context.RouteAsync("**/bar", route =>
            {
                intercepted.Add(1);
                _ = route.FallbackAsync(new() { Url = EmptyPage });
            }).ConfigureAwait(false);
            await context.RouteAsync("**/foo", route =>
            {
                intercepted.Add(2);
                _ = route.FallbackAsync(new() { Url = "http://localhost/bar" });
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(3);
                _ = route.FallbackAsync(new() { Url = "http://localhost/foo" });
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should not chain fulfill")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotChainFulfill()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            bool failed = false;
            await context.RouteAsync("**/empty.html", route =>
            {
                failed = true;
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.FulfillAsync(new() { Status = 200, Body = "fulfilled" });
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(Encoding.UTF8.GetString(await response.GetBodyAsync().ConfigureAwait(false)), Is.EqualTo("fulfilled"));
            Assert.That(failed, Is.False);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should not chain abort")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotChainAbort()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            bool failed = false;
            await context.RouteAsync("**/empty.html", route =>
            {
                failed = true;
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.AbortAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            Exception error = null;
            try
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(failed, Is.False);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should chain fallback into page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChainFallbackIntoPage()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<int> intercepted = new();
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(1);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(2);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(3);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await page.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(4);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await page.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(5);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await page.RouteAsync("**/empty.html", route =>
            {
                intercepted.Add(6);
                _ = route.FallbackAsync();
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { 6, 5, 4, 3, 2, 1 }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should fall back async")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFallBackAsync()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<int> intercepted = new();
            await context.RouteAsync("**/empty.html", async route =>
            {
                intercepted.Add(1);
                await Task.Delay(100).ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", async route =>
            {
                intercepted.Add(2);
                await Task.Delay(100).ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", async route =>
            {
                intercepted.Add(3);
                await Task.Delay(100).ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should bypass disk cache when context interception is enabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBypassDiskCacheWhenContextInterceptionIsEnabled()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/api*", route => route.ContinueAsync()).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            List<int> requests = new();
            Server.SetRoute("/api", async http =>
            {
                requests.Add(1);
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/plain";
                http.Response.Headers["cache-control"] = "public, max-age=31536000";
                await http.Response.WriteAsync("Hello").ConfigureAwait(false);
            });
            for (int i = 0; i < 3; i++)
            {
                Task<IResponse> respPromise = page.WaitForResponseAsync("**/api");
                await page.EvaluateAsync<int>(FetchStatus).ConfigureAwait(false);
                IResponse response = await respPromise.ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(requests.Count, Is.EqualTo(i + 1));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-route.spec.ts", "should fulfill popup main request using alias")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFulfillPopupMainRequestUsingAlias()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/*", async route =>
            {
                RouteFetchResult fetched = await route.FetchResultAsync().ConfigureAwait(false);
                await route.FulfillAsync(fetched, "hello").ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.SetContentAsync("<a target=_blank href=\"" + EmptyPage + "\">click me</a>").ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
            await page.GetByText("click me").ClickAsync().ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await Assertions.Expect(popup.Locator("body")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static SameSiteAttribute DefaultSameSite()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                return SameSiteAttribute.None;
            }

            return SameSiteAttribute.Lax;
        }

        private static void AssertCookie(
            BrowserContextCookiesResult cookie,
            string name,
            string value,
            string domain,
            string path,
            double expires,
            bool httpOnly,
            bool secure,
            SameSiteAttribute sameSite)
        {
            Assert.That(cookie.Name, Is.EqualTo(name));
            Assert.That(cookie.Value, Is.EqualTo(value));
            Assert.That(cookie.Domain, Is.EqualTo(domain));
            Assert.That(cookie.Path, Is.EqualTo(path));
            Assert.That(cookie.Expires, Is.EqualTo(expires));
            Assert.That(cookie.HttpOnly, Is.EqualTo(httpOnly));
            Assert.That(cookie.Secure, Is.EqualTo(secure));
            Assert.That(cookie.SameSite, Is.EqualTo(sameSite));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
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
    }
}
