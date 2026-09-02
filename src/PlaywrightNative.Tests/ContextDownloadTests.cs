/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// NewContext acceptDownloads allow / deny applied via the browser download manager.
    /// </summary>
    [TestFixture]
    public class ContextDownloadTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("download.spec.ts", "acceptDownloads saves the file")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextAcceptDownloadsShouldSaveAttachment()
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
            await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);

            string path = await WaitForDownloadFileAsync(DownloadsPathOf(context), 10_000).ConfigureAwait(false);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
        }

        [PlaywrightTest("download.spec.ts", "options bag acceptDownloads")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldAcceptDownloads()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                AcceptDownloads = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);
            await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);

            string path = await WaitForDownloadFileAsync(DownloadsPathOf(context), 10_000).ConfigureAwait(false);
            Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
        }

        [PlaywrightTest("download.spec.ts", "denied downloads are not saved")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextShouldDenyDownloadsByDefault()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);
            await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);
            await page.WaitForTimeoutAsync(1_000).ConfigureAwait(false);

            string directory = DownloadsPathOf(context);
            Assert.That(Directory.Exists(directory) ? Directory.GetFiles(directory) : Array.Empty<string>(), Is.Empty);
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

        private static string DownloadsPathOf(IBrowserContext context)
        {
            if (context is ChromiumBrowserContext chromium)
            {
                return chromium.DownloadsPath;
            }

            if (context is WKBrowserContext webkit)
            {
                return webkit.DownloadsPath;
            }

            Assert.Fail("Unknown browser context type.");
            return null;
        }

        private static async Task<string> WaitForDownloadFileAsync(string directory, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    string[] files = Directory.GetFiles(directory);
                    foreach (string file in files)
                    {
                        if (new FileInfo(file).Length > 0)
                        {
                            return file;
                        }
                    }
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for an accepted download.");
        }
    }
}
