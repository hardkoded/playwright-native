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
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IBrowserContext.RouteFromHARAsync(string, string, HarNotFound, bool, HarMode, RouteFromHarUpdateContentPolicy)"/>.
    /// </summary>
    [TestFixture]
    public class RouteFromHarTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-route.spec.ts", "context RouteFromHAR fulfills from the file")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldFulfillFromHar()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har-play.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-network</body></html>");
            });

            string path = WriteHar("from-har-context");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-play.html").ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(text, Does.Contain("from-har-context"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("page-route.spec.ts", "page RouteFromHAR fulfills from the file")]
        [Test]
        [Timeout(30_000)]
        public async Task PageShouldFulfillFromHar()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har-play.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-network</body></html>");
            });

            string path = WriteHar("from-har-page");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.RouteFromHARAsync(path).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-play.html").ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(text, Does.Contain("from-har-page"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("page-route.spec.ts", "RouteFromHAR notFound fallback uses the network")]
        [Test]
        [Timeout(30_000)]
        public async Task NotFoundFallbackShouldUseTheNetwork()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har-miss.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-network</body></html>");
            });

            string path = WriteHar("from-har-unused");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path, new() { NotFound = HarNotFound.Fallback }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-miss.html").ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(text, Does.Contain("from-network"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("page-route.spec.ts", "RouteFromHAR url regex fulfills from the file")]
        [Test]
        [Timeout(30_000)]
        public async Task UrlRegexShouldFulfillFromHar()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/har-play.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>from-network</body></html>");
            });

            string path = WriteHar("from-har-regex");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.RouteFromHARAsync(path, new() { UrlRegex = new Regex("har-play\\.html$") }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har-play.html").ConfigureAwait(false);

                string text = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(text, Does.Contain("from-har-regex"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        private static string WriteHar(string marker)
        {
            string url = TestConstants.ServerUrl + "/har-play.html";
            string json =
                "{\"log\":{\"version\":\"1.2\",\"creator\":{\"name\":\"PlaywrightNative\",\"version\":\"1.0.0\"},\"entries\":[{" +
                "\"request\":{\"method\":\"GET\",\"url\":\"" + url + "\"}," +
                "\"response\":{\"status\":200,\"statusText\":\"OK\"," +
                "\"headers\":[{\"name\":\"content-type\",\"value\":\"text/html\"}]," +
                "\"content\":{\"mimeType\":\"text/html\",\"text\":\"<html><body>" + marker + "</body></html>\"}}}" +
                "]}}";
            string path = Path.Combine(Path.GetTempPath(), "pw-wave125-" + Path.GetRandomFileName() + ".har");
            File.WriteAllText(path, json);
            return path;
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
