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
    /// Upstream <c>page-listeners.spec.ts</c> parity for official
    /// <c>page.removeAllListeners(event, { behavior })</c>.
    /// </summary>
    [TestFixture]
    public class PageListenersParityTests : PageTestEx
    {
        [PlaywrightTest("page-listeners.spec.ts", "should not throw with ignoreErrors")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowWithIgnoreErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> reachedHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Console += async (_, _) =>
            {
                reachedHandler.TrySetResult(true);
                await releaseHandler.Task.ConfigureAwait(false);
                throw new Exception("Error in console handler");
            };
            await page.EvaluateAsync("console.log(1)").ConfigureAwait(false);
            await reachedHandler.Task.ConfigureAwait(false);
            await page.RemoveAllListenersAsync("console", "ignoreErrors").ConfigureAwait(false);
            releaseHandler.TrySetResult(true);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
        }

        [PlaywrightTest("page-listeners.spec.ts", "should wait")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWait()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> reachedHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int value = 0;
            page.Console += async (_, _) =>
            {
                reachedHandler.TrySetResult(true);
                value = 42;
            };
            await page.EvaluateAsync("console.log(1)").ConfigureAwait(false);
            await reachedHandler.Task.ConfigureAwait(false);
            Task removePromise = page.RemoveAllListenersAsync("console", "wait");
            releaseHandler.TrySetResult(true);
            await removePromise.ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(42));
        }

        [PlaywrightTest("page-listeners.spec.ts", "wait should throw")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitShouldThrow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> reachedHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Console += async (_, _) =>
            {
                reachedHandler.TrySetResult(true);
                await releaseHandler.Task.ConfigureAwait(false);
                throw new Exception("Error in handler");
            };
            await page.EvaluateAsync("console.log(1)").ConfigureAwait(false);
            await reachedHandler.Task.ConfigureAwait(false);
            Task removePromise = page.RemoveAllListenersAsync("console", "wait");
            releaseHandler.TrySetResult(true);
            Exception error = Assert.CatchAsync(() => removePromise);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Error in handler"));
        }
    }
}
