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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-cookies.spec.ts</c> parity for
    /// <see cref="IBrowserContext.CookiesAsync()"/>.
    /// Skipped (official <c>it.skip</c>):
    /// <c>should support requestStorageAccess</c> on Chromium
    /// (requestStorageAccess API is not available in Chromium).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextCookiesParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string Hostname = "localhost";
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19830;
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
                    Hostname = "localhost";
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
                Hostname = "localhost";
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
            TestServerSetup.Server?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should return no cookies in pristine browser context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnNoCookiesInPristineBrowserContext()
        {
            EnsureServer();
            Assert.That(await _context.CookiesAsync().ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should get a cookie @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGetACookie()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string documentCookie = await _page.EvaluateAsync<string>(
                @"() => {
    document.cookie = 'username=John Doe';
    return document.cookie;
  }").ConfigureAwait(false);
            Assert.That(documentCookie, Is.EqualTo("username=John Doe"));
            AssertCookiesEqual(
                await _context.CookiesAsync().ConfigureAwait(false),
                new[]
                {
                    Expected("username", "John Doe", Hostname, "/", -1, false, false, DefaultSameSiteCookieValue()),
                });
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should get a non-session cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGetANonSessionCookie()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            double date = new DateTime(2038, 1, 1, 0, 0, 0, DateTimeKind.Local)
                .ToUniversalTime()
                .Subtract(DateTime.UnixEpoch)
                .TotalMilliseconds;
            string documentCookie = await _page.EvaluateAsync<string>(
                @"timestamp => {
    const date = new Date(timestamp);
    document.cookie = `username=John Doe;expires=${date.toUTCString()}`;
    return document.cookie;
  }",
                date).ConfigureAwait(false);
            Assert.That(documentCookie, Is.EqualTo("username=John Doe"));
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            AssertCookie(
                cookies[0],
                "username",
                "John Doe",
                Hostname,
                "/",
                cookies[0].Expires,
                false,
                false,
                DefaultSameSiteCookieValue());
            const double fourHundredDays = 1000d * 60 * 60 * 24 * 400;
            const double fiveMinutes = 1000d * 60 * 5;
            Assert.That(
                cookies[0].Expires,
                Is.GreaterThan((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + fourHundredDays - fiveMinutes) / 1000d));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should allow adding cookies with >400 days expiration")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowAddingCookiesWithMoreThan400DaysExpiration()
        {
            EnsureServer();
            double expire = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d) + (401d * 24 * 3600);
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "username",
                    Value = "John Doe",
                    Domain = Hostname,
                    Path = "/",
                    Expires = (float?)expire,
                    HttpOnly = false,
                    Secure = false,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            Assert.That(cookies[0].Name, Is.EqualTo("username"));
            Assert.That(cookies[0].Value, Is.EqualTo("John Doe"));
            Assert.That(cookies[0].Domain, Is.EqualTo(Hostname));
            Assert.That(cookies[0].Path, Is.EqualTo("/"));
            Assert.That(cookies[0].Expires, Is.GreaterThan(0d));
            Assert.That(cookies[0].Expires, Is.LessThanOrEqualTo(expire));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should properly report httpOnly cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlyReportHttpOnlyCookie()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Set-Cookie"] = "name=value;HttpOnly; Path=/";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            Assert.That(cookies[0].HttpOnly, Is.True);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should properly report \"Strict\" sameSite cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlyReportStrictSameSiteCookie()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Set-Cookie"] = "name=value;SameSite=Strict";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            Assert.That(cookies[0].SameSite, Is.EqualTo(SameSiteAttribute.Strict));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should properly report \"Lax\" sameSite cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlyReportLaxSameSiteCookie()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Set-Cookie"] = "name=value;SameSite=Lax";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            Assert.That(cookies[0].SameSite, Is.EqualTo(SameSiteAttribute.Lax));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should get multiple cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGetMultipleCookies()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string documentCookie = await _page.EvaluateAsync<string>(
                @"() => {
    document.cookie = 'username=John Doe';
    document.cookie = 'password=1234';
    return document.cookie.split('; ').sort().join('; ');
  }").ConfigureAwait(false);
            Assert.That(documentCookie, Is.EqualTo("password=1234; username=John Doe"));
            Dictionary<string, BrowserContextCookiesResult> cookies =
                (await _context.CookiesAsync().ConfigureAwait(false))
                .ToDictionary(c => c.Name, StringComparer.Ordinal);
            AssertCookie(
                cookies["password"],
                "password",
                "1234",
                Hostname,
                "/",
                -1,
                false,
                false,
                DefaultSameSiteCookieValue());
            AssertCookie(
                cookies["username"],
                "username",
                "John Doe",
                Hostname,
                "/",
                -1,
                false,
                false,
                DefaultSameSiteCookieValue());
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should get cookies from multiple urls")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGetCookiesFromMultipleUrls()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = "https://foo.com", Name = "doggo", Value = "woofs", SameSite = SameSiteAttribute.None },
                new Cookie { Url = "https://bar.com", Name = "catto", Value = "purrs", SameSite = SameSiteAttribute.Lax },
                new Cookie { Url = "https://baz.com", Name = "birdo", Value = "tweets", SameSite = SameSiteAttribute.Lax },
            }).ConfigureAwait(false);
            Dictionary<string, BrowserContextCookiesResult> cookies =
                (await _context.CookiesAsync(new[] { "https://foo.com", "https://baz.com" }).ConfigureAwait(false))
                .ToDictionary(c => c.Name, StringComparer.Ordinal);
            Assert.That(cookies.ContainsKey("catto"), Is.False);
            AssertCookie(
                cookies["birdo"],
                "birdo",
                "tweets",
                "baz.com",
                "/",
                -1,
                false,
                true,
                SameSiteLaxOrWindowsNone());
            AssertCookie(
                cookies["doggo"],
                "doggo",
                "woofs",
                "foo.com",
                "/",
                -1,
                false,
                true,
                SameSiteAttribute.None);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should work with subdomain cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithSubdomainCookie()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Domain = ".foo.com",
                    Path = "/",
                    Name = "doggo",
                    Value = "woofs",
                    SameSite = SameSiteAttribute.Lax,
                    Secure = true,
                },
            }).ConfigureAwait(false);
            AssertCookiesEqual(
                await _context.CookiesAsync("https://foo.com").ConfigureAwait(false),
                new[]
                {
                    Expected("doggo", "woofs", ".foo.com", "/", -1, false, true, SameSiteLaxOrWindowsNone()),
                });
            AssertCookiesEqual(
                await _context.CookiesAsync("https://sub.foo.com").ConfigureAwait(false),
                new[]
                {
                    Expected("doggo", "woofs", ".foo.com", "/", -1, false, true, SameSiteLaxOrWindowsNone()),
                });
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should return cookies with empty value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnCookiesWithEmptyValue()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Set-Cookie"] = "name=;Path=/";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            Assert.That(cookies[0].Name, Is.EqualTo("name"));
            Assert.That(cookies[0].Value, Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should return secure cookies based on HTTP(S) protocol")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnSecureCookiesBasedOnHttpProtocol()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Url = "https://foo.com",
                    Name = "doggo",
                    Value = "woofs",
                    SameSite = SameSiteAttribute.Lax,
                    Secure = true,
                },
                new Cookie
                {
                    Url = "http://foo.com",
                    Name = "catto",
                    Value = "purrs",
                    SameSite = SameSiteAttribute.Lax,
                    Secure = false,
                },
            }).ConfigureAwait(false);
            Dictionary<string, BrowserContextCookiesResult> httpsCookies =
                (await _context.CookiesAsync("https://foo.com").ConfigureAwait(false))
                .ToDictionary(c => c.Name, StringComparer.Ordinal);
            AssertCookie(
                httpsCookies["catto"],
                "catto",
                "purrs",
                "foo.com",
                "/",
                -1,
                false,
                false,
                SameSiteLaxOrWindowsNone());
            AssertCookie(
                httpsCookies["doggo"],
                "doggo",
                "woofs",
                "foo.com",
                "/",
                -1,
                false,
                true,
                SameSiteLaxOrWindowsNone());
            AssertCookiesEqual(
                await _context.CookiesAsync("http://foo.com/").ConfigureAwait(false),
                new[]
                {
                    Expected("catto", "purrs", "foo.com", "/", -1, false, false, SameSiteLaxOrWindowsNone()),
                });
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should add cookies with an expiration")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddCookiesWithAnExpiration()
        {
            EnsureServer();
            double expires = Math.Floor(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d) + 3600;
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Url = "https://foo.com",
                    Name = "doggo",
                    Value = "woofs",
                    SameSite = SameSiteAttribute.None,
                    Expires = (float)expires,
                },
            }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync(new[] { "https://foo.com" }).ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            AssertCookiesEqual(
                cookies,
                new[]
                {
                    Expected("doggo", "woofs", "foo.com", "/", expires, false, true, SameSiteAttribute.None),
                });

            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Url = "https://foo.com",
                    Name = "doggo",
                    Value = "woofs",
                    SameSite = SameSiteAttribute.None,
                    Expires = 253402300799,
                },
            }).ConfigureAwait(false);

            Exception overflow = null;
            try
            {
                await _context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Url = "https://foo.com",
                        Name = "doggo",
                        Value = "woofs",
                        SameSite = SameSiteAttribute.None,
                        Expires = 253402300800,
                    },
                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                overflow = e;
            }

            Assert.That(overflow, Is.Not.Null);
            Assert.That(overflow.Message, Does.Match("Cookie should have a valid expires"));

            Exception negative = null;
            try
            {
                await _context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Url = "https://foo.com",
                        Name = "doggo",
                        Value = "woofs",
                        SameSite = SameSiteAttribute.None,
                        Expires = -42,
                    },
                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                negative = e;
            }

            Assert.That(negative, Is.Not.Null);
            Assert.That(negative.Message, Does.Match("Cookie should have a valid expires"));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should support requestStorageAccess")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportRequestStorageAccess()
        {
            EnsureServer();
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("requestStorageAccess API is not available in Chromium");
            }

            Server.SetRoute("/set-cookie.html", async http =>
            {
                http.Response.Headers["Set-Cookie"] = "name=value; Path=/";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _page.GoToAsync(CrossProcessPrefix + "/set-cookie.html").ConfigureAwait(false);
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _page.SetContentAsync("<iframe src=\"" + CrossProcessPrefix + "/empty.html\"></iframe>")
                .ConfigureAwait(false);
            IFrame frame = new List<IFrame>(_page.Frames)[1];
            if (TestConstants.IsWebKit && !TestConstants.IsWindows && !TestConstants.IsMacOSX)
            {
                Assert.That(await frame.EvaluateAsync<bool>("() => document.hasStorageAccess()").ConfigureAwait(false), Is.True);
            }
            else if (!TestConstants.IsFirefox)
            {
                Assert.That(await frame.EvaluateAsync<bool>("() => document.hasStorageAccess()").ConfigureAwait(false), Is.False);
            }
            else
            {
                Assert.That(await frame.EvaluateAsync<bool>("() => document.hasStorageAccess()").ConfigureAwait(false), Is.True);
            }

            Task<string> firstCookie = Server.WaitForRequest("/title.html", req => req.Headers["Cookie"].ToString());
            await frame.EvaluateAsync("() => fetch('/title.html')").ConfigureAwait(false);
            string firstHeader = await firstCookie.ConfigureAwait(false);
            if (TestConstants.IsFirefox)
            {
                Assert.That(firstHeader, Is.EqualTo("name=value"));
            }
            else if (TestConstants.IsWindows && TestConstants.IsWebKit)
            {
                Assert.That(firstHeader, Is.EqualTo("name=value"));
            }
            else
            {
                Assert.That(string.IsNullOrEmpty(firstHeader), Is.True);
            }

            if (TestConstants.IsFirefox)
            {
                return;
            }

            Assert.That(
                await frame.EvaluateAsync<bool>("() => document.requestStorageAccess().then(() => true, e => false)")
                    .ConfigureAwait(false),
                Is.True);
            Assert.That(await frame.EvaluateAsync<bool>("() => document.hasStorageAccess()").ConfigureAwait(false), Is.True);

            Task<string> secondCookie = Server.WaitForRequest("/title.html", req => req.Headers["Cookie"].ToString());
            await frame.EvaluateAsync("() => fetch('/title.html')").ConfigureAwait(false);
            string secondHeader = await secondCookie.ConfigureAwait(false);
            if (TestConstants.IsWebKit && !TestConstants.IsWindows && !TestConstants.IsMacOSX)
            {
                Assert.That(string.IsNullOrEmpty(secondHeader), Is.True);
            }
            else
            {
                Assert.That(secondHeader, Is.EqualTo("name=value"));
            }
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "should parse cookie with large Max-Age correctly")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldParseCookieWithLargeMaxAgeCorrectly()
        {
            EnsureServer();
            Server.SetRoute("/foobar", async http =>
            {
                http.Response.Headers["Set-Cookie"] =
                    "cookie1=value1; Path=/; Expires=Thu, 08 Sep 2270 15:06:12 GMT; Max-Age=7776000000";
                http.Response.StatusCode = 200;
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _page.GoToAsync(Prefix + "/foobar").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false),
                Is.EqualTo("cookie1=value1"));
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count, Is.EqualTo(1));
            Assert.That(cookies[0].Name, Is.EqualTo("cookie1"));
            Assert.That(cookies[0].Value, Is.EqualTo("value1"));
            Assert.That(cookies[0].Domain, Is.EqualTo(Hostname));
            Assert.That(cookies[0].Path, Is.EqualTo("/"));
            Assert.That(cookies[0].Expires, Is.TypeOf<double>());
            Assert.That(cookies[0].HttpOnly, Is.False);
            Assert.That(cookies[0].Secure, Is.False);
            Assert.That(cookies[0].SameSite, Is.EqualTo(DefaultSameSiteCookieValue()));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "iframe should inherit cookies from parent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IframeShouldInheritCookiesFromParent()
        {
            EnsureServer();
            await _page.RouteAsync("**/*", async route =>
            {
                if (route.Request.Url.Contains("sub.example.test", StringComparison.Ordinal))
                {
                    await route.FulfillAsync(new() { Body = @"
            <p id=""result""></p>
            <script>document.getElementById('result').textContent = document.cookie || 'no cookies';</script>
        ", ContentType = "text/html" }).ConfigureAwait(false);
                    return;
                }

                await route.FulfillAsync(new()
                {
                    Headers = new[]
                    {
                        new KeyValuePair<string, string>(
                            "set-cookie",
                            "testCookie=value; SameSite=Lax; Domain=example.test"),
                    },
                    ContentType = "text/html",
                    Body = @"
          <p id=""result""></p>
          <script>document.getElementById('result').textContent = document.cookie || 'no cookies';</script>
          <iframe src=""http://sub.example.test""></iframe>
      "
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await _page.GoToAsync("http://example.test").ConfigureAwait(false);
            await Assertions.Expect(_page.Locator("body")).ToContainTextAsync("testCookie=value")
                .ConfigureAwait(false);
            await Assertions.Expect(_page.FrameLocator("iframe").Locator("body"))
                .ToContainTextAsync("testCookie=value")
                .ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
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

        private static SameSiteAttribute DefaultSameSiteCookieValue()
        {
            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                return SameSiteAttribute.None;
            }

            if (TestConstants.IsFirefox)
            {
                return SameSiteAttribute.None;
            }

            return SameSiteAttribute.Lax;
        }

        private static SameSiteAttribute SameSiteLaxOrWindowsNone()
            => TestConstants.IsWebKit && TestConstants.IsWindows
                ? SameSiteAttribute.None
                : SameSiteAttribute.Lax;

        private static BrowserContextCookiesResult Expected(
            string name,
            string value,
            string domain,
            string path,
            double expires,
            bool httpOnly,
            bool secure,
            SameSiteAttribute sameSite)
            => new BrowserContextCookiesResult
            {
                Name = name,
                Value = value,
                Domain = domain,
                Path = path,
                Expires = (float)expires,
                HttpOnly = httpOnly,
                Secure = secure,
                SameSite = sameSite,
                PartitionKey = string.Empty,
            };

        private static void AssertCookiesEqual(
            IReadOnlyList<BrowserContextCookiesResult> actual,
            IReadOnlyList<BrowserContextCookiesResult> expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                AssertCookie(
                    actual[i],
                    expected[i].Name,
                    expected[i].Value,
                    expected[i].Domain,
                    expected[i].Path,
                    expected[i].Expires,
                    expected[i].HttpOnly,
                    expected[i].Secure,
                    expected[i].SameSite);
            }
        }

        private static void AssertCookie(
            BrowserContextCookiesResult actual,
            string name,
            string value,
            string domain,
            string path,
            double expires,
            bool httpOnly,
            bool secure,
            SameSiteAttribute sameSite)
        {
            Assert.That(actual.Name, Is.EqualTo(name));
            Assert.That(actual.Value, Is.EqualTo(value));
            Assert.That(actual.Domain, Is.EqualTo(domain));
            Assert.That(actual.Path, Is.EqualTo(path));
            Assert.That(actual.Expires, Is.EqualTo(expires));
            Assert.That(actual.HttpOnly, Is.EqualTo(httpOnly));
            Assert.That(actual.Secure, Is.EqualTo(secure));
            Assert.That(actual.SameSite, Is.EqualTo(sameSite));
        }
    }
}
