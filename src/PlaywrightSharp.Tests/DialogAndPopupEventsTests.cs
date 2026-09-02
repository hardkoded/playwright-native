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
