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
    /// Direct-connection tests for <see cref="IElementHandle.ScrollIntoViewIfNeededAsync"/>.
    /// </summary>
    [TestFixture]
    public class ScrollIntoViewTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "ScrollIntoViewIfNeededAsync brings an offscreen element into view")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBringOffscreenElementIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:2000px\"></div><div id=\"t\">target</div>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            var before = await target.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(before, Is.Not.Null);
            Assert.That(before.Y, Is.GreaterThan(400));

            await target.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);

            var after = await target.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(after, Is.Not.Null);
            Assert.That(after.Y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(after.Y, Is.LessThan(400f));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "page ScrollIntoViewIfNeededAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollFromPageSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:2000px\"></div><div id=\"t\">target</div>").ConfigureAwait(false);

            await page.ScrollIntoViewIfNeededAsync("#t").ConfigureAwait(false);
            var after = await (await page.QuerySelectorAsync("#t").ConfigureAwait(false))
                .BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(after, Is.Not.Null);
            Assert.That(after.Y, Is.LessThan(400f));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "frame ScrollIntoViewIfNeededAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollFromMainFrameSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:2000px\"></div><div id=\"t\">target</div>").ConfigureAwait(false);

            await page.MainFrame.ScrollIntoViewIfNeededAsync("#t").ConfigureAwait(false);
            var after = await (await page.QuerySelectorAsync("#t").ConfigureAwait(false))
                .BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(after, Is.Not.Null);
            Assert.That(after.Y, Is.LessThan(400f));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "page ScrollIntoViewIfNeededAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageScrollIntoViewIfNeededAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.ScrollIntoViewIfNeededAsync(".nope", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "page ScrollIntoViewIfNeededAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageScrollIntoViewIfNeededAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(500, 400).ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\" style=\"height:2000px\"></div>").ConfigureAwait(false);

            Task scrollTask = page.ScrollIntoViewIfNeededAsync("#t", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.body.insertAdjacentHTML('beforeend', '<div id=\"t\">target</div>')")
                .ConfigureAwait(false);
            await scrollTask.ConfigureAwait(false);

            var after = await (await page.QuerySelectorAsync("#t").ConfigureAwait(false))
                .BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(after, Is.Not.Null);
            Assert.That(after.Y, Is.LessThan(400f));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "ScrollIntoViewIfNeededAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">target</div>").ConfigureAwait(false);
            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => target.ScrollIntoViewIfNeededAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "ScrollIntoViewIfNeededAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none\">target</div>").ConfigureAwait(false);
            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);

            Task scrollTask = target.ScrollIntoViewIfNeededAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').style.display = 'block'").ConfigureAwait(false);
            await scrollTask.ConfigureAwait(false);

            Assert.That(await target.IsVisibleAsync().ConfigureAwait(false), Is.True);
        }
    }
}
