/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation. 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
