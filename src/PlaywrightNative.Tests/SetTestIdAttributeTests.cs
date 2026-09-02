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
    /// Official <c>selectors.setTestIdAttribute</c>.
    /// </summary>
    [TestFixture]
    public class SetTestIdAttributeTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "SetTestIdAttribute changes GetByTestIdAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task SetTestIdAttributeShouldChangeGetByTestId()
        {
            Playwright.SetTestIdAttribute("data-pw");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<div data-pw=\"hello\">North</div><div data-testid=\"hello\">South</div>").ConfigureAwait(false);

                IElementHandle handle = await page.GetByTestIdAsync("hello").ConfigureAwait(false);
                Assert.That(handle, Is.Not.Null);
                Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("North"));
            }
            finally
            {
                Playwright.SetTestIdAttribute("data-testid");
            }
        }
    }
}
