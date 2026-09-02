/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for leftover <see cref="IPage.PdfAsync"/> options.
    /// Chromium headless only.
    /// </summary>
    [TestFixture]
    public class PagePdfTests : PageTestEx
    {
        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors scale")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorScale()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1 style=\"font-size:72px\">Scale</h1>").ConfigureAwait(false);

            byte[] full = await page.PdfAsync(new() { Scale = 1f }).ConfigureAwait(false);
            byte[] half = await page.PdfAsync(new() { Scale = 0.5f }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(full, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(Encoding.ASCII.GetString(half, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(half, Is.Not.EqualTo(full));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors width and height")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorWidthAndHeight()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Paper</h1>").ConfigureAwait(false);

            byte[] letter = await page.PdfAsync().ConfigureAwait(false);
            byte[] small = await page.PdfAsync(new() { Width = "4in", Height = "6in" }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(small, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(small, Is.Not.EqualTo(letter));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors format")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorFormat()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Format</h1>").ConfigureAwait(false);

            byte[] letter = await page.PdfAsync(new() { Format = "Letter" }).ConfigureAwait(false);
            byte[] a4 = await page.PdfAsync(new() { Format = "A4" }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(a4, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(a4, Is.Not.EqualTo(letter));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors margin")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorMargin()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Margin</h1>").ConfigureAwait(false);

            byte[] none = await page.PdfAsync().ConfigureAwait(false);
            byte[] inset = await page.PdfAsync(new()
            {
                Margin = new Margin
                {
                    Top = "1in",
                    Right = "1in",
                    Bottom = "1in",
                    Left = "1in",
                }
            }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(inset, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(inset, Is.Not.EqualTo(none));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors pageRanges")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorPageRanges()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"page-break-after:always\">one</div><div>two</div>").ConfigureAwait(false);

            byte[] all = await page.PdfAsync().ConfigureAwait(false);
            byte[] first = await page.PdfAsync(new() { PageRanges = "1" }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(first, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(first, Is.Not.EqualTo(all));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors displayHeaderFooter")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorDisplayHeaderFooter()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Header</h1>").ConfigureAwait(false);

            byte[] none = await page.PdfAsync(new() { DisplayHeaderFooter = false }).ConfigureAwait(false);
            byte[] shown = await page.PdfAsync(new() { DisplayHeaderFooter = true }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(shown, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(shown, Is.Not.EqualTo(none));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors header and footer templates")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorHeaderFooterTemplates()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Body</h1>").ConfigureAwait(false);

            byte[] alpha = await page.PdfAsync(new() { DisplayHeaderFooter = true, HeaderTemplate = "<span style=\"font-size:16px\">ALPHA</span>", FooterTemplate = "<span style=\"font-size:12px\"></span>" }).ConfigureAwait(false);
            byte[] beta = await page.PdfAsync(new() { DisplayHeaderFooter = true, HeaderTemplate = "<span style=\"font-size:16px\">BETA-HEADER</span>", FooterTemplate = "<span style=\"font-size:12px\"></span>" }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(alpha, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(beta, Is.Not.EqualTo(alpha));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors preferCSSPageSize")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorPreferCSSPageSize()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<style>@page { size: 4in 6in; }</style><h1>CSS page</h1>").ConfigureAwait(false);

            byte[] paper = await page.PdfAsync(new() { PreferCSSPageSize = false }).ConfigureAwait(false);
            byte[] css = await page.PdfAsync(new() { PreferCSSPageSize = true }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(css, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(css, Is.Not.EqualTo(paper));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors tagged")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorTagged()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Tagged PDF</h1><p>wave345</p>").ConfigureAwait(false);

            byte[] plain = await page.PdfAsync(new() { Tagged = false }).ConfigureAwait(false);
            byte[] tagged = await page.PdfAsync(new() { Tagged = true }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(tagged, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(tagged, Is.Not.EqualTo(plain));
            Assert.That(Encoding.ASCII.GetString(tagged), Does.Contain("MarkInfo").Or.Contain("StructTreeRoot"));
        }

        [PlaywrightTest("pdf.spec.ts", "PdfAsync honors outline")]
        [Test]
        [Timeout(30_000)]
        public async Task PdfAsyncShouldHonorOutline()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("PDF generation is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync(headless: true).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<h1>Chapter</h1><p>wave346</p>").ConfigureAwait(false);

            byte[] plain = await page.PdfAsync(new() { Tagged = true, Outline = false }).ConfigureAwait(false);
            byte[] outlined = await page.PdfAsync(new() { Tagged = true, Outline = true }).ConfigureAwait(false);

            Assert.That(Encoding.ASCII.GetString(outlined, 0, 5), Is.EqualTo("%PDF-"));
            Assert.That(outlined, Is.Not.EqualTo(plain));
        }
    }
}
