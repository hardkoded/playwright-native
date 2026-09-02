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
    /// Official <c>eval-on-selector-all.spec.ts</c> parity for
    /// <see cref="IPage.EvalOnSelectorAllAsync{T}"/>.
    /// Skipped: none (no Node-only internals, inspector, Electron, or Android).
    /// </summary>
    [TestFixture]
    public class EvalOnSelectorAllParityTests : PageTestEx
    {
        [PlaywrightTest("eval-on-selector-all.spec.ts", "should work with css selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCssSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><div>beautiful</div><div>world!</div>").ConfigureAwait(false);
            int divsCount = await page.EvalOnSelectorAllAsync<int>("css=div", "divs => divs.length").ConfigureAwait(false);
            Assert.That(divsCount, Is.EqualTo(3));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should work with text selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithTextSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><div>beautiful</div><div>beautiful</div><div>world!</div>").ConfigureAwait(false);
            int divsCount = await page.EvalOnSelectorAllAsync<int>("text=\"beautiful\"", "divs => divs.length").ConfigureAwait(false);
            Assert.That(divsCount, Is.EqualTo(2));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should work with xpath selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithXpathSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><div>beautiful</div><div>world!</div>").ConfigureAwait(false);
            int divsCount = await page.EvalOnSelectorAllAsync<int>("xpath=/html/body/div", "divs => divs.length").ConfigureAwait(false);
            Assert.That(divsCount, Is.EqualTo(3));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should auto-detect css selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectCssSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><div>beautiful</div><div>world!</div>").ConfigureAwait(false);
            int divsCount = await page.EvalOnSelectorAllAsync<int>("div", "divs => divs.length").ConfigureAwait(false);
            Assert.That(divsCount, Is.EqualTo(3));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should support >> syntax")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportDoubleGreaterThanSyntax()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><span>hello</span></div><div>beautiful</div><div><span>wo</span><span>rld!</span></div><span>Not this one</span>").ConfigureAwait(false);
            int spansCount = await page.EvalOnSelectorAllAsync<int>("css=div >> css=span", "spans => spans.length").ConfigureAwait(false);
            Assert.That(spansCount, Is.EqualTo(3));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should support * capture")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportStarCapture()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section><div><span>a</span></div></section><section><div><span>b</span></div></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*css=div >> \"b\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> *css=div >> \"b\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> *", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));

            await page.SetContentAsync("<section><div><span>a</span><span>a</span></div></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*css=div >> \"a\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> *css=div >> \"a\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));

            await page.SetContentAsync("<div><span>a</span></div><div><span>a</span></div><section><div><span>a</span></div></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*css=div >> \"a\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> *css=div >> \"a\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should support * capture when multiple paths match")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportStarCaptureWhenMultiplePathsMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><div><span></span></div></div><div></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*css=div >> span", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            await page.SetContentAsync("<div><div><span></span></div><span></span><span></span></div><div></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*css=div >> span", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should return complex values")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnComplexValues()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><div>beautiful</div><div>world!</div>").ConfigureAwait(false);
            string[] texts = await page.EvalOnSelectorAllAsync<string[]>("css=div", "divs => divs.map(div => div.textContent)").ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "hello", "beautiful", "world!" }));
        }

        [PlaywrightTest("eval-on-selector-all.spec.ts", "should work with bogus Array.from")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithBogusArrayFrom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><div>beautiful</div><div>world!</div>").ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => { Array.from = () => []; })()").ConfigureAwait(false);
            int divsCount = await page.EvalOnSelectorAllAsync<int>("css=div", "divs => divs.length").ConfigureAwait(false);
            Assert.That(divsCount, Is.EqualTo(3));
        }
    }
}
