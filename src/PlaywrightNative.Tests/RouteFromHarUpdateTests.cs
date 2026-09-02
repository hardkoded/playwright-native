/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
    /// Official <see cref="IPage.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/> /
    /// <see cref="IBrowserContext.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/>
    /// <c>update: true</c> records live traffic into the HAR.
    /// </summary>
    [TestFixture]
    public class RouteFromHarUpdateTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-route.spec.ts", "context RouteFromHAR update writes the HAR")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextUpdateShouldWriteHarOnClose()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareRoute("from-network-context");
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path, new() { Update = true }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-update.html").ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(text, Does.Contain("from-network-context"));

                await context.CloseAsync().ConfigureAwait(false);
                AssertHarContains(path, "/har-update.html", "from-network-context");
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("page-route.spec.ts", "page RouteFromHAR update writes the HAR")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUpdateShouldWriteHarOnClose()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareRoute("from-network-page");
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.RouteFromHARAsync(path, new() { Update = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-update.html").ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(text, Does.Contain("from-network-page"));

                await context.CloseAsync().ConfigureAwait(false);
                AssertHarContains(path, "/har-update.html", "from-network-page");
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("page-route.spec.ts", "RouteFromHAR update overwrites an existing HAR")]
        [Test]
        [Timeout(30_000)]
        public async Task UpdateShouldOverwriteExistingHarAndHitTheNetwork()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareRoute("from-network-overwrite");
            string path = WriteStaleHar("from-old-har");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path, new Regex("har-update\\.html$"), update: true).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-update.html").ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(text, Does.Contain("from-network-overwrite"));
                Assert.That(text, Does.Not.Contain("from-old-har"));

                await context.CloseAsync().ConfigureAwait(false);
                AssertHarContains(path, "/har-update.html", "from-network-overwrite");
                string json = File.ReadAllText(path);
                Assert.That(json, Does.Not.Contain("from-old-har"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        private static void PrepareRoute(string marker)
        {
            Server.Reset();
            Server.SetRoute("/har-update.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>" + marker + "</body></html>");
            });
        }

        private static string TempHarPath()
            => Path.Combine(Path.GetTempPath(), "pw-wave560-" + Guid.NewGuid().ToString("N") + ".har");

        private static string WriteStaleHar(string marker)
        {
            string url = TestConstants.ServerUrl + "/har-update.html";
            string json =
                "{\"log\":{\"version\":\"1.2\",\"creator\":{\"name\":\"PlaywrightNative\",\"version\":\"1.0.0\"},\"entries\":[{" +
                "\"request\":{\"method\":\"GET\",\"url\":\"" + url + "\"}," +
                "\"response\":{\"status\":200,\"statusText\":\"OK\"," +
                "\"headers\":[{\"name\":\"content-type\",\"value\":\"text/html\"}]," +
                "\"content\":{\"mimeType\":\"text/html\",\"text\":\"<html><body>" + marker + "</body></html>\"}}}" +
                "]}}";
            string path = TempHarPath();
            File.WriteAllText(path, json);
            return path;
        }

        private static void AssertHarContains(string path, string fragment, string marker)
        {
            Assert.That(File.Exists(path), Is.True);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement entries = document.RootElement.GetProperty("log").GetProperty("entries");
            JsonElement entry = FindEntry(entries, fragment);
            Assert.That(entry.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
            string text = entry.GetProperty("response").GetProperty("content").GetProperty("text").GetString();
            Assert.That(text, Does.Contain(marker));
        }

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
    }
}
