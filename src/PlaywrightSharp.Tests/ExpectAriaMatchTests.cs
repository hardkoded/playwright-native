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
    /// Expect ToHaveAccessibleDescription and ToMatchAriaSnapshot.
    /// </summary>
    [TestFixture]
    public class ExpectAriaMatchTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToMatchAriaSnapshot contains the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ToMatchAriaSnapshotShouldContainTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#go")).ToMatchAriaSnapshotAsync("button").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#go")).ToMatchAriaSnapshotAsync("Go").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleDescription matches aria-describedby")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveAccessibleDescriptionShouldMatchDescribedBy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"go\" aria-describedby=\"h\">Go</button><span id=\"h\">Hint text</span>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#go")).ToHaveAccessibleDescriptionAsync("Hint", new() { Timeout = 5000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveAccessibleDescription matches a regex")]
        [Test]
        [Timeout(30_000)]
        public async Task AccessibleDescriptionShouldMatchARegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"go\" aria-describedby=\"h\">Go</button><span id=\"h\">Hint text</span>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#go"));
            await expect.ToHaveAccessibleDescriptionAsync(new Regex("Hint")).ConfigureAwait(false);
            await expect.ToHaveAccessibleDescriptionAsync(new Regex("^Hint text$")).ConfigureAwait(false);
            await expect.Not.ToHaveAccessibleDescriptionAsync(new Regex("^missing$"), new() { Timeout = 2000 }).ConfigureAwait(false);
        }
    }
}
