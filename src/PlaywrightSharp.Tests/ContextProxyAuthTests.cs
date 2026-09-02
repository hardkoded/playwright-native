/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for context proxy username/password.
    /// </summary>
    [TestFixture]
    public class ContextProxyAuthTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-proxy.spec.ts", "context proxy sends Basic credentials")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAuthenticateToHttpProxy()
        {
            await using LoopbackHttpProxy proxy = new LoopbackHttpProxy("user", "secret");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy { Server = "http://per-context" }).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "http://" + proxy.Server,
                    Username = "user",
                    Password = "secret",
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync("http://non-existent.invalid/authed.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("proxied"));
            Assert.That(proxy.Targets, Has.Some.Contain("authed.html"));
        }
    }
}
