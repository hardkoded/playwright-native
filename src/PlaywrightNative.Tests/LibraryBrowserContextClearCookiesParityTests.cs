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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-clearcookies.spec.ts</c> parity for
    /// <see cref="IBrowserContext.ClearCookiesAsync()"/>.
    /// Skipped (official <c>it.skip</c>):
    /// <c>should not transiently delete non-matching cookies when filtering</c>
    /// on WebKit Windows (cookieStore change events not supported);
    /// <c>should remove partitioned cookies by name</c> outside Chromium
    /// (CHIPS is Chromium-specific).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextClearCookiesParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsHostname = "localhost";

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19831;
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
                    await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
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
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
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
            if (_browser == null || !_browser.IsConnected)
            {
                if (_browser != null)
                {
                    await RecycleBrowserAsync().ConfigureAwait(false);
                }
                else
                {
                    _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                }
            }

            try
            {
                _context = await NewContextOrRecycleAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                _context = await _browser.NewContextAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            _ownedHttps?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should clear cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClearCookies()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "cookie1", Value = "1" },
            }).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1"));
            await _context.ClearCookiesAsync().ConfigureAwait(false);
            Assert.That(await _context.CookiesAsync().ConfigureAwait(false), Is.Empty);
            await _page.ReloadAsync().ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should isolate cookies when clearing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateCookiesWhenClearing()
        {
            EnsureServer();
            IBrowserContext anotherContext = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                await _context.AddCookiesAsync(new[]
                {
                    new Cookie { Url = EmptyPage, Name = "page1cookie", Value = "page1value" },
                }).ConfigureAwait(false);
                await anotherContext.AddCookiesAsync(new[]
                {
                    new Cookie { Url = EmptyPage, Name = "page2cookie", Value = "page2value" },
                }).ConfigureAwait(false);

                Assert.That((await _context.CookiesAsync().ConfigureAwait(false)).Count, Is.EqualTo(1));
                Assert.That((await anotherContext.CookiesAsync().ConfigureAwait(false)).Count, Is.EqualTo(1));

                await _context.ClearCookiesAsync().ConfigureAwait(false);
                Assert.That((await _context.CookiesAsync().ConfigureAwait(false)).Count, Is.EqualTo(0));
                Assert.That((await anotherContext.CookiesAsync().ConfigureAwait(false)).Count, Is.EqualTo(1));

                await anotherContext.ClearCookiesAsync().ConfigureAwait(false);
                Assert.That((await _context.CookiesAsync().ConfigureAwait(false)).Count, Is.EqualTo(0));
                Assert.That((await anotherContext.CookiesAsync().ConfigureAwait(false)).Count, Is.EqualTo(0));
            }
            finally
            {
                await DisposeQuietlyAsync(anotherContext).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should remove cookies by name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookiesByName()
        {
            EnsureServer();
            string hostname = new Uri(Prefix).Host;
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "cookie1", Value = "1", Domain = hostname, Path = "/" },
                new Cookie { Name = "cookie2", Value = "2", Domain = hostname, Path = "/" },
            }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1; cookie2=2"));
            await _context.ClearCookiesAsync("cookie1").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie2=2"));
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should remove cookies by name regex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookiesByNameRegex()
        {
            EnsureServer();
            string hostname = new Uri(Prefix).Host;
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "cookie1", Value = "1", Domain = hostname, Path = "/" },
                new Cookie { Name = "cookie2", Value = "2", Domain = hostname, Path = "/" },
            }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1; cookie2=2"));
            await _context.ClearCookiesAsync(new Regex("coo.*1")).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie2=2"));
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should remove cookies by domain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookiesByDomain()
        {
            EnsureServer();
            string hostname = new Uri(Prefix).Host;
            string crossHost = new Uri(CrossProcessPrefix).Host;
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "cookie1", Value = "1", Domain = hostname, Path = "/" },
                new Cookie { Name = "cookie2", Value = "2", Domain = crossHost, Path = "/" },
            }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1"));
            await _page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie2=2"));
            await _context.ClearCookiesAsync((string)null, crossHost).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo(string.Empty));
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1"));
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should remove cookies by path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookiesByPath()
        {
            EnsureServer();
            string hostname = new Uri(Prefix).Host;
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "cookie1", Value = "1", Domain = hostname, Path = "/api/v1" },
                new Cookie { Name = "cookie2", Value = "2", Domain = hostname, Path = "/api/v2" },
                new Cookie { Name = "cookie3", Value = "3", Domain = hostname, Path = "/" },
            }).ConfigureAwait(false);
            ServeHtml("/api/v1");
            ServeHtml("/api/v2");
            ServeHtml("/");
            await _page.GoToAsync(Prefix + "/api/v1").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1; cookie3=3"));
            await _context.ClearCookiesAsync((string)null, (string)null, "/api/v1").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie3=3"));
            await _page.GoToAsync(Prefix + "/api/v2").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie2=2; cookie3=3"));
            await _page.GoToAsync(Prefix + "/").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie3=3"));
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should remove cookies by name and domain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookiesByNameAndDomain()
        {
            EnsureServer();
            string hostname = new Uri(Prefix).Host;
            string crossHost = new Uri(CrossProcessPrefix).Host;
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "cookie1", Value = "1", Domain = hostname, Path = "/" },
                new Cookie { Name = "cookie1", Value = "1", Domain = crossHost, Path = "/" },
            }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1"));
            await _context.ClearCookiesAsync("cookie1", hostname).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo(string.Empty));
            await _page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=1"));
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should not transiently delete non-matching cookies when filtering")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotTransientlyDeleteNonMatchingCookiesWhenFiltering()
        {
            EnsureServer();
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("cookieStore change events not supported on WebKit/Windows (curl backend lacks cookie change notifications)");
            }

            string hostname = new Uri(Prefix).Host;
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "keep_me", Value = "1", Domain = hostname, Path = "/" },
                new Cookie { Name = "delete_me", Value = "2", Domain = hostname, Path = "/" },
            }).ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IJSHandle eventsHandle = await _page.EvaluateHandleAsync(
                @"(() => {
    const events = [];
    cookieStore.addEventListener('change', event => {
      for (const changed of event.changed)
        events.push(`changed ${changed.name}`);
      for (const deleted of event.deleted)
        events.push(`deleted ${deleted.name}`);
    });
    return events;
  })()").ConfigureAwait(false);
            await _context.ClearCookiesAsync("delete_me").ConfigureAwait(false);
            string[] events = null;
            for (int i = 0; i < 50; i++)
            {
                events = await eventsHandle.JsonValueAsync<string[]>().ConfigureAwait(false);
                if (events != null && events.Length == 1 && events[0] == "deleted delete_me")
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(events, Is.EqualTo(new[] { "deleted delete_me" }));
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("keep_me=1"));
        }

        [PlaywrightTest("browsercontext-clearcookies.spec.ts", "should remove partitioned cookies by name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemovePartitionedCookiesByName()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Partitioned cookies (CHIPS) are Chromium-specific");
            }

            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }

            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true })
                .ConfigureAwait(false);
            try
            {
                string partitionKey = "https://" + HttpsHostname;
                await context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Name = "delete_me",
                        Value = "1",
                        Domain = HttpsHostname,
                        Path = "/",
                        Secure = true,
                        SameSite = SameSiteAttribute.None,
                        PartitionKey = partitionKey,
                    },
                    new Cookie
                    {
                        Name = "keep_me",
                        Value = "2",
                        Domain = HttpsHostname,
                        Path = "/",
                        Secure = true,
                        SameSite = SameSiteAttribute.None,
                        PartitionKey = partitionKey,
                    },
                }).ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> before =
                    await context.CookiesAsync().ConfigureAwait(false);
                Assert.That(before.Any(c => c.Name == "delete_me" && (c.PartitionKey ?? string.Empty).Contains(HttpsHostname, StringComparison.Ordinal)), Is.True);
                Assert.That(before.Any(c => c.Name == "keep_me" && (c.PartitionKey ?? string.Empty).Contains(HttpsHostname, StringComparison.Ordinal)), Is.True);

                await context.ClearCookiesAsync("delete_me").ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> after =
                    await context.CookiesAsync().ConfigureAwait(false);
                Assert.That(after.Any(c => c.Name == "delete_me"), Is.False);
                Assert.That(after.Any(c => c.Name == "keep_me" && (c.PartitionKey ?? string.Empty).Contains(HttpsHostname, StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsHostname = "localhost";
                return;
            }

            string certPath = Path.Combine(contentRoot, "testCert.cer");
            if (!File.Exists(certPath))
            {
                return;
            }

            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19931;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
                    await https.StartAsync().ConfigureAwait(false);
                    _ownedHttps = https;
                    HttpsPrefix = "https://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    HttpsHostname = "localhost";
                    return;
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

        private static void ServeHtml(string path)
        {
            Server.SetRoute(path, async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<html></html>").ConfigureAwait(false);
            });
        }

        private async Task<IBrowserContext> NewContextOrRecycleAsync()
        {
            Task<IBrowserContext> create = _browser.NewContextAsync();
            Task finished = await Task.WhenAny(create, Task.Delay(5000)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, create))
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                return await _browser.NewContextAsync().ConfigureAwait(false);
            }

            return await create.ConfigureAwait(false);
        }

        private async Task RecycleBrowserAsync()
        {
            IBrowser previous = _browser;
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (previous != null)
            {
                await DisposeQuietlyAsync(previous).ConfigureAwait(false);
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
