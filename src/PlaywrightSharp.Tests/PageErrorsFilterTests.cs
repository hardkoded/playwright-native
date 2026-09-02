/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page.pageErrors({ filter })</c> (Playwright v1.59).
    /// </summary>
    [TestFixture]
    public class PageErrorsFilterTests : PageTestEx
    {
        [PlaywrightTest("page-event-pageerror.spec.ts", "since-navigation is the default")]
        [Test]
        [Timeout(30_000)]
        public async Task SinceNavigationFilterShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<string> firstWait = page.WaitForPageErrorAsync();
            await page.GoToAsync("data:text/html,<script>throw new Error('page1 error');</script>").ConfigureAwait(false);
            await firstWait.ConfigureAwait(false);

            Task<string> secondWait = page.WaitForPageErrorAsync();
            await page.GoToAsync("data:text/html,<script>throw new Error('page2 error');</script>").ConfigureAwait(false);
            await secondWait.ConfigureAwait(false);

            IReadOnlyList<string> all = await page.PageErrorsAsync(PageErrorsFilter.All).ConfigureAwait(false);
            Assert.That(string.Join("\n", all), Does.Contain("page1 error"));
            Assert.That(string.Join("\n", all), Does.Contain("page2 error"));

            IReadOnlyList<string> sinceNav = await page.PageErrorsAsync().ConfigureAwait(false);
            Assert.That(string.Join("\n", sinceNav), Does.Not.Contain("page1 error"));
            Assert.That(string.Join("\n", sinceNav), Does.Contain("page2 error"));

            IReadOnlyList<string> explicitSince = await page.PageErrorsAsync(PageErrorsFilter.SinceNavigation).ConfigureAwait(false);
            Assert.That(string.Join("\n", explicitSince), Does.Not.Contain("page1 error"));
            Assert.That(string.Join("\n", explicitSince), Does.Contain("page2 error"));
        }
    }
}
