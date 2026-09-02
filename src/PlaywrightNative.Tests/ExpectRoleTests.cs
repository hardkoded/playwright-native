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
    /// Expect ToHaveRole and ToHaveAccessibleName.
    /// </summary>
    [TestFixture]
    public class ExpectRoleTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveRole and ToHaveAccessibleName match a button")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveRoleAndNameShouldMatchAButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#go"));
            await expect.ToHaveRoleAsync(AriaRole.Button).ConfigureAwait(false);
            await expect.ToHaveAccessibleNameAsync("Go").ConfigureAwait(false);
            await expect.ToHaveAccessibleNameAsync("Go", exact: true).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleName waits until aria-label is set")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAccessibleNameShouldWaitUntilAriaLabelIsSet()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#go")).ToHaveAccessibleNameAsync("Save", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#go').setAttribute('aria-label', 'Save')")
                .ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleName matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task AccessibleNameShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\" aria-label=\"Submit form\">Go</button>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#go"));
            await expect.ToHaveAccessibleNameAsync(new Regex("Submit")).ConfigureAwait(false);
            await expect.ToHaveAccessibleNameAsync(new Regex("^Submit form$")).ConfigureAwait(false);
            await expect.Not.ToHaveAccessibleNameAsync(new Regex("^Cancel$"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
