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
    /// Integration tests for <see cref="CRElementHandle"/>: query, focus, bounding box,
    /// click, dispose.
    /// </summary>
    [TestFixture]
    public class CRElementHandleTests : CRTestBase
    {
        [PlaywrightTest("elementhandle-query-selector.spec.ts", "should query existing element")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QuerySelectorShouldReturnElement()
        {
            await Page.GoToAsync("data:text/html,<button id='go'>Go</button>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#go").ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "should return null for non-existing element")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QuerySelectorShouldReturnNullWhenNoMatch()
        {
            await Page.GoToAsync("data:text/html,<div>Hi</div>").ConfigureAwait(false);

            CRElementHandle handle = await Page.QuerySelectorAsync("#not-there").ConfigureAwait(false);

            Assert.That(handle, Is.Null);
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should focus a button")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FocusShouldMakeElementActive()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text'><input id='other'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FocusAsync().ConfigureAwait(false);

            string activeId = await Page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false);
            Assert.That(activeId, Is.EqualTo("t"));
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should work")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BoundingBoxShouldReturnElementGeometry()
        {
            await Page.GoToAsync(@"data:text/html,<div id='d' style='position:absolute;left:20px;top:30px;width:100px;height:50px'></div>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);
            BoundingBox? box = await handle.BoundingBoxAsync().ConfigureAwait(false);

            Assert.That(box, Is.Not.Null);
            BoundingBox b = box.Value;
            Assert.That(b.X, Is.EqualTo(20.0));
            Assert.That(b.Y, Is.EqualTo(30.0));
            Assert.That(b.Width, Is.EqualTo(100.0));
            Assert.That(b.Height, Is.EqualTo(50.0));
        }

        [PlaywrightTest("elementhandle-bounding-box.spec.ts", "should return null for invisible elements")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BoundingBoxShouldReturnNullForDisplayNone()
        {
            await Page.GoToAsync("data:text/html,<div id='d' style='display:none'>hidden</div>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);
            BoundingBox? box = await handle.BoundingBoxAsync().ConfigureAwait(false);

            Assert.That(box, Is.Null);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should work @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickShouldFireEventOnElement()
        {
            await Page.GoToAsync(@"data:text/html,<button id='b' style='position:absolute;left:50px;top:50px;width:80px;height:30px'>click</button>
                <script>window.clicked = false;
                document.getElementById('b').addEventListener('click', () => window.clicked = true);</script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await handle.ClickAsync().ConfigureAwait(false);

            bool clicked = await Page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false);
            Assert.That(clicked, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should throw for hidden nodes with force")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ClickShouldThrowForInvisibleElement()
        {
            await Page.GoToAsync("data:text/html,<button id='b' style='display:none'>x</button>").ConfigureAwait(false);
            CRElementHandle handle = await Page.QuerySelectorAsync("#b").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.ClickAsync());
            Assert.That(ex.Message, Does.Contain("no layout").Or.Contain("not visible"));

            await handle.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw if underlying element was disposed")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DisposeShouldReleaseHandle()
        {
            await Page.GoToAsync("data:text/html,<div id='d'>x</div>").ConfigureAwait(false);
            CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);

            await handle.DisposeAsync().ConfigureAwait(false);

            Assert.That(handle.IsDisposed, Is.True);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.FocusAsync());
            Assert.That(ex.Message, Does.Contain("disposed"));
        }

        [PlaywrightTest("elementhandle-misc.spec.ts", "should allow disposing twice")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DisposeShouldBeIdempotent()
        {
            await Page.GoToAsync("data:text/html,<div id='d'>x</div>").ConfigureAwait(false);
            CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);

            await handle.DisposeAsync().ConfigureAwait(false);
            Assert.DoesNotThrowAsync(() => handle.DisposeAsync().AsTask());
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should accept element handle as an argument")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task EvaluateFunctionShouldReceiveHandleAsArgument()
        {
            await Page.GoToAsync("data:text/html,<div id='d' data-value='42'>hi</div>").ConfigureAwait(false);
            await using CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);

            string value = await handle.EvaluateFunctionAsync<string>("node => node.getAttribute('data-value')").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("42"));
        }
    }
}
