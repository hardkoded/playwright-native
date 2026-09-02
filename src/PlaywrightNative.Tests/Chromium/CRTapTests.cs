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
    /// Integration tests for <see cref="CRElementHandle.TapAsync"/>.
    /// </summary>
    [TestFixture]
    public class CRTapTests : CRTestBase
    {
        [PlaywrightTest("tap.spec.ts", "should fire touch start and touch end")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireTouchStartAndTouchEnd()
        {
            await Page.GoToAsync(@"data:text/html,<div id='t' style='position:absolute;left:20px;top:20px;width:80px;height:80px'>tap</div>
                <script>
                window.events = [];
                const t = document.getElementById('t');
                t.addEventListener('touchstart', () => window.events.push('touchstart'));
                t.addEventListener('touchend', () => window.events.push('touchend'));
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.TapAsync().ConfigureAwait(false);

            string json = await Page.EvaluateAsync<string>("JSON.stringify(window.events)").ConfigureAwait(false);
            Assert.That(json, Is.EqualTo("[\"touchstart\",\"touchend\"]"));
        }

        [PlaywrightTest("tap.spec.ts", "should fire click on simple button")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireClickOnSimpleButton()
        {
            // With touch events on a plain button, browsers synthesize a click.
            await Page.GoToAsync(@"data:text/html,<button id='b' style='position:absolute;left:20px;top:20px;width:80px;height:40px'>tap me</button>
                <script>
                window.clicked = false;
                document.getElementById('b').addEventListener('click', () => window.clicked = true);
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await handle.TapAsync().ConfigureAwait(false);

            bool clicked = await Page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false);
            Assert.That(clicked, Is.True);
        }

        [PlaywrightTest("tap.spec.ts", "should throw for invisible element")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForInvisibleElement()
        {
            await Page.GoToAsync("data:text/html,<div id='t' style='display:none'>hidden</div>").ConfigureAwait(false);

            CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.TapAsync());
            Assert.That(ex.Message, Does.Contain("no layout").Or.Contain("not visible"));

            await handle.DisposeAsync().ConfigureAwait(false);
        }
    }
}
