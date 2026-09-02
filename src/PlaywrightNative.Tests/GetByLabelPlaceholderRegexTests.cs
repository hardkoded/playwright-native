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
    /// Official <c>GetByLabel(Regex)</c> / <c>GetByPlaceholder(Regex)</c>.
    /// </summary>
    [TestFixture]
    public class GetByLabelPlaceholderRegexTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByLabel and GetByPlaceholder Regex")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByLabelAndPlaceholderRegexShouldResolve()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<label for=\"n\">Full name</label><input id=\"n\" placeholder=\"Your name\" />").ConfigureAwait(false);

            string labelId = await page.GetByLabel(new Regex("full", RegexOptions.IgnoreCase)).GetAttributeAsync("id").ConfigureAwait(false);
            string placeholderId = await page.GetByPlaceholder(new Regex("^Your")).GetAttributeAsync("id").ConfigureAwait(false);
            string locatorId = await page.Locator("body").GetByLabel(new Regex("name$")).GetAttributeAsync("id").ConfigureAwait(false);

            Assert.That(labelId, Is.EqualTo("n"));
            Assert.That(placeholderId, Is.EqualTo("n"));
            Assert.That(locatorId, Is.EqualTo("n"));
        }
    }
}
