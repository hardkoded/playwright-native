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
