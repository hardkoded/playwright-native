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
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>selectors.register</c>.
    /// </summary>
    [TestFixture]
    public class SelectorsRegisterTests : PageTestEx
    {
        private const string TagEngine = @"{
  query(root, selector) {
    return root.querySelector(selector);
  },
  queryAll(root, selector) {
    return Array.from(root.querySelectorAll(selector));
  }
}";

        [PlaywrightTest("selectors-register.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await Playwright.Selectors.RegisterAsync("tag635work", TagEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>Click me</button></div>").ConfigureAwait(false);

            Assert.That(await page.Locator("tag635work=button").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            string name = await page.EvalOnSelectorAsync<string>("tag635work=button", "el => el.nodeName").ConfigureAwait(false);
            Assert.That(name, Is.EqualTo("BUTTON"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "should work with nested locators")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNestedLocators()
        {
            await Playwright.Selectors.RegisterAsync("tag635nested", TagEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>Click me</button></div>").ConfigureAwait(false);

            ILocator button = page.Locator("tag635nested=div").GetByText("Click me");
            Assert.That(await button.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            await button.ClickAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-register.spec.ts", "should work with path")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "pw-tag635-" + Guid.NewGuid().ToString("N") + ".js");
            await File.WriteAllTextAsync(path, TagEngine).ConfigureAwait(false);
            try
            {
                await Playwright.Selectors.RegisterAsync("tag635path", new() { Path = path }).ConfigureAwait(false);
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<section>here</section>").ConfigureAwait(false);
                Assert.That(await page.Locator("tag635path=section").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("selectors-register.spec.ts", "should throw on invalid name")]
        [Test]
        [Timeout(30_000)]
        public void ShouldThrowOnInvalidName()
        {
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => Playwright.Selectors.RegisterAsync("$", TagEngine));
            Assert.That(ex.Message, Does.Contain("Selector engine name may only contain [a-zA-Z0-9_] characters"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "should throw already registered")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowAlreadyRegistered()
        {
            await Playwright.Selectors.RegisterAsync("tag635dup", TagEngine).ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => Playwright.Selectors.RegisterAsync("tag635dup", TagEngine));
            Assert.That(ex.Message, Does.Contain("\"tag635dup\" selector engine has been already registered"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "should throw on predefined engine")]
        [Test]
        [Timeout(30_000)]
        public void ShouldThrowOnPredefinedEngine()
        {
            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => Playwright.Selectors.RegisterAsync("css", TagEngine));
            Assert.That(ex.Message, Does.Contain("\"css\" is a predefined selector engine"));
        }
    }
}
