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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Expect ToHaveAttribute ignoreCase.
    /// </summary>
    [TestFixture]
    public class ExpectAttributeIgnoreCaseTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAttribute ignoreCase matches mixed case")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAttributeIgnoreCaseShouldMatchMixedCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" data-x=\"Hello\"></div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveAttributeAsync("data-x", "hello", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).ToHaveAttributeAsync("data-x", new Regex("^hello$"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).Not.ToHaveAttributeAsync("data-x", "hello", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAttribute ignoreCase waits for the value")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAttributeIgnoreCaseShouldWaitForTheValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" data-x=\"nope\"></div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToHaveAttributeAsync("data-x", "HELLO", new() { Timeout = 5000, IgnoreCase = true });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').setAttribute('data-x', 'Hello')").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }
    }
}
