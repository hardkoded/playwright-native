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
    /// Official persistent-context <c>recordHarContent</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentRecordHarContentTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync RecordHarContent")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorRecordHarContent()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har-attach.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>wave-499-har-attach</body></html>");
            });

            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                executablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-har-content-" + Guid.NewGuid().ToString("N"));
            string harPath = Path.Combine(Path.GetTempPath(), "pwsharp-persist-har-content-" + Guid.NewGuid().ToString("N") + ".har");
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    RecordHarPath = harPath,
                    RecordHarContent = HarContentPolicy.Attach,
                }).ConfigureAwait(false);

                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-attach.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(harPath));
                JsonElement entries = document.RootElement.GetProperty("log").GetProperty("entries");
                JsonElement found = default;
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    string url = entry.GetProperty("request").GetProperty("url").GetString();
                    if (url != null && url.Contains("/har-attach.html", StringComparison.Ordinal))
                    {
                        found = entry;
                        break;
                    }
                }

                Assert.That(found.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
                JsonElement content = found.GetProperty("response").GetProperty("content");
                Assert.That(content.TryGetProperty("text", out _), Is.False);
                Assert.That(content.TryGetProperty("_file", out JsonElement fileName), Is.True);

                string sidecar = Path.Combine(Path.GetDirectoryName(harPath), fileName.GetString().Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(sidecar), Is.True);
                Assert.That(File.ReadAllText(sidecar), Does.Contain("wave-499-har-attach"));
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(harPath))
                    {
                        string dir = Path.GetDirectoryName(harPath);
                        string folder = Path.GetFileNameWithoutExtension(harPath) + "-files";
                        string attachDir = string.IsNullOrEmpty(dir) ? folder : Path.Combine(dir, folder);
                        if (Directory.Exists(attachDir))
                        {
                            Directory.Delete(attachDir, recursive: true);
                        }
                    }
                }
                catch (IOException)
                {
                }

                try
                {
                    if (File.Exists(harPath))
                    {
                        File.Delete(harPath);
                    }
                }
                catch (IOException)
                {
                }

                try
                {
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
