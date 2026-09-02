/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>expect(locator).toHaveRole(AriaRole)</c>.
    /// </summary>
    [TestFixture]
    public class ExpectRoleAriaRoleTests : PageTestEx
    {
        [PlaywrightTest("expect-misc.spec.ts", "ToHaveRole matches AriaRole.Button")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveRoleShouldMatchAriaRoleButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"go\">Go</button>").ConfigureAwait(false);

            ILocatorAssertions expect = Assertions.Expect(page.Locator("#go"));
            await expect.ToHaveRoleAsync(AriaRole.Button).ConfigureAwait(false);
            await expect.Not.ToHaveRoleAsync(AriaRole.Link, new() { Timeout = 2000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "ToHaveRole matches a link")]
        [Test]
        [Timeout(30_000)]
        public async Task ToHaveRoleShouldMatchAriaRoleLink()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a id=\"go\" href=\"#\">Go</a>").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("#go")).ToHaveRoleAsync(AriaRole.Link).ConfigureAwait(false);
        }
    }
}
