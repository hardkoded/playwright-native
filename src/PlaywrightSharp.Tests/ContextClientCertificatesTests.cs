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
    /// Official <c>NewContextAsync(clientCertificates)</c> on Chromium and WebKit.
    /// </summary>
    [TestFixture]
    public class ContextClientCertificatesTests : PageTestEx
    {
        [PlaywrightTest("client-certificates.spec.ts", "APIRequest presents a matching client certificate")]
        [Test]
        [Timeout(30_000)]
        public async Task ApiRequestShouldPresentMatchingClientCertificate()
        {
            await using MutualTlsServer server = await MutualTlsServer.StartAsync().ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
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

            IAPIResponse response = await context.APIRequest.GetAsync(server.Origin + "/").ConfigureAwait(false);
            string body = await response.TextAsync().ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(body, Does.Contain("Hello CN=Alice"));
        }

        [PlaywrightTest("client-certificates.spec.ts", "APIRequest skips a certificate for another origin")]
        [Test]
        [Timeout(30_000)]
        public async Task ApiRequestShouldSkipCertificateForOtherOrigin()
        {
            await using MutualTlsServer server = await MutualTlsServer.StartAsync().ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    new ClientCertificate
                    {
                        Origin = "https://example.test:9443",
                        Cert = server.ClientCertPem,
                        Key = server.ClientKeyPem,
                    },
                }
            }).ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(server.Origin + "/").ConfigureAwait(false);
            string body = await response.TextAsync().ConfigureAwait(false);
            Assert.That(body, Does.Contain("Sorry, but you need to provide a client certificate to continue."));
        }

        [PlaywrightTest("client-certificates.spec.ts", "BrowserContextOptions.ClientCertificates")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserContextOptionsShouldForwardClientCertificates()
        {
            await using MutualTlsServer server = await MutualTlsServer.StartAsync().ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
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
                },
            }).ConfigureAwait(false);

            IAPIResponse response = await context.APIRequest.GetAsync(server.Origin + "/").ConfigureAwait(false);
            string body = await response.TextAsync().ConfigureAwait(false);
            Assert.That(body, Does.Contain("Hello CN=Alice"));
        }
    }
}
