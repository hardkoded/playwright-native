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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserType.connectOverCDP()</c>.
    /// </summary>
    [TestFixture]
    public class ConnectOverCdpTests : PageTestEx
    {
        private static async Task<string> WaitForDevToolsEndpointAsync(string userData)
        {
            string portFile = Path.Combine(userData, "DevToolsActivePort");
            for (int i = 0; i < 150; i++)
            {
                if (File.Exists(portFile))
                {
                    string[] lines = await File.ReadAllLinesAsync(portFile).ConfigureAwait(false);
                    if (lines.Length > 0
                        && int.TryParse(lines[0], out int port)
                        && port > 0)
                    {
                        return "http://127.0.0.1:" + port;
                    }
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Chromium did not write DevToolsActivePort.");
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

        [PlaywrightTest("connect-over-cdp.spec.ts", "ConnectOverCDPAsync attaches to Chromium")]
        [Test]
        [Timeout(30_000)]
        public async Task ConnectOverCDPAsyncShouldAttachToChromium()
        {
            if (TestConstants.IsWebKit || TestConstants.IsFirefox)
            {
                Assert.Ignore("ConnectOverCDP is Chromium-only.");
                return;
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            string userData = Path.Combine(Path.GetTempPath(), "pwsharp-cdp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userData);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = BrowserExecutableFixture.ChromiumExecutablePath,
                Arguments = "--headless --no-sandbox --disable-gpu --remote-debugging-port=0 --user-data-dir=\"" + userData + "\" about:blank",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Process process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null);
            try
            {
                string endpoint = await WaitForDevToolsEndpointAsync(userData).ConfigureAwait(false);
                await using IBrowser browser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint).ConfigureAwait(false);
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("data:text/html,<html><body>wave461</body></html>").ConfigureAwait(false);
                string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(body, Does.Contain("wave461"));
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }

                process.Dispose();
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "ConnectOverCDPAsync honors headers")]
        [Test]
        [Timeout(30_000)]
        public async Task ConnectOverCDPAsyncShouldHonorHeaders()
        {
            if (TestConstants.IsWebKit || TestConstants.IsFirefox)
            {
                Assert.Ignore("ConnectOverCDP is Chromium-only.");
                return;
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            string userData = Path.Combine(Path.GetTempPath(), "pwsharp-cdp-h-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userData);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = BrowserExecutableFixture.ChromiumExecutablePath,
                Arguments = "--headless --no-sandbox --disable-gpu --remote-debugging-port=0 --user-data-dir=\"" + userData + "\" about:blank",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Process process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null);
            try
            {
                string endpoint = await WaitForDevToolsEndpointAsync(userData).ConfigureAwait(false);
                List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("X-Playwright-Wave", "679"),
                };
                await using IBrowser browser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint, new() { Headers = headers }).ConfigureAwait(false);
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync("data:text/html,<html><body>wave679</body></html>").ConfigureAwait(false);
                string body = await page.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                Assert.That(body, Does.Contain("wave679"));
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }

                process.Dispose();
            }
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "ConnectOverCDPAsync honors artifactsDir")]
        [Test]
        [Timeout(30_000)]
        public async Task ConnectOverCDPAsyncShouldHonorArtifactsDir()
        {
            if (TestConstants.IsWebKit || TestConstants.IsFirefox)
            {
                Assert.Ignore("ConnectOverCDP is Chromium-only.");
                return;
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            SimpleServer server = TestServerSetup.Server;
            if (server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            server.Reset();
            server.SetRoute("/direct-download.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body><a id=\"dl\" href=\"/direct-download.bin\">download</a></body></html>");
            });
            server.SetRoute("/direct-download.bin", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=hello.txt";
                return http.Response.WriteAsync("Hello world");
            });

            string userData = Path.Combine(Path.GetTempPath(), "pwsharp-cdp-a-" + Guid.NewGuid().ToString("N"));
            string artifactsDir = Path.Combine(Path.GetTempPath(), "pwsharp-cdp-art-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userData);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = BrowserExecutableFixture.ChromiumExecutablePath,
                Arguments = "--headless --no-sandbox --disable-gpu --remote-debugging-port=0 --user-data-dir=\"" + userData + "\" about:blank",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            Process process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null);
            try
            {
                string endpoint = await WaitForDevToolsEndpointAsync(userData).ConfigureAwait(false);
                await using IBrowser browser = await Playwright.Chromium.ConnectOverCDPAsync(endpoint, new() { ArtifactsDir = artifactsDir }).ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true }).ConfigureAwait(false);
                ChromiumBrowserContext chromium = context as ChromiumBrowserContext;
                Assert.That(chromium, Is.Not.Null);
                Assert.That(chromium.DownloadsPath, Is.EqualTo(artifactsDir));

                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/direct-download.html").ConfigureAwait(false);
                await page.ClickAsync("#dl", new() { NoWaitAfter = true }).ConfigureAwait(false);

                string path = await WaitForDownloadFileAsync(artifactsDir, 10_000).ConfigureAwait(false);
                Assert.That(path, Does.StartWith(artifactsDir));
                Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false), Is.EqualTo("Hello world"));
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }

                process.Dispose();
            }
        }
    }
}
