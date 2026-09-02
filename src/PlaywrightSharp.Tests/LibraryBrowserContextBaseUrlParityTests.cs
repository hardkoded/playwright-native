/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-base-url.spec.ts</c> parity for
    /// context <c>baseURL</c>.
    /// Skipped (official <c>it.skip</c>): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextBaseUrlParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19833;
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

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
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
                    await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                }

                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }
        }

        [TearDown]
        public void ResetServer()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should construct a new URL when a baseURL in browser.newContext is passed to page.goto @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConstructANewUrlWhenABaseUrlInBrowserNewContextIsPassedToPageGoto()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { BaseURL = Prefix }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync("/empty.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(EmptyPage));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should construct a new URL when a baseURL in browser.newPage is passed to page.goto")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConstructANewUrlWhenABaseUrlInBrowserNewPageIsPassedToPageGoto()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { BaseURL = Prefix }).ConfigureAwait(false);
            IResponse response = await page.GoToAsync("/empty.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(EmptyPage));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should construct a new URL when a baseURL in browserType.launchPersistentContext is passed to page.goto")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConstructANewUrlWhenABaseUrlInBrowserTypeLaunchPersistentContextIsPassedToPageGoto()
        {
            EnsureServer();
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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave833-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(
                    userDataDir,
                    new BrowserTypeLaunchPersistentContextOptions
                    {
                        ExecutablePath = executablePath,
                        Headless = true,
                        BaseURL = Prefix,
                    }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                IResponse response = await page.GoToAsync("/empty.html").ConfigureAwait(false);
                Assert.That(response, Is.Not.Null);
                Assert.That(response.Url, Is.EqualTo(EmptyPage));
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
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should construct the URLs correctly when a baseURL without a trailing slash in browser.newPage is passed to page.goto")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConstructTheUrlsCorrectlyWhenABaseUrlWithoutATrailingSlashInBrowserNewPageIsPassedToPageGoto()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { BaseURL = Prefix + "/url-construction" }).ConfigureAwait(false);
            IResponse response = await page.GoToAsync("mypage.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/mypage.html"));
            response = await page.GoToAsync("./mypage.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/mypage.html"));
            response = await page.GoToAsync("/mypage.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/mypage.html"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should construct the URLs correctly when a baseURL with a trailing slash in browser.newPage is passed to page.goto")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConstructTheUrlsCorrectlyWhenABaseUrlWithATrailingSlashInBrowserNewPageIsPassedToPageGoto()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { BaseURL = Prefix + "/url-construction/" }).ConfigureAwait(false);
            IResponse response = await page.GoToAsync("mypage.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/url-construction/mypage.html"));
            response = await page.GoToAsync("./mypage.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/url-construction/mypage.html"));
            response = await page.GoToAsync("/mypage.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/mypage.html"));
            response = await page.GoToAsync(".").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/url-construction/"));
            response = await page.GoToAsync("/").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should not construct a new URL when valid URLs are passed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotConstructANewUrlWhenValidUrlsArePassed()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { BaseURL = "http://microsoft.com" }).ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Url, Is.EqualTo(EmptyPage));

            await page.GoToAsync("data:text/html,Hello world").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => window.location.href)()").ConfigureAwait(false),
                Is.EqualTo("data:text/html,Hello world"));

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("(() => window.location.href)()").ConfigureAwait(false),
                Is.EqualTo("about:blank"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should be able to match a URL relative to its given URL with urlMatcher")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToMatchAUrlRelativeToItsGivenUrlWithUrlMatcher()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { BaseURL = Prefix + "/foobar/" }).ConfigureAwait(false);
            await page.GoToAsync("/kek/index.html").ConfigureAwait(false);
            await page.WaitForURLAsync("/kek/index.html").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/kek/index.html"));

            await page.RouteAsync("./kek/index.html", route => route.FulfillAsync(new() { Body = "base-url-matched-route" }))
                .ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForRequestAsync("./kek/index.html");
            Task<IResponse> responseTask = page.WaitForResponseAsync("./kek/index.html");
            await page.GoToAsync("./kek/index.html").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(Prefix + "/foobar/kek/index.html"));
            Assert.That(response.Url, Is.EqualTo(Prefix + "/foobar/kek/index.html"));
            Assert.That(
                Encoding.UTF8.GetString(await response.BodyAsync().ConfigureAwait(false)),
                Is.EqualTo("base-url-matched-route"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-base-url.spec.ts", "should not construct a new URL with baseURL when a glob was used")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotConstructANewUrlWithBaseUrlWhenAGlobWasUsed()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { BaseURL = Prefix + "/foobar/" }).ConfigureAwait(false);
            await page.GoToAsync("./kek/index.html").ConfigureAwait(false);
            await page.WaitForURLAsync("**/foobar/kek/index.html").ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
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
    }
}
