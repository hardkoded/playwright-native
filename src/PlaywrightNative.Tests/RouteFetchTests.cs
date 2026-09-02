/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IRoute.FetchAsync"/>.
    /// </summary>
    [TestFixture]
    public class RouteFetchTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-route.spec.ts", "FetchAsync then FulfillAsync")]
        [Test]
        [Order(1)]
        [Timeout(30_000)]
        public async Task ShouldFulfillWithFetchedResponse()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fetch.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>wave-107-from-server</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/route-fetch.html", async route =>
            {
                RouteFetchResult fetched = await route.FetchResultAsync().ConfigureAwait(false);
                await route.FulfillAsync(fetched).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fetch.html").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Does.Contain("wave-107-from-server"));
        }

        [PlaywrightTest("page-route.spec.ts", "FetchAsync then modify body")]
        [Test]
        [Order(1)]
        [Timeout(30_000)]
        public async Task ShouldAllowModifyingFetchedBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fetch-patch.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>original</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/route-fetch-patch.html", async route =>
            {
                RouteFetchResult fetched = await route.FetchResultAsync().ConfigureAwait(false);
                string text = Encoding.UTF8.GetString(fetched.Body ?? Array.Empty<byte>());
                string patched = text.Replace("original", "patched", StringComparison.Ordinal);
                await route.FulfillAsync(new() { Body = patched, ContentType = "text/html", Status = fetched.Status }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fetch-patch.html").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Does.Contain("patched"));
            Assert.That(body, Does.Not.Contain("original"));
        }

        [PlaywrightTest("page-route.spec.ts", "FetchAsync maxRedirects 0 returns the redirect")]
        [Test]
        [Order(1)]
        [Timeout(30_000)]
        public async Task FetchShouldReturnRedirectWhenMaxRedirectsIsZero()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fetch-dest-zero", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("should-not-follow");
            });
            Server.SetRedirect("/route-fetch-src-zero", TestConstants.ServerUrl + "/route-fetch-dest-zero");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            int status = 0;
            await page.RouteAsync("**/route-fetch-src-zero", async route =>
            {
                RouteFetchResult fetched = await route.FetchResultAsync(new() { MaxRedirects = 0 }).ConfigureAwait(false);
                status = fetched.Status;
                await route.FulfillAsync(new() { Status = 200, Body = "redirect-status-" + fetched.Status, ContentType = "text/html" }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fetch-src-zero").ConfigureAwait(false);
            Assert.That(status, Is.EqualTo(302));
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Does.Contain("redirect-status-302"));
        }

        [PlaywrightTest("page-route.spec.ts", "FetchAsync maxRedirects throws when the chain is longer")]
        [Test]
        [Order(1)]
        [Timeout(30_000)]
        public async Task FetchShouldThrowWhenMaxRedirectsExceeded()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fetch-final", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("done");
            });
            Server.SetRedirect("/route-fetch-hop2", TestConstants.ServerUrl + "/route-fetch-final");
            Server.SetRedirect("/route-fetch-hop1", TestConstants.ServerUrl + "/route-fetch-hop2");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightNativeException caught = null;
            await page.RouteAsync("**/route-fetch-hop1", async route =>
            {
                try
                {
                    await route.FetchAsync(new() { MaxRedirects = 1 }).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex)
                {
                    caught = ex;
                }

                await route.FulfillAsync(new() { Status = 200, Body = "failed", ContentType = "text/html" }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fetch-hop1").ConfigureAwait(false);
            Assert.That(caught, Is.Not.Null);
            Assert.That(caught.Message, Does.Contain("maxRedirects"));
        }

        [PlaywrightTest("page-route.spec.ts", "FetchAsync maxRetries recovers after connection reset")]
        [Test]
        [Order(10)]
        [Timeout(30_000)]
        public async Task FetchShouldRetryConnectionReset()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            int hits = 0;
            Server.SetRoute("/route-fetch-reset", http =>
            {
                int n = Interlocked.Increment(ref hits);
                if (n <= 2)
                {
                    http.Abort();
                    return Task.CompletedTask;
                }

                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>recovered</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/route-fetch-reset", async route =>
            {
                RouteFetchResult fetched = await route.FetchResultAsync(new() { MaxRetries = 2 }).ConfigureAwait(false);
                await route.FulfillAsync(fetched).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fetch-reset").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Does.Contain("recovered"));
            Assert.That(hits, Is.GreaterThanOrEqualTo(3));
        }

        [PlaywrightTest("page-route.spec.ts", "FetchAsync maxRetries 0 does not retry")]
        [Test]
        [Order(11)]
        [Timeout(30_000)]
        public async Task FetchShouldNotRetryWhenMaxRetriesIsZero()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            int hits = 0;
            Server.SetRoute("/route-fetch-reset-once", http =>
            {
                Interlocked.Increment(ref hits);
                http.Abort();
                return Task.CompletedTask;
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightNativeException caught = null;
            await page.RouteAsync("**/route-fetch-reset-once", async route =>
            {
                try
                {
                    await route.FetchAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex)
                {
                    caught = ex;
                }
                catch (HttpRequestException ex)
                {
                    caught = new PlaywrightNativeException(ex.Message, ex);
                }

                await route.FulfillAsync(new() { Status = 200, Body = "failed", ContentType = "text/html" }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fetch-reset-once").ConfigureAwait(false);
            Assert.That(caught, Is.Not.Null);
            Assert.That(hits, Is.EqualTo(1));
        }

        [PlaywrightTest("page-route.spec.ts", "FetchAsync maxRetries rejects a negative value")]
        [Test]
        [Order(2)]
        [Timeout(30_000)]
        public async Task FetchShouldRejectNegativeMaxRetries()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fetch-retries.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>ok</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ArgumentOutOfRangeException caught = null;
            await page.RouteAsync("**/route-fetch-retries.html", async route =>
            {
                try
                {
                    await route.FetchAsync(new() { MaxRetries = -1 }).ConfigureAwait(false);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    caught = ex;
                }

                await route.FulfillAsync(new() { Status = 200, Body = "ok", ContentType = "text/html" }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fetch-retries.html").ConfigureAwait(false);
            Assert.That(caught, Is.Not.Null);
            Assert.That(caught.ParamName, Is.EqualTo("maxRetries"));
        }
    }
}
