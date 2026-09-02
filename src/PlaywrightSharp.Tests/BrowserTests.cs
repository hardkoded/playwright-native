/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// IBrowser Contexts, Disconnected, NewPageAsync, and options-bag NewContextAsync.
    /// </summary>
    [TestFixture]
    public class BrowserTests : PageTestEx
    {
        [PlaywrightTest("browser.spec.ts", "contexts should include created context")]
        [Test]
        [Timeout(30_000)]
        public async Task ContextsShouldIncludeCreatedContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Assert.That(browser.Contexts, Has.Exactly(1).Items);
            Assert.That(browser.Contexts, Does.Contain(context));
        }

        [PlaywrightTest("browser.spec.ts", "new context options bag")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldReturnUsableContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions()).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page, Is.Not.Null);
            Assert.That(await page.EvaluateAsync<int>("1 + 1").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("browser.spec.ts", "browser new page")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserNewPageShouldReturnUsablePage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);

            Assert.That(page, Is.Not.Null);
            Assert.That(page.IsClosed, Is.False);
            Assert.That(browser.Contexts, Has.Exactly(1).Items);
            Assert.That(await page.EvaluateAsync<int>("2 + 2").ConfigureAwait(false), Is.EqualTo(4));
        }

        [PlaywrightTest("browser.spec.ts", "disconnected after close")]
        [Test]
        [Timeout(30_000)]
        public async Task DisconnectedShouldFireOnClose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            TaskCompletionSource<IBrowser> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            browser.Disconnected += (_, closed) => tcs.TrySetResult(closed);

            await browser.CloseAsync().ConfigureAwait(false);

            using CancellationTokenSource cts = new(10_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IBrowser disconnected = await tcs.Task.ConfigureAwait(false);

            Assert.That(disconnected, Is.SameAs(browser));
            Assert.That(browser.IsConnected, Is.False);
        }

        [PlaywrightTest("browser.spec.ts", "CloseAsync reason is surfaced on later page errors")]
        [Test]
        [Timeout(30_000)]
        public async Task CloseReasonShouldSurfaceOnLaterPageErrors()
        {
            IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await browser.CloseAsync(new() { Reason = "wave-377-reason" }).ConfigureAwait(false);

            TargetClosedException ex = Assert.ThrowsAsync<TargetClosedException>(
                () => page.EvaluateAsync("1 + 1"));
            Assert.That(ex.Message, Does.Contain("wave-377-reason"));
            Assert.That(ex.CloseReason, Is.EqualTo("wave-377-reason"));
        }

        [PlaywrightTest("browser.spec.ts", "WaitForDisconnectedAsync after close")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForDisconnectedShouldResolveOnClose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            Task<IBrowser> waitTask = browser.WaitForDisconnectedAsync();
            await browser.CloseAsync().ConfigureAwait(false);
            IBrowser disconnected = await waitTask.ConfigureAwait(false);
            Assert.That(disconnected, Is.SameAs(browser));
            Assert.That(browser.IsConnected, Is.False);
        }

        [PlaywrightTest("browser.spec.ts", "WaitForDisconnectedAsync times out")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForDisconnectedShouldTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await browser.WaitForDisconnectedAsync(timeout: 200).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("browser.waitForEvent"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("browser.spec.ts", "BrowserType reports the launched engine")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserTypeShouldReportLaunchedEngine()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);

            Assert.That(browser.BrowserType, Is.Not.Null);
            string expected = TestConstants.IsWebKit
                ? "webkit"
                : TestConstants.IsFirefox
                    ? "firefox"
                    : "chromium";
            Assert.That(browser.BrowserType.Name, Is.EqualTo(expected));
        }
    }
}
