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
    /// Check, Uncheck, SetChecked, and IsChecked on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorCheckTests : PageTestEx
    {
        [PlaywrightTest("page-check.spec.ts", "Locator CheckAsync checks a checkbox")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckShouldCheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            await page.Locator("#c").CheckAsync().ConfigureAwait(false);

            Assert.That(await page.Locator("#c").IsCheckedAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<bool>("document.getElementById('c').checked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "Locator UncheckAsync unchecks a checkbox")]
        [Test]
        [Timeout(30_000)]
        public async Task UncheckShouldUncheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked />").ConfigureAwait(false);

            await page.Locator("#c").UncheckAsync().ConfigureAwait(false);

            Assert.That(await page.Locator("#c").IsCheckedAsync().ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "Locator SetCheckedAsync sets both states")]
        [Test]
        [Timeout(30_000)]
        public async Task SetCheckedShouldSetBothStates()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);
            ILocator box = page.Locator("#c");

            await box.SetCheckedAsync(true).ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.True);

            await box.SetCheckedAsync(false).ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "Locator CheckAsync is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"checkbox\" /><input type=\"checkbox\" />").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator("input").CheckAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
