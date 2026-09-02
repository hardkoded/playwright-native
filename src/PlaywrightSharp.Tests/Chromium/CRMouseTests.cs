/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.Input;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <see cref="PlaywrightSharp.Input.Mouse"/>.
    /// Uses simple inline HTML so tests are self-contained and don't depend on
    /// complex external fixtures.
    /// </summary>
    [TestFixture]
    public class CRMouseTests : CRTestBase
    {
        [PlaywrightTest("page-mouse.spec.ts", "should click the document @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheDocument()
        {
            await Page.GoToAsync("data:text/html,<script>window.clickCount = 0; document.addEventListener('click', () => window.clickCount++);</script>").ConfigureAwait(false);

            await Page.Mouse.ClickAsync(50, 60).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.clickCount").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should report coordinates")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportCoordinates()
        {
            await Page.GoToAsync(@"data:text/html,<script>
                window.x = -1; window.y = -1;
                document.addEventListener('click', e => { window.x = e.clientX; window.y = e.clientY; });
            </script>").ConfigureAwait(false);

            await Page.Mouse.ClickAsync(123, 45).ConfigureAwait(false);

            int x = await Page.EvaluateAsync<int>("window.x").ConfigureAwait(false);
            int y = await Page.EvaluateAsync<int>("window.y").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(123));
            Assert.That(y, Is.EqualTo(45));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should trigger hover on move")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerHoverOnMove()
        {
            await Page.GoToAsync(@"data:text/html,<div id='d' style='width:100px;height:100px'>hover me</div>
                <script>
                window.hovered = false;
                document.getElementById('d').addEventListener('mouseover', () => window.hovered = true);
                </script>").ConfigureAwait(false);

            await Page.Mouse.MoveAsync(50, 50).ConfigureAwait(false);

            bool hovered = await Page.EvaluateAsync<bool>("window.hovered").ConfigureAwait(false);
            Assert.That(hovered, Is.True);
        }

        [PlaywrightTest("page-mouse.spec.ts", "should dispatch double click")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispatchDoubleClick()
        {
            await Page.GoToAsync(@"data:text/html,<script>
                window.dbl = 0;
                document.addEventListener('dblclick', () => window.dbl++);
            </script>").ConfigureAwait(false);

            await Page.Mouse.DoubleClickAsync(50, 50).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.dbl").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should dispatch right click via context menu")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispatchRightClickViaContextMenu()
        {
            await Page.GoToAsync(@"data:text/html,<script>
                window.ctx = 0;
                document.addEventListener('contextmenu', e => { window.ctx++; e.preventDefault(); });
            </script>").ConfigureAwait(false);

            await Page.Mouse.ClickAsync(50, 50, Input.MouseButton.Right).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.ctx").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should dispatch mouse down and up separately")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispatchMouseDownAndUpSeparately()
        {
            await Page.GoToAsync(@"data:text/html,<script>
                window.events = [];
                document.addEventListener('mousedown', () => window.events.push('down'));
                document.addEventListener('mouseup', () => window.events.push('up'));
            </script>").ConfigureAwait(false);

            await Page.Mouse.MoveAsync(50, 50).ConfigureAwait(false);
            await Page.Mouse.DownAsync().ConfigureAwait(false);
            await Page.Mouse.UpAsync().ConfigureAwait(false);

            string json = await Page.EvaluateAsync<string>("JSON.stringify(window.events)").ConfigureAwait(false);
            Assert.That(json, Is.EqualTo("[\"down\",\"up\"]"));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should scroll with wheel")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScrollWithWheel()
        {
            await Page.GoToAsync("data:text/html,<div style='height:5000px'>scrollable</div>").ConfigureAwait(false);

            await Page.Mouse.MoveAsync(50, 50).ConfigureAwait(false);
            await Page.Mouse.WheelAsync(0, 200).ConfigureAwait(false);

            // Allow a frame for scroll to apply.
            await Task.Delay(100).ConfigureAwait(false);

            int scrollY = await Page.EvaluateAsync<int>("Math.round(window.scrollY)").ConfigureAwait(false);
            Assert.That(scrollY, Is.GreaterThan(0));
        }

        [PlaywrightTest("page-mouse.spec.ts", "should interpolate move with steps")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInterpolateMoveWithSteps()
        {
            await Page.GoToAsync(@"data:text/html,<script>
                window.moves = [];
                document.addEventListener('mousemove', e => window.moves.push([e.clientX, e.clientY]));
            </script>").ConfigureAwait(false);

            await Page.Mouse.MoveAsync(0, 0).ConfigureAwait(false);
            await Page.Mouse.MoveAsync(100, 100, steps: 5).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.moves.length").ConfigureAwait(false);
            // 1 initial move + 5 interpolated = at least 5 observed.
            Assert.That(count, Is.GreaterThanOrEqualTo(5));
        }
    }
}
