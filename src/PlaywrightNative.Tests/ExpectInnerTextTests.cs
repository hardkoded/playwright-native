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
    /// Expect ToHaveText / ToContainText useInnerText.
    /// </summary>
    [TestFixture]
    public class ExpectInnerTextTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveText useInnerText skips hidden text")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveTextUseInnerTextShouldSkipHiddenText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"t\">Hello<span style=\"display:none\"> hidden</span> World</div>")
                .ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveTextAsync("Hello World", new() { UseInnerText = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).ToHaveTextAsync("Hello hidden World").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).Not.ToHaveTextAsync("Hello World", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToContainText useInnerText skips hidden text")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainTextUseInnerTextShouldSkipHiddenText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"t\">Hello<span style=\"display:none\">hidden</span> World</div>")
                .ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToContainTextAsync("Hello World", new() { UseInnerText = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).Not.ToContainTextAsync("hidden", new() { UseInnerText = true, Timeout = 2000 }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).ToContainTextAsync("hidden").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "useInnerText waits until innerText matches")]
        [Test]
        [Timeout(30_000)]
        public async Task UseInnerTextShouldWaitUntilInnerTextMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToHaveTextAsync("world", new() { Timeout = 5000, UseInnerText = true });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').textContent = 'world'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "useInnerText list and regex skip hidden text")]
        [Test]
        [Timeout(30_000)]
        public async Task UseInnerTextListAndRegexShouldSkipHiddenText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<ul>" +
                "<li>Text<span style=\"display:none\"> hidden</span> 1</li>" +
                "<li>Text 2</li>" +
                "</ul>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("li"));
            await expect.ToHaveTextAsync(new[] { "Text 1", "Text 2" }, new() { UseInnerText = true }).ConfigureAwait(false);
            await expect.ToContainTextAsync(new[] { "Text 1", "2" }, new() { UseInnerText = true }).ConfigureAwait(false);
            await expect.ToHaveTextAsync(new[] { new Regex("^Text 1$"), new Regex("2$") }, new() { UseInnerText = true }).ConfigureAwait(false);
            await expect.Not.ToHaveTextAsync(new[] { "Text 1", "Text 2" }, new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
