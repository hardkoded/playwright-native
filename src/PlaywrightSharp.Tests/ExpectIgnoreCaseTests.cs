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
