/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/favicon.spec.ts</c> parity. One title. Official
    /// skips headless Chromium/WebKit and headed WebKit.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryFaviconParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19875;
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

            if (TestServerSetup.Server != null)
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

            if (_browser != null)
            {
                try
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
#pragma warning disable RCS1075
                catch (Exception)
#pragma warning restore RCS1075
                {
                }
            }
        }

        [PlaywrightTest("favicon.spec.ts", "should load svg favicon with prefer-color-scheme")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLoadSvgFaviconWithPreferColorScheme()
        {
            if (!TestConstants.IsFirefox)
            {
                Assert.Ignore("official skip: headless browsers, except firefox, do not request favicons");
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            string favicon = "/favicon.svg?d=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            Server.SetRoute(favicon, async context =>
            {
                context.Response.ContentType = "image/svg+xml";
                await context.Response.WriteAsync(
                    "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 32 32\">" +
                    "<style>circle { fill: black; } @media (prefers-color-scheme: dark) { circle { fill: white; } }</style>" +
                    "<circle cx=\"16\" cy=\"16\" r=\"16\"/></svg>").ConfigureAwait(false);
            });
            Server.SetRoute("/page.html", async context =>
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(
                    "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
                    "<link rel=\"icon\" type=\"image/svg+xml\" href=\"" + favicon + "\">" +
                    "<title>SVG Favicon Test</title></head><body>favicons</body></html>").ConfigureAwait(false);
            });

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await (await _browser.NewContextAsync().ConfigureAwait(false)).NewPageAsync().ConfigureAwait(false);
            Task requestTask = Server.WaitForRequest(favicon);
            await Task.WhenAll(requestTask, page.GoToAsync(Prefix + "/page.html")).ConfigureAwait(false);
            await page.WaitForTimeoutAsync(500).ConfigureAwait(false);
            await page.WaitForSelectorAsync("text=favicons").ConfigureAwait(false);
        }
    }
}
