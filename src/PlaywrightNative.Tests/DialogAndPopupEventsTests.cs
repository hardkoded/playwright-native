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
    /// Integration tests for page Dialog and page Popup events.
    /// Wires Dialog/Popup on top of the lifecycle events.
    /// </summary>
    [TestFixture]
    public class DialogAndPopupEventsTests : PageTestEx
    {
        [PlaywrightTest("page-dialog.spec.ts", "Dialog event should fire for alert")]
        [Test]
        [Timeout(30_000)]
        public async Task DialogEventShouldFireForAlert()
        {
            BrowserLauncher.SkipUnlessChromium();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IDialog> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Dialog += (_, dialog) => tcs.TrySetResult(dialog);

            // Fire-and-forget the navigation — alert blocks parse, dialog event resolves TCS.
            _ = page.GoToAsync("data:text/html,<script>alert('hello');</script>");

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IDialog received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Page, Is.SameAs(page));
            Assert.That(received.Message, Is.EqualTo("hello"));
            Assert.That(received.Type, Is.EqualTo("alert"));
            await received.AcceptAsync(null).ConfigureAwait(false);
        }

        [PlaywrightTest("page-dialog.spec.ts", "Dialog can be dismissed")]
        [Test]
        [Timeout(30_000)]
        public async Task DialogCanBeDismissed()
        {
            BrowserLauncher.SkipUnlessChromium();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += async (_, dialog) => await dialog.DismissAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<script>window.__answer = confirm('ok?');</script>").ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);

            bool answer = await page.EvaluateAsync<bool>("window.__answer").ConfigureAwait(false);
            Assert.That(answer, Is.False);
        }

        [PlaywrightTest("page-dialog.spec.ts", "Popup event should fire on window open")]
        [Test]
        [Timeout(30_000)]
        public async Task PopupEventShouldFireOnWindowOpen()
        {
            BrowserLauncher.SkipUnlessChromium();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IPage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Popup += (_, popup) => tcs.TrySetResult(popup);

            await page.GoToAsync("data:text/html,<button id='b'>x</button>").ConfigureAwait(false);
            await page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IPage popup = await tcs.Task.ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
        }

        [PlaywrightTest("page-dialog.spec.ts", "Popup page should be usable")]
        [Test]
        [Timeout(30_000)]
        public async Task PopupPageShouldBeUsable()
        {
            BrowserLauncher.SkipUnlessChromium();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IPage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Popup += (_, popup) => tcs.TrySetResult(popup);

            await page.GoToAsync("data:text/html,<div>opener</div>").ConfigureAwait(false);
            await page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IPage popup = await tcs.Task.ConfigureAwait(false);

            // The popup needs time to initialize before it accepts Evaluate calls.
            await Task.Delay(500).ConfigureAwait(false);

            int two = await popup.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
            Assert.That(two, Is.EqualTo(2));
        }

    }
}
