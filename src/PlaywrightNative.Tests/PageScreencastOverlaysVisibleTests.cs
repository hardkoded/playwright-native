/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>screencast.showOverlays</c> / <c>hideOverlays</c>.
    /// </summary>
    [TestFixture]
    public class PageScreencastOverlaysVisibleTests : PageTestEx
    {
        [PlaywrightTest("screencast-overlay.spec.ts", "hideOverlays hides without removing")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHideOverlaysWithoutRemoving()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body>page</body>").ConfigureAwait(false);
            await using IAsyncDisposable overlay = await page.Screencast.ShowOverlayAsync(
                "<div id=\"pw-ov-mark\">wave-634</div>").ConfigureAwait(false);

            Assert.That(await page.Locator("#pw-ov-mark").IsVisibleAsync().ConfigureAwait(false), Is.True);
            await page.Screencast.HideOverlaysAsync().ConfigureAwait(false);
            Assert.That(await page.Locator("#pw-ov-mark").CountAsync().ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.Locator("#pw-ov-mark").IsVisibleAsync().ConfigureAwait(false), Is.False);
            await page.Screencast.ShowOverlaysAsync().ConfigureAwait(false);
            Assert.That(await page.Locator("#pw-ov-mark").IsVisibleAsync().ConfigureAwait(false), Is.True);
        }
    }
}
