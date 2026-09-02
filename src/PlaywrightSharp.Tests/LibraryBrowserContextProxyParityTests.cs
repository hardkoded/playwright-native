/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-proxy.spec.ts</c> parity.
    /// Skipped (Node-only channel type validation):
    /// <c>should throw for bad server value</c> (<c>proxy.server: expected
    /// string, got number</c>; C# <see cref="Proxy.Server"/> is already a
    /// string). Do not edit leftover <c>ContextProxyTests</c>,
    /// <c>ContextProxyAuthTests</c>, or <c>LoopbackHttpProxy</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextProxyParityTests : PageTestEx
    {
        private const string ProxiedTitleHtml = "<html><title>Served by the proxy</title></html>";

        private static SimpleServer _ownedServer;
        private static OfficialHttpsTargetServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static int ServerPort = TestConstants.Port;

        private IBrowser _browser;
        private OfficialTestProxy _proxy;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19847;
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
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should work when passing the proxy only on the context level")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenPassingTheProxyOnlyOnTheContextLevel()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            try
            {
                IBrowserContext context = await browser.NewContextAsync(new() { Proxy = new Proxy { Server = _proxy.Host } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should use proxy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseProxy()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new() { Proxy = new Proxy { Server = _proxy.Host } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should send secure cookies to subdomain.localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendSecureCookiesToSubdomainLocalhost()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new() { Proxy = new Proxy { Server = _proxy.Host } }).ConfigureAwait(false);
            Server.SetRoute("/set-cookie.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "non-secure=1; HttpOnly");
                http.Response.Headers.Append("Set-Cookie", "secure=1; HttpOnly; Secure");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            Server.SetRoute("/read-cookie.html", async http =>
            {
                http.Response.ContentType = "text/html";
                string cookie = http.Request.Headers["Cookie"].ToString();
                string[] parts = cookie.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Array.Sort(parts, StringComparer.Ordinal);
                await http.Response.WriteAsync("<div>Cookie: " + string.Join("; ", parts) + "</div>").ConfigureAwait(false);
            });

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://subdomain.localhost/set-cookie.html").ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync("http://subdomain.localhost").ConfigureAwait(false);
            List<(string Name, string Domain)> actual = cookies
                .Select(cookie => (cookie.Name, cookie.Domain))
                .ToList();
            List<(string Name, string Domain)> expected = new()
            {
                ("non-secure", "subdomain.localhost"),
            };
            if (!(TestConstants.IsWebKit && (!TestConstants.IsWindows || IsWebkitWsl())))
            {
                expected.Add(("secure", "subdomain.localhost"));
            }

            Assert.That(actual, Is.EqualTo(expected));

            await page.GoToAsync("http://subdomain.localhost/read-cookie.html").ConfigureAwait(false);
            string expectedText = TestConstants.IsWebKit
                ? "Cookie: non-secure=1"
                : "Cookie: non-secure=1; secure=1";
            await Assertions.Expect(page.Locator("div")).ToHaveTextAsync(expectedText).ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should set cookie for top-level domain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetCookieForTopLevelDomain()
        {
            bool isLinux = !TestConstants.IsWindows && !TestConstants.IsMacOSX;
            if (TestConstants.IsWebKit && (isLinux || IsWebkitWsl()))
            {
                Assert.Ignore("official it.fixme(webkit && (isLinux || webkit-wsl))");
            }

            EnsureServer();
            _proxy.ForwardTo(ServerPort, allowConnectRequests: true);
            IBrowserContext context = await _browser.NewContextAsync(new() { Proxy = new Proxy { Server = _proxy.Host } }).ConfigureAwait(false);
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name=val; Domain=codes; Path=/;");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });

            await context.APIRequest.GetAsync("http://codes/empty.html").ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies, Is.Not.Empty);
            BrowserContextCookiesResult cookie = cookies[0];
            Assert.That(cookie, Is.Not.Null);
            Assert.That(cookie.Name, Is.EqualTo("name"));
            Assert.That(cookie.Value, Is.EqualTo("val"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LocalhostByDefault()
            => ProxyLocalNetworkAsync("localhost", additionalBypass: false);

        [PlaywrightTest("browsercontext-proxy.spec.ts", "loopback address")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LoopbackAddressByDefault()
            => ProxyLocalNetworkAsync("127.0.0.1", additionalBypass: false);

        [PlaywrightTest("browsercontext-proxy.spec.ts", "link-local")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LinkLocalByDefault()
            => ProxyLocalNetworkAsync("169.254.3.4", additionalBypass: false);

        [PlaywrightTest("browsercontext-proxy.spec.ts", "localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LocalhostWithOtherBypasses()
            => ProxyLocalNetworkAsync("localhost", additionalBypass: true);

        [PlaywrightTest("browsercontext-proxy.spec.ts", "loopback address")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LoopbackAddressWithOtherBypasses()
            => ProxyLocalNetworkAsync("127.0.0.1", additionalBypass: true);

        [PlaywrightTest("browsercontext-proxy.spec.ts", "link-local")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task LinkLocalWithOtherBypasses()
            => ProxyLocalNetworkAsync("169.254.3.4", additionalBypass: true);

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should use ipv6 proxy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseIpv6Proxy()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "[0:0:0:0:0:0:0:1]:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should use proxy twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseProxyTwice()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new() { Proxy = new Proxy { Server = _proxy.Host } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
            await page.GoToAsync("http://non-existent-2.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent-2.com/target.html"));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should use proxy for second page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseProxyForSecondPage()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new() { Proxy = new Proxy { Server = _proxy.Host } }).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));

            IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
            _proxy.RequestUrls = Array.Empty<string>();
            await page2.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
            Assert.That(await page2.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should use proxy for https urls")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseProxyForHttpsUrls()
        {
            if (_ownedHttps == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }

            _proxy.ForwardTo(_ownedHttps.Port, allowConnectRequests: true);
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, Proxy = new Proxy { Server = _proxy.Host } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("https://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.ConnectHosts, Does.Contain("non-existent.com:443"));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by https server via proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should work with IP:PORT notion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithIpPortNotion()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "127.0.0.1:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should throw for socks5 authentication")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowForSocks5Authentication()
        {
            Exception error = Assert.CatchAsync(
                () => _browser.NewContextAsync(new()
                {
                    Proxy = new Proxy
                    {
                        Server = "socks5://localhost:1234",
                        Username = "user",
                        Password = "secret",
                    }
                }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Browser does not support socks5 proxy authentication"));
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should throw for socks4 authentication")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowForSocks4Authentication()
        {
            Exception error = Assert.CatchAsync(
                () => _browser.NewContextAsync(new()
                {
                    Proxy = new Proxy
                    {
                        Server = "socks4://localhost:1234",
                        Username = "user",
                        Password = "secret",
                    }
                }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Socks4 proxy protocol does not support authentication"));
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should authenticate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAuthenticate()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            string auth = null;
            _proxy.SetAuthHandler(header =>
            {
                auth = header;
                return !string.IsNullOrEmpty(header);
            });
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = _proxy.Host,
                    Username = "user",
                    Password = "secret",
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(_proxy.RequestUrls, Does.Contain("http://non-existent.com/target.html"));
            Assert.That(auth, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"))));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should authenticate with empty password")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAuthenticateWithEmptyPassword()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            string auth = null;
            _proxy.SetAuthHandler(header =>
            {
                auth = header;
                return !string.IsNullOrEmpty(header);
            });
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = _proxy.Host,
                    Username = "user",
                    Password = string.Empty,
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            Assert.That(auth, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:"))));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should isolate proxy credentials between contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateProxyCredentialsBetweenContexts()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            string auth = null;
            _proxy.SetAuthHandler(header =>
            {
                auth = header;
                return !string.IsNullOrEmpty(header);
            });
            {
                IBrowserContext context = await _browser.NewContextAsync(new()
                {
                    Proxy = new Proxy
                    {
                        Server = _proxy.Host,
                        Username = "user1",
                        Password = "secret1",
                    }
                }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(auth, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user1:secret1"))));
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
                await context.CloseAsync().ConfigureAwait(false);
            }

            auth = null;
            {
                IBrowserContext context = await _browser.NewContextAsync(new()
                {
                    Proxy = new Proxy
                    {
                        Server = _proxy.Host,
                        Username = "user2",
                        Password = "secret2",
                    }
                }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
                Assert.That(auth, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user2:secret2"))));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should exclude patterns")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExcludePatterns()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = _proxy.Host,
                    Bypass = "1.non.existent.domain.for.the.test, 2.non.existent.domain.for.the.test, .another.test",
                }
            }).ConfigureAwait(false);

            {
                _proxy.RequestUrls = Array.Empty<string>();
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://0.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                Assert.That(_proxy.RequestUrls, Does.Contain("http://0.non.existent.domain.for.the.test/target.html"));
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
                await page.CloseAsync().ConfigureAwait(false);
            }

            {
                _proxy.RequestUrls = Array.Empty<string>();
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Exception error = await CatchGotoAsync(page, "http://1.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                Assert.That(NonFaviconUrls(), Is.Empty);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Is.Not.Empty);
                await page.CloseAsync().ConfigureAwait(false);
            }

            {
                _proxy.RequestUrls = Array.Empty<string>();
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Exception error = await CatchGotoAsync(page, "http://2.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                Assert.That(NonFaviconUrls(), Is.Empty);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Is.Not.Empty);
                await page.CloseAsync().ConfigureAwait(false);
            }

            {
                _proxy.RequestUrls = Array.Empty<string>();
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Exception error = await CatchGotoAsync(page, "http://foo.is.the.another.test/target.html").ConfigureAwait(false);
                Assert.That(NonFaviconUrls(), Is.Empty);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Is.Not.Empty);
                await page.CloseAsync().ConfigureAwait(false);
            }

            {
                _proxy.RequestUrls = Array.Empty<string>();
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("http://3.non.existent.domain.for.the.test/target.html").ConfigureAwait(false);
                Assert.That(NonFaviconUrls(), Does.Contain("http://3.non.existent.domain.for.the.test/target.html"));
                Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the proxy"));
                await page.CloseAsync().ConfigureAwait(false);
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should use socks proxy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseSocksProxy()
        {
            await using MockSocksProxy socks = new MockSocksProxy();
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "socks5://localhost:" + socks.Port.ToString(CultureInfo.InvariantCulture),
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com").ConfigureAwait(false);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the SOCKS proxy"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should use socks proxy in second page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseSocksProxyInSecondPage()
        {
            await using MockSocksProxy socks = new MockSocksProxy();
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "socks5://localhost:" + socks.Port.ToString(CultureInfo.InvariantCulture),
                }
            }).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("http://non-existent.com").ConfigureAwait(false);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the SOCKS proxy"));

            IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
            await page2.GoToAsync("http://non-existent.com").ConfigureAwait(false);
            Assert.That(await page2.TitleAsync().ConfigureAwait(false), Is.EqualTo("Served by the SOCKS proxy"));

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "does launch without a port")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DoesLaunchWithoutAPort()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { Proxy = new Proxy { Server = "http://localhost" } }).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "should isolate proxy credentials between contexts on navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateProxyCredentialsBetweenContextsOnNavigation()
        {
            EnsureServer();
            Server.SetRoute("/target.html", async http =>
            {
                string authHeader = http.Request.Headers["Proxy-Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader))
                {
                    http.Response.StatusCode = 407;
                    http.Response.Headers["Proxy-Authenticate"] = "Basic realm=\"proxy\"";
                    await http.Response.WriteAsync("Proxy authorization required").ConfigureAwait(false);
                    return;
                }

                string encoded = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                string username = decoded.Split(':')[0];
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("Hello <div data-testid=user>" + username + "</div>!\n").ConfigureAwait(false);
            });

            IBrowserContext context1 = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = Prefix,
                    Username = "user1",
                    Password = "secret1",
                }
            }).ConfigureAwait(false);
            IPage page1 = await context1.NewPageAsync().ConfigureAwait(false);
            await page1.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            await Assertions.Expect(page1.GetByTestId("user")).ToHaveTextAsync("user1").ConfigureAwait(false);

            IBrowserContext context2 = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = Prefix,
                    Username = "user2",
                    Password = "secret2",
                }
            }).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await page2.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            await Assertions.Expect(page2.GetByTestId("user")).ToHaveTextAsync("user2").ConfigureAwait(false);

            await page1.GoToAsync("http://non-existent.com/target.html").ConfigureAwait(false);
            await Assertions.Expect(page1.GetByTestId("user")).ToHaveTextAsync("user1").ConfigureAwait(false);
        }

        private async Task ProxyLocalNetworkAsync(string target, bool additionalBypass)
        {
            if (TestConstants.IsWebKit
                && TestConstants.IsMacOSX
                && (target == "localhost" || target == "127.0.0.1")
                && additionalBypass)
            {
                Assert.Ignore("Mac webkit does not proxy localhost when bypass rules are set");
            }

            EnsureServer();
            string path = "/target-" + additionalBypass.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() + "-" + target + ".html";
            Server.SetRoute(path, async http =>
            {
                await http.Response.WriteAsync(ProxiedTitleHtml).ConfigureAwait(false);
            });

            string url = "http://" + target + ":55555" + path;
            _proxy.ForwardTo(ServerPort);
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = _proxy.Host,
                    Bypass = additionalBypass ? "1.non.existent.domain.for.the.test" : null,
                }
            }).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
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

            await context.CloseAsync().ConfigureAwait(false);
        }

        private string[] NonFaviconUrls()
            => _proxy.RequestUrls.Where(url => url.IndexOf("favicon", StringComparison.Ordinal) < 0).ToArray();

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

        private static bool IsWebkitWsl()
            => string.Equals(Environment.GetEnvironmentVariable("PLAYWRIGHT_WEBKIT_WSL"), "1", StringComparison.Ordinal);

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
