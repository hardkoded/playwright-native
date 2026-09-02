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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserContext.on('download')</c>.
    /// </summary>
    [TestFixture]
    public class ContextDownloadEventTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("download.spec.ts", "WaitForDownloadAsync resolves on a page download")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextWaitForDownloadShouldResolve()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);

            IDownload download = await context.RunAndWaitForDownloadAsync(
                () => page.ClickAsync("#dl", new() { NoWaitAfter = true })).ConfigureAwait(false);

            Assert.That(download, Is.Not.Null);
            Assert.That(download.Page, Is.SameAs(page));
            Assert.That(download.Url, Does.Contain("direct-download.bin"));
        }

        private static void PrepareDownloadRoutes()
        {
            Server.Reset();
            Server.SetRoute("/direct-download.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body><a id=\"dl\" href=\"/direct-download.bin\">download</a></body></html>");
            });
            Server.SetRoute("/direct-download.bin", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=hello.txt";
                return http.Response.WriteAsync("Hello world");
            });
        }
    }
}
