/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>selectors-frame.spec.ts</c> parity for
    /// <c>internal:control=enter-frame</c> and
    /// <c>internal:control=pierce-frames</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class SelectorsFrameParityTests : PageTestEx
    {
        private static readonly string Prefix = TestConstants.ServerUrl.TrimEnd('/');
        private static readonly string EmptyPage = TestConstants.EmptyPage;

        private static async Task RouteIframeAsync(IPage page)
        {
            await page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { Body = "<iframe src=\"iframe.html\"></iframe>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/iframe.html", route => route.FulfillAsync(new() { Body = @"
        <html>
          <div>
            <button>Hello iframe</button>
            <iframe src=""iframe-2.html""></iframe>
          </div>
          <span>1</span>
          <span>2</span>
        </html>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/iframe-2.html", route => route.FulfillAsync(new() { Body = "<html><button tag=\"iframe2\">Hello nested iframe</button></html>", ContentType = "text/html" })).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for iframe @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("iframe >> internal:control=enter-frame >> button");
            await button.WaitForAsync().ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            await Assertions.Expect(button).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for iframe (handle)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForIframeHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            IElementHandle button = await body.WaitForSelectorAsync("iframe >> internal:control=enter-frame >> button").ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for nested iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForNestedIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("iframe >> internal:control=enter-frame >> iframe >> internal:control=enter-frame >> button");
            await button.WaitForAsync().ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
            await Assertions.Expect(button).ToHaveTextAsync("Hello nested iframe").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for nested iframe (handle)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForNestedIframeHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            IElementHandle button = await body.WaitForSelectorAsync("iframe >> internal:control=enter-frame >> iframe >> internal:control=enter-frame >> button").ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
            Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for $ and $$")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForDollarAndDollarDollar()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("iframe >> internal:control=enter-frame >> button").ConfigureAwait(false);
            Assert.That(await element.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            IReadOnlyList<IElementHandle> elements = await page.QuerySelectorAllAsync("iframe >> internal:control=enter-frame >> span").ConfigureAwait(false);
            Assert.That(elements, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "$ should not wait for frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DollarShouldNotWaitForFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("iframe >> internal:control=enter-frame >> canvas").ConfigureAwait(false), Is.Null);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            Assert.That(await body.QuerySelectorAsync("iframe >> internal:control=enter-frame >> canvas").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "$$ should not wait for frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DollarDollarShouldNotWaitForFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAllAsync("iframe >> internal:control=enter-frame >> canvas").ConfigureAwait(false), Has.Count.EqualTo(0));
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            Assert.That(await body.QuerySelectorAllAsync("iframe >> internal:control=enter-frame >> canvas").ConfigureAwait(false), Has.Count.EqualTo(0));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "$eval should throw for missing frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DollarEvalShouldThrowForMissingFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightNativeException pageError = Assert.CatchAsync<PlaywrightNativeException>(() => page.EvalOnSelectorAsync<int>("iframe >> internal:control=enter-frame >> canvas", "e => 1"));
            Assert.That(pageError.Message, Does.Contain("page.$eval: Failed to find element matching selector"));
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            PlaywrightNativeException handleError = Assert.CatchAsync<PlaywrightNativeException>(() => body.EvalOnSelectorAsync<int>("iframe >> internal:control=enter-frame >> canvas", "e => 1"));
            Assert.That(handleError.Message, Does.Contain("elementHandle.$eval: Failed to find element matching selector"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "$$eval should not throw for missing frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DollarDollarEvalShouldNotThrowForMissingFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("iframe >> internal:control=enter-frame >> canvas", "e => e.length").ConfigureAwait(false), Is.EqualTo(0));
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            Assert.That(await body.EvalOnSelectorAllAsync<int>("iframe >> internal:control=enter-frame >> canvas", "e => e.length").ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for $ and $$ (handle)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForDollarAndDollarDollarHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            IElementHandle element = await body.QuerySelectorAsync("iframe >> internal:control=enter-frame >> button").ConfigureAwait(false);
            Assert.That(await element.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello iframe"));
            IReadOnlyList<IElementHandle> elements = await body.QuerySelectorAllAsync("iframe >> internal:control=enter-frame >> span").ConfigureAwait(false);
            Assert.That(elements, Has.Count.EqualTo(2));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for $eval")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForDollarEval()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string value = await page.EvalOnSelectorAsync<string>("iframe >> internal:control=enter-frame >> button", "b => b.nodeName").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("BUTTON"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for $eval (handle)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForDollarEvalHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            string value = await body.EvalOnSelectorAsync<string>("iframe >> internal:control=enter-frame >> button", "b => b.nodeName").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("BUTTON"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for $$eval")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForDollarDollarEval()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string[] value = await page.EvalOnSelectorAllAsync<string[]>("iframe >> internal:control=enter-frame >> span", "ss => ss.map(s => s.textContent)").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(new[] { "1", "2" }));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should work for $$eval (handle)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForDollarDollarEvalHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            string[] value = await body.EvalOnSelectorAllAsync<string[]>("iframe >> internal:control=enter-frame >> span", "ss => ss.map(s => s.textContent)").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(new[] { "1", "2" }));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow dangling enter-frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowDanglingEnterFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("iframe >> internal:control=enter-frame");
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => button.ClickAsync());
            Assert.That(error.Message, Does.Contain("Selector cannot end with"));
            Assert.That(error.Message, Does.Contain("iframe >> internal:control=enter-frame"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow leading enter-frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowLeadingEnterFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.WaitForSelectorAsync("internal:control=enter-frame >> button"));
            Assert.That(error.Message, Does.Contain("Selector cannot start with"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow capturing before enter-frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowCapturingBeforeEnterFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("*css=iframe >> internal:control=enter-frame >> div");
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => button.ClickAsync());
            Assert.That(error.Message, Does.Contain("Can not capture the selector before diving into the frame"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should capture after the enter-frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCaptureAfterTheEnterFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator div = page.Locator("iframe >> internal:control=enter-frame >> *css=div >> button");
            Assert.That(await div.InnerHTMLAsync().ConfigureAwait(false), Does.Contain("<button>"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should click in lazy iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickInLazyIframe()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/iframe.html", route => route.FulfillAsync(new() { Body = "<html><button>Hello iframe</button></html>", ContentType = "text/html" })).ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                await page.EvaluateAsync(@"() => {
      const iframe = document.createElement('iframe');
      document.body.appendChild(iframe);
    }").ConfigureAwait(false);
                await Task.Delay(500).ConfigureAwait(false);
                await page.EvaluateAsync("() => document.querySelector('iframe').src = 'iframe.html'").ConfigureAwait(false);
            });

            ILocator button = page.Locator("iframe >> internal:control=enter-frame >> button");
            Task click = button.ClickAsync();
            Task<string> textTask = button.InnerTextAsync();
            Task expect = Assertions.Expect(button).ToHaveTextAsync("Hello iframe");
            await Task.WhenAll(click, textTask, expect).ConfigureAwait(false);
            Assert.That(await textTask.ConfigureAwait(false), Is.EqualTo("Hello iframe"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "waitFor should survive frame reattach")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WaitForShouldSurviveFrameReattach()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("iframe >> internal:control=enter-frame >> button:has-text(\"Hello nested iframe\")");
            Task promise = button.WaitForAsync();
            await page.Locator("iframe").EvaluateAsync<object>("e => e.remove()").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
    const iframe = document.createElement('iframe');
    iframe.src = 'iframe-2.html';
    document.body.appendChild(iframe);
  }").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "waitForSelector should survive frame reattach (handle)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WaitForSelectorShouldSurviveFrameReattachHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            Task<IElementHandle> promise = body.WaitForSelectorAsync("iframe >> internal:control=enter-frame >> button:has-text(\"Hello nested iframe\")");
            await page.Locator("iframe").EvaluateAsync<object>("e => e.remove()").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
    const iframe = document.createElement('iframe');
    iframe.src = 'iframe-2.html';
    document.body.appendChild(iframe);
  }").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "waitForSelector should survive iframe navigation (handle)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WaitForSelectorShouldSurviveIframeNavigationHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            Task<IElementHandle> promise = body.WaitForSelectorAsync("iframe >> internal:control=enter-frame >> button:has-text(\"Hello nested iframe\")");
            _ = page.Locator("iframe").EvaluateAsync<object>("e => { e.src = 'iframe-2.html'; }");
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "click should survive frame reattach")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickShouldSurviveFrameReattach()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("iframe >> internal:control=enter-frame >> button:has-text(\"Hello nested iframe\")");
            Task promise = button.ClickAsync();
            await page.Locator("iframe").EvaluateAsync<object>("e => e.remove()").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
    const iframe = document.createElement('iframe');
    iframe.src = 'iframe-2.html';
    document.body.appendChild(iframe);
  }").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "click should survive iframe navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickShouldSurviveIframeNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("iframe >> internal:control=enter-frame >> button:has-text(\"Hello nested iframe\")");
            Task promise = button.ClickAsync();
            _ = page.Locator("iframe").EvaluateAsync<object>("e => { e.src = 'iframe-2.html'; }");
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "click should survive navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickShouldSurviveNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/iframe.html").ConfigureAwait(false);
            Task promise = page.ClickAsync("button:has-text(\"Hello nested iframe\")");
            await page.WaitForTimeoutAsync(100).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/iframe-2.html").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should non work for non-frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNonWorkForNonFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator button = page.Locator("div >> internal:control=enter-frame >> button");
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => button.WaitForAsync());
            Assert.That(error.Message, Does.Contain("<div></div>"));
            Assert.That(error.Message, Does.Contain("<iframe> was expected"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should pierce frames into a single descendant frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPierceFramesIntoASingleDescendantFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator div = page.Locator("internal:control=pierce-frames >> div");
            await div.WaitForAsync().ConfigureAwait(false);
            await Assertions.Expect(div).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(await div.InnerHTMLAsync().ConfigureAwait(false), Does.Contain("<button>Hello iframe</button>"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should pierce through multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPierceThroughMultipleFrames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("internal:control=pierce-frames >> button[tag=\"iframe2\"]");
            await button.WaitForAsync().ConfigureAwait(false);
            await Assertions.Expect(button).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should pierce multiple times")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPierceMultipleTimes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("internal:control=pierce-frames >> div >> button[tag=\"iframe2\"]");
            await button.WaitForAsync().ConfigureAwait(false);
            await Assertions.Expect(button).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should match multiple elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchMultipleElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { Body = "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/a.html", route => route.FulfillAsync(new() { Body = "<div>one</div>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/b.html", route => route.FulfillAsync(new() { Body = "<span>two</span><span>three</span>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string[] texts = await page.EvalOnSelectorAllAsync<string[]>("internal:control=pierce-frames >> span", "els => els.map(e => e.textContent)").ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "two", "three" }));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should throw when piercing frames matches multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenPiercingFramesMatchesMultipleFrames()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { Body = "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/a.html", route => route.FulfillAsync(new() { Body = "<div>one</div>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.RouteAsync("**/b.html", route => route.FulfillAsync(new() { Body = "<div>two</div>", ContentType = "text/html" })).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Stopwatch sw = Stopwatch.StartNew();
            while (page.Frames.Count < 3 && sw.ElapsedMilliseconds < 5000)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(page.Frames.Count, Is.EqualTo(3));
            foreach (IFrame frame in page.Frames)
            {
                if (!ReferenceEquals(frame, page.MainFrame))
                {
                    await frame.WaitForSelectorAsync("div").ConfigureAwait(false);
                }
            }

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.Locator("internal:control=pierce-frames >> div").InnerHTMLAsync());
            Assert.That(error.Message, Does.Contain("Pierce-frame mode matched elements from multiple frames"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow pierce-frames in the middle of a selector")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowPierceFramesInTheMiddleOfASelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.Locator("iframe >> internal:control=pierce-frames >> div").WaitForAsync());
            Assert.That(error.Message, Does.Contain("\"pierce-frames\" is only allowed as the first selector token"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should allow entering frames while piercing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowEnteringFramesWhilePiercing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await RouteIframeAsync(page).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = page.Locator("internal:control=pierce-frames >> iframe[src=\"iframe-2.html\"] >> internal:control=enter-frame >> button");
            await button.WaitForAsync().ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow pierce-frames after entering a frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowPierceFramesAfterEnteringAFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.Locator("iframe >> internal:control=enter-frame >> internal:control=pierce-frames >> button").CountAsync());
            Assert.That(error.Message, Does.Contain("\"pierce-frames\" is only allowed as the first selector token"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow dangling enter-frame while piercing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowDanglingEnterFrameWhilePiercing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.Locator("internal:control=pierce-frames >> iframe >> internal:control=enter-frame").CountAsync());
            Assert.That(error.Message, Does.Contain("Selector cannot end with entering frame"));
        }
    }
}
