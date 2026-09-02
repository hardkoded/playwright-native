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
    /// NewContext javaScriptEnabled and ignoreHTTPSErrors applied to pages.
    /// </summary>
    [TestFixture]
    public class ContextScriptHttpsTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-csp.spec.ts", "javaScriptEnabled false skips page scripts")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextJavaScriptDisabledShouldSkipPageScripts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { JavaScriptEnabled = false }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<script>window.__wave58 = 58;</script><div id=\"d\">ok</div>").ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<string>("typeof window.__wave58").ConfigureAwait(false), Is.EqualTo("undefined"));
            Assert.That(await page.EvaluateAsync<string>("document.getElementById('d').textContent").ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "options bag javaScriptEnabled")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldDisableJavaScript()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                JavaScriptEnabled = false,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<script>window.__wave58 = 58;</script>").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("typeof window.__wave58").ConfigureAwait(false), Is.EqualTo("undefined"));
        }

        [PlaywrightTest("browsercontext-csp.spec.ts", "ignoreHTTPSErrors allows self-signed")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextIgnoreHttpsErrorsShouldAllowSelfSigned()
        {
            if (TestServerSetup.HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync($"{TestConstants.HttpsPrefix}/empty.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Ok, Is.True);
        }
    }
}
