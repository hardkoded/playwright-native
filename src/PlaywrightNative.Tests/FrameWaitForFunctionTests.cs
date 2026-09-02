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
    /// Direct-connection tests for <see cref="IFrame.WaitForFunctionAsync"/>.
    /// </summary>
    [TestFixture]
    public class FrameWaitForFunctionTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-function.spec.ts", "resolves when a child predicate becomes truthy")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveWhenPredicateBecomesTruthy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            Task<IJSHandle> waitTask = child.WaitForFunctionAsync("window.__FOO === 1");
            await child.EvaluateAsync("window.__FOO = 1").ConfigureAwait(false);
            IJSHandle handle = await waitTask.ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await page.EvaluateAsync<bool>("window.__FOO === 1").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "wait times out")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await child.WaitForFunctionAsync("false", options: new() { Timeout = 200 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("frame.waitForFunction"));
            Assert.That(ex.Message, Does.Contain("Timeout 200ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-function.spec.ts", "WaitForTimeout waits")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            await child.WaitForTimeoutAsync(300).ConfigureAwait(false);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(200));
        }

        private static async Task<IFrame> AttachBlankChildFrameAsync(IPage page)
        {
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                foreach (IFrame frame in page.MainFrame.ChildFrames)
                {
                    child = frame;
                    break;
                }

                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);

            for (int i = 0; i < 50; i++)
            {
                try
                {
                    if (!await child.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false))
                    {
                        return child;
                    }
                }
                catch (PlaywrightNativeException)
                {
                    // Execution context is not ready yet.
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Child frame execution context did not become ready.");
        }
    }
}
