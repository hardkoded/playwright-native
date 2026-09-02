/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <see cref="ClientCertificate"/> option bag.
    /// </summary>
    [TestFixture]
    public class ClientCertificateTests : PageTestEx
    {
        [PlaywrightTest("client-certificates.spec.ts", "ClientCertificate stores origin cert and key")]
        [Test]
        [Timeout(30_000)]
        public async Task ClientCertificateShouldStoreOriginCertAndKey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            Assert.That(browser, Is.Not.Null);

            ClientCertificate cert = new ClientCertificate
            {
                Origin = "https://example.test:8443",
                CertPath = "/tmp/client.pem",
                KeyPath = "/tmp/client.key",
                Cert = Encoding.ASCII.GetBytes("CERT"),
                Key = Encoding.ASCII.GetBytes("KEY"),
                PfxPath = "/tmp/client.pfx",
                Pfx = Encoding.ASCII.GetBytes("PFX"),
                Passphrase = "secret",
            };

            Assert.That(cert.Origin, Is.EqualTo("https://example.test:8443"));
            Assert.That(cert.CertPath, Is.EqualTo("/tmp/client.pem"));
            Assert.That(cert.KeyPath, Is.EqualTo("/tmp/client.key"));
            Assert.That(cert.PfxPath, Is.EqualTo("/tmp/client.pfx"));
            Assert.That(cert.Passphrase, Is.EqualTo("secret"));
            Assert.That(cert.Cert, Is.EqualTo(Encoding.ASCII.GetBytes("CERT")));
            Assert.That(cert.Key, Is.EqualTo(Encoding.ASCII.GetBytes("KEY")));
            Assert.That(cert.Pfx, Is.EqualTo(Encoding.ASCII.GetBytes("PFX")));
        }
    }
}
