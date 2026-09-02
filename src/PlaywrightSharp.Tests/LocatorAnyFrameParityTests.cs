/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>locator-any-frame.spec.ts</c> parity for
    /// <c>page.frameLocator()</c> any-frame search.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LocatorAnyFrameParityTests : PageTestEx
    {
        private static readonly string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [OneTimeSetUp]
        public async Task StartBrowserAsync()
        {
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [OneTimeTearDown]
        public async Task StopBrowserAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            try
            {
                _context = await NewContextOrRecycleAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                _context = await _browser.NewContextAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            if (_page != null)
            {
                try
                {
                    await _page.UnrouteAllAsync(UnrouteBehavior.IgnoreErrors).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }

            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        private async Task<IBrowserContext> NewContextOrRecycleAsync()
        {
            Task<IBrowserContext> create = _browser.NewContextAsync();
            Task finished = await Task.WhenAny(create, Task.Delay(5000)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, create))
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                return await _browser.NewContextAsync().ConfigureAwait(false);
            }

            return await create.ConfigureAwait(false);
        }

        private async Task RecycleBrowserAsync()
        {
            IBrowser previous = _browser;
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (previous != null)
            {
                await DisposeQuietlyAsync(previous).ConfigureAwait(false);
            }
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private IPage Page => _page;

        private static Task RoutePageAsync(IPage page, string url, string body)
            => page.RouteAsync("**/" + url, route => route.FulfillAsync(new() { Body = body, ContentType = "text/html" }));

        private static async Task WaitForAllFramesAsync(IPage page, int frameCount, string selector)
        {
            await PollUntilAsync(
                () => Task.FromResult(page.Frames.Count == frameCount),
                "expected " + frameCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " frames").ConfigureAwait(false);
            foreach (IFrame frame in page.Frames)
            {
                if (!ReferenceEquals(frame, page.MainFrame))
                {
                    await frame.WaitForSelectorAsync(selector, WaitForSelectorState.Attached, timeout: 5000).ConfigureAwait(false);
                }
            }
        }

        private static async Task PollUntilAsync(Func<Task<bool>> ready, string message)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (await ready().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail(message);
        }

        private static IFrame FindFrame(IPage page, string urlPart)
        {
            foreach (IFrame frame in page.Frames)
            {
                if (frame.Url != null && frame.Url.Contains(urlPart, StringComparison.Ordinal))
                {
                    return frame;
                }
            }

            return null;
        }

        private static IFrame FrameAt(IPage page, int index)
        {
            int i = 0;
            foreach (IFrame frame in page.Frames)
            {
                if (i == index)
                {
                    return frame;
                }

                i++;
            }

            return null;
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should click a button inside an iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickAButtonInsideAnIframe()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button onclick=\"window.__clicked = true\">Click me</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.FrameLocator().GetByRole("button", name: "Click me").ClickAsync().ConfigureAwait(false);
            Assert.That(await FrameAt(Page, 1).EvaluateAsync<bool>("() => window.__clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should click a button in the main frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickAButtonInTheMainFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><button onclick=\"window.__clicked = true\">Click me</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>No buttons here</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.FrameLocator().Locator("button").ClickAsync().ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<bool>("() => window.__clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail click when elements match in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailClickWhenElementsMatchInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>one</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>two</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 3, "button").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("button").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
            Assert.That(error.Message, Does.Contain("waiting for frameLocator().locator('button')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail click upon strict mode violation inside a single frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailClickUponStrictModeViolationInsideASingleFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>one</button><button>two</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "button").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("button").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("waiting for frameLocator().locator('button')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should time out on click when there are no matches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeOutOnClickWhenThereAreNoMatches()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>Nothing here</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("button").ClickAsync(new() { Timeout = 1000 }));
            Assert.That(error.Message, Does.Contain("Timeout 1000ms exceeded"));
            Assert.That(error.Message, Does.Contain("waiting for frameLocator().locator('button')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should count elements in a single frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCountElementsInASingleFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>1</div><div>2</div><div>3</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "div").ConfigureAwait(false);
            Assert.That(await Page.FrameLocator().Locator("div").CountAsync().ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await Page.FrameLocator().Locator("button").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail count when elements match in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailCountWhenElementsMatchInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<div>main</div><iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>child</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "div").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("div").CountAsync());
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support toHaveCount")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportToHaveCount()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span><span>two</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("span")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("button")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should wait for a frame to appear with toHaveCount")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForAFrameToAppearWithToHaveCount()
        {
            await RoutePageAsync(Page, "empty.html", "<div>No frames yet</div>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span><span>two</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.EvaluateAsync(@"() => {
                setTimeout(() => {
                    const iframe = document.createElement('iframe');
                    iframe.src = 'a.html';
                    document.body.appendChild(iframe);
                }, 500);
            }").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("span")).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail toHaveCount when elements match in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailToHaveCountWhenElementsMatchInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<span>two</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 3, "span").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Assertions.Expect(Page.FrameLocator().Locator("span")).ToHaveCountAsync(2, new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
            Assert.That(error.Message, Does.Contain("Locator: frameLocator().locator('span')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support toHaveText")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportToHaveText()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>Hello iframe</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("div")).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support toHaveText with an array")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportToHaveTextWithAnArray()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span><span>two</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("span")).ToHaveTextAsync(new List<string> { "one", "two" }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail toHaveText when elements match in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailToHaveTextWhenElementsMatchInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>one</div>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<div>two</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 3, "div").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Assertions.Expect(Page.FrameLocator().Locator("div")).ToHaveTextAsync("one", new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
            Assert.That(error.Message, Does.Contain("Locator: frameLocator().locator('div')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail toHaveText with an array when elements match in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailToHaveTextWithAnArrayWhenElementsMatchInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<span>two</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 3, "span").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Assertions.Expect(Page.FrameLocator().Locator("span")).ToHaveTextAsync(new List<string> { "one", "two" }, new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail toHaveText upon strict mode violation inside a single frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailToHaveTextUponStrictModeViolationInsideASingleFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>one</div><div>two</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "div").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Assertions.Expect(Page.FrameLocator().Locator("div")).ToHaveTextAsync("one", new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("strict mode violation"));
            Assert.That(error.Message, Does.Contain("Locator: frameLocator().locator('div')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support evaluate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportEvaluate()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div data-foo=\"bar\">Hello</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await Page.FrameLocator().Locator("div").EvaluateAsync<string>("e => e.getAttribute('data-foo')").ConfigureAwait(false),
                Is.EqualTo("bar"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail evaluate when elements match in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailEvaluateWhenElementsMatchInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>one</div>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<div>two</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 3, "div").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("div").EvaluateAsync<string>("e => e.textContent", null, 3000));
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should time out on evaluate when there are no matches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeOutOnEvaluateWhenThereAreNoMatches()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>Nothing here</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("button").EvaluateAsync<string>("e => e.textContent", null, 1000));
            Assert.That(error.Message, Does.Contain("Timeout 1000ms exceeded"));
            Assert.That(error.Message, Does.Contain("waiting for frameLocator().locator('button')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support evaluateAll")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportEvaluateAll()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span><span>two</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "span").ConfigureAwait(false);
            Assert.That(
                await Page.FrameLocator().Locator("span").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent)").ConfigureAwait(false),
                Is.EqualTo(new[] { "one", "two" }));
            Assert.That(
                await Page.FrameLocator().Locator("button").EvaluateAllAsync<int>("els => els.length").ConfigureAwait(false),
                Is.EqualTo(0));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail evaluateAll when elements match in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailEvaluateAllWhenElementsMatchInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<span>two</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 3, "span").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("span").EvaluateAllAsync<int>("els => els.length"));
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support hasText filter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportHasTextFilter()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>foo</div><div>bar</div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "div").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("div", new() { HasText = "bar" })).ToHaveTextAsync("bar").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support first/last/nth")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportFirstLastNth()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<span>one</span><span>two</span><span>three</span>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "span").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("span").First).ToHaveTextAsync("one").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("span").Last).ToHaveTextAsync("three").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("span").Nth(1)).ToHaveTextAsync("two").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support nth in the middle of the chain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportNthInTheMiddleOfTheChain()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div><span>one</span></div><div><span>two</span></div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("div").Nth(1).Locator("span")).ToHaveTextAsync("two").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support composite locators")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCompositeLocators()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div><span>foo</span></div><div><i>bar</i></div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("div", new() { Has = Page.Locator("span") })).ToHaveTextAsync("foo").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support capture")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCapture()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div id=\"target\"><span>hello</span></div>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("*css=div >> span")).ToHaveAttributeAsync("id", "target").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should find a frame inside the scope")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFindAFrameInsideTheScope()
        {
            await RoutePageAsync(Page, "empty.html", "<section><iframe src=\"a.html\"></iframe></section><button>outside</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>inside</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "button").ConfigureAwait(false);
            IElementHandle scope = await Page.QuerySelectorAsync("section").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> buttons = await scope.QuerySelectorAllAsync("internal:control=any-frame >> button").ConfigureAwait(false);
            Assert.That(buttons.Count, Is.EqualTo(1));
            Assert.That(await buttons[0].TextContentAsync().ConfigureAwait(false), Is.EqualTo("inside"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should find a nested frame inside the scope")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFindANestedFrameInsideTheScope()
        {
            await RoutePageAsync(Page, "empty.html", "<section><iframe src=\"a.html\"></iframe></section><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe src=\"c.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>outside</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "c.html", "<button>deep</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 4), "expected 4 frames").ConfigureAwait(false);
            foreach (IFrame frame in Page.Frames)
            {
                if (frame.Url.Contains("b.html", StringComparison.Ordinal) || frame.Url.Contains("c.html", StringComparison.Ordinal))
                {
                    await frame.WaitForSelectorAsync("button", WaitForSelectorState.Attached).ConfigureAwait(false);
                }
            }

            IElementHandle scope = await Page.QuerySelectorAsync("section").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> buttons = await scope.QuerySelectorAllAsync("internal:control=any-frame >> button").ConfigureAwait(false);
            Assert.That(buttons.Count, Is.EqualTo(1));
            Assert.That(await buttons[0].TextContentAsync().ConfigureAwait(false), Is.EqualTo("deep"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should find a frame inside the scope while another iframe is stalled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFindAFrameInsideTheScopeWhileAnotherIframeIsStalled()
        {
            await RoutePageAsync(Page, "empty.html", "<section><iframe src=\"a.html\"></iframe><iframe src=\"stall.html\"></iframe></section>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>inside</button>").ConfigureAwait(false);
            await Page.RouteAsync("**/stall.html", _ => { }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage, waitUntil: WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            IElementHandle scope = await Page.QuerySelectorAsync("section").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> buttons = await scope.QuerySelectorAllAsync("internal:control=any-frame >> button").ConfigureAwait(false);
            Assert.That(buttons.Count, Is.EqualTo(1));
            Assert.That(await buttons[0].TextContentAsync().ConfigureAwait(false), Is.EqualTo("inside"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should respect the scope without a frame inside the scope")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectTheScopeWithoutAFrameInsideTheScope()
        {
            await RoutePageAsync(Page, "empty.html", "<section><button>target</button></section><iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>in-frame</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "button").ConfigureAwait(false);
            IElementHandle scope = await Page.QuerySelectorAsync("section").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> buttons = await scope.QuerySelectorAllAsync("internal:control=any-frame >> button").ConfigureAwait(false);
            Assert.That(buttons.Count, Is.EqualTo(1));
            Assert.That(await buttons[0].TextContentAsync().ConfigureAwait(false), Is.EqualTo("target"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should not match a chain across a frame boundary")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotMatchAChainAcrossAFrameBoundary()
        {
            await RoutePageAsync(Page, "empty.html", "<section><iframe src=\"a.html\"></iframe></section>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>deep</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            IFrame deepFrame = FindFrame(Page, "b.html");
            await deepFrame.WaitForSelectorAsync("button", WaitForSelectorState.Attached).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("section").Locator("button")).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("button")).ToHaveTextAsync("deep").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should only search frames inside the starting frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOnlySearchFramesInsideTheStartingFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><button>main</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>deep</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            IFrame deepFrame = FindFrame(Page, "b.html");
            await deepFrame.WaitForSelectorAsync("button", WaitForSelectorState.Attached).ConfigureAwait(false);
            IFrame middleFrame = FindFrame(Page, "a.html");
            await Assertions.Expect(middleFrame.FrameLocator().Locator("button")).ToHaveTextAsync("deep").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should enter a frame found in a nested frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEnterAFrameFoundInANestedFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><button>main</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe id=\"target\" src=\"b.html\"></iframe><button>decoy</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>inside</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().FrameLocator("#target").Locator("button")).ToHaveTextAsync("inside").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should click inside an entered frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickInsideAnEnteredFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe id=\"target\" src=\"b.html\"></iframe><button>Click me</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button onclick=\"window.__clicked = true\">Click me</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.FrameLocator().FrameLocator("#target").GetByRole("button", name: "Click me").ClickAsync().ConfigureAwait(false);
            IFrame frame = FindFrame(Page, "b.html");
            Assert.That(await frame.EvaluateAsync<bool>("() => window.__clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should not search nested frames after entering a frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotSearchNestedFramesAfterEnteringAFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe id=\"target\" src=\"a.html\"></iframe><button>main</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>deep</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().FrameLocator("#target").Locator("button")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support two frameLocators")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportTwoFrameLocators()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe id=\"x\" src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<iframe id=\"y\" src=\"c.html\"></iframe><button>decoy</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "c.html", "<button>bottom</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().FrameLocator("#x").FrameLocator("#y").Locator("button")).ToHaveTextAsync("bottom").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support locator before frameLocator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportLocatorBeforeFrameLocator()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<section><iframe src=\"b.html\"></iframe></section><iframe src=\"c.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>in-section</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "c.html", "<button>outside</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("section").FrameLocator("iframe").Locator("button")).ToHaveTextAsync("in-section").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support owner of a frameLocator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportOwnerOfAFrameLocator()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe id=\"target\" src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>inside</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await Page.FrameLocator().FrameLocator("#target").Owner.GetAttributeAsync("id").ConfigureAwait(false),
                Is.EqualTo("target"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should wait for the frame to enter to appear")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForTheFrameToEnterToAppear()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div>Nothing yet</div>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>late</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 2), "expected 2 frames").ConfigureAwait(false);
            await FrameAt(Page, 1).EvaluateAsync(@"() => {
                setTimeout(() => {
                    const iframe = document.createElement('iframe');
                    iframe.id = 'late';
                    iframe.src = 'b.html';
                    document.body.appendChild(iframe);
                }, 3000);
            }").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().FrameLocator("#late").Locator("button")).ToHaveTextAsync("late").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should fail when the frame to enter matches in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWhenTheFrameToEnterMatchesInMultipleFrames()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe class=\"inner\" src=\"c.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<iframe class=\"inner\" src=\"c.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "c.html", "<button>Click me</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 5), "expected 5 frames").ConfigureAwait(false);
            foreach (IFrame frame in Page.Frames)
            {
                if (frame.Url.Contains("c.html", StringComparison.Ordinal))
                {
                    await frame.WaitForSelectorAsync("button", WaitForSelectorState.Attached).ConfigureAwait(false);
                }
            }

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().FrameLocator(".inner").Locator("button").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support contentFrame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportContentFrame()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe id=\"target\" src=\"b.html\"></iframe><button>decoy</button>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<button>inside</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("#target").ContentFrame.Locator("button")).ToHaveTextAsync("inside").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should render frameLocator() in the locator description")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRenderFrameLocatorInTheLocatorDescription()
        {
            Assert.That(
                Page.FrameLocator().FrameLocator("#x").Locator("button").ToString(),
                Is.EqualTo("frameLocator().locator('#x').contentFrame().locator('button')"));
            Assert.That(
                Page.FrameLocator().Locator("section").FrameLocator("iframe").GetByText("foo").ToString(),
                Is.EqualTo("frameLocator().locator('section').locator('iframe').contentFrame().getByText('foo')"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should not allow frameLocator() inside a composite locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowFrameLocatorInsideACompositeLocator()
        {
            await RoutePageAsync(Page, "empty.html", "<button>main</button><iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<a href=\"#\">link</a>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "a").ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.Locator("button").Or(Page.FrameLocator().Locator("a")).CountAsync());
            Assert.That(error.Message, Does.Contain("frameLocator() is not allowed inside composite locators, while querying \"locator('button').or(frameLocator().locator('a'))\""));

            PlaywrightSharpException error2 = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.Locator("button").Filter(has: Page.FrameLocator().Locator("a")).CountAsync());
            Assert.That(error2.Message, Does.Contain("frameLocator() is not allowed inside composite locators"));

            PlaywrightSharpException error3 = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("button").Or(Page.FrameLocator().Locator("a")).CountAsync());
            Assert.That(error3.Message, Does.Contain("frameLocator() is not allowed inside composite locators, while querying \"frameLocator().locator('button').or(frameLocator().locator('a'))\""));

            PlaywrightSharpException error4 = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().FrameLocator("#f").Locator("a").Or(Page.FrameLocator().FrameLocator("#f").Locator("button")).CountAsync());
            Assert.That(error4.Message, Does.Contain("frameLocator() is not allowed inside composite locators"));

            PlaywrightSharpException error5 = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.FrameLocator().Locator("a").Or(Page.Locator("button")).CountAsync());
            Assert.That(error5.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support a composite locator under frameLocator()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportACompositeLocatorUnderFrameLocator()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<div class=\"classname\">first</div><button>second</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "button").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator(".classname").Or(Page.GetByRole("button"))).ToHaveTextAsync(new List<string> { "first", "second" }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support a composite locator under frameLocator() and a frame locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportACompositeLocatorUnderFrameLocatorAndAFrameLocator()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<iframe id=\"f\" src=\"b.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "b.html", "<div class=\"classname\">first</div><button>second</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            await Assertions.Expect(
                Page.FrameLocator().FrameLocator("#f").Locator(".classname").Or(Page.FrameLocator("#f").GetByRole("button")))
                .ToHaveTextAsync(new List<string> { "first", "second" }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should not allow first/last/nth on frameLocator()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNotAllowFirstLastNthOnFrameLocator()
        {
            PlaywrightSharpException first = Assert.Throws<PlaywrightSharpException>(() => _ = Page.FrameLocator().First);
            Assert.That(first.Message, Does.Contain("Selecting the nth frame is not allowed on frameLocator()"));
            PlaywrightSharpException last = Assert.Throws<PlaywrightSharpException>(() => _ = Page.FrameLocator().Last);
            Assert.That(last.Message, Does.Contain("Selecting the nth frame is not allowed on frameLocator()"));
            PlaywrightSharpException nth = Assert.Throws<PlaywrightSharpException>(() => Page.FrameLocator().Nth(1));
            Assert.That(nth.Message, Does.Contain("Selecting the nth frame is not allowed on frameLocator()"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should not allow owner on frameLocator()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNotAllowOwnerOnFrameLocator()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(() => Page.FrameLocator().Owner.CountAsync());
            Assert.That(error.Message, Does.Contain("Selector cannot be empty after frameLocator()"));
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should resolve aria-ref selectors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveAriaRefSelectors()
        {
            await RoutePageAsync(Page, "empty.html", "<button>main</button><iframe src=\"a.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>inside</button>").ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await WaitForAllFramesAsync(Page, 2, "button").ConfigureAwait(false);
            string snapshot = await Page.AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai }).ConfigureAwait(false);
            Match insideMatch = Regex.Match(snapshot, "button \"inside\" \\[ref=(.*?)\\]");
            Assert.That(insideMatch.Success, Is.True);
            Assert.That(insideMatch.Groups[1].Value, Does.Match("^f\\d+e\\d+$"));
            await Assertions.Expect(Page.FrameLocator().Locator("aria-ref=" + insideMatch.Groups[1].Value)).ToHaveTextAsync("inside").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should click while another iframe is stalled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickWhileAnotherIframeIsStalled()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"stall.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button onclick=\"window.__clicked = true\">Click me</button>").ConfigureAwait(false);
            await Page.RouteAsync("**/stall.html", _ => { }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage, waitUntil: WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            await Page.FrameLocator().Locator("button").ClickAsync().ConfigureAwait(false);
            IFrame frame = FindFrame(Page, "a.html");
            Assert.That(await frame.EvaluateAsync<bool>("() => window.__clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support toBeVisible while another iframe is stalled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportToBeVisibleWhileAnotherIframeIsStalled()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"stall.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>Click me</button>").ConfigureAwait(false);
            await Page.RouteAsync("**/stall.html", _ => { }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage, waitUntil: WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("button")).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-any-frame.spec.ts", "should support toHaveCount while another iframe is stalled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportToHaveCountWhileAnotherIframeIsStalled()
        {
            await RoutePageAsync(Page, "empty.html", "<iframe src=\"a.html\"></iframe><iframe src=\"stall.html\"></iframe>").ConfigureAwait(false);
            await RoutePageAsync(Page, "a.html", "<button>Click me</button>").ConfigureAwait(false);
            await Page.RouteAsync("**/stall.html", _ => { }).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage, waitUntil: WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(Page.Frames.Count == 3), "expected 3 frames").ConfigureAwait(false);
            await Assertions.Expect(Page.FrameLocator().Locator("button")).ToHaveCountAsync(1).ConfigureAwait(false);
        }
    }
}
