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
    /// Official <c>library/role-utils.spec.ts</c> titles that use public
    /// locator APIs. Skipped (Node-only <c>__injectedScript</c> / WPT):
    /// name-and-role internals, hidden/aria-hidden cases that call
    /// <c>getNameAndRole</c>, and the injected-script suite.
    /// </summary>
    [TestFixture]
    public class LibraryRoleUtilsParityTests : PageTestEx
    {
        [PlaywrightTest("role-utils.spec.ts", "display:contents should be visible when contents are visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DisplayContentsShouldBeVisibleWhenContentsAreVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button style='display: contents;'>yo</button>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button")).ToHaveCountAsync(1).ConfigureAwait(false);
        }
    }
}
