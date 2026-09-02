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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official standalone <c>APIRequest.NewContextAsync(clientCertificates)</c>.
    /// </summary>
    [TestFixture]
    public class StandaloneApiClientCertificatesTests : PageTestEx
    {
        [PlaywrightTest("client-certificates.spec.ts", "Playwright.APIRequest clientCertificates")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneApiRequestShouldPresentMatchingClientCertificate()
        {
            await using MutualTlsServer server = await MutualTlsServer.StartAsync().ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    new ClientCertificate
                    {
                        Origin = server.Origin,
                        Cert = server.ClientCertPem,
                        Key = server.ClientKeyPem,
                    },
                }
            }).ConfigureAwait(false);

            IAPIResponse response = await request.GetAsync(server.Origin + "/").ConfigureAwait(false);
            string body = await response.TextAsync().ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(body, Does.Contain("Hello CN=Alice"));
        }
    }
}
