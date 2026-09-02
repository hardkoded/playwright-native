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
    /// Official <c>browserContext.backgroundPages()</c>.
    /// </summary>
    [TestFixture]
    public class ContextBackgroundPagesTests : PageTestEx
    {
        [PlaywrightTest("chromium.spec.ts", "BackgroundPages is empty without extensions")]
        [Test]
        [Timeout(30_000)]
        public async Task BackgroundPagesShouldBeEmptyWithoutExtensions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("data:text/html,<html><body>wave459</body></html>").ConfigureAwait(false);

            Assert.That(context.Pages, Does.Contain(page));
            Assert.That(context.BackgroundPages, Is.Empty);
        }
    }
}
