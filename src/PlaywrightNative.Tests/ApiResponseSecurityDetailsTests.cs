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
    /// Official <c>APIResponse.securityDetails()</c>.
    /// </summary>
    [TestFixture]
    public class ApiResponseSecurityDetailsTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("global-fetch.spec.ts", "should return null security details for http response")]
        [Test]
        [Timeout(30_000)]
        public async Task HttpResponseShouldHaveNoSecurityDetails()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(await response.SecurityDetailsAsync().ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("global-fetch.spec.ts", "should return security details from response")]
        [Test]
        [Timeout(30_000)]
        public async Task HttpsResponseShouldReportSecurityDetails()
        {
            if (TestServerSetup.HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(TestConstants.HttpsPrefix + "/empty.html").ConfigureAwait(false);
            ResponseSecurityDetailsResult details = await response.SecurityDetailsAsync().ConfigureAwait(false);
            Assert.That(details, Is.Not.Null);
            Assert.That(details.Protocol, Does.Contain("TLS").IgnoreCase);
            Assert.That(details.SubjectName, Is.Not.Null.And.Not.Empty);
        }
    }
}
