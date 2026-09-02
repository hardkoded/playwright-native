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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IRoute.FallbackAsync"/>.
    /// </summary>
    [TestFixture]
    public class RouteFallbackTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-route.spec.ts", "FallbackAsync chains to the next handler")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldInvokeTheNextMatchingHandler()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fallback.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-server</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/route-fallback.html", async route =>
            {
                await route.FulfillAsync(new() { Body = "<html><body>from-first</body></html>", ContentType = "text/html", Status = 200 }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.RouteAsync("**/route-fallback.html", async route =>
            {
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fallback.html").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Does.Contain("from-first"));
        }

        [PlaywrightTest("page-route.spec.ts", "FallbackAsync continues to the network")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldContinueToTheNetworkWhenAlone()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fallback-net.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-server</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/route-fallback-net.html", async route =>
            {
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/route-fallback-net.html").ConfigureAwait(false);
            string body = (await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false)) ?? string.Empty;
            Assert.That(body, Does.Contain("from-server"));
        }
    }
}
