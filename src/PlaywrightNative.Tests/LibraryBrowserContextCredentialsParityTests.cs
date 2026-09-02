/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-credentials.spec.ts</c> parity.
    /// Official <c>failsOn401</c> is Chromium that is not headless-shell.
    /// This suite uses regular headless Chrome, so Chromium navigations without
    /// valid credentials throw <c>net::ERR_INVALID_AUTH_CREDENTIALS</c>.
    /// Do not edit leftover <c>ContextAuthTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextCredentialsParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static int ServerPort = TestConstants.Port;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static bool FailsOn401 => TestConstants.IsChromium;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19835;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    Prefix = "http://localhost:" + portText;
                    EmptyPage = Prefix + "/empty.html";
                    CrossProcessPrefix = "http://127.0.0.1:" + portText;
                    ServerPort = port;
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
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                ServerPort = TestConstants.Port;
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
            if (_browser == null || !_browser.IsConnected)
            {
                if (_browser != null)
                {
                    await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                }

                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }

            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should fail without credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWithoutCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertUnauthorizedAsync(page, EmptyPage).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should work with setHTTPCredentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithSetHTTPCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertUnauthorizedAsync(page, EmptyPage).ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new HttpCredentials { Username = "user", Password = "pass" }).ConfigureAwait(false);
            IResponse reloaded = await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(reloaded.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should work with setHTTPCredentials and multiple credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithSetHTTPCredentialsAndMultipleCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user1", "pass1");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new[]
            {
                new HttpCredentials { Username = "user1", Password = "pass1", Origin = Prefix },
                new HttpCredentials { Username = "user2", Password = "pass2", Origin = CrossProcessPrefix },
            }).ConfigureAwait(false);
            IResponse response1 = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(200));
            IResponse response2 = await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(401));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should work with correct credentials @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCorrectCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should fail with wrong credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWithWrongCredentials()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "foo", Password = "bar" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should return resource body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnResourceBody()
        {
            EnsureServer();
            Server.SetAuth("/playground.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/playground.html").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Playground"));
            Assert.That(System.Text.Encoding.UTF8.GetString(await response.GetBodyAsync().ConfigureAwait(false)), Does.Contain("Playground"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should work with a single credential in an array")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithASingleCredentialInAnArray()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new[]
            {
                new HttpCredentials { Username = "user", Password = "pass" },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should work with multiple credentials for different origins")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithMultipleCredentialsForDifferentOrigins()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user1", "pass1");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new[]
            {
                new HttpCredentials { Username = "user1", Password = "pass1", Origin = Prefix },
                new HttpCredentials { Username = "user2", Password = "pass2", Origin = CrossProcessPrefix },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response1 = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(200));
            IResponse response2 = await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(401));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should fall back to credentials without origin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFallBackToCredentialsWithoutOrigin()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new[]
            {
                new HttpCredentials { Username = "user2", Password = "pass2", Origin = CrossProcessPrefix },
                new HttpCredentials { Username = "user", Password = "pass" },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response1 = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response1.Status, Is.EqualTo(200));
            IResponse response2 = await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(response2.Status, Is.EqualTo(401));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should use the first matching credential")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseTheFirstMatchingCredential()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            await context.SetHttpCredentialsAsync(new[]
            {
                new HttpCredentials { Username = "wrong", Password = "wrong" },
                new HttpCredentials { Username = "user", Password = "pass", Origin = Prefix },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(401));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should work with correct credentials and matching origin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCorrectCredentialsAndMatchingOrigin()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = Prefix } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should work with correct credentials and matching origin case insensitive")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCorrectCredentialsAndMatchingOriginCaseInsensitive()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = Prefix.ToUpperInvariant() } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should fail with correct credentials and mismatching scheme")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWithCorrectCredentialsAndMismatchingScheme()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "user",
                    Password = "pass",
                    Origin = Prefix.Replace("http://", "https://", StringComparison.Ordinal),
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertUnauthorizedAsync(page, EmptyPage).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should fail with correct credentials and mismatching hostname")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWithCorrectCredentialsAndMismatchingHostname()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            string hostname = new Uri(Prefix).Host;
            string origin = Prefix.Replace(hostname, "mismatching-hostname", StringComparison.Ordinal);
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = origin } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertUnauthorizedAsync(page, EmptyPage).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should fail with correct credentials and mismatching port")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWithCorrectCredentialsAndMismatchingPort()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            string origin = Prefix.Replace(
                ServerPort.ToString(CultureInfo.InvariantCulture),
                (ServerPort + 1).ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass", Origin = origin } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertUnauthorizedAsync(page, EmptyPage).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "should not override Authorization header set by the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotOverrideAuthorizationHeaderSetByThePage()
        {
            EnsureServer();
            Server.SetAuth("/empty.html", "user", "pass");
            Server.SetRoute("/echo-auth", http =>
            {
                string authorization = http.Request.Headers["Authorization"].ToString();
                return http.Response.WriteAsync(string.IsNullOrEmpty(authorization) ? "<none>" : authorization);
            });
            IBrowserContext context = await _browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            string received = await page.EvaluateAsync<string>(@"(async () => {
                const response = await fetch('/echo-auth', { headers: { 'Authorization': 'Bearer my-own-app-token' } });
                return await response.text();
            })()").ConfigureAwait(false);
            Assert.That(received, Is.EqualTo("Bearer my-own-app-token"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task AssertUnauthorizedAsync(IPage page, string url)
        {
            try
            {
                IResponse response = await page.GoToAsync(url).ConfigureAwait(false);
                if (FailsOn401)
                {
                    Assert.Fail("expected net::ERR_INVALID_AUTH_CREDENTIALS, got status " + response?.Status);
                }

                Assert.That(response, Is.Not.Null);
                Assert.That(response.Status, Is.EqualTo(401));
            }
            catch (Exception ex)
            {
                if (!FailsOn401)
                {
                    throw;
                }

                Assert.That(ex.Message, Does.Contain("net::ERR_INVALID_AUTH_CREDENTIALS"));
            }
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new System.Collections.Generic.List<IBrowserContext>(_browser.Contexts))
            {
                try
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
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
