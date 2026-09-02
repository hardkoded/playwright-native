/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
        [PlaywrightTest("locator-element-handle.spec.ts", "Query selector should return element")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QuerySelectorShouldReturnElement()
        {
            await Page.GoToAsync("data:text/html,<button id='go'>Go</button>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#go").ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "Query selector should return null when no match")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task QuerySelectorShouldReturnNullWhenNoMatch()
        {
            await Page.GoToAsync("data:text/html,<div>Hi</div>").ConfigureAwait(false);

            CRElementHandle handle = await Page.QuerySelectorAsync("#not-there").ConfigureAwait(false);

            Assert.That(handle, Is.Null);
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "Focus should make element active")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FocusShouldMakeElementActive()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text'><input id='other'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FocusAsync().ConfigureAwait(false);

            string activeId = await Page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false);
            Assert.That(activeId, Is.EqualTo("t"));
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "Bounding box should return element geometry")]
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

        [PlaywrightTest("locator-element-handle.spec.ts", "Bounding box should return null for display none")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BoundingBoxShouldReturnNullForDisplayNone()
        {
            await Page.GoToAsync("data:text/html,<div id='d' style='display:none'>hidden</div>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);
            BoundingBox? box = await handle.BoundingBoxAsync().ConfigureAwait(false);

            Assert.That(box, Is.Null);
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "Click should fire event on element")]
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

        [PlaywrightTest("locator-element-handle.spec.ts", "Click should throw for invisible element")]
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

        [PlaywrightTest("locator-element-handle.spec.ts", "Dispose should release handle")]
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

        [PlaywrightTest("locator-element-handle.spec.ts", "Dispose should be idempotent")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DisposeShouldBeIdempotent()
        {
            await Page.GoToAsync("data:text/html,<div id='d'>x</div>").ConfigureAwait(false);
            CRElementHandle handle = await Page.QuerySelectorAsync("#d").ConfigureAwait(false);

            await handle.DisposeAsync().ConfigureAwait(false);
            Assert.DoesNotThrowAsync(() => handle.DisposeAsync().AsTask());
        }

        [PlaywrightTest("locator-element-handle.spec.ts", "Evaluate function should receive handle as argument")]
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
