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
    /// Official <c>page-navigation.spec.ts</c> parity for <c>_blank</c> clicks
    /// and cross-origin POST redirects. Skipped: none.
    /// </summary>
    [TestFixture]
    public class PageNavigationParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19792;
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
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [SetUp]
        public void ResetOwnedRoutes()
        {
            _ownedServer?.Reset();
        }

        [PlaywrightTest("page-navigation.spec.ts", "should work with _blank target")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBlankTarget()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http => WriteHtmlAsync(
                http,
                "<a href=\"" + EmptyPage + "\" target=\"_blank\">Click me</a>"));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.ClickAsync("\"Click me\"").ConfigureAwait(false);
        }

        [PlaywrightTest("page-navigation.spec.ts", "should work with cross-process _blank target")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCrossProcessBlankTarget()
        {
            EnsureServer();
            Server.SetRoute("/empty.html", http => WriteHtmlAsync(
                http,
                "<a href=\"" + CrossProcessPrefix + "/empty.html\" target=\"_blank\">Click me</a>"));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.ClickAsync("\"Click me\"").ConfigureAwait(false);
        }

        [PlaywrightTest("page-navigation.spec.ts", "should work with _blank target in form")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBlankTargetInForm()
        {
            EnsureServer();
            Server.SetRoute("/done.html?", http => WriteHtmlAsync(http, "Done"));
            Server.SetRoute("/done.html", http => WriteHtmlAsync(http, "Done"));
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            await page.SetContentAsync(
                "<form target=\"_blank\" action=\"done.html\" >" +
                "<input type=\"submit\" value=\"Click me\">" +
                "</form>").ConfigureAwait(false);
            await Task.WhenAll(
                page.WaitForEventAsync(PageEvent.Popup),
                page.ClickAsync("\"Click me\"")).ConfigureAwait(false);

            await page.SetContentAsync(
                "<form target=\"_blank\" action=\"done.html\" method=\"post\">" +
                "<input type=\"submit\" value=\"Click me\">" +
                "</form>").ConfigureAwait(false);
            await Task.WhenAll(
                page.WaitForEventAsync(PageEvent.Popup),
                page.ClickAsync("\"Click me\"")).ConfigureAwait(false);
        }

        [PlaywrightTest("page-navigation.spec.ts", "should not throw TargetClosedException on cross-origin redirect after click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowTargetClosedExceptionOnCrossOriginRedirectAfterClick()
        {
            EnsureServer();
            Server.SetRoute("/target.html", http => WriteHtmlAsync(http, "<title>final page</title>"));
            Server.SetRedirect("/redirect", CrossProcessPrefix + "/target.html");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync(
                "<form action=\"" + Prefix + "/redirect\" method=\"POST\">" +
                "<button type=\"submit\">Submit</button>" +
                "</form>").ConfigureAwait(false);
            await Task.WhenAll(
                page.WaitForNavigationAsync(),
                page.ClickAsync("button")).ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(CrossProcessPrefix + "/target.html").ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveTitleAsync("final page").ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task WriteHtmlAsync(HttpContext http, string html)
        {
            http.Response.StatusCode = 200;
            http.Response.ContentType = "text/html";
            await http.Response.WriteAsync(html).ConfigureAwait(false);
        }
    }
}
