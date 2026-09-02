/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-screenshot.spec.ts</c> titles.
    /// Skipped (Node-only): <c>__testHookBeforeScreenshot</c>,
    /// <c>should capture screenshots after layoutchanges in transitionend event</c>
    /// (uses Node <c>window.builtins.Date</c>).
    /// Do not edit leftover <c>PageScreenshot*</c> /
    /// <c>ExpectScreenshot*</c>.
    /// </summary>
    [TestFixture]
    public class PageScreenshotParityTests : PageTestEx
    {
        private static string Prefix => TestConstants.ServerUrl;

        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-screenshot.spec.ts", "should throw on clip outside the viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnClipOutsideTheViewport()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.ScreenshotAsync(new()
            {
                Clip = new Clip
                {
                    X = 50,
                    Y = 650,
                    Width = 100,
                    Height = 100,
                }
            }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Clipped area is either empty or outside the resulting image"));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should throw on a negative clip size")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnANegativeClipSize()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.ScreenshotAsync(new()
            {
                Clip = new Clip
                {
                    X = 50,
                    Y = 50,
                    Width = -100,
                    Height = 100,
                }
            }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Expected options.clip.width to be greater than 0"));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should restore viewport after fullPage screenshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRestoreViewportAfterFullPageScreenshot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            byte[] screenshot = await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
            Assert.That(screenshot, Is.Not.Null);
            Assert.That(screenshot.Length, Is.GreaterThan(0));
            Assert.That(page.ViewportSize.Width, Is.EqualTo(500));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(500));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "path option should throw for unsupported mime type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PathOptionShouldThrowForUnsupportedMimeType()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.ScreenshotAsync(new() { Path = "file.txt" }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("path: unsupported mime type \"text/plain\""));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "quality option should throw for png")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QualityOptionShouldThrowForPng()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.ScreenshotAsync(new() { Quality = 10 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("options.quality is unsupported for the png"));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "zero quality option should throw for png")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ZeroQualityOptionShouldThrowForPng()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.ScreenshotAsync(new() { Quality = 0, Type = ScreenshotType.Png }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("options.quality is unsupported for the png"));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "quality option should work for jpeg")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QualityOptionShouldWorkForJpeg()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            byte[] zeroQuality = await page.ScreenshotAsync(new() { Type = ScreenshotType.Jpeg, Quality = 0 }).ConfigureAwait(false);
            byte[] highQuality = await page.ScreenshotAsync(new() { Type = ScreenshotType.Jpeg, Quality = 100 }).ConfigureAwait(false);
            Assert.That(zeroQuality.Length, Is.LessThan(highQuality.Length));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should prefer type over extension")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreferTypeOverExtension()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string outputPath = Path.Combine(Path.GetTempPath(), "pw-shot-" + Path.GetRandomFileName() + ".png");
            try
            {
                byte[] buffer = await page.ScreenshotAsync(new() { Path = outputPath, Type = ScreenshotType.Jpeg }).ConfigureAwait(false);
                Assert.That(buffer[0], Is.EqualTo(0xFF));
                Assert.That(buffer[1], Is.EqualTo(0xD8));
                Assert.That(buffer[2], Is.EqualTo(0xFF));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should not issue resize event")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotIssueResizeEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            bool resizeTriggered = false;
            await page.ExposeFunctionAsync("resize", () =>
            {
                resizeTriggered = true;
                return true;
            }).ConfigureAwait(false);
            await page.EvaluateAsync("() => { window.addEventListener('resize', () => window.resize()); }").ConfigureAwait(false);
            await page.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(resizeTriggered, Is.False);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-sanity.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should clip rect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClipRect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-clip-rect.png", await page.ScreenshotAsync(new()
            {
                Clip = new Clip
                {
                    X = 50,
                    Y = 100,
                    Width = 150,
                    Height = 100,
                }
            }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should clip rect with fullPage")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClipRectWithFullPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollBy(150, 200)").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-clip-rect.png", await page.ScreenshotAsync(new() { FullPage = true, Clip = new Clip { X = 50, Y = 100, Width = 150, Height = 100 } }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should clip elements to the viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClipElementsToTheViewport()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-offscreen-clip.png", await page.ScreenshotAsync(new()
            {
                Clip = new Clip
                {
                    X = 50,
                    Y = 450,
                    Width = 1000,
                    Height = 100,
                }
            }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should run in parallel")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunInParallel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            Task<byte[]>[] tasks = new Task<byte[]>[3];
            for (int i = 0; i < 3; i++)
            {
                tasks[i] = page.ScreenshotAsync(new() { Clip = new Clip { X = 50 * i, Y = 0, Width = 50, Height = 50 } });
            }

            byte[][] screenshots = await Task.WhenAll(tasks).ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("grid-cell-1.png", screenshots[1]);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should take fullPage screenshots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeFullPageScreenshots()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-grid-fullpage.png", await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should allow transparency")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowTransparency()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            await page.SetContentAsync(@"
      <style>
        body { margin: 0 }
        div { width: 300px; height: 100px; }
      </style>
      <div style=""background:black""></div>
      <div style=""background:white""></div>
      <div style=""background:transparent""></div>
    ").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("transparent.png", await page.ScreenshotAsync(new() { OmitBackground = true }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should render white background on jpeg file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRenderWhiteBackgroundOnJpegFile()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("white.jpg", await page.ScreenshotAsync(new() { OmitBackground = true, Type = ScreenshotType.Jpeg }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work with odd clip size on Retina displays")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithOddClipSizeOnRetinaDisplays()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-clip-odd-size.png", await page.ScreenshotAsync(new()
            {
                Clip = new Clip
                {
                    X = 0,
                    Y = 0,
                    Width = 11,
                    Height = 11,
                }
            }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work for canvas")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForCanvas()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/screenshots/canvas.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-canvas.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work for translateZ")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForTranslateZ()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/screenshots/translateZ.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-translateZ.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work with iframe in shadow")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithIframeInShadow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid-iframe-in-shadow.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-iframe.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "path option should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PathOptionShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            string outputPath = Path.Combine(Path.GetTempPath(), "pw-shot-" + Path.GetRandomFileName() + ".png");
            try
            {
                await page.ScreenshotAsync(new() { Path = outputPath }).ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("screenshot-sanity.png", outputPath);
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "path option should create subdirectories")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PathOptionShouldCreateSubdirectories()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            string outputPath = Path.Combine(Path.GetTempPath(), "pw-shot-" + Path.GetRandomFileName(), "these", "are", "directories", "screenshot.png");
            try
            {
                await page.ScreenshotAsync(new() { Path = outputPath }).ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("screenshot-sanity.png", outputPath);
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should hide elements based on attr")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHideElementsBasedOnAttr()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.Locator("div").Nth(5).EvaluateAsync<object>("element => element.setAttribute('data-test-screenshot', 'hide')").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("hide-should-work.png", await page.ScreenshotAsync(new() { Style = @"[data-test-screenshot=""hide""] {
          visibility: hidden;
        }" }).ConfigureAwait(false));
            string visibility = await page.Locator("div").Nth(5).EvaluateAsync<string>("element => element.style.visibility").ConfigureAwait(false);
            Assert.That(visibility, Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should remove elements based on attr")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveElementsBasedOnAttr()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.Locator("div").Nth(5).EvaluateAsync<object>("element => element.setAttribute('data-test-screenshot', 'remove')").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("remove-should-work.png", await page.ScreenshotAsync(new() { Style = @"[data-test-screenshot=""remove""] {
          display: none;
        }" }).ConfigureAwait(false));
            string display = await page.Locator("div").Nth(5).EvaluateAsync<string>("element => element.style.display").ConfigureAwait(false);
            Assert.That(display, Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should not capture infinite css animation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCaptureInfiniteCssAnimation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rotate-z.html").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            byte[] screenshot = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))").ConfigureAwait(false);
                byte[] next = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
                Assert.That(next, Is.EqualTo(screenshot));
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should not capture blinking caret by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCaptureBlinkingCaretByDefault()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
      <link rel=stylesheet href=""" + TestConstants.CrossProcessHttpPrefix + @"/injectedstyle.css"">
      <style>
        div {
          caret-color: #000 !important;
        }
      </style>
      <div contenteditable=""true""></div>
    ").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            await div.TypeAsync("foo bar").ConfigureAwait(false);
            byte[] screenshot = await div.ScreenshotAsync().ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(150).ConfigureAwait(false);
                byte[] next = await div.ScreenshotAsync().ConfigureAwait(false);
                Assert.That(next, Is.EqualTo(screenshot));
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should capture blinking caret if explicitly asked for")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureBlinkingCaretIfExplicitlyAskedFor()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("browser-level screenshot API in firefox does not capture caret");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
      <link rel=stylesheet href=""" + TestConstants.CrossProcessHttpPrefix + @"/injectedstyle.css"">
      <style>
        div {
          caret-color: #000 !important;
        }
      </style>
      <div contenteditable=""true""></div>
    ").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            await div.TypeAsync("foo bar").ConfigureAwait(false);
            byte[] screenshot = await div.ScreenshotAsync().ConfigureAwait(false);
            bool hasDifferentScreenshots = false;
            for (int i = 0; !hasDifferentScreenshots && i < 10; i++)
            {
                await Task.Delay(150).ConfigureAwait(false);
                byte[] next = await div.ScreenshotAsync(new() { Caret = ScreenshotCaret.Initial }).ConfigureAwait(false);
                hasDifferentScreenshots = next.Length != screenshot.Length;
                if (!hasDifferentScreenshots)
                {
                    for (int b = 0; b < screenshot.Length; b++)
                    {
                        if (screenshot[b] != next[b])
                        {
                            hasDifferentScreenshots = true;
                            break;
                        }
                    }
                }
            }

            Assert.That(hasDifferentScreenshots, Is.True);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should capture blinking caret in shadow dom")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureBlinkingCaretInShadowDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.AddScriptTagAsync(new() { Content = @"
      class CustomElementContainer extends HTMLElement {
        #shadowRoot;
        constructor() {
          super();
          this.#shadowRoot = this.attachShadow({ mode: 'open' });
          this.#shadowRoot.innerHTML = '<custom-element-input-wrapper><input type=""text""/></custom-element-input-wrapper>';
        }
      }
      class CustomElementInputWrapper extends HTMLElement {
        #shadowRoot;
        constructor() {
          super();
          this.#shadowRoot = this.attachShadow({ mode: 'open' });
          this.#shadowRoot.innerHTML = '<style>:host { all: initial; }</style><slot/>';
        }
      }
      customElements.define('custom-element-input-wrapper', CustomElementInputWrapper);
      customElements.define('custom-element-container', CustomElementContainer);

      const container = document.createElement('custom-element-container');
      document.body.appendChild(container);" }).ConfigureAwait(false);

            ILocator input = page.Locator("input");
            await input.FocusAsync().ConfigureAwait(false);

            byte[] screenshot = await input.ScreenshotAsync().ConfigureAwait(false);
            bool hasDifferentScreenshots = false;
            for (int i = 0; !hasDifferentScreenshots && i < 10; i++)
            {
                await Task.Delay(150).ConfigureAwait(false);
                byte[] next = await input.ScreenshotAsync(new() { Caret = ScreenshotCaret.Hide }).ConfigureAwait(false);
                hasDifferentScreenshots = next.Length != screenshot.Length;
                if (!hasDifferentScreenshots)
                {
                    for (int b = 0; b < screenshot.Length; b++)
                    {
                        if (screenshot[b] != next[b])
                        {
                            hasDifferentScreenshots = true;
                            break;
                        }
                    }
                }
            }

            Assert.That(hasDifferentScreenshots, Is.False);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should take fullPage screenshots and mask elements outside of it")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeFullPageScreenshotsAndMaskElementsOutsideOfIt()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot(
                "screenshot-grid-fullpage-mask-outside-viewport.png",
                await page.ScreenshotAsync(new() { FullPage = true, Mask = new[] { page.Locator(".box").Nth(144) } }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MaskOptionShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot(
                "mask-should-work.png",
                await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("div").Nth(5) } }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work with locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            ILocator bodyLocator = page.Locator("body");
            OfficialSnapshot.ToMatchSnapshot(
                "mask-should-work-with-locator.png",
                await bodyLocator.ScreenshotAsync(new() { Mask = new[] { page.Locator("div").Nth(5) } }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work with elementhandle")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithElementhandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            IElementHandle bodyHandle = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot(
                "mask-should-work-with-elementhandle.png",
                await bodyHandle.ScreenshotAsync(new() { Mask = new[] { page.Locator("div").Nth(5) } }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should mask multiple elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMaskMultipleElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot(
                "should-mask-multiple-elements.png",
                await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("div").Nth(5), page.Locator("div").Nth(12) } }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should mask inside iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMaskInsideIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", Prefix + "/grid.html").ConfigureAwait(false);
            await page.AddStyleTagAsync(new() { Content = "iframe { border: none; }" }).ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot(
                "should-mask-inside-iframe.png",
                await page.ScreenshotAsync(new()
                {
                    Mask = new[]
                {
                    page.Locator("div").Nth(5),
                    page.FrameLocator("#frame1").Locator("div").Nth(12),
                }
                }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should mask in parallel")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMaskInParallel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", Prefix + "/grid.html").ConfigureAwait(false);
            await AttachFrameAsync(page, "frame2", Prefix + "/grid.html").ConfigureAwait(false);
            await page.AddStyleTagAsync(new() { Content = "iframe { border: none; }" }).ConfigureAwait(false);
            byte[][] screenshots = await Task.WhenAll(
                page.ScreenshotAsync(new() { Mask = new[] { page.FrameLocator("#frame1").Locator("div").Nth(1) } }),
                page.ScreenshotAsync(new() { Mask = new[] { page.FrameLocator("#frame2").Locator("div").Nth(3) } })).ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("should-mask-in-parallel-1.png", screenshots[0]);
            OfficialSnapshot.ToMatchSnapshot("should-mask-in-parallel-2.png", screenshots[1]);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should remove mask after screenshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveMaskAfterScreenshot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            byte[] screenshot1 = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("div").Nth(1) } }).ConfigureAwait(false);
            byte[] screenshot2 = await page.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(screenshot2, Is.EqualTo(screenshot1));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work when subframe has stalled navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenSubframeHasStalledNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IRoute> routeReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync("**/subframe.html", route => routeReady.TrySetResult(route)).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Task done = page.SetContentAsync("<iframe src='/subframe.html'></iframe>");
            IRoute route = await routeReady.Task.ConfigureAwait(false);
            await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("non-existent") } }).ConfigureAwait(false);
            await route.FulfillAsync(new() { Body = string.Empty }).ConfigureAwait(false);
            await done.ConfigureAwait(false);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work when subframe used document.open after a weird url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenSubframeUsedDocumentOpenAfterAWeirdUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
        const iframe = document.createElement('iframe');
        iframe.src = 'javascript:hi';
        document.body.appendChild(iframe);
        iframe.contentDocument.open();
        iframe.contentDocument.write('Hello');
        iframe.contentDocument.close();
      }").ConfigureAwait(false);
            await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("non-existent") } }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work when mask color is not pink #F0F")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenMaskColorIsNotPink()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot(
                "mask-color-should-work.png",
                await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("div").Nth(5) }, MaskColor = "#00FF00" }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should not capture pseudo element css animation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCapturePseudoElementCssAnimation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rotate-pseudo.html").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            byte[] screenshot = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                await RafRafAsync(page).ConfigureAwait(false);
                byte[] next = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
                Assert.That(next, Is.EqualTo(screenshot));
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should not capture css animations in shadow DOM")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCaptureCssAnimationsInShadowDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rotate-z-shadow-dom.html").ConfigureAwait(false);
            byte[] screenshot = await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            for (int i = 0; i < 4; i++)
            {
                await RafRafAsync(page).ConfigureAwait(false);
                byte[] next = await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
                Assert.That(next, Is.EqualTo(screenshot));
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should resume infinite animations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResumeInfiniteAnimations()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rotate-z.html").ConfigureAwait(false);
            await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            byte[] buffer1 = await page.ScreenshotAsync().ConfigureAwait(false);
            await RafRafAsync(page).ConfigureAwait(false);
            byte[] buffer2 = await page.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(buffer2, Is.Not.EqualTo(buffer1));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should not capture infinite web animations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCaptureInfiniteWebAnimations()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/web-animation.html").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            byte[] screenshot = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                await RafRafAsync(page).ConfigureAwait(false);
                byte[] next = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
                Assert.That(next, Is.EqualTo(screenshot));
            }

            byte[] buffer1 = await page.ScreenshotAsync().ConfigureAwait(false);
            await RafRafAsync(page).ConfigureAwait(false);
            byte[] buffer2 = await page.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(buffer2, Is.Not.EqualTo(buffer1));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should fire transitionend for finite transitions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireTransitionendForFiniteTransitions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/css-transition.html").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            await div.EvaluateAsync<object>("el => { el.addEventListener('transitionend', () => window['__TRANSITION_END'] = true, false); }").ConfigureAwait(false);
            byte[] running1 = await page.ScreenshotAsync().ConfigureAwait(false);
            await RafRafAsync(page).ConfigureAwait(false);
            byte[] running2 = await page.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(running2, Is.Not.EqualTo(running1));
            byte[] screenshot1 = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            await RafRafAsync(page).ConfigureAwait(false);
            byte[] screenshot2 = await div.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Allow }).ConfigureAwait(false);
            Assert.That(screenshot2, Is.EqualTo(screenshot1));
            Assert.That(await page.EvaluateAsync<bool>("() => window['__TRANSITION_END']").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should not change animation with playbackRate equal to 0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotChangeAnimationWithPlaybackRateEqualTo0()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rotate-z.html").ConfigureAwait(false);
            await page.EvaluateAsync(@"async () => {
      window.animation = document.getAnimations()[0];
      await window.animation.ready;
      window.animation.updatePlaybackRate(0);
      await window.animation.ready;
      window.animation.currentTime = 500;
    }").ConfigureAwait(false);
            byte[] screenshot1 = await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            await RafRafAsync(page).ConfigureAwait(false);
            byte[] screenshot2 = await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            Assert.That(screenshot2, Is.EqualTo(screenshot1));
            JsonElement state = await page.EvaluateAsync<JsonElement>(@"() => ({
      playbackRate: window.animation.playbackRate,
      currentTime: window.animation.currentTime,
    })").ConfigureAwait(false);
            Assert.That(state.GetProperty("playbackRate").GetDouble(), Is.EqualTo(0));
            Assert.That(state.GetProperty("currentTime").GetDouble(), Is.EqualTo(500));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should trigger particular events for css transitions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerParticularEventsForCssTransitions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/css-transition.html").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            await div.EvaluateAsync<object>(@"el => (async () => {
      window._EVENTS = [];
      el.addEventListener('transitionend', () => {
        window._EVENTS.push('transitionend');
        console.log('transitionend');
      }, false);
      const animation = el.getAnimations()[0];
      animation.oncancel = () => window._EVENTS.push('oncancel');
      animation.onfinish = () => window._EVENTS.push('onfinish');
      animation.onremove = () => window._EVENTS.push('onremove');
      await animation.ready;
    })()").ConfigureAwait(false);
            await Task.WhenAll(
                page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }),
                page.WaitForConsoleMessageAsync(msg => msg.Text == "transitionend")).ConfigureAwait(false);
            string[] events = await page.EvaluateAsync<string[]>("() => window._EVENTS").ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "onfinish", "transitionend" }));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should trigger particular events for INfinite css animation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerParticularEventsForInfiniteCssAnimation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rotate-z.html").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            await div.EvaluateAsync<object>(@"el => (async () => {
      window._EVENTS = [];
      el.addEventListener('animationcancel', () => {
        window._EVENTS.push('animationcancel');
        console.log('animationcancel');
      }, false);
      const animation = el.getAnimations()[0];
      animation.oncancel = () => window._EVENTS.push('oncancel');
      animation.onfinish = () => window._EVENTS.push('onfinish');
      animation.onremove = () => window._EVENTS.push('onremove');
      await animation.ready;
    })()").ConfigureAwait(false);
            await Task.WhenAll(
                page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }),
                page.WaitForConsoleMessageAsync(msg => msg.Text == "animationcancel")).ConfigureAwait(false);
            string[] events = await page.EvaluateAsync<string[]>("() => window._EVENTS").ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "oncancel", "animationcancel" }));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should trigger particular events for finite css animation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerParticularEventsForFiniteCssAnimation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rotate-z.html").ConfigureAwait(false);
            ILocator div = page.Locator("div");
            await div.EvaluateAsync<object>(@"el => (async () => {
      window._EVENTS = [];
      el.style.setProperty('animation-iteration-count', '1000');
      el.addEventListener('animationend', () => {
        window._EVENTS.push('animationend');
        console.log('animationend');
      }, false);
      const animation = el.getAnimations()[0];
      animation.oncancel = () => window._EVENTS.push('oncancel');
      animation.onfinish = () => window._EVENTS.push('onfinish');
      animation.onremove = () => window._EVENTS.push('onremove');
      await animation.ready;
    })()").ConfigureAwait(false);
            Assert.That(await div.EvaluateAsync<bool>("async el => Number.isFinite(el.getAnimations()[0].effect.getComputedTiming().endTime)").ConfigureAwait(false), Is.True);
            await Task.WhenAll(
                page.WaitForConsoleMessageAsync(msg => msg.Text == "animationend"),
                page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled })).ConfigureAwait(false);
            string[] events = await page.EvaluateAsync<string[]>("() => window._EVENTS").ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "onfinish", "animationend" }));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work with Array deleted")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithArrayDeleted()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => delete window.Array").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-grid-fullpage.png", await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "path option should detect jpeg")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PathOptionShouldDetectJpeg()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string outputPath = Path.Combine(Path.GetTempPath(), "pw-jpeg-" + Path.GetRandomFileName() + ".jpg");
            try
            {
                byte[] screenshot = await page.ScreenshotAsync(new() { OmitBackground = true, Path = outputPath }).ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("white.jpg", outputPath);
                OfficialSnapshot.ToMatchSnapshot("white.jpg", screenshot);
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should produce a valid webp screenshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProduceAValidWebpScreenshot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync("() => { document.body.style.background = 'rgb(255, 0, 0)'; }").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("red.webp", await page.ScreenshotAsync(new() { Type = ScreenshotType.Webp }).ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "path option should detect webp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PathOptionShouldDetectWebp()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync("() => { document.body.style.background = 'rgb(255, 0, 0)'; }").ConfigureAwait(false);
            string outputPath = Path.Combine(Path.GetTempPath(), "pw-webp-" + Path.GetRandomFileName() + ".webp");
            try
            {
                byte[] screenshot = await page.ScreenshotAsync(new() { Path = outputPath }).ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("red.webp", outputPath);
                OfficialSnapshot.ToMatchSnapshot("red.webp", screenshot);
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "quality option should work for webp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QualityOptionShouldWorkForWebp()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            byte[] lowQuality = await page.ScreenshotAsync(new() { Type = ScreenshotType.Webp, Quality = 0 }).ConfigureAwait(false);
            byte[] highQuality = await page.ScreenshotAsync(new() { Type = ScreenshotType.Webp, Quality = 100 }).ConfigureAwait(false);
            Assert.That(lowQuality.Length, Is.LessThan(highQuality.Length));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "webp screenshots should be lossless by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WebpScreenshotsShouldBeLosslessByDefault()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            Assert.That(OfficialWebp.IsLossless(await page.ScreenshotAsync(new() { Type = ScreenshotType.Webp }).ConfigureAwait(false)), Is.True);
            Assert.That(OfficialWebp.IsLossless(await page.ScreenshotAsync(new() { Type = ScreenshotType.Webp, Quality = 80 }).ConfigureAwait(false)), Is.False);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should allow transparency with webp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowTransparencyWithWebp()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(300, 300).ConfigureAwait(false);
            await page.SetContentAsync(@"
      <style>
        body { margin: 0 }
        div { width: 300px; height: 100px; }
      </style>
      <div style=""background:black""></div>
      <div style=""background:white""></div>
      <div style=""background:transparent""></div>
    ").ConfigureAwait(false);
            byte[] screenshot = await page.ScreenshotAsync(new() { OmitBackground = true, Type = ScreenshotType.Webp }).ConfigureAwait(false);
            (int width, int height, byte[] data) = OfficialWebp.Decode(screenshot);
            Assert.That(width, Is.EqualTo(300));
            Assert.That(height, Is.EqualTo(300));
            Assert.That(OfficialWebp.Pixel(data, width, 150, 50), Is.EqualTo(new[] { 0, 0, 0, 255 }));
            Assert.That(OfficialWebp.Pixel(data, width, 150, 150), Is.EqualTo(new[] { 255, 255, 255, 255 }));
            Assert.That(OfficialWebp.Pixel(data, width, 150, 250)[3], Is.EqualTo(0));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "quality option should throw for webp when out of range")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QualityOptionShouldThrowForWebpWhenOutOfRange()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.ScreenshotAsync(new() { Type = ScreenshotType.Webp, Quality = 101 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Expected options.quality to be between 0 and 100"));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should capture canvas changes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureCanvasChanges()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<canvas></canvas>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
      const canvas = document.querySelector('canvas');
      canvas.width = 600;
      canvas.height = 600;
    }").ConfigureAwait(false);
            for (int i = 0; i < 3; i++)
            {
                await page.EvaluateAsync<object>(@"n => {
        const canvas = document.querySelector('canvas');
        const ctx = canvas.getContext('2d');
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(0, n * 100);
        ctx.lineTo(300, n * 100);
        ctx.stroke();
      }", i).ConfigureAwait(false);
                await Task.Delay(100).ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("canvas-changes-" + i + ".png", await page.ScreenshotAsync().ConfigureAwait(false));
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work for webgl")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForWebgl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(640, 480).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/screenshots/webgl.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-webgl.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should work while navigating")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhileNavigating()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/redirectloop1.html").ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    byte[] screenshot = await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
                    Assert.That(screenshot, Is.Not.Null);
                }
                catch (Exception ex) when (ex.Message.Contains("Cannot take a screenshot while page is navigating", StringComparison.Ordinal))
                {
                    Assert.That(Array.Empty<byte>(), Is.InstanceOf<byte[]>());
                }
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should take fullPage screenshots during navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeFullPageScreenshotsDuringNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            async Task ReloadSeveralTimesAsync()
            {
                for (int i = 0; i < 5; i++)
                {
                    await page.ReloadAsync().ConfigureAwait(false);
                }
            }

            async Task ScreenshotSeveralTimesAsync()
            {
                for (int i = 0; i < 5; i++)
                {
                    await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
                }
            }

            await Task.WhenAll(ReloadSeveralTimesAsync(), ScreenshotSeveralTimesAsync()).ConfigureAwait(false);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should wait for fonts to load")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForFontsToLoad()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            TaskCompletionSource<bool> fontArrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseFont = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] fontBytes = await File.ReadAllBytesAsync(
                Path.Combine(TestUtils.FindParentDirectory("PlaywrightSharp.TestServer"), "wwwroot", "webfont", "iconfont.woff2")).ConfigureAwait(false);
            Server.SetRoute("/webfont/iconfont.woff2", async ctx =>
            {
                fontArrived.TrySetResult(true);
                await releaseFont.Task.ConfigureAwait(false);
                ctx.Response.ContentType = "font/woff2";
                await ctx.Response.Body.WriteAsync(fontBytes).ConfigureAwait(false);
            });
            try
            {
                await page.GoToAsync(Prefix + "/webfont/webfont.html", WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
                Exception error = Assert.CatchAsync(() => page.ScreenshotAsync(new() { Timeout = 200 }));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("waiting for fonts to load..."));
                Assert.That(error.Message, Does.Contain("Timeout 200ms exceeded"));

                await fontArrived.Task.ConfigureAwait(false);
                releaseFont.TrySetResult(true);
                OfficialSnapshot.ToMatchSnapshot("screenshot-web-font.png", await page.ScreenshotAsync().ConfigureAwait(false));
            }
            finally
            {
                releaseFont.TrySetResult(true);
                Server.SetRoute("/webfont/iconfont.woff2", async ctx =>
                {
                    ctx.Response.ContentType = "font/woff2";
                    await ctx.Response.Body.WriteAsync(fontBytes).ConfigureAwait(false);
                });
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should throw if screenshot size is too large")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldThrowIfScreenshotSizeIsTooLarge()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            const int maxSize = 32767;
            await page.SetContentAsync("<style>body {margin: 0; padding: 0;}</style><div style='min-height: " + maxSize + "px; background: red;'></div>").ConfigureAwait(false);
            byte[] result = await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.GreaterThan(0));

            await page.SetContentAsync("<style>body {margin: 0; padding: 0;}</style><div style='min-height: " + (maxSize + 1) + "px; background: red;'></div>").ConfigureAwait(false);
            Exception exception = null;
            try
            {
                await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (TestConstants.IsWebKit)
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception.Message, Does.Contain("Cannot take screenshot larger than 32767"));
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "page screenshot should capture css transform")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageScreenshotShouldCaptureCssTransform()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("official fixme(browserName === 'webkit')");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
    .container {
      width: 150px;
      height: 150px;
      margin: 75px 0 0 75px;
      border: none;
    }

    .cube {
      width: 100%;
      height: 100%;
      perspective: 550px;
      perspective-origin: 150% 150%;
    }

    .face {
      display: block;
      position: absolute;
      width: 100px;
      height: 100px;
      border: none;
    }

    .right {
      background: rgba(196, 0, 0, 0.7);
      transform: rotateY(70deg);
    }

    </style>
    <div class=""container"">
      <div class=""cube showbf"">
        <div class=""face right""></div>
      </div>
    </div>
  ").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("page-screenshot-should-capture-css-transform.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "should capture css box-shadow")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureCssBoxShadow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"box-shadow: red 10px 10px 10px; width: 50px; height: 50px;\"></div>").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("should-capture-css-box-shadow.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        private static Task RafRafAsync(IPage page)
            => page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");

        private static async Task AttachFrameAsync(IPage page, string frameId, string url)
        {
            string frameIdJson = System.Text.Json.JsonSerializer.Serialize(frameId);
            string urlJson = System.Text.Json.JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(async () => { const frame = document.createElement('iframe'); frame.src = " +
                urlJson + "; frame.id = " + frameIdJson + "; document.body.appendChild(frame); await new Promise(x => frame.onload = x); })()")
                .ConfigureAwait(false);
        }
    }
}
