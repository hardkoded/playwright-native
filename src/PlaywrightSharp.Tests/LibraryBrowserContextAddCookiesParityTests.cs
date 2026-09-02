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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-add-cookies.spec.ts</c> parity for
    /// <see cref="IBrowserContext.AddCookiesAsync"/>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextAddCookiesParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string Hostname = "localhost";
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
        private static string HttpsHost = "localhost:" + TestConstants.HttpsPort.ToString(CultureInfo.InvariantCulture);

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19829;
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
                    Hostname = "localhost";
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
                Hostname = "localhost";
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

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "password", Value = "123456" },
            }).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false),
                Is.EqualTo("password=123456"));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should work with expires=-1")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithExpiresMinus1()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "username",
                    Value = "John Doe",
                    Domain = "www.example.com",
                    Path = "/",
                    Expires = -1,
                    HttpOnly = false,
                    Secure = false,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);
            await _page.RouteAsync("**/*", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html></html>" });
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            await _page.GoToAsync("https://www.example.com").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false),
                Is.EqualTo("username=John Doe"));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should add cookies with empty value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddCookiesWithEmptyValue()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "marker",
                    Value = string.Empty,
                    Domain = "www.example.com",
                    Path = "/",
                    Expires = -1,
                    HttpOnly = false,
                    Secure = false,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);
            await _page.RouteAsync("**/*", route =>
            {
                _ = route.FulfillAsync(new() { Body = "<html></html>" });
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            await _page.GoToAsync("https://www.example.com").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("() => document.cookie").ConfigureAwait(false),
                Is.EqualTo("marker="));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should set cookies with SameSite attribute and no secure attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetCookiesWithSameSiteAttributeAndNoSecureAttribute()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Domain = "foo.com", Path = "/", Name = "same-site-unset", Value = "1" },
                new Cookie { Domain = "foo.com", Path = "/", Name = "same-site-none", Value = "1", SameSite = SameSiteAttribute.None },
                new Cookie { Domain = "foo.com", Path = "/", Name = "same-site-lax", Value = "1", SameSite = SameSiteAttribute.Lax },
                new Cookie { Domain = "foo.com", Path = "/", Name = "same-site-strict", Value = "1", SameSite = SameSiteAttribute.Strict },
            }).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync(new[] { "https://foo.com" }).ConfigureAwait(false);
            Dictionary<string, BrowserContextCookiesResult> byName =
                cookies.ToDictionary(c => c.Name, StringComparer.Ordinal);

            AssertCookie(
                byName["same-site-unset"],
                "same-site-unset",
                "1",
                "foo.com",
                "/",
                -1,
                false,
                false,
                DefaultSameSiteCookieValue());

            if (DropsInsecureSameSiteNone())
            {
                Assert.That(byName.ContainsKey("same-site-none"), Is.False);
            }
            else
            {
                AssertCookie(
                    byName["same-site-none"],
                    "same-site-none",
                    "1",
                    "foo.com",
                    "/",
                    -1,
                    false,
                    false,
                    SameSiteAttribute.None);
            }

            AssertCookie(
                byName["same-site-lax"],
                "same-site-lax",
                "1",
                "foo.com",
                "/",
                -1,
                false,
                false,
                SameSiteLaxOrWindowsNone());
            AssertCookie(
                byName["same-site-strict"],
                "same-site-strict",
                "1",
                "foo.com",
                "/",
                -1,
                false,
                false,
                SameSiteStrictOrWindowsNone());
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should roundtrip cookie")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripCookie()
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
            await _context.ClearCookiesAsync().ConfigureAwait(false);
            Assert.That(await _context.CookiesAsync().ConfigureAwait(false), Is.Empty);
            await _context.AddCookiesAsync(cookies.Select(ToSetCookie)).ConfigureAwait(false);
            AssertCookiesEqual(
                NormalizeExpires(await _context.CookiesAsync().ConfigureAwait(false)),
                NormalizeExpires(cookies));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should send cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendCookieHeader()
        {
            EnsureServer();
            string cookie = string.Empty;
            Server.SetRoute("/empty.html", async http =>
            {
                cookie = http.Request.Headers["Cookie"].ToString();
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "cookie", Value = "value" },
            }).ConfigureAwait(false);
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(cookie, Is.EqualTo("cookie=value"));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should isolate cookies in browser contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateCookiesInBrowserContexts()
        {
            EnsureServer();
            IBrowserContext anotherContext = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                await _context.AddCookiesAsync(new[]
                {
                    new Cookie { Url = EmptyPage, Name = "isolatecookie", Value = "page1value" },
                }).ConfigureAwait(false);
                await anotherContext.AddCookiesAsync(new[]
                {
                    new Cookie { Url = EmptyPage, Name = "isolatecookie", Value = "page2value" },
                }).ConfigureAwait(false);

                IReadOnlyList<BrowserContextCookiesResult> cookies1 =
                    await _context.CookiesAsync().ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> cookies2 =
                    await anotherContext.CookiesAsync().ConfigureAwait(false);
                Assert.That(cookies1.Count, Is.EqualTo(1));
                Assert.That(cookies2.Count, Is.EqualTo(1));
                Assert.That(cookies1[0].Name, Is.EqualTo("isolatecookie"));
                Assert.That(cookies1[0].Value, Is.EqualTo("page1value"));
                Assert.That(cookies2[0].Name, Is.EqualTo("isolatecookie"));
                Assert.That(cookies2[0].Value, Is.EqualTo("page2value"));
            }
            finally
            {
                await DisposeQuietlyAsync(anotherContext).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should isolate session cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateSessionCookies()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers["Set-Cookie"] = "session=value";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            {
                IPage page = await _context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
            }

            {
                IPage page = await _context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> cookies =
                    await _context.CookiesAsync().ConfigureAwait(false);
                Assert.That(cookies.Count, Is.EqualTo(1));
                Assert.That(string.Join(",", cookies.Select(c => c.Value)), Is.EqualTo("value"));
            }

            {
                IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
                try
                {
                    IPage page = await context2.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                    IReadOnlyList<BrowserContextCookiesResult> cookies =
                        await context2.CookiesAsync().ConfigureAwait(false);
                    Assert.That(cookies.Count > 0 ? cookies[0].Name : null, Is.Null);
                }
                finally
                {
                    await DisposeQuietlyAsync(context2).ConfigureAwait(false);
                }
            }
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should isolate persistent cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolatePersistentCookies()
        {
            EnsureServer();
            Server.SetRoute("/setcookie.html", async http =>
            {
                http.Response.Headers["Set-Cookie"] = "persistent=persistent-value; max-age=3600";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            IPage page = await _context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/setcookie.html").ConfigureAwait(false);

            IBrowserContext context1 = _context;
            IBrowserContext context2 = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                Task<IPage> page1Task = context1.NewPageAsync();
                Task<IPage> page2Task = context2.NewPageAsync();
                IPage[] pages = await Task.WhenAll(page1Task, page2Task).ConfigureAwait(false);
                await Task.WhenAll(
                    pages[0].GoToAsync(EmptyPage),
                    pages[1].GoToAsync(EmptyPage)).ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult>[] cookieLists = await Task.WhenAll(
                    context1.CookiesAsync(),
                    context2.CookiesAsync()).ConfigureAwait(false);
                Assert.That(cookieLists[0].Count, Is.EqualTo(1));
                Assert.That(cookieLists[0][0].Name, Is.EqualTo("persistent"));
                Assert.That(cookieLists[0][0].Value, Is.EqualTo("persistent-value"));
                Assert.That(cookieLists[1].Count, Is.EqualTo(0));
            }
            finally
            {
                await DisposeQuietlyAsync(context2).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should isolate send cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateSendCookieHeader()
        {
            EnsureServer();
            string cookie = string.Empty;
            Server.SetRoute("/empty.html", async http =>
            {
                cookie = http.Request.Headers["Cookie"].ToString();
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "sendcookie", Value = "value" },
            }).ConfigureAwait(false);
            {
                IPage page = await _context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(cookie, Is.EqualTo("sendcookie=value"));
            }

            {
                IBrowserContext isolated = await _browser.NewContextAsync().ConfigureAwait(false);
                try
                {
                    IPage page = await isolated.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                    Assert.That(cookie, Is.EqualTo(string.Empty));
                }
                finally
                {
                    await DisposeQuietlyAsync(isolated).ConfigureAwait(false);
                }
            }
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should isolate cookies between launches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIsolateCookiesBetweenLaunches()
        {
            EnsureServer();
            IBrowser browser1 = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            try
            {
                IBrowserContext context1 = await browser1.NewContextAsync().ConfigureAwait(false);
                await context1.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Url = EmptyPage,
                        Name = "cookie-in-context-1",
                        Value = "value",
                        Expires = (float)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10000),
                    },
                }).ConfigureAwait(false);
            }
            finally
            {
                await DisposeQuietlyAsync(browser1).ConfigureAwait(false);
            }

            IBrowser browser2 = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            try
            {
                IBrowserContext context2 = await browser2.NewContextAsync().ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> cookies =
                    await context2.CookiesAsync().ConfigureAwait(false);
                Assert.That(cookies.Count, Is.EqualTo(0));
            }
            finally
            {
                await DisposeQuietlyAsync(browser2).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should set multiple cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetMultipleCookies()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "multiple-1", Value = "123456" },
                new Cookie { Url = EmptyPage, Name = "multiple-2", Value = "bar" },
            }).ConfigureAwait(false);
            string[] documentCookies = await _page.EvaluateAsync<string[]>(
                @"() => {
    const cookies = document.cookie.split(';');
    return cookies.map(cookie => cookie.trim()).sort();
  }").ConfigureAwait(false);
            Assert.That(documentCookies, Is.EqualTo(new[] { "multiple-1=123456", "multiple-2=bar" }));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should have |expires| set to |-1| for session cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveExpiresSetToMinus1ForSessionCookies()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "expires", Value = "123456" },
            }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync().ConfigureAwait(false);
            Assert.That(cookies[0].Expires, Is.EqualTo(-1f));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should set cookie with reasonable defaults")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetCookieWithReasonableDefaults()
        {
            EnsureServer();
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = "defaults", Value = "123456" },
            }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                (await _context.CookiesAsync().ConfigureAwait(false))
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .ToList();
            AssertCookiesEqual(
                cookies,
                new[]
                {
                    Expected(
                        "defaults",
                        "123456",
                        Hostname,
                        "/",
                        -1,
                        false,
                        false,
                        DefaultSameSiteCookieValue()),
                });
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should set a cookie with a path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetACookieWithAPath()
        {
            EnsureServer();
            await _page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Domain = Hostname,
                    Path = "/grid.html",
                    Name = "gridcookie",
                    Value = "GRID",
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);
            AssertCookiesEqual(
                await _context.CookiesAsync().ConfigureAwait(false),
                new[]
                {
                    Expected(
                        "gridcookie",
                        "GRID",
                        Hostname,
                        "/grid.html",
                        -1,
                        false,
                        false,
                        SameSiteLaxOrWindowsNone()),
                });
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("gridcookie=GRID"));
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo(string.Empty));
            await _page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("gridcookie=GRID"));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should not set a cookie with blank page URL")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotSetACookieWithBlankPageUrl()
        {
            EnsureServer();
            Exception error = null;
            try
            {
                await _context.AddCookiesAsync(new[]
                {
                    new Cookie { Url = EmptyPage, Name = "example-cookie", Value = "best" },
                    new Cookie { Url = "about:blank", Name = "example-cookie-blank", Value = "best" },
                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                error = e;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Blank page can not have cookie \"example-cookie-blank\""));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should not set a cookie on a data URL page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotSetACookieOnADataUrlPage()
        {
            Exception error = null;
            try
            {
                await _context.AddCookiesAsync(new[]
                {
                    new Cookie { Url = "data:,Hello%2C%20World!", Name = "example-cookie", Value = "best" },
                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                error = e;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(
                error.Message,
                Does.Contain("Data URL page can not have cookie \"example-cookie\""));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should default to setting secure cookie for HTTPS websites")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDefaultToSettingSecureCookieForHttpsWebsites()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            const string secureUrl = "https://example.com";
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = secureUrl, Name = "foo", Value = "bar" },
            }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync(secureUrl).ConfigureAwait(false);
            Assert.That(cookies[0].Secure, Is.True);
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should be able to set unsecure cookie for HTTP website")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToSetUnsecureCookieForHttpWebsite()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            const string httpUrl = "http://example.com";
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = httpUrl, Name = "foo", Value = "bar" },
            }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies =
                await _context.CookiesAsync(httpUrl).ConfigureAwait(false);
            Assert.That(cookies[0].Secure, Is.False);
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should set a cookie on a different domain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetACookieOnADifferentDomain()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Url = "https://www.example.com",
                    Name = "example-cookie",
                    Value = "best",
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);
            Assert.That(
                await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo(string.Empty));
            AssertCookiesEqual(
                await _context.CookiesAsync("https://www.example.com").ConfigureAwait(false),
                new[]
                {
                    Expected(
                        "example-cookie",
                        "best",
                        "www.example.com",
                        "/",
                        -1,
                        false,
                        true,
                        SameSiteLaxOrWindowsNone()),
                });
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should set cookies for a frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetCookiesForAFrame()
        {
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = Prefix, Name = "frame-cookie", Value = "value" },
            }).ConfigureAwait(false);
            await _page.EvaluateAsync(
                @"src => {
    let fulfill;
    const promise = new Promise(x => fulfill = x);
    const iframe = document.createElement('iframe');
    document.body.appendChild(iframe);
    iframe.onload = fulfill;
    iframe.src = src;
    return promise;
  }",
                Prefix + "/grid.html").ConfigureAwait(false);
            List<IFrame> frames = new List<IFrame>(_page.Frames);
            Assert.That(
                await frames[1].EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                Is.EqualTo("frame-cookie=value"));
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should allow unnamed cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowUnnamedCookies()
        {
            EnsureServer();
            Server.SetRoute("/cookies", async http =>
            {
                string header = http.Request.Headers["Cookie"].ToString();
                await http.Response.WriteAsync(
                    string.IsNullOrEmpty(header) ? "undefined-on-server" : header).ConfigureAwait(false);
            });
            await _context.AddCookiesAsync(new[]
            {
                new Cookie { Url = EmptyPage, Name = string.Empty, Value = "unnamed-via-add-cookies" },
            }).ConfigureAwait(false);
            IResponse resp = await _page.GoToAsync(Prefix + "/cookies").ConfigureAwait(false);
            if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                Assert.That(await resp.TextAsync().ConfigureAwait(false), Is.EqualTo("undefined-on-server"));
                Assert.That(
                    await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                    Is.EqualTo(string.Empty));
            }
            else
            {
                Assert.That(await resp.TextAsync().ConfigureAwait(false), Is.EqualTo("unnamed-via-add-cookies"));
                Assert.That(
                    await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                    Is.EqualTo("unnamed-via-add-cookies"));
            }

            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await _page.EvaluateAsync("() => document.cookie = '=unnamed-via-js;'").ConfigureAwait(false);
            await _context.AddCookiesAsync(
                (await _context.CookiesAsync().ConfigureAwait(false)).Select(ToSetCookie)).ConfigureAwait(false);
            if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                Assert.That(
                    await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                    Is.EqualTo(string.Empty));
            }
            else
            {
                Assert.That(
                    await _page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false),
                    Is.EqualTo("unnamed-via-js"));
            }
        }

        [PlaywrightTest("browsercontext-add-cookies.spec.ts", "should set secure cookies on secure WebSocket")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetSecureCookiesOnSecureWebSocket()
        {
            EnsureHttps();
            TaskCompletionSource<string> received = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<RequestReceivedEventArgs> handler = (_, args) =>
            {
                if (args.Request.Path == "/ws")
                {
                    received.TrySetResult(args.Request.Headers["Cookie"].ToString());
                }
            };
            HttpsServer.RequestReceived += handler;
            IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true })
                .ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
                await context.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Domain = new Uri(HttpsPrefix).Host,
                        Path = "/",
                        Name = "foo",
                        Value = "bar",
                        Secure = true,
                    },
                }).ConfigureAwait(false);
                await page.EvaluateAsync(
                    "hostname => new WebSocket(`wss://${hostname}/ws`)",
                    HttpsHost).ConfigureAwait(false);
                string cookieHeader = await received.Task.ConfigureAwait(false);
                Assert.That(cookieHeader, Is.EqualTo("foo=bar"));
            }
            finally
            {
                HttpsServer.RequestReceived -= handler;
                await DisposeQuietlyAsync(context).ConfigureAwait(false);
            }
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
                HttpsHost = "localhost:" + TestConstants.HttpsPort.ToString(CultureInfo.InvariantCulture);
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
            int basePort = 19929;
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
                    HttpsHost = "localhost:" + portText;
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

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
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

        private static SameSiteAttribute SameSiteStrictOrWindowsNone()
            => TestConstants.IsWebKit && TestConstants.IsWindows
                ? SameSiteAttribute.None
                : SameSiteAttribute.Strict;

        private static bool DropsInsecureSameSiteNone()
            => TestConstants.IsChromium
                || (TestConstants.IsWebKit && !TestConstants.IsWindows && !TestConstants.IsMacOSX);

        private static Cookie ToSetCookie(BrowserContextCookiesResult cookie)
            => new Cookie
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = cookie.Domain,
                Path = cookie.Path,
                Expires = cookie.Expires,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
                SameSite = cookie.SameSite,
                PartitionKey = string.IsNullOrEmpty(cookie.PartitionKey) ? null : cookie.PartitionKey,
            };

        private static IReadOnlyList<BrowserContextCookiesResult> NormalizeExpires(
            IReadOnlyList<BrowserContextCookiesResult> cookies)
        {
            List<BrowserContextCookiesResult> normalized = new();
            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                normalized.Add(new BrowserContextCookiesResult
                {
                    Name = cookie.Name,
                    Value = cookie.Value,
                    Domain = cookie.Domain,
                    Path = cookie.Path,
                    Expires = (float)Math.Floor(cookie.Expires),
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure,
                    SameSite = cookie.SameSite,
                    PartitionKey = cookie.PartitionKey ?? string.Empty,
                });
            }

            return normalized;
        }

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
