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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/defaultbrowsercontext-2.spec.ts</c> parity.
    /// Do not edit leftover <c>LaunchPersistent*</c> tests.
    /// Skipped (Node-only): <c>should have passed URL when launching with
    /// ignoreDefaultArgs: true</c> (<c>toImpl</c> defaultArgs),
    /// <c>should handle timeout</c> / <c>should handle exception</c>
    /// (<c>__testHookBeforeCreateBrowser</c>),
    /// <c>should connect to a browser with the default page</c>
    /// (<c>__testHookOnConnectToBrowser</c>),
    /// <c>user agent is up to date</c>
    /// (<c>_channel.defaultUserAgentForTest</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryDefaultBrowserContext2ParityTests : PageTestEx
    {
        private const string DefaultContextCss = @"{
  query(root, selector) {
    return root.querySelector(selector);
  },
  queryAll(root, selector) {
    return Array.from(root.querySelectorAll(selector));
  }
}";

        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
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
        public void SetUp()
        {
            Server?.Reset();
            HttpsServer?.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Server?.Reset();
            HttpsServer?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support hasTouch option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHasTouchOption()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                HasTouch = true,
            }).ConfigureAwait(false);
            await launch.Page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => 'ontouchstart' in window").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should work in persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkInPersistentContext()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                ViewportSize = new ViewportSize { Width = 320, Height = 480 },
                IsMobile = true,
            }).ConfigureAwait(false);
            await launch.Page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<int>("() => window.innerWidth").ConfigureAwait(false), Is.EqualTo(980));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support colorScheme option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportColorSchemeOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                ColorScheme = ColorScheme.Dark,
            }).ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(prefers-color-scheme: light)').matches").ConfigureAwait(false), Is.False);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(prefers-color-scheme: dark)').matches").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support reducedMotion option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportReducedMotionOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                ReducedMotion = ReducedMotion.Reduce,
            }).ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(prefers-reduced-motion: reduce)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(prefers-reduced-motion: no-preference)').matches").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support forcedColors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportForcedColorsOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                ForcedColors = ForcedColors.Active,
            }).ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(forced-colors: active)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(forced-colors: none)').matches").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support contrast option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportContrastOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                Contrast = Contrast.More,
            }).ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(prefers-contrast: more)').matches").ConfigureAwait(false), Is.True);
            Assert.That(await launch.Page.EvaluateAsync<bool>("() => matchMedia('(prefers-contrast: no-preference)').matches").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support timezoneId option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTimezoneIdOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                Locale = "en-US",
                TimezoneId = "America/Jamaica",
            }).ConfigureAwait(false);
            Assert.That(
                await launch.Page.EvaluateAsync<string>("() => new Date(1479579154987).toString()").ConfigureAwait(false),
                Is.EqualTo("Sat Nov 19 2016 13:12:34 GMT-0500 (Eastern Standard Time)"));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support locale option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportLocaleOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                Locale = "fr-FR",
            }).ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<string>("() => navigator.language").ConfigureAwait(false), Is.EqualTo("fr-FR"));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support geolocation and permissions options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportGeolocationAndPermissionsOptions()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                Geolocation = new Geolocation { Longitude = 10, Latitude = 10 },
                Permissions = new[] { "geolocation" },
            }).ConfigureAwait(false);
            await launch.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            GeoCoords geolocation = await ReadGeolocationAsync(launch.Page).ConfigureAwait(false);
            Assert.That(geolocation.Latitude, Is.EqualTo(10d));
            Assert.That(geolocation.Longitude, Is.EqualTo(10d));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support ignoreHTTPSErrors option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportIgnoreHttpsErrorsOption()
        {
            EnsureHttps();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                IgnoreHTTPSErrors = true,
            }).ConfigureAwait(false);
            Exception error = null;
            IResponse response = null;
            try
            {
                response = await launch.Page.GoToAsync(HttpsEmptyPage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Null);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support extraHTTPHeaders option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportExtraHttpHeadersOption()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync(new BrowserTypeLaunchPersistentContextOptions
            {
                ExtraHTTPHeaders = new Dictionary<string, string> { ["foo"] = "bar" },
            }).ConfigureAwait(false);
            Task<string> requestTask = Server.WaitForRequest("/empty.html", request => request.Headers["foo"].ToString());
            Task gotoTask = launch.Page.GoToAsync(EmptyPage);
            await gotoTask.ConfigureAwait(false);
            Assert.That(await requestTask.ConfigureAwait(false), Is.EqualTo("bar"));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should accept userDataDir")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptUserDataDir()
        {
            string userDataDir = CreateUserDataDir();
            try
            {
                await using PersistentLaunch launch = await LaunchPersistentAsync(userDataDir: userDataDir).ConfigureAwait(false);
                Assert.That(Directory.GetFileSystemEntries(userDataDir).Length, Is.GreaterThan(0));
                await launch.Context.CloseAsync().ConfigureAwait(false);
                Assert.That(Directory.GetFileSystemEntries(userDataDir).Length, Is.GreaterThan(0));
            }
            finally
            {
                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should accept relative userDataDir")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptRelativeUserDataDir()
        {
            string userDataDir = CreateUserDataDir();
            try
            {
                string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(userDataDir, "foobar"));
                await using PersistentLaunch launch = await LaunchPersistentAsync(userDataDir: relative).ConfigureAwait(false);
                Assert.That(Directory.GetFileSystemEntries(Path.Combine(userDataDir, "foobar")).Length, Is.GreaterThan(0));
            }
            finally
            {
                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should restore state from userDataDir")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRestoreStateFromUserDataDir()
        {
            EnsureServer();
            string userDataDir = CreateUserDataDir();
            string userDataDir2 = CreateUserDataDir();
            try
            {
                {
                    await using PersistentLaunch launch = await LaunchPersistentAsync(userDataDir: userDataDir).ConfigureAwait(false);
                    IPage page = await launch.Context.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                    await page.EvaluateAsync("() => localStorage.hey = 'hello'").ConfigureAwait(false);
                    await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                    await launch.Context.CloseAsync().ConfigureAwait(false);
                }

                {
                    await using PersistentLaunch launch = await LaunchPersistentAsync(userDataDir: userDataDir).ConfigureAwait(false);
                    IPage page = await launch.Context.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                    Assert.That(await page.EvaluateAsync<string>("() => localStorage.hey").ConfigureAwait(false), Is.EqualTo("hello"));
                    await launch.Context.CloseAsync().ConfigureAwait(false);
                }

                await using PersistentLaunch launch3 = await LaunchPersistentAsync(userDataDir: userDataDir2).ConfigureAwait(false);
                IPage page3 = await launch3.Context.NewPageAsync().ConfigureAwait(false);
                await page3.GoToAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(await page3.EvaluateAsync<string>("() => localStorage.hey").ConfigureAwait(false), Is.Not.EqualTo("hello"));
            }
            finally
            {
                TryDeleteDirectory(userDataDir);
                TryDeleteDirectory(userDataDir2);
            }
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should create userDataDir if it does not exist")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateUserDataDirIfItDoesNotExist()
        {
            string parent = CreateUserDataDir();
            try
            {
                string userDataDir = Path.Combine(parent, "nonexisting");
                await using PersistentLaunch launch = await LaunchPersistentAsync(userDataDir: userDataDir).ConfigureAwait(false);
                await launch.Context.CloseAsync().ConfigureAwait(false);
                Assert.That(Directory.GetFileSystemEntries(userDataDir).Length, Is.GreaterThan(0));
            }
            finally
            {
                TryDeleteDirectory(parent);
            }
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should goto about:blank on relaunched persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGotoAboutBlankOnRelaunchedPersistentContext()
        {
            string userDataDir = CreateUserDataDir();
            try
            {
                {
                    await using PersistentLaunch launch = await LaunchPersistentAsync(userDataDir: userDataDir).ConfigureAwait(false);
                    await launch.Context.Pages.First().GoToAsync("about:blank").ConfigureAwait(false);
                    await launch.Context.CloseAsync().ConfigureAwait(false);
                }

                await using PersistentLaunch relaunch = await LaunchPersistentAsync(userDataDir: userDataDir).ConfigureAwait(false);
                await relaunch.Context.Pages.First().GoToAsync("about:blank").ConfigureAwait(false);
                Assert.That(relaunch.Context.Pages.First().Url, Is.EqualTo("about:blank"));
            }
            finally
            {
                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should have default URL when launching browser")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveDefaultUrlWhenLaunchingBrowser()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            List<string> urls = new();
            foreach (IPage page in launch.Context.Pages)
            {
                urls.Add(page.Url);
            }

            Assert.That(urls, Is.EqualTo(new[] { "about:blank" }));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should throw if page argument is passed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowIfPageArgumentIsPassed()
        {
            EnsureServer();
            IBrowserType browserType = PersistentBrowserType();
            string userDataDir = CreateUserDataDir();
            try
            {
                Exception error = await CatchAsync(() => browserType.LaunchPersistentContextAsync(
                    userDataDir,
                    new BrowserTypeLaunchPersistentContextOptions
                    {
                        ExecutablePath = PersistentExecutablePath(),
                        Headless = true,
                        Args = new[] { EmptyPage },
                    })).ConfigureAwait(false);
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("can not specify page"));
            }
            finally
            {
                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should fire close event for a persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireCloseEventForAPersistentContext()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            bool closed = false;
            launch.Context.Close += (_, _) => closed = true;
            await launch.Context.CloseAsync().ConfigureAwait(false);
            Assert.That(closed, Is.True);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "coverage should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CoverageShouldWork()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("official skip: browserName !== 'chromium'");
            }

            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            await launch.Page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await launch.Page.GoToAsync(Prefix + "/jscoverage/simple.html", WaitUntilState.Load).ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> coverage = await launch.Page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Url, Does.Contain("/jscoverage/simple.html"));
            JSCoverageFunction foo = coverage[0].Functions.First(f => f.FunctionName == "foo");
            Assert.That(foo.Ranges[0].Count, Is.EqualTo(1));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should respect selectors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectSelectors()
        {
            await RegisterEngineAsync("defaultContextCSS", DefaultContextCss).ConfigureAwait(false);
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            await launch.Page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            Assert.That(await launch.Page.InnerHTMLAsync("css=div").ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await launch.Page.InnerHTMLAsync("defaultContextCSS=div").ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support har option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHarOption()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            await launch.Page.RouteFromHARAsync(Asset("har-fulfill.har")).ConfigureAwait(false);
            await launch.Page.GoToAsync("http://no.playwright/").ConfigureAwait(false);
            Assert.That(await launch.Page.EvaluateAsync<string>("window.value").ConfigureAwait(false), Is.EqualTo("foo"));
            await Assertions.Expect(launch.Page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(255, 0, 0)").ConfigureAwait(false);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "dialog.accept should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DialogAcceptShouldWork()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            await launch.Page.GoToAsync("data:text/html,<html><title>Title</title><button onclick=\"alert('Alert')\">Button</button></html>").ConfigureAwait(false);
            bool shown = false;
            launch.Page.Dialog += (_, dialog) =>
            {
                shown = true;
                _ = dialog.AcceptAsync();
            };
            await launch.Page.GetByRole("button", name: "Button").ClickAsync().ConfigureAwait(false);
            Assert.That(shown, Is.True);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "CacheStorage entry should survive page.reload()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CacheStorageEntryShouldSurvivePageReload()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            await launch.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await launch.Page.EvaluateAsync(
                @"async () => {
                    const cache = await caches.open('repro-cache');
                    await cache.put('/meta', new Response('payload'));
                }").ConfigureAwait(false);
            await launch.Page.ReloadAsync().ConfigureAwait(false);
            string after = await launch.Page.EvaluateAsync<string>(
                @"async () => {
                    const cache = await caches.open('repro-cache');
                    const resp = await cache.match('/meta');
                    return resp ? await resp.text() : null;
                }").ConfigureAwait(false);
            Assert.That(after, Is.EqualTo("payload"));
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "exposes browser")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ExposesBrowser()
        {
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            IBrowser browser = launch.Context.Browser;
            Assert.That(browser, Is.Not.Null);
            Assert.That(browser.Version, Is.Not.Null.And.Not.Empty);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><title>Title</title></html>").ConfigureAwait(false);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Title"));
            await browser.CloseAsync().ConfigureAwait(false);
            Assert.That(launch.Context.Pages.Count, Is.EqualTo(0));
            await launch.Context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("defaultbrowsercontext-2.spec.ts", "should support storage.getDirectory()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportStorageGetDirectory()
        {
            EnsureServer();
            await using PersistentLaunch launch = await LaunchPersistentAsync().ConfigureAwait(false);
            IPage page = await launch.Context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            object name;
            try
            {
                name = await page.EvaluateAsync<string>(
                    @"async () => {
                        const dir = await navigator.storage.getDirectory();
                        return dir.name;
                    }").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                name = ex;
            }

            Assert.That(name, Is.EqualTo(string.Empty));
        }

        private static async Task StartOwnedHttpAsync(string contentRoot)
        {
            int basePort = 19967;
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

            int basePort = 19987;
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

        private static async Task<PersistentLaunch> LaunchPersistentAsync(
            BrowserTypeLaunchPersistentContextOptions options = null,
            string userDataDir = null)
        {
            options ??= new BrowserTypeLaunchPersistentContextOptions();
            options.Headless = true;
            IBrowserType browserType = PersistentBrowserType();
            options.ExecutablePath = PersistentExecutablePath();
            bool ownsDir = string.IsNullOrEmpty(userDataDir);
            if (ownsDir)
            {
                userDataDir = CreateUserDataDir();
            }

            IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, options).ConfigureAwait(false);
            IPage page = context.Pages.FirstOrDefault();
            if (page == null)
            {
                page = await context.NewPageAsync().ConfigureAwait(false);
            }

            return new PersistentLaunch(context, page, userDataDir, ownsDir);
        }

        private static IBrowserType PersistentBrowserType()
        {
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                return Playwright.Webkit;
            }

            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return null;
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            return Playwright.Chromium;
        }

        private static string PersistentExecutablePath()
        {
            if (TestConstants.IsWebKit)
            {
                return BrowserExecutableFixture.WebkitExecutablePath;
            }

            if (TestConstants.IsFirefox)
            {
                return BrowserExecutableFixture.FirefoxExecutablePath;
            }

            return BrowserExecutableFixture.ChromiumExecutablePath;
        }

        private static string CreateUserDataDir()
        {
            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave867-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            return userDataDir;
        }

        private static async Task RegisterEngineAsync(string name, string script)
        {
            try
            {
                await Playwright.Selectors.RegisterAsync(name, script).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException ex)
                when (ex.Message.IndexOf("already registered", StringComparison.Ordinal) >= 0)
            {
            }
        }

        private static async Task<GeoCoords> ReadGeolocationAsync(IPage page)
        {
            return await page.EvaluateAsync<GeoCoords>(
                @"new Promise(resolve => navigator.geolocation.getCurrentPosition(position => {
    resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude });
  }))").ConfigureAwait(false);
        }

        private static string Asset(string name)
        {
            string fromSource = Path.Combine(TestUtils.FindParentDirectory("PlaywrightSharp.Tests"), "Assets", name);
            if (File.Exists(fromSource))
            {
                return fromSource;
            }

            return Path.Combine(AppContext.BaseDirectory, "Assets", name);
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

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException)
            {
            }
        }

        private sealed class PersistentLaunch : IAsyncDisposable
        {
            private readonly string _userDataDir;
            private readonly bool _ownsUserDataDir;

            internal PersistentLaunch(IBrowserContext context, IPage page, string userDataDir, bool ownsUserDataDir)
            {
                Context = context;
                Page = page;
                _userDataDir = userDataDir;
                _ownsUserDataDir = ownsUserDataDir;
            }

            internal IBrowserContext Context { get; }

            internal IPage Page { get; }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await Context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                if (_ownsUserDataDir)
                {
                    TryDeleteDirectory(_userDataDir);
                }
            }
        }

        private sealed class GeoCoords
        {
            [JsonPropertyName("latitude")]
            public double Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double Longitude { get; set; }
        }
    }
}
