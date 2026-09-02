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
    /// Highlight and PressSequentially on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorHighlightTests : PageTestEx
    {
        [PlaywrightTest("locator-highlight.spec.ts", "Highlight marks the element")]
        [Test]
        [Timeout(30_000)]
        public async Task HighlightShouldMarkTheElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\">target</div>").ConfigureAwait(false);

            await page.Locator("#d").HighlightAsync().ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<string>("#d", "el => el.getAttribute('data-pw-highlight')").ConfigureAwait(false), Is.EqualTo("true"));
            string outline = await page.EvalOnSelectorAsync<string>("#d", "el => getComputedStyle(el).outlineColor").ConfigureAwait(false);
            Assert.That(outline, Does.Contain("255"));
        }

        [PlaywrightTest("locator-highlight.spec.ts", "PressSequentially types into an input")]
        [Test]
        [Timeout(30_000)]
        public async Task PressSequentiallyShouldTypeIntoAnInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);

            await page.Locator("#n").PressSequentiallyAsync("Ada").ConfigureAwait(false);

            Assert.That(await page.Locator("#n").InputValueAsync().ConfigureAwait(false), Is.EqualTo("Ada"));
        }

        [PlaywrightTest("locator-highlight.spec.ts", "Highlight is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task HighlightShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").HighlightAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("locator-highlight.spec.ts", "HideHighlight clears the mark")]
        [Test]
        [Timeout(30_000)]
        public async Task HideHighlightShouldClearTheMark()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\">target</div>").ConfigureAwait(false);

            ILocator locator = page.Locator("#d");
            await locator.HighlightAsync().ConfigureAwait(false);
            await locator.HideHighlightAsync().ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<string>("#d", "el => el.getAttribute('data-pw-highlight')").ConfigureAwait(false), Is.Null);
            string outline = await page.EvalOnSelectorAsync<string>("#d", "el => getComputedStyle(el).outlineStyle").ConfigureAwait(false);
            Assert.That(outline, Is.EqualTo("none"));
        }

        [PlaywrightTest("locator-highlight.spec.ts", "page HideHighlight clears every mark")]
        [Test]
        [Timeout(30_000)]
        public async Task PageHideHighlightShouldClearEveryMark()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"a\">one</div><div id=\"b\">two</div>").ConfigureAwait(false);

            await page.Locator("#a").HighlightAsync().ConfigureAwait(false);
            await page.Locator("#b").HighlightAsync().ConfigureAwait(false);
            await page.HideHighlightAsync().ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<string>("#a", "el => el.getAttribute('data-pw-highlight')").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("#b", "el => el.getAttribute('data-pw-highlight')").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("locator-highlight.spec.ts", "Highlight style applies extra CSS")]
        [Test]
        [Timeout(30_000)]
        public async Task HighlightShouldApplyExtraStyle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\">target</div>").ConfigureAwait(false);

            await page.Locator("#d").HighlightAsync(style: "outline: 3px solid rgb(0, 128, 0)").ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<string>("#d", "el => el.getAttribute('data-pw-highlight')").ConfigureAwait(false), Is.EqualTo("true"));
            string outline = await page.EvalOnSelectorAsync<string>("#d", "el => getComputedStyle(el).outlineColor").ConfigureAwait(false);
            Assert.That(outline, Does.Contain("128"));
        }
    }
}
