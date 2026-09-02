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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests for page input accessors: Mouse, Keyboard, Touchscreen.
    /// </summary>
    [TestFixture]
    public class PageInputTests : PageTestEx
    {
        [PlaywrightTest("page-keyboard.spec.ts", "MouseClickAsyncFiresEvent")]
        [Test]
        [Timeout(30_000)]
        public async Task MouseClickAsyncFiresEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(@"data:text/html,<button id='b' style='position:absolute;left:10px;top:10px;width:100px;height:40px' onclick='window.clicked=true'>go</button>").ConfigureAwait(false);

            await page.Mouse.ClickAsync(50, 25).ConfigureAwait(false);

            bool clicked = await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false);
            Assert.That(clicked, Is.True);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "KeyboardTypeAsyncSetsTextareaValue")]
        [Test]
        [Timeout(30_000)]
        public async Task KeyboardTypeAsyncSetsTextareaValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<textarea id='t'></textarea>").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FocusAsync().ConfigureAwait(false);

            await page.Keyboard.TypeAsync("hello").ConfigureAwait(false);

            string value = await page.EvaluateAsync<string>("document.querySelector('#t').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("hello"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "KeyboardPressEnterInsertsNewline")]
        [Test]
        [Timeout(30_000)]
        public async Task KeyboardPressEnterInsertsNewline()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<textarea id='t'></textarea>").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.FocusAsync().ConfigureAwait(false);

            await page.Keyboard.TypeAsync("a").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);
            await page.Keyboard.TypeAsync("b").ConfigureAwait(false);

            string value = await page.EvaluateAsync<string>("document.querySelector('#t').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("a\nb"));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "MouseDblClickFiresDblClick")]
        [Test]
        [Timeout(30_000)]
        public async Task MouseDblClickFiresDblClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(@"data:text/html,<button id='b' style='position:absolute;left:10px;top:10px;width:100px;height:40px'>dbl</button>
                <script>
                window.dbl = 0;
                document.getElementById('b').addEventListener('dblclick', () => window.dbl++);
                </script>").ConfigureAwait(false);

            await page.Mouse.DblClickAsync(50, 25).ConfigureAwait(false);

            int count = await page.EvaluateAsync<int>("window.dbl").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "TouchscreenTapFiresEvent")]
        [Test]
        [Timeout(30_000)]
        public async Task TouchscreenTapFiresEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(@"data:text/html,<div id='t' style='position:absolute;left:20px;top:20px;width:80px;height:80px'>tap</div>
                <script>
                window.tapped = false;
                document.getElementById('t').addEventListener('touchstart', () => window.tapped = true);
                </script>").ConfigureAwait(false);

            await page.Touchscreen.TapAsync(50, 50).ConfigureAwait(false);

            bool tapped = await page.EvaluateAsync<bool>("window.tapped").ConfigureAwait(false);
            Assert.That(tapped, Is.True);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "MouseMoveUpdatesCoordinates")]
        [Test]
        [Timeout(30_000)]
        public async Task MouseMoveUpdatesCoordinates()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(@"data:text/html,<script>
                window.coords = null;
                document.addEventListener('mousedown', e => { window.coords = { x: e.clientX, y: e.clientY }; });
                </script>").ConfigureAwait(false);

            // Move cursor first, then click with separate down/up at the current position.
            await page.Mouse.MoveAsync(100, 50).ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);

            double x = await page.EvaluateAsync<double>("window.coords.x").ConfigureAwait(false);
            double y = await page.EvaluateAsync<double>("window.coords.y").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(100));
            Assert.That(y, Is.EqualTo(50));
        }

        [PlaywrightTest("page-keyboard.spec.ts", "MouseWheelAsyncDispatchesWheel")]
        [Test]
        [Timeout(30_000)]
        public async Task MouseWheelAsyncDispatchesWheel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div style='height:5000px'>scrollable</div>").ConfigureAwait(false);

            await page.Mouse.MoveAsync(50, 50).ConfigureAwait(false);
            await page.Mouse.WheelAsync(0, 200).ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);

            int scrollY = await page.EvaluateAsync<int>("Math.round(window.scrollY)").ConfigureAwait(false);
            Assert.That(scrollY, Is.GreaterThan(0));
        }
    }
}
