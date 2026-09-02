/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>getByRole({ description })</c>.
    /// </summary>
    [TestFixture]
    public class GetByRoleDescriptionTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "Matches aria-description")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldMatchAriaDescription()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"a\" aria-description=\"Save file\">Save</button>" +
                "<button id=\"b\" aria-description=\"Close dialog\">Close</button>").ConfigureAwait(false);

            ILocator save = page.GetByRole("button", description: "Save file");
            Assert.That(await save.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await save.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("a"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Matches aria-describedby")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldMatchAriaDescribedBy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"hint\">Upload a photo</div>" +
                "<button id=\"u\" aria-describedby=\"hint\">Upload</button>").ConfigureAwait(false);

            Assert.That(
                await page.GetByRole("button", description: "Upload a photo").GetAttributeAsync("id").ConfigureAwait(false),
                Is.EqualTo("u"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Exact description does not substring-match")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleDescriptionExactShouldNotSubstringMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\" aria-description=\"Save file\">Save</button>").ConfigureAwait(false);

            Assert.That(await page.GetByRole("button", description: "Save").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.GetByRole("button", exact: true, description: "Save").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.GetByRole("button", exact: true, description: "Save file").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "Locator GetByRole is scoped")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorGetByRoleDescriptionShouldStayInside()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<section id=\"one\"><button aria-description=\"Save file\">A</button></section>" +
                "<section id=\"two\"><button aria-description=\"Save file\">B</button></section>").ConfigureAwait(false);

            ILocator inner = page.Locator("#two").GetByRole("button", description: "Save file");
            Assert.That(await inner.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That((await inner.TextContentAsync().ConfigureAwait(false)).Trim(), Is.EqualTo("B"));
        }
    }
}
