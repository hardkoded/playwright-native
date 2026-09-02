/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForEventAsync{T}(PlaywrightEvent{T}, Func{T, bool}, float?)"/>.
    /// </summary>
    [TestFixture]
    public class WaitForEventTests : PageTestEx
    {
        [PlaywrightTest("page-event-popup.spec.ts", "wait for Console then log")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForConsole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IConsoleMessage> waitTask = page.WaitForEventAsync(PageEvent.Console);
            await page.EvaluateAsync<object>("console.log('wave72')").ConfigureAwait(false);
            IConsoleMessage received = await waitTask.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Text, Does.Contain("wave72"));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "wait for Dialog then alert")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForDialog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IDialog> waitTask = page.WaitForEventAsync(PageEvent.Dialog);
            _ = page.GoToAsync("data:text/html,<script>alert('wave72');</script>");
            IDialog dialog = await waitTask.ConfigureAwait(false);

            Assert.That(dialog, Is.Not.Null);
            Assert.That(dialog.Message, Is.EqualTo("wave72"));
            await dialog.AcceptAsync(null).ConfigureAwait(false);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "wait times out")]
        [Test]
        [Timeout(30_000)]
        public void ShouldTimeoutWaitingForEvent()
        {
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.WaitForEventAsync(PageEvent.Download, timeout: 200).ConfigureAwait(false);
            });

            Assert.That(ex.Message, Does.Contain("page.waitForEvent"));
        }
    }
}
