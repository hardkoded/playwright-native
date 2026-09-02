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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <c>recordHarPath</c> write-on-close.
    /// </summary>
    [TestFixture]
    public class ContextHarTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-har.spec.ts", "recordHarPath writes entries on close")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWriteHarEntriesOnContextClose()
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
                IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = path }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement log = document.RootElement.GetProperty("log");
                Assert.That(log.GetProperty("version").GetString(), Is.EqualTo("1.2"));
                Assert.That(log.GetProperty("creator").GetProperty("name").GetString(), Is.EqualTo("Playwright"));

                bool found = ContainsUrl(log.GetProperty("entries"), "/har.html");
                Assert.That(found, Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "recordHarPath includes response text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldIncludeResponseContentByDefault()
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
                IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = path }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement entry = FindEntry(document.RootElement.GetProperty("log").GetProperty("entries"), "/har.html");
                Assert.That(entry.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
                Assert.That(entry.GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(200));
                string text = entry.GetProperty("response").GetProperty("content").GetProperty("text").GetString();
                Assert.That(text, Does.Contain("wave-102-har-marker"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "recordHarOmitContent skips response text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldOmitResponseContentWhenRequested()
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
                IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = path, RecordHarOmitContent = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement entry = FindEntry(document.RootElement.GetProperty("log").GetProperty("entries"), "/har.html");
                Assert.That(entry.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
                JsonElement content = entry.GetProperty("response").GetProperty("content");
                Assert.That(content.TryGetProperty("text", out _), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "recordHarMode Minimal skips response text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldOmitResponseContentWhenModeIsMinimal()
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
                IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = path, RecordHarMode = HarMode.Minimal }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement entry = FindEntry(document.RootElement.GetProperty("log").GetProperty("entries"), "/har.html");
                Assert.That(entry.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
                JsonElement content = entry.GetProperty("response").GetProperty("content");
                Assert.That(content.TryGetProperty("text", out _), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "options bag recordHarPath")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWriteHarFromOptionsBag()
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
                IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
                {
                    RecordHarPath = path,
                }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                Assert.That(
                    ContainsUrl(document.RootElement.GetProperty("log").GetProperty("entries"), "/har.html"),
                    Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "recordHarUrl filters recorded entries")]
        [Test]
        [Timeout(30_000)]
        public async Task RecordHarUrlShouldFilterEntries()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>keep</body></html>");
            });
            Server.SetRoute("/other.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>skip</body></html>");
            });

            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = path, RecordHarUrlFilter = "**/har.html" }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/other.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement entries = document.RootElement.GetProperty("log").GetProperty("entries");
                Assert.That(ContainsUrl(entries, "/har.html"), Is.True);
                Assert.That(ContainsUrl(entries, "/other.html"), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "recordHarUrlRegex filters recorded entries")]
        [Test]
        [Timeout(30_000)]
        public async Task RecordHarUrlRegexShouldFilterEntries()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>keep</body></html>");
            });
            Server.SetRoute("/other.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>skip</body></html>");
            });

            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = path, RecordHarUrlFilterRegex = new Regex("har\\.html$") }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/other.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement entries = document.RootElement.GetProperty("log").GetProperty("entries");
                Assert.That(ContainsUrl(entries, "/har.html"), Is.True);
                Assert.That(ContainsUrl(entries, "/other.html"), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("browsercontext-har.spec.ts", "recordHarContent Attach writes sidecar files")]
        [Test]
        [Timeout(30_000)]
        public async Task RecordHarContentAttachShouldWriteSidecarFiles()
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
                IBrowserContext context = await browser.NewContextAsync(new() { RecordHarPath = path, RecordHarContent = HarContentPolicy.Attach }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement entry = FindEntry(document.RootElement.GetProperty("log").GetProperty("entries"), "/har.html");
                Assert.That(entry.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
                JsonElement content = entry.GetProperty("response").GetProperty("content");
                Assert.That(content.TryGetProperty("text", out _), Is.False);
                Assert.That(content.TryGetProperty("_file", out JsonElement fileName), Is.True);

                string sidecar = Path.Combine(Path.GetDirectoryName(path), fileName.GetString().Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(sidecar), Is.True);
                Assert.That(File.ReadAllText(sidecar), Does.Contain("wave-102-har-marker"));
            }
            finally
            {
                TryDeleteAttached(path);
                TryDelete(path);
            }
        }

        private static void PrepareHarRoute()
        {
            Server.Reset();
            Server.SetRoute("/har.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>wave-102-har-marker</body></html>");
            });
        }

        private static string TempHarPath()
            => Path.Combine(Path.GetTempPath(), "pw-wave102-" + Guid.NewGuid().ToString("N") + ".har");

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
        }

        private static void TryDeleteAttached(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string dir = Path.GetDirectoryName(path);
            string folder = Path.GetFileNameWithoutExtension(path) + "-files";
            string attachDir = string.IsNullOrEmpty(dir) ? folder : Path.Combine(dir, folder);
            try
            {
                if (Directory.Exists(attachDir))
                {
                    Directory.Delete(attachDir, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }

        private static bool ContainsUrl(JsonElement entries, string fragment)
            => FindEntry(entries, fragment).ValueKind != JsonValueKind.Undefined;

        private static JsonElement FindEntry(JsonElement entries, string fragment)
        {
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string url = entry.GetProperty("request").GetProperty("url").GetString();
                if (url != null && url.Contains(fragment, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return default;
        }
    }
}
