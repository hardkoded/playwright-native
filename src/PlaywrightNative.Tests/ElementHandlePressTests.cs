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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-press.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandlePressTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-press.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type='text' />").ConfigureAwait(false);
            await page.PressAsync("input", "h").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("h"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "should not select existing value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotSelectExistingValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type='text' value='hello' />").ConfigureAwait(false);
            await page.PressAsync("input", "w").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("whello"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "should reset selection when not focused")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResetSelectionWhenNotFocused()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type='text' value='hello' /><div tabIndex=2>text</div>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", @"input => {
                input.selectionStart = 2;
                input.selectionEnd = 4;
                document.querySelector('div').focus();
            }").ConfigureAwait(false);
            await page.PressAsync("input", "w").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("whello"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "should not modify selection when focused")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotModifySelectionWhenFocused()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type='text' value='hello' />").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("input", @"input => {
                input.focus();
                input.selectionStart = 2;
                input.selectionEnd = 4;
            }").ConfigureAwait(false);
            await page.PressAsync("input", "w").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("hewo"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "should work with number input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNumberInput()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Started failing after https://github.com/WebKit/WebKit/commit/c92a2aea185d63b5e9998608a9c0321a461c496c");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type='number' value=2 />").ConfigureAwait(false);
            await page.PressAsync("input", "1").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input", "input => input.value").ConfigureAwait(false), Is.EqualTo("12"));
        }
    }
}
