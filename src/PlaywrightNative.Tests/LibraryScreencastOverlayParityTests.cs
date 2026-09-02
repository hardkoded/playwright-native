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
    /// Official <c>library/screencast-overlay.spec.ts</c> parity. Do not edit leftover
    /// <c>PageScreencastOverlay*.cs</c> classes.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryScreencastOverlayParityTests : PageTestEx
    {
        private static string EmptyPage => TestConstants.EmptyPage;

        private static async Task GoEmptyAsync(IPage page)
        {
            if (TestServerSetup.Server != null)
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                return;
            }

            await page.SetContentAsync("<html><body></body></html>").ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should add and remove overlay")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddAndRemoveOverlay()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            IAsyncDisposable disposable = await page.Screencast.ShowOverlayAsync("<div id=\"my-overlay\">Hello Overlay</div>")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-user-overlays")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#my-overlay")).ToHaveTextAsync("Hello Overlay").ConfigureAwait(false);

            await disposable.DisposeAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should add multiple overlays")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAddMultipleOverlays()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            IAsyncDisposable d1 = await page.Screencast.ShowOverlayAsync("<div id=\"overlay-1\">First</div>")
                .ConfigureAwait(false);
            IAsyncDisposable d2 = await page.Screencast.ShowOverlayAsync("<div id=\"overlay-2\">Second</div>")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#overlay-1")).ToHaveTextAsync("First").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#overlay-2")).ToHaveTextAsync("Second").ConfigureAwait(false);

            await d1.DisposeAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#overlay-2")).ToHaveTextAsync("Second").ConfigureAwait(false);

            await d2.DisposeAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should hide and show overlays")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHideAndShowOverlays()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            await page.Screencast.ShowOverlayAsync("<div id=\"my-overlay\">Visible</div>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-user-overlays")).ToBeVisibleAsync().ConfigureAwait(false);

            await page.Screencast.HideOverlaysAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-user-overlays")).ToBeHiddenAsync().ConfigureAwait(false);

            await page.Screencast.ShowOverlaysAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-user-overlays")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#my-overlay")).ToHaveTextAsync("Visible").ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should survive navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            await page.Screencast.ShowOverlayAsync("<div id=\"persistent\">Survives Reload</div>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#persistent")).ToHaveTextAsync("Survives Reload").ConfigureAwait(false);

            await GoEmptyAsync(page).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#persistent")).ToHaveTextAsync("Survives Reload").ConfigureAwait(false);

            await page.ReloadAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#persistent")).ToHaveTextAsync("Survives Reload").ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should remove overlay and not restore after navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveOverlayAndNotRestoreAfterNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            IAsyncDisposable disposable = await page.Screencast.ShowOverlayAsync("<div id=\"temp\">Temporary</div>")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#temp")).ToHaveTextAsync("Temporary").ConfigureAwait(false);

            await disposable.DisposeAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(0).ConfigureAwait(false);

            await page.ReloadAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should sanitize scripts from overlay html")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSanitizeScriptsFromOverlayHtml()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            await page.Screencast.ShowOverlayAsync("<div id=\"safe\">Safe</div><script>window.__injected = true</script>")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#safe")).ToHaveTextAsync("Safe").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<object>("() => window.__injected").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should strip event handlers from overlay html")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStripEventHandlersFromOverlayHtml()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            await page.Screencast.ShowOverlayAsync("<div id=\"clean\" onclick=\"window.__clicked=true\">Click me</div>")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#clean")).ToHaveTextAsync("Click me").ConfigureAwait(false);
            bool hasOnclick = await page.Locator("#clean").EvaluateAsync<bool>("el => el.hasAttribute('onclick')")
                .ConfigureAwait(false);
            Assert.That(hasOnclick, Is.False);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should auto-remove overlay after timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAutoRemoveOverlayAfterTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            await page.Screencast.ShowOverlayAsync("<div id=\"timed\">Temporary</div>", duration: 1).ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".x-pw-user-overlay")).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-overlay.spec.ts", "should allow styles in overlay html")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowStylesInOverlayHtml()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoEmptyAsync(page).ConfigureAwait(false);

            await page.Screencast.ShowOverlayAsync("<div id=\"styled\" style=\"color: red; font-size: 20px;\">Styled</div>")
                .ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#styled")).ToHaveTextAsync("Styled").ConfigureAwait(false);
            string color = await page.Locator("#styled").EvaluateAsync<string>("el => getComputedStyle(el).color")
                .ConfigureAwait(false);
            Assert.That(color, Is.EqualTo("rgb(255, 0, 0)"));
        }
    }
}
