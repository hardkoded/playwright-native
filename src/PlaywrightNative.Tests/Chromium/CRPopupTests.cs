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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
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
