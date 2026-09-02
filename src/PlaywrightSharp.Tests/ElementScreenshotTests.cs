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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IElementHandle.ScreenshotAsync"/>.
    /// </summary>
    [TestFixture]
    public class ElementScreenshotTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync clips to the element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClipToTheElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(400, 300).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:80px;height:50px;background:#0a0\"></div>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            byte[] bytes = await target.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(20));
            Assert.That(bytes[0], Is.EqualTo(0x89));
            Assert.That(bytes[1], Is.EqualTo(0x50));
            Assert.That(bytes[2], Is.EqualTo(0x4E));
            Assert.That(bytes[3], Is.EqualTo(0x47));

            using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
            Assert.That(image.Width, Is.LessThan(400));
            Assert.That(image.Height, Is.LessThan(300));
            Assert.That(image.Width, Is.GreaterThan(0));
            Assert.That(image.Height, Is.GreaterThan(0));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync writes a PNG path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWritePngPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:40px;height:40px;background:blue\"></div>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            string file = Path.Combine(Path.GetTempPath(), "pw-el-" + Path.GetRandomFileName() + ".png");
            try
            {
                byte[] bytes = await target.ScreenshotAsync(new() { Path = file }).ConfigureAwait(false);
                Assert.That(File.Exists(file), Is.True);
                byte[] onDisk = File.ReadAllBytes(file);
                Assert.That(onDisk, Is.EqualTo(bytes));
                Assert.That(onDisk[0], Is.EqualTo(0x89));
                Assert.That(onDisk[1], Is.EqualTo(0x50));
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"h\" style=\"display:none\">hidden</div>").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#h").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.ScreenshotAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"h\" style=\"display:none;width:40px;height:40px;background:blue\">x</div>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#h").ConfigureAwait(false);
            Task<byte[]> shotTask = target.ScreenshotAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#h').style.display = 'block'").ConfigureAwait(false);
            byte[] bytes = await shotTask.ConfigureAwait(false);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(20));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync honors css scale")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldHonorCssScale()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                DeviceScaleFactor = 2,
                Viewport = new ViewportSize { Width = 200, Height = 160 },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"width:80px;height:50px;background:#0a0\"></div>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            byte[] device = await target.ScreenshotAsync(new() { Scale = ScreenshotScale.Device }).ConfigureAwait(false);
            byte[] css = await target.ScreenshotAsync(new() { Scale = ScreenshotScale.Css }).ConfigureAwait(false);

            using Image<Rgba32> deviceImage = Image.Load<Rgba32>(device);
            using Image<Rgba32> cssImage = Image.Load<Rgba32>(css);
            Assert.That(cssImage.Width, Is.EqualTo(80));
            Assert.That(cssImage.Height, Is.EqualTo(50));
            Assert.That(deviceImage.Width, Is.GreaterThan(cssImage.Width));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync disables animations")]
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
                "#t { width: 60px; height: 60px; background: #c00; animation: spin 1s linear infinite; }</style>" +
                "<div id=\"t\"></div>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            byte[] first = await target.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);
            await page.WaitForTimeoutAsync(120).ConfigureAwait(false);
            byte[] second = await target.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled }).ConfigureAwait(false);

            Assert.That(first, Is.EqualTo(second));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync hides caret")]
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

            IElementHandle target = await page.QuerySelectorAsync("#i").ConfigureAwait(false);
            byte[] unfocused = await target.ScreenshotAsync().ConfigureAwait(false);
            await page.FocusAsync("#i").ConfigureAwait(false);
            byte[] hidden = await target.ScreenshotAsync(new() { Caret = ScreenshotCaret.Hide }).ConfigureAwait(false);

            Assert.That(hidden, Is.EqualTo(unfocused));
        }

        [PlaywrightTest("elementhandle-screenshot.spec.ts", "ScreenshotAsync applies style")]
        [Test]
        [Timeout(30_000)]
        public async Task ScreenshotAsyncShouldApplyStyle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"width:80px;height:50px;background:#c00\"></div>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            byte[] styled = await target.ScreenshotAsync(new() { Style = "#t { background: #00c !important; }" }).ConfigureAwait(false);
            byte[] baseline = await target.ScreenshotAsync().ConfigureAwait(false);

            Assert.That(styled, Is.Not.EqualTo(baseline));
            using Image<Rgba32> image = Image.Load<Rgba32>(styled);
            Rgba32 pixel = image[10, 10];
            Assert.That(pixel.B, Is.GreaterThan(pixel.R));
        }
    }
}
