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
using PlaywrightSharp.Chromium;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;
using PlaywrightSharp.WebKit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>artifactsDir</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchArtifactsDirTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsertype-launch.spec.ts", "launch ArtifactsDir receives accepted files")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchArtifactsDirShouldReceiveAcceptedFilesAndSurviveClose()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareDownloadRoutes();
            string artifactsDir = Path.Combine(Path.GetTempPath(), "pw-wave466-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(artifactsDir);
            try
            {
                await using (IBrowser browser = await LaunchWithArtifactsDirAsync(artifactsDir).ConfigureAwait(false))
                {
                    await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
                    Assert.That(DownloadsPathOf(context), Is.EqualTo(artifactsDir));

                    IPage page = await context.NewPageAsync().ConfigureAwait(false);
                    await page.GoToAsync($"{TestConstants.ServerUrl}/direct-download.html").ConfigureAwait(false);
                    await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);

                    string path = await WaitForDownloadFileAsync(artifactsDir, 10_000).ConfigureAwait(false);
                    Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
                }

                Assert.That(Directory.Exists(artifactsDir), Is.True);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(artifactsDir))
                    {
                        Directory.Delete(artifactsDir, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        private static Task<IBrowser> LaunchWithArtifactsDirAsync(string artifactsDir)
        {
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                return Playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath,
                    ArtifactsDir = artifactsDir,
                });
            }

            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox is not wired into launch-artifacts-dir tests yet.");
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            return Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath,
                ArtifactsDir = artifactsDir,
            });
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
