/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-autowaiting-no-hang.spec.ts</c> parity for click and
    /// navigation auto-wait that must not stall. Skipped: none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageAutowaitingNoHangParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                if (TestServerSetup.HttpsServer != null)
                {
                    HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
                }

                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19789;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    EmptyPage = origin + "/empty.html";
                    await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
                    return;
                }
                catch (Exception)
                {
                }
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
        }

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
            HttpsServer?.Reset();
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "clicking on links which do not commit navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickingOnLinksWhichDoNotCommitNavigation()
        {
            EnsureHttps();
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href='" + HttpsEmptyPage + "'>foobar</a>").ConfigureAwait(false);
            await page.ClickAsync("a").ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "calling window.stop async")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CallingWindowStopAsync()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", _ => Task.Delay(Timeout.Infinite));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "url => { window.location.href = url; setTimeout(() => window.stop(), 100); }",
                EmptyPage).ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "calling window.stop sync")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CallingWindowStopSync()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "url => { window.location.href = url; window.stop(); }",
                EmptyPage).ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "assigning location to about:blank")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AssigningLocationToAboutBlank()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>("window.location.href = \"about:blank\";").ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "assigning location to about:blank after non-about:blank")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AssigningLocationToAboutBlankAfterNonAboutBlank()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", _ => Task.Delay(Timeout.Infinite));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "window.location.href = \"" + EmptyPage + "\"; window.location.href = \"about:blank\";")
                .ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "calling window.open and window.close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CallingWindowOpenAndWindowClose()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>("() => { const popup = window.open(window.location.href); popup.close(); }")
                .ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "opening a popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task OpeningAPopup()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Task.WhenAll(
                page.WaitForPopupAsync(),
                page.EvaluateAsync<object>("() => window.open(window.location.href) && 1")).ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "clicking in the middle of navigation that aborts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickingInTheMiddleOfNavigationThatAborts()
        {
            EnsureServer();
            TaskCompletionSource<bool> abort = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> stall = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/stall.html", async http =>
            {
                stall.TrySetResult(true);
                await abort.Task.ConfigureAwait(false);
                http.Abort();
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            _ = page.GoToAsync(Prefix + "/stall.html").ContinueWith(_ => 0);
            await stall.Task.ConfigureAwait(false);
            Task clickPromise = page.ClickAsync("body");
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            abort.TrySetResult(true);
            await clickPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "clicking in the middle of navigation that commits")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickingInTheMiddleOfNavigationThatCommits()
        {
            EnsureServer();
            TaskCompletionSource<bool> commit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> stall = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/stall.html", async http =>
            {
                stall.TrySetResult(true);
                await commit.Task.ConfigureAwait(false);
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("hello world").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            _ = page.GoToAsync(Prefix + "/stall.html").ContinueWith(_ => 0);
            await stall.Task.ConfigureAwait(false);
            Task clickPromise = page.ClickAsync("body");
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            commit.TrySetResult(true);
            await clickPromise.ConfigureAwait(false);
            await Assertions.Expect(page.Locator("body")).ToContainTextAsync("hello world").ConfigureAwait(false);
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "clicking a link intercepted by the Navigation API same-document")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickingALinkInterceptedByTheNavigationApiSameDocument()
        {
            EnsureServer();
            Server.SetRoute("/intercept.html", async http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(@"
      <a id=""go"" href=""/other"">go</a>
      <p id=""status"">initial</p>
      <script>
        navigation.addEventListener('navigate', event => {
          if (!event.canIntercept)
            return;
          event.intercept({
            handler: async () => {
              const dest = new URL(event.destination.url).pathname;
              document.getElementById('status').textContent = 'intercepted:' + dest;
            },
          });
        });
      </script>
    ").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/intercept.html").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("initial").ConfigureAwait(false);
            await page.Locator("#go").ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("intercepted:/other").ConfigureAwait(false);
            Assert.That(new Uri(page.Url).AbsolutePath, Is.EqualTo("/other"));
        }

        [PlaywrightTest("page-autowaiting-no-hang.spec.ts", "goBack in the middle of navigation that commits")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GoBackInTheMiddleOfNavigationThatCommits()
        {
            EnsureServer();
            TaskCompletionSource<bool> commit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> stall = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/stall.html", async http =>
            {
                stall.TrySetResult(true);
                await commit.Task.ConfigureAwait(false);
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("hello world").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            _ = page.GoToAsync(Prefix + "/stall.html").ContinueWith(_ => 0);
            await stall.Task.ConfigureAwait(false);
            Task goBackPromise = page.GoBackAsync().ContinueWith(_ => 0);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            commit.TrySetResult(true);
            await goBackPromise.ConfigureAwait(false);
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

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
                return;
            }

            string certPath = Path.Combine(contentRoot, "testCert.cer");
            if (!File.Exists(certPath))
            {
                return;
            }

            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            try
            {
                SimpleServer https = SimpleServer.CreateHttps(TestConstants.HttpsPort, contentRoot);
                await https.StartAsync().ConfigureAwait(false);
                _ownedHttps = https;
                HttpsEmptyPage = TestConstants.HttpsPrefix + "/empty.html";
            }
            catch (Exception)
            {
            }
        }

        private static async Task<bool> FixtureReachableAsync(string origin)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(1),
                };
                using System.Net.Http.HttpResponseMessage response = await client.GetAsync(origin + "/empty.html")
                    .ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
