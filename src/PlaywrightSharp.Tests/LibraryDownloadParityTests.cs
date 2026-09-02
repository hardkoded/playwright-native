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
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/download.spec.ts</c> parity.
    /// Do not edit leftover <c>PageDownloadTests</c>,
    /// <c>ContextDownload*</c>, or <c>Launch*Download*</c>.
    /// Official skip (Node-only <c>_channel.killForTests</c>):
    /// <c>should throw if browser dies</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryDownloadParityTests : PageTestEx
    {
        private const string TargetClosedErrorMessage = "Target page, context or browser has been closed";

        private static readonly byte[] EmptyPdfBytes = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 3 3]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");

        private static readonly byte[] SmallZipBytes = new byte[]
        {
            0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x50, 0x4B, 0x05, 0x06,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;

        private IBrowser _browser;
        private string _outputDir;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        private static bool IsLinux => !TestConstants.IsWindows && !TestConstants.IsMacOSX;

        private static bool IsHeadlessShell
        {
            get
            {
                string path = BrowserExecutableFixture.ChromiumExecutablePath ?? string.Empty;
                return path.IndexOf("headless-shell", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private static bool IsBidi
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("PW_BIDI");
                return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

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

            _outputDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave861-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDir);
            Server?.Reset();
            HttpsServer?.Reset();
            PrepareDefaultDownloadRoutes(Server);
            if (HttpsServer != null)
            {
                PrepareDefaultDownloadRoutes(HttpsServer);
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            HttpsServer?.Reset();
            TestServerSetup.Server?.Reset();
            TestServerSetup.HttpsServer?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            TryDeleteDirectory(_outputDir);
        }

        [PlaywrightTest("download.spec.ts", "should report download when navigation turns into download @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadWhenNavigationTurnsIntoDownload()
        {
            EnsureServer();
            SkipOldChromiumDownloadNavigation();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            Task<IDownload> downloadTask = page.WaitForDownloadAsync();
            Task<Exception> gotoTask = CatchAsync(() => page.GoToAsync(Prefix + "/download"));
            await Task.WhenAll(downloadTask, gotoTask).ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);
            Exception responseOrError = await gotoTask.ConfigureAwait(false);
            Assert.That(download.Page, Is.SameAs(page));
            Assert.That(download.Url, Is.EqualTo(Prefix + "/download"));
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            Assert.That(responseOrError, Is.Not.Null);
            Assert.That(responseOrError.Message, Does.Contain("Download is starting"));
            if (!TestConstants.IsFirefox || IsBidi)
            {
                Assert.That(page.Url, Is.EqualTo("about:blank"));
            }

            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should work with Cross-Origin-Opener-Policy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCrossOriginOpenerPolicy()
        {
            EnsureServer();
            SkipOldChromiumDownloadNavigation();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            Task<IDownload> downloadTask = page.WaitForDownloadAsync();
            Task<Exception> gotoTask = CatchAsync(() => page.GoToAsync(Prefix + "/downloadWithCOOP"));
            await Task.WhenAll(downloadTask, gotoTask).ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);
            Exception responseOrError = await gotoTask.ConfigureAwait(false);
            Assert.That(download.Page, Is.SameAs(page));
            Assert.That(download.Url, Is.EqualTo(Prefix + "/downloadWithCOOP"));
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            Assert.That(responseOrError, Is.Not.Null);
            Assert.That(responseOrError.Message, Does.Contain("Download is starting"));
            if (!TestConstants.IsFirefox || IsBidi)
            {
                Assert.That(page.Url, Is.EqualTo("about:blank"));
            }

            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report downloads with acceptDownloads: false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadsWithAcceptDownloadsFalse()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { AcceptDownloads = false }).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/downloadWithFilename\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            Exception error = await CatchAsync(() => download.PathAsync()).ConfigureAwait(false);
            Assert.That(download.Page, Is.SameAs(page));
            Assert.That(download.Url, Is.EqualTo(Prefix + "/downloadWithFilename"));
            Assert.That(download.SuggestedFilename, Is.EqualTo("file.txt"));
            Assert.That(await download.FailureAsync().ConfigureAwait(false), Does.Contain("acceptDownloads"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("acceptDownloads: true"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report downloads with acceptDownloads: true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadsWithAcceptDownloadsTrue()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report proper download url when download is from download attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportProperDownloadUrlWhenDownloadIsFromDownloadAttribute()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/chromium-linux.zip\" download=\"foo.zip\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            Assert.That(download.Url, Is.EqualTo(Prefix + "/chromium-linux.zip"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report downloads for download attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadsForDownloadAttribute()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/chromium-linux.zip\" download=\"foo.zip\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            Assert.That(download.SuggestedFilename, Is.EqualTo("foo.zip"));
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should save to user-specified path without updating original path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSaveToUserSpecifiedPathWithoutUpdatingOriginalPath()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string userPath = OutputPath("download.txt");
            await download.SaveAsAsync(userPath).ConfigureAwait(false);
            Assert.That(File.Exists(userPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(userPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            string originalPath = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(originalPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(originalPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should save to two different paths with multiple saveAs calls")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSaveToTwoDifferentPathsWithMultipleSaveAsCalls()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string userPath = OutputPath("download.txt");
            await download.SaveAsAsync(userPath).ConfigureAwait(false);
            Assert.That(File.Exists(userPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(userPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            string anotherUserPath = OutputPath("download (2).txt");
            await download.SaveAsAsync(anotherUserPath).ConfigureAwait(false);
            Assert.That(File.Exists(anotherUserPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(anotherUserPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should save to overwritten filepath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSaveToOverwrittenFilepath()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string dir = OutputPath("downloads");
            string userPath = Path.Combine(dir, "download.txt");
            await download.SaveAsAsync(userPath).ConfigureAwait(false);
            Assert.That(Directory.GetFiles(dir).Length, Is.EqualTo(1));
            await download.SaveAsAsync(userPath).ConfigureAwait(false);
            Assert.That(Directory.GetFiles(dir).Length, Is.EqualTo(1));
            Assert.That(File.Exists(userPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(userPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should create subdirectories when saving to non-existent user-specified path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateSubdirectoriesWhenSavingToNonExistentUserSpecifiedPath()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string nestedPath = OutputPath("these", "are", "directories", "download.txt");
            await download.SaveAsAsync(nestedPath).ConfigureAwait(false);
            Assert.That(File.Exists(nestedPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(nestedPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should error when saving with downloads disabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldErrorWhenSavingWithDownloadsDisabled()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync(new() { AcceptDownloads = false }).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string userPath = OutputPath("download.txt");
            Exception error = await CatchAsync(() => download.SaveAsAsync(userPath)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Pass { acceptDownloads: true } when you are creating your browser context"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should error when saving after deletion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldErrorWhenSavingAfterDeletion()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string userPath = OutputPath("download.txt");
            await download.DeleteAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(() => download.SaveAsAsync(userPath)).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(TargetClosedErrorMessage));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report non-navigation downloads")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportNonNavigationDownloads()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                return http.Response.WriteAsync("Hello world");
            });
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a download=\"file.txt\" href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            Assert.That(download.SuggestedFilename, Is.EqualTo("file.txt"));
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report download path within page.on('download', …) handler for Files")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadPathWithinPageOnDownloadHandlerForFiles()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<string> onDownloadPath = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Download += (_, download) =>
            {
                _ = ResolvePathAsync(download, onDownloadPath);
            };
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            await page.ClickAsync("a", new() { NoWaitAfter = true }).ConfigureAwait(false);
            string path = await onDownloadPath.Task.ConfigureAwait(false);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report download path within page.on('download', …) handler for Blobs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadPathWithinPageOnDownloadHandlerForBlobs()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<string> onDownloadPath = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Download += (_, download) =>
            {
                _ = ResolvePathAsync(download, onDownloadPath);
            };
            await page.GoToAsync(Prefix + "/download-blob.html").ConfigureAwait(false);
            await page.ClickAsync("a", new() { NoWaitAfter = true }).ConfigureAwait(false);
            string path = await onDownloadPath.Task.ConfigureAwait(false);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report alt-click downloads")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportAltClickDownloads()
        {
            EnsureServer();
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox does not download on alt-click.");
            }

            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                return http.Response.WriteAsync("Hello world");
            });
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page, modifiers: new[] { KeyboardModifier.Alt }).ConfigureAwait(false);
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report new window downloads")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportNewWindowDownloads()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a target=_blank href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should delete file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDeleteFile()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            await download.DeleteAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.False);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should expose stream")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeStream()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            byte[] data = await ReadDownloadAsync(download).ConfigureAwait(false);
            Assert.That(Encoding.UTF8.GetString(data), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should delete downloads on context destruction")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDeleteDownloadsOnContextDestruction()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download1 = await ClickDownloadAsync(page).ConfigureAwait(false);
            IDownload download2 = await ClickDownloadAsync(page).ConfigureAwait(false);
            string path1 = await download1.PathAsync().ConfigureAwait(false);
            string path2 = await download2.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path1), Is.True);
            Assert.That(File.Exists(path2), Is.True);
            await page.Context.CloseAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path1), Is.False);
            Assert.That(File.Exists(path2), Is.False);
        }

        [PlaywrightTest("download.spec.ts", "should delete downloads on browser gone")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDeleteDownloadsOnBrowserGone()
        {
            EnsureServer();
            IBrowser browser = await LaunchBrowserAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download1 = await ClickDownloadAsync(page).ConfigureAwait(false);
            IDownload download2 = await ClickDownloadAsync(page).ConfigureAwait(false);
            string path1 = await download1.PathAsync().ConfigureAwait(false);
            string path2 = await download2.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path1), Is.True);
            Assert.That(File.Exists(path2), Is.True);
            await browser.CloseAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path1), Is.False);
            Assert.That(File.Exists(path2), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(path1)), Is.False);
        }

        [PlaywrightTest("download.spec.ts", "should save downloads to artifactsDir")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSaveDownloadsToArtifactsDir()
        {
            EnsureServer();
            string artifactsDir = OutputPath("artifacts");
            Directory.CreateDirectory(artifactsDir);
            IBrowser browser = await LaunchBrowserAsync(new BrowserTypeLaunchOptions
            {
                ArtifactsDir = artifactsDir,
            }).ConfigureAwait(false);
            try
            {
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
                IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
                string downloadPath = await download.PathAsync().ConfigureAwait(false);
                Assert.That(downloadPath.StartsWith(artifactsDir, StringComparison.Ordinal), Is.True);
                Assert.That(File.Exists(downloadPath), Is.True);
            }
            finally
            {
                await DisposeQuietlyAsync(browser).ConfigureAwait(false);
            }

            Assert.That(Directory.Exists(artifactsDir), Is.True);
        }

        [PlaywrightTest("download.spec.ts", "should close the context without awaiting the failed download")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCloseTheContextWithoutAwaitingTheFailedDownload()
        {
            EnsureServer();
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Only Chromium downloads on alt-click");
            }

            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + HttpsPrefix + "/downloadWithFilename\" download=\"file.txt\">click me</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page, modifiers: new[] { KeyboardModifier.Alt }).ConfigureAwait(false);
            Task<Exception> downloadErrorTask = CatchAsync(() => download.PathAsync());
            Task<Exception> saveErrorTask = CatchAsync(() => download.SaveAsAsync(OutputPath("download.txt")));
            Task closeTask = page.Context.CloseAsync();
            await Task.WhenAll(downloadErrorTask, saveErrorTask, closeTask).ConfigureAwait(false);
            Exception downloadError = await downloadErrorTask.ConfigureAwait(false);
            Exception saveError = await saveErrorTask.ConfigureAwait(false);
            Assert.That(downloadError, Is.Not.Null);
            Assert.That(downloadError.Message, Is.EqualTo("download.path: canceled"));
            Assert.That(saveError, Is.Not.Null);
            Assert.That(
                new[]
                {
                    "download.saveAs: File not found on disk. Check download.failure() for details.",
                    "download.saveAs: canceled",
                },
                Does.Contain(saveError.Message));
        }

        [PlaywrightTest("download.spec.ts", "should close the context without awaiting the download")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCloseTheContextWithoutAwaitingTheDownload()
        {
            EnsureServer();
            if (TestConstants.IsWebKit && IsLinux)
            {
                Assert.Ignore("WebKit on linux does not convert to the download immediately upon receiving headers");
            }

            SetStallRoute(Server, "/downloadStall");
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/downloadStall\" download=\"file.txt\">click me</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            Task<Exception> downloadErrorTask = CatchAsync(() => download.PathAsync());
            Task<Exception> saveErrorTask = CatchAsync(() => download.SaveAsAsync(OutputPath("download.txt")));
            Task closeTask = page.Context.CloseAsync();
            await Task.WhenAll(downloadErrorTask, saveErrorTask, closeTask).ConfigureAwait(false);
            Exception downloadError = await downloadErrorTask.ConfigureAwait(false);
            Exception saveError = await saveErrorTask.ConfigureAwait(false);
            Assert.That(downloadError, Is.Not.Null);
            Assert.That(
                new[]
                {
                    "download.path: canceled",
                    "download.path: " + TargetClosedErrorMessage,
                },
                Does.Contain(downloadError.Message));
            Assert.That(saveError, Is.Not.Null);
            Assert.That(
                new[]
                {
                    "download.saveAs: canceled",
                    "download.saveAs: " + TargetClosedErrorMessage,
                },
                Does.Contain(saveError.Message));
        }

        [PlaywrightTest("download.spec.ts", "should download large binary.zip")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDownloadLargeBinaryZip()
        {
            EnsureServer();
            byte[] content = new byte[1 << 20];
            RandomNumberGenerator.Fill(content);
            Server.SetRoute("/binary.zip", async http =>
            {
                http.Response.ContentType = "application/zip";
                await http.Response.Body.WriteAsync(content).ConfigureAwait(false);
            });
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/binary.zip\" download=\"binary.zip\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string downloadPath = await download.PathAsync().ConfigureAwait(false);
            byte[] fileContent = await File.ReadAllBytesAsync(downloadPath).ConfigureAwait(false);
            Assert.That(fileContent.Length, Is.EqualTo(content.Length));
            Assert.That(fileContent, Is.EqualTo(content));
            byte[] data = await ReadDownloadAsync(download).ConfigureAwait(false);
            Assert.That(data.Length, Is.EqualTo(content.Length));
            Assert.That(data, Is.EqualTo(content));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should be able to cancel pending downloads")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToCancelPendingDownloads()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/downloadWithDelay\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            await download.CancelAsync().ConfigureAwait(false);
            string failure = await download.FailureAsync().ConfigureAwait(false);
            Assert.That(failure, Is.EqualTo("canceled"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should not fail explicitly to cancel a download even if that is already finished")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFailExplicitlyToCancelADownloadEvenIfThatIsAlreadyFinished()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await download.CancelAsync().ConfigureAwait(false);
            string failure = await download.FailureAsync().ConfigureAwait(false);
            Assert.That(failure, Is.Null);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should report downloads with interception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDownloadsWithInterception()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should emit download event from nested iframes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitDownloadEventFromNestedIframes()
        {
            EnsureServer();
            Server.SetRoute("/1", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<iframe src=\"" + Prefix + "/2\"></iframe>");
            });
            Server.SetRoute("/2", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<iframe src=\"" + Prefix + "/3\"></iframe>");
            });
            Server.SetRoute("/3", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(" <a href=\"" + Prefix + "/download\">download</a>");
            });
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/1").ConfigureAwait(false);
            Task<IDownload> downloadTask = page.WaitForDownloadAsync();
            IFrame frame = page.FrameByUrl(Prefix + "/3");
            Assert.That(frame, Is.Not.Null);
            await frame.ClickAsync("text=download", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);
            string userPath = OutputPath("download.txt");
            await download.SaveAsAsync(userPath).ConfigureAwait(false);
            Assert.That(File.Exists(userPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(userPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should be able to download a PDF file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToDownloadAPdfFile()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"/empty.pdf\" download>download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            await AssertDownloadToPdfAsync(download).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should be able to download a inline PDF file via response interception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToDownloadAInlinePdfFileViaResponseInterception()
        {
            EnsureServer();
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("official it.fixme(browserName === 'webkit')");
            }

            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.RouteAsync("**/empty.pdf", async route =>
            {
                IAPIResponse response = await page.Context.APIRequest.FetchAsync(route.Request).ConfigureAwait(false);
                Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> header in response.Headers)
                {
                    headers[header.Key] = header.Value;
                }

                headers["Content-Disposition"] = "attachment";
                await route.FulfillAsync(response, headers: headers).ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"/empty.pdf\">open</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            await AssertDownloadToPdfAsync(download).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should be able to download a inline PDF file via navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToDownloadAInlinePdfFileViaNavigation()
        {
            EnsureServer();
            if (TestConstants.IsChromium && !IsHeadlessShell)
            {
                Assert.Ignore("We expect PDF Viewer to open up in headed Chromium");
            }

            if (TestConstants.IsFirefox && IsBidi)
            {
                Assert.Ignore("We expect PDF Viewer to open up in Firefox with Bidi");
            }

            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"/empty.pdf\">open</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            await AssertDownloadToPdfAsync(download).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should save to user-specified path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSaveToUserSpecifiedPath()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment";
                return http.Response.WriteAsync("Hello world");
            });
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            string userPath = OutputPath("download.txt");
            await download.SaveAsAsync(userPath).ConfigureAwait(false);
            Assert.That(File.Exists(userPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(userPath).ConfigureAwait(false), Is.EqualTo("Hello world"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should download even if there is no \"attachment\" value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDownloadEvenIfThereIsNoAttachmentValue()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "filename=foo.txt";
                return http.Response.WriteAsync("Hello world");
            });
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            await ClickDownloadAsync(page).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should convert navigation to a resource with unsupported mime type into download")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConvertNavigationToAResourceWithUnsupportedMimeTypeIntoDownload()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                return http.Response.WriteAsync("Hello world");
            });
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            Task<IDownload> downloadTask = page.WaitForDownloadAsync();
            Task gotoTask = CatchAsync(() => page.GoToAsync(Prefix + "/download"));
            await Task.WhenAll(downloadTask, gotoTask).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should download links with data url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDownloadLinksWithDataUrl()
        {
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a download=\"SomeFile.txt\" href=\"data:text/plain;charset=utf8;,hello world\">Download!</a>").ConfigureAwait(false);
            Task<IDownload> downloadTask = page.WaitForDownloadAsync();
            await page.GetByText("Download").ClickAsync(new() { NoWaitAfter = true }).ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);
            Assert.That(download.SuggestedFilename, Is.EqualTo("SomeFile.txt"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("download.spec.ts", "should download successfully when routing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDownloadSuccessfullyWhenRouting()
        {
            EnsureServer();
            IPage page = await _browser.NewPageAsync().ConfigureAwait(false);
            await page.Context.RouteAsync("**/*", route => route.ContinueAsync()).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/chromium-linux.zip\" download=\"foo.zip\">download</a>").ConfigureAwait(false);
            IDownload download = await ClickDownloadAsync(page).ConfigureAwait(false);
            Assert.That(download.SuggestedFilename, Is.EqualTo("foo.zip"));
            Assert.That(download.Url, Is.EqualTo(Prefix + "/chromium-linux.zip"));
            Assert.That(await download.FailureAsync().ConfigureAwait(false), Is.Null);
            await page.CloseAsync().ConfigureAwait(false);
        }

        private static async Task StartOwnedHttpAsync(string contentRoot)
        {
            int basePort = 19961;
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
                return;
            }

            string certPath = EnsureTestCertificate(contentRoot);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19981;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
                    await https.StartAsync().ConfigureAwait(false);
                    _ownedHttps = https;
                    HttpsPrefix = "https://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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

        private static void PrepareDefaultDownloadRoutes(SimpleServer server)
        {
            if (server == null)
            {
                return;
            }

            server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment";
                return http.Response.WriteAsync("Hello world");
            });
            server.SetRoute("/downloadWithFilename", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=file.txt";
                return http.Response.WriteAsync("Hello world");
            });
            server.SetRoute("/downloadWithDelay", async http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=file.txt";
                string payload = new string('a', 4096) + "foo";
                await http.Response.WriteAsync(payload).ConfigureAwait(false);
                await http.Response.Body.FlushAsync().ConfigureAwait(false);
                try
                {
                    await Task.Delay(Timeout.Infinite, http.RequestAborted).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            });
            server.SetRoute("/downloadWithCOOP", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment";
                http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                return http.Response.WriteAsync("Hello world");
            });
            server.SetRoute("/chromium-linux.zip", async http =>
            {
                http.Response.ContentType = "application/zip";
                await http.Response.Body.WriteAsync(SmallZipBytes).ConfigureAwait(false);
            });
            server.SetRoute("/empty.pdf", async http =>
            {
                http.Response.ContentType = "application/pdf";
                await http.Response.Body.WriteAsync(EmptyPdfBytes).ConfigureAwait(false);
            });
        }

        private static void SetStallRoute(SimpleServer server, string path)
        {
            server.SetRoute(path, async http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=file.txt";
                http.Response.StatusCode = 200;
                await http.Response.StartAsync().ConfigureAwait(false);
                byte[] payload = Encoding.UTF8.GetBytes("Hello world");
                await http.Response.Body.WriteAsync(payload).ConfigureAwait(false);
                await http.Response.Body.FlushAsync().ConfigureAwait(false);
                try
                {
                    await Task.Delay(Timeout.Infinite, http.RequestAborted).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private static async Task<IDownload> ClickDownloadAsync(
            IPage page,
            string selector = "a",
            IEnumerable<KeyboardModifier> modifiers = null)
        {
            Task<IDownload> waitTask = page.WaitForDownloadAsync();
            await page.ClickAsync(selector, new() { Modifiers = modifiers, NoWaitAfter = true }).ConfigureAwait(false);
            return await waitTask.ConfigureAwait(false);
        }

        private static async Task ResolvePathAsync(IDownload download, TaskCompletionSource<string> completion)
        {
            try
            {
                completion.TrySetResult(await download.PathAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }

        private static async Task<byte[]> ReadDownloadAsync(IDownload download)
        {
            using Stream stream = await download.CreateReadStreamAsync().ConfigureAwait(false);
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer).ConfigureAwait(false);
            return buffer.ToArray();
        }

        private static async Task AssertDownloadToPdfAsync(IDownload download)
        {
            Assert.That(download.SuggestedFilename, Is.EqualTo("empty.pdf"));
            byte[] data = await ReadDownloadAsync(download).ConfigureAwait(false);
            Assert.That(download.Url.EndsWith("/empty.pdf", StringComparison.Ordinal), Is.True);
            string expectedPrefix = "%PDF";
            for (int i = 0; i < expectedPrefix.Length; i++)
            {
                Assert.That(data[i], Is.EqualTo((byte)expectedPrefix[i]));
            }

            Assert.That(data.Length, Is.EqualTo(EmptyPdfBytes.Length));
            Assert.That(data, Is.EqualTo(EmptyPdfBytes));
        }

        private static Task<IBrowser> LaunchBrowserAsync(BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
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

        private string OutputPath(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return _outputDir;
            }

            return Path.Combine(_outputDir, Path.Combine(parts));
        }

        private void SkipOldChromiumDownloadNavigation()
        {
            if (!TestConstants.IsChromium)
            {
                return;
            }

            string version = _browser?.Version;
            if (string.IsNullOrEmpty(version))
            {
                return;
            }

            int dot = version.IndexOf('.');
            string majorText = dot < 0 ? version : version.Substring(0, dot);
            if (int.TryParse(majorText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int major)
                && major < 140)
            {
                Assert.Ignore("old chromium throws net::ERR_ABORTED, depends on https://chromium-review.googlesource.com/c/chromium/src/+/6696011");
            }
        }
    }
}
