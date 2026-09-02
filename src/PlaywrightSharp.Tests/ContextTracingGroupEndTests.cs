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
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>tracing.groupEnd()</c>.
    /// </summary>
    [TestFixture]
    public class ContextTracingGroupEndTests : PageTestEx
    {
        [PlaywrightTest("tracing.spec.ts", "GroupEndAsync writes a duration-end event")]
        [Test]
        [Timeout(30_000)]
        public async Task GroupEndAsyncShouldWriteADurationEndEvent()
        {
            string path = Path.Combine(Path.GetTempPath(), "pwsharp-trace-group-end-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

                await context.Tracing.StartAsync().ConfigureAwait(false);
                await context.Tracing.GroupAsync("wave588").ConfigureAwait(false);
                await context.Tracing.GroupEndAsync().ConfigureAwait(false);
                await context.Tracing.StopAsync(path).ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
                JsonElement events = document.RootElement.GetProperty("traceEvents");
                Assert.That(events.ValueKind, Is.EqualTo(JsonValueKind.Array));

                bool foundEnd = false;
                foreach (JsonElement item in events.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out JsonElement name)
                        && name.ValueKind == JsonValueKind.String
                        && name.GetString() == "wave588"
                        && item.TryGetProperty("ph", out JsonElement phase)
                        && phase.GetString() == "E")
                    {
                        foundEnd = true;
                        break;
                    }
                }

                Assert.That(foundEnd, Is.True);
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
