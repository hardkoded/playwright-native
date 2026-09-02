/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for mouse-based drag: <see cref="PlaywrightNative.Input.Mouse.DragToAsync"/>
    /// and <see cref="CRElementHandle.DragToAsync"/>.
    /// </summary>
    [TestFixture]
    public class CRDragTests : CRTestBase
    {
        [PlaywrightTest("page-drag.spec.ts", "MouseDragToAsync should fire mousemove events during drag")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MouseDragToAsyncShouldFireMousemoveEventsDuringDrag()
        {
            await Page.GoToAsync(@"data:text/html,<script>
                window.events = [];
                document.addEventListener('mousedown', () => window.events.push('down'));
                document.addEventListener('mousemove', e => {
                    if (e.buttons) window.events.push('move');
                });
                document.addEventListener('mouseup', () => window.events.push('up'));
                </script>").ConfigureAwait(false);

            await Page.Mouse.DragToAsync(10, 10, 100, 100, steps: 5).ConfigureAwait(false);

            int downs = await Page.EvaluateAsync<int>("window.events.filter(e => e === 'down').length").ConfigureAwait(false);
            int movesWithButton = await Page.EvaluateAsync<int>("window.events.filter(e => e === 'move').length").ConfigureAwait(false);
            int ups = await Page.EvaluateAsync<int>("window.events.filter(e => e === 'up').length").ConfigureAwait(false);

            Assert.That(downs, Is.EqualTo(1));
            Assert.That(ups, Is.EqualTo(1));
            Assert.That(movesWithButton, Is.GreaterThanOrEqualTo(5));
        }

        [PlaywrightTest("page-drag.spec.ts", "ElementHandleDragToAsync should move from source to target")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementHandleDragToAsyncShouldMoveFromSourceToTarget()
        {
            await Page.GoToAsync(@"data:text/html,
                <div id='src' style='position:absolute;left:10px;top:10px;width:20px;height:20px;background:red'></div>
                <div id='dst' style='position:absolute;left:200px;top:200px;width:20px;height:20px;background:blue'></div>
                <script>
                window.downX = null; window.downY = null;
                window.upX = null; window.upY = null;
                document.addEventListener('mousedown', e => { window.downX = e.clientX; window.downY = e.clientY; });
                document.addEventListener('mouseup', e => { window.upX = e.clientX; window.upY = e.clientY; });
                </script>").ConfigureAwait(false);

            await using CRElementHandle src = await Page.QuerySelectorAsync("#src").ConfigureAwait(false);
            await using CRElementHandle dst = await Page.QuerySelectorAsync("#dst").ConfigureAwait(false);

            await src.DragToAsync(dst).ConfigureAwait(false);

            int downX = await Page.EvaluateAsync<int>("window.downX").ConfigureAwait(false);
            int downY = await Page.EvaluateAsync<int>("window.downY").ConfigureAwait(false);
            int upX = await Page.EvaluateAsync<int>("window.upX").ConfigureAwait(false);
            int upY = await Page.EvaluateAsync<int>("window.upY").ConfigureAwait(false);

            // Source center ~ (20, 20); target center ~ (210, 210).
            Assert.That(downX, Is.InRange(18, 22));
            Assert.That(downY, Is.InRange(18, 22));
            Assert.That(upX, Is.InRange(208, 212));
            Assert.That(upY, Is.InRange(208, 212));
        }

        [PlaywrightTest("page-drag.spec.ts", "DragToAsync should throw when source has no layout")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DragToAsyncShouldThrowWhenSourceHasNoLayout()
        {
            await Page.GoToAsync(@"data:text/html,
                <div id='src' style='display:none'></div>
                <div id='dst' style='position:absolute;left:50px;top:50px;width:20px;height:20px'></div>").ConfigureAwait(false);

            await using CRElementHandle dst = await Page.QuerySelectorAsync("#dst").ConfigureAwait(false);
            CRElementHandle src = await Page.QuerySelectorAsync("#src").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => src.DragToAsync(dst));
            Assert.That(ex.Message, Does.Contain("layout").Or.Contain("visible"));

            await src.DisposeAsync().ConfigureAwait(false);
        }
    }
}
