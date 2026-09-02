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
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/har.spec.ts</c> parity. Do not edit leftover
    /// <c>ContextHarTests</c> or <c>LaunchPersistentRecordHar*</c>.
    /// Skip Node-only <c>should populate entry startedDateTime from the
    /// browser</c> (Node event-loop busy-wait vs protocol observation).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryHarParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
        private static int ServerPort = TestConstants.Port;
        private static int HttpsPort;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19879;
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
                    ServerPort = port;
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
                ServerPort = TestConstants.Port;
            }

            await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
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
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            _ownedServer?.Reset();
            _ownedHttps?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            _ownedHttps?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("har.spec.ts", "should have version and creator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveVersionAndCreator()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("version").GetString(), Is.EqualTo("1.2"));
            Assert.That(log.GetProperty("creator").GetProperty("name").GetString(), Is.EqualTo("Playwright"));
            Assert.That(log.GetProperty("creator").GetProperty("version").GetString(), Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("har.spec.ts", "should have browser")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveBrowser()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("browser").GetProperty("name").GetString(), Is.EqualTo(BrowserName()));
            Assert.That(log.GetProperty("browser").GetProperty("version").GetString(), Is.EqualTo(_browser.Version));
        }

        [PlaywrightTest("har.spec.ts", "should have pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHavePages()
        {
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync("data:text/html,<title>Hello</title>").ConfigureAwait(false);
            await session.Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement pages = log.GetProperty("pages");
            Assert.That(pages.GetArrayLength(), Is.EqualTo(1));
            JsonElement pageEntry = pages[0];
            Assert.That(pageEntry.GetProperty("id").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(pageEntry.GetProperty("title").GetString(), Is.EqualTo("Hello"));
            DateTimeOffset started = DateTimeOffset.Parse(
                pageEntry.GetProperty("startedDateTime").GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            Assert.That(started.ToUnixTimeMilliseconds(), Is.GreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (3600L * 1000)));
            Assert.That(pageEntry.GetProperty("pageTimings").GetProperty("onContentLoad").GetDouble(), Is.GreaterThan(0));
            Assert.That(pageEntry.GetProperty("pageTimings").GetProperty("onLoad").GetDouble(), Is.GreaterThan(0));
        }

        [PlaywrightTest("har.spec.ts", "should have pages in persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHavePagesInPersistentContext()
        {
            string harPath = TempHarPath("persistent");
            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave879-ud-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserType type;
                BrowserTypeLaunchPersistentContextOptions options = new BrowserTypeLaunchPersistentContextOptions
                {
                    RecordHarPath = harPath,
                    Headless = true,
                };
                if (PlaywrightTestAttribute.IsWebkit)
                {
                    if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                    {
                        Assert.Ignore("WebKit executable not available (download skipped or failed).");
                    }

                    type = Playwright.Webkit;
                    options.ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath;
                }
                else
                {
                    if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                    {
                        Assert.Ignore("Chromium executable not available (download skipped or failed).");
                    }

                    type = Playwright.Chromium;
                    options.ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath;
                }

                IBrowserContext context = await type.LaunchPersistentContextAsync(userDataDir, options).ConfigureAwait(false);
                IPage page = context.Pages.FirstOrDefault()
                    ?? await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("data:text/html,<title>Hello</title>").ConfigureAwait(false);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                Assert.That(log.GetProperty("pages").GetArrayLength(), Is.EqualTo(1));
                JsonElement pageEntry = log.GetProperty("pages")[0];
                Assert.That(pageEntry.GetProperty("id").GetString(), Is.Not.Null.And.Not.Empty);
                Assert.That(pageEntry.GetProperty("title").GetString(), Is.EqualTo("Hello"));
            }
            finally
            {
                TryDelete(harPath);
                try
                {
                    Directory.Delete(userDataDir, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("har.spec.ts", "should include request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeRequest()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
            JsonElement entry = log.GetProperty("entries")[0];
            Assert.That(entry.GetProperty("pageref").GetString(), Is.EqualTo(log.GetProperty("pages")[0].GetProperty("id").GetString()));
            Assert.That(entry.GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(EmptyPage));
            Assert.That(entry.GetProperty("request").GetProperty("method").GetString(), Is.EqualTo("GET"));
            Assert.That(entry.GetProperty("request").GetProperty("httpVersion").GetString(), Is.EqualTo("HTTP/1.1"));
            Assert.That(entry.GetProperty("request").GetProperty("headers").GetArrayLength(), Is.GreaterThan(1));
            Assert.That(FindHeader(entry.GetProperty("request").GetProperty("headers"), "user-agent"), Is.Not.Null);
            Assert.That(entry.GetProperty("request").GetProperty("bodySize").GetInt32(), Is.EqualTo(0));
        }

        [PlaywrightTest("har.spec.ts", "should include response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeResponse()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement entry = log.GetProperty("entries")[0];
            Assert.That(entry.GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(200));
            Assert.That(entry.GetProperty("response").GetProperty("statusText").GetString(), Is.EqualTo("OK"));
            Assert.That(entry.GetProperty("response").GetProperty("httpVersion").GetString(), Is.EqualTo("HTTP/1.1"));
            Assert.That(entry.GetProperty("response").GetProperty("headers").GetArrayLength(), Is.GreaterThan(1));
            Assert.That(
                FindHeader(entry.GetProperty("response").GetProperty("headers"), "content-type"),
                Does.Contain("text/html"));
        }

        [PlaywrightTest("har.spec.ts", "should include redirectURL")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeRedirectUrl()
        {
            EnsureServer();
            Server.SetRedirect("/foo.html", "/empty.html");
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/foo.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(2));
            JsonElement entry = log.GetProperty("entries")[0];
            Assert.That(entry.GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(302));
            Assert.That(entry.GetProperty("response").GetProperty("redirectURL").GetString(), Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("har.spec.ts", "should include query params")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeQueryParams()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html?name=value").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement query = log.GetProperty("entries")[0].GetProperty("request").GetProperty("queryString");
            Assert.That(query.GetArrayLength(), Is.EqualTo(1));
            Assert.That(query[0].GetProperty("name").GetString(), Is.EqualTo("name"));
            Assert.That(query[0].GetProperty("value").GetString(), Is.EqualTo("value"));
        }

        [PlaywrightTest("har.spec.ts", "should include postData")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludePostData()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => fetch('./post', { method: 'POST', body: 'Hello' })").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement postData = log.GetProperty("entries")[1].GetProperty("request").GetProperty("postData");
            Assert.That(postData.GetProperty("mimeType").GetString(), Is.EqualTo("text/plain;charset=UTF-8"));
            Assert.That(postData.GetProperty("params").GetArrayLength(), Is.EqualTo(0));
            Assert.That(postData.GetProperty("text").GetString(), Is.EqualTo("Hello"));
        }

        [PlaywrightTest("har.spec.ts", "should include cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeCookies()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "name1", Value = "\"value1\"", Domain = "localhost", Path = "/", HttpOnly = true },
                new Cookie { Name = "name2", Value = "val\"ue2", Domain = "localhost", Path = "/", SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "name3", Value = "val=ue3", Domain = "localhost", Path = "/" },
                new Cookie { Name = "name4", Value = "val,ue4", Domain = "localhost", Path = "/" },
            }).ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement cookies = log.GetProperty("entries")[0].GetProperty("request").GetProperty("cookies");
            Assert.That(cookies.GetArrayLength(), Is.EqualTo(4));
            Assert.That(cookies[0].GetProperty("name").GetString(), Is.EqualTo("name1"));
            Assert.That(cookies[0].GetProperty("value").GetString(), Is.EqualTo("\"value1\""));
            Assert.That(cookies[1].GetProperty("name").GetString(), Is.EqualTo("name2"));
            Assert.That(cookies[1].GetProperty("value").GetString(), Is.EqualTo("val\"ue2"));
            Assert.That(cookies[2].GetProperty("name").GetString(), Is.EqualTo("name3"));
            Assert.That(cookies[2].GetProperty("value").GetString(), Is.EqualTo("val=ue3"));
            Assert.That(cookies[3].GetProperty("name").GetString(), Is.EqualTo("name4"));
            Assert.That(cookies[3].GetProperty("value").GetString(), Is.EqualTo("val,ue4"));
        }

        [PlaywrightTest("har.spec.ts", "should include set-cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeSetCookies()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name1=value1; HttpOnly");
                http.Response.Headers.Append("Set-Cookie", "name2=\"value2\"");
                http.Response.Headers.Append("Set-Cookie", "name3=value4; Path=/; Domain=example.com; Max-Age=1500");
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement cookies = log.GetProperty("entries")[0].GetProperty("response").GetProperty("cookies");
            Assert.That(cookies[0].GetProperty("name").GetString(), Is.EqualTo("name1"));
            Assert.That(cookies[0].GetProperty("value").GetString(), Is.EqualTo("value1"));
            Assert.That(cookies[0].GetProperty("httpOnly").GetBoolean(), Is.True);
            Assert.That(cookies[1].GetProperty("name").GetString(), Is.EqualTo("name2"));
            Assert.That(cookies[1].GetProperty("value").GetString(), Is.EqualTo("\"value2\""));
            DateTimeOffset expires = DateTimeOffset.Parse(
                cookies[2].GetProperty("expires").GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            Assert.That(expires.ToUnixTimeMilliseconds(), Is.GreaterThan(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        [PlaywrightTest("har.spec.ts", "should exclude API request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExcludeApiRequest()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await session.Page.APIRequest.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            List<string> urls = new List<string>();
            foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
            {
                urls.Add(entry.GetProperty("request").GetProperty("url").GetString());
            }

            Assert.That(urls, Is.EqualTo(new[] { EmptyPage }));
        }

        [PlaywrightTest("har.spec.ts", "should include binary postData")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeBinaryPostData()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await session.Page.EvaluateAsync(
                @"async () => {
                    await fetch('./post', { method: 'POST', body: new Uint8Array(Array.from(Array(16).keys())) });
                }").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement postData = log.GetProperty("entries")[1].GetProperty("request").GetProperty("postData");
            Assert.That(postData.GetProperty("mimeType").GetString(), Is.EqualTo("application/octet-stream"));
            Assert.That(postData.GetProperty("params").GetArrayLength(), Is.EqualTo(0));
            Assert.That(postData.GetProperty("text").GetString(), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("har.spec.ts", "should include form params")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeFormParams()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await session.Page.SetContentAsync(
                "<form method='POST' action='/post'><input type='text' name='foo' value='bar'><input type='number' name='baz' value='123'><input type='submit'></form>").ConfigureAwait(false);
            await session.Page.ClickAsync("input[type=submit]").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement postData = log.GetProperty("entries")[1].GetProperty("request").GetProperty("postData");
            Assert.That(postData.GetProperty("mimeType").GetString(), Is.EqualTo("application/x-www-form-urlencoded"));
            Assert.That(postData.GetProperty("params").GetArrayLength(), Is.EqualTo(2));
            Assert.That(postData.GetProperty("params")[0].GetProperty("name").GetString(), Is.EqualTo("foo"));
            Assert.That(postData.GetProperty("params")[0].GetProperty("value").GetString(), Is.EqualTo("bar"));
            Assert.That(postData.GetProperty("params")[1].GetProperty("name").GetString(), Is.EqualTo("baz"));
            Assert.That(postData.GetProperty("params")[1].GetProperty("value").GetString(), Is.EqualTo("123"));
            Assert.That(postData.GetProperty("text").GetString(), Is.EqualTo("foo=bar&baz=123"));
        }

        [PlaywrightTest("har.spec.ts", "should include set-cookies with lowercase attributes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeSetCookiesWithLowercaseAttributes()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name=value; path=/; httponly; secure; samesite=Lax");
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement cookie = log.GetProperty("entries")[0].GetProperty("response").GetProperty("cookies")[0];
            Assert.That(cookie.GetProperty("name").GetString(), Is.EqualTo("name"));
            Assert.That(cookie.GetProperty("value").GetString(), Is.EqualTo("value"));
            Assert.That(cookie.GetProperty("path").GetString(), Is.EqualTo("/"));
            Assert.That(cookie.GetProperty("httpOnly").GetBoolean(), Is.True);
            Assert.That(cookie.GetProperty("secure").GetBoolean(), Is.True);
            Assert.That(cookie.GetProperty("sameSite").GetString(), Is.EqualTo("Lax"));
        }

        [PlaywrightTest("har.spec.ts", "should skip invalid Expires")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSkipInvalidExpires()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name=value;Expires=Sat Sep 14 01:02:27 CET 2024");
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement cookie = log.GetProperty("entries")[0].GetProperty("response").GetProperty("cookies")[0];
            Assert.That(cookie.GetProperty("name").GetString(), Is.EqualTo("name"));
            Assert.That(cookie.GetProperty("value").GetString(), Is.EqualTo("value"));
            Assert.That(cookie.TryGetProperty("expires", out _), Is.False);
        }

        [PlaywrightTest("har.spec.ts", "should include set-cookies with comma")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeSetCookiesWithComma()
        {
            if (PlaywrightTestAttribute.IsWebkit)
            {
                Assert.Ignore("We get \"name1=val, ue1, name2=val, ue2\" as a header value");
            }

            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name1=val, ue1");
                http.Response.Headers.Append("Set-Cookie", "name2=val, ue2");
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement cookies = log.GetProperty("entries")[0].GetProperty("response").GetProperty("cookies");
            Assert.That(cookies[0].GetProperty("name").GetString(), Is.EqualTo("name1"));
            Assert.That(cookies[0].GetProperty("value").GetString(), Is.EqualTo("val, ue1"));
            Assert.That(cookies[1].GetProperty("name").GetString(), Is.EqualTo("name2"));
            Assert.That(cookies[1].GetProperty("value").GetString(), Is.EqualTo("val, ue2"));
        }

        [PlaywrightTest("har.spec.ts", "should include content @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeContent()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => fetch('/pptr.png').then(r => r.arrayBuffer())").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement first = log.GetProperty("entries")[0].GetProperty("response").GetProperty("content");
            Assert.That(first.TryGetProperty("encoding", out _), Is.False);
            Assert.That(first.GetProperty("mimeType").GetString(), Is.EqualTo("text/html; charset=utf-8"));
            Assert.That(first.GetProperty("text").GetString(), Does.Contain("HAR Page"));
            Assert.That(first.GetProperty("size").GetInt32(), Is.GreaterThanOrEqualTo(96));
            Assert.That(first.GetProperty("compression").GetInt32(), Is.EqualTo(0));

            JsonElement second = log.GetProperty("entries")[1].GetProperty("response").GetProperty("content");
            Assert.That(second.TryGetProperty("encoding", out _), Is.False);
            Assert.That(second.GetProperty("mimeType").GetString(), Is.EqualTo("text/css; charset=utf-8"));
            Assert.That(second.GetProperty("text").GetString(), Does.Contain("pink"));
            Assert.That(second.GetProperty("size").GetInt32(), Is.GreaterThanOrEqualTo(37));
            Assert.That(second.GetProperty("compression").GetInt32(), Is.EqualTo(0));

            JsonElement third = log.GetProperty("entries")[2].GetProperty("response").GetProperty("content");
            Assert.That(third.GetProperty("encoding").GetString(), Is.EqualTo("base64"));
            Assert.That(third.GetProperty("mimeType").GetString(), Is.EqualTo("image/png"));
            Assert.That(Convert.FromBase64String(third.GetProperty("text").GetString()).Length, Is.GreaterThan(0));
            Assert.That(third.GetProperty("size").GetInt32(), Is.GreaterThanOrEqualTo(6000));
            Assert.That(third.GetProperty("compression").GetInt32(), Is.EqualTo(0));
        }

        [PlaywrightTest("har.spec.ts", "should omit content")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitContent()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync(content: HarContentPolicy.Omit).ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => fetch('/pptr.png').then(r => r.arrayBuffer())").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement content = log.GetProperty("entries")[0].GetProperty("response").GetProperty("content");
            Assert.That(content.TryGetProperty("text", out _), Is.False);
            Assert.That(content.TryGetProperty("_file", out _), Is.False);
        }

        [PlaywrightTest("har.spec.ts", "should omit content legacy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitContentLegacy()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync(omitContent: true).ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => fetch('/pptr.png').then(r => r.arrayBuffer())").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement content = log.GetProperty("entries")[0].GetProperty("response").GetProperty("content");
            Assert.That(content.TryGetProperty("text", out _), Is.False);
            Assert.That(content.TryGetProperty("_file", out _), Is.False);
        }

        [PlaywrightTest("har.spec.ts", "should filter by glob")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterByGlob()
        {
            EnsureServer();
            string harPath = TempHarPath("glob");
            IBrowserContext context = await _browser.NewContextAsync(new() { BaseURL = Prefix, RecordHarPath = harPath, RecordHarUrlFilter = "/*.css", IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
                Assert.That(log.GetProperty("entries")[0].GetProperty("request").GetProperty("url").GetString().EndsWith("one-style.css", StringComparison.Ordinal), Is.True);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should filter by regexp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterByRegexp()
        {
            EnsureServer();
            string harPath = TempHarPath("regex");
            IBrowserContext context = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarUrlFilterRegex = new System.Text.RegularExpressions.Regex("HAR.X?HTML", System.Text.RegularExpressions.RegexOptions.IgnoreCase), IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            try
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
                Assert.That(log.GetProperty("entries")[0].GetProperty("request").GetProperty("url").GetString().EndsWith("har.html", StringComparison.Ordinal), Is.True);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should calculate time")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCalculateTime()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[0].GetProperty("time").GetDouble(), Is.GreaterThan(0));
        }

        [PlaywrightTest("har.spec.ts", "should return receive time")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnReceiveTime()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[0].GetProperty("timings").GetProperty("receive").GetDouble(), Is.GreaterThan(0));
        }

        [PlaywrightTest("har.spec.ts", "should have different hars for concurrent contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveDifferentHarsForConcurrentContexts()
        {
            await using HarSession session0 = await PageWithHarAsync("test-0.har").ConfigureAwait(false);
            await session0.Page.GoToAsync("data:text/html,<title>Zero</title>").ConfigureAwait(false);
            await session0.Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await using HarSession session1 = await PageWithHarAsync("test-1.har").ConfigureAwait(false);
            await session1.Page.GoToAsync("data:text/html,<title>One</title>").ConfigureAwait(false);
            await session1.Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            Task<JsonElement> log0Task = session0.GetLogAsync();
            Task<JsonElement> log1Task = session1.GetLogAsync();
            await Task.WhenAll(log0Task, log1Task).ConfigureAwait(false);
            JsonElement log0 = log0Task.Result;
            JsonElement log1 = log1Task.Result;
            Assert.That(log0.GetProperty("pages").GetArrayLength(), Is.EqualTo(1));
            Assert.That(log0.GetProperty("pages")[0].GetProperty("title").GetString(), Is.EqualTo("Zero"));
            Assert.That(log1.GetProperty("pages").GetArrayLength(), Is.EqualTo(1));
            Assert.That(log1.GetProperty("pages")[0].GetProperty("id").GetString(), Is.Not.EqualTo(log0.GetProperty("pages")[0].GetProperty("id").GetString()));
            Assert.That(log1.GetProperty("pages")[0].GetProperty("title").GetString(), Is.EqualTo("One"));
        }

        [PlaywrightTest("har.spec.ts", "should include secure set-cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeSecureSetCookies()
        {
            EnsureHttps();
            HttpsServer.SetRoute("/empty.html", http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name1=value1; Secure");
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement cookie = log.GetProperty("entries")[0].GetProperty("response").GetProperty("cookies")[0];
            Assert.That(cookie.GetProperty("name").GetString(), Is.EqualTo("name1"));
            Assert.That(cookie.GetProperty("value").GetString(), Is.EqualTo("value1"));
            Assert.That(cookie.GetProperty("secure").GetBoolean(), Is.True);
        }

        [PlaywrightTest("har.spec.ts", "should record request overrides")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordRequestOverrides()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.RouteAsync("**/foo", route =>
            {
                Dictionary<string, string> headers = new Dictionary<string, string>(route.Request.Headers, StringComparer.OrdinalIgnoreCase)
                {
                    ["content-type"] = "text/plain",
                    ["cookie"] = "foo=bar",
                    ["custom"] = "value",
                };
                return route.FallbackAsync(new() { Url = EmptyPage, Method = "POST", Headers = headers, PostData = System.Text.Encoding.UTF8.GetBytes("Hi!") });
            }).ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/foo").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement request = log.GetProperty("entries")[0].GetProperty("request");
            Assert.That(request.GetProperty("url").GetString(), Is.EqualTo(EmptyPage));
            Assert.That(request.GetProperty("method").GetString(), Is.EqualTo("POST"));
            Assert.That(FindHeader(request.GetProperty("headers"), "custom"), Is.EqualTo("value"));
            Assert.That(request.GetProperty("cookies").GetArrayLength(), Is.EqualTo(0));
            JsonElement postData = request.GetProperty("postData");
            Assert.That(postData.GetProperty("mimeType").GetString(), Is.EqualTo("text/plain"));
            Assert.That(postData.GetProperty("params").GetArrayLength(), Is.EqualTo(0));
            Assert.That(postData.GetProperty("text").GetString(), Is.EqualTo("Hi!"));
        }

        [PlaywrightTest("har.spec.ts", "should use attach mode for zip extension")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseAttachModeForZipExtension()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync("test.har.zip").ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => fetch('/pptr.png').then(r => r.arrayBuffer())").ConfigureAwait(false);
            IReadOnlyDictionary<string, byte[]> zip = await session.GetZipAsync().ConfigureAwait(false);
            JsonElement log = JsonDocument.Parse(zip["har.har"]).RootElement.GetProperty("log").Clone();
            Assert.That(log.GetProperty("entries")[0].GetProperty("response").GetProperty("content").TryGetProperty("text", out _), Is.False);
            string htmlKey = zip.Keys.First(key => key.EndsWith(".html", StringComparison.Ordinal));
            Assert.That(System.Text.Encoding.UTF8.GetString(zip[htmlKey]), Does.Contain("HAR Page"));
        }

        [PlaywrightTest("har.spec.ts", "should attach content")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAttachContent()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync("test.har.zip", content: HarContentPolicy.Attach).ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => fetch('/pptr.png').then(r => r.arrayBuffer())").ConfigureAwait(false);
            IReadOnlyDictionary<string, byte[]> zip = await session.GetZipAsync().ConfigureAwait(false);
            JsonElement log = JsonDocument.Parse(zip["har.har"]).RootElement.GetProperty("log").Clone();
            JsonElement first = log.GetProperty("entries")[0].GetProperty("response").GetProperty("content");
            Assert.That(first.TryGetProperty("encoding", out _), Is.False);
            Assert.That(first.GetProperty("mimeType").GetString(), Is.EqualTo("text/html; charset=utf-8"));
            Assert.That(first.GetProperty("_file").GetString(), Does.Contain("75841480e2606c03389077304342fac2c58ccb1b"));
            Assert.That(first.GetProperty("size").GetInt32(), Is.GreaterThanOrEqualTo(96));
            Assert.That(first.GetProperty("compression").GetInt32(), Is.EqualTo(0));
            JsonElement second = log.GetProperty("entries")[1].GetProperty("response").GetProperty("content");
            Assert.That(second.GetProperty("mimeType").GetString(), Is.EqualTo("text/css; charset=utf-8"));
            Assert.That(second.GetProperty("_file").GetString(), Does.Contain("79f739d7bc88e80f55b9891a22bf13a2b4e18adb"));
            JsonElement third = log.GetProperty("entries")[2].GetProperty("response").GetProperty("content");
            Assert.That(third.GetProperty("mimeType").GetString(), Is.EqualTo("image/png"));
            Assert.That(third.GetProperty("_file").GetString(), Does.Contain("a4c3a18f0bb83f5d9fe7ce561e065c36205762fa"));
            Assert.That(zip["75841480e2606c03389077304342fac2c58ccb1b.html"].Length, Is.GreaterThan(0));
            Assert.That(System.Text.Encoding.UTF8.GetString(zip["79f739d7bc88e80f55b9891a22bf13a2b4e18adb.css"]), Does.Contain("pink"));
            Assert.That(zip["a4c3a18f0bb83f5d9fe7ce561e065c36205762fa.png"].Length, Is.EqualTo(third.GetProperty("size").GetInt32()));
        }

        [PlaywrightTest("har.spec.ts", "should include sizes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeSizes()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(2));
            JsonElement html = log.GetProperty("entries")[0];
            Assert.That(html.GetProperty("request").GetProperty("url").GetString().EndsWith("har.html", StringComparison.Ordinal), Is.True);
            Assert.That(html.GetProperty("request").GetProperty("headersSize").GetInt32(), Is.GreaterThanOrEqualTo(100));
            Assert.That(html.GetProperty("response").GetProperty("bodySize").GetInt32(), Is.EqualTo(AssetSize("har.html")));
            Assert.That(html.GetProperty("response").GetProperty("headersSize").GetInt32(), Is.GreaterThanOrEqualTo(100));
            Assert.That(html.GetProperty("response").GetProperty("_transferSize").GetInt32(), Is.GreaterThanOrEqualTo(250));
            JsonElement css = log.GetProperty("entries")[1];
            Assert.That(css.GetProperty("request").GetProperty("url").GetString().EndsWith("one-style.css", StringComparison.Ordinal), Is.True);
            Assert.That(css.GetProperty("response").GetProperty("bodySize").GetInt32(), Is.EqualTo(AssetSize("one-style.css")));
            Assert.That(css.GetProperty("response").GetProperty("headersSize").GetInt32(), Is.GreaterThanOrEqualTo(100));
            Assert.That(css.GetProperty("response").GetProperty("_transferSize").GetInt32(), Is.GreaterThanOrEqualTo(150));
        }

        [PlaywrightTest("har.spec.ts", "should work with gzip compression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithGzipCompression()
        {
            EnsureServer();
            Server.EnableGzip("/simplezip.json");
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            IResponse response = await session.Page.GoToAsync(Prefix + "/simplezip.json").ConfigureAwait(false);
            Assert.That(await response.HeaderValueAsync("content-encoding").ConfigureAwait(false), Is.EqualTo("gzip"));
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
            Assert.That(log.GetProperty("entries")[0].GetProperty("response").GetProperty("content").GetProperty("compression").GetInt32(), Is.GreaterThan(4000));
        }

        [PlaywrightTest("har.spec.ts", "should report the correct _transferSize with PNG files")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportTheCorrectTransferSizeWithPngFiles()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await session.Page.SetContentAsync("<img src=\"" + Prefix + "/pptr.png\">").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[1].GetProperty("response").GetProperty("_transferSize").GetInt32(), Is.GreaterThan(AssetSize("pptr.png")));
        }

        [PlaywrightTest("har.spec.ts", "should have -1 _transferSize when its a failed request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveMinusOneTransferSizeWhenItsAFailedRequest()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            List<IRequest> failed = new List<IRequest>();
            session.Page.RequestFailed += (_, request) => failed.Add(request);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[1].GetProperty("request").GetProperty("url").GetString().EndsWith("/one-style.css", StringComparison.Ordinal), Is.True);
            Assert.That(log.GetProperty("entries")[1].GetProperty("response").GetProperty("_transferSize").GetInt32(), Is.EqualTo(-1));
        }

        [PlaywrightTest("har.spec.ts", "should record failed request headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordFailedRequestHeaders()
        {
            EnsureServer();
            Server.SetRoute("/har.html", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            try
            {
                await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
            }

            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[0].GetProperty("response").GetProperty("_failureText").GetString(), Is.Not.Null.And.Not.Empty);
            JsonElement request = log.GetProperty("entries")[0].GetProperty("request");
            Assert.That(request.GetProperty("url").GetString().EndsWith("/har.html", StringComparison.Ordinal), Is.True);
            Assert.That(request.GetProperty("method").GetString(), Is.EqualTo("GET"));
            Assert.That(FindHeader(request.GetProperty("headers"), "user-agent"), Is.Not.Null);
        }

        [PlaywrightTest("har.spec.ts", "should record failed request overrides")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordFailedRequestOverrides()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.RouteAsync("**/foo", route =>
            {
                Dictionary<string, string> headers = new Dictionary<string, string>(route.Request.Headers, StringComparer.OrdinalIgnoreCase)
                {
                    ["content-type"] = "text/plain",
                    ["cookie"] = "foo=bar",
                    ["custom"] = "value",
                };
                return route.FallbackAsync(new() { Url = EmptyPage, Method = "POST", Headers = headers, PostData = System.Text.Encoding.UTF8.GetBytes("Hi!") });
            }).ConfigureAwait(false);
            try
            {
                await session.Page.GoToAsync(Prefix + "/foo").ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
            }

            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[0].GetProperty("response").GetProperty("_failureText").GetString(), Is.Not.Null.And.Not.Empty);
            JsonElement request = log.GetProperty("entries")[0].GetProperty("request");
            Assert.That(request.GetProperty("url").GetString(), Is.EqualTo(EmptyPage));
            Assert.That(request.GetProperty("method").GetString(), Is.EqualTo("POST"));
            Assert.That(FindHeader(request.GetProperty("headers"), "custom"), Is.EqualTo("value"));
            Assert.That(request.GetProperty("cookies").GetArrayLength(), Is.EqualTo(0));
            JsonElement postData = request.GetProperty("postData");
            Assert.That(postData.GetProperty("mimeType").GetString(), Is.EqualTo("text/plain"));
            Assert.That(postData.GetProperty("text").GetString(), Is.EqualTo("Hi!"));
        }

        [PlaywrightTest("har.spec.ts", "should report the correct request body size")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportTheCorrectRequestBodySize()
        {
            EnsureServer();
            Server.SetRoute("/api", _ => Task.CompletedTask);
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Task.WhenAll(
                session.Page.WaitForResponseAsync(Prefix + "/api1"),
                session.Page.EvaluateAsync("() => { void fetch('/api1', { method: 'POST', body: 'abc123' }); }")).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[1].GetProperty("request").GetProperty("bodySize").GetInt32(), Is.EqualTo(6));
        }

        [PlaywrightTest("har.spec.ts", "should report the correct request body size when the bodySize is 0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportTheCorrectRequestBodySizeWhenTheBodySizeIs0()
        {
            EnsureServer();
            Server.SetRoute("/api", _ => Task.CompletedTask);
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Task.WhenAll(
                session.Page.WaitForResponseAsync(Prefix + "/api2"),
                session.Page.EvaluateAsync("() => { void fetch('/api2', { method: 'POST', body: '' }); }")).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[1].GetProperty("request").GetProperty("bodySize").GetInt32(), Is.EqualTo(0));
        }

        [PlaywrightTest("har.spec.ts", "should report the correct response body size when the bodySize is 0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportTheCorrectResponseBodySizeWhenTheBodySizeIs0()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            IResponse response = await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await response.FinishedAsync().ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[0].GetProperty("response").GetProperty("bodySize").GetInt32(), Is.EqualTo(0));
        }

        [PlaywrightTest("har.spec.ts", "should have popup requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHavePopupRequests()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await session.Page.SetContentAsync("<a target=_blank href=\"" + Prefix + "/one-style.html\">yo</a>").ConfigureAwait(false);
            Task<IPage> popupTask = session.Page.WaitForEventAsync(PageEvent.Popup);
            await Task.WhenAll(popupTask, session.Page.ClickAsync("a")).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("pages").GetArrayLength(), Is.EqualTo(2));
            string popupId = log.GetProperty("pages")[1].GetProperty("id").GetString();
            List<JsonElement> entries = new List<JsonElement>();
            foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
            {
                if (entry.TryGetProperty("pageref", out JsonElement pageRef)
                    && pageRef.GetString() == popupId)
                {
                    entries.Add(entry);
                }
            }

            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(Prefix + "/one-style.html"));
            Assert.That(entries[0].GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(200));
            Assert.That(entries[1].GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(Prefix + "/one-style.css"));
            Assert.That(entries[1].GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(200));
        }

        [PlaywrightTest("har.spec.ts", "should not contain internal pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotContainInternalPages()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name=value");
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IEnumerable<BrowserContextCookiesResult> cookies = await session.Context.GetCookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Count(), Is.EqualTo(1));
            await session.Context.StorageStateAsync().ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("pages").GetArrayLength(), Is.EqualTo(1));
        }

        [PlaywrightTest("har.spec.ts", "should have connection details")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveConnectionDetails()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement entry = log.GetProperty("entries")[0];
            Assert.That(entry.GetProperty("serverIPAddress").GetString(), Does.Match(@"^127\.0\.0\.1|\[::1\]"));
            Assert.That(entry.GetProperty("_serverPort").GetInt32(), Is.EqualTo(ServerPort));
            Assert.That(entry.GetProperty("_securityDetails").EnumerateObject().Any(), Is.False);
        }

        [PlaywrightTest("har.spec.ts", "should have security details")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveSecurityDetails()
        {
            EnsureHttps();
            string apiHarPath = TempHarPath("api");
            try
            {
                await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
                await session.Context.APIRequest.Tracing.StartHarAsync(apiHarPath).ConfigureAwait(false);
                await session.Page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
                await session.Page.APIRequest.GetAsync(HttpsEmptyPage).ConfigureAwait(false);
                await session.Context.APIRequest.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
                Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
                JsonElement entry = log.GetProperty("entries")[0];
                Assert.That(entry.GetProperty("serverIPAddress").GetString(), Does.Match(@"^127\.0\.0\.1|\[::1\]"));
                Assert.That(entry.GetProperty("_serverPort").GetInt32(), Is.EqualTo(HttpsPort));
                AssertSecurityDetails(entry.GetProperty("_securityDetails"), api: false);
                JsonElement apiLog = ReadLog(apiHarPath);
                Assert.That(apiLog.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
                AssertSecurityDetails(apiLog.GetProperty("entries")[0].GetProperty("_securityDetails"), api: true);
            }
            finally
            {
                TryDelete(apiHarPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should have connection details for redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveConnectionDetailsForRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/foo.html", "/empty.html");
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/foo.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(2));
            JsonElement detailsFoo = log.GetProperty("entries")[0];
            if (PlaywrightTestAttribute.IsWebkit)
            {
                Assert.That(detailsFoo.TryGetProperty("serverIPAddress", out _), Is.False);
                Assert.That(detailsFoo.TryGetProperty("_serverPort", out _), Is.False);
            }
            else
            {
                Assert.That(detailsFoo.GetProperty("serverIPAddress").GetString(), Does.Match(@"^127\.0\.0\.1|\[::1\]"));
                Assert.That(detailsFoo.GetProperty("_serverPort").GetInt32(), Is.EqualTo(ServerPort));
            }

            JsonElement detailsEmpty = log.GetProperty("entries")[1];
            Assert.That(detailsEmpty.GetProperty("serverIPAddress").GetString(), Does.Match(@"^127\.0\.0\.1|\[::1\]"));
            Assert.That(detailsEmpty.GetProperty("_serverPort").GetInt32(), Is.EqualTo(ServerPort));
        }

        [PlaywrightTest("har.spec.ts", "should have connection details for failed requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveConnectionDetailsForFailedRequests()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement entry = log.GetProperty("entries")[0];
            Assert.That(entry.GetProperty("serverIPAddress").GetString(), Does.Match(@"^127\.0\.0\.1|\[::1\]"));
            Assert.That(entry.GetProperty("_serverPort").GetInt32(), Is.EqualTo(ServerPort));
        }

        [PlaywrightTest("har.spec.ts", "should return server address directly from response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnServerAddressDirectlyFromResponse()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            try
            {
                IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                ResponseServerAddrResult addr = await response.ServerAddrAsync().ConfigureAwait(false);
                Assert.That(addr.IpAddress, Does.Match(@"^127\.0\.0\.1|\[::1\]"));
                Assert.That(addr.Port, Is.EqualTo(ServerPort));
            }
            finally
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("har.spec.ts", "should return security details directly from response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnSecurityDetailsDirectlyFromResponse()
        {
            EnsureHttps();
            await using IBrowserContext context = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            ResponseSecurityDetailsResult details = await response.SecurityDetailsAsync().ConfigureAwait(false);
            AssertBrowserSecurityDetails(details);
        }

        [PlaywrightTest("har.spec.ts", "should contain http2 for http2 requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldContainHttp2ForHttp2Requests()
        {
            EnsureHttps();
            await using Http2Host host = await Http2Host.StartAsync().ConfigureAwait(false);
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(host.Url).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(log.GetProperty("entries")[0].GetProperty("request").GetProperty("httpVersion").GetString(), Is.EqualTo("HTTP/2.0"));
            Assert.That(log.GetProperty("entries")[0].GetProperty("response").GetProperty("httpVersion").GetString(), Is.EqualTo("HTTP/2.0"));
            Assert.That(log.GetProperty("entries")[0].GetProperty("response").GetProperty("content").GetProperty("text").GetString(), Is.EqualTo("<h1>Hello World</h1>"));
        }

        [PlaywrightTest("har.spec.ts", "should filter favicon and favicon redirects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterFaviconAndFaviconRedirects()
        {
            Assert.Ignore("headless browsers, except firefox, do not request favicons");
        }

        [PlaywrightTest("har.spec.ts", "should correctly record API request cookies with equals sign in value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCorrectlyRecordApiRequestCookiesWithEqualsSignInValue()
        {
            EnsureServer();
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("request");
            try
            {
                await context.APIRequest.Tracing.StartHarAsync(harPath).ConfigureAwait(false);
                string url = Prefix + "/simple.json";
                await context.APIRequest.GetAsync(url, new()
                {
                    Headers = new[]
                {
                    new KeyValuePair<string, string>("cookie", "token=abc=xyz; other=val"),
                }
                }).ConfigureAwait(false);
                await context.APIRequest.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                JsonElement cookies = log.GetProperty("entries")[0].GetProperty("request").GetProperty("cookies");
                Assert.That(cookies[0].GetProperty("name").GetString(), Is.EqualTo("token"));
                Assert.That(cookies[0].GetProperty("value").GetString(), Is.EqualTo("abc=xyz"));
                Assert.That(cookies[1].GetProperty("name").GetString(), Is.EqualTo("other"));
                Assert.That(cookies[1].GetProperty("value").GetString(), Is.EqualTo("val"));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should respect minimal mode for API Requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectMinimalModeForApiRequests()
        {
            EnsureServer();
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("request-min");
            try
            {
                await context.APIRequest.Tracing.StartHarAsync(harPath, mode: HarMode.Minimal).ConfigureAwait(false);
                await context.APIRequest.PostAsync(Prefix + "/simple.json", new()
                {
                    Headers = new[]
                {
                    new KeyValuePair<string, string>("cookie", "a=b; c=d"),
                },
                    DataObject = new { foo = "bar" }
                }).ConfigureAwait(false);
                await context.APIRequest.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement entries = ReadLog(harPath).GetProperty("entries");
                Assert.That(entries.GetArrayLength(), Is.EqualTo(1));
                JsonElement entry = entries[0];
                Assert.That(entry.GetProperty("timings").GetProperty("receive").GetInt32(), Is.EqualTo(-1));
                Assert.That(entry.GetProperty("timings").GetProperty("send").GetInt32(), Is.EqualTo(-1));
                Assert.That(entry.GetProperty("timings").GetProperty("wait").GetInt32(), Is.EqualTo(-1));
                Assert.That(entry.TryGetProperty("serverIPAddress", out _), Is.False);
                Assert.That(entry.TryGetProperty("_serverPort", out _), Is.False);
                Assert.That(entry.GetProperty("request").GetProperty("cookies").GetArrayLength(), Is.EqualTo(0));
                Assert.That(entry.GetProperty("request").GetProperty("bodySize").GetInt32(), Is.EqualTo(-1));
                Assert.That(entry.GetProperty("response").GetProperty("bodySize").GetInt32(), Is.EqualTo(-1));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should include redirects from API request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeRedirectsFromApiRequest()
        {
            EnsureServer();
            Server.SetRedirect("/redirect-me", "/simple.json");
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("request-redirect");
            try
            {
                await context.APIRequest.Tracing.StartHarAsync(harPath).ConfigureAwait(false);
                await context.APIRequest.PostAsync(Prefix + "/redirect-me", new()
                {
                    Headers = new[]
                {
                    new KeyValuePair<string, string>("cookie", "a=b; c=d"),
                },
                    DataObject = new { foo = "bar" }
                }).ConfigureAwait(false);
                await context.APIRequest.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(2));
                Assert.That(log.GetProperty("entries")[0].GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(Prefix + "/redirect-me"));
                Assert.That(log.GetProperty("entries")[1].GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(Prefix + "/simple.json"));
                Assert.That(log.GetProperty("entries")[0].TryGetProperty("timings", out _), Is.True);
                Assert.That(log.GetProperty("entries")[1].TryGetProperty("timings", out _), Is.True);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should not hang on resources served from cache")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangOnResourcesServedFromCache()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", async http =>
            {
                http.Response.Headers["Content-Type"] = "text/css";
                http.Response.Headers["Cache-Control"] = "public, max-age=10031518";
                await http.Response.WriteAsync("body { background: red }").ConfigureAwait(false);
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            await session.Page.GoToAsync(Prefix + "/har.html").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            int css = 0;
            foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
            {
                if (entry.GetProperty("request").GetProperty("url").GetString().EndsWith("one-style.css", StringComparison.Ordinal))
                {
                    css++;
                }
            }

            Assert.That(css, Is.EqualTo(2));
        }

        [PlaywrightTest("har.spec.ts", "should not hang on slow chunked response")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangOnSlowChunkedResponse()
        {
            EnsureServer();
            string html =
                "<script>window.receivedFirstData = new Promise(f => { setTimeout(() => { var x = new XMLHttpRequest(); x.open('GET', 'slow.txt'); x.onprogress = () => f(); x.send(); }, 0); });</script>";
            Server.SetRoute("/empty.html", async http =>
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(html);
                http.Response.ContentType = "text/html";
                http.Response.ContentLength = bytes.Length;
                await http.Response.Body.WriteAsync(bytes).ConfigureAwait(false);
            });
            TaskCompletionSource<bool> hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/slow.txt", async http =>
            {
                Microsoft.AspNetCore.Server.Kestrel.Core.Features.IHttpMinResponseDataRateFeature minRate =
                    http.Features.Get<Microsoft.AspNetCore.Server.Kestrel.Core.Features.IHttpMinResponseDataRateFeature>();
                if (minRate != null)
                {
                    minRate.MinDataRate = null;
                }

                http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                http.Response.Headers["Content-Type"] = "text/plain";
                // Node res.write('begin') is enough for Chromium XHR onprogress.
                // Kestrel/WebKit need a full TCP window before the renderer sees bytes.
                byte[] first = System.Text.Encoding.ASCII.GetBytes("begin" + new string(' ', 16384));
                http.Response.ContentLength = first.Length;
                await http.Response.StartAsync().ConfigureAwait(false);
                await http.Response.Body.WriteAsync(first).ConfigureAwait(false);
                await http.Response.Body.FlushAsync().ConfigureAwait(false);
                await hold.Task.ConfigureAwait(false);
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => window.receivedFirstData").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            hold.TrySetResult(true);
            Assert.That(log.GetProperty("browser").GetProperty("name").GetString(), Is.EqualTo(BrowserName()));
            Assert.That(log.GetProperty("browser").GetProperty("version").GetString(), Is.EqualTo(_browser.Version));
        }

        [PlaywrightTest("har.spec.ts", "should close the context when saving the har fails")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCloseTheContextWhenSavingTheHarFails()
        {
            EnsureServer();
            string filePath = Path.Combine(Path.GetTempPath(), "pwsharp-wave879-not-a-directory-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(filePath, "data");
            string harPath = Path.Combine(filePath, "test.har");
            IBrowserContext context = await _browser.NewContextAsync(new() { RecordHarPath = harPath }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            TaskCompletionSource<bool> closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Close += (_, _) => closed.TrySetResult(true);
            Exception error = Assert.CatchAsync(async () => await context.CloseAsync().ConfigureAwait(false));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message + error.InnerException?.Message, Does.Match("ENOTDIR|ENOENT|EEXIST|not a directory|cannot find the path|already exists").IgnoreCase);
            await closed.Task.ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
            TryDelete(filePath);
        }

        [PlaywrightTest("har.spec.ts", "should support HAR larger than 512MB")]
        [Test]
        [Timeout(600_000)]
        public async Task ShouldSupportHarLargerThan512Mb()
        {
            if (PlaywrightTestAttribute.IsWebkit)
            {
                Assert.Ignore("serializer is browser-agnostic; one browser is enough");
            }

            EnsureServer();
            string harPath = TempHarPath("large");
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            try
            {
                await context.APIRequest.Tracing.StartHarAsync(harPath).ConfigureAwait(false);
                string body = new string('a', 20 * 1024 * 1024);
                Server.SetRoute("/large", async http =>
                {
                    http.Response.ContentType = "text/plain";
                    await http.Response.WriteAsync(body).ConfigureAwait(false);
                });
                for (int i = 0; i < 30; i++)
                {
                    await context.APIRequest.GetAsync(Prefix + "/large").ConfigureAwait(false);
                }

                await context.APIRequest.Tracing.StopHarAsync().ConfigureAwait(false);
                FileInfo stats = new FileInfo(harPath);
                Assert.That(stats.Length, Is.GreaterThan(512L * 1024 * 1024));
                byte[] head = new byte[64];
                byte[] tail = new byte[64];
                using (FileStream stream = File.OpenRead(harPath))
                {
                    stream.Read(head, 0, 64);
                    stream.Seek(-64, SeekOrigin.End);
                    stream.Read(tail, 0, 64);
                }

                Assert.That(System.Text.Encoding.UTF8.GetString(head), Does.Match(@"^\{\s*""log""\s*:\s*\{"));
                Assert.That(System.Text.Encoding.UTF8.GetString(tail), Does.Match(@"\}\s*\}\s*$"));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should record resource type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordResourceType()
        {
            EnsureServer();
            Server.SetRoute("/resource-types.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(
                    "<link rel='stylesheet' href='/resource-type-stylesheet.css'><script src='/resource-type-script.js'></script><img src='/resource-type-image.png'>").ConfigureAwait(false);
            });
            Server.SetRoute("/resource-type-stylesheet.css", async http =>
            {
                http.Response.ContentType = "text/css";
                await http.Response.WriteAsync("@font-face { font-family: 'iconfont'; src: url('/resource-type-font.woff2') format('woff2'); } body { font-family: 'iconfont'; }").ConfigureAwait(false);
            });
            Server.SetRoute("/resource-type-font.woff2", async http =>
            {
                byte[] font = await File.ReadAllBytesAsync(TestUtils.GetWebServerFile("webfont/iconfont.woff2")).ConfigureAwait(false);
                http.Response.ContentType = "font/woff2";
                await http.Response.Body.WriteAsync(font).ConfigureAwait(false);
            });
            Server.SetRoute("/resource-type-script.js", async http =>
            {
                http.Response.ContentType = "application/javascript";
                await http.Response.WriteAsync("window.__loaded = true;").ConfigureAwait(false);
            });
            Server.SetRoute("/resource-type-image.png", async http =>
            {
                byte[] png = await File.ReadAllBytesAsync(TestUtils.GetWebServerFile("pptr.png")).ConfigureAwait(false);
                http.Response.ContentType = "image/png";
                await http.Response.Body.WriteAsync(png).ConfigureAwait(false);
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            Task<IRequest> fontTask = session.Page.WaitForRequestAsync("**/resource-type-font.woff2");
            await session.Page.GoToAsync(Prefix + "/resource-types.html").ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => document.fonts.load('16px iconfont')").ConfigureAwait(false);
            await fontTask.ConfigureAwait(false);
            await session.Page.EvaluateAsync("() => fetch('/resource-type-fetch').catch(() => {})").ConfigureAwait(false);
            await session.Page.EvaluateAsync(
                @"() => new Promise(resolve => {
                    const xhr = new XMLHttpRequest();
                    xhr.open('GET', '/resource-type-xhr');
                    xhr.onloadend = () => resolve();
                    xhr.send();
                })").ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Dictionary<string, string> types = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
            {
                types[entry.GetProperty("request").GetProperty("url").GetString()] = entry.GetProperty("_resourceType").GetString();
            }

            Assert.That(types[Prefix + "/resource-types.html"], Is.EqualTo("document"));
            Assert.That(types[Prefix + "/resource-type-stylesheet.css"], Is.EqualTo("stylesheet"));
            Assert.That(types[Prefix + "/resource-type-script.js"], Is.EqualTo("script"));
            Assert.That(types[Prefix + "/resource-type-image.png"], Is.EqualTo("image"));
            Assert.That(types[Prefix + "/resource-type-font.woff2"], Is.EqualTo("font"));
            Assert.That(types[Prefix + "/resource-type-fetch"], Is.EqualTo("fetch"));
            Assert.That(types[Prefix + "/resource-type-xhr"], Is.EqualTo("xhr"));
        }

        [PlaywrightTest("har.spec.ts", "should record a HAR with options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordAHarWithOptions()
        {
            EnsureServer();
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("tracing");
            try
            {
                await context.Tracing.StartHarAsync(harPath, mode: HarMode.Minimal, url: "**/one-style.css").ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
                Assert.That(log.GetProperty("entries")[0].GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(Prefix + "/one-style.css"));
                Assert.That(log.GetProperty("entries")[0].GetProperty("request").GetProperty("bodySize").GetInt32(), Is.EqualTo(-1));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should record a HAR for a context APIRequestContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordAHarForAContextApiRequestContext()
        {
            EnsureServer();
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("request-full");
            try
            {
                await context.APIRequest.Tracing.StartHarAsync(harPath).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                string url = Prefix + "/simple.json";
                IAPIResponse response = await context.APIRequest.PostAsync(url, new()
                {
                    Headers = new[]
                {
                    new KeyValuePair<string, string>("cookie", "a=b; c=d"),
                },
                    DataObject = new { foo = "bar" }
                }).ConfigureAwait(false);
                byte[] responseBody = await response.BodyAsync().ConfigureAwait(false);
                await context.APIRequest.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                Assert.That(log.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
                JsonElement entry = log.GetProperty("entries")[0];
                Assert.That(entry.GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(url));
                Assert.That(entry.GetProperty("request").GetProperty("method").GetString(), Is.EqualTo("POST"));
                Assert.That(entry.GetProperty("request").GetProperty("httpVersion").GetString(), Is.EqualTo("HTTP/1.1"));
                Assert.That(entry.GetProperty("request").GetProperty("cookies")[0].GetProperty("name").GetString(), Is.EqualTo("a"));
                Assert.That(entry.GetProperty("request").GetProperty("cookies")[0].GetProperty("value").GetString(), Is.EqualTo("b"));
                Assert.That(FindHeader(entry.GetProperty("request").GetProperty("headers"), "user-agent"), Is.Not.Null);
                Assert.That(FindHeader(entry.GetProperty("request").GetProperty("headers"), "content-type"), Is.EqualTo("application/json"));
                Assert.That(FindHeader(entry.GetProperty("request").GetProperty("headers"), "content-length"), Is.EqualTo("13"));
                Assert.That(entry.GetProperty("request").GetProperty("bodySize").GetInt32(), Is.EqualTo(13));
                Assert.That(entry.GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(200));
                Assert.That(entry.GetProperty("response").GetProperty("content").GetProperty("size").GetInt32(), Is.EqualTo(15));
                Assert.That(entry.GetProperty("response").GetProperty("content").GetProperty("text").GetString(), Is.EqualTo(System.Text.Encoding.UTF8.GetString(responseBody)));
                Assert.That(entry.GetProperty("response").GetProperty("bodySize").GetInt32(), Is.EqualTo(15));
                Assert.That(entry.GetProperty("time").GetDouble(), Is.GreaterThan(0));
                Assert.That(entry.GetProperty("serverIPAddress").GetString(), Is.Not.Null.And.Not.Empty);
                Assert.That(entry.GetProperty("_serverPort").GetInt32(), Is.EqualTo(ServerPort));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should include pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludePages()
        {
            EnsureServer();
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("tracing-pages");
            try
            {
                await context.Tracing.StartHarAsync(harPath).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/title.html").ConfigureAwait(false);
                IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
                await page2.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                Assert.That(log.GetProperty("pages").GetArrayLength(), Is.EqualTo(2));
                Assert.That(log.GetProperty("pages")[0].GetProperty("title").GetString(), Is.EqualTo("Woof-Woof"));
                Assert.That(log.GetProperty("pages")[1].GetProperty("title").GetString(), Is.EqualTo(string.Empty));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should record a zipped HAR for APIRequestContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordAZippedHarForApiRequestContext()
        {
            EnsureServer();
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("tracing", ".har.zip");
            try
            {
                await request.Tracing.StartHarAsync(harPath, new() { Content = HarContentPolicy.Attach }).ConfigureAwait(false);
                await request.GetAsync(Prefix + "/simple.json").ConfigureAwait(false);
                await request.Tracing.StopHarAsync().ConfigureAwait(false);
                IReadOnlyDictionary<string, byte[]> zip = ReadZip(harPath);
                JsonElement log = JsonDocument.Parse(zip["har.har"]).RootElement.GetProperty("log").Clone();
                bool found = false;
                foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
                {
                    if (string.Equals(entry.GetProperty("request").GetProperty("url").GetString(), Prefix + "/simple.json", StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True);
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should record correct cookie expires for APIRequestContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordCorrectCookieExpiresForApiRequestContext()
        {
            EnsureServer();
            Server.SetRoute("/set-cookie", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "name=value; Expires=Tue, 01 Jan 2030 00:00:00 GMT");
                await http.Response.WriteAsync("hello").ConfigureAwait(false);
            });
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("api", ".har.zip");
            try
            {
                await request.Tracing.StartHarAsync(harPath, new() { Content = HarContentPolicy.Attach }).ConfigureAwait(false);
                await request.GetAsync(Prefix + "/set-cookie").ConfigureAwait(false);
                await request.Tracing.StopHarAsync().ConfigureAwait(false);
                IReadOnlyDictionary<string, byte[]> zip = ReadZip(harPath);
                JsonElement log = JsonDocument.Parse(zip["har.har"]).RootElement.GetProperty("log").Clone();
                JsonElement? found = null;
                foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
                {
                    if (entry.GetProperty("request").GetProperty("url").GetString().EndsWith("/set-cookie", StringComparison.Ordinal))
                    {
                        found = entry;
                        break;
                    }
                }

                Assert.That(found.HasValue, Is.True);
                JsonElement cookies = found.Value.GetProperty("response").GetProperty("cookies");
                JsonElement? cookie = null;
                foreach (JsonElement item in cookies.EnumerateArray())
                {
                    if (string.Equals(item.GetProperty("name").GetString(), "name", StringComparison.Ordinal))
                    {
                        cookie = item;
                        break;
                    }
                }

                Assert.That(cookie.HasValue, Is.True);
                DateTimeOffset expires = DateTimeOffset.Parse(
                    cookie.Value.GetProperty("expires").GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                Assert.That(expires.UtcDateTime.Year, Is.EqualTo(2030));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should record mixed-case request content-type for APIRequestContext")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordMixedCaseRequestContentTypeForApiRequestContext()
        {
            EnsureServer();
            Server.SetRoute("/post", async http =>
            {
                await http.Response.WriteAsync("ok").ConfigureAwait(false);
            });
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("api-post", ".har.zip");
            try
            {
                await request.Tracing.StartHarAsync(harPath, new() { Content = HarContentPolicy.Attach }).ConfigureAwait(false);
                await request.PostAsync(Prefix + "/post", new()
                {
                    Headers = new[]
                    {
                        new KeyValuePair<string, string>("Content-Type", "application/json"),
                    },
                    DataByte = System.Text.Encoding.UTF8.GetBytes("{\"a\":1}")
                }).ConfigureAwait(false);
                await request.Tracing.StopHarAsync().ConfigureAwait(false);
                IReadOnlyDictionary<string, byte[]> zip = ReadZip(harPath);
                JsonElement log = JsonDocument.Parse(zip["har.har"]).RootElement.GetProperty("log").Clone();
                JsonElement? found = null;
                foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
                {
                    if (entry.GetProperty("request").GetProperty("url").GetString().EndsWith("/post", StringComparison.Ordinal))
                    {
                        found = entry;
                        break;
                    }
                }

                Assert.That(found.HasValue, Is.True);
                Assert.That(found.Value.GetProperty("request").GetProperty("postData").GetProperty("mimeType").GetString(), Is.EqualTo("application/json"));
            }
            finally
            {
                TryDelete(harPath);
            }
        }

        [PlaywrightTest("har.spec.ts", "should record a HAR with resourcesDir")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordAHarWithResourcesDir()
        {
            EnsureServer();
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("tracing-resources");
            string resourcesDir = Path.Combine(Path.GetDirectoryName(harPath), "har-resources-" + Guid.NewGuid().ToString("N"));
            try
            {
                await context.Tracing.StartHarAsync(harPath, content: HarContentPolicy.Attach, resourcesDir: resourcesDir).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
                await context.Tracing.StopHarAsync().ConfigureAwait(false);
                JsonElement log = ReadLog(harPath);
                JsonElement? styleEntry = null;
                foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
                {
                    if (entry.GetProperty("request").GetProperty("url").GetString().EndsWith("/one-style.css", StringComparison.Ordinal))
                    {
                        styleEntry = entry;
                        break;
                    }
                }

                Assert.That(styleEntry.HasValue, Is.True);
                string file = styleEntry.Value.GetProperty("response").GetProperty("content").GetProperty("_file").GetString();
                Assert.That(file, Is.Not.Null.And.Not.Empty);
                string resourcePath = Path.Combine(Path.GetDirectoryName(harPath), file);
                Assert.That(resourcePath.StartsWith(resourcesDir + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.True);
                Assert.That(File.Exists(resourcePath), Is.True);
                Assert.That(File.ReadAllText(resourcePath), Does.Contain("pink"));
            }
            finally
            {
                TryDelete(harPath);
                TryDeleteDir(resourcesDir);
            }
        }

        [PlaywrightTest("har.spec.ts", "should reject resourcesDir together with a .zip har file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectResourcesDirTogetherWithAZipHarFile()
        {
            await using IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            string harPath = TempHarPath("tracing", ".har.zip");
            string resourcesDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave879-har-resources-" + Guid.NewGuid().ToString("N"));
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => context.Tracing.StartHarAsync(harPath, content: HarContentPolicy.Attach, resourcesDir: resourcesDir));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Match("resourcesDir option is not compatible with a \\.zip har file"));
        }

        private async Task<HarSession> PageWithHarAsync(
            string outputName = "test.har",
            HarContentPolicy content = default,
            bool? omitContent = null,
            HarMode mode = default)
        {
            string harPath = TempHarPath(Path.GetFileNameWithoutExtension(outputName), Path.GetExtension(outputName));
            IBrowserContext context = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarContent = content, RecordHarOmitContent = omitContent, RecordHarMode = mode, IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            return new HarSession(context, page, harPath);
        }

        private static string BrowserName()
            => PlaywrightTestAttribute.IsWebkit ? "webkit" : "chromium";

        private static int AssetSize(string name)
            => (int)new FileInfo(TestUtils.GetWebServerFile(name)).Length;

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            string certPath = EnsurePlaywrightTestCertificate(contentRoot);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");

            Exception lastError = null;
            int basePort = 19979;
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
                    HttpsPort = port;
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new InvalidOperationException("Owned HTTPS server failed to bind official playwright-test certificate.", lastError);
        }

        private static string EnsurePlaywrightTestCertificate(string contentRoot)
        {
            string pemPath = Path.Combine(contentRoot, "playwright-test.pem");
            string keyPath = Path.Combine(contentRoot, "playwright-test-key.pem");
            if (!File.Exists(pemPath) || !File.Exists(keyPath))
            {
                throw new FileNotFoundException("Official playwright-test certificate is missing.", pemPath);
            }

            string certPath = Path.Combine(Path.GetTempPath(), "pwsharp-wave879-playwright-test-" + Guid.NewGuid().ToString("N") + ".pfx");
            using X509Certificate2 pem = X509Certificate2.CreateFromPemFile(pemPath, keyPath);
            using X509Certificate2 exportable = X509CertificateLoader.LoadPkcs12(
                pem.Export(X509ContentType.Pfx, "playwright"),
                "playwright",
                X509KeyStorageFlags.Exportable);
            File.WriteAllBytes(certPath, exportable.Export(X509ContentType.Pfx, "playwright"));
            return certPath;
        }

        private static void AssertSecurityDetails(JsonElement details, bool api)
        {
            if (PlaywrightTestAttribute.IsWebkit && !api)
            {
                Assert.That(details.GetProperty("protocol").GetString(), Is.EqualTo("TLS 1.3"));
                Assert.That(details.GetProperty("subjectName").GetString(), Is.EqualTo("playwright-test"));
                Assert.That(details.GetProperty("validFrom").GetDouble(), Is.EqualTo(1691708270));
                Assert.That(details.GetProperty("validTo").GetDouble(), Is.EqualTo(2007068270));
                return;
            }

            if (api)
            {
                Assert.That(details.GetProperty("protocol").GetString(), Is.EqualTo("TLSv1.3"));
            }
            else
            {
                Assert.That(details.GetProperty("protocol").GetString(), Is.EqualTo("TLS 1.3"));
            }

            Assert.That(details.GetProperty("issuer").GetString(), Is.EqualTo("playwright-test"));
            Assert.That(details.GetProperty("subjectName").GetString(), Is.EqualTo("playwright-test"));
            Assert.That(details.GetProperty("validFrom").GetDouble(), Is.EqualTo(1691708270));
            Assert.That(details.GetProperty("validTo").GetDouble(), Is.EqualTo(2007068270));
        }

        private static void AssertBrowserSecurityDetails(ResponseSecurityDetailsResult details)
        {
            if (PlaywrightTestAttribute.IsWebkit)
            {
                Assert.That(details.Protocol, Is.EqualTo("TLS 1.3"));
                Assert.That(details.SubjectName, Is.EqualTo("playwright-test"));
                Assert.That(details.ValidFrom, Is.EqualTo(1691708270));
                Assert.That(details.ValidTo, Is.EqualTo(2007068270));
                return;
            }

            Assert.That(details.Issuer, Is.EqualTo("playwright-test"));
            Assert.That(details.Protocol, Is.EqualTo("TLS 1.3"));
            Assert.That(details.SubjectName, Is.EqualTo("playwright-test"));
            Assert.That(details.ValidFrom, Is.EqualTo(1691708270));
            Assert.That(details.ValidTo, Is.EqualTo(2007068270));
        }

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }
        }

        private static string FindHeader(JsonElement headers, string name)
        {
            foreach (JsonElement header in headers.EnumerateArray())
            {
                if (string.Equals(header.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.GetProperty("value").GetString();
                }
            }

            return null;
        }

        private static JsonElement ReadLog(string harPath)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(harPath));
            return document.RootElement.GetProperty("log").Clone();
        }

        private static IReadOnlyDictionary<string, byte[]> ReadZip(string harPath)
        {
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using ZipArchive archive = ZipFile.OpenRead(harPath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream stream = entry.Open();
                using MemoryStream memory = new MemoryStream();
                stream.CopyTo(memory);
                files[entry.FullName.Replace('\\', '/')] = memory.ToArray();
            }

            return files;
        }

        private static string TempHarPath(string prefix, string extension = ".har")
        {
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".har";
            }

            if (!extension.StartsWith('.'))
            {
                extension = "." + extension;
            }

            return Path.Combine(
                Path.GetTempPath(),
                "pwsharp-wave879-" + prefix + "-" + Guid.NewGuid().ToString("N") + extension);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }

        private static void TryDeleteDir(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
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

        private sealed class HarSession : IAsyncDisposable
        {
            private readonly string _harPath;
            private bool _closed;

            internal HarSession(IBrowserContext context, IPage page, string harPath)
            {
                Context = context;
                Page = page;
                _harPath = harPath;
            }

            internal IBrowserContext Context { get; }

            internal IPage Page { get; }

            internal async Task<JsonElement> GetLogAsync()
            {
                await CloseAsync().ConfigureAwait(false);
                return ReadLog(_harPath);
            }

            internal async Task<IReadOnlyDictionary<string, byte[]>> GetZipAsync()
            {
                await CloseAsync().ConfigureAwait(false);
                Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                using ZipArchive archive = ZipFile.OpenRead(_harPath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    using Stream stream = entry.Open();
                    using MemoryStream memory = new MemoryStream();
                    stream.CopyTo(memory);
                    files[entry.FullName.Replace('\\', '/')] = memory.ToArray();
                }

                return files;
            }

            private async Task CloseAsync()
            {
                if (!_closed)
                {
                    await Context.CloseAsync().ConfigureAwait(false);
                    _closed = true;
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (!_closed)
                {
                    try
                    {
                        await Context.CloseAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    _closed = true;
                }

                TryDelete(_harPath);
            }
        }

        private sealed class Http2Host : IAsyncDisposable
        {
            private readonly IWebHost _host;

            private Http2Host(IWebHost host, string url)
            {
                _host = host;
                Url = url;
            }

            internal string Url { get; }

            internal static async Task<Http2Host> StartAsync()
            {
                string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
                string certPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH");
                if (string.IsNullOrEmpty(certPath))
                {
                    certPath = Path.Combine(contentRoot, "playwright-test-har.pfx");
                }

                string password = Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD") ?? "playwright";
                int port = 0;
                IWebHost host = new WebHostBuilder()
                    .UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Loopback, 0, listen =>
                        {
                            listen.Protocols = HttpProtocols.Http2;
                            listen.UseHttps(certPath, password);
                        });
                    })
                    .Configure(app => app.Run(async context =>
                    {
                        context.Response.ContentType = "text/html; charset=utf-8";
                        await context.Response.WriteAsync("<h1>Hello World</h1>").ConfigureAwait(false);
                    }))
                    .Build();
                await host.StartAsync().ConfigureAwait(false);
                port = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
                    .Addresses
                    .Select(address => new Uri(address).Port)
                    .First();
                return new Http2Host(host, "https://localhost:" + port.ToString(CultureInfo.InvariantCulture));
            }

            public async ValueTask DisposeAsync()
            {
                await _host.StopAsync().ConfigureAwait(false);
                _host.Dispose();
            }
        }
    }
}
