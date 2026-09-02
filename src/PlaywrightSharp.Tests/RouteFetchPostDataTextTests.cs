/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>route.fetch({ postData })</c> string body.
    /// </summary>
    [TestFixture]
    public class RouteFetchPostDataTextTests : PageTestEx
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

        [PlaywrightTest("page-route.spec.ts", "FetchAsync postDataText overrides the body")]
        [Test]
        [Timeout(30_000)]
        public async Task FetchAsyncPostDataTextShouldOverrideBody()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/route-fetch-postdata-text", async http =>
            {
                using StreamReader reader = new StreamReader(http.Request.Body);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                http.Response.ContentType = "text/plain";
                await http.Response.WriteAsync(body).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.RouteAsync("**/route-fetch-postdata-text", async route =>
            {
                RouteFetchResult fetched = await route.FetchResultAsync(new() { PostData = System.Text.Encoding.UTF8.GetBytes("from-fetch") }).ConfigureAwait(false);
                await route.FulfillAsync(fetched).ConfigureAwait(false);
            }).ConfigureAwait(false);

            string text = await page.EvaluateAsync<string>(
                "fetch('/route-fetch-postdata-text', { method: 'POST', body: 'from-page' }).then(r => r.text())").ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("from-fetch"));
        }
    }
}
