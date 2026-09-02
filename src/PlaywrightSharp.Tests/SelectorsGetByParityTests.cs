/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>selectors-get-by.spec.ts</c> parity for getByTestId,
    /// getByText, getByLabel, getByPlaceholder, getByAltText, getByTitle,
    /// and getByRole (including description). Do not edit leftover
    /// <c>GetByTests.cs</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class SelectorsGetByParityTests : PageTestEx
    {
        [TearDown]
        public void ResetTestIdAttribute()
        {
            Playwright.SetTestIdAttribute("data-testid");
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTestId should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByTestIdShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><div data-testid=\"Hello\">Hello world</div></div>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTestId with custom testId should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByTestIdWithCustomTestIdShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><div data-my-custom-testid=\"Hello\">Hello world</div></div>").ConfigureAwait(false);
            Playwright.Selectors.SetTestIdAttribute("data-my-custom-testid");
            await Assertions.Expect(page.GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTestId with comma-separated testIdAttributes should match any")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByTestIdWithCommaSeparatedTestIdAttributesShouldMatchAny()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <section>
      <div data-pw=""Hello"">first</div>
      <div data-ti=""Hello"">second</div>
      <div data-testid=""Hello"">third</div>
    </section>
  ").ConfigureAwait(false);
            Playwright.Selectors.SetTestIdAttribute("data-pw,data-ti");
            await Assertions.Expect(page.GetByTestId("Hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("Hello")).ToHaveTextAsync(new[] { "first", "second" }).ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.GetByTestId("Hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("section").GetByTestId("Hello")).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTestId should escape id")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByTestIdShouldEscapeId()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><div data-testid='He\"llo'>Hello world</div></div>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("He\"llo")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTestId should work for regex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByTestIdShouldWorkForRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><div data-testid=\"Hello\">Hello world</div></div>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId(new Regex("He[l]*o"))).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").GetByTestId("Hello")).ToHaveTextAsync("Hello world").ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByText should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByTextShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>yo</div><div>ya</div><div>\nye  </div>").ConfigureAwait(false);
            Assert.That(await page.GetByText("ye").EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false), Does.Contain(">\nye  </div>"));
            Assert.That(await page.GetByText(new Regex("ye")).EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false), Does.Contain(">\nye  </div>"));
            Assert.That(await page.GetByText(new Regex("e")).EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false), Does.Contain(">\nye  </div>"));

            await page.SetContentAsync("<div> ye </div><div>ye</div>").ConfigureAwait(false);
            Assert.That(await page.GetByText("ye", exact: true).First.EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false), Does.Contain("> ye </div>"));

            await page.SetContentAsync("<div>Hello world</div><div>Hello</div>").ConfigureAwait(false);
            Assert.That(await page.GetByText("Hello", exact: true).EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false), Does.Contain(">Hello</div>"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><label for=target>Name</label><input id=target type=text></div>").ConfigureAwait(false);
            Assert.That(await page.GetByText("Name").EvaluateAsync<string>("e => e.nodeName").ConfigureAwait(false), Is.EqualTo("LABEL"));
            Assert.That(await page.GetByLabel("Name").EvaluateAsync<string>("e => e.nodeName").ConfigureAwait(false), Is.EqualTo("INPUT"));
            Assert.That(await page.MainFrame.GetByLabel("Name").EvaluateAsync<string>("e => e.nodeName").ConfigureAwait(false), Is.EqualTo("INPUT"));
            Assert.That(await page.Locator("div").GetByLabel("Name").EvaluateAsync<string>("e => e.nodeName").ConfigureAwait(false), Is.EqualTo("INPUT"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work with nested elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldWorkWithNestedElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label for=target>Last <span>Name</span></label><input id=target type=text>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("last name")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("st na")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Name")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Last Name", exact: true)).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel(new Regex(@"Last\s+name", RegexOptions.IgnoreCase))).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Last", exact: true).ElementHandlesAsync().ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByLabel("last name", exact: true).ElementHandlesAsync().ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByLabel("Name", exact: true).ElementHandlesAsync().ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByLabel("what?").ElementHandlesAsync().ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByLabel(new Regex("last name")).ElementHandlesAsync().ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work with multiply-labelled input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldWorkWithMultiplyLabelledInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label for=target>Name</label><input id=target type=text><label for=target>First or Last</label>").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Name").EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target"));
            Assert.That(await page.GetByLabel("First or Last").EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work with ancestor label and multiple controls")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldWorkWithAncestorLabelAndMultipleControls()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label>Name<button id=target>Click me</button><input type=text></label>").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Name").EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work with ancestor label and for")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldWorkWithAncestorLabelAndFor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <label for=target>Name<input type=text id=nontarget></label>
    <input type=text id=target>
  ").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Name").EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work with aria-labelledby")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldWorkWithAriaLabelledby()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label id=name-label>Name</label><button aria-labelledby=name-label>Click me</button>").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Name").EvaluateAsync<string>("e => e.textContent").ConfigureAwait(false), Is.EqualTo("Click me"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should prioritize aria-labelledby over native label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldPrioritizeAriaLabelledbyOverNativeLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <label id=name-label>Name</label>
    <label>Wrong<button aria-labelledby=name-label>Click me</button></label>
  ").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Name").EvaluateAsync<string>("e => e.textContent").ConfigureAwait(false), Is.EqualTo("Click me"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should work with aria-label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldWorkWithAriaLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=target aria-label=\"Name\">").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Name").EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should ignore empty aria-label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldIgnoreEmptyAriaLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label for=target>Last Name</label><input id=target type=text aria-label>").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Last Name").EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByLabel should prioritize aria-labelledby over aria-label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByLabelShouldPrioritizeAriaLabelledbyOverAriaLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label id=other-label>Other</label><input id=target aria-label=\"Name\" aria-labelledby=other-label>").ConfigureAwait(false);
            Assert.That(await page.GetByLabel("Other").EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByPlaceholder should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByPlaceholderShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>\n    <input placeholder='Hello'>\n    <input placeholder='Hello World'>\n  </div>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByPlaceholder("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.GetByPlaceholder("Hello", exact: true)).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.GetByPlaceholder(new Regex("wor", RegexOptions.IgnoreCase))).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.GetByPlaceholder("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").GetByPlaceholder("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByAltText should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByAltTextShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>\n    <input alt='Hello'>\n    <input alt='Hello World'>\n  </div>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByAltText("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.GetByAltText("Hello", exact: true)).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.GetByAltText(new Regex("wor", RegexOptions.IgnoreCase))).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.GetByAltText("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").GetByAltText("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByTitle should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByTitleShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>\n    <input title='Hello'>\n    <input title='Hello World'>\n  </div>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("Hello", exact: true)).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle(new Regex("wor", RegexOptions.IgnoreCase))).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.GetByTitle("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div").GetByTitle("hello")).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getBy escaping")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByEscaping()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label id=label for=control>Hello my\nwo\"rld</label><input id=control />").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", @"input => {
    input.setAttribute('placeholder', 'hello my\nwo""rld');
    input.setAttribute('title', 'hello my\nwo""rld');
    input.setAttribute('alt', 'hello my\nwo""rld');
  }").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("hello my\nwo\"rld")).ToHaveAttributeAsync("id", "label").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("hello       my     wo\"rld")).ToHaveAttributeAsync("id", "label").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("hello my\nwo\"rld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);
            await Assertions.Expect(page.GetByPlaceholder("hello my\nwo\"rld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);
            await Assertions.Expect(page.GetByAltText("hello my\nwo\"rld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("hello my\nwo\"rld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);

            await page.SetContentAsync("<label id=label for=control>Hello my\nworld</label><input id=control />").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", @"input => {
    input.setAttribute('placeholder', 'hello my\nworld');
    input.setAttribute('title', 'hello my\nworld');
    input.setAttribute('alt', 'hello my\nworld');
  }").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("hello my\nworld")).ToHaveAttributeAsync("id", "label").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("hello        my    world")).ToHaveAttributeAsync("id", "label").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("hello my\nworld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);
            await Assertions.Expect(page.GetByPlaceholder("hello my\nworld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);
            await Assertions.Expect(page.GetByAltText("hello my\nworld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("hello my\nworld")).ToHaveAttributeAsync("id", "control").ConfigureAwait(false);

            await page.SetContentAsync("<div id=target title=\"my title\">Text here</div>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("my title", exact: true)).ToHaveCountAsync(1, new() { Timeout = 500 }).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("my title", exact: true)).ToHaveCountAsync(1, new() { Timeout = 500 }).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("my t\\itle", exact: true)).ToHaveCountAsync(0, new() { Timeout = 500 }).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("my t\\itle", exact: true)).ToHaveCountAsync(0, new() { Timeout = 500 }).ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("my t\\\\itle", exact: true)).ToHaveCountAsync(0, new() { Timeout = 500 }).ConfigureAwait(false);

            await page.SetContentAsync("<label for=target>foo &gt;&gt; bar</label><input id=target>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", @"input => {
    input.setAttribute('placeholder', 'foo >> bar');
    input.setAttribute('title', 'foo >> bar');
    input.setAttribute('alt', 'foo >> bar');
  }").ConfigureAwait(false);
            Assert.That(await page.GetByText("foo >> bar").TextContentAsync().ConfigureAwait(false), Is.EqualTo("foo >> bar"));
            await Assertions.Expect(page.Locator("label")).ToHaveTextAsync("foo >> bar").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("foo >> bar")).ToHaveTextAsync("foo >> bar").ConfigureAwait(false);
            Assert.That(await page.GetByText(new Regex("foo >> bar")).TextContentAsync().ConfigureAwait(false), Is.EqualTo("foo >> bar"));
            await Assertions.Expect(page.GetByLabel("foo >> bar")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel(new Regex("foo >> bar"))).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByPlaceholder("foo >> bar")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByAltText("foo >> bar")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle("foo >> bar")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByPlaceholder(new Regex("foo >> bar"))).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByAltText(new Regex("foo >> bar"))).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
            await Assertions.Expect(page.GetByTitle(new Regex("foo >> bar"))).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole escaping")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByRoleEscaping()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <a href=""https://playwright.dev"">issues 123</a>
    <a href=""https://playwright.dev"">he llo 56</a>
    <button>Click me</button>
  ").ConfigureAwait(false);
            Assert.That(await page.GetByRole("button").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Click me</button>" }));
            Assert.That(await page.GetByRole("link").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<a href=\"https://playwright.dev\">issues 123</a>", "<a href=\"https://playwright.dev\">he llo 56</a>" }));
            Assert.That(await page.GetByRole("link", name: "issues 123").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<a href=\"https://playwright.dev\">issues 123</a>" }));
            Assert.That(await page.GetByRole("link", name: "sues").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<a href=\"https://playwright.dev\">issues 123</a>" }));
            Assert.That(await page.GetByRole("link", name: "  he    \n  llo ").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<a href=\"https://playwright.dev\">he llo 56</a>" }));
            Assert.That(await page.GetByRole("button", name: "issues").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByRole("link", name: "sues", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByRole("link", name: "   he \n llo 56 ", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<a href=\"https://playwright.dev\">he llo 56</a>" }));
            Assert.That(await page.GetByRole("button", name: "Click me", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Click me</button>" }));
            Assert.That(await page.GetByRole("button", name: "Click me", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Click me</button>" }));
            Assert.That(await page.GetByRole("button", name: "Click \\me", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByRole("button", name: "Click \\me", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByRole("button", name: "Click \\\\me", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole should accept regexp with v flag")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByRoleShouldAcceptRegexpWithVFlag()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button>Click me</button><button>Submit</button>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex("Click me"))).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex("click me", RegexOptions.IgnoreCase))).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button", nameRegex: new Regex("Missing"))).ToHaveCountAsync(0, new() { Timeout = 1000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole with description")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByRoleWithDescription()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div role=""alert"" aria-label=""Upload successful"" aria-description=""File doc-2025.pdf was uploaded successfully"">Alert 1</div>
    <div role=""alert"" aria-label=""Upload successful"" aria-description=""File report-2026.pdf was uploaded successfully"">Alert 2</div>
    <div role=""alert"" aria-label=""Invalid file"" aria-description=""File demo.doc has an invalid file format"">Alert 3</div>
  ").ConfigureAwait(false);
            Assert.That(await page.GetByRole("alert", description: "doc-2025").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert 1" }));
            Assert.That(await page.GetByRole("alert", description: "report-2026").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert 2" }));
            Assert.That(await page.GetByRole("alert", name: "Upload successful", description: "doc-2025").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert 1" }));
            Assert.That(await page.GetByRole("alert", name: "Upload successful", description: "report-2026").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert 2" }));
            Assert.That(await page.GetByRole("alert", name: "Invalid file", description: "doc-2025").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByRole("alert", description: "doc-2025", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByRole("alert", description: "File doc-2025.pdf was uploaded successfully", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert 1" }));
            Assert.That(await page.GetByRole("alert", descriptionRegex: new Regex(@"report-\d+")).EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert 2" }));
            Assert.That(await page.GetByRole("alert", descriptionRegex: new Regex("uploaded successfully$")).EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert 1", "Alert 2" }));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole with description via aria-describedby")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByRoleWithDescriptionViaAriaDescribedby()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <button aria-describedby=""desc1"">Submit</button>
    <span id=""desc1"">Submits the form data</span>
    <button aria-describedby=""desc2"">Submit</button>
    <span id=""desc2"">Saves as draft</span>
  ").ConfigureAwait(false);
            Assert.That(await page.GetByRole("button", name: "Submit", description: "form data").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Submit" }));
            Assert.That(await page.GetByRole("button", name: "Submit", description: "draft").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Submit" }));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole with description via title fallback")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByRoleWithDescriptionViaTitleFallback()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <button title=""Submits the form"">Submit</button>
    <button title=""Resets the form"">Reset</button>
  ").ConfigureAwait(false);
            Assert.That(await page.GetByRole("button", description: "Submits").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Submit" }));
            Assert.That(await page.GetByRole("button", description: "Resets").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Reset" }));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "getByRole with description whitespace normalization")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetByRoleWithDescriptionWhitespaceNormalization()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div role=""alert"" aria-description=""File  doc-2025.pdf   was uploaded   successfully"">Alert</div>
  ").ConfigureAwait(false);
            Assert.That(await page.GetByRole("alert", description: "  doc-2025.pdf \n was  uploaded ").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "Alert" }));
        }
    }
}
