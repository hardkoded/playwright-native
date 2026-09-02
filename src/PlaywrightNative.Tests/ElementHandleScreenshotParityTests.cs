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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-screenshot.spec.ts</c> titles.
    /// Do not edit leftover <c>ElementScreenshotTests</c>.
    /// </summary>
    [TestFixture]
    public class ElementHandleScreenshotParityTests : PageTestEx
    {
        private static string Prefix => TestConstants.ServerUrl;

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollBy(50, 100)").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-bounding-box.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should take into account padding and border")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeIntoAccountPaddingAndBorder()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.SetContentAsync(@"
      <div style=""height: 14px"">oooo</div>
      <style>div {
        border: 2px solid blue;
        background: green;
        width: 50px;
        height: 50px;
      }
      </style>
      <div id=""d""></div>
    ").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div#d").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-padding-border.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should capture full element when larger than viewport")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureFullElementWhenLargerThanViewport()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
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
            IElementHandle elementHandle = await page.QuerySelectorAsync("div.to-screenshot").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-larger-than-viewport.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
            Assert.That(page.ViewportSize.Width, Is.EqualTo(500));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(500));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should capture full element when larger than viewport in parallel")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureFullElementWhenLargerThanViewportInParallel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
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
            var handles = await page.QuerySelectorAllAsync("div.to-screenshot").ConfigureAwait(false);
            byte[][] screenshots = await Task.WhenAll(handles.Select(h => h.ScreenshotAsync())).ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-larger-than-viewport.png", screenshots[2]);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(500));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(500));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should scroll element into view")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollElementIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.SetContentAsync(@"
      <div style=""height: 14px"">oooo</div>
      <style>div.above {
        border: 2px solid blue;
        background: red;
        height: 1500px;
      }
      div.to-screenshot {
        border: 2px solid blue;
        background: green;
        width: 50px;
        height: 50px;
      }
      </style>
      <div class=""above""></div>
      <div class=""to-screenshot""></div>
    ").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div.to-screenshot").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-scrolled-into-view.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should scroll 15000px into view")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScroll15000PxIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.SetContentAsync(@"
      <div style=""height: 14px"">oooo</div>
      <style>div.above {
        border: 2px solid blue;
        background: red;
        height: 15000px;
      }
      div.to-screenshot {
        border: 2px solid blue;
        background: green;
        width: 50px;
        height: 50px;
      }
      </style>
      <div class=""above""></div>
      <div class=""to-screenshot""></div>
    ").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div.to-screenshot").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-scrolled-into-view.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should work with a rotated element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithARotatedElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.SetContentAsync(@"<div style=""position:absolute;
                                      top: 100px;
                                      left: 100px;
                                      width: 100px;
                                      height: 100px;
                                      background: green;
                                      transform: rotateZ(200deg);"">&nbsp;</div>").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-rotate.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should fail to screenshot a detached element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailToScreenshotADetachedElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>remove this</h1>").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("h1").ConfigureAwait(false);
            await page.EvaluateAsync("element => element.remove()", elementHandle).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => elementHandle.ScreenshotAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should work for an element with fractional dimensions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForAnElementWithFractionalDimensions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:48.51px;height:19.8px;border:1px solid black;\"></div>").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-fractional.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should work for an element with an offset")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForAnElementWithAnOffset()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"position:absolute; top: 10.3px; left: 20.4px;width:50.3px;height:20.2px;border:1px solid black;\"></div>").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-fractional-offset.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should take screenshot of disabled button")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeScreenshotOfDisabledButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.SetContentAsync("<button disabled>Click me</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            byte[] screenshot = await button.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(screenshot, Is.Not.Null);
            Assert.That(screenshot.Length, Is.GreaterThan(0));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "path option should create subdirectories")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PathOptionShouldCreateSubdirectories()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollBy(50, 100)").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            string outputPath = Path.Combine(Path.GetTempPath(), "pw-el-" + Path.GetRandomFileName(), "these", "are", "directories", "screenshot.png");
            try
            {
                await elementHandle.ScreenshotAsync(new() { Path = outputPath }).ConfigureAwait(false);
                OfficialSnapshot.ToMatchSnapshot("screenshot-element-bounding-box.png", outputPath);
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should work with webp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithWebp()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            (int webpWidth, int webpHeight, byte[] webpData) = OfficialWebp.Decode(
                await elementHandle.ScreenshotAsync(new() { Type = ScreenshotType.Webp }).ConfigureAwait(false));
            using SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> pngImage =
                SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(
                    await elementHandle.ScreenshotAsync(new() { Type = ScreenshotType.Png }).ConfigureAwait(false));
            Assert.That(webpWidth, Is.EqualTo(pngImage.Width));
            Assert.That(webpHeight, Is.EqualTo(pngImage.Height));
            byte[] pngData = new byte[pngImage.Width * pngImage.Height * 4];
            int i = 0;
            for (int y = 0; y < pngImage.Height; y++)
            {
                for (int x = 0; x < pngImage.Width; x++)
                {
                    SixLabors.ImageSharp.PixelFormats.Rgba32 pixel = pngImage[x, y];
                    pngData[i++] = pixel.R;
                    pngData[i++] = pixel.G;
                    pngData[i++] = pixel.B;
                    pngData[i++] = pixel.A;
                }
            }

            Assert.That(webpData, Is.EqualTo(pngData));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should work when main world busts JSON.stringify")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenMainWorldBustsJsonStringify()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => {
      window.scrollBy(50, 100);
      JSON.stringify = () => undefined;
    }").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-bounding-box.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should timeout waiting for visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutWaitingForVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width: 50px; height: 0\"></div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => div.ScreenshotAsync(new() { Timeout = 3000 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("elementHandle.screenshot: Timeout 3000ms exceeded"));
            Assert.That(error.Message, Does.Contain("element is not visible"));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should wait for visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollBy(50, 100)").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            await elementHandle.EvaluateAsync<object>("e => { e.style.visibility = 'hidden'; }").ConfigureAwait(false);
            bool done = false;
            Task<byte[]> promise = elementHandle.ScreenshotAsync().ContinueWith(
                t =>
                {
                    done = true;
                    return t.Result;
                },
                TaskScheduler.Default);
            await RafRafAsync(page, 10).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await elementHandle.EvaluateAsync<object>("e => { e.style.visibility = 'visible'; }").ConfigureAwait(false);
            byte[] screenshot = await promise.ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-bounding-box.png", screenshot);
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should wait for element to stop moving")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForElementToStopMoving()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            await elementHandle.EvaluateAsync<object>("e => e.classList.add('animation')").ConfigureAwait(false);
            await RafRafAsync(page, 1).ConfigureAwait(false);
            OfficialSnapshot.ToMatchSnapshot("screenshot-element-bounding-box.png", await elementHandle.ScreenshotAsync().ConfigureAwait(false));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should prefer type over extension")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreferTypeOverExtension()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollBy(50, 100)").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            string outputPath = Path.Combine(Path.GetTempPath(), "pw-el-type-" + Path.GetRandomFileName() + ".png");
            try
            {
                byte[] buffer = await elementHandle.ScreenshotAsync(new() { Path = outputPath, Type = ScreenshotType.Jpeg }).ConfigureAwait(false);
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

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "should not issue resize event")]
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
            IElementHandle elementHandle = await page.QuerySelectorAsync(".box:nth-of-type(3)").ConfigureAwait(false);
            await elementHandle.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(resizeTriggered, Is.False);
        }

        private static async Task RafRafAsync(IPage page, int count)
        {
            for (int i = 0; i < count; i++)
            {
                await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))").ConfigureAwait(false);
            }
        }
    }
}
