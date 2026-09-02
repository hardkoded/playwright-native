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
    /// Direct-connection tests for <see cref="IPage.DragAndDropAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageDragAndDropTests : PageTestEx
    {
        [PlaywrightTest("page-drag.spec.ts", "DragAndDrop moves the mouse from source to target")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDragFromSourceToTarget()
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

            await page.DragAndDropAsync("#src", "#dst").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("dropped"));
        }

        [PlaywrightTest("page-drag.spec.ts", "frame DragAndDrop moves the mouse from source to target")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameShouldDragFromSourceToTarget()
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

            await page.MainFrame.DragAndDropAsync("#src", "#dst").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("dropped"));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragAndDrop times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='src' style='visibility:hidden;width:80px;height:80px;background:#c00'>src</div>
                <div id='dst' style='width:80px;height:80px;background:#0c0'>dst</div>")
                .ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.DragAndDropAsync("#src", "#dst", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragAndDrop force drags while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='src' style='visibility:hidden;width:80px;height:80px;background:#c00'>src</div>
                <div id='dst' style='width:80px;height:80px;background:#0c0'>dst</div>
                <script>
                  let dragging = false;
                  document.addEventListener('mousedown', e => {
                    const r = document.getElementById('src').getBoundingClientRect();
                    if (e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom) {
                      dragging = true;
                    }
                  });
                  document.getElementById('dst').addEventListener('mouseup', () => {
                    if (dragging) document.getElementById('dst').textContent = 'dropped';
                  });
                </script>").ConfigureAwait(false);

            await page.DragAndDropAsync("#src", "#dst", new() { Force = true }).ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("dropped"));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragAndDrop trial does not drop")]
        [Test]
        [Timeout(30_000)]
        public async Task TrialShouldNotDispatchTheDrop()
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

            await page.DragAndDropAsync("#src", "#dst", new() { Trial = true }).ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("dst"));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragAndDrop steps interpolates the mouse path")]
        [Test]
        [Timeout(30_000)]
        public async Task StepsShouldInterpolateTheMousePath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='src' style='position:absolute;left:0;top:0;width:40px;height:40px;background:#c00'>src</div>
                <div id='dst' style='position:absolute;left:240px;top:0;width:40px;height:40px;background:#0c0'>dst</div>
                <script>
                  window.moves = 0;
                  document.addEventListener('mousemove', () => { window.moves++; });
                </script>").ConfigureAwait(false);

            await page.DragAndDropAsync("#src", "#dst", new PageDragAndDropOptions { Steps = 20 }).ConfigureAwait(false);
            int count = await page.EvaluateAsync<int>("window.moves").ConfigureAwait(false);
            Assert.That(count, Is.GreaterThanOrEqualTo(20));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragAndDrop Auto scrolls the target into view")]
        [Test]
        [Timeout(30_000)]
        public async Task AutoShouldScrollTheTargetIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='box' style='width:240px;height:120px;overflow:auto;border:1px solid #000'>
                  <div id='src' style='width:80px;height:80px;background:#c00'>src</div>
                  <div style='height:600px'></div>
                  <div id='dst' style='width:80px;height:80px;background:#0c0'>dst</div>
                </div>
                <script>
                  let dragging = false;
                  document.getElementById('src').addEventListener('mousedown', () => { dragging = true; });
                  document.getElementById('dst').addEventListener('mouseup', () => {
                    if (dragging) document.getElementById('dst').textContent = 'dropped';
                  });
                </script>").ConfigureAwait(false);

            double before = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            await page.DragAndDropAsync("#src", "#dst").ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);

            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.GreaterThan(0));
            Assert.That(text, Is.EqualTo("dropped"));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragAndDrop scroll None does not scroll")]
        [Test]
        [Timeout(30_000)]
        public async Task ScrollNoneShouldNotScrollThePage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div id='box' style='width:240px;height:120px;overflow:auto;border:1px solid #000'>
                  <div id='src' style='width:80px;height:80px;background:#c00'>src</div>
                  <div style='height:600px'></div>
                  <div id='dst' style='width:80px;height:80px;background:#0c0'>dst</div>
                </div>").ConfigureAwait(false);

            await page.DragAndDropAsync("#src", "#dst", new PageDragAndDropOptions { Scroll = ScrollMode.None }).ConfigureAwait(false);
            double after = await page.EvaluateAsync<double>("document.getElementById('box').scrollTop").ConfigureAwait(false);
            Assert.That(after, Is.EqualTo(0));
        }
    }
}
