/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for context <c>baseURL</c>.
    /// </summary>
    [TestFixture]
    public class ContextBaseUrlTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-base-url.spec.ts", "baseURL resolves a relative GoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task BaseUrlShouldResolveRelativeGoTo()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { BaseURL = TestConstants.ServerUrl }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("/empty.html").ConfigureAwait(false);
            Assert.That(page.Url, Does.Contain("/empty.html"));
        }
    }
}
