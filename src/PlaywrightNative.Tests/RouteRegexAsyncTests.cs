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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.RouteAsync(Regex, Func{IRoute, Task})"/>.
    /// </summary>
    [TestFixture]
    public class RouteRegexAsyncTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-route.spec.ts", "async regex route fulfills")]
        [Test]
        [Timeout(30_000)]
        public async Task AsyncRegexRouteShouldFulfill()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/regex-async.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-network</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync(new Regex("regex-async\\.html$"), async route =>
            {
                await route.FulfillAsync(new() { ContentType = "text/html", Body = "<html><body>from-async-regex</body></html>" }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await page.GoToAsync(TestConstants.ServerUrl + "/regex-async.html").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
            Assert.That(text, Does.Contain("from-async-regex"));
        }
    }
}
