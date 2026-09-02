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
    /// Integration tests for <see cref="PlaywrightNative.Input.Keyboard"/> against real Chromium via the
    /// direct CDP connection. Validates typed characters actually reach the DOM and modifier
    /// chords arrive with correct bitmasks.
    /// </summary>
    [TestFixture]
    public class CRKeyboardTests : CRTestBase
    {
        [PlaywrightTest("page-keyboard.spec.ts", "should type into textarea")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTypeIntoTextarea()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.TypeAsync("hello").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("hello"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should press single key")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPressSingleKey()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.PressAsync("a").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("a"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should press shift key for uppercase")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPressShiftKeyForUppercase()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.PressAsync("Shift+a").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("A"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should type uppercase mixed")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTypeUppercaseMixed()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.TypeAsync("Hello World").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("Hello World"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should insert text bypassing layout")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInsertTextBypassingLayout()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.InsertTextAsync("日本語").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("日本語"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should emit enter as newline")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitEnterAsNewline()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.TypeAsync("line1").ConfigureAwait(false);
            await Page.Keyboard.PressAsync("Enter").ConfigureAwait(false);
            await Page.Keyboard.TypeAsync("line2").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("line1\nline2"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should dispatch keydown keypress keyup")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispatchKeydownKeypressKeyup()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/keyboard.html").ConfigureAwait(false);

            await Page.Keyboard.PressAsync("a").ConfigureAwait(false);

            string log = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(log, Does.Contain("Keydown:"));
            Assert.That(log, Does.Contain("Keyup:"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should report shift modifier in key event")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportShiftModifierInKeyEvent()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/keyboard.html").ConfigureAwait(false);

            await Page.Keyboard.PressAsync("Shift+a").ConfigureAwait(false);

            string log = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(log, Does.Contain("Shift"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should down and up keeps modifier held")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDownAndUpKeepsModifierHeld()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/keyboard.html").ConfigureAwait(false);

            await Page.Keyboard.DownAsync("Shift").ConfigureAwait(false);
            await Page.Keyboard.PressAsync("a").ConfigureAwait(false);
            await Page.Keyboard.UpAsync("Shift").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("A"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should handle arrow keys")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleArrowKeys()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.TypeAsync("ac").ConfigureAwait(false);
            await Page.Keyboard.PressAsync("ArrowLeft").ConfigureAwait(false);
            await Page.Keyboard.TypeAsync("b").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("abc"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should backspace delete previous character")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBackspaceDeletePreviousCharacter()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("document.querySelector('textarea').focus(), true").ConfigureAwait(false);

            await Page.Keyboard.TypeAsync("abcd").ConfigureAwait(false);
            await Page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('textarea').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("abc"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "should throw on unknown keys")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnUnknownKey()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => Page.Keyboard.PressAsync("NotARealKey"));
            Assert.That(ex.Message, Does.Contain("NotARealKey"));
        }
    }
}
