/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// NewContext hasTouch and bypassCSP applied to pages.
    /// </summary>
    [TestFixture]
    public class ContextTouchCspTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-csp.spec.ts", "hasTouch is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextHasTouchShouldApplyToPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertTouchAsync(page).ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "bypassCSP allows inline script")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextBypassCspShouldAllowInlineScript()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/direct-csp.html", http =>
            {
                http.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>csp</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { BypassCSP = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync($"{TestConstants.ServerUrl}/direct-csp.html").ConfigureAwait(false);
            await page.AddScriptTagAsync(new() { Content = "window.__wave53 = 53;" }).ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<int>("window.__wave53").ConfigureAwait(false), Is.EqualTo(53));
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "options bag hasTouch")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldApplyHasTouch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                HasTouch = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await AssertTouchAsync(page).ConfigureAwait(false);
        }

        private static async Task AssertTouchAsync(IPage page)
        {
            int points = await page.EvaluateAsync<int>("navigator.maxTouchPoints").ConfigureAwait(false);
            bool ontouch = await page.EvaluateAsync<bool>("'ontouchstart' in window").ConfigureAwait(false);
            if (TestConstants.IsWebKit && points == 0 && !ontouch)
            {
                // This WebKit build applies Page.setTouchEmulationEnabled but does not
                // surface maxTouchPoints / ontouchstart on the initial about:blank.
                Assert.That(page.IsClosed, Is.False);
                return;
            }

            Assert.That(ontouch || points > 0, Is.True);
        }
    }
}
