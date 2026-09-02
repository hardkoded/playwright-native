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
    /// Official <c>queryselector.spec.ts</c> parity for
    /// <see cref="IPage.QuerySelectorAsync"/> and
    /// <see cref="IPage.QuerySelectorAllAsync"/>.
    /// Skipped: none (no Node-only internals, inspector, Electron, or Android).
    /// </summary>
    [TestFixture]
    public class QuerySelectorParityTests : PageTestEx
    {
        [PlaywrightTest("queryselector.spec.ts", "should throw for non-string selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForNonStringSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync(null));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("selector: expected string, got object"));
        }

        [PlaywrightTest("queryselector.spec.ts", "should query existing element with css selector")]
        [PlaywrightTest("queryselector.spec.ts", "should query existing element with css selector @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElementWithCssSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("css=section").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should query existing element with text selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElementWithTextSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("text=\"test\"").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should query existing element with xpath selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElementWithXpathSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("xpath=/html/body/section").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should return null for non-existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullForNonExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("non-existing-element").ConfigureAwait(false);
            Assert.That(element, Is.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should auto-detect xpath selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectXpathSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("//html/body/section").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should auto-detect xpath selector with starting parenthesis")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectXpathSelectorWithStartingParenthesis()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("(//section)[1]").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should auto-detect xpath selector starting with ..")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectXpathSelectorStartingWithDotDot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><section>test</section><span></span></div>").ConfigureAwait(false);
            IElementHandle span = await page.QuerySelectorAsync("\"test\" >> ../span").ConfigureAwait(false);
            Assert.That(await span.EvaluateAsync<string>("e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SPAN"));
            IElementHandle div = await page.QuerySelectorAsync("\"test\" >> ..").ConfigureAwait(false);
            Assert.That(await div.EvaluateAsync<string>("e => e.nodeName").ConfigureAwait(false), Is.EqualTo("DIV"));
        }

        [PlaywrightTest("queryselector.spec.ts", "should auto-detect text selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectTextSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("\"test\"").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should auto-detect css selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectCssSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("section").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should support >> syntax")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportDoubleGreaterThanSyntax()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section><div>test</div></section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("css=section >> css=div").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("queryselector.spec.ts", "should query existing elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>A</div><br/><div>B</div>").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> elements = await page.QuerySelectorAllAsync("div").ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(2));
            string text0 = await page.EvaluateAsync<string>("e => e.textContent", elements[0]).ConfigureAwait(false);
            string text1 = await page.EvaluateAsync<string>("e => e.textContent", elements[1]).ConfigureAwait(false);
            Assert.That(new[] { text0, text1 }, Is.EqualTo(new[] { "A", "B" }));
        }

        [PlaywrightTest("queryselector.spec.ts", "should return empty array if nothing is found")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyArrayIfNothingIsFound()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IReadOnlyList<IElementHandle> elements = await page.QuerySelectorAllAsync("div").ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("queryselector.spec.ts", "xpath should query existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task XpathShouldQueryExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> elements = await page.QuerySelectorAllAsync("xpath=/html/body/section").ConfigureAwait(false);
            Assert.That(elements[0], Is.Not.Null);
            Assert.That(elements.Count, Is.EqualTo(1));
        }

        [PlaywrightTest("queryselector.spec.ts", "xpath should return empty array for non-existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task XpathShouldReturnEmptyArrayForNonExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IReadOnlyList<IElementHandle> element = await page.QuerySelectorAllAsync("//html/body/non-existing-element").ConfigureAwait(false);
            Assert.That(element, Is.Empty);
        }

        [PlaywrightTest("queryselector.spec.ts", "xpath should return multiple elements")]
        [Test]
        [Timeout(30_000)]
        public async Task XpathShouldReturnMultipleElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div><div></div>").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> elements = await page.QuerySelectorAllAsync("xpath=/html/body/div").ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(2));
        }

        [PlaywrightTest("queryselector.spec.ts", "$$ should work with bogus Array.from")]
        [Test]
        [Timeout(30_000)]
        public async Task QuerySelectorAllShouldWorkWithBogusArrayFrom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><div></div>").ConfigureAwait(false);
            IJSHandle div1 = await page.EvaluateHandleAsync("() => { Array.from = () => []; return document.querySelector('div'); }").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> elements = await page.QuerySelectorAllAsync("div").ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(2));
            Assert.That(await elements[0].EvaluateAsync<bool>("(div, div1) => div === div1", div1).ConfigureAwait(false), Is.True);
        }
    }
}
