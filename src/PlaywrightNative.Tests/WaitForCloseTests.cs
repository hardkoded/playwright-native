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
    /// Direct-connection tests for <see cref="IPage.WaitForCloseAsync"/>.
    /// </summary>
    [TestFixture]
    public class WaitForCloseTests : PageTestEx
    {
        [PlaywrightTest("page-basic.spec.ts", "WaitForCloseAsync resolves on CloseAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWhenPageCloses()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task waitTask = page.WaitForCloseAsync();
            await page.CloseAsync().ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);

            Assert.That(page.IsClosed, Is.True);
        }

        [PlaywrightTest("page-basic.spec.ts", "CloseAsync reason is surfaced on later errors")]
        [Test]
        [Timeout(30_000)]
        public async Task CloseReasonShouldSurfaceOnLaterErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.CloseAsync(new() { Reason = "wave-375-reason" }).ConfigureAwait(false);

            TargetClosedException ex = Assert.ThrowsAsync<TargetClosedException>(
                () => page.EvaluateAsync("1 + 1"));
            Assert.That(ex.Message, Does.Contain("wave-375-reason"));
            Assert.That(ex.CloseReason, Is.EqualTo("wave-375-reason"));
        }
    }
}
