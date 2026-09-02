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
    /// Official <c>webError.location</c>.
    /// </summary>
    [TestFixture]
    public class WebErrorLocationTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-event-pageerror.spec.ts", "WebError.Location reports the throwing script")]
        [Test]
        [Timeout(30_000)]
        public async Task WebErrorLocationShouldReportTheThrowingScript()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/wave455.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<!DOCTYPE html><html><body><script>\nthrow new Error('wave455');\n</script></body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IWebError error = await context.RunAndWaitForWebErrorAsync(
                () => page.GoToAsync(TestConstants.ServerUrl + "/wave455.html")).ConfigureAwait(false);

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Page, Is.SameAs(page));
            Assert.That(error.Error, Does.Contain("wave455"));
            Assert.That(error.Location, Is.Not.Null);
            Assert.That(error.Location.Url, Does.Contain("wave455.html"));
            Assert.That(error.Location.Line, Is.GreaterThanOrEqualTo(0));
            Assert.That(error.Location.Column, Is.GreaterThanOrEqualTo(0));
        }
    }
}
