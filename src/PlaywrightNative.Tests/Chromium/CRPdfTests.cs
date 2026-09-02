/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <c>CRPage.PdfAsync</c>. Chromium headless only.
    /// </summary>
    [TestFixture]
    public class CRPdfTests : CRTestBase
    {
        [PlaywrightTest("pdf.spec.ts", "should return pdf bytes")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnPdfBytes()
        {
            await Page.SetContentAsync("<h1>Hello PDF</h1>").ConfigureAwait(false);

            byte[] bytes = await Page.PdfAsync().ConfigureAwait(false);

            Assert.That(bytes, Is.Not.Null.And.Not.Empty);

            // PDF magic: "%PDF-"
            Assert.That(bytes[0], Is.EqualTo(0x25));
            Assert.That(bytes[1], Is.EqualTo(0x50));
            Assert.That(bytes[2], Is.EqualTo(0x44));
            Assert.That(bytes[3], Is.EqualTo(0x46));
            Assert.That(bytes[4], Is.EqualTo(0x2D));
        }

        [PlaywrightTest("pdf.spec.ts", "Landscape should produce different output")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LandscapeShouldProduceDifferentOutput()
        {
            await Page.SetContentAsync("<h1>test</h1>").ConfigureAwait(false);

            byte[] portrait = await Page.PdfAsync(landscape: false).ConfigureAwait(false);
            byte[] landscape = await Page.PdfAsync(landscape: true).ConfigureAwait(false);

            // Page dimensions flip, so the PDFs won't be byte-identical.
            Assert.That(landscape, Is.Not.EqualTo(portrait));
        }

        [PlaywrightTest("pdf.spec.ts", "Print background should change output")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PrintBackgroundShouldChangeOutput()
        {
            await Page.SetContentAsync(
                "<body style='background:red'><div>x</div></body>").ConfigureAwait(false);

            byte[] noBg = await Page.PdfAsync(printBackground: false).ConfigureAwait(false);
            byte[] withBg = await Page.PdfAsync(printBackground: true).ConfigureAwait(false);

            Assert.That(withBg, Is.Not.EqualTo(noBg));
        }
    }
}
