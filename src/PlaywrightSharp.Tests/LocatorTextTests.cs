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
    /// GetAttribute, InnerText, InnerHTML, InputValue, Press, and Type on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorTextTests : PageTestEx
    {
        [PlaywrightTest("locator-convenience.spec.ts", "GetAttribute InnerText InnerHTML")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReadAttributeAndText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\" data-x=\"n\"><b>Hi</b></div>").ConfigureAwait(false);

            ILocator loc = page.Locator("#d");
            Assert.That(await loc.GetAttributeAsync("data-x").ConfigureAwait(false), Is.EqualTo("n"));
            Assert.That(await loc.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Hi"));
            Assert.That(await loc.InnerHTMLAsync().ConfigureAwait(false), Does.Contain("<b>Hi</b>"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "InputValue after Type")]
        [Test]
        [Timeout(30_000)]
        public async Task TypeShouldUpdateInputValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);

            await page.Locator("#n").TypeAsync("Ada").ConfigureAwait(false);

            Assert.That(await page.Locator("#n").InputValueAsync().ConfigureAwait(false), Is.EqualTo("Ada"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Press Enter submits")]
        [Test]
        [Timeout(30_000)]
        public async Task PressShouldDispatchTheKey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" onkeydown=\"if(event.key==='Enter') window.hit=true\" />").ConfigureAwait(false);

            await page.Locator("#n").PressAsync("Enter").ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "GetAttribute is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task GetAttributeShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\" data-x=\"a\"></div><div class=\"x\" data-x=\"b\"></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").GetAttributeAsync("data-x"));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
