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
    /// DragTo on <see cref="ILocator"/>.
    /// </summary>
    [TestFixture]
    public class LocatorDragToTests : PageTestEx
    {
        [PlaywrightTest("page-drag.spec.ts", "DragTo moves the mouse from source to target")]
        [Test]
        [Timeout(30_000)]
        public async Task DragToShouldMoveFromSourceToTarget()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='src' style='width:80px;height:80px;background:#c00'>src</div>
                <div id='dst' style='width:80px;height:80px;background:#0c0'>dst</div>
                <script>
                  let dragging = false;
                  document.getElementById('src').addEventListener('mousedown', () => { dragging = true; });
                  document.getElementById('dst').addEventListener('mouseup', () => {
                    if (dragging) document.getElementById('dst').textContent = 'dropped';
                  });
                </script>").ConfigureAwait(false);

            await page.Locator("#src").DragToAsync(page.Locator("#dst")).ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("dropped"));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragTo is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task DragToShouldThrowWhenTwoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div class='src' style='width:80px;height:80px;background:#c00'>a</div>
                <div class='src' style='width:80px;height:80px;background:#c00'>b</div>
                <div id='dst' style='width:80px;height:80px;background:#0c0'>dst</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator(".src").DragToAsync(page.Locator("#dst")));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
