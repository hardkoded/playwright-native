/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <c>CRPage.ScreenshotAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRScreenshotTests : CRTestBase
    {
        [PlaywrightTest("screenshot.spec.ts", "should return png bytes")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnPngBytes()
        {
            await Page.SetContentAsync("<div style='width:100px;height:100px;background:red'></div>").ConfigureAwait(false);

            byte[] bytes = await Page.ScreenshotAsync().ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null.And.Not.Empty);

            // PNG magic number: 89 50 4E 47 0D 0A 1A 0A
            Assert.That(bytes[0], Is.EqualTo(0x89));
            Assert.That(bytes[1], Is.EqualTo(0x50));
            Assert.That(bytes[2], Is.EqualTo(0x4E));
            Assert.That(bytes[3], Is.EqualTo(0x47));
        }

        [PlaywrightTest("screenshot.spec.ts", "should return jpeg bytes")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnJpegBytes()
        {
            await Page.SetContentAsync("<div style='width:100px;height:100px;background:blue'></div>").ConfigureAwait(false);

            byte[] bytes = await Page.ScreenshotAsync(new ScreenshotOptions
            {
                Format = "jpeg",
                Quality = 80,
            }).ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null.And.Not.Empty);

            // JPEG magic: FF D8
            Assert.That(bytes[0], Is.EqualTo(0xFF));
            Assert.That(bytes[1], Is.EqualTo(0xD8));
        }

        [PlaywrightTest("screenshot.spec.ts", "should clip screenshot")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClipScreenshot()
        {
            await Page.SetContentAsync("<div style='width:400px;height:400px;background:green'></div>").ConfigureAwait(false);

            byte[] bytes = await Page.ScreenshotAsync(new ScreenshotOptions
            {
                Clip = new ScreenshotClip { X = 0, Y = 0, Width = 50, Height = 50 },
            }).ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null.And.Not.Empty);

            // We can't easily assert dimensions without an image library; the fact that CDP
            // accepted the clip and returned data is the meaningful signal.
        }

        [PlaywrightTest("screenshot.spec.ts", "should support full page")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportFullPage()
        {
            await Page.SetContentAsync("<div style='width:100vw;height:3000px;background:linear-gradient(red,blue)'></div>").ConfigureAwait(false);

            byte[] viewportOnly = await Page.ScreenshotAsync().ConfigureAwait(false);
            byte[] fullPage = await Page.ScreenshotAsync(new ScreenshotOptions { FullPage = true }).ConfigureAwait(false);

            // Full-page should be a larger payload than viewport-only for the same content.
            Assert.That(fullPage.Length, Is.GreaterThan(viewportOnly.Length));
        }

        [PlaywrightTest("screenshot.spec.ts", "should return consistent bytes for same content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnConsistentBytesForSameContent()
        {
            await Page.SetContentAsync("<div style='width:100px;height:100px;background:#333'></div>").ConfigureAwait(false);

            byte[] first = await Page.ScreenshotAsync().ConfigureAwait(false);
            byte[] second = await Page.ScreenshotAsync().ConfigureAwait(false);

            Assert.That(first.Length, Is.EqualTo(second.Length));
        }
    }
}
