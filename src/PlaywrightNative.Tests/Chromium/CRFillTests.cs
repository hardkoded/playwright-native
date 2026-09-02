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
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <see cref="CRElementHandle.FillAsync"/> against input and textarea elements.
    /// </summary>
    [TestFixture]
    public class CRFillTests : CRTestBase
    {
        [PlaywrightTest("page-fill.spec.ts", "should fill text input")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFillTextInput()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FillAsync("hello").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#t').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("hello"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should replace existing value")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReplaceExistingValue()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text' value='old'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FillAsync("new").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#t').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("new"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should clear with empty string")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClearWithEmptyString()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text' value='abc'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FillAsync(string.Empty).ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#t').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill textarea @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFillTextarea()
        {
            await Page.GoToAsync("data:text/html,<textarea id='ta'></textarea>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#ta").ConfigureAwait(false);
            await handle.FillAsync("multi\nline").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#ta').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("multi\nline"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill email input")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFillEmailInput()
        {
            await Page.GoToAsync("data:text/html,<input id='e' type='email'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#e").ConfigureAwait(false);
            await handle.FillAsync("a@b.com").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#e').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("a@b.com"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill number input")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFillNumberInput()
        {
            await Page.GoToAsync("data:text/html,<input id='n' type='number'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#n").ConfigureAwait(false);
            await handle.FillAsync("42").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#n').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("42"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fire input event on fill")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireInputEventOnFill()
        {
            await Page.GoToAsync(@"data:text/html,<input id='t' type='text'>
                <script>
                window.events = [];
                document.getElementById('t').addEventListener('input', e => window.events.push('input'));
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FillAsync("abc").ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.events.length").ConfigureAwait(false);
            Assert.That(count, Is.GreaterThanOrEqualTo(1));
        }

        [PlaywrightTest("page-fill.spec.ts", "should focus element during fill")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFocusElementDuringFill()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text'><input id='other'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FillAsync("focused").ConfigureAwait(false);

            string activeId = await Page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false);
            Assert.That(activeId, Is.EqualTo("t"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should throw when element is not fillable")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenElementIsNotFillable()
        {
            await Page.GoToAsync("data:text/html,<div id='d'>not fillable</div>").ConfigureAwait(false);
            await using CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.FillAsync("anything"));
            Assert.That(ex.Message, Does.Contain("input").Or.Contain("textarea"));
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill unicode text")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFillUnicodeText()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FillAsync("日本語 ¡ñ 🎉").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#t').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("日本語 ¡ñ 🎉"));
        }
    }
}
