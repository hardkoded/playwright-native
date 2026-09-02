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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>tracing.group()</c>.
    /// </summary>
    [TestFixture]
    public class ContextTracingGroupTests : PageTestEx
    {
        [PlaywrightTest("tracing.spec.ts", "GroupAsync writes the name into the trace")]
        [Test]
        [Timeout(30_000)]
        public async Task GroupAsyncShouldWriteTheNameIntoTheTrace()
        {
            string path = Path.Combine(Path.GetTempPath(), "pwsharp-trace-group-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

                await context.Tracing.StartAsync().ConfigureAwait(false);
                await context.Tracing.GroupAsync("wave587").ConfigureAwait(false);
                await context.Tracing.StopAsync(path).ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
                JsonElement events = document.RootElement.GetProperty("traceEvents");
                Assert.That(events.ValueKind, Is.EqualTo(JsonValueKind.Array));

                bool found = false;
                foreach (JsonElement item in events.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out JsonElement name)
                        && name.ValueKind == JsonValueKind.String
                        && name.GetString() == "wave587")
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True);
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
