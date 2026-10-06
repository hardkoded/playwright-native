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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator-is-visible.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class LocatorIsVisibleTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string EmptyPage = TestConstants.EmptyPage;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19220;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    EmptyPage = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/empty.html";
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

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible and isHidden should work")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleAndIsHiddenShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Hi</div><span></span>").ConfigureAwait(false);

            ILocator div = page.Locator("div");
            Assert.That(await div.IsVisibleAsync().ConfigureAwait(false), Is.True);
            Assert.That(await div.IsHiddenAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsVisibleAsync("div").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsHiddenAsync("div").ConfigureAwait(false), Is.False);

            ILocator span = page.Locator("span");
            Assert.That(await span.IsVisibleAsync().ConfigureAwait(false), Is.False);
            Assert.That(await span.IsHiddenAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsVisibleAsync("span").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("span").ConfigureAwait(false), Is.True);

            Assert.That(await page.IsVisibleAsync("no-such-element").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("no-such-element").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible should be true for opacity:0")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleShouldBeTrueForOpacity0()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"opacity:0\">Hi</div>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible should be true for element outside view")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleShouldBeTrueForElementOutsideView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"position: absolute; left: -1000px\">Hi</div>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible and isHidden should work with details")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleAndIsHiddenShouldWorkWithDetails()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<details>
    <summary>click to open</summary>
      <ul>
        <li>hidden item 1</li>
        <li>hidden item 2</li>
        <li>hidden item 3</li>
      </ul
  </details>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("ul")).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible and isHidden should work with nested details")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleAndIsHiddenShouldWorkWithNestedDetails()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<details>
    <summary>outer</summary>
    <details open>
      <summary>inner</summary>
      <button>hidden button</button>
    </details>
  </details>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("summary", new() { HasText = "outer" })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("summary", new() { HasText = "inner" })).ToBeHiddenAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button")).ToBeHiddenAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Button)).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible and isHidden should work with unslotted content")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleAndIsHiddenShouldWorkWithUnslottedContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=unslotted><span>hidden 1</span><div><span>hidden 2</span></div></div>
    <div id=slotted><span>visible</span></div>
    <script>
      unslotted.attachShadow({ mode: 'open' }).innerHTML = 'nothing to see here';
      slotted.attachShadow({ mode: 'open' }).innerHTML = '<slot></slot>';
    </script>
  ").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("span", new() { HasText = "hidden 1" })).ToBeHiddenAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("span", new() { HasText = "hidden 2" })).ToBeHiddenAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("span", new() { HasText = "visible" })).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible and isHidden should work with list box options")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleAndIsHiddenShouldWorkWithListBoxOptions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <select multiple>
      <option>visible 1</option>
      <optgroup label=""group""><option>visible 2</option></optgroup>
      <option hidden>hidden 1</option>
      <optgroup label=""hidden group"" style=""display:none""><option>hidden 2</option></optgroup>
    </select>
    <select size=3><option>visible 3</option></select>
    <select multiple style=""display:none""><option>hidden 3</option></select>
  ").ConfigureAwait(false);

            foreach (string name in new[] { "visible 1", "visible 2", "visible 3" })
            {
                await Assertions.Expect(page.GetByRole(AriaRole.Option, new() { Name = name, Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
            }

            foreach (string text in new[] { "hidden 1", "hidden 2", "hidden 3" })
            {
                await Assertions.Expect(page.Locator("option", new() { HasText = text })).ToBeHiddenAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible inside a button")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleInsideAButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button><span></span>a button</button>").ConfigureAwait(false);
            ILocator span = page.Locator("span");
            Assert.That(await span.IsVisibleAsync().ConfigureAwait(false), Is.False);
            Assert.That(await span.IsHiddenAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsVisibleAsync("span").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("span").ConfigureAwait(false), Is.True);
            await Assertions.Expect(span).Not.ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(span).ToBeHiddenAsync().ConfigureAwait(false);
            await span.WaitForAsync(new() { State = WaitForSelectorState.Hidden }).ConfigureAwait(false);
            await page.Locator("button").WaitForAsync(new() { State = WaitForSelectorState.Visible }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible inside a role=button")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleInsideARoleButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div role=button><span></span>a button</div>").ConfigureAwait(false);
            ILocator span = page.Locator("span");
            Assert.That(await span.IsVisibleAsync().ConfigureAwait(false), Is.False);
            Assert.That(await span.IsHiddenAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsVisibleAsync("span").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("span").ConfigureAwait(false), Is.True);
            await Assertions.Expect(span).Not.ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(span).ToBeHiddenAsync().ConfigureAwait(false);
            await span.WaitForAsync(new() { State = WaitForSelectorState.Hidden }).ConfigureAwait(false);
            await page.Locator("[role=button]").WaitForAsync(new() { State = WaitForSelectorState.Visible }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible during navigation should not throw")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleDuringNavigationShouldNotThrow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string emptyJson = JsonSerializer.Serialize(EmptyPage);
            for (int i = 0; i < 20; i++)
            {
                string html = @"
      <script>
        setTimeout(() => {
          window.location.href = " + emptyJson + @";
        }, Math.random(50));
      </script>
    ";
                try
                {
                    await page.SetContentAsync(html).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Avoid page.SetContent throwing because of scheduled navigation.
                }

                Assert.That(await page.Locator("div").IsVisibleAsync().ConfigureAwait(false), Is.False);
            }
        }

        [PlaywrightTest("locator-is-visible.spec.ts", "isVisible with invalid selector should throw")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleWithInvalidSelectorShouldThrow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightException error = Assert.CatchAsync<PlaywrightException>(
                () => page.Locator("hey=what").IsVisibleAsync());

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Unknown engine \"hey\" while parsing selector hey=what"));
        }
    }
}
