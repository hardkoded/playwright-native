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
    /// Direct-connection tests for <see cref="IBrowserContext.Tracing"/>.
    /// </summary>
    [TestFixture]
    public class ContextTracingTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("tracing.spec.ts", "Tracing writes a Chrome trace file")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWriteTraceFileOnStop()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CDP Tracing is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = Path.Combine(Path.GetTempPath(), "pwsharp-trace-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await context.Tracing.StartAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.EvaluateAsync("performance.mark('wave112')").ConfigureAwait(false);
                await context.Tracing.StopAsync(path).ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
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

        [PlaywrightTest("tracing.spec.ts", "WaitForCloseAsync resolves on Close")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForCloseShouldResolveWhenContextCloses()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            Task waitTask = context.WaitForCloseAsync();
            await context.CloseAsync().ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
        }
    }
}
