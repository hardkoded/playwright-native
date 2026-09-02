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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>APIRequestContext.fetch(request)</c>.
    /// </summary>
    [TestFixture]
    public class ApiRequestFetchRequestTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("global-fetch.spec.ts", "FetchAsync replays a page request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReplayAPageRequest()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/api-replay", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("replayed");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string url = TestConstants.ServerUrl + "/api-replay";
            Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("/api-replay", StringComparison.Ordinal));
            await page.GoToAsync(url).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.FetchAsync(request).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo("replayed"));
        }

        [PlaywrightTest("global-fetch.spec.ts", "FetchAsync overrides request parameters")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldOverrideRequestParameters()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string seenMethod = null;
            string seenBody = null;
            Server.Reset();
            Server.SetRoute("/api-override", async http =>
            {
                seenMethod = http.Request.Method;
                seenBody = await new System.IO.StreamReader(http.Request.Body).ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync("ok").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string url = TestConstants.ServerUrl + "/api-override";
            Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("/api-override", StringComparison.Ordinal));
            await page.GoToAsync(url).ConfigureAwait(false);
            IRequest request = await waitTask.ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.FetchAsync(request, new() { Method = "POST", Data = "Data" }).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(seenMethod, Is.EqualTo("POST"));
            Assert.That(seenBody, Is.EqualTo("Data"));
        }
    }
}
