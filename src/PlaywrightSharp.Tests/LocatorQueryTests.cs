/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>locator-query.spec.ts</c>.
    /// Skipped: count() should not throw during navigation (Node-only custom
    /// selector engine that navigates during query).
    /// </summary>
    [TestFixture]
    public class LocatorQueryTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "should respect first() and last()")]
        [PlaywrightTest("locator-query.spec.ts", "should respect first() and last() @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectFirstAndLast()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
  <section>
    <div><p>A</p></div>
    <div><p>A</p><p>A</p></div>
    <div><p>A</p><p>A</p><p>A</p></div>
  </section>").ConfigureAwait(false);

            Assert.That(await page.Locator("div >> p").CountAsync().ConfigureAwait(false), Is.EqualTo(6));
            Assert.That(await page.Locator("div").Locator("p").CountAsync().ConfigureAwait(false), Is.EqualTo(6));
            Assert.That(await page.Locator("div").First.Locator("p").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.Locator("div").Last.Locator("p").CountAsync().ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("locator-query.spec.ts", "should respect nth()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectNth()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
  <section>
    <div><p>A</p></div>
    <div><p>A</p><p>A</p></div>
    <div><p>A</p><p>A</p><p>A</p></div>
  </section>").ConfigureAwait(false);

            Assert.That(await page.Locator("div >> p").Nth(0).CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.Locator("div").Nth(1).Locator("p").CountAsync().ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.Locator("div").Nth(2).Locator("p").CountAsync().ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("locator-query.spec.ts", "should throw on capture w/ nth()")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnCaptureWNth()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section><div><p>A</p></div></section>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator("*css=div >> p").Nth(1).ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("Can't query n-th element"));
        }

        [PlaywrightTest("locator-query.spec.ts", "should throw on due to strictness")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnDueToStrictness()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>A</div><div>B</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator("div").IsVisibleAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("locator-query.spec.ts", "should throw on due to strictness 2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnDueToStrictness2()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<select><option>One</option><option>Two</option></select>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator("option").EvaluateAsync<object>("e => {}"));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Foobar</div><div>Bar</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasText = "Foo" })).ToHaveTextAsync("Foobar").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by text 2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByText2()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>foo <span>hello world</span> bar</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasText = "hello world" })).ToHaveTextAsync("foo hello world bar").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Foobar</div><div>Bar</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("Foo.*") })).ToHaveTextAsync("Foobar").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by text with quotes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByTextWithQuotes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Hello \"world\"</div><div>Hello world</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasText = "Hello \"world\"" })).ToHaveTextAsync("Hello \"world\"").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by regex with quotes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByRegexWithQuotes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Hello \"world\"</div><div>Hello world</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("Hello \"world\"") })).ToHaveTextAsync("Hello \"world\"").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by regex with a single quote")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByRegexWithASingleQuote()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>let's let's<span>hello</span></button>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let's", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let's", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"\'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"\'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let['abc]s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let['abc]s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\\'s", RegexOptions.IgnoreCase) })).Not.ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\\'s", RegexOptions.IgnoreCase))).Not.ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let's let\'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let's let\'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\'s let's", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\'s let's", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);

            await page.SetContentAsync("<button>let\\'s let\\'s<span>hello</span></button>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\'s", RegexOptions.IgnoreCase) })).Not.ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\'s", RegexOptions.IgnoreCase))).Not.ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\\'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\\'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\\\'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\\\'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\\'s let\\\'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\\'s let\\\'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let\\\'s let\\'s", RegexOptions.IgnoreCase) }).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let\\\'s let\\'s", RegexOptions.IgnoreCase)).Locator("span")).ToHaveTextAsync("hello").ConfigureAwait(false);

            await page.SetContentAsync("<button>let's hello</button>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button", new() { HasTextRegex = new Regex(@"let's", RegexOptions.IgnoreCase) })).ToHaveTextAsync("let's hello").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex(@"let's", RegexOptions.IgnoreCase))).ToHaveTextAsync("let's hello").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by regex and regexp flags")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByRegexAndRegexpFlags()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Hello \"world\"</div><div>Hello world</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("hElLo \"world\"", RegexOptions.IgnoreCase) })).ToHaveTextAsync("Hello \"world\"").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by case-insensitive regex in a child")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByCaseInsensitiveRegexInAChild()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"test\"><h5>Title Text</h5></div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("^title text$", RegexOptions.IgnoreCase) })).ToHaveTextAsync("Title Text").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by case-insensitive regex in multiple children")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByCaseInsensitiveRegexInMultipleChildren()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"test\"><h5>Title</h5> <h2><i>Text</i></h2></div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("^title text$", RegexOptions.IgnoreCase) })).ToHaveClassAsync("test").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by regex with special symbols")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFilterByRegexWithSpecialSymbols()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"test\"><h5>First/\"and\"</h5><h2><i>Second\\</i></h2></div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("^first/\".*\"second\\\\$", RegexOptions.IgnoreCase | RegexOptions.Singleline) })).ToHaveClassAsync("test").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should support has:locator")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportHasLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><span>hello</span></div><div><span>world</span></div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div", new() { Has = page.Locator("text=world") })).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(
                await page.Locator("div", new() { Has = page.Locator("text=world") }).EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false),
                Is.EqualTo("<div><span>world</span></div>"));
            await Assertions.Expect(page.Locator("div", new() { Has = page.Locator("text=\"hello\"") })).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(
                await page.Locator("div", new() { Has = page.Locator("text=\"hello\"") }).EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false),
                Is.EqualTo("<div><span>hello</span></div>"));
            await Assertions.Expect(page.Locator("div", new() { Has = page.Locator("xpath=./span") })).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div", new() { Has = page.Locator("span") })).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div", new() { Has = page.Locator("span", new() { HasText = "wor" }) })).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(
                await page.Locator("div", new() { Has = page.Locator("span", new() { HasText = "wor" }) }).EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false),
                Is.EqualTo("<div><span>world</span></div>"));
            await Assertions.Expect(page.Locator("div", new() { Has = page.Locator("span"), HasText = "wor" })).ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should support locator.filter")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportLocatorFilter()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<section><div><span>hello</span></div><div><span>world</span></div></section>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div").Filter(new() { HasText = "hello" })).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div", new() { HasText = "hello" }).Filter(new() { HasText = "hello" })).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div", new() { HasText = "hello" }).Filter(new() { HasText = "world" })).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("section", new() { HasText = "hello" }).Filter(new() { HasText = "world" })).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { HasText = "hello" }).Locator("span")).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { Has = page.Locator("span", new() { HasText = "world" }) })).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { Has = page.Locator("span") })).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { Has = page.Locator("span"), HasText = "world" })).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { HasNot = page.Locator("span", new() { HasText = "world" }) })).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { HasNot = page.Locator("section") })).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { HasNot = page.Locator("span") })).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { HasNotText = "hello" })).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Filter(new() { HasNotText = "foo" })).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should support locator.and")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportLocatorAnd()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div data-testid=foo>hello</div><div data-testid=bar>world</div>
    <span data-testid=foo>hello2</span><span data-testid=bar>world2</span>
  ").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div").And(page.Locator("div"))).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").And(page.GetByTestId("foo"))).ToHaveTextAsync(new[] { "hello" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").And(page.GetByTestId("bar"))).ToHaveTextAsync(new[] { "world" }).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("foo").And(page.Locator("div"))).ToHaveTextAsync(new[] { "hello" }).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("bar").And(page.Locator("span"))).ToHaveTextAsync(new[] { "world2" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("span").And(page.GetByTestId(new Regex("bar|foo")))).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should support locator.or")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportLocatorOr()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>hello</div><span>world</span>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div").Or(page.Locator("span"))).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Or(page.Locator("span"))).ToHaveTextAsync(new[] { "hello", "world" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("span").Or(page.Locator("article")).Or(page.Locator("div"))).ToHaveTextAsync(new[] { "hello", "world" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("article").Or(page.Locator("something"))).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("article").Or(page.Locator("div"))).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("article").Or(page.Locator("span"))).ToHaveTextAsync("world").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Or(page.Locator("article"))).ToHaveTextAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("span").Or(page.Locator("article"))).ToHaveTextAsync("world").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should support locator.locator with and/or")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportLocatorLocatorWithAndOr()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div>one <span>two</span> <button>three</button> </div>
    <span>four</span>
    <button>five</button>
  ").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div").Locator(page.Locator("button"))).ToHaveTextAsync(new[] { "three" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Locator(page.Locator("button").Or(page.Locator("span")))).ToHaveTextAsync(new[] { "two", "three" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button").Or(page.Locator("span"))).ToHaveTextAsync(new[] { "two", "three", "four", "five" }).ConfigureAwait(false);

            await Assertions.Expect(page.Locator("div").Locator(page.Locator("button").And(page.GetByRole("button")))).ToHaveTextAsync(new[] { "three" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button").And(page.GetByRole("button"))).ToHaveTextAsync(new[] { "three", "five" }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should allow some, but not all nested frameLocators")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAllowSomeButNotAllNestedFrameLocators()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe srcdoc=\"<span id=target>world</span>\"></iframe><span>hello</span>").ConfigureAwait(false);

            await Assertions.Expect(page.FrameLocator("iframe").Locator("span").Or(page.FrameLocator("iframe").Locator("article"))).ToHaveTextAsync("world").ConfigureAwait(false);
            await Assertions.Expect(page.FrameLocator("iframe").Locator("article").Or(page.FrameLocator("iframe").Locator("span"))).ToHaveTextAsync("world").ConfigureAwait(false);
            await Assertions.Expect(page.FrameLocator("iframe").Locator("span").And(page.FrameLocator("iframe").Locator("#target"))).ToHaveTextAsync("world").ConfigureAwait(false);

            PlaywrightSharpException error1 = Assert.CatchAsync<PlaywrightSharpException>(
                () => Assertions.Expect(page.FrameLocator("iframe").Locator("div").Or(page.FrameLocator("#iframe").Locator("span"))).ToHaveTextAsync("world"));
            Assert.That(error1, Is.Not.Null);
            Assert.That(
                error1.Message,
                Does.Contain("Frame locators are not allowed inside composite locators, while querying \"locator('iframe').contentFrame().locator('div').or(locator('#iframe').contentFrame().locator('span'))"));

            PlaywrightSharpException error2 = Assert.CatchAsync<PlaywrightSharpException>(
                () => Assertions.Expect(page.FrameLocator("iframe").Locator("div").And(page.FrameLocator("#iframe").Locator("span"))).ToHaveTextAsync("world"));
            Assert.That(error2, Is.Not.Null);
            Assert.That(
                error2.Message,
                Does.Contain("Frame locators are not allowed inside composite locators, while querying \"locator('iframe').contentFrame().locator('div').and(locator('#iframe').contentFrame().locator('span'))"));
        }

        [PlaywrightTest("locator-query.spec.ts", "should keep the capture when removing a common frame prefix")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldKeepTheCaptureWhenRemovingACommonFramePrefix()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe id=f srcdoc=\"<section id=target><span>hi</span></section>\"></iframe>").ConfigureAwait(false);

            ILocator inner = page.FrameLocator("#f").Locator("*css=section >> span");
            await Assertions.Expect(page.FrameLocator("#f").Locator("body").Locator(inner)).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);

            ILocator captureFrame = page.Locator("*css=#f").ContentFrame.Locator("span");
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.FrameLocator("#f").Locator("body").Locator(captureFrame).CountAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Can not capture the selector before diving into the frame"));
        }

        [PlaywrightTest("locator-query.spec.ts", "should enforce same frame for has/leftOf/rightOf/above/below/near")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEnforceSameFrameForHasLeftOfRightOfAboveBelowNear()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/frames/two-frames.html").ConfigureAwait(false);

            IFrame child = null;
            foreach (IFrame frame in page.Frames)
            {
                if (frame.ParentFrame != null)
                {
                    child = frame;
                    break;
                }
            }

            Assert.That(child, Is.Not.Null);

            PlaywrightSharpException error = Assert.Throws<PlaywrightSharpException>(
                () => page.Locator("div", new() { Has = child.Locator("span") }));

            Assert.That(error.Message, Does.Contain("Inner \"has\" locator must belong to the same frame."));
        }

        [PlaywrightTest("locator-query.spec.ts", "alias methods coverage")]
        [Test]
        [Timeout(30_000)]
        public async Task AliasMethodsCoverage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>Submit</button></div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("button")).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").Locator("button")).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").GetByRole("button")).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.Locator("button")).ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "count() should not throw during navigation")]
        [Test]
        [Timeout(30_000)]
        public void CountShouldNotThrowDuringNavigation()
        {
            Assert.Ignore("Node-only: playwright.selectors.register with a query-time navigation helper.");
        }
    }
}
