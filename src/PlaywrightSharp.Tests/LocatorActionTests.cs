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
    /// Hover, DblClick, Focus, and Tap on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorActionTests : PageTestEx
    {
        [PlaywrightTest("locator-convenience.spec.ts", "Locator HoverAsync dispatches mouseover")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverShouldDispatchMouseOver()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\" onmouseover=\"window.hov=true\" style=\"width:80px;height:40px\">x</div>").ConfigureAwait(false);

            await page.Locator("#d").HoverAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.hov === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Locator DblClickAsync dispatches dblclick")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickShouldDispatchDblClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" ondblclick=\"window.dbl=true\">Go</button>").ConfigureAwait(false);

            await page.Locator("#b").DblClickAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.dbl === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Locator FocusAsync focuses the element")]
        [Test]
        [Timeout(30_000)]
        public async Task FocusShouldFocusTheElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"name\" /><input id=\"other\" />").ConfigureAwait(false);

            await page.Locator("#name").FocusAsync().ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("name"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Locator TapAsync taps with hasTouch")]
        [Test]
        [Timeout(30_000)]
        public async Task TapShouldDispatchATap()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" ontouchstart=\"window.tapped=true\">Go</button>").ConfigureAwait(false);

            await page.Locator("#b").TapAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.tapped === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "Locator HoverAsync is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class=\"x\">a</div><div class=\"x\">b</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".x").HoverAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
