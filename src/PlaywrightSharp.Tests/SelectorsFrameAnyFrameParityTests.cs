/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>selectors-frame.spec.ts</c> remaining any-frame titles.
    /// Leftover pierce-frames stay in <c>SelectorsFrameParityTests</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class SelectorsFrameAnyFrameParityTests : PageTestEx
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

        private IPage Page => _page;

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

        [PlaywrightTest("selectors-frame.spec.ts", "should match in a descendant frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchInADescendantFrame()
        {
            await RouteIframeAsync(Page).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator div = Page.Locator("internal:control=any-frame >> div");
            await div.WaitForAsync().ConfigureAwait(false);
            await Assertions.Expect(div).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(await div.InnerHTMLAsync().ConfigureAwait(false), Does.Contain("<button>Hello iframe</button>"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should match in a deeply nested frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchInADeeplyNestedFrame()
        {
            await RouteIframeAsync(Page).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = Page.Locator("internal:control=any-frame >> button[tag=\"iframe2\"]");
            await button.WaitForAsync().ConfigureAwait(false);
            await Assertions.Expect(button).ToHaveCountAsync(1).ConfigureAwait(false);
            Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not match a chain across frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotMatchAChainAcrossFrames()
        {
            await RouteIframeAsync(Page).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("internal:control=any-frame >> div >> button[tag=\"iframe2\"]")).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("internal:control=any-frame >> div >> button")).ToHaveTextAsync("Hello iframe").ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should throw when matching elements in multiple frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenMatchingElementsInMultipleFrames()
        {
            await Page.RouteAsync("**/empty.html", route => route.FulfillAsync(new() { Body = "<iframe src=\"a.html\"></iframe><iframe src=\"b.html\"></iframe>", ContentType = "text/html" })).ConfigureAwait(false);
            await Page.RouteAsync("**/a.html", route => route.FulfillAsync(new() { Body = "<div>one</div>", ContentType = "text/html" })).ConfigureAwait(false);
            await Page.RouteAsync("**/b.html", route => route.FulfillAsync(new() { Body = "<div>two</div>", ContentType = "text/html" })).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (Page.Frames.Count != 3 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(Page.Frames.Count, Is.EqualTo(3));
            foreach (IFrame frame in Page.Frames)
            {
                if (!ReferenceEquals(frame, Page.MainFrame))
                {
                    await frame.WaitForSelectorAsync("div", new() { Timeout = 5000 }).ConfigureAwait(false);
                }
            }

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.Locator("internal:control=any-frame >> div").InnerHTMLAsync());
            Assert.That(error.Message, Does.Contain("frameLocator() matched elements in multiple frames"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow any-frame in the middle of a selector")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowAnyFrameInTheMiddleOfASelector()
        {
            await RouteIframeAsync(Page).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.Locator("iframe >> internal:control=any-frame >> div").WaitForAsync());
            Assert.That(error.Message, Does.Contain("\"any-frame\" is only allowed as the first selector token"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should allow entering frames from any frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowEnteringFramesFromAnyFrame()
        {
            await RouteIframeAsync(Page).ConfigureAwait(false);
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            ILocator button = Page.Locator("internal:control=any-frame >> iframe[src=\"iframe-2.html\"] >> internal:control=enter-frame >> button");
            await button.WaitForAsync().ConfigureAwait(false);
            Assert.That(await button.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hello nested iframe"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow any-frame after entering a frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowAnyFrameAfterEnteringAFrame()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.Locator("iframe >> internal:control=enter-frame >> internal:control=any-frame >> button").CountAsync());
            Assert.That(error.Message, Does.Contain("\"any-frame\" is only allowed as the first selector token"));
        }

        [PlaywrightTest("selectors-frame.spec.ts", "should not allow dangling enter-frame after any-frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAllowDanglingEnterFrameAfterAnyFrame()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.Locator("internal:control=any-frame >> iframe >> internal:control=enter-frame").CountAsync());
            Assert.That(error.Message, Does.Contain("Selector cannot end with entering frame"));
        }
    }
}
