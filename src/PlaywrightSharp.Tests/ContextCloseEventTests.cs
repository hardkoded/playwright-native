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
    /// Direct-connection tests for <see cref="IBrowserContext.Close"/>.
    /// </summary>
    [TestFixture]
    public class ContextCloseEventTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-basic.spec.ts", "WaitForEvent Close resolves on CloseAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireCloseOnCloseAsync()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Task<IBrowserContext> waitTask = context.WaitForEventAsync(BrowserContextEvent.Close);
            await context.CloseAsync().ConfigureAwait(false);
            IBrowserContext fromEvent = await waitTask.ConfigureAwait(false);

            Assert.That(fromEvent, Is.SameAs(context));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "Close fires once")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireCloseOnce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            int count = 0;
            context.Close += (_, _) => count++;

            await context.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);

            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "IsClosed is true after CloseAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task IsClosedShouldBeTrueAfterCloseAsync()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Assert.That(context.IsClosed, Is.False);
            await context.CloseAsync().ConfigureAwait(false);
            Assert.That(context.IsClosed, Is.True);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "CloseAsync reason is surfaced on later page errors")]
        [Test]
        [Timeout(30_000)]
        public async Task CloseReasonShouldSurfaceOnLaterPageErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await context.CloseAsync(new() { Reason = "wave-376-reason" }).ConfigureAwait(false);

            TargetClosedException ex = Assert.ThrowsAsync<TargetClosedException>(
                () => page.EvaluateAsync("1 + 1"));
            Assert.That(ex.Message, Does.Contain("wave-376-reason"));
            Assert.That(ex.CloseReason, Is.EqualTo("wave-376-reason"));
        }
    }
}
