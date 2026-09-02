/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Expect ToHaveAccessibleErrorMessage and list ToContainText.
    /// </summary>
    [TestFixture]
    public class ExpectErrorTextListTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleErrorMessage reads aria-errormessage")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAccessibleErrorMessageShouldReadAriaErrorMessage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<input id=\"n\" aria-invalid=\"true\" aria-errormessage=\"err\" />" +
                "<div id=\"err\">Hello</div>" +
                "<div>This should not be considered.</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#n")).ToHaveAccessibleErrorMessageAsync("Hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#n")).Not.ToHaveAccessibleErrorMessageAsync("considered", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleErrorMessage matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task AccessibleErrorMessageShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<input id=\"n\" aria-invalid=\"true\" aria-errormessage=\"err\" />" +
                "<div id=\"err\">Hello</div>" +
                "<div>This should not be considered.</div>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#n"));
            await expect.ToHaveAccessibleErrorMessageAsync(new Regex("^Hel+o$")).ConfigureAwait(false);
            await expect.Not.ToHaveAccessibleErrorMessageAsync(new Regex("considered"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleErrorMessage ignores valid controls")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAccessibleErrorMessageShouldIgnoreValidControls()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<input id=\"n\" aria-invalid=\"false\" aria-errormessage=\"err\" />" +
                "<div id=\"err\">Error message</div>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#n")).Not.ToHaveAccessibleErrorMessageAsync("Error message", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToContainText list matches in document order")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainTextListShouldMatchInDocumentOrder()
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

            await Assertions.Expect(page.Locator("li")).ToContainTextAsync(new[] { "Text 1", "Text 3" }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("li")).Not.ToContainTextAsync(new[] { "Text 3", "Text 1" }, new() { Timeout = 2000 }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("li")).ToContainTextAsync(new[] { new Regex("1$"), new Regex("3$") }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("li")).Not.ToContainTextAsync(new[] { new Regex("3$"), new Regex("1$") }, new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToContainText list waits for a later item")]
        [Test]
        [Timeout(30_000)]
        public async Task ToContainTextListShouldWaitForALaterItem()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<ul id=\"l\"><li>Text 1</li></ul>").ConfigureAwait(false);

            Task assertTask = Assertions.Expect(page.Locator("li")).ToContainTextAsync(new[] { "Text 1", "Text 3" }, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('l').insertAdjacentHTML('beforeend', '<li>Text 3</li>')")
                .ConfigureAwait(false);
            await assertTask.ConfigureAwait(false);
        }
    }
}
