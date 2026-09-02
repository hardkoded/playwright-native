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
    /// Official <c>page.clearConsoleMessages()</c> (Playwright v1.59).
    /// </summary>
    [TestFixture]
    public class ClearConsoleMessagesTests : PageTestEx
    {
        [PlaywrightTest("page-event-console.spec.ts", "ClearConsoleMessagesAsync drops recorded logs")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClearRecordedConsoleMessages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync<object>("console.log('before-clear')").ConfigureAwait(false);
            IReadOnlyList<IConsoleMessage> before = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(before.Select(item => item.Text), Does.Contain("before-clear"));

            await page.ClearConsoleMessagesAsync().ConfigureAwait(false);
            IReadOnlyList<IConsoleMessage> cleared = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(cleared.Select(item => item.Text), Does.Not.Contain("before-clear"));

            await page.EvaluateAsync<object>("console.log('after-clear')").ConfigureAwait(false);
            IReadOnlyList<IConsoleMessage> after = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(after.Select(item => item.Text), Does.Contain("after-clear"));
            Assert.That(after.Select(item => item.Text), Does.Not.Contain("before-clear"));
        }
    }
}
