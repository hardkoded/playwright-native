/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>APIResponse.serverAddr()</c>.
    /// </summary>
    [TestFixture]
    public class ApiResponseServerAddrTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("global-fetch.spec.ts", "should return server address from response")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnServerAddressFromResponse()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
            {
                IAPIResponse response = await request.GetAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                ResponseServerAddrResult addr = await response.ServerAddrAsync().ConfigureAwait(false);
                Assert.That(addr, Is.Not.Null);
                Assert.That(addr.IpAddress, Does.Match(new Regex(@"^(127\.0\.0\.1|::1)$")));
                Assert.That(addr.Port, Is.EqualTo(TestConstants.Port));
                await response.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch.spec.ts", "context APIRequest reports server address")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextApiRequestShouldReportServerAddress()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await context.APIRequest.GetAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            ResponseServerAddrResult addr = await response.ServerAddrAsync().ConfigureAwait(false);
            Assert.That(addr, Is.Not.Null);
            Assert.That(addr.IpAddress, Does.Match(new Regex(@"^(127\.0\.0\.1|::1)$")));
            Assert.That(addr.Port, Is.EqualTo(TestConstants.Port));
        }
    }
}
