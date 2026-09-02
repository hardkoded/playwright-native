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
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/fetch-proxy.spec.ts</c> parity. Six portable
    /// titles. File-level <c>mode !== 'default'</c> does not apply here.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryFetchProxyParityTests : BrowserTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static int ServerPort = TestConstants.Port;

        private IBrowser _browser;
        private OfficialTestProxy _proxy;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19876;
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
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _proxy = new OfficialTestProxy();
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
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("fetch-proxy.spec.ts", "context request should pick up proxy credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextRequestShouldPickUpProxyCredentials()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort, allowConnectRequests: true);
            TaskCompletionSource<string> auth = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _proxy.SetAuthHandler(header =>
            {
                if (Array.IndexOf(_proxy.ConnectHosts, "non-existent.com:80") >= 0)
                {
                    auth.TrySetResult(header);
                }

                return !string.IsNullOrEmpty(header);
            });

            IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy
                {
                    Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                    Username = "user",
                    Password = "secret",
                }).ConfigureAwait(false);
            try
            {
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IAPIResponse response = await context.APIRequest.GetAsync("http://non-existent.com/simple.json")
                    .ConfigureAwait(false);
                Assert.That(_proxy.ConnectHosts, Does.Contain("non-existent.com:80"));
                string header = await auth.Task.ConfigureAwait(false);
                Assert.That(header, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"))));
                JsonElement? json = await response.JsonAsync().ConfigureAwait(false);
                Assert.That(json.HasValue, Is.True);
                Assert.That(json.Value.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("fetch-proxy.spec.ts", "global request should pick up proxy credentials")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GlobalRequestShouldPickUpProxyCredentials()
        {
            EnsureServer();
            _proxy.ForwardTo(ServerPort, allowConnectRequests: true);
            string auth = null;
            _proxy.SetAuthHandler(header =>
            {
                auth = header;
                return !string.IsNullOrEmpty(auth);
            });

            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                    Username = "user",
                    Password = "secret",
                }
            }).ConfigureAwait(false);
            try
            {
                IAPIResponse response = await request.GetAsync("http://non-existent.com/simple.json")
                    .ConfigureAwait(false);
                Assert.That(_proxy.ConnectHosts, Does.Contain("non-existent.com:80"));
                Assert.That(auth, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"))));
                JsonElement? json = await response.JsonAsync().ConfigureAwait(false);
                Assert.That(json.HasValue, Is.True);
                Assert.That(json.Value.GetProperty("foo").GetString(), Is.EqualTo("bar"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("fetch-proxy.spec.ts", "should work with context level proxy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithContextLevelProxy()
        {
            EnsureServer();
            Server.SetRoute("/target.html", async http =>
            {
                await http.Response.WriteAsync("<title>Served by the proxy</title>").ConfigureAwait(false);
            });
            _proxy.ForwardTo(ServerPort, allowConnectRequests: true);
            IBrowserContext context = await _browser.NewContextAsync(new() { Proxy = new Proxy { Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture) } })
                .ConfigureAwait(false);
            try
            {
                Task<string> requestTask = Server.WaitForRequest("/target.html", req => req.Path.ToString());
                Task<IAPIResponse> responseTask = context.APIRequest.GetAsync("http://non-existent.com/target.html");
                await Task.WhenAll(requestTask, responseTask).ConfigureAwait(false);
                Assert.That(responseTask.Result.Status, Is.EqualTo(200));
                Assert.That(requestTask.Result, Is.EqualTo("/target.html"));
            }
            finally
            {
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("fetch-proxy.spec.ts", "should support proxy.bypass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportProxyBypass()
        {
            EnsureServer();
            Server.SetRoute("/target.html", async http =>
            {
                await http.Response.WriteAsync("Served by the proxy").ConfigureAwait(false);
            });
            _proxy.ForwardTo(ServerPort, allowConnectRequests: true);
            IBrowserContext context = await _browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "localhost:" + _proxy.Port.ToString(CultureInfo.InvariantCulture),
                    Bypass = "1.non.existent.domain.for.the.test, 2.non.existent.domain.for.the.test, .another.test",
                }
            }).ConfigureAwait(false);
            try
            {
                {
                    IAPIResponse res = await context.APIRequest.GetAsync(CrossProcessPrefix + "/target.html")
                        .ConfigureAwait(false);
                    Assert.That(await res.TextAsync().ConfigureAwait(false), Does.Contain("Served by the proxy"));
                    Assert.That(_proxy.ConnectHosts, Does.Contain(new Uri(CrossProcessPrefix).Authority));
                    _proxy.ConnectHosts = Array.Empty<string>();
                }

                {
                    IAPIResponse res = await context.APIRequest
                        .GetAsync("http://0.non.existent.domain.for.the.test/target.html")
                        .ConfigureAwait(false);
                    Assert.That(await res.TextAsync().ConfigureAwait(false), Does.Contain("Served by the proxy"));
                    _proxy.ConnectHosts = Array.Empty<string>();
                }

                {
                    Exception error = Assert.CatchAsync(() => context.APIRequest.GetAsync("http://1.non.existent.domain.for.the.test/target.html"));
                    Assert.That(error.Message, Is.Not.Empty);
                    Assert.That(_proxy.ConnectHosts, Is.EqualTo(Array.Empty<string>()));
                }

                {
                    Exception error = Assert.CatchAsync(() => context.APIRequest.GetAsync("http://2.non.existent.domain.for.the.test/target.html"));
                    Assert.That(error.Message, Is.Not.Empty);
                    Assert.That(_proxy.ConnectHosts, Is.EqualTo(Array.Empty<string>()));
                }

                {
                    Exception error = Assert.CatchAsync(() => context.APIRequest.GetAsync("http://foo.is.the.another.test/target.html"));
                    Assert.That(error.Message, Is.Not.Empty);
                    Assert.That(_proxy.ConnectHosts, Is.EqualTo(Array.Empty<string>()));
                }

                {
                    IAPIResponse res = await context.APIRequest
                        .GetAsync("http://3.non.existent.domain.for.the.test/target.html")
                        .ConfigureAwait(false);
                    Assert.That(await res.TextAsync().ConfigureAwait(false), Does.Contain("Served by the proxy"));
                }
            }
            finally
            {
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("fetch-proxy.spec.ts", "should use socks proxy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseSocksProxy()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INSIDE_DOCKER")))
            {
                Assert.Ignore("official skip: connect ECONNREFUSED 127.0.0.1:<port>");
            }

            EnsureServer();
            await using MockSocksProxy socks = new MockSocksProxy();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "socks5://localhost:" + socks.Port.ToString(CultureInfo.InvariantCulture),
                }
            }).ConfigureAwait(false);
            try
            {
                IAPIResponse response = await request.GetAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await response.TextAsync().ConfigureAwait(false), Does.Contain("Served by the SOCKS proxy"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("fetch-proxy.spec.ts", "should send correct ALPN protocol to HTTPS proxy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendCorrectAlpnProtocolToHttpsProxy()
        {
            EnsureServer();
            await using OfficialHttpsAlpnProxy proxy = OfficialHttpsAlpnProxy.Start();
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "https://localhost:" + proxy.Port.ToString(CultureInfo.InvariantCulture),
                },
                IgnoreHTTPSErrors = true
            }).ConfigureAwait(false);
            try
            {
                Assert.CatchAsync(() => request.GetAsync(EmptyPage));
                Assert.That(proxy.OfferedProtocols, Does.Contain("http/1.1"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
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
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }
        }
    }
}
