/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>eval-on-selector.spec.ts</c> parity for
    /// <see cref="IPage.EvalOnSelectorAsync{T}"/>.
    /// Skipped: none (no Node-only internals, inspector, Electron, or Android).
    /// </summary>
    [TestFixture]
    public class EvalOnSelectorParityTests : PageTestEx
    {
        [PlaywrightTest("eval-on-selector.spec.ts", "should work with css selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithCssSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("css=section", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with id selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithIdSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("id=testAttribute", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with data-test selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDataTestSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section data-test=foo id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("data-test=foo", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with data-testid selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDataTestidSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section data-testid=foo id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("data-testid=foo", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with data-test-id selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithDataTestIdSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section data-test-id=foo id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("data-test-id=foo", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with text selector in quotes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithTextSelectorInQuotes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("text=\"43543\"", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with xpath selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithXpathSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("xpath=/html/body/section", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with text selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithTextSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("text=43543", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should auto-detect css selector")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectCssSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("section", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should auto-detect css selector with attributes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectCssSelectorWithAttributes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("section[id=\"testAttribute\"]", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("testAttribute"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should auto-detect nested selectors")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDetectNestedSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div foo=bar><section>43543<span>Hello<div id=target></div></span></section></div>").ConfigureAwait(false);
            string idAttribute = await page.EvalOnSelectorAsync<string>("div[foo=bar] > section >> \"Hello\" >> div", "e => e.id").ConfigureAwait(false);
            Assert.That(idAttribute, Is.EqualTo("target"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should accept arguments")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptArguments()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>hello</section>").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("section", "(e, suffix) => e.textContent + suffix", " world!").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello world!"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should accept ElementHandles as arguments")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptElementHandlesAsArguments()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section>hello</section><div> world</div>").ConfigureAwait(false);
            IElementHandle divHandle = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("section", "(e, div) => e.textContent + div.textContent", divHandle).ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello world"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should throw error if no element is found")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowErrorIfNoElementIsFound()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.EvalOnSelectorAsync<string>("section", "e => e.id"));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("Failed to find element matching selector \"section\""));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should support >> syntax")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportDoubleGreaterThanSyntax()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section><div>hello</div></section>").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("css=section >> css=div", "(e, suffix) => e.textContent + suffix", " world!").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello world!"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should support >> syntax with different engines")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportDoubleGreaterThanSyntaxWithDifferentEngines()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section><div><span>hello</span></div></section>").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>("xpath=/html/body/section >> css=div >> text=\"hello\"", "(e, suffix) => e.textContent + suffix", " world!").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("hello world!"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should support spaces with >> syntax")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportSpacesWithDoubleGreaterThanSyntax()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/deep-shadow.html").ConfigureAwait(false);
            string text = await page.EvalOnSelectorAsync<string>(" css = div >>css=div>>css   = span  ", "e => e.textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("Hello from root2"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should not stop at first failure with >> syntax")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotStopAtFirstFailureWithDoubleGreaterThanSyntax()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><span>Next</span><button>Previous</button><button>Next</button></div>").ConfigureAwait(false);
            string html = await page.EvalOnSelectorAsync<string>("button >> \"Next\"", "e => e.outerHTML").ConfigureAwait(false);
            Assert.That(html, Is.EqualTo("<button>Next</button>"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should support * capture")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportStarCapture()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section><div><span>a</span></div></section><section><div><span>b</span></div></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("*css=div >> \"b\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div><span>b</span></div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("section >> *css=div >> \"b\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div><span>b</span></div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=div >> *text=\"b\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>b</span>"));
            Assert.That(await page.QuerySelectorAsync("*").ConfigureAwait(false), Is.Not.Null);
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should throw on multiple * captures")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnMultipleStarCaptures()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.EvalOnSelectorAsync<string>("*css=div >> *css=span", "e => e.outerHTML"));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("Only one of the selectors can capture using * modifier"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should throw on malformed * capture")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnMalformedStarCapture()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.EvalOnSelectorAsync<string>("*=div", "e => e.outerHTML"));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("Unknown engine \"\" while parsing selector *=div"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with spaces in css attributes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithSpacesInCssAttributes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><input placeholder=\"Select date\"></div>").ConfigureAwait(false);
            Assert.That(await page.WaitForSelectorAsync("[placeholder=\"Select date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.WaitForSelectorAsync("[placeholder='Select date']").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.WaitForSelectorAsync("input[placeholder=\"Select date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.WaitForSelectorAsync("input[placeholder='Select date']").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("[placeholder=\"Select date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("[placeholder='Select date']").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("input[placeholder=\"Select date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("input[placeholder='Select date']").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("[placeholder=\"Select date\"]", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("[placeholder='Select date']", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("input[placeholder=\"Select date\"]", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("input[placeholder='Select date']", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=[placeholder=\"Select date\"]", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=[placeholder='Select date']", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=input[placeholder=\"Select date\"]", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=input[placeholder='Select date']", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> [placeholder=\"Select date\"]", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> [placeholder='Select date']", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input placeholder=\"Select date\">"));
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with quotes in css attributes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithQuotesInCssAttributes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><input placeholder=\"Select&quot;date\"></div>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("[placeholder=\"Select\\\"date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("[placeholder='Select\"date']").ConfigureAwait(false), Is.Not.Null);
            await page.SetContentAsync("<div><input placeholder=\"Select &quot; date\"></div>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("[placeholder=\"Select \\\" date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("[placeholder='Select \" date']").ConfigureAwait(false), Is.Not.Null);
            await page.SetContentAsync("<div><input placeholder=\"Select&apos;date\"></div>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("[placeholder=\"Select'date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("[placeholder='Select\\'date']").ConfigureAwait(false), Is.Not.Null);
            await page.SetContentAsync("<div><input placeholder=\"Select &apos; date\"></div>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("[placeholder=\"Select ' date\"]").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await page.QuerySelectorAsync("[placeholder='Select \\' date']").ConfigureAwait(false), Is.Not.Null);
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with spaces in css attributes when missing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithSpacesInCssAttributesWhenMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IElementHandle> inputPromise = page.WaitForSelectorAsync("[placeholder=\"Select date\"]");
            Assert.That(await page.QuerySelectorAsync("[placeholder=\"Select date\"]").ConfigureAwait(false), Is.Null);
            await page.SetContentAsync("<div><input placeholder=\"Select date\"></div>").ConfigureAwait(false);
            await inputPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should work with quotes in css attributes when missing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithQuotesInCssAttributesWhenMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IElementHandle> inputPromise = page.WaitForSelectorAsync("[placeholder=\"Select\\\"date\"]");
            Assert.That(await page.QuerySelectorAsync("[placeholder=\"Select\\\"date\"]").ConfigureAwait(false), Is.Null);
            await page.SetContentAsync("<div><input placeholder=\"Select&quot;date\"></div>").ConfigureAwait(false);
            await inputPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("eval-on-selector.spec.ts", "should return complex values")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnComplexValues()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            Dictionary<string, string>[] idAttribute = await page.EvalOnSelectorAsync<Dictionary<string, string>[]>("css=section", "e => [{ id: e.id }]").ConfigureAwait(false);
            Assert.That(idAttribute, Has.Length.EqualTo(1));
            Assert.That(idAttribute[0]["id"], Is.EqualTo("testAttribute"));
        }
    }
}
