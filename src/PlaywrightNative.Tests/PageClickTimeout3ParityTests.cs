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
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-click-timeout-3.spec.ts</c> parity for hit-target
    /// click timeouts. Skipped (Node-only internals):
    /// <c>should fail when element jumps during hit testing</c> uses
    /// <c>__testHookBeforeHitTarget</c>.
    /// </summary>
    [TestFixture]
    public class PageClickTimeout3ParityTests : PageTestEx
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

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19786;
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

        [PlaywrightTest("page-click-timeout-3.spec.ts", "should timeout waiting for hit target")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutWaitingForHitTarget()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
    document.body.style.position = 'relative';
    const blocker = document.createElement('div');
    blocker.id = 'blocker';
    blocker.style.position = 'absolute';
    blocker.style.width = '400px';
    blocker.style.height = '20px';
    blocker.style.left = '0';
    blocker.style.top = '0';
    document.body.appendChild(blocker);
}").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => button.ClickAsync(new() { Timeout = 5000 }));
            Assert.That(error.Message, Does.Contain("elementHandle.click: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("<div id=\"blocker\"></div> intercepts pointer events"));
            Assert.That(error.Message, Does.Contain("retrying click action"));
            Assert.That(error.Message, Does.Contain("waiting 500ms"));
        }

        [PlaywrightTest("page-click-timeout-3.spec.ts", "should still click when force but hit target is obscured")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStillClickWhenForceButHitTargetIsObscured()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
    document.body.style.position = 'relative';
    const blocker = document.createElement('div');
    blocker.id = 'blocker';
    blocker.style.position = 'absolute';
    blocker.style.width = '400px';
    blocker.style.height = '200px';
    blocker.style.left = '0';
    blocker.style.top = '0';
    document.body.appendChild(blocker);
}").ConfigureAwait(false);
            await button.ClickAsync(new() { Force = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("page-click-timeout-3.spec.ts", "should report wrong hit target subtree")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportWrongHitTargetSubtree()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
    document.body.style.position = 'relative';

    const blocker = document.createElement('div');
    blocker.id = 'blocker';
    blocker.style.position = 'absolute';
    blocker.style.width = '400px';
    blocker.style.height = '20px';
    blocker.style.left = '0';
    blocker.style.top = '0';
    document.body.appendChild(blocker);

    const inner = document.createElement('div');
    inner.id = 'inner';
    inner.style.position = 'absolute';
    inner.style.left = '0';
    inner.style.top = '0';
    inner.style.right = '0';
    inner.style.bottom = '0';
    blocker.appendChild(inner);
}").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => button.ClickAsync(new() { Timeout = 5000 }));
            Assert.That(error.Message, Does.Contain("elementHandle.click: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("<div id=\"inner\"></div> from <div id=\"blocker\">…</div> subtree intercepts pointer events"));
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
