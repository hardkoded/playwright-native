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
    /// Official <c>page-click-timeout-1.spec.ts</c> parity for click timeouts.
    /// Skipped (Node-only internals):
    /// <c>should avoid side effects after timeout</c> uses
    /// <c>__testHookBeforePointerAction</c>.
    /// </summary>
    [TestFixture]
    public class PageClickTimeout1ParityTests : PageTestEx
    {
        [PlaywrightTest("page-click-timeout-1.spec.ts", "should timeout waiting for button to be enabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutWaitingForButtonToBeEnabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button onclick=\"javascript:window.__CLICKED=true;\" disabled><span>Click target</span></button>")
                .ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.ClickAsync("text=Click target", new() { Timeout = 3000 }));
            Assert.That(await page.EvaluateAsync<object>("window.__CLICKED").ConfigureAwait(false), Is.Null);
            Assert.That(error.Message, Does.Contain("page.click: Timeout 3000ms exceeded."));
            Assert.That(error.Message, Does.Contain("element is not enabled"));
            Assert.That(error.Message, Does.Contain("retrying click action"));
        }
    }
}
