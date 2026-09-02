/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/proxy.spec.ts</c> parity. Launch-level
    /// <c>browserType.launch({ proxy })</c>. Skipped (Node-only type
    /// validation): <c>should throw for bad server value</c>
    /// (<c>proxy.server: expected string, got number</c>; C#
    /// <see cref="Proxy.Server"/> is already a string). Official
    /// <c>it.fixme</c> on <c>should use proxy with emulated user agent</c>.
    /// Do not edit leftover <c>ContextProxyTests</c>,
    /// <c>ContextProxyAuthTests</c>, or <c>LoopbackHttpProxy</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryProxyParityTests : PageTestEx
    {
        private const string ProxiedTitleHtml = "<html><title>Served by the proxy</title></html>";
        private const string ServerTitleHtml = "<html><title>Served by the server</title></html>";
        private const string NipHost = "fake-localhost-127-0-0-1.nip.io";

        private static SimpleServer _ownedServer;
        private static OfficialHttpsTargetServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static int ServerPort = TestConstants.Port;

        private OfficialTestProxy _proxy;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19888;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    ServerPort = port;
                    _ownedHttps = OfficialHttpsTargetServer.Start();
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                ServerPort = TestConstants.Port;
                _ownedHttps = OfficialHttpsTargetServer.Start();
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedHttps != null)
            {
                await _ownedHttps.DisposeAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }

            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _proxy = new OfficialTestProxy();
            if (Server != null)
            {
                Server.SetRoute("/target.html", async context =>
                {
                    await context.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
                });
            }
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            if (_proxy != null)
            {
                await _proxy.DisposeAsync().ConfigureAwait(false);
                _proxy = null;
            }

            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
        }

        [PlaywrightTest("proxy.spec.ts", "should use proxy @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseProxySmoke()
        {
            EnsureServer();
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy { Server = ServerHost() }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should use proxy for second page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseProxyForSecondPage()
        {
            EnsureServer();
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "localhost:" + ServerPort.ToString(CultureInfo.InvariantCulture),
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));

                IPage page2 = await browser.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page2.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should work with IP:PORT notion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithIpPortNotion()
        {
            EnsureServer();
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "127.0.0.1:" + ServerPort.ToString(CultureInfo.InvariantCulture),
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LocalhostByDefault()
            => ProxyLocalNetworkAsync("localhost", additionalBypass: false);

        [PlaywrightTest("proxy.spec.ts", "loopback address")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LoopbackAddressByDefault()
            => ProxyLocalNetworkAsync("127.0.0.1", additionalBypass: false);

        [PlaywrightTest("proxy.spec.ts", "link-local")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LinkLocalByDefault()
            => ProxyLocalNetworkAsync("169.254.3.4", additionalBypass: false);

        [PlaywrightTest("proxy.spec.ts", "localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LocalhostWithOtherBypasses()
            => ProxyLocalNetworkAsync("localhost", additionalBypass: true);

        [PlaywrightTest("proxy.spec.ts", "loopback address")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LoopbackAddressWithOtherBypasses()
            => ProxyLocalNetworkAsync("127.0.0.1", additionalBypass: true);

        [PlaywrightTest("proxy.spec.ts", "link-local")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LinkLocalWithOtherBypasses()
            => ProxyLocalNetworkAsync("169.254.3.4", additionalBypass: true);

        [PlaywrightTest("proxy.spec.ts", "should allow bypassing localhost requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldAllowBypassingLocalhostRequests()
            => AllowBypassingHostAsync("localhost");

        [PlaywrightTest("proxy.spec.ts", "should allow bypassing 127.0.0.1 requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldAllowBypassingLoopbackRequests()
            => AllowBypassingHostAsync("127.0.0.1");

        [PlaywrightTest("proxy.spec.ts", "should authenticate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAuthenticate()
        {
            EnsureServer();
            string expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"));
            Server.SetRoute("/target.html", async http =>
            {
                string auth = http.Request.Headers["Proxy-Authorization"].ToString();
                if (string.IsNullOrEmpty(auth))
                {
                    http.Response.StatusCode = 407;
                    http.Response.Headers["Proxy-Authenticate"] = "Basic realm=\"Access to internal site\"";
                    return;
                }

                await http.Response.WriteAsync("<html><title>" + auth + "</title></html>").ConfigureAwait(false);
            });

            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = ServerHost(),
                    Username = "user",
                    Password = "secret",
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo(expected));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should reconnect with credentials after CONNECT 407 closes the socket")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReconnectWithCredentialsAfterConnect407ClosesTheSocket()
        {
            if (_ownedHttps == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }

            _proxy.ForwardTo(_ownedHttps.Port, allowConnectRequests: true);
            List<bool> connectAttempts = new();
            bool closedFirstConnect = false;
            _proxy.SetAuthHandler(req =>
            {
                if (!string.Equals(req.Method, "CONNECT", StringComparison.OrdinalIgnoreCase)
                    || req.Host == null
                    || !req.Host.StartsWith("non-existent.com", StringComparison.Ordinal))
                {
                    return true;
                }

                connectAttempts.Add(!string.IsNullOrEmpty(req.ProxyAuthorization));
                if (!closedFirstConnect)
                {
                    closedFirstConnect = true;
                    return false;
                }

                return !string.IsNullOrEmpty(req.ProxyAuthorization);
            });

            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = _proxy.Host,
                    Username = "user",
                    Password = "secret",
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
                await page.GoToAsync("https://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by https server via proxy"));
                Assert.That(connectAttempts.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(connectAttempts.Exists(hadAuth => hadAuth), Is.True);
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should work with authenticate followed by redirect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAuthenticateFollowedByRedirect()
        {
            EnsureServer();
            static bool HasAuth(HttpContext http)
            {
                string auth = http.Request.Headers["Proxy-Authorization"].ToString();
                if (string.IsNullOrEmpty(auth))
                {
                    http.Response.StatusCode = 407;
                    http.Response.Headers["Proxy-Authenticate"] = "Basic realm=\"Access to internal site\"";
                    return false;
                }

                return true;
            }

            Server.SetRoute("/page1.html", async http =>
            {
                if (!HasAuth(http))
                {
                    return;
                }

                http.Response.StatusCode = 302;
                http.Response.Headers["location"] = "/page2.html";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            Server.SetRoute("/page2.html", async http =>
            {
                if (!HasAuth(http))
                {
                    return;
                }

                await http.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
            });

            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = ServerHost(),
                    Username = "user",
                    Password = "secret",
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/page1.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should exclude patterns")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExcludePatterns()
        {
            EnsureServer();
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = ServerHost(),
                    Bypass = "1.non.existent.domain.for.the.test, 2.non.existent.domain.for.the.test, .another.test",
                }).ConfigureAwait(false);
            try
            {
                {
                    IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync("http://0.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                    Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
                    await page.CloseAsync().ConfigureAwait(false);
                }

                {
                    IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                    Exception error = await CatchGotoAsync(page, "http://1.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                    Assert.That(error, Is.Not.Null);
                    Assert.That(error.Message, Is.Not.Empty);
                    await page.CloseAsync().ConfigureAwait(false);
                }

                {
                    IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                    Exception error = await CatchGotoAsync(page, "http://2.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                    Assert.That(error, Is.Not.Null);
                    Assert.That(error.Message, Is.Not.Empty);
                    await page.CloseAsync().ConfigureAwait(false);
                }

                {
                    IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                    Exception error = await CatchGotoAsync(page, "http://foo.is.the.another.test/target.html").ConfigureAwait(false);
                    Assert.That(error, Is.Not.Null);
                    Assert.That(error.Message, Is.Not.Empty);
                    await page.CloseAsync().ConfigureAwait(false);
                }

                {
                    IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync("http://3.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                    Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
                    await page.CloseAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should bypass proxy for localhost when localhost is in bypass list")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldBypassProxyForLocalhostWhenLocalhostIsInBypassList()
            => BypassProxyForHostWhenInBypassListAsync("localhost");

        [PlaywrightTest("proxy.spec.ts", "should bypass proxy for 127.0.0.1 when 127.0.0.1 is in bypass list")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldBypassProxyForLoopbackWhenLoopbackIsInBypassList()
            => BypassProxyForHostWhenInBypassListAsync("127.0.0.1");

        [PlaywrightTest("proxy.spec.ts", "should use socks proxy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseSocksProxy()
        {
            await using MockSocksProxy socks = new MockSocksProxy();
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "socks5://localhost:" + socks.Port.ToString(CultureInfo.InvariantCulture),
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the SOCKS proxy"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should use socks proxy in second page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseSocksProxyInSecondPage()
        {
            await using MockSocksProxy socks = new MockSocksProxy();
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "socks5://localhost:" + socks.Port.ToString(CultureInfo.InvariantCulture),
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the SOCKS proxy"));

                IPage page2 = await browser.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync("http://non-existent.com").ConfigureAwait(false);
                Assert.That(await page2.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the SOCKS proxy"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "does launch without a port")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DoesLaunchWithoutAPort()
        {
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy { Server = "http://localhost" }).ConfigureAwait(false);
            await DisposeQuietlyAsync(browser).ConfigureAwait(false);
        }

        [PlaywrightTest("proxy.spec.ts", "should use proxy with emulated user agent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldUseProxyWithEmulatedUserAgent()
        {
            Assert.Ignore("Non-emulated user agent is used in proxy CONNECT");
        }

        [PlaywrightTest("proxy.spec.ts", "should use SOCKS proxy for websocket requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseSocksProxyForWebsocketRequests()
        {
            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            await using OfficialSocksForwardingProxy socks = new OfficialSocksForwardingProxy(ServerPort, 1337);
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy { Server = socks.Server }).ConfigureAwait(false);
            try
            {
                Server.SetRoute("/target.html", async http =>
                {
                    await http.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
                });

                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://" + NipHost + ":1337/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));

                string value = await page.EvaluateAsync<string>(@"() => {
                    let cb;
                    const result = new Promise(f => cb = f);
                    const ws = new WebSocket('ws://fake-localhost-127-0-0-1.nip.io:1337/ws');
                    ws.addEventListener('message', data => { ws.close(); cb(data.data); });
                    return result;
                }").ConfigureAwait(false);
                Assert.That(value, Is.EqualTo("incoming"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("proxy.spec.ts", "should use http proxy for websocket requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseHttpProxyForWebsocketRequests()
        {
            if (TestConstants.IsMacOSX && Environment.OSVersion.Version.Major == 13)
            {
                Assert.Ignore("Times out on Mac 13");
            }

            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            _proxy.ForwardTo(ServerPort, allowConnectRequests: true);
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy { Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture) }).ConfigureAwait(false);
            try
            {
                Server.SetRoute("/target.html", async http =>
                {
                    await http.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
                });

                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://" + NipHost + ":1337/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));

                string value = await page.EvaluateAsync<string>(@"() => {
                    let cb;
                    const result = new Promise(f => cb = f);
                    const ws = new WebSocket('ws://fake-localhost-127-0-0-1.nip.io:1337/ws');
                    ws.addEventListener('message', data => { ws.close(); cb(data.data); });
                    return result;
                }").ConfigureAwait(false);
                Assert.That(value, Is.EqualTo("incoming"));

                if (TestConstants.IsWebKit)
                {
                    string expected = TestConstants.IsWindows
                        ? "/ws"
                        : "ws://" + NipHost + ":1337/ws";
                    Assert.That(_proxy.WsUrls, Does.Contain(expected));
                }
                else
                {
                    Assert.That(_proxy.ConnectHosts, Does.Contain(NipHost + ":1337"));
                }
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        private async Task ProxyLocalNetworkAsync(string target, bool additionalBypass)
        {
            if (TestConstants.IsWebKit
                && TestConstants.IsMacOSX
                && (target == "localhost" || target == "127.0.0.1")
                && additionalBypass)
            {
                Assert.Ignore("Mac webkit does not proxy localhost when bypass rules are set.");
            }

            EnsureServer();
            string path = "/target-" + additionalBypass.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() + "-" + target + ".html";
            Server.SetRoute(path, async http =>
            {
                await http.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
            });

            string url = "http://" + target + ":55555" + path;
            _proxy.ForwardTo(ServerPort);
            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                    Bypass = additionalBypass ? "1.non.existent.domain.for.the.test" : null,
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(url).ConfigureAwait(false);
                Assert.That(_proxy.RequestUrls, Does.Contain(url));
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));

                await CatchGotoAsync(page, "http://1.non.existent.domain.for.the.test/foo.html").ConfigureAwait(false);
                if (additionalBypass)
                {
                    Assert.That(_proxy.RequestUrls, Does.Not.Contain("http://1.non.existent.domain.for.the.test/foo.html"));
                }
                else
                {
                    Assert.That(_proxy.RequestUrls, Does.Contain("http://1.non.existent.domain.for.the.test/foo.html"));
                }
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        private async Task AllowBypassingHostAsync(string host)
        {
            EnsureServer();
            Server.SetRoute("/proxied/target.html", async http =>
            {
                await http.Response.WriteAsync(ServerTitleHtml).ConfigureAwait(false);
            });
            Server.SetRoute("/target.html", async http =>
            {
                await http.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
            });
            _proxy.ForwardTo(ServerPort, removePrefix: "/proxied");

            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                    Bypass = host,
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://" + host + ":" + ServerPort.ToString(CultureInfo.InvariantCulture) + "/proxied/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the server"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        private async Task BypassProxyForHostWhenInBypassListAsync(string host)
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort, removePrefix: "/proxied");
            Server.SetRoute("/proxied/target.html", async http =>
            {
                await http.Response.WriteAsync(ServerTitleHtml).ConfigureAwait(false);
            });
            Server.SetRoute("/target.html", async http =>
            {
                await http.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
            });

            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                    Bypass = host,
                }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://" + host + ":" + ServerPort.ToString(CultureInfo.InvariantCulture) + "/proxied/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the server"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        private string ServerHost()
            => "localhost:" + ServerPort.ToString(CultureInfo.InvariantCulture);

        private static async Task<Exception> CatchGotoAsync(IPage page, string url)
        {
            try
            {
                await page.GoToAsync(url).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
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
            if (disposable == null)
            {
                return;
            }

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
