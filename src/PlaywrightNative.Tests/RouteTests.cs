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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests for page / browser context RouteAsync wiring.
    /// Exercises intercept / fulfill / continue / abort plus
    /// context-level routing against the direct CDP stack.
    /// </summary>
    [TestFixture]
    public class RouteTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [SetUp]
        public void SkipFirefox()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("RouteAsync is Chromium/WebKit until Firefox interception is wired.");
            }
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync intercepts request and fulfills")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteAsyncInterceptsRequestAndFulfills()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-route-intercept.html", context =>
            {
                // Server-side handler should never execute — the route fulfills before the request hits it.
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>from-server</html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/direct-route-intercept.html", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html><body>intercepted-body</body></html>", ContentType = "text/html", Status = 200 });
            }).ConfigureAwait(false);

            await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-intercept.html").ConfigureAwait(false);

            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("intercepted-body"));
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync resume allows server response")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteAsyncResumeAllowsServerResponse()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-route-resume.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><body>from-server</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            int invocations = 0;
            await page.RouteAsync("**/direct-route-resume.html", route =>
            {
                Interlocked.Increment(ref invocations);
                _ = route.ResumeAsync();
            }).ConfigureAwait(false);

            await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-resume.html").ConfigureAwait(false);

            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("from-server"));
            Assert.That(invocations, Is.GreaterThanOrEqualTo(1));
        }

        [PlaywrightTest("page-route.spec.ts", "ContinueAsync aliases ResumeAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ContinueAsyncShouldAllowServerResponse()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-route-continue.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><body>from-continue</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/direct-route-continue.html", route =>
            {
                _ = route.ContinueAsync();
            }).ConfigureAwait(false);

            await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-continue.html").ConfigureAwait(false);

            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("from-continue"));
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync abort fails navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteAsyncAbortFailsNavigation()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-route-abort.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html>should-not-reach</html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/direct-route-abort.html", route =>
            {
                _ = route.AbortAsync();
            }).ConfigureAwait(false);

            bool navigationFailed = false;
            try
            {
                await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-abort.html").ConfigureAwait(false);
            }
            catch (NavigationException)
            {
                navigationFailed = true;
            }
            catch (PlaywrightNativeException)
            {
                navigationFailed = true;
            }

            Assert.That(navigationFailed, Is.True, "Aborted request should cause navigation to fail.");
        }

        [PlaywrightTest("page-route.spec.ts", "Context route applies to newly created page")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextRouteAppliesToNewlyCreatedPage()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);

            await ctx.RouteAsync("**/direct-context-route.html", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html><body>context-fulfilled</body></html>", ContentType = "text/html", Status = 200 });
            }).ConfigureAwait(false);

            // Page created AFTER the context route is registered — must still observe it.
            IPage newPage = await ctx.NewPageAsync().ConfigureAwait(false);
            await newPage.GoToAsync($"http://localhost:{TestConstants.Port}/direct-context-route.html").ConfigureAwait(false);

            string body = (await newPage.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("context-fulfilled"));
        }

        [PlaywrightTest("page-route.spec.ts", "Route request url is accessible inside handler")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteRequestUrlIsAccessibleInsideHandler()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            string capturedUrl = null;
            string capturedMethod = null;

            await page.RouteAsync("**/direct-route-url.html", route =>
            {
                IRequest req = route.Request;
                capturedUrl = req?.Url;
                capturedMethod = req?.Method;
                _ = route.FulfillAsync(new() { Body = "<html><body>ok</body></html>", ContentType = "text/html", Status = 200 });
            }).ConfigureAwait(false);

            string target = $"http://localhost:{TestConstants.Port}/direct-route-url.html";
            await page.GoToAsync(target).ConfigureAwait(false);

            Assert.That(capturedUrl, Is.EqualTo(target));
            Assert.That(capturedMethod, Is.EqualTo("GET"));
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync matches regex pattern")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteAsyncMatchesRegexPattern()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync(new System.Text.RegularExpressions.Regex("direct-route-regex\\.html$"), route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html><body>regex-ok</body></html>", ContentType = "text/html", Status = 200 });
            }).ConfigureAwait(false);

            await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-regex.html").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("regex-ok"));
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync matches predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteAsyncMatchesPredicate()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync(url => url.Contains("direct-route-func.html", StringComparison.Ordinal), route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html><body>func-ok</body></html>", ContentType = "text/html", Status = 200 });
            }).ConfigureAwait(false);

            await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-func.html").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("func-ok"));
        }

        [PlaywrightTest("page-route.spec.ts", "UnrouteAsync stops intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task UnrouteAsyncStopsIntercepting()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-route-unroute.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><body>from-server</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

            Action<IRoute> handler = route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html><body>still-intercepted</body></html>", ContentType = "text/html", Status = 200 });
            };

            await page.RouteAsync("**/direct-route-unroute.html", handler).ConfigureAwait(false);
            await page.UnrouteAsync("**/direct-route-unroute.html", handler).ConfigureAwait(false);

            await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-unroute.html").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("from-server"));
        }

        [PlaywrightTest("page-route.spec.ts", "RouteAsync fulfills from path")]
        [Test]
        [Timeout(30_000)]
        public async Task RouteAsyncFulfillsFromPath()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            string file = Path.Combine(Path.GetTempPath(), $"pwsharp-route-{Guid.NewGuid():N}.html");
            File.WriteAllText(file, "<html><body>from-path</body></html>");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext ctx = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await ctx.NewPageAsync().ConfigureAwait(false);

                await page.RouteAsync("**/direct-route-path.html", route =>
                {
                    _ = route.FulfillAsync(new() { Path = file });
                }).ConfigureAwait(false);

                await page.GoToAsync($"http://localhost:{TestConstants.Port}/direct-route-path.html").ConfigureAwait(false);
                string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
                Assert.That(body, Is.EqualTo("from-path"));
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [PlaywrightTest("page-route.spec.ts", "FulfillAsync json serializes the body")]
        [Test]
        [Timeout(30_000)]
        public async Task FulfillAsyncJsonShouldSerializeTheBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/direct-route-json.json", route =>
            {
                _ = route.FulfillAsync(new() { Json = new { wave = 406, ok = true } });
            }).ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.ServerUrl + "/direct-route-json.json").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Does.Contain("\"wave\":406"));
            Assert.That(body, Does.Contain("\"ok\":true"));
            string contentType = await response.HeaderValueAsync("content-type").ConfigureAwait(false);
            Assert.That(contentType, Does.Contain("application/json"));
        }
    }
}
