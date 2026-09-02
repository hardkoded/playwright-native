/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator-misc-2.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class LocatorMisc2Tests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static bool IsHeadless
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("HEADLESS");
                return string.IsNullOrEmpty(value)
                    || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void SkipFirefoxHeadedScreenshot()
        {
            if (TestConstants.IsFirefox && !IsHeadless)
            {
                Assert.Ignore("Firefox headed screenshots are skipped upstream.");
            }
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19424;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should press")]
        [PlaywrightTest("locator-misc-2.spec.ts", "should press @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPress()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='text' />").ConfigureAwait(false);
            await page.Locator("input").PressAsync("h").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("h"));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should scroll into view")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/offscreenbuttons.html").ConfigureAwait(false);

            for (int i = 0; i < 11; ++i)
            {
                ILocator button = page.Locator("#btn" + i.ToString(CultureInfo.InvariantCulture));
                double before = await button.EvaluateAsync<double>("button => { return button.getBoundingClientRect().right - window.innerWidth; }").ConfigureAwait(false);
                Assert.That(before, Is.EqualTo(10 * i));
                await button.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                double after = await button.EvaluateAsync<double>("button => { return button.getBoundingClientRect().right - window.innerWidth; }").ConfigureAwait(false);
                Assert.That(after <= 0, Is.True);
                await page.EvaluateAsync<object>("(() => window.scrollTo(0, 0))()").ConfigureAwait(false);
            }
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should scroll zero-sized element into view")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollZeroSizedElementIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(1280, 720).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      html,body { margin: 0; padding: 0; }
      ::-webkit-scrollbar { display: none; }
      * { scrollbar-width: none; }
    </style>
    <div style=""height: 2000px; text-align: center; border: 10px solid blue;"">
      <h1>SCROLL DOWN</h1>
    </div>
    <div id=lazyload style=""font-size:75px; background-color: green;""></div>
    <script>
      const lazyLoadElement = document.querySelector('#lazyload');
      const observer = new IntersectionObserver((entries) => {
        if (entries.some(entry => entry.isIntersecting)) {
          lazyLoadElement.textContent = 'LAZY LOADED CONTENT';
          lazyLoadElement.style.height = '20px';
          observer.disconnect();
        }
      });
      observer.observe(lazyLoadElement);
    </script>
  ").ConfigureAwait(false);

            var box = await page.Locator("#lazyload").BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(box, Is.Not.Null);
            Assert.That(box.X, Is.EqualTo(0));
            Assert.That(box.Y, Is.EqualTo(2020));
            Assert.That(box.Width, Is.EqualTo(1280));
            Assert.That(box.Height, Is.EqualTo(0));
            await page.Locator("#lazyload").ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#lazyload")).ToHaveTextAsync("LAZY LOADED CONTENT").ConfigureAwait(false);
            box = await page.Locator("#lazyload").BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(box, Is.Not.Null);
            Assert.That(box.X, Is.EqualTo(0));
            Assert.That(box.Y, Is.EqualTo(720));
            Assert.That(box.Width, Is.EqualTo(1280));
            Assert.That(box.Height, Is.EqualTo(20));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should select textarea")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectTextarea()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/textarea.html").ConfigureAwait(false);
            ILocator textarea = page.Locator("textarea");
            await textarea.EvaluateAsync<object>("textarea => { textarea.value = 'some value'; }").ConfigureAwait(false);
            await textarea.SelectTextAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window.getSelection().toString())()").ConfigureAwait(false), Is.EqualTo("some value"));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should type")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldType()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='text' />").ConfigureAwait(false);
            await page.Locator("input").TypeAsync("hello").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should pressSequentially")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPressSequentially()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='text' />").ConfigureAwait(false);
            await page.Locator("input").PressSequentiallyAsync("hello").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should take screenshot")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTakeScreenshot()
        {
            SkipFirefoxHeadedScreenshot();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window.scrollBy(50, 100))()").ConfigureAwait(false);
            ILocator element = page.Locator(".box:nth-of-type(3)");
            byte[] screenshot = await element.ScreenshotAsync().ConfigureAwait(false);
            Assert.That(screenshot, Is.Not.Null);
            Assert.That(screenshot.Length, Is.GreaterThan(20));
            Assert.That(screenshot[0], Is.EqualTo(0x89));
            Assert.That(screenshot[1], Is.EqualTo(0x50));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should return bounding box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnBoundingBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 500).ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            ILocator element = page.Locator(".box:nth-of-type(13)");
            var box = await element.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(box, Is.Not.Null);
            Assert.That(box.X, Is.EqualTo(100));
            Assert.That(box.Y, Is.EqualTo(50));
            Assert.That(box.Width, Is.EqualTo(50));
            Assert.That(box.Height, Is.EqualTo(50));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should waitFor")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitFor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = page.Locator("span");
            Task promise = locator.WaitForAsync();
            await page.EvalOnSelectorAsync<object>("div", "div => div.innerHTML = '<span>target</span>'").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveTextAsync("target").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should waitFor hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><span>target</span></div>").ConfigureAwait(false);
            ILocator locator = page.Locator("span");
            Task promise = locator.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await page.EvalOnSelectorAsync<object>("div", "div => div.innerHTML = ''").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should combine visible with other selectors")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCombineVisibleWithOtherSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<div>
  <div class=""item"" style=""display: none"">Hidden data0</div>
  <div class=""item"">visible data1</div>
  <div class=""item"" style=""display: none"">Hidden data1</div>
  <div class=""item"">visible data2</div>
  <div class=""item"" style=""display: none"">Hidden data1</div>
  <div class=""item"">visible data3</div>
  </div>").ConfigureAwait(false);
            ILocator locator = page.Locator(".item >> visible=true").Nth(1);
            await Assertions.Expect(locator).ToHaveTextAsync("visible data2").ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".item >> visible=true >> text=data3")).ToHaveTextAsync("visible data3").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should support filter(visible)")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSupportFilterVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<div>
    <div class=""item"" style=""display: none"">Hidden data0</div>
    <div class=""item"">visible data1</div>
    <div class=""item"" style=""display: none"">Hidden data1</div>
    <div class=""item"">visible data2</div>
    <div class=""item"" style=""display: none"">Hidden data2</div>
    <div class=""item"">visible data3</div>
    </div>
  ").ConfigureAwait(false);
            ILocator locator = page.Locator(".item").Filter(visible: true).Nth(1);
            await Assertions.Expect(locator).ToHaveTextAsync("visible data2").ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".item").Filter(visible: true).GetByText("data3")).ToHaveTextAsync("visible data3").ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".item").Filter(visible: false).GetByText("data1")).ToHaveTextAsync("Hidden data1").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "locator.count should work with deleted Map in main world")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorCountShouldWorkWithDeletedMapInMainWorld()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => { Map = 1; })()").ConfigureAwait(false);
            await page.Locator("#searchResultTableDiv .x-grid3-row").CountAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#searchResultTableDiv .x-grid3-row")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "Locator.locator() and FrameLocator.locator() should accept locator")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorLocatorAndFrameLocatorLocatorShouldAcceptLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div><input value=outer></div>
    <iframe srcdoc=""<div><input value=inner></div>""></iframe>
  ").ConfigureAwait(false);

            ILocator inputLocator = page.Locator("input");
            Assert.That(await inputLocator.InputValueAsync().ConfigureAwait(false), Is.EqualTo("outer"));
            Assert.That(await page.Locator("div").Locator(inputLocator).InputValueAsync().ConfigureAwait(false), Is.EqualTo("outer"));
            Assert.That(await page.FrameLocator("iframe").Locator(inputLocator).InputValueAsync().ConfigureAwait(false), Is.EqualTo("inner"));
            Assert.That(await page.FrameLocator("iframe").Locator("div").Locator(inputLocator).InputValueAsync().ConfigureAwait(false), Is.EqualTo("inner"));

            ILocator divLocator = page.Locator("div");
            Assert.That(await divLocator.Locator("input").InputValueAsync().ConfigureAwait(false), Is.EqualTo("outer"));
            Assert.That(await page.FrameLocator("iframe").Locator(divLocator).Locator("input").InputValueAsync().ConfigureAwait(false), Is.EqualTo("inner"));
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "should fill programmatically enabled textarea")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillProgrammaticallyEnabledTextarea()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button>Enable</button>
    <form>
      <textarea id=""text"" disabled></textarea>
    </form>
    <script>
      document.querySelector('button').addEventListener('click', () => {
        document.querySelector('#text').disabled = false;
      });
    </script>
  ").ConfigureAwait(false);
            await page.Locator("button").ClickAsync().ConfigureAwait(false);
            await page.Locator("#text").FillAsync("Hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#text")).ToHaveValueAsync("Hello").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-misc-2.spec.ts", "press should throw on unknown keys")]
        [Test]
        [Timeout(30_000)]
        public async Task PressShouldThrowOnUnknownKeys()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='text' value='hello' />").ConfigureAwait(false);
            ILocator locator = page.GetByRole("textbox");
            PlaywrightNativeException unknown = Assert.CatchAsync<PlaywrightNativeException>(() => locator.PressAsync("NotARealKey"));
            Assert.That(unknown, Is.Not.Null);
            Assert.That(unknown.Message, Does.Match(new Regex("Unknown key: \"NotARealKey\"")));
            PlaywrightNativeException yo = Assert.CatchAsync<PlaywrightNativeException>(() => locator.PressAsync("ё"));
            Assert.That(yo, Is.Not.Null);
            Assert.That(yo.Message, Does.Match(new Regex("Unknown key: \"ё\"")));
            PlaywrightNativeException emoji = Assert.CatchAsync<PlaywrightNativeException>(() => locator.PressAsync("😊"));
            Assert.That(emoji, Is.Not.Null);
            Assert.That(emoji.Message, Does.Match(new Regex("Unknown key: \"😊\"")));
        }
    }
}
