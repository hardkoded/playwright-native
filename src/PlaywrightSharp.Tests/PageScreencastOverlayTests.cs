/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>screencast.showOverlay</c>.
    /// </summary>
    [TestFixture]
    public class PageScreencastOverlayTests : PageTestEx
    {
        [PlaywrightTest("screencast-overlay.spec.ts", "showOverlay injects HTML")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldInjectOverlayHtml()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body>page</body>").ConfigureAwait(false);

            await using IAsyncDisposable overlay = await page.Screencast.ShowOverlayAsync(
                "<div id=\"pw-ov-mark\">wave-631</div>").ConfigureAwait(false);

            Assert.That(await page.Locator("#pw-ov-mark").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(
                await page.Locator("#pw-ov-mark").TextContentAsync().ConfigureAwait(false),
                Is.EqualTo("wave-631"));
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "disposing showOverlay removes HTML")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRemoveOverlayWhenDisposed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body>page</body>").ConfigureAwait(false);

            await using (IAsyncDisposable overlay = await page.Screencast.ShowOverlayAsync(
                "<div id=\"pw-ov-mark\">wave-631</div>").ConfigureAwait(false))
            {
                Assert.That(await page.Locator("#pw-ov-mark").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            }

            Assert.That(await page.Locator("#pw-ov-mark").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }
    }
}
