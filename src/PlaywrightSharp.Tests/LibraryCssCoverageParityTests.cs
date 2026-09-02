/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/chromium/css-coverage.spec.ts</c> parity. Do not
    /// edit leftover <c>PageCoverageTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryCssCoverageParityTests : PageTestEx
    {
        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only css-coverage.spec.ts.");
            }
        }

        [PlaywrightTest("css-coverage.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/simple.html").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Url, Does.Contain("/csscoverage/simple.html"));
            Assert.That(coverage[0].Ranges.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Ranges[0].Start, Is.EqualTo(1));
            Assert.That(coverage[0].Ranges[0].End, Is.EqualTo(22));
            CSSCoverageRange range = coverage[0].Ranges[0];
            Assert.That(coverage[0].Text.Substring(range.Start, range.End - range.Start), Is.EqualTo("div { color: green; }"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should report sourceURLs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportSourceURLs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/sourceurl.html").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Url, Is.EqualTo("nicename.css"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should report multiple stylesheets")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportMultipleStylesheets()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/multiple.html").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(2));
            List<CSSCoverageEntry> sorted = coverage.OrderBy(entry => entry.Url, StringComparer.Ordinal).ToList();
            Assert.That(sorted[0].Url, Does.Contain("/csscoverage/stylesheet1.css"));
            Assert.That(sorted[1].Url, Does.Contain("/csscoverage/stylesheet2.css"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should report stylesheets that have no coverage")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportStylesheetsThatHaveNoCoverage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/unused.html").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Url, Is.EqualTo("unused.css"));
            Assert.That(coverage[0].Ranges.Count, Is.EqualTo(0));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should work with media queries")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithMediaQueries()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/media.html").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Url, Does.Contain("/csscoverage/media.html"));
            Assert.That(coverage[0].Ranges.Count, Is.EqualTo(2));
            Assert.That(coverage[0].Ranges[0].Start, Is.EqualTo(8));
            Assert.That(coverage[0].Ranges[0].End, Is.EqualTo(15));
            Assert.That(coverage[0].Ranges[1].Start, Is.EqualTo(17));
            Assert.That(coverage[0].Ranges[1].End, Is.EqualTo(38));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should work with complicated usecases")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithComplicatedUsecases()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/involved.html").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Ranges.Count, Is.EqualTo(3));
            Assert.That(coverage[0].Ranges[0].Start, Is.EqualTo(149));
            Assert.That(coverage[0].Ranges[0].End, Is.EqualTo(297));
            Assert.That(coverage[0].Ranges[1].Start, Is.EqualTo(306));
            Assert.That(coverage[0].Ranges[1].End, Is.EqualTo(323));
            Assert.That(coverage[0].Ranges[2].Start, Is.EqualTo(327));
            Assert.That(coverage[0].Ranges[2].End, Is.EqualTo(433));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should ignore injected stylesheets")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreInjectedStylesheets()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.AddStyleTagAsync(new() { Content = "body { margin: 10px;}" }).ConfigureAwait(false);
            string margin = await page.EvaluateAsync<string>("() => window.getComputedStyle(document.body).margin").ConfigureAwait(false);
            Assert.That(margin, Is.EqualTo("10px"));
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(0));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should report stylesheets across navigations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportStylesheetsAcrossNavigations()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync(resetOnNavigation: false).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/multiple.html").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(2));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should NOT report scripts across navigations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotReportScriptsAcrossNavigations()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/csscoverage/multiple.html").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(0));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("css-coverage.spec.ts", "should work with a recently loaded stylesheet")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithARecentlyLoadedStylesheet()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.EvaluateAsync(
                @"(url) => {
                    return (async () => {
                      document.body.textContent = 'hello, world';
                      const link = document.createElement('link');
                      link.rel = 'stylesheet';
                      link.href = url;
                      document.head.appendChild(link);
                      await new Promise(x => link.onload = x);
                      await new Promise(f => requestAnimationFrame(f));
                    })();
                }",
                TestConstants.ServerUrl + "/csscoverage/stylesheet1.css").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> coverage = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
