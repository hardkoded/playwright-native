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
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/client-certificates.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextClientCertificatesTests</c>,
    /// <c>StandaloneApiClientCertificatesTests</c>,
    /// <c>LaunchPersistentClientCertificatesTests</c>, or
    /// <c>ClientCertificateTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryClientCertificatesParityTests : PageTestEx
    {
        private const string TrustedMessage = "Hello Alice, your certificate was issued by localhost!";
        private const string SelfSignedMessage = "Sorry Bob, certificates from Bob are not welcome here.";
        private const string MissingMessage = "Sorry, but you need to provide a client certificate to continue.";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 18711;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "validate input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchValidateInput()
        {
            foreach ((ClientCertificate[] certs, string expected) in ValidationCases())
            {
                Exception error = await CatchAsync(() => Playwright.APIRequest.NewContextAsync(new() { ClientCertificates = certs }))
                    .ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain(expected));
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "should fail with no client certificates provided")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldFailWithNoClientCertificatesProvided()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true })
                .ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(server.Url).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain(MissingMessage));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should keep supporting http")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldKeepSupportingHttp()
        {
            EnsureServer();
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(new Uri(Prefix).GetLeftPart(UriPartial.Authority)) } })
                .ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/one-style.html"));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("<div>hello, world!</div>"));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should throw with untrusted client certs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldThrowWithUntrustedClientCerts()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { SelfSigned(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(server.Url).ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(server.Url));
            Assert.That(response.Status, Is.EqualTo(403));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain(SelfSignedMessage));
        }

        [PlaywrightTest("client-certificates.spec.ts", "pass with trusted client certificates")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchPassWithTrustedClientCertificates()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(server.Url).ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(server.Url));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain(TrustedMessage));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should not leak client certificate to cross-origin redirect target")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldNotLeakClientCertificateToCrossOriginRedirectTarget()
        {
            await using OfficialClientCertificateServer target = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using OfficialHttpsRedirectServer redirect = await OfficialHttpsRedirectServer.StartAsync(target.Url)
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(redirect.Url)) } })
                .ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(redirect.Url).ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(target.Url));
            Assert.That(response.Status, Is.EqualTo(401));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("you need to provide a client certificate"));
        }

        [PlaywrightTest("client-certificates.spec.ts", "pass with trusted client certificates in pfx format")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchPassWithTrustedClientCertificatesInPfxFormat()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { TrustedPfx(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(server.Url).ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(server.Url));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain(TrustedMessage));
        }

        [PlaywrightTest("client-certificates.spec.ts", "pass with trusted client certificates and when a http proxy is used")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchPassWithTrustedClientCertificatesAndWhenAHttpProxyIsUsed()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using OfficialTestProxy proxyServer = new OfficialTestProxy();
            proxyServer.ForwardTo(server.Port, allowConnectRequests: true);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) }, Proxy = new Proxy { Server = "localhost:" + proxyServer.Port.ToString(CultureInfo.InvariantCulture) } })
                .ConfigureAwait(false);
            Assert.That(proxyServer.ConnectHosts, Is.Empty);
            IAPIResponse response = await request.GetAsync(server.Url).ConfigureAwait(false);
            Assert.That(proxyServer.ConnectHosts, Is.EqualTo(new[] { new Uri(server.Url).Authority }));
            Assert.That(response.Url, Is.EqualTo(server.Url));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain(TrustedMessage));
        }

        [PlaywrightTest("client-certificates.spec.ts", "pass with trusted client certificates and when a socks proxy is used")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchPassWithTrustedClientCertificatesAndWhenASocksProxyIsUsed()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using OfficialSocksForwardingProxy socks = new OfficialSocksForwardingProxy(server.Port, server.Port);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) }, Proxy = new Proxy { Server = socks.Server } })
                .ConfigureAwait(false);
            Assert.That(socks.ConnectHosts, Is.Empty);
            IAPIResponse response = await request.GetAsync(server.Url).ConfigureAwait(false);
            Assert.That(socks.ConnectHosts, Is.EqualTo(new[] { new Uri(server.Url).Authority }));
            Assert.That(response.Url, Is.EqualTo(server.Url));
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain(TrustedMessage));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should throw a http error if the pfx passphrase is incorect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldThrowAHttpErrorIfThePfxPassphraseIsIncorect()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    new ClientCertificate
                    {
                        Origin = OriginOf(server.Url),
                        PfxPath = Asset("client-certificates/client/trusted/cert.pfx"),
                        Passphrase = "this-password-is-incorrect",
                    },
                }
            })
                .ConfigureAwait(false);
            Exception error = await CatchAsync(() => request.GetAsync(server.Url)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("mac verify failure"));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should fail with matching certificates in legacy pfx format")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldFailWithMatchingCertificatesInLegacyPfxFormat()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { LegacyPfx(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            Exception error = await CatchAsync(() => request.GetAsync(server.Url)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Unsupported TLS certificate"));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should work in the browser with request interception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FetchShouldWorkInTheBrowserWithRequestInterception()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            await page.RouteAsync("**/*", async route =>
            {
                IAPIResponse response = await request.FetchAsync(route.Request).ConfigureAwait(false);
                await route.FulfillAsync(response).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "validate input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserValidateInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            foreach ((ClientCertificate[] certs, string expected) in ValidationCases())
            {
                Exception error = await CatchAsync(() => browser.NewContextAsync(new() { ClientCertificates = certs }))
                    .ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain(expected));
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "should keep supporting http")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldKeepSupportingHttp()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { ClientCertificates = new[] { Trusted(new Uri(Prefix).GetLeftPart(UriPartial.Authority)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("hello, world!")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 192, 203)")
                .ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass through to non-matching origin with self-signed cert")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassThroughToNonMatchingOriginWithSelfSignedCert()
        {
            await using OfficialPlaywrightTestHttpsServer https = await OfficialPlaywrightTestHttpsServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { ClientCertificates = new[] { Trusted("https://not-matching.com") } })
                .ConfigureAwait(false);
            await page.GoToAsync(https.Prefix + "/hello.html").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should not intercept TLS for origins without a client certificate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldNotInterceptTlsForOriginsWithoutAClientCertificate()
        {
            await using OfficialPlaywrightTestHttpsServer https = await OfficialPlaywrightTestHttpsServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { ClientCertificates = new[] { Trusted("https://not-matching.com") } })
                .ConfigureAwait(false);
            IResponse response = await page.GoToAsync(https.EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            ResponseSecurityDetailsResult details = await response.SecurityDetailsAsync().ConfigureAwait(false);
            Assert.That(details.SubjectName, Does.Contain("playwright-test"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should fail with no client certificates")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldFailWithNoClientCertificates()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted("https://not-matching.com") } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(MissingMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should fail with self-signed client certificates")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldFailWithSelfSignedClientCertificates()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { SelfSigned(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(SelfSignedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificates()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates when passing as content")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesWhenPassingAsContent()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    new ClientCertificate
                    {
                        Origin = OriginOf(server.Url),
                        Cert = File.ReadAllBytes(Asset("client-certificates/client/trusted/cert.pem")),
                        Key = File.ReadAllBytes(Asset("client-certificates/client/trusted/key.pem")),
                    },
                }
            })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates and when a http proxy is used")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesAndWhenAHttpProxyIsUsed()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using OfficialTestProxy proxyServer = new OfficialTestProxy();
            proxyServer.ForwardTo(server.Port, allowConnectRequests: true);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) }, Proxy = new Proxy { Server = "localhost:" + proxyServer.Port.ToString(CultureInfo.InvariantCulture) } })
                .ConfigureAwait(false);
            Assert.That(proxyServer.ConnectHosts, Is.Empty);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            Assert.That(proxyServer.ConnectHosts.Distinct().ToArray(), Is.EqualTo(new[] { "127.0.0.1:" + server.Port.ToString(CultureInfo.InvariantCulture) }));
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates and when a http proxy is used from env")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesAndWhenAHttpProxyIsUsedFromEnv()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using OfficialTestProxy proxyServer = new OfficialTestProxy();
            proxyServer.ForwardTo(server.Port, allowConnectRequests: true);
            string previous = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://localhost:" + proxyServer.Port.ToString(CultureInfo.InvariantCulture));
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) } })
                    .ConfigureAwait(false);
                proxyServer.ConnectHosts = Array.Empty<string>();
                await page.GoToAsync(server.Url).ConfigureAwait(false);
                Assert.That(
                    proxyServer.ConnectHosts.Where(host => host.StartsWith("127.0.0.1:", StringComparison.Ordinal)).Distinct().ToArray(),
                    Is.EqualTo(new[] { "127.0.0.1:" + server.Port.ToString(CultureInfo.InvariantCulture) }));
                await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                Environment.SetEnvironmentVariable("HTTPS_PROXY", previous);
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates and when a http proxy is used from config but env is there")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesAndWhenAHttpProxyIsUsedFromConfigButEnvIsThere()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using OfficialTestProxy proxyServer = new OfficialTestProxy();
            proxyServer.ForwardTo(server.Port, allowConnectRequests: true);
            string previous = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://this-should-not-taken-into-account:4242");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) }, Proxy = new Proxy { Server = "localhost:" + proxyServer.Port.ToString(CultureInfo.InvariantCulture) } })
                    .ConfigureAwait(false);
                Assert.That(proxyServer.ConnectHosts, Is.Empty);
                await page.GoToAsync(server.Url).ConfigureAwait(false);
                Assert.That(proxyServer.ConnectHosts.Distinct().ToArray(), Is.EqualTo(new[] { "127.0.0.1:" + server.Port.ToString(CultureInfo.InvariantCulture) }));
                await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                Environment.SetEnvironmentVariable("HTTPS_PROXY", previous);
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates and when a socks proxy is used")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesAndWhenASocksProxyIsUsed()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using OfficialSocksForwardingProxy socks = new OfficialSocksForwardingProxy(server.Port, server.Port);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) }, Proxy = new Proxy { Server = socks.Server } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            Assert.That(
                socks.ConnectHosts.Distinct().ToArray(),
                Is.EqualTo(new[] { "127.0.0.1:" + server.Port.ToString(CultureInfo.InvariantCulture) }));
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should not hang on tls errors during TLS 1.2 handshake")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldNotHangOnTlsErrorsDuringTls12Handshake()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            foreach (SslProtocols version in new[] { SslProtocols.Tls13, SslProtocols.Tls12 })
            {
                await using OfficialTlsSniRejectServer server = OfficialTlsSniRejectServer.Start(version);
                IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { SelfSigned(OriginOf(server.Url)) } })
                    .ConfigureAwait(false);
                await page.GoToAsync(server.Url).ConfigureAwait(false);
                await Assertions.Expect(page.GetByText(
                    "Playwright client-certificate error: Client network socket disconnected before secure TLS connection was established"))
                    .ToBeVisibleAsync().ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates in pfx format")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesInPfxFormat()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { TrustedPfx(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should handle TLS renegotiation with client certificates")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldHandleTlsRenegotiationWithClientCertificates()
        {
            await using OfficialTlsRenegotiationServer server = OfficialTlsRenegotiationServer.Start();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(server.Url) } })
                .ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            string response = await page.EvaluateAsync<string>(@"async () => {
                const response = await fetch('/from-fetch-api', {
                  method: 'POST',
                  body: 'client-request-payload'
                });
                return await response.text();
            }").ConfigureAwait(false);
            Assert.That(response, Is.EqualTo(string.Join("\n", new[]
            {
                "server received: client-request-payload",
                "0-from-server",
                "1-from-server",
                "2-from-server",
                "3-from-server",
                "server closed the connection",
            })));
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await page.SetContentAsync("<button>Click me</button><link rel=\"stylesheet\" href=\"/style.css\">")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button")).ToHaveCSSAsync("background-color", "rgb(255, 0, 0)")
                .ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates in pfx format when passing as content")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesInPfxFormatWhenPassingAsContent()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    new ClientCertificate
                    {
                        Origin = OriginOf(server.Url),
                        Pfx = File.ReadAllBytes(Asset("client-certificates/client/trusted/cert.pfx")),
                        Passphrase = "secure",
                    },
                }
            })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should fail with matching certificates in legacy pfx format")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldFailWithMatchingCertificatesInLegacyPfxFormat()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { LegacyPfx(OriginOf(server.Url)) } }))
                .ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Unsupported TLS certificate"));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should throw a http error if the pfx passphrase is incorect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldThrowAHttpErrorIfThePfxPassphraseIsIncorect()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => browser.NewPageAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    new ClientCertificate
                    {
                        Origin = OriginOf(server.Url),
                        PfxPath = Asset("client-certificates/client/trusted/cert.pfx"),
                        Passphrase = "this-password-is-incorrect",
                    },
                }
            }))
                .ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Failed to load client certificate: mac verify failure"));
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates on context APIRequestContext instance")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesOnContextApiRequestContextInstance()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            string origin = OriginOf(server.Url);
            IPage page = await browser.NewPageAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    Trusted(origin),
                    Trusted(origin.Replace("localhost", "127.0.0.1", StringComparison.Ordinal)),
                }
            })
                .ConfigureAwait(false);
            foreach (string url in new[] { server.Url, server.Url.Replace("localhost", "127.0.0.1", StringComparison.Ordinal) })
            {
                IAPIResponse response = await page.APIRequest.GetAsync(url).ConfigureAwait(false);
                Assert.That(response.Status, Is.EqualTo(200));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain(TrustedMessage));
            }

            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates and trailing slash")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldPassWithMatchingCertificatesAndTrailingSlash()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(server.Url) } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByText(TrustedMessage)).ToBeVisibleAsync().ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should have ignoreHTTPSErrors=false by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldHaveIgnoreHttpsErrorsFalseByDefault()
        {
            await using OfficialPlaywrightTestHttpsServer https = await OfficialPlaywrightTestHttpsServer.StartAsync()
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { ClientCertificates = new[] { Trusted(OriginOf(https.EmptyPage)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(https.EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Playwright client-certificate error: self-signed certificate"))
                .ToBeVisibleAsync().ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "support http2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserSupportHttp2()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync(http2: true)
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url.Replace("127.0.0.1", "local.playwright", StringComparison.Ordinal))
                .ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(MissingMessage).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("alpn-protocol")).ToHaveTextAsync("h2").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("servername")).ToHaveTextAsync("local.playwright").ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("alpn-protocol")).ToHaveTextAsync("h2").ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "support http2 if the browser only supports http1.1")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserSupportHttp2IfTheBrowserOnlySupportsHttp11()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("official skip: browserName !== chromium");
            }

            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync(http2: true, enableHttp1Fallback: true)
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = new[] { "--disable-http2" },
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true, ClientCertificates = new[] { Trusted(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url.Replace("127.0.0.1", "local.playwright", StringComparison.Ordinal))
                .ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(MissingMessage).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("alpn-protocol")).ToHaveTextAsync("http/1.1").ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("alpn-protocol")).ToHaveTextAsync("http/1.1").ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should return target connection errors when using http2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldReturnTargetConnectionErrorsWhenUsingHttp2()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync(http2: true)
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { ClientCertificates = new[] { Trusted(OriginOf(server.Url)) } })
                .ConfigureAwait(false);
            await page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Playwright client-certificate error: self-signed certificate"))
                .ToBeVisibleAsync().ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("client-certificates.spec.ts", "should handle rejected certificate in handshake with HTTP/2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BrowserShouldHandleRejectedCertificateInHandshakeWithHttp2()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync(http2: true)
                .ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[]
                {
                    Trusted("https://just-there-that-the-client-certificates-proxy-server-is-getting-launched.com"),
                }
            })
                .ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            try
            {
                await page.GoToAsync(server.Url.Replace("127.0.0.1", "localhost", StringComparison.Ordinal))
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "validate input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PersistentContextValidateInput()
        {
            foreach ((ClientCertificate[] certs, string expected) in ValidationCases())
            {
                Exception error = await CatchAsync(() => LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
                {
                    ClientCertificates = certs,
                })).ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain(expected));
            }
        }

        [PlaywrightTest("client-certificates.spec.ts", "should pass with matching certificates")]
        [Test]
        [Timeout(60_000)]
        public async Task PersistentContextShouldPassWithMatchingCertificates()
        {
            await using OfficialClientCertificateServer server = await OfficialClientCertificateServer.StartAsync()
                .ConfigureAwait(false);
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                IgnoreHTTPSErrors = true,
                ClientCertificates = new[] { Trusted(OriginOf(server.Url)) },
            }).ConfigureAwait(false);
            await launch.Page.GoToAsync(server.Url).ConfigureAwait(false);
            await Assertions.Expect(launch.Page.GetByTestId("message")).ToHaveTextAsync(TrustedMessage)
                .ConfigureAwait(false);
        }

        private static IEnumerable<(ClientCertificate[] Certs, string Expected)> ValidationCases()
        {
            yield return (new[] { new ClientCertificate { Origin = "test" } }, "None of cert, key, passphrase or pfx is specified");
            yield return (
                new[]
                {
                    new ClientCertificate
                    {
                        Origin = "test",
                        CertPath = DummyFile(),
                        KeyPath = DummyFile(),
                        PfxPath = DummyFile(),
                        Passphrase = DummyFile(),
                    },
                },
                "pfx is specified together with cert, key or passphrase");
        }

        private static string DummyFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "pwsharp-cc-dummy-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(path, "dummy");
            return path;
        }

        private static ClientCertificate Trusted(string origin) => new()
        {
            Origin = origin,
            CertPath = Asset("client-certificates/client/trusted/cert.pem"),
            KeyPath = Asset("client-certificates/client/trusted/key.pem"),
        };

        private static ClientCertificate SelfSigned(string origin) => new()
        {
            Origin = origin,
            CertPath = Asset("client-certificates/client/self-signed/cert.pem"),
            KeyPath = Asset("client-certificates/client/self-signed/key.pem"),
        };

        private static ClientCertificate TrustedPfx(string origin) => new()
        {
            Origin = origin,
            PfxPath = Asset("client-certificates/client/trusted/cert.pfx"),
            Passphrase = "secure",
        };

        private static ClientCertificate LegacyPfx(string origin) => new()
        {
            Origin = origin,
            PfxPath = Asset("client-certificates/client/trusted/cert-legacy.pfx"),
            Passphrase = "secure",
        };

        private static string Asset(string relative) => OfficialClientCertificateServer.Asset(relative);

        private static string OriginOf(string url) => new Uri(url).GetLeftPart(UriPartial.Authority);

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static async Task<PersistentLaunch> LaunchPersistentAsync(BrowserTypeLaunchPersistentContextOptions options)
        {
            options ??= new BrowserTypeLaunchPersistentContextOptions();
            options.Headless = true;
            IBrowserType browserType;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                options.ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return null;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                options.ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-cc-persist-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, options)
                .ConfigureAwait(false);
            IPage page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync().ConfigureAwait(false);
            return new PersistentLaunch(context, page, userDataDir);
        }

        private sealed class PersistentLaunch : IAsyncDisposable
        {
            private readonly string _userDataDir;

            internal PersistentLaunch(IBrowserContext context, IPage page, string userDataDir)
            {
                Context = context;
                Page = page;
                _userDataDir = userDataDir;
            }

            internal IBrowserContext Context { get; }

            internal IPage Page { get; }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await Context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                try
                {
                    if (Directory.Exists(_userDataDir))
                    {
                        Directory.Delete(_userDataDir, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
