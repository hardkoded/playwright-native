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
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/screenshot.spec.ts</c> titles.
    /// Skipped (Node <c>__testHook*</c>):
    /// <c>should not hang when event loop is blocked</c>,
    /// <c>should restore viewport after page screenshot and exception</c>,
    /// <c>should restore viewport after page screenshot and timeout</c>,
    /// <c>should restore viewport after element screenshot and exception</c>.
    /// Do not edit leftover screenshot classes.
    /// </summary>
    [TestFixture]
    public class LibraryScreenshotParityTests : PageTestEx
    {
        private static string Prefix => TestConstants.ServerUrl;

        [PlaywrightTest("screenshot.spec.ts", "should run in parallel in multiple pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunInParallelInMultiplePages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage[] pages = new IPage[5];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = await context.NewPageAsync().ConfigureAwait(false);
                await pages[i].GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            }

            Task<byte[]>[] promises = new Task<byte[]>[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                promises[i] = pages[index].ScreenshotAsync(new()
                {
                    Clip = new Clip
                    {
                        X = 50 * (index % 2),
                        Y = 0,
                        Width = 50,
                        Height = 50,
                    }
                });
            }

            byte[][] screenshots = await Task.WhenAll(promises).ConfigureAwait(false);
            for (int i = 0; i < pages.Length; i++)
            {
                OfficialSnapshot.ToMatchSnapshot("grid-cell-" + (i % 2) + ".png", screenshots[i]);
            }
        }

        [PlaywrightTest("screenshot.spec.ts", "should work with a mobile viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAMobileViewport()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/overflow.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-mobile.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should work with a mobile viewport and clip")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAMobileViewportAndClip()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/overflow.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-mobile-clip.png", await page.ScreenshotAsync(new()
            {
                Clip = new Clip
                {
                    X = 10,
                    Y = 10,
                    Width = 100,
                    Height = 150,
                }
            }).ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should work with a mobile viewport and fullPage")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAMobileViewportAndFullPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/overflow-large.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-mobile-fullpage.png", await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should work with device scale factor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithDeviceScaleFactor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, DeviceScaleFactor = 2 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-device-scale-factor.png", await page.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should work with device scale factor and clip")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithDeviceScaleFactorAndClip()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 500 }, DeviceScaleFactor = 3 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-device-scale-factor-clip.png", await page.ScreenshotAsync(new()
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

        [PlaywrightTest("screenshot.spec.ts", "should work with device scale factor and scale:css")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithDeviceScaleFactorAndScaleCss()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, DeviceScaleFactor = 2 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-device-scale-factor-css-size.png", await page.ScreenshotAsync(new() { Scale = ScreenshotScale.Css }).ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should produce screenshot of correct size with scale:css and null viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProduceScreenshotOfCorrectSizeWithScaleCssAndNullViewport()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            int[] size = await page.EvaluateAsync<int[]>("() => [window.innerWidth, window.innerHeight]").ConfigureAwait(false);
            byte[] screenshot = await page.ScreenshotAsync(new() { Scale = ScreenshotScale.Css }).ConfigureAwait(false);
            using Image<Rgb24> decoded = Image.Load<Rgb24>(screenshot);
            Assert.That(decoded.Width, Is.EqualTo(size[0]));
            Assert.That(decoded.Height, Is.EqualTo(size[1]));
        }

        [PlaywrightTest("screenshot.spec.ts", "should work with device scale factor, clip and scale:css")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithDeviceScaleFactorClipAndScaleCss()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 500 }, DeviceScaleFactor = 3 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-device-scale-factor-clip-css-size.png", await page.ScreenshotAsync(new() { Clip = new Clip { X = 50, Y = 100, Width = 150, Height = 100 }, Scale = ScreenshotScale.Css }).ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should throw if screenshot size is too large with device scale factor")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldThrowIfScreenshotSizeIsTooLargeWithDeviceScaleFactor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 500 }, DeviceScaleFactor = 2 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<style>body {margin: 0; padding: 0;}</style><div style='min-height: 16383px; background: red;'></div>").ConfigureAwait(false);
            byte[] result = await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.GreaterThan(0));

            await page.SetContentAsync("<style>body {margin: 0; padding: 0;}</style><div style='min-height: 16384px; background: red;'></div>").ConfigureAwait(false);
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

            byte[] css = await page.ScreenshotAsync(new() { FullPage = true, Scale = ScreenshotScale.Css }).ConfigureAwait(false);
            Assert.That(css, Is.Not.Null);
            Assert.That(css.Length, Is.GreaterThan(0));
        }

        [PlaywrightTest("screenshot.spec.ts", "should work with large size")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldWorkWithLargeSize()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(1280, 800).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
      document.body.style.margin = '0';
      document.body.style.padding = '0';
      document.documentElement.style.margin = '0';
      document.documentElement.style.padding = '0';
      const div = document.createElement('div');
      div.style.width = '1250px';
      div.style.height = '8440px';
      div.style.background = 'linear-gradient(red, blue)';
      document.body.appendChild(div);
    }").ConfigureAwait(false);
            using Image<Rgba32> decoded = Image.Load<Rgba32>(await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false));
            Rgba32 top = decoded[0, 0];
            Rgba32 bottom = decoded[0, 8339];
            Assert.That(top.R, Is.GreaterThan(128));
            Assert.That(top.B, Is.LessThan(128));
            Assert.That(bottom.R, Is.LessThan(128));
            Assert.That(bottom.B, Is.GreaterThan(128));
        }

        [PlaywrightTest("screenshot.spec.ts", "should handle vh units ")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleVhUnits()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(800, 500).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
      document.body.style.margin = '0';
      document.body.style.padding = '0';
      document.documentElement.style.margin = '0';
      document.documentElement.style.padding = '0';
      const div = document.createElement('div');
      div.style.width = '100%';
      div.style.borderTop = '100vh solid red';
      div.style.borderBottom = '100vh solid blue';
      document.body.appendChild(div);
    }").ConfigureAwait(false);
            using Image<Rgba32> decoded = Image.Load<Rgba32>(await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false));
            Rgba32 top = decoded[0, 0];
            Rgba32 bottom = decoded[0, 999];
            Assert.That(top.R, Is.GreaterThan(128));
            Assert.That(top.B, Is.LessThan(128));
            Assert.That(bottom.R, Is.LessThan(128));
            Assert.That(bottom.B, Is.GreaterThan(128));
        }

        [PlaywrightTest("screenshot.spec.ts", "element screenshot should work with a mobile viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementScreenshotShouldWorkWithAMobileViewport()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, IsMobile = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollBy(50, 100)").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-mobile.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "element screenshot should work with device scale factor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementScreenshotShouldWorkWithDeviceScaleFactor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 320, Height = 480 }, DeviceScaleFactor = 2 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollBy(50, 100)").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-mobile-dsf.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should take screenshots when default viewport is null")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeScreenshotsWhenDefaultViewportIsNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style='height: 10000px; background: red'></div>").ConfigureAwait(false);
            int[] windowSize = await page.EvaluateAsync<int[]>("() => [window.innerWidth * window.devicePixelRatio, window.innerHeight * window.devicePixelRatio]").ConfigureAwait(false);
            int[] sizeBefore = await page.EvaluateAsync<int[]>("() => [document.body.offsetWidth, document.body.offsetHeight]").ConfigureAwait(false);
            byte[] screenshot = await page.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(screenshot, Is.Not.Null);
            using Image<Rgb24> decoded = Image.Load<Rgb24>(screenshot);
            Assert.That(decoded.Width, Is.EqualTo(windowSize[0]));
            Assert.That(decoded.Height, Is.EqualTo(windowSize[1]));
            int[] sizeAfter = await page.EvaluateAsync<int[]>("() => [document.body.offsetWidth, document.body.offsetHeight]").ConfigureAwait(false);
            Assert.That(sizeBefore[0], Is.EqualTo(sizeAfter[0]));
            Assert.That(sizeBefore[1], Is.EqualTo(sizeAfter[1]));
        }

        [PlaywrightTest("screenshot.spec.ts", "should take fullPage screenshots when default viewport is null")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeFullPageScreenshotsWhenDefaultViewportIsNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            int[] sizeBefore = await page.EvaluateAsync<int[]>("() => [document.body.offsetWidth, document.body.offsetHeight]").ConfigureAwait(false);
            byte[] screenshot = await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
            Assert.That(screenshot, Is.Not.Null);
            Assert.That(screenshot.Length, Is.GreaterThan(0));
            int[] sizeAfter = await page.EvaluateAsync<int[]>("() => [document.body.offsetWidth, document.body.offsetHeight]").ConfigureAwait(false);
            Assert.That(sizeBefore[0], Is.EqualTo(sizeAfter[0]));
            Assert.That(sizeBefore[1], Is.EqualTo(sizeAfter[1]));
        }

        [PlaywrightTest("screenshot.spec.ts", "should restore default viewport after fullPage screenshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRestoreDefaultViewportAfterFullPageScreenshot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 456, Height = 789 } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(456));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(789));
            byte[] screenshot = await page.ScreenshotAsync(new() { FullPage = true }).ConfigureAwait(false);
            Assert.That(screenshot, Is.Not.Null);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(456));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(789));
        }

        [PlaywrightTest("screenshot.spec.ts", "should take element screenshot when default viewport is null and restore back")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeElementScreenshotWhenDefaultViewportIsNullAndRestoreBack()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = ViewportSize.NoViewport }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
      <div style=""height: 14px"">oooo</div>
      <style>
      div.to-screenshot {
        border: 1px solid blue;
        width: 600px;
        height: 600px;
        margin-left: 50px;
      }
      ::-webkit-scrollbar{
        display: none;
      }
      </style>
      <div class=""to-screenshot""></div>
      <div class=""to-screenshot""></div>
      <div class=""to-screenshot""></div>
    ").ConfigureAwait(false);
            int[] sizeBefore = await page.EvaluateAsync<int[]>("() => [document.body.offsetWidth, document.body.offsetHeight]").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div.to-screenshot").ConfigureAwait(false);
            byte[] screenshot = await elementHandle.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(screenshot, Is.Not.Null);
            Assert.That(screenshot.Length, Is.GreaterThan(0));
            int[] sizeAfter = await page.EvaluateAsync<int[]>("() => [document.body.offsetWidth, document.body.offsetHeight]").ConfigureAwait(false);
            Assert.That(sizeBefore[0], Is.EqualTo(sizeAfter[0]));
            Assert.That(sizeBefore[1], Is.EqualTo(sizeAfter[1]));
        }

        [PlaywrightTest("screenshot.spec.ts", "element screenshots should handle vh units ")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementScreenshotsShouldHandleVhUnits()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(800, 500).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
      const div = document.createElement('div');
      div.style.width = '100%';
      div.style.borderTop = '100vh solid red';
      div.style.borderBottom = '100vh solid blue';
      document.body.appendChild(div);
    }").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            using Image<Rgba32> decoded = Image.Load<Rgba32>(await elementHandle.ScreenshotAsync().ConfigureAwait(false));
            Rgba32 top = decoded[0, 0];
            Rgba32 bottom = decoded[0, 999];
            Assert.That(top.R, Is.GreaterThan(128));
            Assert.That(top.B, Is.LessThan(128));
            Assert.That(bottom.R, Is.LessThan(128));
            Assert.That(bottom.B, Is.GreaterThan(128));
        }

        [PlaywrightTest("screenshot.spec.ts", "should work if the main resource hangs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkIfTheMainResourceHangs()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("https://github.com/microsoft/playwright/issues/9757");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            TestServerSetup.Server.SetRoute("/slow", ctx =>
            {
                ctx.Response.ContentType = "text/html";
                ctx.Response.ContentLength = 4096;
                return Task.CompletedTask;
            });
            try
            {
                try
                {
                    await page.GoToAsync(Prefix + "/slow", timeout: 1000).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                OfficialSnapshot.ToMatchSnapshot("hanging-main-resource.png", await page.ScreenshotAsync().ConfigureAwait(false));
            }
            finally
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("screenshot.spec.ts", "should capture full element when larger than viewport with device scale factor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureFullElementWhenLargerThanViewportWithDeviceScaleFactor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 501, Height = 501 }, DeviceScaleFactor = 2.5f }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
      <div style=""height: 14px"">oooo</div>
      <style>
      div.to-screenshot {
        border: 4px solid red;
        box-sizing: border-box;
        width: 600px;
        height: 600px;
        margin-left: 50px;
        background: rgb(0, 100, 200);
      }
      ::-webkit-scrollbar{
        display: none;
      }
      </style>
      <div class=""to-screenshot""></div>
    ").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("element-larger-than-viewport-dsf.png", await page.Locator("div.to-screenshot").ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "should capture full element when larger than viewport with device scale factor and scale:css")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureFullElementWhenLargerThanViewportWithDeviceScaleFactorAndScaleCss()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 501, Height = 501 }, DeviceScaleFactor = 2.5f }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
      <div style=""height: 14px"">oooo</div>
      <style>
      div.to-screenshot {
        border: 4px solid red;
        box-sizing: border-box;
        width: 600px;
        height: 600px;
        margin-left: 50px;
        background: rgb(0, 100, 200);
      }
      ::-webkit-scrollbar{
        display: none;
      }
      </style>
      <div class=""to-screenshot""></div>
    ").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("element-larger-than-viewport-dsf-css-size.png", await page.Locator("div.to-screenshot").ScreenshotAsync(new() { Scale = ScreenshotScale.Css }).ConfigureAwait(false));
        }

        [PlaywrightTest("screenshot.spec.ts", "page screenshot should capture css transform with device pixels")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageScreenshotShouldCaptureCssTransformWithDevicePixels()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("official fixme(browserName === 'webkit')");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { ViewportSize = new ViewportSize { Width = 500, Height = 500 }, DeviceScaleFactor = 3 }).ConfigureAwait(false);
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
            OfficialSnapshot.ToMatchSnapshot("page-screenshot-should-capture-css-transform-with-device-pixels.png", await page.ScreenshotAsync(new() { Scale = ScreenshotScale.Device }).ConfigureAwait(false));
        }
    }
}
