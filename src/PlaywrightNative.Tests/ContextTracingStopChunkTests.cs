/*
 * Copyright (c) 2020 Dario Kondratiuk
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
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>tracing.stopChunk()</c>.
    /// </summary>
    [TestFixture]
    public class ContextTracingStopChunkTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("tracing.spec.ts", "StopChunkAsync writes a file")]
        [Test]
        [Timeout(30_000)]
        public async Task StopChunkAsyncShouldWriteAFileWithoutEndingTheSession()
        {
            if (!TestConstants.IsChromium)
            {
                string otherPath = Path.Combine(Path.GetTempPath(), "pwsharp-trace-chunk-empty-" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                    await using IBrowserContext otherContext = await browser.NewContextAsync().ConfigureAwait(false);
                    await otherContext.Tracing.StartAsync().ConfigureAwait(false);
                    await otherContext.Tracing.StartChunkAsync().ConfigureAwait(false);
                    await otherContext.Tracing.StopChunkAsync(otherPath).ConfigureAwait(false);
                    Assert.That(File.Exists(otherPath), Is.True);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(otherPath))
                        {
                            File.Delete(otherPath);
                        }
                    }
                    catch (IOException)
                    {
                    }
                }

                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string chunkPath = Path.Combine(Path.GetTempPath(), "pwsharp-trace-chunk-" + Guid.NewGuid().ToString("N") + ".json");
            string stopPath = Path.Combine(Path.GetTempPath(), "pwsharp-trace-stop-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);

                await context.Tracing.StartAsync().ConfigureAwait(false);
                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.EvaluateAsync("performance.mark('wave583')").ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(chunkPath).ConfigureAwait(false);

                Assert.That(File.Exists(chunkPath), Is.True);
                using (JsonDocument chunkDocument = JsonDocument.Parse(await File.ReadAllTextAsync(chunkPath).ConfigureAwait(false)))
                {
                    JsonElement chunkEvents = chunkDocument.RootElement.GetProperty("traceEvents");
                    Assert.That(chunkEvents.ValueKind, Is.EqualTo(JsonValueKind.Array));
                }

                await page.EvaluateAsync("performance.mark('wave583-after-chunk')").ConfigureAwait(false);
                await context.Tracing.StopAsync(stopPath).ConfigureAwait(false);
                Assert.That(File.Exists(stopPath), Is.True);
            }
            finally
            {
                foreach (string path in new[] { chunkPath, stopPath })
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
}
