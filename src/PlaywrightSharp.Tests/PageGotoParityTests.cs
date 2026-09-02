/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-goto.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android, BiDi-only):
    /// <c>should not leak listeners during navigation</c>,
    /// <c>should not leak listeners during bad navigation</c>,
    /// <c>should not leak listeners during 20 waitForNavigation</c>
    /// (Node <c>process.on('warning')</c> listener-leak checks).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageGotoParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _ownedHttps;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static int ServerPort = TestConstants.Port;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19770;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = origin + "/empty.html";
                    ServerPort = port;
                    await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }

            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
            HttpsServer?.Reset();
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }

            HttpsServer.Reset();
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                return;
            }

            string certPath = Path.Combine(contentRoot, "testCert.cer");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            try
            {
                SimpleServer https = SimpleServer.CreateHttps(TestConstants.HttpsPort, contentRoot);
                await https.StartAsync().ConfigureAwait(false);
                _ownedHttps = https;
            }
            catch (Exception)
            {
            }
        }

        private static void AssertSslError(string message)
        {
            if (TestConstants.IsChromium)
            {
                Assert.That(
                    message.Contains("net::ERR_CERT_AUTHORITY_INVALID", StringComparison.Ordinal)
                    || message.Contains("net::ERR_CERT_INVALID", StringComparison.Ordinal),
                    Is.True,
                    message);
                return;
            }

            if (TestConstants.IsWebKit)
            {
                if (TestConstants.IsMacOSX)
                {
                    Assert.That(message, Does.Contain("The certificate for this server is invalid"));
                    return;
                }

                if (TestConstants.IsWindows)
                {
                    Assert.That(message, Does.Contain("SSL peer certificate or SSH remote key was not OK"));
                    return;
                }

                Assert.That(
                    message.Contains("Unacceptable TLS certificate", StringComparison.Ordinal)
                    || message.Contains("Operation was cancelled", StringComparison.Ordinal),
                    Is.True,
                    message);
                return;
            }

            Assert.That(message, Does.Contain("SSL_ERROR_UNKNOWN"));
        }

        private static string FileUrl(string relativePath)
        {
            string path = TestUtils.GetWebServerFile(relativePath);
            return new Uri(path).AbsoluteUri;
        }

        private static void HangRoute(string path)
        {
            Server.SetRoute(path, _ => Task.Delay(Timeout.Infinite));
        }

        private static async Task ServeWwwFileAsync(HttpContext http, string relativePath)
        {
            string path = TestUtils.GetWebServerFile(relativePath);
            byte[] bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            string contentType = relativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                ? "text/html"
                : "application/octet-stream";
            http.Response.ContentType = contentType;
            await http.Response.Body.WriteAsync(bytes).ConfigureAwait(false);
        }

        private static void InstallLoadEventPage()
        {
            Server.SetRoute("/load-event/load-event.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(
                    "<html lang=\"en\"><head><meta charset=\"UTF-8\"><title>Load Event Test</title></head><body>" +
                    "<script>window.results=[];window.addEventListener('load',function(){window.results.push('load');});" +
                    "window.addEventListener('DOMContentLoaded',function(){window.results.push('DOMContentLoaded');});</script>" +
                    "<script type=\"module\" src=\"./module.js\"></script>" +
                    "<script>window.results.push('script tag after after module');</script>" +
                    "</body></html>").ConfigureAwait(false);
            });
            Server.SetRoute("/load-event/module.js", async http =>
            {
                http.Response.ContentType = "application/javascript";
                await http.Response.WriteAsync("import {foo} from '/slow.js';console.log('foo is', foo);window.results.push('module');").ConfigureAwait(false);
            });
        }

        private static void InstallWindowStopPage()
        {
            Server.SetRoute("/window-stop.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(
                    "<script type=\"module\" src=\"./module.js\"></script><script>setTimeout(() => window.stop(), 100);</script>").ConfigureAwait(false);
            });
        }

        private static async Task WaitUntilEventsEqualAsync(List<string> events, IReadOnlyList<string> expected, int timeoutMs = 5000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (events.Count == expected.Count)
                {
                    bool match = true;
                    for (int i = 0; i < expected.Count; i++)
                    {
                        if (!string.Equals(events[i], expected[i], StringComparison.Ordinal))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        return;
                    }
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(events, Is.EqualTo(expected));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work")]
        [PlaywrightTest("page-goto.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with file URL")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithFileUrl()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("No files on Android");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CHANNEL"), "webkit-wsl", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("separate filesystem on wsl");
            }

            string fileurl = FileUrl("empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(fileurl).ConfigureAwait(false);
            Assert.That(page.Url.ToLowerInvariant(), Is.EqualTo(fileurl.ToLowerInvariant()));
            Assert.That(new List<IFrame>(page.Frames).Count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-goto.spec.ts", "should navigate from file URL to about:blank")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNavigateFromFileUrlToAboutBlank()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("No files on Android");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CHANNEL"), "webkit-wsl", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("separate filesystem on wsl");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(FileUrl("empty.html")).ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with file URL with subframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithFileUrlWithSubframes()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("No files on Android");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CHANNEL"), "webkit-wsl", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("separate filesystem on wsl");
            }

            string fileurl = FileUrl(Path.Combine("frames", "two-frames.html"));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(fileurl).ConfigureAwait(false);
            Assert.That(page.Url.ToLowerInvariant(), Is.EqualTo(fileurl.ToLowerInvariant()));
            Assert.That(new List<IFrame>(page.Frames).Count, Is.EqualTo(3));
        }

        [PlaywrightTest("page-goto.spec.ts", "should use http for no protocol")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUseHttpForNoProtocol()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("isAndroid");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage.Substring("http://".Length)).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work cross-process")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkCrossProcess()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));

            string url = CrossProcessPrefix + "/empty.html";
            IFrame requestFrame = null;
            page.Request += (_, request) =>
            {
                if (request.Url == url)
                {
                    requestFrame = request.Frame;
                }
            };

            IResponse response = await page.GoToAsync(url).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(url));
            Assert.That(response.Frame, Is.SameAs(page.MainFrame));
            Assert.That(requestFrame, Is.SameAs(page.MainFrame));
            Assert.That(response.Url, Is.EqualTo(url));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with cross-process that fails before committing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCrossProcessThatFailsBeforeCommitting()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Abort();
                return Task.CompletedTask;
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response1 = await page.GoToAsync(CrossProcessPrefix + "/title.html").ConfigureAwait(false);
            await response1.FinishedAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync(EmptyPage));
            Assert.That(error, Is.Not.Null);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with Cross-Origin-Opener-Policy")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCrossOriginOpenerPolicy()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                await http.Response.WriteAsync(
                    "<div>Hello there!</div><script>window.onload = () => console.log('onload')</script>").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            HashSet<IRequest> requests = new HashSet<IRequest>();
            List<string> events = new List<string>();
            page.Request += (_, request) =>
            {
                events.Add("request");
                requests.Add(request);
            };
            page.RequestFailed += (_, request) =>
            {
                events.Add("requestfailed");
                requests.Add(request);
            };
            page.RequestFinished += (_, request) =>
            {
                events.Add("requestfinished");
                requests.Add(request);
            };
            page.Response += (_, response) =>
            {
                events.Add("response");
                requests.Add(response.Request);
            };

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
            await response.FinishedAsync().ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "request", "response", "requestfinished" }));
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(response.Request.Failure, Is.Null.Or.Empty);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with Cross-Origin-Opener-Policy and interception")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCrossOriginOpenerPolicyAndInterception()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                await http.Response.WriteAsync(
                    "<div>Hello there!</div><script>window.onload = () => console.log('onload')</script>").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            HashSet<IRequest> requests = new HashSet<IRequest>();
            List<string> events = new List<string>();
            page.Request += (_, request) =>
            {
                events.Add("request");
                requests.Add(request);
            };
            page.RequestFailed += (_, request) =>
            {
                events.Add("requestfailed");
                requests.Add(request);
            };
            page.RequestFinished += (_, request) =>
            {
                events.Add("requestfinished");
                requests.Add(request);
            };
            page.Response += (_, response) =>
            {
                events.Add("response");
                requests.Add(response.Request);
            };

            await page.RouteAsync("**/*", async route =>
            {
                await Task.Delay(100).ConfigureAwait(false);
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
            await response.FinishedAsync().ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "request", "response", "requestfinished" }));
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(response.Request.Failure, Is.Null.Or.Empty);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with Cross-Origin-Opener-Policy after redirect")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCrossOriginOpenerPolicyAfterRedirect()
        {
            EnsureServer();
            Server.SetRedirect("/redirect", "/empty.html");
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                await http.Response.WriteAsync(
                    "<div>Hello there!</div><script>window.onload = () => console.log('onload')</script>").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            HashSet<IRequest> requests = new HashSet<IRequest>();
            List<string> events = new List<string>();
            page.Request += (_, request) =>
            {
                events.Add("request");
                requests.Add(request);
            };
            page.RequestFailed += (_, request) =>
            {
                events.Add("requestfailed");
                requests.Add(request);
            };
            page.RequestFinished += (_, request) =>
            {
                events.Add("requestfinished");
                requests.Add(request);
            };
            page.Response += (_, response) =>
            {
                events.Add("response");
                requests.Add(response.Request);
            };

            IResponse response = await page.GoToAsync(Prefix + "/redirect").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
            await response.FinishedAsync().ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(new[] { "request", "response", "requestfinished", "request", "response", "requestfinished" }));
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(response.Request.Failure, Is.Null.Or.Empty);
            IRequest firstRequest = response.Request.RedirectedFrom;
            Assert.That(firstRequest, Is.Not.Null);
            Assert.That(firstRequest.Url, Is.EqualTo(Prefix + "/redirect"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should capture iframe navigation request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCaptureIframeNavigationRequest()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));

            IFrame requestFrame = null;
            page.Request += (_, request) =>
            {
                if (request.Url == Prefix + "/frames/frame.html")
                {
                    requestFrame = request.Frame;
                }
            };

            IResponse response = await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/frames/one-frame.html"));
            Assert.That(response.Frame, Is.SameAs(page.MainFrame));
            Assert.That(response.Url, Is.EqualTo(Prefix + "/frames/one-frame.html"));
            Assert.That(new List<IFrame>(page.Frames).Count, Is.EqualTo(2));
            Assert.That(requestFrame, Is.SameAs(new List<IFrame>(page.Frames)[1]));
        }

        [PlaywrightTest("page-goto.spec.ts", "should capture cross-process iframe navigation request")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCaptureCrossProcessIframeNavigationRequest()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));

            IFrame requestFrame = null;
            page.Request += (_, request) =>
            {
                if (request.Url == CrossProcessPrefix + "/frames/frame.html")
                {
                    requestFrame = request.Frame;
                }
            };

            IResponse response = await page.GoToAsync(CrossProcessPrefix + "/frames/one-frame.html").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(CrossProcessPrefix + "/frames/one-frame.html"));
            Assert.That(response.Frame, Is.SameAs(page.MainFrame));
            Assert.That(response.Url, Is.EqualTo(CrossProcessPrefix + "/frames/one-frame.html"));
            Assert.That(new List<IFrame>(page.Frames).Count, Is.EqualTo(2));
            Assert.That(requestFrame, Is.SameAs(new List<IFrame>(page.Frames)[1]));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with anchor navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithAnchorNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
            await page.GoToAsync(EmptyPage + "#foo").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage + "#foo"));
            await page.GoToAsync(EmptyPage + "#bar").ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(EmptyPage + "#bar"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with redirects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithRedirects()
        {
            EnsureServer();
            Server.SetRedirect("/redirect/1.html", "/redirect/2.html");
            Server.SetRedirect("/redirect/2.html", "/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/redirect/1.html").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(page.Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("page-goto.spec.ts", "should navigate to about:blank")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNavigateToAboutBlank()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync("about:blank").ConfigureAwait(false);
            Assert.That(response, Is.Null);
        }

        [PlaywrightTest("page-goto.spec.ts", "should return response when page changes its URL after load")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnResponseWhenPageChangesItsUrlAfterLoad()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/historyapi.html").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with subframes return 204")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithSubframesReturn204()
        {
            EnsureServer();
            Server.SetRoute("/frames/frame.html", http =>
            {
                http.Response.StatusCode = 204;
                return Task.CompletedTask;
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with subframes return 204 with domcontentloaded")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithSubframesReturn204WithDomcontentloaded()
        {
            EnsureServer();
            Server.SetRoute("/frames/frame.html", http =>
            {
                http.Response.StatusCode = 204;
                return Task.CompletedTask;
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html", WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when server returns 204")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenServerReturns204()
        {
            if (TestConstants.IsWebKit && !TestConstants.IsWindows && !TestConstants.IsMacOSX)
            {
                Assert.Ignore("Regressed in https://github.com/microsoft/playwright-browsers/pull/1297");
            }

            EnsureServer();
            Server.SetRoute("/empty.html", http =>
            {
                http.Response.StatusCode = 204;
                return Task.CompletedTask;
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync(EmptyPage));
            Assert.That(error, Is.Not.Null);
            if (TestConstants.IsChromium)
            {
                Assert.That(error.Message, Does.Contain("net::ERR_ABORTED"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(error.Message, Does.Contain("Aborted: 204 No Content"));
            }
            else
            {
                Assert.That(error.Message, Does.Contain("NS_BINDING_ABORTED"));
            }
        }

        [PlaywrightTest("page-goto.spec.ts", "should navigate to empty page with domcontentloaded")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNavigateToEmptyPageWithDomcontentloaded()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage, WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work when page calls history API in beforeunload")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenPageCallsHistoryApiInBeforeunload()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "(() => { window.addEventListener('beforeunload', () => history.replaceState(null, 'initial', window.location.href), false); })()").ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when navigating to bad url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenNavigatingToBadUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync("asdfasdf"));
            Assert.That(error, Is.Not.Null);
            if (TestConstants.IsChromium || TestConstants.IsWebKit)
            {
                Assert.That(error.Message, Does.Contain("Cannot navigate to invalid URL"));
            }
            else
            {
                Assert.That(error.Message, Does.Contain("Invalid url"));
            }
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when navigating to bad SSL")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenNavigatingToBadSsl()
        {
            EnsureHttps();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.Request += (_, request) => Assert.That(request, Is.Not.Null);
            page.RequestFinished += (_, request) => Assert.That(request, Is.Not.Null);
            page.RequestFailed += (_, request) => Assert.That(request, Is.Not.Null);
            Exception error = Assert.CatchAsync(() => page.GoToAsync(TestConstants.HttpsPrefix + "/empty.html"));
            Assert.That(error, Is.Not.Null);
            AssertSslError(error.Message);
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when navigating to bad SSL after redirects")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenNavigatingToBadSslAfterRedirects()
        {
            EnsureServer();
            EnsureHttps();
            Server.SetRedirect("/redirect/1.html", "/redirect/2.html");
            Server.SetRedirect("/redirect/2.html", "/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync(TestConstants.HttpsPrefix + "/redirect/1.html"));
            Assert.That(error, Is.Not.Null);
            AssertSslError(error.Message);
        }

        [PlaywrightTest("page-goto.spec.ts", "should not crash when navigating to bad SSL after a cross origin navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotCrashWhenNavigatingToBadSslAfterACrossOriginNavigation()
        {
            EnsureServer();
            EnsureHttps();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            try
            {
                await page.GoToAsync(TestConstants.HttpsPrefix + "/empty.html").ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("page-goto.spec.ts", "should not throw if networkidle0 is passed as an option")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowIfNetworkidle0IsPassedAsAnOption()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage, WaitUntilState.NetworkIdle).ConfigureAwait(false);
        }

        [PlaywrightTest("page-goto.spec.ts", "should throw if networkidle2 is passed as an option")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowIfNetworkidle2IsPassedAsAnOption()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync(EmptyPage, waitUntil: "networkidle2"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("waitUntil: expected one of (load|domcontentloaded|networkidle|commit)"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when main resources failed to load")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenMainResourcesFailedToLoad()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CHANNEL"), "webkit-wsl", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("Networking mode mirrored ends up stalling connections rather than terminating them, see https://github.com/microsoft/WSL/issues/10855.");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync("http://localhost:44123/non-existing-url"));
            Assert.That(error, Is.Not.Null);
            if (TestConstants.IsChromium)
            {
                Assert.That(error.Message, Does.Contain("net::ERR_CONNECTION_REFUSED"));
            }
            else if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.That(error.Message, Does.Contain("Could not connect to server"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(error.Message, Does.Contain("Could not connect"));
            }
            else
            {
                Assert.That(error.Message, Does.Contain("NS_ERROR_CONNECTION_REFUSED"));
            }
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when exceeding maximum navigation timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenExceedingMaximumNavigationTimeout()
        {
            EnsureServer();
            HangRoute("/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.GoToAsync(Prefix + "/empty.html", timeout: 1));
            Assert.That(error.Message, Does.Contain("page.goto: Timeout 1ms exceeded."));
            Assert.That(error.Message, Does.Contain(Prefix + "/empty.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when exceeding default maximum navigation timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenExceedingDefaultMaximumNavigationTimeout()
        {
            EnsureServer();
            HangRoute("/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.Context.SetDefaultNavigationTimeout(2);
            page.SetDefaultNavigationTimeout(1);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(() => page.GoToAsync(Prefix + "/empty.html"));
            Assert.That(error.Message, Does.Contain("page.goto: Timeout 1ms exceeded."));
            Assert.That(error.Message, Does.Contain(Prefix + "/empty.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when exceeding browser context navigation timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenExceedingBrowserContextNavigationTimeout()
        {
            EnsureServer();
            HangRoute("/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.Context.SetDefaultNavigationTimeout(2);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(() => page.GoToAsync(Prefix + "/empty.html"));
            Assert.That(error.Message, Does.Contain("page.goto: Timeout 2ms exceeded."));
            Assert.That(error.Message, Does.Contain(Prefix + "/empty.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when exceeding default maximum timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenExceedingDefaultMaximumTimeout()
        {
            EnsureServer();
            HangRoute("/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.Context.SetDefaultTimeout(2);
            page.SetDefaultTimeout(1);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(() => page.GoToAsync(Prefix + "/empty.html"));
            Assert.That(error.Message, Does.Contain("page.goto: Timeout 1ms exceeded."));
            Assert.That(error.Message, Does.Contain(Prefix + "/empty.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when exceeding browser context timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenExceedingBrowserContextTimeout()
        {
            EnsureServer();
            HangRoute("/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.Context.SetDefaultTimeout(2);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(() => page.GoToAsync(Prefix + "/empty.html"));
            Assert.That(error.Message, Does.Contain("page.goto: Timeout 2ms exceeded."));
            Assert.That(error.Message, Does.Contain(Prefix + "/empty.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should prioritize default navigation timeout over default timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPrioritizeDefaultNavigationTimeoutOverDefaultTimeout()
        {
            EnsureServer();
            HangRoute("/empty.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(0);
            page.SetDefaultNavigationTimeout(1);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(() => page.GoToAsync(Prefix + "/empty.html"));
            Assert.That(error.Message, Does.Contain("page.goto: Timeout 1ms exceeded."));
            Assert.That(error.Message, Does.Contain(Prefix + "/empty.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should disable timeout when its set to 0")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDisableTimeoutWhenItsSetTo0()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            bool loaded = false;
            page.Load += (_, _) => loaded = true;
            await page.GoToAsync(Prefix + "/grid.html", WaitUntilState.Load, timeout: 0).ConfigureAwait(false);
            Assert.That(loaded, Is.True);
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when replaced by another navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenReplacedByAnotherNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task anotherPromise = null;
            Server.SetRoute("/empty.html", _ =>
            {
                anotherPromise = page.GoToAsync(Prefix + "/one-style.html");
                return Task.Delay(Timeout.Infinite);
            });
            Exception error = Assert.CatchAsync(() => page.GoToAsync(Prefix + "/empty.html"));
            Assert.That(anotherPromise, Is.Not.Null);
            await anotherPromise.ConfigureAwait(false);
            if (TestConstants.IsChromium)
            {
                Assert.That(error.Message, Does.Contain("net::ERR_ABORTED"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(
                    error.Message,
                    Does.Contain("page.goto: Navigation to \"" + Prefix + "/empty.html\" is interrupted by another navigation to \"" + Prefix + "/one-style.html\""));
            }
            else
            {
                Assert.That(
                    error.Message.Contains("page.goto: Navigation to \"" + Prefix + "/empty.html\" is interrupted by another navigation to \"" + Prefix + "/one-style.html\"", StringComparison.Ordinal)
                    || error.Message.Contains("NS_BINDING_ABORTED", StringComparison.Ordinal),
                    Is.True);
            }
        }

        [PlaywrightTest("page-goto.spec.ts", "js redirect overrides url bar navigation ")]
        [Test]
        [Timeout(30_000)]
        public async Task JsRedirectOverridesUrlBarNavigation()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("TRACE"), "on", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("tracing waits for snapshot that never arrives because pending navigation");
            }

            EnsureServer();
            Server.SetRoute("/a", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(
                    "<body><script>setTimeout(() => { window.location.pathname = '/c'; }, 1000);</script></body>").ConfigureAwait(false);
            });
            List<string> events = new List<string>();
            Server.SetRoute("/b", async http =>
            {
                events.Add("started b");
                await Task.Delay(2000).ConfigureAwait(false);
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("BBB").ConfigureAwait(false);
                events.Add("finished b");
            });
            Server.SetRoute("/c", async http =>
            {
                events.Add("started c");
                await Task.Delay(2000).ConfigureAwait(false);
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("CCC").ConfigureAwait(false);
                events.Add("finished c");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/a").ConfigureAwait(false);
            Exception error = null;
            try
            {
                await page.GoToAsync(Prefix + "/b").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            string[] expectEvents = TestConstants.IsChromium
                ? new[] { "started b", "finished b" }
                : new[] { "started b", "started c", "finished b", "finished c" };
            await WaitUntilEventsEqualAsync(events, expectEvents).ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(expectEvents));
            if (TestConstants.IsChromium)
            {
                Assert.That(error, Is.Null);
                Assert.That(page.Url, Is.EqualTo(Prefix + "/b"));
            }
            else if (TestConstants.IsWebKit)
            {
                Assert.That(error, Is.Not.Null);
                Assert.That(
                    error.Message,
                    Does.Contain("page.goto: Navigation to \"" + Prefix + "/b\" is interrupted by another navigation to \"" + Prefix + "/c\""));
                Assert.That(page.Url, Is.EqualTo(Prefix + "/c"));
            }
            else
            {
                Assert.That(error.Message, Does.Contain("NS_BINDING_ABORTED"));
                Assert.That(page.Url, Is.EqualTo(Prefix + "/c"));
            }
        }

        [PlaywrightTest("page-goto.spec.ts", "should succeed on url bar navigation when there is pending navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSucceedOnUrlBarNavigationWhenThereIsPendingNavigation()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("PW_CLOCK"), "frozen", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("PW_CLOCK=frozen");
            }

            EnsureServer();
            Server.SetRoute("/a", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(
                    "<body><script>setTimeout(() => { window.location.pathname = '/c'; }, 10);</script></body>").ConfigureAwait(false);
            });
            List<string> events = new List<string>();
            Server.SetRoute("/b", async http =>
            {
                events.Add("started b");
                await Task.Delay(2000).ConfigureAwait(false);
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("BBB").ConfigureAwait(false);
                events.Add("finished b");
            });
            Server.SetRoute("/c", async http =>
            {
                events.Add("started c");
                await Task.Delay(2000).ConfigureAwait(false);
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("CCC").ConfigureAwait(false);
                events.Add("finished c");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/a").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Exception error = null;
            try
            {
                await page.GoToAsync(Prefix + "/b").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            string[] expectEvents = { "started c", "started b", "finished c", "finished b" };
            await WaitUntilEventsEqualAsync(events, expectEvents).ConfigureAwait(false);
            Assert.That(events, Is.EqualTo(expectEvents));
            Assert.That(error, Is.Null);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/b"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work when navigating to valid url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenNavigatingToValidUrl()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work when navigating to data url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenNavigatingToDataUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync("data:text/html,hello").ConfigureAwait(false);
            Assert.That(response, Is.Null);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work when navigating to 404")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenNavigatingTo404()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/not-found").ConfigureAwait(false);
            Assert.That(response.Ok, Is.False);
            Assert.That(response.Status, Is.EqualTo(404));
        }

        [PlaywrightTest("page-goto.spec.ts", "should return last response in redirect chain")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnLastResponseInRedirectChain()
        {
            EnsureServer();
            Server.SetRedirect("/redirect/1.html", "/redirect/2.html");
            Server.SetRedirect("/redirect/2.html", "/redirect/3.html");
            Server.SetRedirect("/redirect/3.html", EmptyPage);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/redirect/1.html").ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("page-goto.spec.ts", "should navigate to dataURL and not fire dataURL requests")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNavigateToDataUrlAndNotFireDataUrlRequests()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            string dataUrl = "data:text/html,<div>yo</div>";
            IResponse response = await page.GoToAsync(dataUrl).ConfigureAwait(false);
            Assert.That(response, Is.Null);
            Assert.That(requests.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("page-goto.spec.ts", "should navigate to URL with hash and fire requests without hash")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNavigateToUrlWithHashAndFireRequestsWithoutHash()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<IRequest> requests = new List<IRequest>();
            page.Request += (_, request) => requests.Add(request);
            IResponse response = await page.GoToAsync(EmptyPage + "#hash").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.Url, Is.EqualTo(EmptyPage));
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with self requesting page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithSelfRequestingPage()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/self-request.html").ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.Url, Does.Contain("self-request.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when navigating and show the url at the error message")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenNavigatingAndShowTheUrlAtTheErrorMessage()
        {
            EnsureHttps();
            string url = TestConstants.HttpsPrefix + "/redirect/1.html";
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync(url));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(url));
        }

        [PlaywrightTest("page-goto.spec.ts", "should be able to navigate to a page controlled by service worker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToNavigateToAPageControlledByServiceWorker()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['activationPromise'])()").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/serviceworkers/fetch/sw.html").ConfigureAwait(false);
        }

        [PlaywrightTest("page-goto.spec.ts", "should send referer")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendReferer()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<string> request1 = Server.WaitForRequest("/grid.html", r => r.Headers["referer"].ToString());
            Task<string> request2 = Server.WaitForRequest("/digits/1.png", r => r.Headers["referer"].ToString());
            await Task.WhenAll(
                request1,
                request2,
                page.GoToAsync(Prefix + "/grid.html", referer: "http://google.com/")).ConfigureAwait(false);
            Assert.That(request1.Result, Is.EqualTo("http://google.com/"));
            Assert.That(request2.Result, Is.EqualTo(Prefix + "/grid.html"));
            Assert.That(page.Url, Is.EqualTo(Prefix + "/grid.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should send referer of cross-origin URL")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSendRefererOfCrossOriginUrl()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<string> request1 = Server.WaitForRequest("/grid.html", r => r.Headers["referer"].ToString());
            Task<string> request2 = Server.WaitForRequest("/digits/1.png", r => r.Headers["referer"].ToString());
            await Task.WhenAll(
                request1,
                request2,
                page.GoToAsync(Prefix + "/grid.html", referer: "https://microsoft.com/xbox/")).ConfigureAwait(false);
            Assert.That(request1.Result, Is.EqualTo("https://microsoft.com/xbox/"));
            Assert.That(request2.Result, Is.EqualTo(Prefix + "/grid.html"));
            Assert.That(page.Url, Is.EqualTo(Prefix + "/grid.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should reject referer option when setExtraHTTPHeaders provides referer")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRejectRefererOptionWhenSetExtraHttpHeadersProvidesReferer()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["referer"] = "http://microsoft.com/",
            }).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(
                () => page.GoToAsync(Prefix + "/grid.html", referer: "http://google.com/"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("\"referer\" is already specified as extra HTTP header"));
            Assert.That(error.Message, Does.Contain(Prefix + "/grid.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should override referrer-policy")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldOverrideReferrerPolicy()
        {
            EnsureServer();
            Server.SetRoute("/grid.html", async http =>
            {
                http.Response.Headers["Referrer-Policy"] = "no-referrer";
                await ServeWwwFileAsync(http, "grid.html").ConfigureAwait(false);
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<string> request1 = Server.WaitForRequest("/grid.html", r => r.Headers["referer"].ToString());
            Task<string> request2 = Server.WaitForRequest("/digits/1.png", r => r.Headers["referer"].ToString());
            await Task.WhenAll(
                request1,
                request2,
                page.GoToAsync(Prefix + "/grid.html", referer: "http://microsoft.com/")).ConfigureAwait(false);
            Assert.That(request1.Result, Is.EqualTo("http://microsoft.com/"));
            Assert.That(string.IsNullOrEmpty(request2.Result), Is.True);
            Assert.That(page.Url, Is.EqualTo(Prefix + "/grid.html"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should fail when canceled by another navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailWhenCanceledByAnotherNavigation()
        {
            EnsureServer();
            HangRoute("/one-style.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IResponse> failed = page.GoToAsync(Prefix + "/one-style.html");
            await Server.WaitForRequest("/one-style.html").ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => failed);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Is.Not.Empty);
        }

        [PlaywrightTest("page-goto.spec.ts", "should work with lazy loading iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithLazyLoadingIframes()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("isAndroid");
            }

            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/lazy-frame.html").ConfigureAwait(false);
            Assert.That(new List<IFrame>(page.Frames).Count, Is.EqualTo(2));
        }

        [PlaywrightTest("page-goto.spec.ts", "should report raw buffer for main resource")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportRawBufferForMainResource()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Chromium sends main resource as text");
            }

            if (TestConstants.IsWebKit && TestConstants.IsWindows)
            {
                Assert.Ignore("Same here");
            }

            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.StatusCode = 200;
                byte[] body = Encoding.UTF8.GetBytes("Ü (lowercase ü)");
                await http.Response.Body.WriteAsync(body).ConfigureAwait(false);
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            byte[] actual = await response.BodyAsync().ConfigureAwait(false);
            Assert.That(Encoding.UTF8.GetString(actual), Is.EqualTo("Ü (lowercase ü)"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should not throw unhandled rejections on invalid url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowUnhandledRejectionsOnInvalidUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.GoToAsync("https://www.youtube Panel Title.com/"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.ToString(), Does.Contain("Panel Title"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should not crash when RTCPeerConnection is used")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotCrashWhenRtcPeerConnectionIsUsed()
        {
            EnsureServer();
            Server.SetRoute("/rtc.html", async http =>
            {
                await http.Response.WriteAsync(
                    "<!DOCTYPE html><html><body><script>window.RTCPeerConnection && new window.RTCPeerConnection({ iceServers: [] });</script></body></html>").ConfigureAwait(false);
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/rtc.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "(() => { window.RTCPeerConnection && new window.RTCPeerConnection({ iceServers: [] }); })()").ConfigureAwait(false);
        }

        [PlaywrightTest("page-goto.spec.ts", "should properly wait for load")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldProperlyWaitForLoad()
        {
            EnsureServer();
            InstallLoadEventPage();
            Server.SetRoute("/slow.js", async http =>
            {
                await Task.Delay(100).ConfigureAwait(false);
                http.Response.ContentType = "application/javascript";
                await http.Response.WriteAsync("window.results.push('slow module');export const foo = 'slow';").ConfigureAwait(false);
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/load-event/load-event.html").ConfigureAwait(false);
            string[] results = await page.EvaluateAsync<string[]>("window.results").ConfigureAwait(false);
            Assert.That(results, Is.EqualTo(new[]
            {
                "script tag after after module",
                "slow module",
                "module",
                "DOMContentLoaded",
                "load",
            }));
        }

        [PlaywrightTest("page-goto.spec.ts", "should not resolve goto upon window.stop()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotResolveGotoUponWindowStop()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("load/domcontentloaded events are flaky");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("PW_CLOCK"), "frozen", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("PW_CLOCK=frozen");
            }

            EnsureServer();
            InstallWindowStopPage();
            HttpResponse response = null;
            Server.SetRoute("/module.js", http =>
            {
                http.Response.ContentType = "text/javascript";
                response = http.Response;
                return Task.Delay(Timeout.Infinite);
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            bool done = false;
            _ = page.GoToAsync(Prefix + "/window-stop.html").ContinueWith(
                t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        done = true;
                    }

                    return t;
                },
                TaskScheduler.Default);
            await Server.WaitForRequest("/module.js").ConfigureAwait(false);
            Assert.That(done, Is.False);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            if (response != null)
            {
                await response.WriteAsync(string.Empty).ConfigureAwait(false);
            }

            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
        }

        [PlaywrightTest("page-goto.spec.ts", "should return from goto if new navigation is started")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnFromGotoIfNewNavigationIsStarted()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("isAndroid");
            }

            EnsureServer();
            InstallLoadEventPage();
            Server.SetRoute("/slow.js", _ => Task.Delay(Timeout.Infinite));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            bool finished = false;
            Task<IResponse> navigation = page.GoToAsync(Prefix + "/load-event/load-event.html").ContinueWith(
                t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        finished = true;
                    }

                    return t.GetAwaiter().GetResult();
                },
                TaskScheduler.Default);
            await Task.Delay(500).ConfigureAwait(false);
            Assert.That(finished, Is.False);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IResponse result = await navigation.ConfigureAwait(false);
            Assert.That(result.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("page-goto.spec.ts", "should return when navigation is committed if commit is specified")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnWhenNavigationIsCommittedIfCommitIsSpecified()
        {
            EnsureServer();
            HangRoute("/script.js");
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<title>Hello</title><script src=\"script.js\"></script>").ConfigureAwait(false);
            });
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(EmptyPage, WaitUntilState.Commit).ConfigureAwait(false);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Hello"));
        }

        [PlaywrightTest("page-goto.spec.ts", "should wait for load when iframe attaches and detaches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadWhenIframeAttachesAndDetaches()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("PW_CLOCK"), "frozen", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("PW_CLOCK=frozen");
            }

            EnsureServer();
            Server.SetRoute("/empty.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(
                    "<body><script>const iframe = document.createElement('iframe');iframe.src = './iframe.html';document.body.appendChild(iframe);setTimeout(() => iframe.remove(), 1000);</script></body>").ConfigureAwait(false);
            });
            Server.SetRoute("/iframe.html", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync("<link rel=\"stylesheet\" href=\"./style2.css\">").ConfigureAwait(false);
            });
            HangRoute("/style2.css");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IFrame> frameDetached = page.WaitForEventAsync(PageEvent.FrameDetached);
            Task<IResponse> done = page.GoToAsync(EmptyPage, WaitUntilState.Load);
            await frameDetached.ConfigureAwait(false);
            await done.ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("iframe").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-goto.spec.ts", "should return url with basic auth info")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnUrlWithBasicAuthInfo()
        {
            EnsureServer();
            string url = "http://admin:admin@localhost:" + ServerPort.ToString(CultureInfo.InvariantCulture) + "/empty.html";
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(url).ConfigureAwait(false);
            Assert.That(page.Url, Is.EqualTo(url));
        }
    }
}
