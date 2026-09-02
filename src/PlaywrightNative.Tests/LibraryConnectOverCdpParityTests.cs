/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/chromium/connect-over-cdp.spec.ts</c> parity.
    /// Chromium-only. Do not edit leftover <c>ConnectOverCdpTests</c>.
    /// Official Node-only skips: <c>toImpl</c> artifacts cleanup, <c>isLocal</c>,
    /// utility-world reuse, and in-process <c>ConnectionTransport</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryConnectOverCdpParityTests : PageTestEx
    {
        private static SimpleServer _ownedHttps;

        private static string _httpsPrefix = TestConstants.HttpsPrefix;

        private static string _httpProxyAtStart;

        private static SimpleServer Server => TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        private static string Prefix => TestConstants.ServerUrl;

        private static string HttpsPrefix => _httpsPrefix;

        [OneTimeSetUp]
        public async Task StartOwnedHttpsAsync()
        {
            _httpProxyAtStart = Environment.GetEnvironmentVariable("HTTP_PROXY");
            if (TestServerSetup.HttpsServer != null)
            {
                _httpsPrefix = TestConstants.HttpsPrefix;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            string certPath = Path.Combine(contentRoot, "key.pfx");
            if (!File.Exists(certPath))
            {
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
            }

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
                    _httpsPrefix = "https://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedHttpsAsync()
        {
            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }
        }

        [SetUp]
        public void SkipNonChromiumConnectOverCdp()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only connect-over-cdp.spec.ts.");
            }
        }

        [TearDown]
        public void ResetServer()
        {
            Server?.Reset();
            HttpsServer?.Reset();
            if (string.IsNullOrEmpty(_httpProxyAtStart))
            {
                Environment.SetEnvironmentVariable("HTTP_PROXY", null);
                Environment.SetEnvironmentVariable("http_proxy", null);
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect to an existing cdp session")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectToAnExistingCdpSession()
        {
            await WithHostAsync(async (host, endpoint) =>
            {
                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should cleanup artifacts dir after connectOverCDP disconnects due to ws close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldCleanupArtifactsDirAfterConnectOverCdpDisconnectsDueToWsClose()
        {
            Assert.Ignore("Official Node-only toImpl(cdpBrowser).options.artifactsDir.");
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should write traces to provided artifactsDir on connectOverCDP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWriteTracesToProvidedArtifactsDirOnConnectOverCdp()
        {
            string artifactsDir = Path.Combine(Path.GetTempPath(), "pwsharp-cdp-art-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(artifactsDir);
            try
            {
                await WithHostAsync(async (host, endpoint) =>
                {
                    await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint, new() { ArtifactsDir = artifactsDir }).ConfigureAwait(false);
                    Assert.That(((IHasTracesDir)cdpBrowser).TracesDir, Is.EqualTo(artifactsDir));
                    IBrowserContext context = FirstContext(cdpBrowser);
                    Assert.That(((ChromiumBrowserContext)context).DownloadsPath, Is.EqualTo(Path.GetFullPath(artifactsDir)));
                    await context.Tracing.StartAsync(new TracingStartOptions
                    {
                        Name = "cdp-trace",
                        Snapshots = true,
                        Screenshots = true,
                    }).ConfigureAwait(false);
                    IPage page = await context.NewPageAsync().ConfigureAwait(false);
                    await page.SetContentAsync("<button>Hello</button>").ConfigureAwait(false);
                    await context.Tracing.StopChunkAsync().ConfigureAwait(false);
                    Assert.That(File.Exists(Path.Combine(artifactsDir, "cdp-trace.trace")), Is.True);
                    Assert.That(File.Exists(Path.Combine(artifactsDir, "cdp-trace.network")), Is.True);
                    Assert.That(Directory.Exists(Path.Combine(artifactsDir, "resources")), Is.True);
                    await cdpBrowser.CloseAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
                Assert.That(Directory.Exists(artifactsDir), Is.True);
            }
            finally
            {
                try
                {
                    Directory.Delete(artifactsDir, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connectOverCDP and manage downloads in default context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectOverCdpAndManageDownloadsInDefaultContext()
        {
            EnsureServer();
            Server.SetRoute("/downloadWithFilename", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=file.txt";
                return http.Response.WriteAsync("Hello world");
            });

            await WithHostAsync(async (_, endpoint) =>
            {
                await using IBrowser browser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                IPage page = await FirstContext(browser).NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/downloadWithFilename\">download</a>").ConfigureAwait(false);
                IDownload download = await page.RunAndWaitForDownloadAsync(() => page.ClickAsync("a")).ConfigureAwait(false);
                Assert.That(download.Page, Is.EqualTo(page));
                Assert.That(download.Url, Is.EqualTo(Prefix + "/downloadWithFilename"));
                Assert.That(download.SuggestedFilename, Is.EqualTo("file.txt"));
                string userPath = Path.Combine(Path.GetTempPath(), "pwsharp-cdp-dl-" + Guid.NewGuid().ToString("N") + ".txt");
                await download.SaveAsAsync(userPath).ConfigureAwait(false);
                Assert.That(File.Exists(userPath), Is.True);
                Assert.That(await File.ReadAllTextAsync(userPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect to an existing cdp session twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectToAnExistingCdpSessionTwice()
        {
            EnsureServer();
            await WithHostAsync(async (_, endpoint) =>
            {
                IBrowser cdpBrowser1 = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                IBrowser cdpBrowser2 = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                try
                {
                    Assert.That(cdpBrowser1.Contexts.Count, Is.EqualTo(1));
                    IBrowserContext context1 = FirstContext(cdpBrowser1);
                    await CloseLeftoverPagesAsync(context1).ConfigureAwait(false);
                    IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                    await page1.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

                    Assert.That(cdpBrowser2.Contexts.Count, Is.EqualTo(1));
                    IBrowserContext context2 = FirstContext(cdpBrowser2);
                    IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
                    await page2.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

                    Assert.That(context1.Pages.Count, Is.EqualTo(2));
                    Assert.That(context2.Pages.Count, Is.EqualTo(2));
                }
                finally
                {
                    await cdpBrowser1.CloseAsync().ConfigureAwait(false);
                    await cdpBrowser2.CloseAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect to existing page with iframe and navigate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectToExistingPageWithIframeAndNavigate()
        {
            EnsureServer();
            await WithHostAsync(async (host, endpoint) =>
            {
                IBrowserContext hostContext = await host.NewContextAsync().ConfigureAwait(false);
                IPage hostPage = await hostContext.NewPageAsync().ConfigureAwait(false);
                await hostPage.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);

                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                await FirstPage(FirstContext(cdpBrowser)).GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect to existing service workers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectToExistingServiceWorkers()
        {
            EnsureServer();
            await WithHostAsync(async (_, endpoint) =>
            {
                string noSlash = endpoint.TrimEnd('/');
                IBrowser cdpBrowser1 = await Playwright.Chromium.ConnectOverCDPAsync(noSlash).ConfigureAwait(false);
                try
                {
                    IBrowserContext context = FirstContext(cdpBrowser1);
                    IPage page = await context.NewPageAsync().ConfigureAwait(false);
                    Task<IWorker> workerTask = context.WaitForEventAsync(BrowserContextEvent.ServiceWorker);
                    await page.GoToAsync(Prefix + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
                    IWorker worker = await workerTask.ConfigureAwait(false);
                    Assert.That(await worker.EvaluateAsync<string>("() => self.toString()").ConfigureAwait(false), Is.EqualTo("[object ServiceWorkerGlobalScope]"));
                }
                finally
                {
                    await cdpBrowser1.CloseAsync().ConfigureAwait(false);
                }

                await using IBrowser cdpBrowser2 = await Playwright.Chromium.ConnectOverCDPAsync(noSlash).ConfigureAwait(false);
                Assert.That(FirstContext(cdpBrowser2).ServiceWorkers().Count, Is.EqualTo(1));
                await cdpBrowser2.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect over a ws endpoint")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectOverAWsEndpoint()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                string json = await new HttpClient().GetStringAsync(new Uri(endpoint + "json/version/")).ConfigureAwait(false);
                string ws = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("webSocketDebuggerUrl").GetString();
                IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(ws).ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                await cdpBrowser.CloseAsync().ConfigureAwait(false);

                IBrowser cdpBrowser2 = await Playwright.Chromium.ConnectOverCDPAsync(ws).ConfigureAwait(false);
                Assert.That(cdpBrowser2.Contexts.Count, Is.EqualTo(1));
                await cdpBrowser2.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should send extra headers with connect request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendExtraHeadersWithConnectRequest()
        {
            EnsureServer();
            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("User-Agent", "Playwright"),
                new KeyValuePair<string, string>("foo", "bar"),
            };
            {
                Task<HttpRequest> requestTask = Server.WaitForWebSocketConnectionRequest();
                Task connectTask = SwallowConnectAsync(
                    Playwright.Chromium.ConnectOverCDPAsync("ws://localhost:" + TestConstants.Port + "/ws", new() { Timeout = 100, Headers = headers }));
                HttpRequest request = await requestTask.ConfigureAwait(false);
                Assert.That(request.Headers["user-agent"].ToString(), Is.EqualTo("Playwright"));
                Assert.That(request.Headers["foo"].ToString(), Is.EqualTo("bar"));
                await connectTask.ConfigureAwait(false);
            }

            {
                Task<HttpRequest> requestTask = Server.WaitForRequest("/json/version/", r => r);
                Task connectTask = SwallowConnectAsync(
                    Playwright.Chromium.ConnectOverCDPAsync("http://localhost:" + TestConstants.Port, new() { Timeout = 100, Headers = headers }));
                HttpRequest request = await requestTask.ConfigureAwait(false);
                Assert.That(request.Headers["user-agent"].ToString(), Is.EqualTo("Playwright"));
                Assert.That(request.Headers["foo"].ToString(), Is.EqualTo("bar"));
                await connectTask.ConfigureAwait(false);
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should keep URL parameters when adding json/version")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepUrlParametersWhenAddingJsonVersion()
        {
            EnsureServer();
            Task wait = Server.WaitForRequest("/browser/json/version/?foo=bar");
            Task connect = SwallowConnectAsync(
                Playwright.Chromium.ConnectOverCDPAsync("http://localhost:" + TestConstants.Port + "/browser/?foo=bar"));
            await Task.WhenAll(wait, connect).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should append /json/version with a slash if there isnt one")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAppendJsonVersionWithASlashIfThereIsntOne()
        {
            EnsureServer();
            Task wait = Server.WaitForRequest("/browser/json/version/?foo=bar");
            Task connect = SwallowConnectAsync(
                Playwright.Chromium.ConnectOverCDPAsync("http://localhost:" + TestConstants.Port + "/browser?foo=bar"));
            await Task.WhenAll(wait, connect).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should send default User-Agent header with connect request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendDefaultUserAgentHeaderWithConnectRequest()
        {
            EnsureServer();
            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("foo", "bar"),
            };
            Task<HttpRequest> requestTask = Server.WaitForWebSocketConnectionRequest();
            Task connectTask = SwallowConnectAsync(
                Playwright.Chromium.ConnectOverCDPAsync("ws://localhost:" + TestConstants.Port + "/ws", new() { Timeout = 100, Headers = headers }));
            HttpRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Headers["user-agent"].ToString(), Is.EqualTo(PlaywrightUserAgent.GetUserAgent()));
            Assert.That(request.Headers["foo"].ToString(), Is.EqualTo("bar"));
            await connectTask.ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should report all pages in an existing browser")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportAllPagesInAnExistingBrowser()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                IBrowserContext context = FirstContext(cdpBrowser);
                await CloseLeftoverPagesAsync(context).ConfigureAwait(false);
                for (int i = 0; i < 3; i++)
                {
                    await context.NewPageAsync().ConfigureAwait(false);
                }

                await cdpBrowser.CloseAsync().ConfigureAwait(false);

                await using IBrowser cdpBrowser2 = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                Assert.That(FirstContext(cdpBrowser2).Pages.Count, Is.EqualTo(3));
                await cdpBrowser2.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect via https")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectViaHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }

            await WithHostAsync(async (_, endpoint) =>
            {
                string json = await new HttpClient().GetStringAsync(new Uri(endpoint + "json/version/")).ConfigureAwait(false);
                HttpsServer.SetRoute("/json/version/", http =>
                {
                    http.Response.StatusCode = 200;
                    return http.Response.WriteAsync(json);
                });
                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(HttpsPrefix + "/").ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                IBrowserContext context = FirstContext(cdpBrowser);
                for (int i = 0; i < 3; i++)
                {
                    await context.NewPageAsync().ConfigureAwait(false);
                }

                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should return valid browser from context.browser()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnValidBrowserFromContextBrowser()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                Assert.That(FirstContext(cdpBrowser).Browser, Is.EqualTo(cdpBrowser));
                IBrowserContext context2 = await cdpBrowser.NewContextAsync().ConfigureAwait(false);
                Assert.That(context2.Browser, Is.EqualTo(cdpBrowser));
                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should report an expected error when the endpointURL returns a non-expected status code")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldReportAnExpectedErrorWhenTheEndpointUrlReturnsANonExpectedStatusCode()
        {
            EnsureServer();
            Server.SetRoute("/json/version/", http =>
            {
                http.Response.StatusCode = 404;
                return http.Response.WriteAsync("{\"webSocketDebuggerUrl\":\"dont-use-me\"}");
            });
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => Playwright.Chromium.ConnectOverCDPAsync(Prefix));
            Assert.That(
                error.Message,
                Does.Contain("browserType.connectOverCDP: Unexpected status 404 when connecting to " + Prefix + "/json/version/"));
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should report an expected error when the endpoint URL JSON webSocketDebuggerUrl is undefined")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldReportAnExpectedErrorWhenTheEndpointUrlJsonWebSocketDebuggerUrlIsUndefined()
        {
            EnsureServer();
            Server.SetRoute("/json/version/", http =>
            {
                http.Response.StatusCode = 200;
                return http.Response.WriteAsync("{}");
            });
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => Playwright.Chromium.ConnectOverCDPAsync(Prefix));
            Assert.That(error.Message, Does.Contain("browserType.connectOverCDP: Invalid URL"));
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect to an existing cdp session when passed as a first argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConnectToAnExistingCdpSessionWhenPassedAsAFirstArgument()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should use proxy with connectOverCDP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseProxyWithConnectOverCdp()
        {
            EnsureServer();
            Server.SetRoute("/target.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><title>Served by the proxy</title></html>");
            });
            await WithHostAsync(async (_, endpoint) =>
            {
                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                IBrowserContext context = await cdpBrowser.NewContextAsync(new BrowserContextOptions
                {
                    Proxy = new Proxy { Server = "localhost:" + TestConstants.Port },
                }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should use env proxy with connectOverCDP discovery request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseEnvProxyWithConnectOverCdpDiscoveryRequest()
        {
            EnsureServer();
            await using OfficialTestProxy proxyServer = new OfficialTestProxy();
            proxyServer.ForwardTo(TestConstants.Port);
            string oldValue = Environment.GetEnvironmentVariable("HTTP_PROXY");
            try
            {
                Environment.SetEnvironmentVariable("HTTP_PROXY", "http://" + proxyServer.Host);
                PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                    () => Playwright.Chromium.ConnectOverCDPAsync(Prefix));
                Assert.That(
                    error.Message,
                    Does.Contain("Unexpected status 404 when connecting to " + Prefix + "/json/version/"));
                Assert.That(proxyServer.RequestUrls, Is.EqualTo(new[] { Prefix + "/json/version/" }));
            }
            finally
            {
                Environment.SetEnvironmentVariable("HTTP_PROXY", oldValue);
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should send target Host header when using env HTTP proxy with connectOverCDP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendTargetHostHeaderWhenUsingEnvHttpProxyWithConnectOverCdp()
        {
            EnsureServer();
            await using OfficialTestProxy proxyServer = new OfficialTestProxy();
            proxyServer.ForwardTo(TestConstants.Port);
            string oldValue = Environment.GetEnvironmentVariable("HTTP_PROXY");
            try
            {
                Environment.SetEnvironmentVariable("HTTP_PROXY", "http://" + proxyServer.Host);
                PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                    () => Playwright.Chromium.ConnectOverCDPAsync(Prefix));
                Assert.That(
                    error.Message,
                    Does.Contain("Unexpected status 404 when connecting to " + Prefix + "/json/version/"));
                Assert.That(proxyServer.RequestHosts, Is.EqualTo(new[] { new Uri(Prefix).Authority }));
            }
            finally
            {
                Environment.SetEnvironmentVariable("HTTP_PROXY", oldValue);
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should be able to connect via localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToConnectViaLocalhost()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                string localhost = "http://localhost:" + new Uri(endpoint).Port;
                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(localhost).ConfigureAwait(false);
                Assert.That(cdpBrowser.Contexts.Count, Is.EqualTo(1));
                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "emulate media should not be affected by second connectOverCDP with noDefaults")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task EmulateMediaShouldNotBeAffectedBySecondConnectOverCdpWithNoDefaults()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                IBrowser browser1 = await Playwright.Chromium.ConnectOverCDPAsync("http://localhost:" + new Uri(endpoint).Port).ConfigureAwait(false);
                IBrowser browser2 = null;
                try
                {
                    IBrowserContext context1 = await browser1.NewContextAsync().ConfigureAwait(false);
                    IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
                    await page1.EmulateMediaAsync(new() { Media = Media.Print }).ConfigureAwait(false);
                    Assert.That(await page1.EvaluateAsync<bool>("() => matchMedia('print').matches").ConfigureAwait(false), Is.True);
                    browser2 = await Playwright.Chromium.ConnectOverCDPAsync("http://localhost:" + new Uri(endpoint).Port, new() { NoDefaults = true }).ConfigureAwait(false);
                    Assert.That(await page1.EvaluateAsync<bool>("() => matchMedia('print').matches").ConfigureAwait(false), Is.True);
                }
                finally
                {
                    await browser1.CloseAsync().ConfigureAwait(false);
                    if (browser2 != null)
                    {
                        await browser2.CloseAsync().ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should allow tracing over cdp session")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowTracingOverCdpSession()
        {
            string traceZip = Path.Combine(Path.GetTempPath(), "pwsharp-cdp-trace-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                await WithHostAsync(async (_, endpoint) =>
                {
                    await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                    IBrowserContext context = FirstContext(cdpBrowser);
                    await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                    IPage page = await context.NewPageAsync().ConfigureAwait(false);
                    await page.EvaluateAsync("() => 2 + 2").ConfigureAwait(false);
                    await context.Tracing.StopAsync(new TracingStopOptions { Path = traceZip }).ConfigureAwait(false);
                    await cdpBrowser.CloseAsync().ConfigureAwait(false);
                    Assert.That(File.Exists(traceZip), Is.True);
                }).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    File.Delete(traceZip);
                }
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "setInputFiles should preserve lastModified timestamp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetInputFilesShouldPreserveLastModifiedTimestamp()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                await using IBrowser cdpBrowser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                IPage page = await FirstContext(cdpBrowser).NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<input type=file multiple=true/>").ConfigureAwait(false);
                ILocator input = page.Locator("input");
                string[] files = { "file-to-upload.txt", "file-to-upload-2.txt" };
                await input.SetInputFilesAsync(new[] { Asset(files[0]), Asset(files[1]) }).ConfigureAwait(false);
                Assert.That(
                    await input.EvaluateAsync<string[]>("e => [...e.files].map(f => f.name)").ConfigureAwait(false),
                    Is.EqualTo(files));
                long[] timestamps = await input.EvaluateAsync<long[]>("e => [...e.files].map(f => f.lastModified)").ConfigureAwait(false);
                long[] expected = new long[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    DateTime utc = File.GetLastWriteTimeUtc(Asset(files[i]));
                    expected[i] = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
                    Assert.That(
                        Math.Abs(timestamps[i] - expected[i]),
                        Is.LessThanOrEqualTo(1000),
                        "expected: " + string.Join(",", expected) + "; actual: " + string.Join(",", timestamps));
                }

                await cdpBrowser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "setInputFiles should use local path when isLocal is set")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void SetInputFilesShouldUseLocalPathWhenIsLocalIsSet()
        {
            Assert.Ignore("Official Node-only toImpl(cdpBrowser)._isBrowserCollocatedWithServer.");
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should print custom ws close error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldPrintCustomWsCloseError()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(async ws =>
            {
                byte[] buffer = new byte[4096];
                await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);
                await ws.CloseAsync((WebSocketCloseStatus)4123, "Oh my!", CancellationToken.None).ConfigureAwait(false);
            });
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => Playwright.Chromium.ConnectOverCDPAsync("ws://localhost:" + TestConstants.Port + "/ws"));
            Assert.That(error.Message, Does.Contain("Browser logs:\n\nOh my!\n"));
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should not reuse utility worlds between two clients")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNotReuseUtilityWorldsBetweenTwoClients()
        {
            Assert.Ignore("Official Node-only toImpl(page.mainFrame()).evaluateExpression utility world.");
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should get title and URL of existing page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGetTitleAndUrlOfExistingPage()
        {
            EnsureServer();
            await WithHostAsync(async (_, endpoint) =>
            {
                List<IBrowser> browsers = new List<IBrowser>();
                try
                {
                    IBrowser first = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                    browsers.Add(first);
                    IBrowserContext firstContext = FirstContext(first);
                    await CloseLeftoverPagesAsync(firstContext).ConfigureAwait(false);
                    IPage page = await firstContext.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                    await page.EvaluateAsync("() => document.title = 'my title'").ConfigureAwait(false);

                    IBrowser second = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                    browsers.Add(second);
                    IPage existing = FirstPage(FirstContext(second));
                    Assert.That(existing.Url, Is.EqualTo(TestConstants.EmptyPage));
                    Assert.That(await existing.TitleAsync().ConfigureAwait(false), Is.EqualTo("my title"));
                }
                finally
                {
                    foreach (IBrowser browser in browsers)
                    {
                        await browser.CloseAsync().ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should skip default overrides with noDefaults")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSkipDefaultOverridesWithNoDefaults()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=file.txt";
                return http.Response.WriteAsync("Hello world");
            });
            await WithHostAsync(async (_, endpoint) =>
            {
                await using IBrowser browser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint, new() { NoDefaults = true }).ConfigureAwait(false);
                IPage page = await FirstContext(browser).NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                bool sawDownload = false;
                page.Download += (_, _) => { sawDownload = true; };
                await page.ClickAsync("a").ConfigureAwait(false);
                await page.WaitForTimeoutAsync(500).ConfigureAwait(false);
                Assert.That(sawDownload, Is.False);
                await browser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "noDefaults should not affect new contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NoDefaultsShouldNotAffectNewContexts()
        {
            await WithHostAsync(async (_, endpoint) =>
            {
                await using IBrowser browser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint, new() { NoDefaults = true }).ConfigureAwait(false);
                IBrowserContext newContext = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await newContext.NewPageAsync().ConfigureAwait(false);
                bool hasFocus = await page.EvaluateAsync<bool>("() => document.hasFocus()").ConfigureAwait(false);
                Assert.That(hasFocus, Is.True);
                await newContext.CloseAsync().ConfigureAwait(false);
                await browser.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect over CDP using a ConnectionTransport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldConnectOverCdpUsingAConnectionTransport()
        {
            Assert.Ignore("Official skip: Passing a transport to connectOverCDP is only available in-process");
        }

        private static async Task WithHostAsync(Func<IBrowser, string, Task> body)
        {
            int port = FreeCdpPort();
            IBrowser host = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = new[] { "--remote-debugging-port=" + port },
            }).ConfigureAwait(false);
            try
            {
                await body(host, "http://127.0.0.1:" + port + "/").ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await host.CloseAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static async Task SwallowConnectAsync(Task<IBrowser> connect)
        {
            try
            {
                IBrowser browser = await connect.ConfigureAwait(false);
                if (browser != null)
                {
                    await browser.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }

        private static async Task CloseLeftoverPagesAsync(IBrowserContext context)
        {
            foreach (IPage page in context.Pages.ToList())
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
        }

        private static IBrowserContext FirstContext(IBrowser browser)
            => browser.Contexts.First();

        private static IPage FirstPage(IBrowserContext context)
            => context.Pages.First();

        private static string Asset(string name) => TestUtils.GetWebServerFile(name);

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static int FreeCdpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
