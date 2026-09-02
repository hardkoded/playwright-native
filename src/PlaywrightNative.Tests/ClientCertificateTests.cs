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
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
