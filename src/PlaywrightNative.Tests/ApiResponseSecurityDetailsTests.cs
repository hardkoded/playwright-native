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
