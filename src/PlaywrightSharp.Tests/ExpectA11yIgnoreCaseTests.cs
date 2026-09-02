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
    /// Expect accessible name / description / error message ignoreCase.
    /// </summary>
    [TestFixture]
    public class ExpectA11yIgnoreCaseTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleName ignoreCase matches mixed case")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAccessibleNameIgnoreCaseShouldMatchMixedCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\" aria-label=\"Submit Form\">Go</button>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#go"));
            await expect.ToHaveAccessibleNameAsync("submit form", new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.ToHaveAccessibleNameAsync(new Regex("^submit form$"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.Not.ToHaveAccessibleNameAsync("submit form", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleDescription ignoreCase matches mixed case")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAccessibleDescriptionIgnoreCaseShouldMatchMixedCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"go\" aria-describedby=\"d\">Go</button>" +
                "<div id=\"d\">Help Text</div>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#go"));
            await expect.ToHaveAccessibleDescriptionAsync("help text", new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.ToHaveAccessibleDescriptionAsync(new Regex("^help text$"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.Not.ToHaveAccessibleDescriptionAsync("help text", new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleErrorMessage ignoreCase matches mixed case")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAccessibleErrorMessageIgnoreCaseShouldMatchMixedCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<input id=\"n\" aria-invalid=\"true\" aria-errormessage=\"err\" />" +
                "<div id=\"err\">Hello</div>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#n"));
            await expect.ToHaveAccessibleErrorMessageAsync("hello", new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.ToHaveAccessibleErrorMessageAsync(new Regex("^hello$"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await expect.Not.ToHaveAccessibleErrorMessageAsync("hello", new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
