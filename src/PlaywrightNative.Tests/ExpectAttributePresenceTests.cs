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
    /// Expect ToHaveAttribute name-only presence.
    /// </summary>
    [TestFixture]
    public class ExpectAttributePresenceTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAttribute name waits until the attribute appears")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAttributeNameShouldWaitUntilTheAttributeAppears()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#n")).ToHaveAttributeAsync("disabled", "", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#n').setAttribute('disabled', '')").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAttribute name matches a present attribute")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAttributeNameShouldMatchAPresentAttribute()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" disabled />").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#n")).ToHaveAttributeAsync("disabled", new System.Text.RegularExpressions.Regex(".*")).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#n")).Not.ToHaveAttributeAsync("open", "", new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
