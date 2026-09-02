/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/chromium/tracing.spec.ts</c> parity. Chromium-only
    /// <c>browser.startTracing</c> / <c>stopTracing</c> (CDP Tracing, not
    /// <c>context.tracing</c>). Official Node <c>process.on('warning')</c> is
    /// Node-only; the portable assertion is the written trace file.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryChromiumTracingParityTests : PageTestEx
    {
        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only tracing.spec.ts.");
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should output a trace")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOutputATrace()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            string outputTraceFile = UniqueTracePath("trace.json");
            await browser.StartTracingAsync(page, path: outputTraceFile, screenshots: true).ConfigureAwait(false);
            for (int i = 0; i < 11; i++)
            {
                await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);
            }

            await browser.StopTracingAsync().ConfigureAwait(false);
            Assert.That(File.Exists(outputTraceFile), Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should create directories as needed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCreateDirectoriesAsNeeded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            string filePath = UniqueTracePath(Path.Combine("these", "are", "directories", "trace.json"));
            await browser.StartTracingAsync(page, path: filePath, screenshots: true).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);
            await browser.StopTracingAsync().ConfigureAwait(false);
            Assert.That(File.Exists(filePath), Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should run with custom categories if provided")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunWithCustomCategoriesIfProvided()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            string outputTraceFile = UniqueTracePath("trace.json");
            await browser.StartTracingAsync(
                page,
                path: outputTraceFile,
                categories: new[] { "disabled-by-default-cc.debug" }).ConfigureAwait(false);
            await RafrafAsync(page).ConfigureAwait(false);
            await browser.StopTracingAsync().ConfigureAwait(false);

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputTraceFile));
            JsonElement root = document.RootElement;
            bool hasConfig = root.TryGetProperty("metadata", out JsonElement metadata)
                && metadata.TryGetProperty("trace-config", out JsonElement config)
                && (config.GetString() ?? string.Empty).Contains("disabled-by-default-cc.debug", StringComparison.Ordinal);
            int eventCount = 0;
            if (root.TryGetProperty("traceEvents", out JsonElement events)
                && events.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in events.EnumerateArray())
                {
                    if (item.TryGetProperty("cat", out JsonElement cat)
                        && cat.GetString() == "disabled-by-default-cc.debug")
                    {
                        eventCount++;
                    }
                }
            }

            Assert.That(hasConfig || eventCount > 0, Is.True);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should throw if tracing on two pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowIfTracingOnTwoPages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            string outputTraceFile = UniqueTracePath("trace.json");
            await browser.StartTracingAsync(page, path: outputTraceFile).ConfigureAwait(false);
            IPage newPage = await browser.NewPageAsync().ConfigureAwait(false);
            Exception error = null;
            try
            {
                await browser.StartTracingAsync(newPage, path: outputTraceFile).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            await newPage.CloseAsync().ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            await browser.StopTracingAsync().ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should return a buffer")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnABuffer()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            string outputTraceFile = UniqueTracePath("trace.json");
            await browser.StartTracingAsync(page, path: outputTraceFile, screenshots: true).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);
            byte[] trace = await browser.StopTracingAsync().ConfigureAwait(false);
            byte[] buf = File.ReadAllBytes(outputTraceFile);
            Assert.That(Encoding.UTF8.GetString(trace), Is.EqualTo(Encoding.UTF8.GetString(buf)));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should work without options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithoutOptions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await browser.StartTracingAsync(page).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);
            byte[] trace = await browser.StopTracingAsync().ConfigureAwait(false);
            Assert.That(trace, Is.Not.Null.And.Not.Empty);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should support a buffer without a path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportABufferWithoutAPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await browser.StartTracingAsync(page, screenshots: true).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);
            await RafrafAsync(page, 100).ConfigureAwait(false);
            byte[] trace = await browser.StopTracingAsync().ConfigureAwait(false);
            Assert.That(Encoding.UTF8.GetString(trace), Does.Contain("screenshot"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        private static string UniqueTracePath(string relative)
        {
            string root = Path.Combine(Path.GetTempPath(), "pwsharp-cr-trace-" + Guid.NewGuid().ToString("N"));
            return Path.Combine(root, relative);
        }

        private static async Task RafrafAsync(IPage page, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                await page.EvaluateAsync<object>(
                    "(() => new Promise(f => requestAnimationFrame(() => requestAnimationFrame(f))))()").ConfigureAwait(false);
            }
        }
    }
}
