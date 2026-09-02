/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page.consoleMessages({ filter })</c> (Playwright v1.59).
    /// </summary>
    [TestFixture]
    public class ConsoleMessagesFilterTests : PageTestEx
    {
        [PlaywrightTest("page-event-console.spec.ts", "since-navigation is the default")]
        [Test]
        [Timeout(30_000)]
        public async Task SinceNavigationFilterShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync<object>("console.log('before navigation')").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>("console.log('after navigation')").ConfigureAwait(false);

            IReadOnlyList<IConsoleMessage> all = await page.ConsoleMessagesAsync(new() { Filter = ConsoleMessagesFilter.All }).ConfigureAwait(false);
            Assert.That(all.Select(item => item.Text), Does.Contain("before navigation"));
            Assert.That(all.Select(item => item.Text), Does.Contain("after navigation"));

            IReadOnlyList<IConsoleMessage> sinceNav = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(sinceNav.Select(item => item.Text), Does.Not.Contain("before navigation"));
            Assert.That(sinceNav.Select(item => item.Text), Does.Contain("after navigation"));

            IReadOnlyList<IConsoleMessage> explicitSince = await page.ConsoleMessagesAsync(new() { Filter = ConsoleMessagesFilter.SinceNavigation }).ConfigureAwait(false);
            Assert.That(explicitSince.Select(item => item.Text), Does.Not.Contain("before navigation"));
            Assert.That(explicitSince.Select(item => item.Text), Does.Contain("after navigation"));
        }
    }
}
