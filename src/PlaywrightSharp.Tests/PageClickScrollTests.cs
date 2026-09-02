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
    /// Official <c>scroll</c> option on <see cref="IPage.ClickAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageClickScrollTests : PageTestEx
    {
        [PlaywrightTest("page-click-scroll.spec.ts", "Click Auto scrolls the target into view")]
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
                  <button id='btn' style='width:80px;height:40px'>go</button>
                </div>
                <script>
                  document.getElementById('btn').addEventListener('click', () => {
                    document.getElementById('btn').textContent = 'clicked';
                  });
                </script>").ConfigureAwait(false);

            double before = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            await page.ClickAsync("#btn").ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('btn').textContent").ConfigureAwait(false);

            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.GreaterThan(0));
            Assert.That(text, Is.EqualTo("clicked"));
        }

        [PlaywrightTest("page-click-scroll.spec.ts", "Click scroll None does not scroll")]
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
                  <button id='btn' style='width:80px;height:40px'>go</button>
                </div>").ConfigureAwait(false);

            await page.ClickAsync("#btn", new() { Force = true, Scroll = ScrollMode.None }).ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            Assert.That(after, Is.EqualTo(0));
        }
    }
}
