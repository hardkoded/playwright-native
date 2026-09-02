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
    /// Official <see cref="IBrowserContext.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/>
    /// <c>updateContent</c> embeds or attaches recorded bodies.
    /// </summary>
    [TestFixture]
    public class RouteFromHarUpdateContentTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-route.spec.ts", "updateContent Embed writes response text")]
        [Test]
        [Timeout(30_000)]
        public async Task UpdateContentEmbedShouldWriteResponseText()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareRoute("wave-562-embed");
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path, new() { Update = true, UpdateContent = RouteFromHarUpdateContentPolicy.Embed }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-update-content.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                JsonElement content = FindContent(path, "/har-update-content.html");
                Assert.That(content.GetProperty("text").GetString(), Does.Contain("wave-562-embed"));
                Assert.That(content.TryGetProperty("_file", out _), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("page-route.spec.ts", "updateContent Attach writes sidecar files")]
        [Test]
        [Timeout(30_000)]
        public async Task UpdateContentAttachShouldWriteSidecarFiles()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareRoute("wave-562-attach");
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.RouteFromHARAsync(path, new() { Update = true, UpdateContent = RouteFromHarUpdateContentPolicy.Attach }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-update-content.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                JsonElement content = FindContent(path, "/har-update-content.html");
                Assert.That(content.TryGetProperty("text", out _), Is.False);
                Assert.That(content.TryGetProperty("_file", out JsonElement fileName), Is.True);

                string sidecar = Path.Combine(Path.GetDirectoryName(path), fileName.GetString().Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(sidecar), Is.True);
                Assert.That(File.ReadAllText(sidecar), Does.Contain("wave-562-attach"));
            }
            finally
            {
                TryDeleteAttached(path);
                TryDelete(path);
            }
        }

        private static void PrepareRoute(string marker)
        {
            Server.Reset();
            Server.SetRoute("/har-update-content.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>" + marker + "</body></html>");
            });
        }

        private static string TempHarPath()
            => Path.Combine(Path.GetTempPath(), "pw-wave562-" + Guid.NewGuid().ToString("N") + ".har");

        private static JsonElement FindContent(string path, string fragment)
        {
            Assert.That(File.Exists(path), Is.True);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (JsonElement entry in document.RootElement.GetProperty("log").GetProperty("entries").EnumerateArray())
            {
                string url = entry.GetProperty("request").GetProperty("url").GetString();
                if (url != null && url.Contains(fragment, StringComparison.Ordinal))
                {
                    return entry.GetProperty("response").GetProperty("content").Clone();
                }
            }

            Assert.Fail("HAR has no entry for " + fragment);
            return default;
        }

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
    }
}
