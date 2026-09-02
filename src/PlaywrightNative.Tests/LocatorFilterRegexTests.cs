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
    /// Official <c>Locator.Filter(Regex)</c> / <c>HasNotText(Regex)</c>.
    /// </summary>
    [TestFixture]
    public class LocatorFilterRegexTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "Filter and HasNotText Regex")]
        [Test]
        [Timeout(30_000)]
        public async Task FilterAndHasNotTextRegexShouldNarrow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div class=\"item\" id=\"keep\">Hello world</div>" +
                "<div class=\"item\" id=\"drop\">Goodbye</div>").ConfigureAwait(false);

            string kept = await page.Locator(".item").Filter(new Regex("hello", RegexOptions.IgnoreCase)).GetAttributeAsync("id").ConfigureAwait(false);
            string notDropped = await page.Locator(".item").HasNotText(new Regex("^Good")).GetAttributeAsync("id").ConfigureAwait(false);

            Assert.That(kept, Is.EqualTo("keep"));
            Assert.That(notDropped, Is.EqualTo("keep"));
        }
    }
}
