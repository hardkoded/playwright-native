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
    /// Official <c>locator.dblclick({ steps })</c>.
    /// </summary>
    [TestFixture]
    public class LocatorDblClickStepsTests : PageTestEx
    {
        [PlaywrightTest("locator-click.spec.ts", "DblClickAsync steps emits intermediate mousemove events")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickStepsShouldEmitIntermediateMouseMoves()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<div style=\"padding:80px 160px\">" +
                    "<button id=\"go\">Go</button></div>" +
                    "<script>window.moves=0;window.dbl=false;" +
                    "document.addEventListener('mousemove',()=>{window.moves++;});" +
                    "document.getElementById('go').addEventListener('dblclick',()=>{window.dbl=true;});" +
                    "</script>")
                .ConfigureAwait(false);

            await page.Mouse.MoveAsync(2, 2).ConfigureAwait(false);
            await page.EvaluateAsync("window.moves=0;window.dbl=false").ConfigureAwait(false);
            await page.Locator("#go").DblClickAsync(new LocatorDblClickOptions { Steps = 1 }).ConfigureAwait(false);
            int one = await page.EvaluateAsync<int>("window.moves").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.dbl").ConfigureAwait(false), Is.True);
            Assert.That(one, Is.GreaterThanOrEqualTo(1));

            await page.Mouse.MoveAsync(2, 2).ConfigureAwait(false);
            await page.EvaluateAsync("window.moves=0;window.dbl=false").ConfigureAwait(false);
            await page.Locator("#go").DblClickAsync(new LocatorDblClickOptions { Steps = 8 }).ConfigureAwait(false);
            int many = await page.EvaluateAsync<int>("window.moves").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.dbl").ConfigureAwait(false), Is.True);
            Assert.That(many, Is.GreaterThan(one));
            Assert.That(many, Is.GreaterThanOrEqualTo(8));
        }

        [PlaywrightTest("locator-click.spec.ts", "DblClickAsync default still double-clicks")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickDefaultShouldStillDoubleClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<button id=\"go\">Go</button>" +
                    "<script>window.dbl=false;" +
                    "document.getElementById('go').addEventListener('dblclick',()=>{window.dbl=true;});" +
                    "</script>")
                .ConfigureAwait(false);

            await page.Locator("#go").DblClickAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.dbl").ConfigureAwait(false), Is.True);
        }
    }
}
