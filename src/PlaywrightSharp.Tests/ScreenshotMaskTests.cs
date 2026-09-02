/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Screenshot <c>mask</c> overlays for locators.
    /// </summary>
    [TestFixture]
    public class ScreenshotMaskTests : PageTestEx
    {
        [PlaywrightTest("page-screenshot.spec.ts", "Page screenshot paints mask locators")]
        [Test]
        [Timeout(30_000)]
        public async Task PageScreenshotShouldPaintMask()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0;background:#c00}</style>" +
                "<div id=\"secret\" style=\"position:absolute;left:20px;top:20px;width:40px;height:40px;background:#00c\"></div>").ConfigureAwait(false);

            byte[] bytes = await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("#secret") } }).ConfigureAwait(false);
            using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
            Rgba32 covered = image[30, 30];
            Rgba32 background = image[150, 80];

            Assert.That(covered.R, Is.GreaterThan(200));
            Assert.That(covered.B, Is.GreaterThan(200));
            Assert.That(covered.G, Is.LessThan(40));
            Assert.That(background.R, Is.GreaterThan(200));
            Assert.That(background.G, Is.LessThan(40));
            Assert.That(background.B, Is.LessThan(40));
            Assert.That(await page.QuerySelectorAsync("[data-pw-mask]").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-screenshot.spec.ts", "maskColor overrides the overlay")]
        [Test]
        [Timeout(30_000)]
        public async Task MaskColorShouldOverrideOverlay()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>html,body{margin:0;background:#c00}</style>" +
                "<div id=\"secret\" style=\"position:absolute;left:20px;top:20px;width:40px;height:40px;background:#00c\"></div>").ConfigureAwait(false);

            byte[] bytes = await page.ScreenshotAsync(new() { Mask = new[] { page.Locator("#secret") }, MaskColor = "#00FF00" }).ConfigureAwait(false);
            using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
            Rgba32 covered = image[30, 30];

            Assert.That(covered.G, Is.GreaterThan(200));
            Assert.That(covered.R, Is.LessThan(40));
            Assert.That(covered.B, Is.LessThan(40));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "Element screenshot honors mask")]
        [Test]
        [Timeout(30_000)]
        public async Task ElementScreenshotShouldHonorMask()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(200, 120).ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"host\" style=\"width:120px;height:80px;background:#c00;position:relative\">" +
                "<div id=\"secret\" style=\"position:absolute;left:10px;top:10px;width:30px;height:30px;background:#00c\"></div>" +
                "</div>").ConfigureAwait(false);

            IElementHandle host = await page.QuerySelectorAsync("#host").ConfigureAwait(false);
            byte[] bytes = await host.ScreenshotAsync(new() { Mask = new[] { page.Locator("#secret") } }).ConfigureAwait(false);
            using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
            Rgba32 covered = image[20, 20];

            Assert.That(covered.R, Is.GreaterThan(200));
            Assert.That(covered.B, Is.GreaterThan(200));
            Assert.That(covered.G, Is.LessThan(40));
        }
    }
}
