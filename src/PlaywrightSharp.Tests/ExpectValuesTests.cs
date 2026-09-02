/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Expect ToContainClass and ToHaveValues.
    /// </summary>
    [TestFixture]
    public class ExpectValuesTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToContainClass waits until all tokens are present")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainClassShouldWaitUntilAllTokensArePresent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" class=\"row\">x</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToContainClassAsync("row selected", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').classList.add('selected')").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToContainClass ignores token order")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainClassShouldIgnoreTokenOrder()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" class=\"middle selected row\">x</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToContainClassAsync("row middle").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).Not.ToContainClassAsync("gone", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToContainClass list matches each locator in order")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainClassListShouldMatchEachLocatorInOrder()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<ul id=\"w\">" +
                "<li class=\"a ready extra\">1</li>" +
                "<li class=\"b\">2</li>" +
                "<li class=\"c done\">3</li>" +
                "</ul>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#w li"));
            await expect.ToContainClassAsync(new[] { "ready a", "b", "done" }, new() { Timeout = 5000 }).ConfigureAwait(false);
            await expect.Not.ToContainClassAsync(new[] { "ready", "b" }, new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveValues waits until the multi-select matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveValuesShouldWaitUntilTheMultiSelectMatches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<select id=\"c\" multiple>" +
                "<option value=\"R\">Red</option>" +
                "<option value=\"G\">Green</option>" +
                "<option value=\"B\">Blue</option>" +
                "</select>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#c")).ToHaveValuesAsync(new[] { "R", "G" }, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.Locator("#c").SelectOptionAsync(new[] { "R", "G" }).ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveValues matches document order")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveValuesShouldMatchDocumentOrder()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<select id=\"c\" multiple>" +
                "<option value=\"R\">Red</option>" +
                "<option value=\"G\">Green</option>" +
                "<option value=\"B\">Blue</option>" +
                "</select>").ConfigureAwait(false);

            await page.Locator("#c").SelectOptionAsync(new[] { "B", "R" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).ToHaveValuesAsync(new[] { "R", "B" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).Not.ToHaveValuesAsync(new[] { "R" }, new() { Timeout = 2000 }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).ToHaveValuesAsync(new[] { new Regex("^R$"), new Regex("^B$") }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#c")).Not.ToHaveValuesAsync(new[] { new Regex("^R$") }, new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
