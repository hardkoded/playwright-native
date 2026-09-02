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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    /// <summary>
    /// Integration tests for popup (new-tab) detection via <c>CRPage.PopupOpened</c>.
    /// </summary>
    [TestFixture]
    public class CRPopupTests : CRTestBase
    {
        [PlaywrightTest("popup.spec.ts", "should fire popup opened on window open")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFirePopupOpenedOnWindowOpen()
        {
            TaskCompletionSource<CRPage> popupTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Page.PopupOpened += (_, popupPage) => popupTcs.TrySetResult(popupPage);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => popupTcs.TrySetCanceled());

            CRPage popup = await popupTcs.Task.ConfigureAwait(false);
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.TargetId, Is.Not.EqualTo(Page.TargetId));
        }

        [PlaywrightTest("popup.spec.ts", "Popup page should be usable for evaluation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PopupPageShouldBeUsableForEvaluation()
        {
            TaskCompletionSource<CRPage> popupTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Page.PopupOpened += (_, popupPage) => popupTcs.TrySetResult(popupPage);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => popupTcs.TrySetCanceled());

            CRPage popup = await popupTcs.Task.ConfigureAwait(false);
            await popup.InitializedTask.ConfigureAwait(false);

            int two = await popup.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
            Assert.That(two, Is.EqualTo(2));
        }
    }
}
