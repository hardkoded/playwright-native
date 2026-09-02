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
using PlaywrightNative.Input;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Click tests against the <c>/input/button.html</c> fixture. Each test navigates to the
    /// fixture, resolves the button center via JS, clicks it, and verifies <c>window.result</c>.
    /// </summary>
    [TestFixture]
    public class CRClickTests : CRTestBase
    {
        private async Task<(double X, double Y)> GetButtonCenterAsync()
        {
            double x = await Page.EvaluateAsync<double>("(() => { const r = document.querySelector('button').getBoundingClientRect(); return r.x + r.width / 2; })()").ConfigureAwait(false);
            double y = await Page.EvaluateAsync<double>("(() => { const r = document.querySelector('button').getBoundingClientRect(); return r.y + r.height / 2; })()").ConfigureAwait(false);
            return (x, y);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButton()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button after navigation ")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButtonAfterNavigation()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-click.spec.ts", "should record shift key on click")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordShiftKeyOnClick()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Keyboard.DownAsync("Shift").ConfigureAwait(false);
            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
            await Page.Keyboard.UpAsync("Shift").ConfigureAwait(false);

            bool shiftPressed = await Page.EvaluateAsync<bool>("window.shiftKey === true").ConfigureAwait(false);
            Assert.That(shiftPressed, Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "should fire context menu on right click")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireContextMenuOnRightClick()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.EvaluateAsync<bool>("window.rmb = false; document.querySelector('button').addEventListener('contextmenu', e => { window.rmb = true; e.preventDefault(); }), true").ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y, Input.MouseButton.Right).ConfigureAwait(false);

            bool fired = await Page.EvaluateAsync<bool>("window.rmb").ConfigureAwait(false);
            Assert.That(fired, Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "should click twice consecutively")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTwiceConsecutively()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("window.clicks = 0; document.querySelector('button').addEventListener('click', () => window.clicks++), true").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.clicks").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(2));
        }

        [PlaywrightTest("page-click.spec.ts", "should not click when coordinates are off button")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotClickWhenCoordinatesAreOffButton()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);

            await Page.Mouse.ClickAsync(1, 1).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Was not clicked"));
        }

        [PlaywrightTest("page-click.spec.ts", "should trigger hover over button")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerHoverOverButton()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            await Page.EvaluateAsync<bool>("window.hovered = false; document.querySelector('button').addEventListener('mouseover', () => window.hovered = true), true").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.MoveAsync(x, y).ConfigureAwait(false);

            bool hovered = await Page.EvaluateAsync<bool>("window.hovered").ConfigureAwait(false);
            Assert.That(hovered, Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "should dispatch click with ctrl modifier")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDispatchClickWithCtrlModifier()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            // Use mousedown (not click): on macOS, Ctrl+click is interpreted as a right-click at
            // the OS/browser level and suppresses the click event. mousedown always fires and
            // captures the ctrlKey modifier from the CDP dispatched event.
            await Page.EvaluateAsync<bool>("window.ctrl = false; document.querySelector('button').addEventListener('mousedown', e => window.ctrl = e.ctrlKey, true), true").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Keyboard.DownAsync("Control").ConfigureAwait(false);
            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
            await Page.Keyboard.UpAsync("Control").ConfigureAwait(false);

            bool ctrl = await Page.EvaluateAsync<bool>("window.ctrl").ConfigureAwait(false);
            Assert.That(ctrl, Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "should report click coordinates")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportClickCoordinates()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            double pageX = await Page.EvaluateAsync<double>("window.pageX").ConfigureAwait(false);
            double pageY = await Page.EvaluateAsync<double>("window.pageY").ConfigureAwait(false);
            Assert.That(pageX, Is.InRange(x - 2, x + 2));
            Assert.That(pageY, Is.InRange(y - 2, y + 2));
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button after reload")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButtonAfterReload()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-click.spec.ts", "should click inside scrollable content")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickInsideScrollableContent()
        {
            await Page.GoToAsync(@"data:text/html,<div style='height:2000px'></div><button style='position:absolute;top:1500px;left:50px'>deep</button>
                <script>
                window.hit = false;
                document.querySelector('button').addEventListener('click', () => window.hit = true);
                window.scrollTo(0, 1400);
                </script>").ConfigureAwait(false);

            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            bool hit = await Page.EvaluateAsync<bool>("window.hit").ConfigureAwait(false);
            Assert.That(hit, Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "Double click should fire double click event")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DoubleClickShouldFireDoubleClickEvent()
        {
            await Page.GoToAsync(@"data:text/html,<button>dbl</button>
                <script>
                window.dbl = 0;
                document.querySelector('button').addEventListener('dblclick', () => window.dbl++);
                </script>").ConfigureAwait(false);

            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.DoubleClickAsync(x, y).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.dbl").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-click.spec.ts", "should click middle button")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickMiddleButton()
        {
            await Page.GoToAsync(@"data:text/html,<button>middle</button>
                <script>
                window.mb = -1;
                document.querySelector('button').addEventListener('auxclick', e => window.mb = e.button);
                </script>").ConfigureAwait(false);

            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y, Input.MouseButton.Middle).ConfigureAwait(false);

            int button = await Page.EvaluateAsync<int>("window.mb").ConfigureAwait(false);
            Assert.That(button, Is.EqualTo(1));
        }

        [PlaywrightTest("page-click.spec.ts", "should release pressed button after click")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReleasePressedButtonAfterClick()
        {
            await Page.GoToAsync(@"data:text/html,<div style='height:100vh;width:100vw'></div>
                <script>
                window.downs = 0;
                window.ups = 0;
                document.addEventListener('mousedown', () => window.downs++);
                document.addEventListener('mouseup', () => window.ups++);
                </script>").ConfigureAwait(false);

            await Page.Mouse.ClickAsync(50, 50).ConfigureAwait(false);

            int downs = await Page.EvaluateAsync<int>("window.downs").ConfigureAwait(false);
            int ups = await Page.EvaluateAsync<int>("window.ups").ConfigureAwait(false);
            Assert.That(downs, Is.EqualTo(1));
            Assert.That(ups, Is.EqualTo(1));
        }

        [PlaywrightTest("page-click.spec.ts", "should support delay between down and up")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportDelayBetweenDownAndUp()
        {
            await Page.GoToAsync(@"data:text/html,<button>held</button>
                <script>
                window.downTs = 0; window.upTs = 0;
                const b = document.querySelector('button');
                b.addEventListener('mousedown', () => window.downTs = performance.now());
                b.addEventListener('mouseup', () => window.upTs = performance.now());
                </script>").ConfigureAwait(false);

            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y, delayMs: 100).ConfigureAwait(false);

            double down = await Page.EvaluateAsync<double>("window.downTs").ConfigureAwait(false);
            double up = await Page.EvaluateAsync<double>("window.upTs").ConfigureAwait(false);
            Assert.That(up - down, Is.GreaterThanOrEqualTo(80));
        }
    }
}
