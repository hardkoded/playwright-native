/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <see cref="ScreenshotType.Webp"/>.
    /// </summary>
    [TestFixture]
    public class PageScreenshotWebpTests : PageTestEx
    {
        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync returns a WebP")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnWebpBytes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(120, 80).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"width:100px;height:60px;background:#c00\"></div>").ConfigureAwait(false);

            byte[] bytes = await page.ScreenshotAsync(new() { Type = ScreenshotType.Webp, Quality = 80 }).ConfigureAwait(false);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(20));
            Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WEBP"));

            using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
            Assert.That(image.Width, Is.GreaterThan(0));
            Assert.That(image.Height, Is.GreaterThan(0));
        }

        [PlaywrightTest("page-screenshot.spec.ts", "ScreenshotAsync WebP is rejected on WebKit")]
        [Test]
        [Timeout(30_000)]
        public async Task WebpShouldThrowOnWebKit()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>webp</div>").ConfigureAwait(false);

            byte[] bytes = await page.ScreenshotAsync(new() { Type = ScreenshotType.Webp }).ConfigureAwait(false);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WEBP"));
        }
    }
}
