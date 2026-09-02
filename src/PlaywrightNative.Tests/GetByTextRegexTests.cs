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
    /// Official <c>GetByText(Regex)</c>.
    /// </summary>
    [TestFixture]
    public class GetByTextRegexTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByText Regex matches on page and locator")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTextRegexShouldMatchOnPageAndLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"root\"><p id=\"p\">Hello world</p></div>").ConfigureAwait(false);

            string pageText = await page.GetByText(new Regex("hello", RegexOptions.IgnoreCase)).TextContentAsync().ConfigureAwait(false);
            string locatorText = await page.Locator("#root").GetByText(new Regex("^Hello")).TextContentAsync().ConfigureAwait(false);
            string frameText = await page.MainFrame.GetByText(new Regex("world$")).TextContentAsync().ConfigureAwait(false);

            Assert.That(pageText, Does.Contain("Hello world"));
            Assert.That(locatorText, Does.Contain("Hello world"));
            Assert.That(frameText, Does.Contain("Hello world"));
        }
    }
}
