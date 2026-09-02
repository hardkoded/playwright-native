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
    /// Official <c>locator.normalize()</c>.
    /// </summary>
    [TestFixture]
    public class LocatorNormalizeTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "Normalize prefers test id")]
        [Test]
        [Timeout(30_000)]
        public async Task NormalizeShouldPreferTestId()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"old\" data-testid=\"save\">Save</button>").ConfigureAwait(false);

            ILocator normalized = await page.Locator("#old").NormalizeAsync().ConfigureAwait(false);
            await page.EvalOnSelectorAsync<bool>("#old", "el => { el.removeAttribute('id'); return true; }").ConfigureAwait(false);

            Assert.That(await normalized.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That((await normalized.TextContentAsync().ConfigureAwait(false)).Trim(), Is.EqualTo("Save"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Normalize prefers alt text")]
        [Test]
        [Timeout(30_000)]
        public async Task NormalizeShouldPreferAltText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<img class=\"x\" alt=\"Cat\" />").ConfigureAwait(false);

            ILocator normalized = await page.Locator(".x").NormalizeAsync().ConfigureAwait(false);
            await page.EvalOnSelectorAsync<bool>("img", "el => { el.removeAttribute('class'); return true; }").ConfigureAwait(false);

            Assert.That(await normalized.CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await normalized.GetAttributeAsync("alt").ConfigureAwait(false), Is.EqualTo("Cat"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Normalize is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task NormalizeShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\"></div><div class=\"x\"></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").NormalizeAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
