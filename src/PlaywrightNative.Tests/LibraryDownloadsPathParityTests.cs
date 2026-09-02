/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/downloads-path.spec.ts</c> parity.
    /// Do not edit leftover <c>PageDownloadTests</c>,
    /// <c>ContextDownload*</c>, or <c>Launch*Download*</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryDownloadsPathParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private string _outputDir;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            await StartOwnedHttpAsync(contentRoot).ConfigureAwait(false);
            if (Server == null && TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
            }

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
        }

        [SetUp]
        public void SetUp()
        {
            _outputDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave862-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDir);
            Server?.Reset();
            if (Server != null)
            {
                Server.SetRoute("/download", http =>
                {
                    http.Response.ContentType = "application/octet-stream";
                    http.Response.Headers["Content-Disposition"] = "attachment; filename=file.txt";
                    return http.Response.WriteAsync("Hello world");
                });
            }
        }

        [TearDown]
        public void TearDown()
        {
            TryDeleteDirectory(_outputDir);
        }

        [PlaywrightTest("downloads-path.spec.ts", "should keep downloadsPath folder")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepDownloadsPathFolder()
        {
            EnsureServer();
            IBrowser browser = await LaunchBrowserAsync(new BrowserTypeLaunchOptions
            {
                DownloadsPath = _outputDir,
            }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
                Assert.That(download.Url, Is.EqualTo(Prefix + "/download"));
                Assert.That(download.SuggestedFilename, Is.EqualTo("file.txt"));
                await CatchAsync(() => download.PathAsync()).ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }

            Assert.That(Directory.Exists(_outputDir), Is.True);
        }

        [PlaywrightTest("downloads-path.spec.ts", "should delete downloads when context closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDeleteDownloadsWhenContextCloses()
        {
            EnsureServer();
            IBrowser browser = await LaunchBrowserAsync(new BrowserTypeLaunchOptions
            {
                DownloadsPath = _outputDir,
            }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
                string path = await download.PathAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
                await page.CloseAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("downloads-path.spec.ts", "should report downloads in downloadsPath folder")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadsInDownloadsPathFolder()
        {
            EnsureServer();
            IBrowser browser = await LaunchBrowserAsync(new BrowserTypeLaunchOptions
            {
                DownloadsPath = _outputDir,
            }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
                string path = await download.PathAsync().ConfigureAwait(false);
                Assert.That(path.StartsWith(_outputDir, StringComparison.Ordinal), Is.True);
                await page.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("downloads-path.spec.ts", "should report downloads in downloadsPath folder with a relative path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadsInDownloadsPathFolderWithARelativePath()
        {
            EnsureServer();
            string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), _outputDir);
            IBrowser browser = await LaunchBrowserAsync(new BrowserTypeLaunchOptions
            {
                DownloadsPath = relative,
            }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
                string downloadPath = await download.PathAsync().ConfigureAwait(false);
                Assert.That(downloadPath.StartsWith(_outputDir, StringComparison.Ordinal), Is.True);
                await page.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }
        }

        [PlaywrightTest("downloads-path.spec.ts", "should accept downloads in persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptDownloadsInPersistentContext()
        {
            EnsureServer();
            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave862-persist-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            IBrowserContext context = null;
            try
            {
                context = await LaunchPersistentAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    DownloadsPath = _outputDir,
                    Headless = true,
                }).ConfigureAwait(false);
                IPage page = context.Pages.Count > 0
                    ? FirstPage(context)
                    : await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
                Assert.That(download.Url, Is.EqualTo(Prefix + "/download"));
                Assert.That(download.SuggestedFilename, Is.EqualTo("file.txt"));
                string path = await download.PathAsync().ConfigureAwait(false);
                Assert.That(path.StartsWith(_outputDir, StringComparison.Ordinal), Is.True);
            }
            finally
            {
                if (context != null)
                {
                    await DisposeQuietlyAsync(context).ConfigureAwait(false);
                }

                TryDeleteDirectory(userDataDir);
            }
        }

        [PlaywrightTest("downloads-path.spec.ts", "should delete downloads when persistent context closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDeleteDownloadsWhenPersistentContextCloses()
        {
            EnsureServer();
            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave862-persist-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            IBrowserContext context = null;
            try
            {
                context = await LaunchPersistentAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    DownloadsPath = _outputDir,
                    Headless = true,
                }).ConfigureAwait(false);
                IPage page = context.Pages.Count > 0
                    ? FirstPage(context)
                    : await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
                string path = await download.PathAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
                await context.CloseAsync().ConfigureAwait(false);
                context = null;
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                if (context != null)
                {
                    await DisposeQuietlyAsync(context).ConfigureAwait(false);
                }

                TryDeleteDirectory(userDataDir);
            }
        }

        private static IPage FirstPage(IBrowserContext context)
        {
            foreach (IPage page in context.Pages)
            {
                return page;
            }

            return null;
        }

        private static async Task StartOwnedHttpAsync(string contentRoot)
        {
            int basePort = 19962;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static async Task<IDownload> ClickDownloadAsync(IPage page)
        {
            Task<IDownload> waitTask = page.WaitForDownloadAsync();
            await page.ClickAsync("a", new() { NoWaitAfter = true }).ConfigureAwait(false);
            return await waitTask.ConfigureAwait(false);
        }

        private static Task<IBrowser> LaunchBrowserAsync(BrowserTypeLaunchOptions options)
        {
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                options.ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath;
                return Playwright.LaunchWebkitAsync(options);
            }

            if (TestConstants.IsFirefox)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                options.ExecutablePath = BrowserExecutableFixture.FirefoxExecutablePath;
                return Playwright.LaunchFirefoxAsync(options);
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            options.ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            return Playwright.LaunchChromiumAsync(options);
        }

        private static Task<IBrowserContext> LaunchPersistentAsync(
            string userDataDir,
            BrowserTypeLaunchPersistentContextOptions options)
        {
            IBrowserType browserType;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                options.ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return Task.FromResult<IBrowserContext>(null);
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                options.ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            return browserType.LaunchPersistentContextAsync(userDataDir, options);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
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

        private static void TryDeleteDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
