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
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official persistent-context <c>recordHarOmitContent</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentRecordHarOmitContentTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync RecordHarOmitContent")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorRecordHarOmitContent()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har-omit.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>wave-494-har-omit</body></html>");
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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-har-omit-" + Guid.NewGuid().ToString("N"));
            string harPath = Path.Combine(Path.GetTempPath(), "pwsharp-persist-har-omit-" + Guid.NewGuid().ToString("N") + ".har");
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    RecordHarPath = harPath,
                    RecordHarOmitContent = true,
                }).ConfigureAwait(false);

                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-omit.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                Assert.That(File.Exists(harPath), Is.True);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(harPath));
                JsonElement entries = document.RootElement.GetProperty("log").GetProperty("entries");
                JsonElement found = default;
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    string url = entry.GetProperty("request").GetProperty("url").GetString();
                    if (url != null && url.Contains("/har-omit.html", StringComparison.Ordinal))
                    {
                        found = entry;
                        break;
                    }
                }

                Assert.That(found.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
                JsonElement content = found.GetProperty("response").GetProperty("content");
                Assert.That(content.TryGetProperty("text", out _), Is.False);
            }
            finally
            {
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
