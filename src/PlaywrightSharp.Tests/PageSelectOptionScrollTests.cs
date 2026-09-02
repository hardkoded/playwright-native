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
    /// Official <c>scroll</c> option on <see cref="IPage.SelectOptionAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageSelectOptionScrollTests : PageTestEx
    {
        [PlaywrightTest("page-select-option.spec.ts", "SelectOption Auto scrolls the target into view")]
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
                  <select id='btn'>
                    <option value=''></option>
                    <option value='wave430'>wave430</option>
                  </select>
                </div>").ConfigureAwait(false);

            double before = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            await page.SelectOptionAsync("#btn", new[] { new SelectOptionValue { Value = "wave430" } }).ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.getElementById('btn').value").ConfigureAwait(false);

            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.GreaterThan(0));
            Assert.That(value, Is.EqualTo("wave430"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "SelectOption scroll None does not scroll")]
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
                  <select id='btn'>
                    <option value=''></option>
                    <option value='wave430'>wave430</option>
                  </select>
                </div>").ConfigureAwait(false);

            await page.SelectOptionAsync("#btn", new[] { new SelectOptionValue { Value = "wave430" } }, force: true, scroll: ActionScroll.None).ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.getElementById('btn').value").ConfigureAwait(false);
            Assert.That(after, Is.EqualTo(0));
            Assert.That(value, Is.EqualTo("wave430"));
        }
    }
}
