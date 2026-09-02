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
    /// Expect ToHaveText / ToContainText ignoreCase.
    /// </summary>
    [TestFixture]
    public class ExpectIgnoreCaseTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveText ignoreCase matches mixed case")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveTextIgnoreCaseShouldMatchMixedCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">Hello World</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveTextAsync("hello world", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).ToHaveTextAsync("HELLO WORLD", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).Not.ToHaveTextAsync("hello world", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToContainText ignoreCase matches a substring")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainTextIgnoreCaseShouldMatchASubstring()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">Hello World</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToContainTextAsync("WORLD", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).Not.ToContainTextAsync("WORLD", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "Text regex ignoreCase overrides the pattern flag")]
        [Test]
        [Timeout(30_000)]
        public async Task TextRegexIgnoreCaseShouldOverrideThePatternFlag()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">Hello World</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveTextAsync(new Regex("^hello world$"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).ToContainTextAsync(new Regex("WORLD"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t"))
                .Not.ToHaveTextAsync(new Regex("hello world", RegexOptions.IgnoreCase), new() { IgnoreCase = false, Timeout = 2000 })
                .ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "Text list ignoreCase matches each item")]
        [Test]
        [Timeout(30_000)]
        public async Task TextListIgnoreCaseShouldMatchEachItem()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<ul>" +
                "<li>Text 1</li>" +
                "<li>Text 2</li>" +
                "</ul>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("li"));
            await expect.ToHaveTextAsync(new[] { "text 1", "TEXT 2" }, new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.ToContainTextAsync(new[] { "text", "2" }, new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.ToHaveTextAsync(new[] { new Regex("^text 1$"), new Regex("text 2") }, new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.Not.ToHaveTextAsync(new[] { "text 1", "TEXT 2" }, new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
