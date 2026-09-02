/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>tracing.startChunk()</c>.
    /// </summary>
    [TestFixture]
    public class ContextTracingStartChunkTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("tracing.spec.ts", "StartChunkAsync begins a new chunk")]
        [Test]
        [Timeout(30_000)]
        public async Task StartChunkAsyncShouldBeginANewChunk()
        {
            if (!TestConstants.IsChromium)
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext otherContext = await browser.NewContextAsync().ConfigureAwait(false);
                await otherContext.Tracing.StartAsync().ConfigureAwait(false);
                await otherContext.Tracing.StartChunkAsync(new() { Name = "wave582", Title = "start-chunk" }).ConfigureAwait(false);
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = Path.Combine(Path.GetTempPath(), "pwsharp-trace-chunk-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await context.Tracing.StartAsync().ConfigureAwait(false);
                await context.Tracing.StartChunkAsync(new() { Name = "wave582", Title = "start-chunk" }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.EvaluateAsync("performance.mark('wave582')").ConfigureAwait(false);
                await context.Tracing.StopAsync(path).ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
                JsonElement events = document.RootElement.GetProperty("traceEvents");
                Assert.That(events.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(events.GetArrayLength(), Is.GreaterThan(0));
            }
            finally
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
