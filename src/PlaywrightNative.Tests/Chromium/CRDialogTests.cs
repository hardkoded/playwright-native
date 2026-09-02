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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for the <c>Page.javascriptDialogOpening</c> event and
    /// <c>CRDialog.AcceptAsync</c>/<c>DismissAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRDialogTests : CRTestBase
    {
        [PlaywrightTest("page-dialog.spec.ts", "should handle alert")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleAlert()
        {
            string capturedMessage = null;
            Page.DialogOpening += async (_, dialog) =>
            {
                capturedMessage = dialog.Message;
                await dialog.AcceptAsync().ConfigureAwait(false);
            };

            await Page.GoToAsync("data:text/html,<script>alert('hi there');</script>").ConfigureAwait(false);

            // Alert dispatches during parse; give CDP a beat to dispatch the event.
            await Task.Delay(300).ConfigureAwait(false);

            Assert.That(capturedMessage, Is.EqualTo("hi there"));
        }

        [PlaywrightTest("page-dialog.spec.ts", "should accept confirm")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptConfirm()
        {
            Page.DialogOpening += async (_, dialog) => await dialog.AcceptAsync().ConfigureAwait(false);

            await Page.GoToAsync("data:text/html,<script>window.__answer = confirm('ok?');</script>").ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);

            bool answer = await Page.EvaluateAsync<bool>("window.__answer").ConfigureAwait(false);
            Assert.That(answer, Is.True);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should dismiss confirm")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDismissConfirm()
        {
            Page.DialogOpening += async (_, dialog) => await dialog.DismissAsync().ConfigureAwait(false);

            await Page.GoToAsync("data:text/html,<script>window.__answer = confirm('ok?');</script>").ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);

            bool answer = await Page.EvaluateAsync<bool>("window.__answer").ConfigureAwait(false);
            Assert.That(answer, Is.False);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should accept prompt with text")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptPromptWithText()
        {
            Page.DialogOpening += async (_, dialog) =>
            {
                Assert.That(dialog.Type, Is.EqualTo("prompt"));
                Assert.That(dialog.DefaultValue, Is.EqualTo("default"));
                await dialog.AcceptAsync("injected").ConfigureAwait(false);
            };

            await Page.GoToAsync("data:text/html,<script>window.__answer = prompt('name?', 'default');</script>").ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);

            string answer = await Page.EvaluateAsync<string>("window.__answer").ConfigureAwait(false);
            Assert.That(answer, Is.EqualTo("injected"));
        }
    }
}
