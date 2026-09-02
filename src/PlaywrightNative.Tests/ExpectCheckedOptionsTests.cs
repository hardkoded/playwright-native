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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Expect ToBeChecked checked and indeterminate options.
    /// </summary>
    [TestFixture]
    public class ExpectCheckedOptionsTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked checked false matches unchecked")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedCheckedFalseShouldMatchUnchecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Checked = false }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).Not.ToBeCheckedAsync(new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked checked false waits until unchecked")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedCheckedFalseShouldWaitUntilUnchecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked />").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Timeout = 5000, Checked = false });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').checked = false").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked indeterminate matches mixed")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedIndeterminateShouldMatchMixed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').indeterminate = true").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Indeterminate = true }).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').indeterminate = false").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).Not.ToBeCheckedAsync(new() { Indeterminate = true, Timeout = 2000 })
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToBeChecked rejects checked with indeterminate")]
        [Test]
        [Timeout(30_000)]
        public async Task ToBeCheckedShouldRejectCheckedWithIndeterminate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => Assertions.Expect(page.Locator("#c")).ToBeCheckedAsync(new() { Checked = false, Indeterminate = true }));
            Assert.That(ex.Message, Does.Contain("indeterminate and checked"));
        }
    }
}
