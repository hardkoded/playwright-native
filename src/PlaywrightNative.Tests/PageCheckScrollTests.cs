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
    /// Official <c>scroll</c> option on <see cref="IPage.CheckAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageCheckScrollTests : PageTestEx
    {
        [PlaywrightTest("page-check.spec.ts", "Check Auto scrolls the target into view")]
        [Test]
        [Timeout(30_000)]
        public async Task AutoShouldScrollTheTargetIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='box' style='width:240px;height:120px;overflow:auto;border:1px solid #000'>
                  <div style='height:600px'></div>
                  <input id='btn' type='checkbox'>
                </div>").ConfigureAwait(false);

            double before = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            await page.CheckAsync("#btn").ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("document.getElementById('btn').checked").ConfigureAwait(false);

            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.GreaterThan(0));
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "Check scroll None does not scroll")]
        [Test]
        [Timeout(30_000)]
        public async Task ScrollNoneShouldNotScrollThePage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='box' style='width:240px;height:120px;overflow:auto;border:1px solid #000'>
                  <div style='height:600px'></div>
                  <input id='btn' type='checkbox'>
                </div>").ConfigureAwait(false);

            try
            {
                await page.CheckAsync("#btn", new() { Force = true, Scroll = ScrollMode.None }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // The click may miss when the checkbox stays outside the overflow box.
            }

            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            Assert.That(after, Is.EqualTo(0));
        }
    }
}
