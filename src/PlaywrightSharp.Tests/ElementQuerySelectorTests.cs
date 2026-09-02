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
    /// Direct-connection tests for <see cref="IElementHandle.QuerySelectorAsync"/> and
    /// <see cref="IElementHandle.QuerySelectorAllAsync"/>.
    /// </summary>
    [TestFixture]
    public class ElementQuerySelectorTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-query-selector.spec.ts", "QuerySelectorAsync returns the first descendant")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnFirstDescendant()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"root\"><span>a</span><span>b</span></div>").ConfigureAwait(false);

            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);
            IElementHandle first = await root.QuerySelectorAsync("span").ConfigureAwait(false);
            Assert.That(first, Is.Not.Null);
            Assert.That(await first.EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "QuerySelectorAllAsync returns descendants")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnDescendants()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"root\"><span>a</span><span>b</span></div>").ConfigureAwait(false);

            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> spans = await root.QuerySelectorAllAsync("span").ConfigureAwait(false);
            Assert.That(spans, Has.Exactly(2).Items);
            Assert.That(await spans[0].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("a"));
            Assert.That(await spans[1].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "scoped query does not see siblings")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotSeeSiblingsOutsideRoot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"outside\"><span>out</span></div><div id=\"root\"><span>in</span></div>").ConfigureAwait(false);

            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);
            IElementHandle outside = await root.QuerySelectorAsync("#outside").ConfigureAwait(false);
            Assert.That(outside, Is.Null);

            IReadOnlyList<IElementHandle> spans = await root.QuerySelectorAllAsync("span").ConfigureAwait(false);
            Assert.That(spans, Has.Exactly(1).Items);
            Assert.That(await spans[0].EvaluateAsync<string>("n => n.textContent").ConfigureAwait(false), Is.EqualTo("in"));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "empty match returns null or empty")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullOrEmptyWhenNothingMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"root\"><p>only</p></div>").ConfigureAwait(false);

            IElementHandle root = await page.QuerySelectorAsync("#root").ConfigureAwait(false);
            IElementHandle missing = await root.QuerySelectorAsync(".nope").ConfigureAwait(false);
            Assert.That(missing, Is.Null);

            IReadOnlyList<IElementHandle> none = await root.QuerySelectorAllAsync(".nope").ConfigureAwait(false);
            Assert.That(none, Is.Empty);
        }
    }
}
