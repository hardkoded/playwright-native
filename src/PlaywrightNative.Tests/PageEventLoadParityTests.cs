/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-event-load.spec.ts</c> parity for <see cref="IPage.Load"/>.
    /// Skipped (Node-only internals): none.
    /// Firefox <c>it.fixme</c> ("Firefox sometimes double fires.") is honored via
    /// <see cref="Assert.Ignore(string)"/>. Chromium and WebKit run.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageEventLoadParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            // Prefer an exclusive server. This spec installs /home and /tracker
            // routes; sharing the campaign fixture races Server.Reset from
            // other Direct classes.
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19743;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        private static async Task<bool> FixtureReachableAsync(string prefix)
        {
            try
            {
                using HttpClient client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2),
                };
                HttpResponseMessage response = await client.GetAsync(prefix + "/empty.html").ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static void IgnoreFirefoxDoubleFire()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox sometimes double fires.");
            }
        }

        private static async Task<string> RaceLoadOrTimeoutAsync(IPage page)
        {
            Task<IPage> loadTask = page.WaitForEventAsync(PageEvent.Load);
            Task timeoutTask = page.WaitForTimeoutAsync(1000);
            Task completed = await Task.WhenAny(loadTask, timeoutTask).ConfigureAwait(false);
            if (completed == timeoutTask)
            {
                return "timeout";
            }

            await loadTask.ConfigureAwait(false);
            return "loadfired";
        }

        [PlaywrightTest("page-event-load.spec.ts", "should fire once")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireOnce()
        {
            IgnoreFirefoxDoubleFire();
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            int count = 0;
            page.Load += (_, _) => count++;
            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-event-load.spec.ts", "should fire once with iframe navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireOnceWithIframeNavigation()
        {
            IgnoreFirefoxDoubleFire();
            EnsureServer();
            Server.Reset();

            int requestCount = 0;
            Server.SetRoute("/tracker", async http =>
            {
                requestCount++;
                await http.Response.WriteAsync(
                    "request count: " + requestCount.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            });
            Server.SetRoute("/home", async http =>
            {
                http.Response.ContentType = "text/html";
                await http.Response.WriteAsync(@"
      <!DOCTYPE html>
      <html>
        <head>
        </head>
        <body>
          <script>
            window.eventLog = [];
            window.addEventListener('load', () => {
              window.eventLog.push('load');
            });
          </script>
          <form id=""trackerForm"" action=""/tracker"" method=""post"" target=""tracker"">
            <input type=""submit"">
          </form>
          <iframe name=""tracker"" src=""/tracker"">
        </body>
      </html>
    ").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            int count = 0;
            page.Load += (_, _) => count++;
            await page.GoToAsync(Prefix + "/home").ConfigureAwait(false);
            IFrameLocator trackingFrame = page.FrameLocator("[name=tracker]");
            await Assertions.Expect(trackingFrame.Locator(":scope")).ToContainTextAsync("request count: 1").ConfigureAwait(false);
            Task<string> loadFired = RaceLoadOrTimeoutAsync(page);
            await page.Locator("input[type=submit]").ClickAsync().ConfigureAwait(false);
            Assert.That(await loadFired.ConfigureAwait(false), Is.EqualTo("timeout"));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(
                await page.EvaluateAsync<string[]>("(() => window['eventLog'])()").ConfigureAwait(false),
                Is.EqualTo(new[] { "load" }));
        }
    }
}
