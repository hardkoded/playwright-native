/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IBrowserContext.Page"/>.
    /// </summary>
    [TestFixture]
    public class ContextPageEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-page-event.spec.ts", "WaitForEvent Page resolves on NewPageAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFirePageOnNewPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Task<IPage> waitTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IPage fromEvent = await waitTask.ConfigureAwait(false);

            Assert.That(fromEvent, Is.SameAs(page));
        }

        [PlaywrightTest("browsercontext-page-event.spec.ts", "WaitForEvent Page resolves on window.open")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFirePageOnWindowOpen()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IPage> waitTask = context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);
            IPage popup = await waitTask.ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
            Assert.That(popup, Is.Not.SameAs(page));
            Assert.That(context.Pages, Does.Contain(popup));
        }
    }
}
