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
    /// Official <c>browserContext.on('backgroundpage')</c>.
    /// </summary>
    [TestFixture]
    public class ContextBackgroundPageEventTests : PageTestEx
    {
        [PlaywrightTest("chromium.spec.ts", "BackgroundPage does not fire for ordinary pages")]
        [Test]
        [Timeout(30_000)]
        public async Task BackgroundPageShouldNotFireForOrdinaryPages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            bool fired = false;
            context.BackgroundPage += (_, _) => fired = true;

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave460</body></html>").ConfigureAwait(false);

            Assert.That(page, Is.Not.Null);
            Assert.That(fired, Is.False);
        }
    }
}
