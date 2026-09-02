/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IResponse.ServerAddrAsync"/> and
    /// <see cref="IResponse.SecurityDetailsAsync"/>.
    /// </summary>
    [TestFixture]
    public class ResponseServerAddrTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-network-request.spec.ts", "ServerAddr reports the test server")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportServerAddress()
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

            ResponseServerAddrResult addr = await response.ServerAddrAsync().ConfigureAwait(false);
            if (addr == null)
            {
                Assert.Ignore("Browser did not report remoteIPAddress for this response.");
                return;
            }

            Assert.That(addr.IpAddress, Does.Contain("127.0.0.1").Or.Contain("::1"));
            Assert.That(addr.Port, Is.EqualTo(TestConstants.Port));
        }

        [PlaywrightTest("page-network-request.spec.ts", "HTTP responses have no security details")]
        [Test]
        [Timeout(30_000)]
        public async Task HttpResponseShouldHaveNoSecurityDetails()
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
            Assert.That(await response.SecurityDetailsAsync().ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-network-request.spec.ts", "HTTPS responses report TLS details")]
        [Test]
        [Timeout(30_000)]
        public async Task HttpsResponseShouldReportSecurityDetails()
        {
            if (TestServerSetup.HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.HttpsPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Ok, Is.True);

            ResponseSecurityDetailsResult details = await response.SecurityDetailsAsync().ConfigureAwait(false);
            if (details == null)
            {
                Assert.Ignore("Browser did not report securityDetails for this response.");
                return;
            }

            Assert.That(details.Protocol, Does.Contain("TLS").IgnoreCase.Or.Contain("SSL").IgnoreCase);
            Assert.That(details.SubjectName, Is.Not.Null.And.Not.Empty);
        }
    }
}
