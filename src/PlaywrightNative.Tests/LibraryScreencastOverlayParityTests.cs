/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
        private const string DialogPage = @"
    <dialog id=""target"" style=""position: fixed; inset: 0; margin: 0; width: 100vw; height: 100vh; padding: 0; border: 0; background: rgb(200, 200, 200);""></dialog>
    <button onclick=""target.showModal()"">Open</button>
  ";

        private const string PopoverPage = @"
    <div id=""target"" popover=""manual"" style=""inset: 0; margin: 0; width: 100%; height: 100%; max-width: none; max-height: none; padding: 0; border: 0; background: rgb(200, 200, 200);""></div>
    <button onclick=""target.showPopover()"">Open</button>
  ";

        private const string RedOverlay = "<div style=\"position: absolute; top: 50px; left: 50px; width: 200px; height: 100px; background: rgb(255, 0, 0);\"></div>";

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

        [PlaywrightTest("screencast-overlay.spec.ts", "should show overlay above dialog opened before it")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldShowOverlayAboveDialogOpenedBeforeIt()
            => ShouldShowOverlayAboveTopLayerOpenedBeforeItAsync(DialogPage);

        [PlaywrightTest("screencast-overlay.spec.ts", "should keep overlay above dialog opened after it")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldKeepOverlayAboveDialogOpenedAfterIt()
            => ShouldKeepOverlayAboveTopLayerOpenedAfterItAsync(DialogPage);

        [PlaywrightTest("screencast-overlay.spec.ts", "should show overlay above popover opened before it")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldShowOverlayAbovePopoverOpenedBeforeIt()
            => ShouldShowOverlayAboveTopLayerOpenedBeforeItAsync(PopoverPage);

        [PlaywrightTest("screencast-overlay.spec.ts", "should keep overlay above popover opened after it")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldKeepOverlayAbovePopoverOpenedAfterIt()
            => ShouldKeepOverlayAboveTopLayerOpenedAfterItAsync(PopoverPage);

        private static async Task ShouldShowOverlayAboveTopLayerOpenedBeforeItAsync(string content)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(content).ConfigureAwait(false);
            IAsyncDisposable disposable = await page.Screencast.ShowOverlayAsync("<div></div>").ConfigureAwait(false);
            await disposable.DisposeAsync().ConfigureAwait(false);

            await page.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync().ConfigureAwait(false);
            Assert.That(Pixel(await page.ScreenshotAsync().ConfigureAwait(false), 100, 100), Is.EqualTo(new[] { 200, 200, 200 }));

            await page.Screencast.ShowOverlayAsync(RedOverlay).ConfigureAwait(false);
            Assert.That(Pixel(await page.ScreenshotAsync().ConfigureAwait(false), 100, 100), Is.EqualTo(new[] { 255, 0, 0 }));

            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task ShouldKeepOverlayAboveTopLayerOpenedAfterItAsync(string content)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(content).ConfigureAwait(false);
            await page.Screencast.ShowOverlayAsync(RedOverlay).ConfigureAwait(false);
            Assert.That(Pixel(await page.ScreenshotAsync().ConfigureAwait(false), 100, 100), Is.EqualTo(new[] { 255, 0, 0 }));

            await page.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync().ConfigureAwait(false);
            int[] pixel = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            do
            {
                pixel = Pixel(await page.ScreenshotAsync().ConfigureAwait(false), 100, 100);
                if (pixel.SequenceEqual(new[] { 255, 0, 0 }))
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
            while (DateTime.UtcNow < deadline);

            Assert.That(pixel, Is.EqualTo(new[] { 255, 0, 0 }));

            await context.CloseAsync().ConfigureAwait(false);
        }

        private static int[] Pixel(byte[] screenshot, int x, int y)
        {
            using Image<Rgba32> png = Image.Load<Rgba32>(screenshot);
            Rgba32 color = png[x, y];
            return new int[] { color.R, color.G, color.B };
        }
    }
}
