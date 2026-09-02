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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-cookies-third-party.spec.ts</c> parity.
    /// Do not edit leftover cookie tests.
    /// Official skips:
    /// <c>save/load third party 'Partitioned;' cookies</c> on WebKit Linux/Windows
    /// and Firefox Juggler;
    /// <c>should be able to send third party cookies via an iframe</c> on WebKit Mac;
    /// <c>should not block third party SameSite=None cookies</c> on WebKit.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextCookiesThirdPartyParityTests : PageTestEx
    {
        private static SimpleServer _ownedHttps;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsCrossPrefix = "https://127.0.0.1:" + TestConstants.HttpsPort.ToString(CultureInfo.InvariantCulture);
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
        private static string HttpsHostname = "localhost";

        private IBrowser _browser;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        private static bool IsLinux => !TestConstants.IsWindows && !TestConstants.IsMacOSX;

        private static bool WebkitPartitions => false;

        private static bool AllowsThirdParty => TestConstants.IsFirefox;

        private static TestUrls Urls => new TestUrls();

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
            if (HttpsServer == null && TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                HttpsCrossPrefix = "https://127.0.0.1:" + TestConstants.HttpsPort.ToString(CultureInfo.InvariantCulture);
                HttpsEmptyPage = HttpsPrefix + "/empty.html";
                HttpsHostname = "localhost";
                return;
            }

            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
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

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedHttps?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "third party non-partitioned cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ThirdPartyNonPartitionedCookies()
        {
            EnsureHttps();
            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RunNonPartitionedTestAsync(page).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "save/load third party non-partitioned cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SaveLoadThirdPartyNonPartitionedCookies()
        {
            EnsureHttps();
            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            (string expectedTopLevel, string expectedThirdParty) = await RunNonPartitionedTestAsync(page).ConfigureAwait(false);

            await CheckNonPartitionedAsync(page, expectedTopLevel, expectedThirdParty).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync().ConfigureAwait(false);
            IBrowserContext context2 = await NewHttpsContextAsync().ConfigureAwait(false);
            await context2.AddCookiesAsync(ToSetCookies(cookies)).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await CheckNonPartitionedAsync(page2, expectedTopLevel, expectedThirdParty).ConfigureAwait(false);
            await context2.CloseAsync().ConfigureAwait(false);

            string storageState = await page.Context.StorageStateAsync().ConfigureAwait(false);
            IBrowserContext context3 = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageState = storageState }).ConfigureAwait(false);
            IPage page3 = await context3.NewPageAsync().ConfigureAwait(false);
            await CheckNonPartitionedAsync(page3, expectedTopLevel, expectedThirdParty).ConfigureAwait(false);
            await context3.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "third party 'Partitioned;' cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ThirdPartyPartitionedCookies()
        {
            EnsureHttps();
            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RunPartitionedTestAsync(page).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "save/load third party 'Partitioned;' cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SaveLoadThirdPartyPartitionedCookies()
        {
            EnsureHttps();
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox cookie partitioning is disabled in Firefox(Juggler).");
            }

            if (TestConstants.IsWebKit && !TestConstants.IsMacOSX)
            {
                Assert.Ignore("Linux and Windows WebKit builds do not partition third-party cookies at all.");
            }

            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RunPartitionedTestAsync(page).ConfigureAwait(false);
            await CheckPartitionedSaveLoadAsync(page).ConfigureAwait(false);

            CheckStorageCookies(await page.Context.CookiesAsync().ConfigureAwait(false));
            CheckStorageCookies(ReadStorageCookies(await page.Context.StorageStateAsync().ConfigureAwait(false)));

            IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync().ConfigureAwait(false);
            IBrowserContext context2 = await NewHttpsContextAsync().ConfigureAwait(false);
            await context2.AddCookiesAsync(ToSetCookies(cookies)).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await CheckPartitionedSaveLoadAsync(page2).ConfigureAwait(false);
            await context2.CloseAsync().ConfigureAwait(false);

            string storageState = await page.Context.StorageStateAsync().ConfigureAwait(false);
            IBrowserContext context3 = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageState = storageState }).ConfigureAwait(false);
            IPage page3 = await context3.NewPageAsync().ConfigureAwait(false);
            await CheckPartitionedSaveLoadAsync(page3).ConfigureAwait(false);
            await context3.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "add 'Partitioned;' cookie via API")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AddPartitionedCookieViaApi()
        {
            EnsureHttps();
            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            AddCommonCookieHandlers();
            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "top-level-partitioned",
                    Value = "value",
                    Domain = HttpsHostname,
                    Path = "/",
                    Expires = -1,
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteAttribute.None,
                    PartitionKey = "https://localhost",
                },
                new Cookie
                {
                    Name = "top-level-non-partitioned",
                    Value = "value",
                    Domain = HttpsHostname,
                    Path = "/",
                    Expires = -1,
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteAttribute.None,
                },
                new Cookie
                {
                    Name = "frame-partitioned",
                    Value = "value",
                    Domain = HttpsHostname,
                    Path = "/",
                    Expires = -1,
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteAttribute.None,
                    PartitionKey = "https://127.0.0.1",
                },
                new Cookie
                {
                    Name = "frame-non-partitioned",
                    Value = "value",
                    Domain = HttpsHostname,
                    Path = "/",
                    Expires = -1,
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteAttribute.None,
                },
            }).ConfigureAwait(false);
            await CheckAddPartitionedApiAsync(page).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "same origin third party 'Partitioned;' cookie with different origin intermediate iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SameOriginThirdPartyPartitionedCookieWithDifferentOriginIntermediateIframe()
        {
            EnsureHttps();
            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            AddCommonCookieHandlers();
            SetPartitionedSetCookieRoute();
            await page.GoToAsync(Urls.SetOrigin1Origin2Origin1).ConfigureAwait(false);
            await CheckNestedSameOriginPartitionedAsync(page).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync().ConfigureAwait(false);
            IBrowserContext context2 = await NewHttpsContextAsync().ConfigureAwait(false);
            await context2.AddCookiesAsync(ToSetCookies(cookies)).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await CheckNestedSameOriginPartitionedAsync(page2).ConfigureAwait(false);
            await context2.CloseAsync().ConfigureAwait(false);

            string storageState = await page.Context.StorageStateAsync().ConfigureAwait(false);
            IBrowserContext context3 = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageState = storageState }).ConfigureAwait(false);
            IPage page3 = await context3.NewPageAsync().ConfigureAwait(false);
            await CheckNestedSameOriginPartitionedAsync(page3).ConfigureAwait(false);
            await context3.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "top level 'Partitioned;' cookie and same origin iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TopLevelPartitionedCookieAndSameOriginIframe()
        {
            EnsureHttps();
            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            AddCommonCookieHandlers();
            HttpsServer.SetRoute("/set-cookie.html", async http =>
            {
                bool framed = !string.IsNullOrEmpty(http.Request.Headers["Referer"].ToString());
                string prefix = framed ? "frame" : "top-level";
                http.Response.Headers.Append("Set-Cookie", prefix + "=value; SameSite=None; Path=/; Secure; Partitioned;");
                http.Response.Headers.Append("Set-Cookie", prefix + "-non-partitioned=value; SameSite=None; Path=/; Secure;");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
            await page.GoToAsync(Urls.SetOrigin1).ConfigureAwait(false);
            await page.Context.StorageStateAsync(new() { Path = Path.Combine(Path.GetTempPath(), "pwsharp-wave859-state2.json") }).ConfigureAwait(false);
            await CheckTopLevelSameOriginAsync(page).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync().ConfigureAwait(false);
            IBrowserContext context2 = await NewHttpsContextAsync().ConfigureAwait(false);
            await context2.AddCookiesAsync(ToSetCookies(cookies)).ConfigureAwait(false);
            IPage page2 = await context2.NewPageAsync().ConfigureAwait(false);
            await CheckTopLevelSameOriginAsync(page2).ConfigureAwait(false);
            await context2.CloseAsync().ConfigureAwait(false);

            string storageState = await page.Context.StorageStateAsync().ConfigureAwait(false);
            IBrowserContext context3 = await _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageState = storageState }).ConfigureAwait(false);
            IPage page3 = await context3.NewPageAsync().ConfigureAwait(false);
            await CheckTopLevelSameOriginAsync(page3).ConfigureAwait(false);
            await context3.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "should be able to send third party cookies via an iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToSendThirdPartyCookiesViaAnIframe()
        {
            EnsureHttps();
            if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                Assert.Ignore("webkit && isMac");
                return;
            }

            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Domain = new Uri(HttpsCrossPrefix).Host,
                    Path = "/",
                    Name = "cookie1",
                    Value = "yes",
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteAttribute.None,
                },
            }).ConfigureAwait(false);
            Task<string> requestTask = HttpsServer.WaitForRequest("/grid.html", request => request.Headers["Cookie"].ToString());
            await page.SetContentAsync("<iframe src=\"" + HttpsCrossPrefix + "/grid.html\"></iframe>").ConfigureAwait(false);
            Assert.That(await requestTask.ConfigureAwait(false), Is.EqualTo("cookie1=yes"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "should(not) block third party cookies - persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotBlockThirdPartyCookiesPersistentContext()
        {
            EnsureHttps();
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
            }

            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                executablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave859-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(
                    userDataDir,
                    new BrowserTypeLaunchPersistentContextOptions
                    {
                        ExecutablePath = executablePath,
                        Headless = true,
                        IgnoreHTTPSErrors = true,
                    }).ConfigureAwait(false);
                IPage page = context.Pages.Count > 0 ? context.Pages.First() : await context.NewPageAsync().ConfigureAwait(false);
                await TestThirdPartyCookiesAreBlockedAsync(page, context).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, recursive: true);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "should(not) block third party cookies - ephemeral context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotBlockThirdPartyCookiesEphemeralContext()
        {
            EnsureHttps();
            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await TestThirdPartyCookiesAreBlockedAsync(page, context).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-cookies-third-party.spec.ts", "should not block third party SameSite=None cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotBlockThirdPartySameSiteNoneCookies()
        {
            EnsureHttps();
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("No third party cookies in WebKit");
                return;
            }

            IBrowserContext context = await NewHttpsContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            HttpsServer.SetRoute("/empty.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<iframe src=\"" + HttpsCrossPrefix + "/grid.html\"></iframe>").ConfigureAwait(false);
            });
            HttpsServer.SetRoute("/grid.html", async http =>
            {
                http.Response.Headers.Append("Set-Cookie", "a=b; Path=/; Max-Age=3600; SameSite=None; Secure");
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("Hello world\n    <script>\n    setTimeout(() => fetch('/json'), 1000);\n    </script>").ConfigureAwait(false);
            });
            Task<string> cookie = HttpsServer.WaitForRequest("/json", request => request.Headers["Cookie"].ToString());
            await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            Assert.That(await cookie.ConfigureAwait(false), Is.EqualTo("a=b"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static string EnsureTestCertificate(string contentRoot)
        {
            string certPath = Path.Combine(contentRoot, "key.pfx");
            if (File.Exists(certPath))
            {
                return certPath;
            }

            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
            using X509Certificate2 cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));
            File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, "playwright"));
            return certPath;
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            string certPath = EnsureTestCertificate(contentRoot);

            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19959;
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
                    HttpsCrossPrefix = "https://127.0.0.1:" + portText;
                    HttpsEmptyPage = HttpsPrefix + "/empty.html";
                    HttpsHostname = "localhost";
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private Task<IBrowserContext> NewHttpsContextAsync()
            => _browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }
        }

        private static void AddCommonCookieHandlers()
        {
            HttpsServer.SetRoute("/read-cookie.html", async http =>
            {
                http.Response.ContentType = "text/html";
                string raw = http.Request.Headers["Cookie"].ToString();
                string cookies = string.IsNullOrEmpty(raw)
                    ? "undefined"
                    : string.Join("; ", raw.Split(';').Select(item => item.Trim()).OrderBy(item => item, StringComparer.Ordinal));
                await http.Response.WriteAsync("Received cookie: " + cookies).ConfigureAwait(false);
            });
            HttpsServer.SetRoute("/frame-set-cookie.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<iframe src='" + Urls.Origin1 + "/set-cookie.html'></iframe>").ConfigureAwait(false);
            });
            HttpsServer.SetRoute("/frame-read-cookie.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<iframe src='" + Urls.Origin1 + "/read-cookie.html'></iframe>").ConfigureAwait(false);
            });
            HttpsServer.SetRoute("/nested-frame-set-cookie.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<iframe src='" + Urls.Origin2 + "/frame-set-cookie.html'></iframe>").ConfigureAwait(false);
            });
            HttpsServer.SetRoute("/nested-frame-read-cookie.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<iframe src='" + Urls.Origin2 + "/frame-read-cookie.html'></iframe>").ConfigureAwait(false);
            });
        }

        private static void SetPartitionedSetCookieRoute()
        {
            HttpsServer.SetRoute("/set-cookie.html", async http =>
            {
                bool framed = !string.IsNullOrEmpty(http.Request.Headers["Referer"].ToString());
                string prefix = framed ? "frame" : "top-level";
                http.Response.Headers.Append("Set-Cookie", prefix + "-partitioned=value; SameSite=None; Path=/; Secure; Partitioned;");
                http.Response.Headers.Append("Set-Cookie", prefix + "-non-partitioned=value; SameSite=None; Path=/; Secure;");
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });
        }

        private static async Task<(string ExpectedTopLevel, string ExpectedThirdParty)> RunNonPartitionedTestAsync(IPage page)
        {
            AddCommonCookieHandlers();
            HttpsServer.SetRoute("/set-cookie.html", async http =>
            {
                bool framed = !string.IsNullOrEmpty(http.Request.Headers["Referer"].ToString());
                http.Response.Headers["Set-Cookie"] = (framed ? "frame" : "top-level") + "=value; SameSite=None; Path=/; Secure;";
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });

            await page.GoToAsync(Urls.SetOrigin1).ConfigureAwait(false);
            await page.GoToAsync(Urls.ReadOrigin1).ConfigureAwait(false);
            Assert.That(await page.Locator("body").TextContentAsync().ConfigureAwait(false), Is.EqualTo("Received cookie: top-level=value"));

            await page.GoToAsync(Urls.ReadOrigin2Origin1).ConfigureAwait(false);
            ILocator frameBody = page.Locator("iframe").ContentFrame.Locator("body");
            if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                await Assertions.Expect(frameBody).ToHaveTextAsync("Received cookie: undefined").ConfigureAwait(false);
            }
            else
            {
                await Assertions.Expect(frameBody).ToHaveTextAsync("Received cookie: top-level=value").ConfigureAwait(false);
            }

            await page.GoToAsync(Urls.SetOrigin2Origin1).ConfigureAwait(false);
            await page.GoToAsync(Urls.ReadOrigin2Origin1).ConfigureAwait(false);
            string expectedThirdParty = "Received cookie: ";
            if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                expectedThirdParty += "undefined";
            }
            else if (TestConstants.IsWebKit && IsLinux)
            {
                expectedThirdParty += "top-level=value";
            }
            else
            {
                expectedThirdParty += "frame=value; top-level=value";
            }

            await Assertions.Expect(frameBody).ToHaveTextAsync(expectedThirdParty, new() { Timeout = 1000 }).ConfigureAwait(false);

            await page.GoToAsync(Urls.ReadOrigin1).ConfigureAwait(false);
            string expectedTopLevel = TestConstants.IsWebKit && (TestConstants.IsMacOSX || IsLinux)
                ? "Received cookie: top-level=value"
                : "Received cookie: frame=value; top-level=value";
            Assert.That(await page.Locator("body").TextContentAsync().ConfigureAwait(false), Is.EqualTo(expectedTopLevel));
            return (expectedTopLevel, expectedThirdParty);
        }

        private static async Task RunPartitionedTestAsync(IPage page)
        {
            AddCommonCookieHandlers();
            SetPartitionedSetCookieRoute();
            await page.GoToAsync(Urls.SetOrigin1).ConfigureAwait(false);
            await page.GoToAsync(Urls.ReadOrigin1).ConfigureAwait(false);
            Assert.That(
                await page.Locator("body").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("Received cookie: top-level-non-partitioned=value; top-level-partitioned=value"));

            await page.GoToAsync(Urls.ReadOrigin2Origin1).ConfigureAwait(false);
            ILocator frameBody = page.Locator("iframe").ContentFrame.Locator("body");
            if (TestConstants.IsWebKit && !TestConstants.IsMacOSX)
            {
                await Assertions.Expect(frameBody).ToHaveTextAsync("Received cookie: top-level-non-partitioned=value; top-level-partitioned=value").ConfigureAwait(false);
                return;
            }

            if (TestConstants.IsWebKit)
            {
                await Assertions.Expect(frameBody).ToHaveTextAsync("Received cookie: undefined").ConfigureAwait(false);
            }
            else
            {
                await Assertions.Expect(frameBody).ToHaveTextAsync("Received cookie: top-level-non-partitioned=value").ConfigureAwait(false);
            }

            await page.GoToAsync(Urls.SetOrigin2Origin1).ConfigureAwait(false);
            await page.GoToAsync(Urls.ReadOrigin2Origin1).ConfigureAwait(false);
            if (TestConstants.IsWebKit)
            {
                await Assertions.Expect(frameBody).ToHaveTextAsync(
                    WebkitPartitions ? "Received cookie: frame-partitioned=value" : "Received cookie: undefined").ConfigureAwait(false);
            }
            else
            {
                await Assertions.Expect(frameBody).ToHaveTextAsync("Received cookie: frame-non-partitioned=value; frame-partitioned=value; top-level-non-partitioned=value").ConfigureAwait(false);
            }
        }

        private static async Task CheckNonPartitionedAsync(IPage page, string expectedTopLevel, string expectedThirdParty)
        {
            await page.GoToAsync(Urls.ReadOrigin1).ConfigureAwait(false);
            Assert.That(await page.Locator("body").TextContentAsync().ConfigureAwait(false), Is.EqualTo(expectedTopLevel));
            await page.GoToAsync(Urls.ReadOrigin2Origin1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("body")).ToHaveTextAsync(expectedThirdParty).ConfigureAwait(false);
        }

        private static async Task CheckPartitionedSaveLoadAsync(IPage page)
        {
            await page.GoToAsync(Urls.ReadOrigin1).ConfigureAwait(false);
            string expectedTopLevel = TestConstants.IsWebKit && TestConstants.IsMacOSX
                ? "Received cookie: top-level-non-partitioned=value; top-level-partitioned=value"
                : "Received cookie: frame-non-partitioned=value; top-level-non-partitioned=value; top-level-partitioned=value";
            Assert.That(await page.Locator("body").TextContentAsync().ConfigureAwait(false), Is.EqualTo(expectedTopLevel));

            await page.GoToAsync(Urls.ReadOrigin2Origin1).ConfigureAwait(false);
            string expectedThirdParty = TestConstants.IsWebKit
                ? (WebkitPartitions ? "Received cookie: frame-partitioned=value" : "Received cookie: undefined")
                : "Received cookie: frame-non-partitioned=value; frame-partitioned=value; top-level-non-partitioned=value";
            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("body")).ToHaveTextAsync(expectedThirdParty, new() { Timeout = 1000 }).ConfigureAwait(false);

            await page.GoToAsync(Urls.ReadOrigin1Origin2Origin1).ConfigureAwait(false);
            string expectedNested = TestConstants.IsWebKit
                ? "Received cookie: top-level-non-partitioned=value; top-level-partitioned=value"
                : "Received cookie: frame-non-partitioned=value; top-level-non-partitioned=value";
            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("iframe").ContentFrame.Locator("body"))
                .ToHaveTextAsync(expectedNested, new() { Timeout = 1000 }).ConfigureAwait(false);
        }

        private static async Task CheckAddPartitionedApiAsync(IPage page)
        {
            await page.GoToAsync(Urls.ReadOrigin1).ConfigureAwait(false);
            string expectedTopLevel;
            if (TestConstants.IsWebKit && TestConstants.IsMacOSX && !WebkitPartitions)
            {
                expectedTopLevel = "Received cookie: frame-non-partitioned=value; top-level-non-partitioned=value";
            }
            else if (TestConstants.IsWebKit && !WebkitPartitions)
            {
                expectedTopLevel = "Received cookie: frame-non-partitioned=value; frame-partitioned=value; top-level-non-partitioned=value; top-level-partitioned=value";
            }
            else
            {
                expectedTopLevel = "Received cookie: frame-non-partitioned=value; top-level-non-partitioned=value; top-level-partitioned=value";
            }

            Assert.That(await page.Locator("body").TextContentAsync().ConfigureAwait(false), Is.EqualTo(expectedTopLevel));

            await page.GoToAsync(Urls.ReadOrigin2Origin1).ConfigureAwait(false);
            string expectedThirdParty = "Received cookie: ";
            if (WebkitPartitions)
            {
                expectedThirdParty += "frame-partitioned=value";
            }
            else if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                expectedThirdParty += "undefined";
            }
            else if (TestConstants.IsChromium)
            {
                expectedThirdParty += "frame-non-partitioned=value; frame-partitioned=value; top-level-non-partitioned=value";
            }
            else
            {
                expectedThirdParty += "frame-non-partitioned=value; frame-partitioned=value; top-level-non-partitioned=value; top-level-partitioned=value";
            }

            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("body")).ToHaveTextAsync(expectedThirdParty, new() { Timeout = 1000 }).ConfigureAwait(false);

            await page.GoToAsync(Urls.ReadOrigin1Origin2Origin1).ConfigureAwait(false);
            string expectedNested = "Received cookie: ";
            if (WebkitPartitions)
            {
                expectedNested += "frame-non-partitioned=value; top-level-non-partitioned=value; top-level-partitioned=value";
            }
            else if (TestConstants.IsWebKit && TestConstants.IsMacOSX)
            {
                expectedNested += "frame-non-partitioned=value; top-level-non-partitioned=value";
            }
            else if (TestConstants.IsWebKit)
            {
                expectedNested += "frame-non-partitioned=value; frame-partitioned=value; top-level-non-partitioned=value; top-level-partitioned=value";
            }
            else
            {
                expectedNested += "frame-non-partitioned=value; top-level-non-partitioned=value";
            }

            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("iframe").ContentFrame.Locator("body"))
                .ToHaveTextAsync(expectedNested, new() { Timeout = 1000 }).ConfigureAwait(false);
        }

        private static async Task CheckNestedSameOriginPartitionedAsync(IPage page)
        {
            await page.GoToAsync(Urls.ReadOrigin1Origin2Origin1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("iframe").ContentFrame.Locator("body"))
                .ToHaveTextAsync("Received cookie: frame-non-partitioned=value; frame-partitioned=value").ConfigureAwait(false);
        }

        private static async Task CheckTopLevelSameOriginAsync(IPage page)
        {
            await page.GoToAsync(Urls.ReadOrigin1).ConfigureAwait(false);
            Assert.That(
                await page.Locator("body").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("Received cookie: top-level-non-partitioned=value; top-level=value"));

            await page.GoToAsync(Urls.ReadOrigin1Origin1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("body"))
                .ToHaveTextAsync("Received cookie: top-level-non-partitioned=value; top-level=value", new() { Timeout = 1000 }).ConfigureAwait(false);

            await page.GoToAsync(Urls.ReadOrigin1Origin2Origin1).ConfigureAwait(false);
            string expectedThirdParty = "Received cookie: ";
            if (TestConstants.IsChromium)
            {
                expectedThirdParty += "top-level-non-partitioned=value";
            }
            else
            {
                expectedThirdParty += "top-level-non-partitioned=value; top-level=value";
            }

            await Assertions.Expect(page.Locator("iframe").ContentFrame.Locator("iframe").ContentFrame.Locator("body"))
                .ToHaveTextAsync(expectedThirdParty, new() { Timeout = 1000 }).ConfigureAwait(false);
        }

        private static async Task TestThirdPartyCookiesAreBlockedAsync(IPage page, IBrowserContext context)
        {
            await page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"(src) => {
                    let fulfill;
                    const promise = new Promise(x => fulfill = x);
                    const iframe = document.createElement('iframe');
                    document.body.appendChild(iframe);
                    iframe.onload = fulfill;
                    iframe.src = src;
                    return promise;
                }",
                HttpsCrossPrefix + "/grid.html").ConfigureAwait(false);
            IFrame[] frames = page.Frames.ToArray();
            string documentCookie = await frames[1].EvaluateAsync<string>(
                @"() => {
                    document.cookie = 'username=John Doe';
                    return document.cookie;
                }").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(2000).ConfigureAwait(false);
            Assert.That(documentCookie, Is.EqualTo(AllowsThirdParty ? "username=John Doe" : string.Empty));
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync(HttpsCrossPrefix + "/grid.html").ConfigureAwait(false);
            if (AllowsThirdParty)
            {
                Assert.That(cookies, Has.Exactly(1).Items);
                Assert.That(cookies[0].Domain, Is.EqualTo("127.0.0.1"));
                Assert.That(cookies[0].Expires, Is.EqualTo(-1));
                Assert.That(cookies[0].HttpOnly, Is.False);
                Assert.That(cookies[0].Name, Is.EqualTo("username"));
                Assert.That(cookies[0].Path, Is.EqualTo("/"));
                Assert.That(cookies[0].SameSite, Is.EqualTo(DefaultSameSiteCookieValue()));
                Assert.That(cookies[0].Secure, Is.False);
                Assert.That(cookies[0].Value, Is.EqualTo("John Doe"));
            }
            else
            {
                Assert.That(cookies, Is.Empty);
            }
        }

        private static void CheckStorageCookies(IReadOnlyList<NamedPartition> cookies)
        {
            bool webkitNoPartition = TestConstants.IsWebKit && !WebkitPartitions;
            ExpectPartitionKey(cookies, "top-level-partitioned", webkitNoPartition ? null : "https://localhost");
            ExpectPartitionKey(cookies, "top-level-non-partitioned", null);
            if (webkitNoPartition)
            {
                Assert.That(cookies.Any(item => item.Name == "frame-partitioned"), Is.False);
                Assert.That(cookies.Any(item => item.Name == "frame-non-partitioned"), Is.False);
            }
            else
            {
                ExpectPartitionKey(cookies, "frame-partitioned", "https://127.0.0.1");
                if (TestConstants.IsWebKit)
                {
                    Assert.That(cookies.Any(item => item.Name == "frame-non-partitioned"), Is.False);
                }
                else
                {
                    ExpectPartitionKey(cookies, "frame-non-partitioned", null);
                }
            }
        }

        private static void CheckStorageCookies(IReadOnlyList<BrowserContextCookiesResult> cookies)
        {
            List<NamedPartition> list = new List<NamedPartition>();
            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                list.Add(new NamedPartition(cookie.Name, cookie.PartitionKey));
            }

            CheckStorageCookies(list);
        }

        private static IReadOnlyList<NamedPartition> ReadStorageCookies(string json)
        {
            List<NamedPartition> list = new List<NamedPartition>();
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("cookies", out JsonElement cookies)
                || cookies.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (JsonElement cookie in cookies.EnumerateArray())
            {
                string name = cookie.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                string partition = null;
                if (cookie.TryGetProperty("partitionKey", out JsonElement partitionEl)
                    && partitionEl.ValueKind == JsonValueKind.String)
                {
                    partition = partitionEl.GetString();
                }

                list.Add(new NamedPartition(name, partition));
            }

            return list;
        }

        private static void ExpectPartitionKey(IReadOnlyList<NamedPartition> cookies, string name, string partitionKey)
        {
            NamedPartition found = cookies.FirstOrDefault(item => item.Name == name);
            Assert.That(found, Is.Not.Null, "Cookie " + name + " not found");
            string actual = string.IsNullOrEmpty(found.PartitionKey) ? null : found.PartitionKey;
            Assert.That(actual, Is.EqualTo(partitionKey), "Cookie " + name + " has partitionKey " + actual + " but expected " + partitionKey + ".");
        }

        private static List<Cookie> ToSetCookies(IReadOnlyList<BrowserContextCookiesResult> cookies)
        {
            List<Cookie> list = new List<Cookie>();
            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                list.Add(new Cookie
                {
                    Name = cookie.Name,
                    Value = cookie.Value,
                    Domain = cookie.Domain,
                    Path = cookie.Path,
                    Expires = cookie.Expires,
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure,
                    SameSite = cookie.SameSite,
                    PartitionKey = cookie.PartitionKey,
                });
            }

            return list;
        }

        private static SameSiteAttribute DefaultSameSiteCookieValue()
        {
            if (TestConstants.IsChromium)
            {
                return SameSiteAttribute.Lax;
            }

            if (TestConstants.IsWebKit && IsLinux)
            {
                return SameSiteAttribute.Lax;
            }

            if (TestConstants.IsWebKit)
            {
                return SameSiteAttribute.None;
            }

            return SameSiteAttribute.None;
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

        private sealed class TestUrls
        {
            internal string Origin1 => HttpsPrefix;

            internal string Origin2 => HttpsCrossPrefix;

            internal string ReadOrigin1 => Origin1 + "/read-cookie.html";

            internal string ReadOrigin2Origin1 => Origin2 + "/frame-read-cookie.html";

            internal string ReadOrigin1Origin1 => Origin1 + "/frame-read-cookie.html";

            internal string ReadOrigin1Origin2Origin1 => Origin1 + "/nested-frame-read-cookie.html";

            internal string SetOrigin1 => Origin1 + "/set-cookie.html";

            internal string SetOrigin2Origin1 => Origin2 + "/frame-set-cookie.html";

            internal string SetOrigin1Origin2Origin1 => Origin1 + "/nested-frame-set-cookie.html";
        }

        private sealed class NamedPartition
        {
            internal NamedPartition(string name, string partitionKey)
            {
                Name = name;
                PartitionKey = partitionKey;
            }

            internal string Name { get; }

            internal string PartitionKey { get; }
        }
    }
}
