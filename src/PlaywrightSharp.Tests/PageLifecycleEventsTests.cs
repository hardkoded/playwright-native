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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Integration tests for the page-level lifecycle events on page:
    /// Load, DOMContentLoaded, Close. No new wrapper types required
    /// (payload is IPage / self).
    /// </summary>
    [TestFixture]
    public class PageLifecycleEventsTests : PageTestEx
    {
        [PlaywrightTest("page-event-load.spec.ts", "LoadEventShouldFireAfterGoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task LoadEventShouldFireAfterGoTo()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Load += (_, _) => tcs.TrySetResult(true);

            await page.GoToAsync("data:text/html,<div>loaded</div>").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetResult(false));
            bool fired = await tcs.Task.ConfigureAwait(false);
            Assert.That(fired, Is.True);
        }

        [PlaywrightTest("page-event-load.spec.ts", "DOMContentLoadedShouldFireAfterGoTo")]
        [Test]
        [Timeout(30_000)]
        public async Task DOMContentLoadedShouldFireAfterGoTo()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.DOMContentLoaded += (_, _) => tcs.TrySetResult(true);

            await page.GoToAsync("data:text/html,<div>loaded</div>").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetResult(false));
            bool fired = await tcs.Task.ConfigureAwait(false);
            Assert.That(fired, Is.True);
        }

        [PlaywrightTest("page-event-load.spec.ts", "CloseEventShouldFireOnPageClose")]
        [Test]
        [Timeout(30_000)]
        public async Task CloseEventShouldFireOnPageClose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Close += (_, _) => tcs.TrySetResult(true);

            await page.CloseAsync().ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetResult(false));
            bool fired = await tcs.Task.ConfigureAwait(false);
            Assert.That(fired, Is.True);
        }

    }
}
