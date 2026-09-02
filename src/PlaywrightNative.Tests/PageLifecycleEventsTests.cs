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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
