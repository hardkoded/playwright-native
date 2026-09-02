/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.QuerySelectorAllAsync"/>.
    /// </summary>
    [TestFixture]
    public class QuerySelectorAllTests : PageTestEx
    {
        [PlaywrightTest("queryselector.spec.ts", "QuerySelectorAllAsync returns matching elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnMatchingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>a</div><div>b</div><span>c</span>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> divs = await page.QuerySelectorAllAsync("div").ConfigureAwait(false);
            Assert.That(divs, Has.Exactly(2).Items);
            Assert.That(await divs[0].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("a"));
            Assert.That(await divs[1].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("queryselector.spec.ts", "QuerySelectorAllAsync is empty when nothing matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyWhenNothingMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> missing = await page.QuerySelectorAllAsync(".nope").ConfigureAwait(false);
            Assert.That(missing, Is.Empty);
        }

        [PlaywrightTest("queryselector.spec.ts", "frame QuerySelectorAllAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryOnMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<li>1</li><li>2</li><li>3</li>").ConfigureAwait(false);

            IReadOnlyList<IElementHandle> items = await page.MainFrame.QuerySelectorAllAsync("li").ConfigureAwait(false);
            Assert.That(items, Has.Exactly(3).Items);
            Assert.That(await items[2].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("3"));
        }
    }
}
