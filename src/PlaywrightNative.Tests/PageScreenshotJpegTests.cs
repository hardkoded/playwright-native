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
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for JPEG <see cref="IPage.ScreenshotAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageScreenshotJpegTests : PageTestEx
    {
        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync returns a JPEG")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnJpegBytes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(120, 80).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:100px;height:60px;background:#c00\"></div>").ConfigureAwait(false);

            byte[] bytes = await page.ScreenshotAsync(new() { Type = ScreenshotType.Jpeg, Quality = 80 }).ConfigureAwait(false);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(20));
            Assert.That(bytes[0], Is.EqualTo(0xFF));
            Assert.That(bytes[1], Is.EqualTo(0xD8));
            Assert.That(bytes[2], Is.EqualTo(0xFF));

            using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
            Assert.That(image.Width, Is.GreaterThan(0));
            Assert.That(image.Height, Is.GreaterThan(0));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync JPEG writes path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWriteJpegPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:40px;height:40px;background:blue\"></div>").ConfigureAwait(false);

            string file = Path.Combine(Path.GetTempPath(), "pw-jpeg-" + Path.GetRandomFileName() + ".jpg");
            try
            {
                byte[] bytes = await page.ScreenshotAsync(new() { Path = file, Type = ScreenshotType.Jpeg, Quality = 60 }).ConfigureAwait(false);
                Assert.That(File.Exists(file), Is.True);
                byte[] onDisk = File.ReadAllBytes(file);
                Assert.That(onDisk, Is.EqualTo(bytes));
                Assert.That(onDisk[0], Is.EqualTo(0xFF));
                Assert.That(onDisk[1], Is.EqualTo(0xD8));
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync honors css scale")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldHonorCssScale()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                DeviceScaleFactor = 2,
                Viewport = new ViewportSize { Width = 100, Height = 80 },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:100px;height:80px;background:#0a0\"></div>").ConfigureAwait(false);

            byte[] device = await page.ScreenshotAsync(new() { Scale = ScreenshotScale.Device }).ConfigureAwait(false);
            byte[] css = await page.ScreenshotAsync(new() { Scale = ScreenshotScale.Css }).ConfigureAwait(false);

            using Image<Rgba32> deviceImage = Image.Load<Rgba32>(device);
            using Image<Rgba32> cssImage = Image.Load<Rgba32>(css);
            Assert.That(cssImage.Width, Is.EqualTo(100));
            Assert.That(cssImage.Height, Is.EqualTo(80));
            Assert.That(deviceImage.Width, Is.GreaterThan(cssImage.Width));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync disables animations")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldDisableAnimations()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(120, 80).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }" +
                "div { width: 60px; height: 60px; background: #c00; animation: spin 1s linear infinite; }</style>" +
                "<div></div>").ConfigureAwait(false);

            byte[] first = await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            await page.WaitForTimeoutAsync(120).ConfigureAwait(false);
            byte[] second = await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);

            Assert.That(first, Is.EqualTo(second));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync hides caret")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldHideCaret()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(240, 80).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>input,input:focus{outline:none;box-shadow:none;border:1px solid #000;caret-color:red;font-size:40px;width:220px;height:50px}</style>" +
                "<input id=\"i\" value=\"hello\" />").ConfigureAwait(false);

            byte[] unfocused = await page.ScreenshotAsync().ConfigureAwait(false);
            await page.FocusAsync("#i").ConfigureAwait(false);
            byte[] hidden = await page.ScreenshotAsync(new() { Caret = ScreenshotCaret.Hide }).ConfigureAwait(false);

            Assert.That(hidden, Is.EqualTo(unfocused));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync applies style")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldApplyStyle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(100, 80).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:100px;height:80px;background:#c00\"></div>").ConfigureAwait(false);

            byte[] styled = await page.ScreenshotAsync(new() { Style = "div { background: #00c !important; }" }).ConfigureAwait(false);
            byte[] baseline = await page.ScreenshotAsync().ConfigureAwait(false);

            Assert.That(styled, Is.Not.EqualTo(baseline));
            using Image<Rgba32> image = Image.Load<Rgba32>(styled);
            Rgba32 pixel = image[10, 10];
            Assert.That(pixel.B, Is.GreaterThan(pixel.R));
        }
    }
}
