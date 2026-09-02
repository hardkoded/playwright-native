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
    /// Expect text, attribute, value, and id matchers.
    /// </summary>
    [TestFixture]
    public class ExpectTextTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveText waits until the text appears")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveTextShouldWaitUntilTheTextAppears()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello</div>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("#t")).ToHaveTextAsync("world", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').textContent = 'hello world'").ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAttribute ToHaveValue ToHaveId")]
        [Test]
        [Timeout(30_000)]
        public async Task AttributeValueAndIdShouldMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" data-x=\"ok\" value=\"Ada\" />").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#n"));
            await expect.ToHaveAttributeAsync("data-x", "ok").ConfigureAwait(false);
            await expect.ToHaveValueAsync("Ada").ConfigureAwait(false);
            await expect.ToHaveIdAsync("n").ConfigureAwait(false);
            await expect.ToHaveTextAsync(string.Empty).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveText and ToContainText match a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task TextMatchersShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\">hello world</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#t")).ToHaveTextAsync(new Regex("hello.*")).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).ToContainTextAsync(new Regex("wo\\w+")).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#t")).Not.ToHaveTextAsync(new Regex("^nope$"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAttribute and ToHaveValue match a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task AttributeAndValueShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" data-x=\"ok-42\" value=\"Ada Lovelace\" />").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#n"));
            await expect.ToHaveAttributeAsync("data-x", new Regex("ok-\\d+")).ConfigureAwait(false);
            await expect.ToHaveValueAsync(new Regex("^Ada")).ConfigureAwait(false);
            await expect.Not.ToHaveValueAsync(new Regex("^Zed"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveText list matches each locator in order")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveTextListShouldMatchEachLocatorInOrder()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<ul>" +
                "<li>Text 1</li>" +
                "<li>Text 2</li>" +
                "<li>Text 3</li>" +
                "</ul>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("li"));
            await expect.ToHaveTextAsync(new[] { "Text 1", "Text 2", "Text 3" }).ConfigureAwait(false);
            await expect.ToHaveTextAsync(new[] { new Regex("^Text 1$"), new Regex("2$"), new Regex("3") }).ConfigureAwait(false);
            await expect.Not.ToHaveTextAsync(new[] { "Text 1", "Text 3" }, new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveText list waits for a later item")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveTextListShouldWaitForALaterItem()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<ul id=\"l\"><li>Text 1</li></ul>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("li")).ToHaveTextAsync(new[] { "Text 1", "Text 2" }, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('l').insertAdjacentHTML('beforeend', '<li>Text 2</li>')")
                .ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }
    }
}
