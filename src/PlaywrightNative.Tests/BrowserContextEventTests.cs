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
    /// Official <c>browser.on('context')</c>.
    /// </summary>
    [TestFixture]
    public class BrowserContextEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-events.spec.ts", "Context fires when NewContextAsync creates a context")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldFireOnNewContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            Task<IBrowserContext> waitTask = browser.WaitForContextAsync();
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IBrowserContext received = await waitTask.ConfigureAwait(false);

            Assert.That(received, Is.SameAs(context));
            await context.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "Context fires when NewPageAsync creates an implicit context")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextShouldFireOnNewPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext received = null;
            browser.Context += (_, ctx) => received = ctx;

            IPage page = await browser.NewPageAsync().ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received, Is.SameAs(page.Context));
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
