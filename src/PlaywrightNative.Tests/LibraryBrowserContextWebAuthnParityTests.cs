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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-webauthn.spec.ts</c> parity.
    /// Do not edit leftover <c>Credentials*Tests</c>,
    /// <c>StorageStateCredentialsTests</c>, or Wave 852
    /// storage-state WebAuthn titles.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextWebAuthnParityTests : PageTestEx
    {
        private const string AuthenticateScript = @"async ({ rpId, credentialId }) => {
            const b64UrlToBytes = (s) => {
                let str = s.replace(/-/g, '+').replace(/_/g, '/');
                while (str.length % 4)
                    str += '=';
                const bin = atob(str);
                const u8 = new Uint8Array(bin.length);
                for (let i = 0; i < bin.length; i++)
                    u8[i] = bin.charCodeAt(i);
                return u8;
            };
            const challenge = crypto.getRandomValues(new Uint8Array(32));
            const cred = await navigator.credentials.get({
                publicKey: {
                    challenge,
                    rpId,
                    allowCredentials: [{ type: 'public-key', id: b64UrlToBytes(credentialId) }],
                    userVerification: 'preferred',
                },
            });
            const resp = cred.response;
            return {
                id: cred.id,
                type: cred.type,
                hasClientData: resp.clientDataJSON.byteLength > 0,
                hasAuthData: resp.authenticatorData.byteLength > 0,
                hasSignature: resp.signature.byteLength > 0,
                authDataFlags: new Uint8Array(resp.authenticatorData)[32],
            };
        }";

        private const string AuthenticateErrorScript = @"async ({ rpId, credentialId }) => {
            const b64UrlToBytes = (s) => {
                let str = s.replace(/-/g, '+').replace(/_/g, '/');
                while (str.length % 4)
                    str += '=';
                const bin = atob(str);
                const u8 = new Uint8Array(bin.length);
                for (let i = 0; i < bin.length; i++)
                    u8[i] = bin.charCodeAt(i);
                return u8;
            };
            const challenge = crypto.getRandomValues(new Uint8Array(32));
            try {
                await navigator.credentials.get({
                    publicKey: {
                        challenge,
                        rpId,
                        allowCredentials: [{ type: 'public-key', id: b64UrlToBytes(credentialId) }],
                    },
                });
                return 'no-error';
            } catch (e) {
                return e.name;
            }
        }";

        private const string CreateResidentScript = @"async ({ rpId }) => {
            const challenge = crypto.getRandomValues(new Uint8Array(32));
            const created = await navigator.credentials.create({
                publicKey: {
                    challenge,
                    rp: { id: rpId, name: 'Test RP' },
                    user: { id: new Uint8Array([1, 2, 3, 4]), name: 'u', displayName: 'User' },
                    pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
                    authenticatorSelection: { residentKey: 'required', userVerification: 'preferred' },
                },
            });
            return created.id;
        }";

        private const string DiscoverableGetScript = @"async ({ rpId }) => {
            const challenge = crypto.getRandomValues(new Uint8Array(32));
            const cred = await navigator.credentials.get({
                publicKey: { challenge, rpId, userVerification: 'preferred' },
            });
            return cred.id;
        }";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static string Hostname => new Uri(Prefix).Host;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19858;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = Prefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-webauthn.spec.ts", "should not intercept navigator.credentials without install()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotInterceptNavigatorCredentialsWithoutInstall()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.CreateAsync(Hostname).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            bool intercepted = await page.EvaluateAsync<bool>("() => globalThis.__pwWebAuthnInstalled === true").ConfigureAwait(false);
            Assert.That(intercepted, Is.False);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-webauthn.spec.ts", "should seed a known credential and authenticate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSeedAKnownCredentialAndAuthenticate()
        {
            EnsureServer();
            IBrowserContext source = await _browser.NewContextAsync().ConfigureAwait(false);
            VirtualCredential known = await source.Credentials.CreateAsync(Hostname).ConfigureAwait(false);

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.CreateAsync(known.RpId, known.Id, known.UserHandle, known.PrivateKey, known.PublicKey).ConfigureAwait(false);
            await context.Credentials.InstallAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Dictionary<string, string> args = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rpId"] = Hostname,
                ["credentialId"] = known.Id,
            };
            JsonElement result = await page.EvaluateAsync<JsonElement>(AuthenticateScript, args).ConfigureAwait(false);
            Assert.That(result.GetProperty("id").GetString(), Is.EqualTo(known.Id));
            Assert.That(result.GetProperty("type").GetString(), Is.EqualTo("public-key"));
            Assert.That(result.GetProperty("hasClientData").GetBoolean(), Is.True);
            Assert.That(result.GetProperty("hasAuthData").GetBoolean(), Is.True);
            Assert.That(result.GetProperty("hasSignature").GetBoolean(), Is.True);
            Assert.That(result.GetProperty("authDataFlags").GetInt32() & 0x05, Is.EqualTo(0x05));

            await context.Credentials.DeleteAsync(known.Id).ConfigureAwait(false);
            Assert.That(await context.Credentials.GetAsync().ConfigureAwait(false), Is.Empty);

            string error = await page.EvaluateAsync<string>(AuthenticateErrorScript, args).ConfigureAwait(false);
            Assert.That(error, Is.EqualTo("NotAllowedError"));
            await context.CloseAsync().ConfigureAwait(false);
            await source.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-webauthn.spec.ts", "should capture a page-created credential and reuse it in another context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureAPageCreatedCredentialAndReuseItInAnotherContext()
        {
            EnsureServer();
            IBrowserContext setupContext = await _browser.NewContextAsync().ConfigureAwait(false);
            await setupContext.Credentials.InstallAsync().ConfigureAwait(false);
            IPage setupPage = await setupContext.NewPageAsync().ConfigureAwait(false);
            await setupPage.GoToAsync(EmptyPage).ConfigureAwait(false);

            Dictionary<string, string> rp = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rpId"] = Hostname,
            };
            string createdId = await setupPage.EvaluateAsync<string>(CreateResidentScript, rp).ConfigureAwait(false);

            IReadOnlyList<VirtualCredential> capturedList = await setupContext.Credentials.GetAsync(Hostname).ConfigureAwait(false);
            Assert.That(capturedList, Has.Exactly(1).Items);
            VirtualCredential captured = capturedList[0];
            Assert.That(captured.Id, Is.EqualTo(createdId));
            Assert.That(captured.PrivateKey, Does.Match("^[A-Za-z0-9_-]+$"));
            Assert.That(captured.PublicKey, Does.Match("^[A-Za-z0-9_-]+$"));

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.CreateAsync(captured.RpId, captured.Id, captured.UserHandle, captured.PrivateKey, captured.PublicKey).ConfigureAwait(false);
            await context.Credentials.InstallAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string gotId = await page.EvaluateAsync<string>(DiscoverableGetScript, rp).ConfigureAwait(false);
            Assert.That(gotId, Is.EqualTo(createdId));
            await context.CloseAsync().ConfigureAwait(false);
            await setupContext.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-webauthn.spec.ts", "should reuse a page-created credential via the storageState option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReuseAPageCreatedCredentialViaTheStorageStateOption()
        {
            EnsureServer();
            IBrowserContext setupContext = await _browser.NewContextAsync().ConfigureAwait(false);
            await setupContext.Credentials.InstallAsync().ConfigureAwait(false);
            IPage setupPage = await setupContext.NewPageAsync().ConfigureAwait(false);
            await setupPage.GoToAsync(EmptyPage).ConfigureAwait(false);

            Dictionary<string, string> rp = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rpId"] = Hostname,
            };
            string createdId = await setupPage.EvaluateAsync<string>(CreateResidentScript, rp).ConfigureAwait(false);

            string storageState = await setupContext.StorageStateAsync(true).ConfigureAwait(false);

            IBrowserContext context = await _browser.NewContextAsync(new() { StorageState = storageState }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string gotId = await page.EvaluateAsync<string>(DiscoverableGetScript, rp).ConfigureAwait(false);
            Assert.That(gotId, Is.EqualTo(createdId));
            await context.CloseAsync().ConfigureAwait(false);
            await setupContext.CloseAsync().ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
