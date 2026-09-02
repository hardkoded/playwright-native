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
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <see cref="IBrowserContext.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/>
    /// <c>updateMode</c> controls whether recorded HAR bodies are kept.
    /// </summary>
    [TestFixture]
    public class RouteFromHarUpdateModeTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-route.spec.ts", "updateMode Minimal omits response text")]
        [Test]
        [Timeout(30_000)]
        public async Task UpdateModeMinimalShouldOmitResponseText()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareRoute("wave-561-minimal");
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path, new() { Update = true, UpdateMode = HarMode.Minimal }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-update-mode.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                JsonElement content = FindContent(path, "/har-update-mode.html");
                Assert.That(content.TryGetProperty("text", out _), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("page-route.spec.ts", "updateMode Full embeds response text")]
        [Test]
        [Timeout(30_000)]
        public async Task UpdateModeFullShouldEmbedResponseText()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareRoute("wave-561-full");
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.RouteFromHARAsync(path, new() { Update = true, UpdateMode = HarMode.Full }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-update-mode.html").ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                JsonElement content = FindContent(path, "/har-update-mode.html");
                Assert.That(content.GetProperty("text").GetString(), Does.Contain("wave-561-full"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        private static void PrepareRoute(string marker)
        {
            Server.Reset();
            Server.SetRoute("/har-update-mode.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>" + marker + "</body></html>");
            });
        }

        private static string TempHarPath()
            => Path.Combine(Path.GetTempPath(), "pw-wave561-" + Guid.NewGuid().ToString("N") + ".har");

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
    }
}
