/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.UnrouteAllAsync"/>.
    /// </summary>
    [TestFixture]
    public class UnrouteAllTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("unroute-behavior.spec.ts", "UnrouteAllAsync stops intercepting")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUnrouteAllShouldStopIntercepting()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/unroute-all.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-server</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/unroute-all.html", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html><body>from-route</body></html>", ContentType = "text/html", Status = 200 });
            }).ConfigureAwait(false);

            await page.UnrouteAllAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/unroute-all.html").ConfigureAwait(false);

            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("from-server"));
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context UnrouteAllAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUnrouteAllShouldStopIntercepting()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/unroute-all-ctx.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-server</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await context.RouteAsync("**/unroute-all-ctx.html", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html><body>from-context-route</body></html>", ContentType = "text/html", Status = 200 });
            }).ConfigureAwait(false);

            await context.UnrouteAllAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/unroute-all-ctx.html").ConfigureAwait(false);

            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Is.EqualTo("from-server"));
        }
    }
}
