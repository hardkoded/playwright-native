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
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator-list.spec.ts</c> parity for <see cref="ILocator.AllAsync"/>.
    /// </summary>
    [TestFixture]
    public class LocatorListParityTests : PageTestEx
    {
        [PlaywrightTest("locator-list.spec.ts", "locator.all should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LocatorAllShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><p>A</p><p>B</p><p>C</p></div>").ConfigureAwait(false);
            List<string> texts = new();
            foreach (ILocator paragraph in await page.Locator("div >> p").AllAsync().ConfigureAwait(false))
            {
                texts.Add(await paragraph.TextContentAsync().ConfigureAwait(false));
            }

            Assert.That(texts, Is.EqualTo(new[] { "A", "B", "C" }));
        }
    }
}
