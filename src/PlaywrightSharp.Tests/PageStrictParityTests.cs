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
    /// Official <c>page-strict.spec.ts</c> parity for strict-mode violation
    /// messages on page actions and locators.
    /// </summary>
    [TestFixture]
    public class PageStrictParityTests : PageTestEx
    {
        [PlaywrightTest("page-strict.spec.ts", "should fail page.textContent in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailPageTextContentInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span>span1</span><div><span>target</span></div>").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.TextContentAsync("span", new() { Strict = true }));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("1) <span>span1</span> aka getByText('span1')"));
            Assert.That(error.Message, Does.Contain("2) <span>target</span> aka getByText('target')"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should fail page.getAttribute in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailPageGetAttributeInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span>span1</span><div><span>target</span></div>").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.GetAttributeAsync("span", "id", new() { Strict = true }));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should fail page.fill in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailPageFillInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input></input><div><input></input></div>").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.FillAsync("input", "text", strict: true));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("1) <input/> aka getByRole('textbox').first()"));
            Assert.That(error.Message, Does.Contain("2) <input/> aka locator('div').getByRole('textbox')"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should fail page.$ in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailPageQuerySelectorInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span>span1</span><div><span>target</span></div>").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync("span", new() { Strict = true }));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should fail page.waitForSelector in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailPageWaitForSelectorInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span>span1</span><div><span>target</span></div>").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.WaitForSelectorAsync("span", new() { Strict = true }));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should fail page.dispatchEvent in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFailPageDispatchEventInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span></span><div><span></span></div>").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.DispatchEventAsync("span", "click", new object(), strict: true));

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("1) <span></span> aka locator('span').first()"));
            Assert.That(error.Message, Does.Contain("2) <span></span> aka locator('div span')"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should properly format :nth-child() in strict mode message")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldProperlyFormatNthChildInStrictModeMessage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
  <div>
    <div>
    </div>
    <div>
      <div>
      </div>
      <div>
      </div>
    </div>
  </div>
  <div>
    <div class='foo'>
    </div>
    <div class='foo'>
    </div>
  </div>
  ").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".foo").HoverAsync());

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("body > div:nth-child(2) > div:nth-child(2)"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should escape class names")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEscapeClassNames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
  <div>
    <div></div>
    <div>
      <div></div>
      <div></div>
    </div>
  </div>
  <div>
    <div class='foo bar:0'>
    </div>
    <div class='foo bar:1'>
    </div>
  </div>
  ").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".foo").HoverAsync());

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("<div class=\"foo bar:0"));
            Assert.That(error.Message, Does.Contain("<div class=\"foo bar:1"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should escape tag names")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEscapeTagNames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <q:template> </q:template>
    <span>special test description</span>
    <q:template hidden="""" aria-hidden=""true"">
      <span>special test description</span>
    </q:template>
  ").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Assertions.Expect(page.GetByText("special test description")).ToBeVisibleAsync());

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("getByText('special test description').first()"));
            Assert.That(error.Message, Does.Contain("locator('q\\\\:template').filter({ hasText: 'special test description' })"));
        }

        [PlaywrightTest("page-strict.spec.ts", "should keep the engine name for a \"text=..\" selector in strict mode")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldKeepTheEngineNameForATextSelectorInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>..loading</div><div>..loading</div>").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator("text=..loading").HoverAsync());

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("locator('text=..loading')"));
        }
    }
}
