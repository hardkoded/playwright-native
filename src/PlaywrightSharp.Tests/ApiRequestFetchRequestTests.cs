/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
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
