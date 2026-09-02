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
    /// Expect API response ToBeOK.
    /// </summary>
    [TestFixture]
    public class ExpectApiTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("expect-misc.spec.ts", "ToBeOK matches 2xx and Not matches 404")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeOKShouldMatch2xxAndNotMatch404()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/expect-ok", http =>
            {
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("ok");
            });
            Server.SetRoute("/expect-missing", http =>
            {
                http.Response.StatusCode = 404;
                return http.Response.WriteAsync("no");
            });

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse ok = await request.GetAsync(TestConstants.ServerUrl + "/expect-ok").ConfigureAwait(false);
            IAPIResponse missing = await request.GetAsync(TestConstants.ServerUrl + "/expect-missing").ConfigureAwait(false);

            await Assertions.Expect(ok).ToBeOKAsync().ConfigureAwait(false);
            await Assertions.Expect(missing).Not.ToBeOKAsync(timeout: 2000).ConfigureAwait(false);
        }
    }
}
