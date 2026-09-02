/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForLoadStateAsync"/>.
    /// Mirrors a first-match subset of upstream <c>page-wait-for-load-state.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class WaitForLoadStateTests : PageTestEx
    {
        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of about:blank")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyWhenAlreadyLoaded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>loaded</div>").ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of already loaded page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForDOMContentLoadedWhenAlreadyReached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>loaded</div>").ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for networkidle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReachNetworkIdleAfterGoTo()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>idle</div>").ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should timeout waiting for load")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForLoad()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            // After `load`, networkidle is a 500ms quiet period. A short wait for
            // networkidle in that window must time out with the upstream message.
            TaskCompletionSource<bool> loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Load += (_, _) => loaded.TrySetResult(true);
            Task gotoTask = page.GoToAsync("data:text/html,<div>timeout-idle</div>");
            await loaded.Task.ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 100 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForLoadState"));
            Assert.That(ex.Message, Does.Contain("Timeout 100ms exceeded."));

            await gotoTask.ConfigureAwait(false);
        }
    }
}
