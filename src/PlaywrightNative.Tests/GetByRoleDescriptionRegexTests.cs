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
    /// Official <c>getByRole({ description })</c> regular-expression form.
    /// </summary>
    [TestFixture]
    public class GetByRoleDescriptionRegexTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "Matches aria-description")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldMatchDescriptionRegex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"a\" aria-description=\"Save file\">Save</button>" +
                "<button id=\"b\" aria-description=\"Close dialog\">Close</button>").ConfigureAwait(false);

            ILocator save = page.GetByRole("button", descriptionRegex: new Regex("^Save"));
            Assert.That(await save.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await save.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "IgnoreCase matches")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleDescriptionRegexShouldHonorIgnoreCase()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\" aria-description=\"Save file\">Save</button>").ConfigureAwait(false);

            Assert.That(
                await page.GetByRole("button", descriptionRegex: new Regex("SAVE FILE", RegexOptions.IgnoreCase)).CountAsync().ConfigureAwait(false),
                Is.EqualTo(1));
            Assert.That(
                await page.GetByRole("button", descriptionRegex: new Regex("SAVE FILE")).CountAsync().ConfigureAwait(false),
                Is.EqualTo(0));
        }
    }
}
