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
    /// Expect ToBeAttached and ToBeFocused.
    /// </summary>
    [TestFixture]
    public class ExpectAttachedTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeAttached waits until the element appears")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeAttachedShouldWaitUntilTheElementAppears()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToBeAttachedAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<span id=\"t\">x</span>')")
                .ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeFocused waits until focus")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeFocusedShouldWaitUntilFocus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" /><input id=\"other\" />").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#n")).ToBeFocusedAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.Locator("#n").FocusAsync().ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "Not ToBeAttached matches a missing locator")]
        [Test]
        [Timeout(30_000)]
        public async Task NotToBeAttachedShouldMatchAMissingLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#gone")).Not.ToBeAttachedAsync().ConfigureAwait(false);
        }
    }
}
