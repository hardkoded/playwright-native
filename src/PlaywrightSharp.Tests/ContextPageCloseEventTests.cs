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
    /// Official <c>browserContext.on('pageclose')</c>.
    /// </summary>
    [TestFixture]
    public class ContextPageCloseEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-page-event.spec.ts", "PageClose fires when a page is closed")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextPageCloseShouldFireOnPageClose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IPage closed = await context.RunAndWaitForPageCloseAsync(() => page.CloseAsync()).ConfigureAwait(false);

            Assert.That(closed, Is.SameAs(page));
            Assert.That(page.IsClosed, Is.True);
        }
    }
}
