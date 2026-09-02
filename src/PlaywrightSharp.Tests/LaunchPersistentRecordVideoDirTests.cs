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
    /// Official persistent-context <c>recordVideoDir</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentRecordVideoDirTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync RecordVideoDir")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorRecordVideoDir()
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

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-video-" + Guid.NewGuid().ToString("N"));
            string videoDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-video-out-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            Directory.CreateDirectory(videoDir);
            try
            {
                IBrowserContext context = await Playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath,
                    Headless = true,
                    RecordVideoDir = videoDir,
                }).ConfigureAwait(false);

                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Assert.That(page.Video, Is.Not.Null);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1_000).ConfigureAwait(false);
                IVideo video = page.Video;
                await context.CloseAsync().ConfigureAwait(false);

                string path = await video.GetPathAsync().ConfigureAwait(false);
                Assert.That(path, Does.StartWith(videoDir));
                Assert.That(File.Exists(path), Is.True);
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(0));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(videoDir))
                    {
                        Directory.Delete(videoDir, recursive: true);
                    }
                }
                catch (IOException)
                {
                }

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
    }
}
