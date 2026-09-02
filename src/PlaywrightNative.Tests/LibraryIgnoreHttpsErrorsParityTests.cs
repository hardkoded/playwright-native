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
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/ignorehttpserrors.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextScriptHttpsTests</c>,
    /// <c>LaunchPersistentIgnoreHttpsErrorsTests</c>, or leftover
    /// <c>ApiRequestTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryIgnoreHttpsErrorsParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        private static bool IsLinux => !TestConstants.IsWindows && !TestConstants.IsMacOSX;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            await StartOwnedHttpAsync(contentRoot).ConfigureAwait(false);
            await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
            if (Server == null && TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
            }

            if (HttpsServer == null && TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsEmptyPage = HttpsPrefix + "/empty.html";
            }

            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
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

            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
                _ownedHttps = null;
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

            Server?.Reset();
            HttpsServer?.Reset();
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            HttpsServer?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("ignorehttpserrors.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = null;
            IResponse response = null;
            try
            {
                response = await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Null);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Ok, Is.True);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("ignorehttpserrors.spec.ts", "should isolate contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateContexts()
        {
            EnsureHttps();
            {
                IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Exception error = null;
                IResponse response = null;
                try
                {
                    response = await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                Assert.That(error, Is.Null);
                Assert.That(response, Is.Not.Null);
                Assert.That(response.Ok, Is.True);
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Exception error = await CatchAsync(() => page.GoToAsync(HttpsEmptyPage)).ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("ignorehttpserrors.spec.ts", "should isolated contexts that share network process")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolatedContextsThatShareNetworkProcess()
        {
            EnsureHttps();
            if (TestConstants.IsWebKit && IsLinux)
            {
                Assert.Ignore("See https://bugs.webkit.org/show_bug.cgi?id=293148");
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                IResponse response = await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Exception error = await CatchAsync(() => page.GoToAsync(HttpsEmptyPage)).ConfigureAwait(false);
                Assert.That(error, Is.Not.Null, "A TLS error expected, but the request succeeded.");
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("ignorehttpserrors.spec.ts", "should work with mixed content")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithMixedContent()
        {
            EnsureHttps();
            EnsureServer();
            HttpsServer.SetRoute("/mixedcontent.html", http =>
            {
                return http.Response.WriteAsync("<iframe src=" + EmptyPage + "></iframe>");
            });
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(HttpsPrefix + "/mixedcontent.html", WaitUntilState.Load).ConfigureAwait(false);
            List<IFrame> frames = new(page.Frames);
            Assert.That(frames.Count, Is.EqualTo(2));
            Assert.That(await frames[0].EvaluateAsync<int>("1 + 2").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await frames[1].EvaluateAsync<int>("2 + 3").ConfigureAwait(false), Is.EqualTo(5));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("ignorehttpserrors.spec.ts", "should work with WebSocket")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithWebSocket()
        {
            EnsureHttps();
            HttpsServer.SendOnWebSocketConnection("incoming");
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string endpoint = HttpsPrefix.Replace("https", "wss", StringComparison.Ordinal) + "/ws";
            string value = await page.EvaluateAsync<string>(
                @"endpoint => {
                    let cb;
                    const result = new Promise(f => cb = f);
                    const ws = new WebSocket(endpoint);
                    ws.addEventListener('message', data => { ws.close(); cb(data.data); });
                    ws.addEventListener('error', error => cb('Error'));
                    return result;
                }",
                endpoint).ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("incoming"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("ignorehttpserrors.spec.ts", "should fail with WebSocket if not ignored")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWithWebSocketIfNotIgnored()
        {
            EnsureHttps();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string endpoint = HttpsPrefix.Replace("https", "wss", StringComparison.Ordinal) + "/ws";
            string value = await page.EvaluateAsync<string>(
                @"endpoint => {
                    let cb;
                    const result = new Promise(f => cb = f);
                    const ws = new WebSocket(endpoint);
                    ws.addEventListener('message', data => { ws.close(); cb(data.data); });
                    ws.addEventListener('error', error => cb('Error'));
                    return result;
                }",
                endpoint).ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("Error"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("ignorehttpserrors.spec.ts", "serviceWorker should intercept document request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ServiceWorkerShouldInterceptDocumentRequest()
        {
            EnsureHttps();
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Failed to register a ServiceWorker: An SSL certificate error occurred when fetching the script.");
            }

            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            HttpsServer.SetRoute("/sw.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync(
                    "self.addEventListener('fetch', event => {\n" +
                    "  event.respondWith(new Response('intercepted'));\n" +
                    "});\n" +
                    "self.addEventListener('activate', event => {\n" +
                    "  event.waitUntil(clients.claim());\n" +
                    "});\n");
            });
            await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async () => {
                    const waitForControllerChange = new Promise(resolve => navigator.serviceWorker.oncontrollerchange = resolve);
                    await navigator.serviceWorker.register('/sw.js');
                    await waitForControllerChange;
                }").ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(await page.TextContentAsync("body").ConfigureAwait(false), Is.EqualTo("intercepted"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task StartOwnedHttpAsync(string contentRoot)
        {
            int basePort = 19963;
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
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsEmptyPage = HttpsPrefix + "/empty.html";
                return;
            }

            string certPath = EnsureTestCertificate(contentRoot);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19983;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
                    await https.StartAsync().ConfigureAwait(false);
                    _ownedHttps = https;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    HttpsPrefix = "https://localhost:" + portText;
                    HttpsEmptyPage = HttpsPrefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static string EnsureTestCertificate(string contentRoot)
        {
            string certPath = Path.Combine(contentRoot, "key.pfx");
            if (File.Exists(certPath))
            {
                return certPath;
            }

            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
            using X509Certificate2 cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));
            File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, "playwright"));
            return certPath;
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
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
