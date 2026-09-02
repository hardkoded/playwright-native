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
