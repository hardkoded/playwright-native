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
using PlaywrightNative.Input;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <see cref="CRElementHandle.SelectOptionAsync"/> against
    /// real select elements via the direct CDP connection.
    /// </summary>
    [TestFixture]
    public class CRSelectTests : CRTestBase
    {
        private const string SingleSelectHtml = @"data:text/html,<select id='s'>
            <option value='a'>Apple</option>
            <option value='b'>Banana</option>
            <option value='c'>Cherry</option>
        </select>";

        private const string MultiSelectHtml = @"data:text/html,<select id='s' multiple>
            <option value='a'>Apple</option>
            <option value='b'>Banana</option>
            <option value='c'>Cherry</option>
        </select>";

        [PlaywrightTest("page-select-option.spec.ts", "should select by value")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSelectByValue()
        {
            await Page.GoToAsync(SingleSelectHtml).ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#s").ConfigureAwait(false);
            string[] result = await handle.SelectOptionAsync("b").ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(new[] { "b" }));
            string value = await Page.EvaluateAsync<string>("document.querySelector('#s').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("b"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select by label")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSelectByLabel()
        {
            await Page.GoToAsync(SingleSelectHtml).ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#s").ConfigureAwait(false);
            string[] result = await handle.SelectOptionAsync(
                new[] { new SelectOption { Label = "Cherry" } }).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(new[] { "c" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select by index")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSelectByIndex()
        {
            await Page.GoToAsync(SingleSelectHtml).ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#s").ConfigureAwait(false);
            string[] result = await handle.SelectOptionAsync(
                new[] { new SelectOption { Index = 0 } }).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(new[] { "a" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select multiple options in multi select")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSelectMultipleOptionsInMultiSelect()
        {
            await Page.GoToAsync(MultiSelectHtml).ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#s").ConfigureAwait(false);
            string[] result = await handle.SelectOptionAsync(new[] { "a", "c" }).ConfigureAwait(false);

            Assert.That(result, Is.EquivalentTo(new[] { "a", "c" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should only select first match in single select")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOnlySelectFirstMatchInSingleSelect()
        {
            await Page.GoToAsync(SingleSelectHtml).ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#s").ConfigureAwait(false);
            string[] result = await handle.SelectOptionAsync(new[] { "a", "b" }).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(new[] { "a" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should fire change and input events")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireChangeAndInputEvents()
        {
            await Page.GoToAsync(@"data:text/html,<select id='s'>
                <option value='a'>A</option>
                <option value='b'>B</option>
                </select>
                <script>
                window.events = [];
                const s = document.getElementById('s');
                s.addEventListener('input', () => window.events.push('input'));
                s.addEventListener('change', () => window.events.push('change'));
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#s").ConfigureAwait(false);
            await handle.SelectOptionAsync("b").ConfigureAwait(false);

            string json = await Page.EvaluateAsync<string>("JSON.stringify(window.events)").ConfigureAwait(false);
            Assert.That(json, Is.EqualTo("[\"input\",\"change\"]"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should return empty array when no match")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnEmptyArrayWhenNoMatch()
        {
            await Page.GoToAsync(SingleSelectHtml).ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#s").ConfigureAwait(false);
            string[] result = await handle.SelectOptionAsync("nonexistent").ConfigureAwait(false);

            Assert.That(result, Is.Empty);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should throw when element is not select")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenElementIsNotSelect()
        {
            await Page.GoToAsync("data:text/html,<div id='d'>not a select</div>").ConfigureAwait(false);
            await using CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.SelectOptionAsync("anything"));
            Assert.That(ex.Message, Does.Contain("select"));
        }
    }
}
