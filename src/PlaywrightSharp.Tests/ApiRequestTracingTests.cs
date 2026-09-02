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
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>apiRequestContext.tracing</c>.
    /// </summary>
    [TestFixture]
    public class ApiRequestTracingTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("tracing.spec.ts", "browser-bound request shares context tracing")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserBoundRequestShouldShareContextTracing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page.APIRequest.Tracing, Is.SameAs(context.APIRequest.Tracing));
            Assert.That(context.APIRequest.Tracing, Is.Not.SameAs(context.Tracing));
        }

        [PlaywrightTest("tracing.spec.ts", "browser-bound request tracing records HAR")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserBoundRequestTracingShouldRecordHar()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareHarRoute();
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.Tracing.StartHarAsync(path).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.Tracing.StopHarAsync().ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement log = document.RootElement.GetProperty("log");
                Assert.That(log.GetProperty("version").GetString(), Is.EqualTo("1.2"));
                Assert.That(ContainsUrl(log.GetProperty("entries"), "/har.html"), Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "standalone request has its own tracing")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneRequestShouldHaveItsOwnTracing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await using IAPIRequestContext first = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            await using IAPIRequestContext second = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);

            Assert.That(first.Tracing, Is.Not.Null);
            Assert.That(second.Tracing, Is.Not.Null);
            Assert.That(first.Tracing, Is.Not.SameAs(second.Tracing));
            Assert.That(first.Tracing, Is.Not.SameAs(context.Tracing));
        }

        [PlaywrightTest("tracing.spec.ts", "standalone tracing group is written")]
        [Test]
        [Timeout(30_000)]
        public async Task StandaloneTracingGroupShouldBeWritten()
        {
            string path = Path.Combine(Path.GetTempPath(), "pwsharp-api-trace-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
                await request.Tracing.StartAsync().ConfigureAwait(false);
                await request.Tracing.GroupAsync("wave641").ConfigureAwait(false);
                await request.Tracing.GroupEndAsync().ConfigureAwait(false);
                await request.Tracing.StopAsync(path).ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
                JsonElement events = document.RootElement.GetProperty("traceEvents");
                Assert.That(events.ValueKind, Is.EqualTo(JsonValueKind.Array));

                bool found = false;
                foreach (JsonElement item in events.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out JsonElement name)
                        && name.ValueKind == JsonValueKind.String
                        && name.GetString() == "wave641")
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        private static void PrepareHarRoute()
        {
            Server.Reset();
            Server.SetRoute("/har.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>wave-641-har-marker</body></html>");
            });
        }

        private static string TempHarPath()
            => Path.Combine(Path.GetTempPath(), "pw-wave641-" + Guid.NewGuid().ToString("N") + ".har");

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
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

        private static bool ContainsUrl(JsonElement entries, string fragment)
        {
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string url = entry.GetProperty("request").GetProperty("url").GetString();
                if (url != null && url.Contains(fragment, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
