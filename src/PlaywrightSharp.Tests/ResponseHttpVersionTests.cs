/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IResponse.HttpVersionAsync"/>.
    /// </summary>
    [TestFixture]
    public class ResponseHttpVersionTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-network-request.spec.ts", "document response reports HTTP version")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportHttpVersion()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);

            string version = await response.HttpVersionAsync().ConfigureAwait(false);
            Assert.That(version, Is.Not.Null.And.Not.Empty);
            Assert.That(version, Does.Contain("HTTP").IgnoreCase.Or.Contain("h2").IgnoreCase);
        }
    }
}
