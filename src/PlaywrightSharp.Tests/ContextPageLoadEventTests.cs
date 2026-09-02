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
    /// Official <c>browserContext.on('pageload')</c>.
    /// </summary>
    [TestFixture]
    public class ContextPageLoadEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-page-event.spec.ts", "PageLoad fires when a page finishes loading")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextPageLoadShouldFireOnNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IPage loaded = await context.RunAndWaitForPageLoadAsync(
                () => page.GoToAsync("data:text/html,<html><body>wave448</body></html>")).ConfigureAwait(false);

            Assert.That(loaded, Is.SameAs(page));
        }
    }
}
