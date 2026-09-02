/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/video.spec.ts</c> titles.
    /// Skipped (Node <c>_channel.killForTests</c>):
    /// <c>should throw if browser dies</c>.
    /// Do not edit leftover video classes.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryVideoParityTests : PageTestEx
    {
        private static string Prefix => TestConstants.ServerUrl;

        private static string CrossProcess => TestConstants.CrossProcessHttpPrefix;

        [PlaywrightTest("video.spec.ts", "should not have video by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHaveVideoByDefault()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            Assert.That(page.Video, Is.Null);
        }

        [PlaywrightTest("video.spec.ts", "should not throw without recordVideo.dir")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWithoutRecordVideoDir()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
            Assert.That(context, Is.Not.Null);
        }

        [PlaywrightTest("video.spec.ts", "should capture static page")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldCaptureStaticPage()
        {
            string dir = TempDir();
            try
            {
                RecordVideoSize size = new() { Width = 320, Height = 240 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = size, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.ExpectRedFrames(await page.Video.PathAsync().ConfigureAwait(false), 320, 240);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should pad a short frame to the video size")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldPadAShortFrameToTheVideoSize()
        {
            string dir = TempDir();
            try
            {
                RecordVideoSize videoSize = new() { Width = 800, Height = 600 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = videoSize, ViewportSize = new ViewportSize { Width = 800, Height = 396 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.Probe probe = OfficialVideo.Read(await page.Video.PathAsync().ConfigureAwait(false));
                Assert.That(probe.Width, Is.EqualTo(800));
                Assert.That(probe.Height, Is.EqualTo(600));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should continue recording main page after popup closes")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldContinueRecordingMainPageAfterPopupCloses()
        {
            string dir = TempDir();
            try
            {
                RecordVideoSize size = new() { Width = 320, Height = 240 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = size, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<a target=_blank href=\"about:blank\">clickme</a>").ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                await page.ClickAsync("a").ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                await popup.CloseAsync().ConfigureAwait(false);
                await page.EvaluateAsync(@"() => {
      document.body.textContent = '';
      document.body.style.backgroundColor = 'red';
    }").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.ExpectRedFrames(await page.Video.PathAsync().ConfigureAwait(false), 320, 240);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should expose video path")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldExposeVideoPath()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                string path = await page.Video.PathAsync().ConfigureAwait(false);
                Assert.That(path, Does.Contain(dir));
                await context.CloseAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should delete video")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldDeleteVideo()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Task deletePromise = page.Video.DeleteAsync();
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string videoPath = await page.Video.PathAsync().ConfigureAwait(false);
                await deletePromise.ConfigureAwait(false);
                Assert.That(File.Exists(videoPath), Is.False);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should expose video path blank page")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldExposeVideoPathBlankPage()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                string path = await page.Video.PathAsync().ConfigureAwait(false);
                Assert.That(path, Does.Contain(dir));
                await context.CloseAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should work with weird screen resolution")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldWorkWithWeirdScreenResolution()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 1904, Height = 609 }, ViewportSize = new ViewportSize { Width = 1904, Height = 609 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                string path = await page.Video.PathAsync().ConfigureAwait(false);
                Assert.That(path, Does.Contain(dir));
                await context.CloseAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should work with relative path for recordVideo.dir")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldWorkWithRelativePathForRecordVideoDir()
        {
            string abs = TempDir();
            string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), abs);
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = relative, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                string videoPath = await page.Video.PathAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                Assert.That(File.Exists(videoPath), Is.True);
            }
            finally
            {
                TryDeleteDir(abs);
            }
        }

        [PlaywrightTest("video.spec.ts", "should expose video path blank popup")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldExposeVideoPathBlankPopup()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                await page.EvaluateAsync("() => window.open('about:blank')").ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                string path = await popup.Video.PathAsync().ConfigureAwait(false);
                Assert.That(path, Does.Contain(dir));
                await context.CloseAsync().ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should capture navigation")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldCaptureNavigation()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/background-color.html#rgb(0,0,0)").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.GoToAsync(CrossProcess + "/background-color.html#rgb(100,100,100)").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string videoFile = await page.Video.PathAsync().ConfigureAwait(false);
                OfficialVideo.Probe probe = OfficialVideo.Read(videoFile);
                Assert.That(probe.Duration, Is.GreaterThan(0));
                Assert.That(OfficialVideo.FindFrame(videoFile, image => OfficialVideo.EveryPixel(image, OfficialVideo.IsAlmostBlack)), Is.True);
                using Image<Rgba32> last = OfficialVideo.LastFrame(videoFile);
                OfficialVideo.ExpectAll(last, OfficialVideo.IsAlmostGray);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should capture css transformation")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldCaptureCssTransformation()
        {
            string dir = TempDir();
            try
            {
                RecordVideoSize size = new() { Width = 600, Height = 400 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = size, ViewportSize = new ViewportSize { Width = 600, Height = 400 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/rotate-z.html").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string videoFile = await page.Video.PathAsync().ConfigureAwait(false);
                Assert.That(OfficialVideo.Read(videoFile).Duration, Is.GreaterThan(0));
                using Image<Rgba32> pixels = OfficialVideo.LastFrame(videoFile, 95, 45, 1, 1);
                OfficialVideo.ExpectAll(pixels, OfficialVideo.IsAlmostRed);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should work for popups")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldWorkForPopups()
        {
            string dir = TempDir();
            try
            {
                RecordVideoSize size = new() { Width = 600, Height = 400 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = size, ViewportSize = new ViewportSize { Width = 600, Height = 400 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                Task<IPage> popupTask = page.WaitForEventAsync(PageEvent.Popup);
                await page.EvaluateAsync("() => { window.open('about:blank'); }").ConfigureAwait(false);
                IPage popup = await popupTask.ConfigureAwait(false);
                await popup.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                await Task.WhenAll(EnsureSomeFramesAsync(page), EnsureSomeFramesAsync(popup)).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string pageVideo = await page.Video.PathAsync().ConfigureAwait(false);
                string popupVideo = await popup.Video.PathAsync().ConfigureAwait(false);
                Assert.That(pageVideo, Is.Not.EqualTo(popupVideo));
                OfficialVideo.ExpectRedFrames(popupVideo, 600, 400);
                Assert.That(Directory.GetFiles(dir, "*.webm").Length, Is.EqualTo(2));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should scale frames down to the requested size ")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldScaleFramesDownToTheRequestedSize()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("official fixme(browserName === 'chromium' && !isHeadlessShell): Chromium (but not headless shell) has a min width issue");
            }

            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 640, Height = 480 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/checkerboard.html").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>(".container", "container => { container.firstElementChild.classList.remove('red'); }").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>(".container", "container => { container.firstElementChild.classList.add('red'); }").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string videoFile = await page.Video.PathAsync().ConfigureAwait(false);
                Assert.That(OfficialVideo.Read(videoFile).Duration, Is.GreaterThan(0));
                using Image<Rgba32> a = OfficialVideo.LastFrame(videoFile, 10, 10, 1, 1);
                OfficialVideo.ExpectAll(a, OfficialVideo.IsAlmostRed);
                using Image<Rgba32> b = OfficialVideo.LastFrame(videoFile, 300, 10, 1, 1);
                OfficialVideo.ExpectAll(b, OfficialVideo.IsAlmostGray);
                using Image<Rgba32> c = OfficialVideo.LastFrame(videoFile, 10, 200, 1, 1);
                OfficialVideo.ExpectAll(c, OfficialVideo.IsAlmostGray);
                using Image<Rgba32> d = OfficialVideo.LastFrame(videoFile, 300, 200, 1, 1);
                OfficialVideo.ExpectAll(d, OfficialVideo.IsAlmostRed);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should use viewport scaled down to fit into 800x800 as default size")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldUseViewportScaledDownToFitInto800x800AsDefaultSize()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, ViewportSize = new ViewportSize { Width = 1600, Height = 1200 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.Probe probe = OfficialVideo.Read(await page.Video.PathAsync().ConfigureAwait(false));
                Assert.That(probe.Width, Is.EqualTo(800));
                Assert.That(probe.Height, Is.EqualTo(600));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should be 800x450 by default")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldBe800x450ByDefault()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.Probe probe = OfficialVideo.Read(await page.Video.PathAsync().ConfigureAwait(false));
                Assert.That(probe.Width, Is.EqualTo(800));
                Assert.That(probe.Height, Is.EqualTo(450));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should be 800x600 with null viewport")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldBe800x600WithNullViewport()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, ViewportSize = ViewportSize.NoViewport }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.Probe probe = OfficialVideo.Read(await page.Video.PathAsync().ConfigureAwait(false));
                Assert.That(probe.Width, Is.EqualTo(800));
                Assert.That(probe.Height, Is.EqualTo(600));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should capture static page in persistent context @smoke")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldCaptureStaticPageInPersistentContext()
        {
            string dir = TempDir();
            string userDataDir = TempDir();
            IBrowserContext context = null;
            try
            {
                context = await LaunchPersistentAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    RecordVideoDir = dir,
                    RecordVideoSize = new RecordVideoSize { Width = 600, Height = 400 },
                    ViewportSize = new ViewportSize { Width = 600, Height = 400 },
                }).ConfigureAwait(false);
                IPage page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync().ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                context = null;
                string videoFile = await page.Video.PathAsync().ConfigureAwait(false);
                OfficialVideo.Probe probe = OfficialVideo.Read(videoFile);
                Assert.That(probe.Duration, Is.GreaterThan(0));
                Assert.That(probe.Width, Is.EqualTo(600));
                Assert.That(probe.Height, Is.EqualTo(400));
                using Image<Rgba32> last = OfficialVideo.LastFrame(videoFile);
                OfficialVideo.ExpectAll(last, OfficialVideo.IsAlmostRed);
            }
            finally
            {
                if (context != null)
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }

                TryDeleteDir(dir);
                TryDeleteDir(userDataDir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should emulate an iphone")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldEmulateAnIphone()
        {
            string dir = TempDir();
            try
            {
                BrowserContextOptions options = new BrowserContextOptions(Playwright.Devices["iPhone 6"])
                {
                    RecordVideoDir = dir,
                };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(options).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.Probe probe = OfficialVideo.Read(await page.Video.PathAsync().ConfigureAwait(false));
                Assert.That(probe.Width, Is.EqualTo(374));
                Assert.That(probe.Height, Is.EqualTo(666));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should throw on browser close")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldThrowOnBrowserClose()
        {
            string dir = TempDir();
            try
            {
                IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await browser.CloseAsync().ConfigureAwait(false);
                string file = Path.Combine(dir, "saved-video-");
                Exception saveResult = null;
                try
                {
                    await page.Video.SaveAsAsync(file).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    saveResult = ex;
                }

                Assert.That(saveResult, Is.Not.Null);
                Assert.That(saveResult.Message, Does.Contain("browser has been closed"));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should wait for video to finish if page was closed")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldWaitForVideoToFinishIfPageWasClosed()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                await browser.CloseAsync().ConfigureAwait(false);
                string[] videoFiles = Directory.GetFiles(dir, "*.webm");
                Assert.That(videoFiles.Length, Is.EqualTo(1));
                OfficialVideo.Probe probe = OfficialVideo.Read(videoFiles[0]);
                Assert.That(probe.Width, Is.EqualTo(320));
                Assert.That(probe.Height, Is.EqualTo(240));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should close ffmpeg even if there were no frames")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldCloseFfmpegEvenIfThereWereNoFrames()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page1 = await context.NewPageAsync().ConfigureAwait(false);
                await page1.CloseAsync().ConfigureAwait(false);
                IPage page2 = await context.NewPageAsync().ConfigureAwait(false);
                await page2.CloseAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                await browser.CloseAsync().ConfigureAwait(false);
                Assert.That(Directory.GetFiles(dir, "*.webm").Length, Is.EqualTo(2));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should not create video for internal pages")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldNotCreateVideoForInternalPages()
        {
            TestServerSetup.Server.SetRoute("/empty.html", ctx =>
            {
                ctx.Response.Headers["Set-Cookie"] = "name=value";
                return Task.CompletedTask;
            });
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                System.Collections.Generic.IReadOnlyList<BrowserContextCookiesResult> cookies = await context.CookiesAsync().ConfigureAwait(false);
                Assert.That(cookies.Count, Is.EqualTo(1));
                await context.StorageStateAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                Assert.That(Directory.GetFiles(dir).Length, Is.EqualTo(1));
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should capture full viewport")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldCaptureFullViewport()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("official fixme(browserName === 'chromium' && !isHeadlessShell): The square is not on the video");
            }

            string dir = TempDir();
            try
            {
                RecordVideoSize size = new() { Width = 600, Height = 400 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 600, Height = 400 }, RecordVideoDir = dir, RecordVideoSize = size }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<div style='margin: 0; background: red; position: fixed; right:0; bottom:0; width: 30; height: 30;'></div>").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string[] videoFiles = Directory.GetFiles(dir, "*.webm");
                Assert.That(videoFiles.Length, Is.EqualTo(1));
                OfficialVideo.Probe probe = OfficialVideo.Read(videoFiles[0]);
                Assert.That(probe.Width, Is.EqualTo(600));
                Assert.That(probe.Height, Is.EqualTo(400));
                using Image<Rgba32> pixels = OfficialVideo.LastFrame(videoFiles[0], 580, 380, 1, 1);
                OfficialVideo.ExpectAll(pixels, OfficialVideo.IsAlmostRed);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should capture full viewport on hidpi")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldCaptureFullViewportOnHidpi()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("official fixme(browserName === 'chromium' && !isHeadlessShell): The square is not on the video");
            }

            string dir = TempDir();
            try
            {
                RecordVideoSize size = new() { Width = 600, Height = 400 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 600, Height = 400 }, DeviceScaleFactor = 3, RecordVideoDir = dir, RecordVideoSize = size }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<div style='margin: 0; background: red; position: fixed; right:0; bottom:0; width: 30; height: 30;'></div>").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string[] videoFiles = Directory.GetFiles(dir, "*.webm");
                Assert.That(videoFiles.Length, Is.EqualTo(1));
                OfficialVideo.Probe probe = OfficialVideo.Read(videoFiles[0]);
                Assert.That(probe.Width, Is.EqualTo(600));
                Assert.That(probe.Height, Is.EqualTo(400));
                using Image<Rgba32> pixels = OfficialVideo.LastFrame(videoFiles[0], 580, 380, 1, 1);
                OfficialVideo.ExpectAll(pixels, OfficialVideo.IsAlmostRed);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should work with video+trace")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldWorkWithVideoAndTrace()
        {
            string dir = TempDir();
            string traceFile = Path.Combine(dir, "trace.zip");
            try
            {
                RecordVideoSize size = new() { Width = 500, Height = 400 };
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = size, ViewportSize = new ViewportSize { Width = 500, Height = 400 } }).ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = traceFile }).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                OfficialVideo.ExpectRedFrames(await page.Video.PathAsync().ConfigureAwait(false), 500, 400);
                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(traceFile);
                System.Text.Json.JsonElement? frame = null;
                foreach (System.Text.Json.JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out System.Text.Json.JsonElement type)
                        && type.GetString() == "screencast-frame")
                    {
                        frame = item;
                    }
                }

                Assert.That(frame.HasValue, Is.True);
                string file = frame.Value.GetProperty("file").GetString();
                byte[] buffer = parsed.Resources[file];
                using Image<Rgba32> image = Image.Load<Rgba32>(buffer);
                Assert.That(image.Width, Is.EqualTo(500));
                Assert.That(image.Height, Is.EqualTo(400));
                Rgba32 center = image[250, 200];
                Assert.That(OfficialVideo.IsAlmostRed(new OfficialVideo.Pixel(center.R, center.G, center.B, center.A)), Is.True);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        [PlaywrightTest("video.spec.ts", "should saveAs video")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldSaveAsVideo()
        {
            string dir = TempDir();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordVideoDir = dir, RecordVideoSize = new RecordVideoSize { Width = 320, Height = 240 }, ViewportSize = new ViewportSize { Width = 320, Height = 240 } }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.EvaluateAsync("() => document.body.style.backgroundColor = 'red'").ConfigureAwait(false);
                await EnsureSomeFramesAsync(page).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);
                string saveAsPath = Path.Combine(dir, "my-video.webm");
                await page.Video.SaveAsAsync(saveAsPath).ConfigureAwait(false);
                Assert.That(File.Exists(saveAsPath), Is.True);
            }
            finally
            {
                TryDeleteDir(dir);
            }
        }

        private static async Task EnsureSomeFramesAsync(IPage page)
        {
            for (int i = 0; i < 100; i++)
            {
                await page.EvaluateAsync(
                    "() => new Promise(f => requestAnimationFrame(() => requestAnimationFrame(f)))").ConfigureAwait(false);
            }

            await page.ScreenshotAsync().ConfigureAwait(false);
        }

        private static async Task<IBrowserContext> LaunchPersistentAsync(string userDataDir, BrowserTypeLaunchPersistentContextOptions options)
        {
            options.Headless = true;
            IBrowserType browserType;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available.");
                }

                browserType = Playwright.Webkit;
                options.ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available.");
                }

                browserType = Playwright.Chromium;
                options.ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            Directory.CreateDirectory(userDataDir);
            return await browserType.LaunchPersistentContextAsync(userDataDir, options).ConfigureAwait(false);
        }

        private static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "pw-video-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

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
