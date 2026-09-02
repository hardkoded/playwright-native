/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <c>recordVideoDir</c>.
    /// </summary>
    [TestFixture]
    public class ContextVideoTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("video.spec.ts", "recordVideoDir writes an mp4 on close")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWriteVideoFileOnContextClose()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Page.startScreencast video recording is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string directory = TempVideoDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = directory }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Assert.That(page.Video, Is.Not.Null);

                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1_000).ConfigureAwait(false);
                IVideo video = page.Video;
                await context.CloseAsync().ConfigureAwait(false);

                string path = await video.GetPathAsync().ConfigureAwait(false);
                string alias = await video.PathAsync().ConfigureAwait(false);
                Assert.That(path, Does.StartWith(directory));
                Assert.That(alias, Is.EqualTo(path));
                Assert.That(File.Exists(path), Is.True);
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(0));
            }
            finally
            {
                TryDeleteDir(directory);
            }
        }

        [PlaywrightTest("video.spec.ts", "options bag recordVideoDir")]
        [Test]
        [Timeout(30_000)]
        public async Task OptionsBagShouldRecordVideo()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Page.startScreencast video recording is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string directory = TempVideoDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
                {
                    RecordVideoDir = directory,
                    RecordVideoSize = new RecordVideoSize { Width = 640, Height = 480 },
                }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1_000).ConfigureAwait(false);
                IVideo video = page.Video;
                Assert.That(video, Is.Not.Null);
                await context.CloseAsync().ConfigureAwait(false);

                string path = await video.GetPathAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(0));
            }
            finally
            {
                TryDeleteDir(directory);
            }
        }

        [PlaywrightTest("video.spec.ts", "SaveAsAsync copies the video")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSaveVideoAs()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Page.startScreencast video recording is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string directory = TempVideoDir();
            string copyPath = Path.Combine(directory, "copy", "saved.mp4");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = directory }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1_000).ConfigureAwait(false);
                IVideo video = page.Video;
                Assert.That(video, Is.Not.Null);
                await context.CloseAsync().ConfigureAwait(false);

                await video.SaveAsAsync(copyPath).ConfigureAwait(false);
                Assert.That(File.Exists(copyPath), Is.True);
                Assert.That(new FileInfo(copyPath).Length, Is.GreaterThan(0));
            }
            finally
            {
                TryDeleteDir(directory);
            }
        }

        [PlaywrightTest("video.spec.ts", "DeleteAsync removes the video")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDeleteVideo()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Page.startScreencast video recording is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string directory = TempVideoDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = directory }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1_000).ConfigureAwait(false);
                IVideo video = page.Video;
                Assert.That(video, Is.Not.Null);
                await context.CloseAsync().ConfigureAwait(false);

                string path = await video.GetPathAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
                await video.DeleteAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                TryDeleteDir(directory);
            }
        }

        private static string TempVideoDir()
            => Path.Combine(Path.GetTempPath(), "pwsharp-video-" + Guid.NewGuid().ToString("N"));

        private static void TryDeleteDir(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
