/*
 * Copyright (c) Microsoft Corporation.
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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/coverage.spec.ts</c> parity. The file-level
    /// <c>trace === 'on'</c> skip does not apply here: tests never run with
    /// an outer trace.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class CoverageTests : BrowserTestEx
    {
        private string _outputDir;

        [SetUp]
        public void CreateOutputDir()
        {
            _outputDir = Path.Combine(Path.GetTempPath(), "pw-coverage-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_outputDir);
        }

        [TearDown]
        public void DeleteOutputDir()
        {
            try
            {
                Directory.Delete(_outputDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        [PlaywrightTest("coverage.spec.ts", "should collect istanbul coverage into the trace")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectIstanbulCoverageIntoTheTrace()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });

            IPage page = await context.NewPageAsync();
            await page.SetContentAsync(CoverageScript("a.js", 3));

            // Coverage of a closed page is collected automatically.
            IPage page2 = await context.NewPageAsync();
            await page2.SetContentAsync(CoverageScript("b.js", 2));
            await page2.CloseAsync();

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data = ParseTraceCoverage(traceFile);
            Assert.That(data["a.js"].S["0"], Is.EqualTo(3));
            Assert.That(data["b.js"].S["0"], Is.EqualTo(2));
        }

        [PlaywrightTest("coverage.spec.ts", "should collect coverage per trace chunk")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectCoveragePerTraceChunk()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();

            await page.SetContentAsync(CoverageScript("a.js", 5));
            string traceFile1 = OutputPath("trace1.zip");
            await context.Tracing.StopChunkAsync(new() { Path = traceFile1 });

            await context.Tracing.StartChunkAsync();
            await page.SetContentAsync(CoverageScript("b.js", 7));
            string traceFile2 = OutputPath("trace2.zip");
            await context.Tracing.StopChunkAsync(new() { Path = traceFile2 });

            await context.Tracing.StopAsync();
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data1 = ParseTraceCoverage(traceFile1);
            Assert.That(data1["a.js"].S["0"], Is.EqualTo(5));
            Assert.That(data1.ContainsKey("b.js"), Is.False);

            Dictionary<string, IstanbulFileCoverage> data2 = ParseTraceCoverage(traceFile2);
            Assert.That(data2["b.js"].S["0"], Is.EqualTo(7));
            Assert.That(data2.ContainsKey("a.js"), Is.False);
        }

        [PlaywrightTest("coverage.spec.ts", "should report maps once and counters incrementally")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportMapsOnceAndCountersIncrementally()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);

            // Actions collect the counters on their own, so hit and collect in one evaluate.
            IstanbulCoverageChunk[] first = ToChunks(await page.EvaluateAsync<JsonElement>(
                @"coverage => {
                    window.__coverage__ = JSON.parse(coverage);
                    return window.__pwCoverageTake().map(json => JSON.parse(json));
                }",
                FileCoverageJson("a.js", 3)));
            Assert.That(first[0].Data["a.js"].StatementMap, Is.Not.Null);
            Assert.That(first[0].Data["a.js"].S, Is.EqualTo(new Dictionary<string, long> { ["0"] = 3 }));

            IstanbulCoverageChunk[] second = ToChunks(await page.EvaluateAsync<JsonElement>(
                @"() => {
                    window.__coverage__['a.js'].s['0'] += 2;
                    return window.__pwCoverageTake().map(json => JSON.parse(json));
                }"));
            Assert.That(second[0].Data["a.js"].StatementMap, Is.Null);
            Assert.That(second[0].Data["a.js"].S, Is.EqualTo(new Dictionary<string, long> { ["0"] = 2 }));

            // Nothing was hit since the last report.
            Assert.That(await page.EvaluateAsync<string[]>("() => window.__pwCoverageTake()"), Is.Empty);

            await context.Tracing.StopAsync();
            await context.CloseAsync();
        }

        [PlaywrightTest("coverage.spec.ts", "should accumulate counters across pulls and keep never hit files")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAccumulateCountersAcrossPullsAndKeepNeverHitFiles()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.SetContentAsync(CoverageScript("a.js", 0));
            await page.EvaluateAsync("() => window.__coverage__['a.js'].s['0'] += 4");
            await page.EvaluateAsync("() => window.__coverage__['a.js'].s['0'] += 3");

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data = ParseTraceCoverage(traceFile);
            Assert.That(data["a.js"].S["0"], Is.EqualTo(7));
            Assert.That(data["a.js"].StatementMap.Keys, Is.EqualTo(new[] { "0" }));
        }

        [PlaywrightTest("coverage.spec.ts", "should collect coverage of a page closed by in-page script")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectCoverageOfAPageClosedByInPageScript()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);

            IPage popup = await OpenPopupAsync(page, "popup.js", 5);

            // An in-page close never reaches Playwright as page.close(), the counters
            // are preserved because the actions collect them as they go.
            await Task.WhenAll(
                popup.WaitForCloseAsync(),
                popup.EvaluateAsync("() => setTimeout(() => window.close(), 0)"));

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data = ParseTraceCoverage(traceFile);
            Assert.That(data["popup.js"].S["0"], Is.EqualTo(5));
        }

        [PlaywrightTest("coverage.spec.ts", "should not double count a stash picked up twice")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotDoubleCountAStashPickedUpTwice()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);

            // Emulate two documents picking up the same stash before either removes it.
            string prefix = "__pwCoverage." + ((IHasOfficialTrace)context).OfficialTrace.CoverageSessionId + ".";
            await page.EvaluateAsync(
                @"({ prefix, coverage }) => {
                    const chunk = JSON.stringify({ id: 'stash-id', data: JSON.parse(coverage) });
                    localStorage.setItem(prefix + 'one', chunk);
                    localStorage.setItem(prefix + 'two', chunk);
                }",
                new { prefix, coverage = FileCoverageJson("stashed.js", 4) });

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data = ParseTraceCoverage(traceFile);
            Assert.That(data["stashed.js"].S["0"], Is.EqualTo(4));
        }

        [PlaywrightTest("coverage.spec.ts", "should discard stashes of other sessions")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDiscardStashesOfOtherSessions()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.EvaluateAsync(
                @"coverage => {
                    localStorage.setItem('__pwCoverage.other-session.1', JSON.stringify({ id: 'other', data: JSON.parse(coverage) }));
                }",
                FileCoverageJson("stale.js", 7));

            string[] remaining = await page.EvaluateAsync<string[]>("() => Object.keys(localStorage).filter(key => key.startsWith('__pwCoverage.'))");
            Assert.That(remaining, Is.Empty);

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            // The stale stash was the only coverage, so nothing was collected at all.
            Assert.That(ParseTraceCoverage(traceFile), Is.Null);
        }

        [PlaywrightTest("coverage.spec.ts", "should collect coverage of an origin left without a page")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectCoverageOfAnOriginLeftWithoutAPage()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);

            IPage popup = await OpenPopupAsync(page, "popup.js", 0);
            await popup.EvaluateAsync(
                @"url => {
                    window.__go = () => {
                        window.__coverage__['popup.js'].s['0'] = 5;
                        window.location.href = url;
                    };
                }",
                TestConstants.CrossProcessHttpPrefix + "/empty.html");

            // The popup is hit and navigates away, leaving a stash behind. Triggered from
            // the opener, so that the pull after the action does not race the navigation.
            await Task.WhenAll(
                popup.WaitForURLAsync(TestConstants.CrossProcessHttpPrefix + "/empty.html"),
                page.EvaluateAsync("() => window.__popup.__go()"));

            // Leave the origin of the stash without a page to relay it.
            await page.GoToAsync(TestConstants.CrossProcessHttpPrefix + "/empty.html");

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data = ParseTraceCoverage(traceFile);
            Assert.That(data["popup.js"].S["0"], Is.EqualTo(5));
        }

        [PlaywrightTest("coverage.spec.ts", "should pull counters as the actions go")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPullCountersAsTheActionsGo()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.SetContentAsync(CoverageScript("a.js", 3));

            // The counters are collected by the action itself, not only by the final flush.
            Assert.That(await page.EvaluateAsync<int>("() => window.__coverage__['a.js'].s['0']"), Is.EqualTo(0));

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data = ParseTraceCoverage(traceFile);
            Assert.That(data["a.js"].S["0"], Is.EqualTo(3));
        }

        [PlaywrightTest("coverage.spec.ts", "should not collect coverage without the option")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCollectCoverageWithoutTheOption()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync();
            IPage page = await context.NewPageAsync();
            await page.SetContentAsync(CoverageScript("a.js", 1));

            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Assert.That(ParseTraceCoverage(traceFile), Is.Null);
        }

        [PlaywrightTest("coverage.spec.ts", "should count only the hits after start")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCountOnlyTheHitsAfterStart()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.EvaluateAsync("coverage => window.__coverage__ = JSON.parse(coverage)", FileCoverageJson("a.js", 3));

            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            await page.EvaluateAsync("() => window.__coverage__['a.js'].s['0'] += 2");
            string traceFile = OutputPath("trace.zip");
            await context.Tracing.StopAsync(new() { Path = traceFile });
            await context.CloseAsync();

            Dictionary<string, IstanbulFileCoverage> data = ParseTraceCoverage(traceFile);
            Assert.That(data["a.js"].S["0"], Is.EqualTo(2));
            Assert.That(data["a.js"].StatementMap.Keys, Is.EqualTo(new[] { "0" }));
        }

        [PlaywrightTest("coverage.spec.ts", "should stop collecting when tracing stops")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStopCollectingWhenTracingStops()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.EvaluateAsync("coverage => window.__coverage__ = JSON.parse(coverage)", FileCoverageJson("a.js", 1));
            await context.Tracing.StopAsync();

            Assert.That(await page.EvaluateAsync<string>("() => typeof window.__pwCoverageTake"), Is.EqualTo("undefined"));

            // Leaving the document no longer stashes the counters.
            await page.EvaluateAsync("() => window.__coverage__['a.js'].s['0'] = 5");
            await page.GoToAsync(TestConstants.ServerUrl + "/title.html");
            Assert.That(await page.EvaluateAsync<string[]>("() => Object.keys(localStorage).filter(key => key.startsWith('__pwCoverage.'))"), Is.Empty);
            Assert.That(await page.EvaluateAsync<string>("() => typeof window.__pwCoverageTake"), Is.EqualTo("undefined"));
            await context.CloseAsync();
        }

        [PlaywrightTest("coverage.spec.ts", "should surface a failure to stash the coverage")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurfaceAFailureToStashTheCoverage()
        {
            IBrowserContext context = await Browser.NewContextAsync();
            await context.Tracing.StartAsync(new TracingStartOptions { Coverage = true });
            IPage page = await context.NewPageAsync();
            await page.GoToAsync(TestConstants.EmptyPage);
            await page.EvaluateAsync(
                @"coverage => {
                    window.__coverage__ = JSON.parse(coverage);
                    // The stash does not fit, the error record still does.
                    const setItem = Storage.prototype.setItem;
                    Storage.prototype.setItem = function(key, value) {
                        if (value.length > 100)
                            throw new DOMException('The quota has been exceeded.', 'QuotaExceededError');
                        setItem.call(this, key, value);
                    };
                    window.dispatchEvent(new Event('pagehide'));
                }",
                FileCoverageJson("a.js", 3));

            PlaywrightException error = Assert.ThrowsAsync<PlaywrightException>(() => context.Tracing.StopAsync(new() { Path = OutputPath("trace.zip") }));
            Assert.That(error.Message, Does.Contain("Failed to stash the coverage: QuotaExceededError: The quota has been exceeded."));
            await context.CloseAsync();
        }

        private static string FileCoverageJson(string file, int s0)
            => JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [file] = new
                {
                    path = file,
                    statementMap = new Dictionary<string, object>
                    {
                        ["0"] = new { start = new { line = 1, column = 0 }, end = new { line = 1, column = 20 } },
                    },
                    fnMap = new { },
                    branchMap = new { },
                    s = new Dictionary<string, int> { ["0"] = s0 },
                    f = new { },
                    b = new { },
                },
            });

        private static string CoverageScript(string file, int s0)
            => "<script>window.__coverage__ = " + FileCoverageJson(file, s0) + "</script>";

        private static IstanbulCoverageChunk[] ToChunks(JsonElement value)
            => JsonSerializer.Deserialize<IstanbulCoverageChunk[]>(value.GetRawText());

        private static Dictionary<string, IstanbulFileCoverage> ParseTraceCoverage(string file)
        {
            using ZipArchive zip = ZipFile.OpenRead(file);
            ZipArchiveEntry[] entries = zip.Entries.Where(entry => Regex.IsMatch(entry.FullName, @"(^|-)trace\.coverage$")).ToArray();
            if (entries.Length == 0)
            {
                return null;
            }

            Dictionary<string, IstanbulFileCoverage> coverage = new(StringComparer.Ordinal);
            foreach (ZipArchiveEntry entry in entries)
            {
                using StreamReader reader = new(entry.Open());
                IstanbulCoverage.Merge(coverage, IstanbulCoverage.Parse(reader.ReadToEnd()));
            }

            return coverage;
        }

        private static async Task<IPage> OpenPopupAsync(IPage page, string file, int s0)
        {
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await Task.WhenAll(
                popupTask,
                page.EvaluateAsync("url => window.__popup = window.open(url)", TestConstants.EmptyPage));
            IPage popup = await popupTask;
            await popup.EvaluateAsync("coverage => window.__coverage__ = JSON.parse(coverage)", FileCoverageJson(file, s0));
            return popup;
        }

        private string OutputPath(string name) => Path.Combine(_outputDir, name);
    }
}
