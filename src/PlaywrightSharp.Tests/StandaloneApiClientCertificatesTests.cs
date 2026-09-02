/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
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
