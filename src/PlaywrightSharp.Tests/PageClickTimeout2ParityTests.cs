/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-click-timeout-2.spec.ts</c> parity for hidden-element
    /// click timeouts. Skipped: none.
    /// </summary>
    [TestFixture]
    public class PageClickTimeout2ParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19785;
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

        [PlaywrightTest("page-click-timeout-2.spec.ts", "should timeout waiting for display:none to be gone")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutWaitingForDisplayNoneToBeGone()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", "b => b.style.display = 'none'").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.ClickAsync("button", new() { Timeout = 5000 }));
            Assert.That(error.Message, Does.Contain("page.click: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("waiting for element to be visible, enabled and stable"));
            Assert.That(error.Message, Does.Contain("element is not visible"));
            Assert.That(error.Message, Does.Contain("retrying click action"));
        }

        [PlaywrightTest("page-click-timeout-2.spec.ts", "should timeout waiting for visibility:hidden to be gone")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutWaitingForVisibilityHiddenToBeGone()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", "b => b.style.visibility = 'hidden'").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.ClickAsync("button", new() { Timeout = 5000 }));
            Assert.That(error.Message, Does.Contain("page.click: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("waiting for element to be visible, enabled and stable"));
            Assert.That(error.Message, Does.Contain("element is not visible"));
            Assert.That(error.Message, Does.Contain("retrying click action"));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }
    }
}
