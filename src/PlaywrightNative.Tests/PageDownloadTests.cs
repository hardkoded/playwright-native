/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// IPage.Download and IDownload after NewContext acceptDownloads.
    /// </summary>
    [TestFixture]
    public class PageDownloadTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("download.spec.ts", "Download event exposes path and filename")]
        [Test]
        [Timeout(30_000)]
        public async Task DownloadEventShouldExposePathAndFilename()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);

            Task<IDownload> downloadTask = page.WaitForDownloadAsync();
            await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);

            Assert.That(download, Is.Not.Null);
            Assert.That(download.Page, Is.SameAs(page));
            Assert.That(download.Url, Does.Contain("direct-download.bin"));
            await WaitForSuggestedFilenameAsync(download, "hello.txt").ConfigureAwait(false);

            string path = await download.PathAsync().ConfigureAwait(false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            Assert.That(await download.FailureAsync().ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("download.spec.ts", "RunAndWaitForDownloadAsync waits for the click")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForDownloadAsyncShouldReturnTheDownload()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);

            IDownload download = await page.RunAndWaitForDownloadAsync(
                () => page.ClickAsync("#dl", new() { NoWaitAfter = true })).ConfigureAwait(false);

            Assert.That(download, Is.Not.Null);
            Assert.That(download.Url, Does.Contain("direct-download.bin"));
        }

        [PlaywrightTest("download.spec.ts", "WaitForDownloadAsync honors a predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForDownloadShouldHonorPredicate()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);

            Task<IDownload> downloadTask = page.WaitForDownloadAsync(
                d => d.Url.Contains("direct-download.bin", StringComparison.Ordinal));
            await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);

            Assert.That(download, Is.Not.Null);
            Assert.That(download.Url, Does.Contain("direct-download.bin"));
        }

        [PlaywrightTest("download.spec.ts", "CancelAsync cancels an in-progress download")]
        [Test]
        [Timeout(30_000)]
        public async Task DownloadCancelAsyncShouldMarkFailureCanceled()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);

            IDownload download = null;
            page.Download += (_, started) =>
            {
                download = started;
                _ = started.CancelAsync();
            };

            // Do not await ClickAsync: the download navigation never finishes after cancel.
            _ = page.ClickAsync("#dl", new() { NoWaitAfter = true });

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (download == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(download, Is.Not.Null);
            string failure = await download.FailureAsync().ConfigureAwait(false);
            Assert.That(failure, Does.Contain("cancel").IgnoreCase);
        }

        [PlaywrightTest("download.spec.ts", "SaveAs copies the artifact")]
        [Test]
        [Timeout(30_000)]
        public async Task DownloadSaveAsShouldCopyTheFile()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);

            Task<IDownload> downloadTask = page.WaitForDownloadAsync();
            await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);

            string dest = Path.Combine(Path.GetTempPath(), "pwsharp-saveas-" + Path.GetRandomFileName() + ".txt");
            try
            {
                await download.SaveAsAsync(dest).ConfigureAwait(false);
                Assert.That(await File.ReadAllTextAsync(dest).ConfigureAwait(false), Is.EqualTo("Hello world"));
            }
            finally
            {
                if (File.Exists(dest))
                {
                    File.Delete(dest);
                }
            }
        }

        private static async Task WaitForSuggestedFilenameAsync(IDownload download, string expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (download.SuggestedFilename != expected && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(download.SuggestedFilename, Is.EqualTo(expected));
        }

        private static void PrepareDownloadRoutes()
        {
            Server.Reset();
            Server.SetRoute("/direct-download.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body><a id=\"dl\" href=\"/direct-download.bin\">download</a></body></html>");
            });
            Server.SetRoute("/direct-download.bin", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=hello.txt";
                return http.Response.WriteAsync("Hello world");
            });
        }

    }
}
