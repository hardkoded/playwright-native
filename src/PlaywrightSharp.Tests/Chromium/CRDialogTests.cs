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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
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
