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
    /// <see cref="IPage.AddLocatorHandlerAsync(ILocator, System.Func{ILocator, System.Threading.Tasks.Task}, int?, bool?)"/>.
    /// </summary>
    [TestFixture]
    public class LocatorHandlerTests : PageTestEx
    {
        [PlaywrightTest("page-add-locator-handler.spec.ts", "Handler dismisses an overlay before click")]
        [Test]
        [Timeout(30_000)]
        public async Task HandlerShouldDismissOverlayBeforeClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await SetOverlayPageAsync(page).ConfigureAwait(false);

            await page.AddLocatorHandlerAsync(
                page.Locator("#overlay"),
                async (ILocator overlay) =>
                {
                    await overlay.Locator("#ok").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

            await page.Locator("#go").ClickAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("#overlay").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "Times removes the handler")]
        [Test]
        [Timeout(30_000)]
        public async Task TimesShouldRemoveTheHandler()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await SetOverlayPageAsync(page).ConfigureAwait(false);

            int calls = 0;
            await page.AddLocatorHandlerAsync(
                page.Locator("#overlay"),
                async () =>
                {
                    calls++;
                    await page.Locator("#ok").ClickAsync().ConfigureAwait(false);
                },
                times: 1).ConfigureAwait(false);

            await page.Locator("#go").ClickAsync().ConfigureAwait(false);
            Assert.That(calls, Is.EqualTo(1));

            await page.EvaluateAsync<object>(
                @"document.body.insertAdjacentHTML('beforeend',
                    '<div id=""overlay"" style=""position:fixed;inset:0;background:#0003""><button id=""ok"">OK</button></div>')").ConfigureAwait(false);

            await page.Locator("#ok").ClickAsync().ConfigureAwait(false);
            Assert.That(calls, Is.EqualTo(1));
        }

        [PlaywrightTest("page-add-locator-handler.spec.ts", "RemoveLocatorHandler stops the handler")]
        [Test]
        [Timeout(30_000)]
        public async Task RemoveShouldStopTheHandler()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await SetOverlayPageAsync(page).ConfigureAwait(false);

            int calls = 0;
            ILocator overlay = page.Locator("#overlay");
            await page.AddLocatorHandlerAsync(
                overlay,
                async () =>
                {
                    calls++;
                    await page.Locator("#ok").ClickAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
            await page.RemoveLocatorHandlerAsync(overlay).ConfigureAwait(false);

            await page.Locator("#ok").ClickAsync().ConfigureAwait(false);
            Assert.That(calls, Is.EqualTo(0));
        }

        private static Task SetOverlayPageAsync(IPage page)
            => page.SetContentAsync(
                "<button id=\"go\" onclick=\"window.hit=true\">Go</button>" +
                "<div id=\"overlay\" style=\"position:fixed;inset:0;background:#0003\">" +
                "<button id=\"ok\" onclick=\"this.parentElement.remove()\">OK</button>" +
                "</div>");
    }
}
