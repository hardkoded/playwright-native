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
    /// Official <c>library/chromium/js-coverage.spec.ts</c> parity. Do not
    /// edit leftover <c>PageCoverageTests</c>. Official file-level skip
    /// when <c>trace === 'on'</c> (this suite does not run TRACE=on).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryJsCoverageParityTests : PageTestEx
    {
        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only js-coverage.spec.ts.");
            }
        }

        [PlaywrightTest("js-coverage.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/jscoverage/simple.html", WaitUntilState.Load).ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> coverage = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Url, Does.Contain("/jscoverage/simple.html"));
            JSCoverageFunction foo = coverage[0].Functions.First(f => f.FunctionName == "foo");
            Assert.That(foo.Ranges[0].Count, Is.EqualTo(1));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("js-coverage.spec.ts", "should report sourceURLs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportSourceURLs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/jscoverage/sourceurl.html").ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> coverage = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            Assert.That(coverage[0].Url, Is.EqualTo("nicename.js"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("js-coverage.spec.ts", "should ignore eval() scripts by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreEvalScriptsByDefault()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/jscoverage/eval.html").ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> coverage = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(1));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("js-coverage.spec.ts", "shouldn't ignore eval() scripts if reportAnonymousScripts is true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldntIgnoreEvalScriptsIfReportAnonymousScriptsIsTrue()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PW_CLOCK")))
            {
                Assert.Ignore("Official it.skip(!!process.env.PW_CLOCK);");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync(reportAnonymousScripts: true).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/jscoverage/eval.html").ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> coverage = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage, Has.Some.Matches<JSCoverageEntry>(entry =>
                entry.Url == string.Empty && entry.Source == "console.log(\"foo\")"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("js-coverage.spec.ts", "should report multiple scripts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportMultipleScripts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/jscoverage/multiple.html").ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> coverage = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(2));
            List<JSCoverageEntry> sorted = coverage.OrderBy(entry => entry.Url, StringComparer.Ordinal).ToList();
            Assert.That(sorted[0].Url, Does.Contain("/jscoverage/script1.js"));
            Assert.That(sorted[1].Url, Does.Contain("/jscoverage/script2.js"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("js-coverage.spec.ts", "should NOT report scripts across navigations when enabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotReportScriptsAcrossNavigationsWhenEnabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/jscoverage/multiple.html").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> coverage = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            Assert.That(coverage.Count, Is.EqualTo(0));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("js-coverage.spec.ts", "should not hang when there is a debugger statement")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangWhenThereIsADebuggerStatement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync("() => { debugger; }").ConfigureAwait(false);
            await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
