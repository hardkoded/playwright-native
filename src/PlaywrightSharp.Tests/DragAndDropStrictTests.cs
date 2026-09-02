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
    /// Official <c>page.dragAndDrop({ strict })</c>.
    /// </summary>
    [TestFixture]
    public class DragAndDropStrictTests : PageTestEx
    {
        [PlaywrightTest("page-drag.spec.ts", "strict true throws when two sources match")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldThrowWhenTwoSourcesMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class='src'>a</div><div class='src'>b</div><div id='dst'>dst</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.DragAndDropAsync(".src", "#dst", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("page-drag.spec.ts", "strict true accepts unique selectors")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictTrueShouldAcceptUniqueSelectors()
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

            await page.DragAndDropAsync("#src", "#dst", new() { Strict = true }).ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("dropped"));
        }

        [PlaywrightTest("page-drag.spec.ts", "strict false overrides context StrictSelectors")]
        [Test]
        [Timeout(30_000)]
        public async Task StrictFalseShouldOverrideContextStrictSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                StrictSelectors = true,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <div class='src' id='first' style='width:80px;height:80px;background:#c00'>src</div>
                <div class='src' style='width:80px;height:80px;background:#c00'>other</div>
                <div id='dst' style='width:80px;height:80px;background:#0c0'>dst</div>
                <script>
                  let dragging = false;
                  document.getElementById('first').addEventListener('mousedown', () => { dragging = true; });
                  document.getElementById('dst').addEventListener('mouseup', () => {
                    if (dragging) document.getElementById('dst').textContent = 'dropped';
                  });
                </script>").ConfigureAwait(false);

            await page.DragAndDropAsync(".src", "#dst", new() { Strict = false }).ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.getElementById('dst').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("dropped"));
        }

        [PlaywrightTest("page-drag.spec.ts", "frame honors strict")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameShouldHonorStrict()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div class='src'>a</div><div class='src'>b</div><div id='dst'>dst</div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.MainFrame.DragAndDropAsync(".src", "#dst", new() { Strict = true }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
        }
    }
}
