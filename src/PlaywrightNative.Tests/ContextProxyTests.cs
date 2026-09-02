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
    /// Direct-connection tests for context-level <see cref="Proxy"/>.
    /// </summary>
    [TestFixture]
    public class ContextProxyTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-proxy.spec.ts", "context proxy serves HTTP navigations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUseContextProxyForHttpNavigation()
        {
            await using LoopbackHttpProxy proxy = new LoopbackHttpProxy();
            await using IBrowser browser = await LaunchForProxyAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { Proxy = new Proxy { Server = "http://" + proxy.Server } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync("http://non-existent.invalid/from-proxy.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("proxied"));
            Assert.That(proxy.Targets, Has.Some.Contain("from-proxy.html"));
        }

        [PlaywrightTest("browsercontext-proxy.spec.ts", "context proxy bypass skips the proxy")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBypassProxyForListedHosts()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using LoopbackHttpProxy proxy = new LoopbackHttpProxy();
            await using IBrowser browser = await LaunchForProxyAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { Proxy = new Proxy { Server = "http://" + proxy.Server, Bypass = "localhost" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(page.Url, Does.Contain("empty.html"));
            Assert.That(proxy.Targets, Has.None.Contain("empty.html"));
            Assert.That(proxy.Targets, Has.None.Contain("localhost"));
        }

        private static Task<IBrowser> LaunchForProxyAsync()
        {
            // Both engines apply per-context proxies when the process was launched
            // with some proxy (a dummy server is enough).
            return BrowserLauncher.LaunchAsync(proxy: new Proxy { Server = "http://per-context" });
        }
    }
}
