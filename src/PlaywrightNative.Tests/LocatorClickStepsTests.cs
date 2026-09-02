/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator.click({ steps })</c>.
    /// </summary>
    [TestFixture]
    public class LocatorClickStepsTests : PageTestEx
    {
        [PlaywrightTest("locator-click.spec.ts", "ClickAsync steps emits intermediate mousemove events")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickStepsShouldEmitIntermediateMouseMoves()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<div style=\"padding:80px 160px\">" +
                    "<button id=\"go\">Go</button></div>" +
                    "<script>window.moves=0;window.clicked=false;" +
                    "document.addEventListener('mousemove',()=>{window.moves++;});" +
                    "document.getElementById('go').addEventListener('click',()=>{window.clicked=true;});" +
                    "</script>")
                .ConfigureAwait(false);

            await page.Mouse.MoveAsync(2, 2).ConfigureAwait(false);
            await page.EvaluateAsync("window.moves=0;window.clicked=false").ConfigureAwait(false);
            await page.Locator("#go").ClickAsync(new LocatorClickOptions { Steps = 1 }).ConfigureAwait(false);
            int one = await page.EvaluateAsync<int>("window.moves").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false), Is.True);
            Assert.That(one, Is.GreaterThanOrEqualTo(1));

            await page.Mouse.MoveAsync(2, 2).ConfigureAwait(false);
            await page.EvaluateAsync("window.moves=0;window.clicked=false").ConfigureAwait(false);
            await page.Locator("#go").ClickAsync(new LocatorClickOptions { Steps = 8 }).ConfigureAwait(false);
            int many = await page.EvaluateAsync<int>("window.moves").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false), Is.True);
            Assert.That(many, Is.GreaterThan(one));
            Assert.That(many, Is.GreaterThanOrEqualTo(8));
        }

        [PlaywrightTest("locator-click.spec.ts", "ClickAsync default still clicks")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickDefaultShouldStillClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            await page.Locator("#go").ClickAsync().ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("go"));
        }
    }
}
