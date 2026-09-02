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
    /// GetBy* chained from an <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorGetByChainTests : PageTestEx
    {
        [PlaywrightTest("selectors-get-by.spec.ts", "GetByRole is scoped to the locator")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByRoleShouldStayInsideTheLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div class=\"card\"><button id=\"a\">Save</button></div>" +
                "<div class=\"card\"><button id=\"b\">Save</button></div>").ConfigureAwait(false);

            await page.Locator(".card").Nth(1).GetByRole("button", name: "Save").ClickAsync().ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("b"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByText and GetByTestId")]
        [Test]
        [Timeout(30_000)]
        public async Task GetByTextAndTestIdShouldResolve()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<section id=\"one\"><p>Hello world</p><button data-testid=\"go\">X</button></section>" +
                "<section id=\"two\"><p>Hello world</p><button data-testid=\"go\">Y</button></section>").ConfigureAwait(false);

            ILocator one = page.Locator("#one");
            Assert.That(await one.GetByText("Hello world").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await one.GetByTestId("go").TextContentAsync().ConfigureAwait(false), Is.EqualTo("X"));
        }

        [PlaywrightTest("selectors-get-by.spec.ts", "GetByLabel GetByPlaceholder GetByAltText GetByTitle")]
        [Test]
        [Timeout(30_000)]
        public async Task AttributeGetByShouldStayInsideTheLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<form id=\"f\">" +
                "<label for=\"n\">Name</label><input id=\"n\" placeholder=\"Your name\" />" +
                "<img alt=\"Logo\" title=\"Company\" />" +
                "</form>" +
                "<input id=\"other\" placeholder=\"Your name\" />").ConfigureAwait(false);

            ILocator form = page.Locator("#f");
            Assert.That(await form.GetByLabel("Name").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await form.GetByPlaceholder("Your name").GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await form.GetByAltText("Logo").GetAttributeAsync("alt").ConfigureAwait(false), Is.EqualTo("Logo"));
            Assert.That(await form.GetByTitle("Company").GetAttributeAsync("title").ConfigureAwait(false), Is.EqualTo("Company"));
        }
    }
}
