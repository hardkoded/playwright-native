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
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests for DirectElementHandle interaction surface: Click,
    /// DblClick, Fill, Focus, Type, Press, Hover, Check, Uncheck, SelectOption.
    /// </summary>
    [TestFixture]
    public class ElementHandleInteractionTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-click.spec.ts", "ClickAsync fires click event")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickAsyncFiresClickEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<button id='b' onclick='window.clicked=true'>go</button>").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#b").ConfigureAwait(false);

            await handle.ClickAsync().ConfigureAwait(false);

            bool clicked = await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false);
            Assert.That(clicked, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "ClickAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" style=\"display:none\">go</button>").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.ClickAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "ClickAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" style=\"display:none\" onclick=\"window.clicked=true\">go</button>").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            Task clickTask = target.ClickAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#b').style.display = 'inline-block'").ConfigureAwait(false);
            await clickTask.ConfigureAwait(false);
            bool clicked = await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false);
            Assert.That(clicked, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "ClickAsync force clicks while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"b\" style=\"visibility:hidden;width:80px;height:40px\">go</button>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.addEventListener('click', e => {
                    const r = document.querySelector('#b').getBoundingClientRect();
                    window.hit = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
                })")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await hidden.ClickAsync(force: true).ConfigureAwait(false);
            bool hit = await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false);
            Assert.That(hit, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "ClickAsync honors modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickAsyncShouldHonorModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\">go</button>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#b').addEventListener('click', e => { window.shift = e.shiftKey; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await handle.ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } }).ConfigureAwait(false);
            bool shift = await page.EvaluateAsync<bool>("window.shift === true").ConfigureAwait(false);
            Assert.That(shift, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "DblClickAsync honors modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickAsyncShouldHonorModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\">go</button>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#b').addEventListener('dblclick', e => { window.shift = e.shiftKey; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await handle.DblClickAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } }).ConfigureAwait(false);
            bool shift = await page.EvaluateAsync<bool>("window.shift === true").ConfigureAwait(false);
            Assert.That(shift, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "HoverAsync honors modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverAsyncShouldHonorModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"d\" style=\"width:80px;height:40px;background:red\">x</div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#d').addEventListener('mouseover', e => { window.shift = e.shiftKey; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#d").ConfigureAwait(false);
            await handle.HoverAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } }).ConfigureAwait(false);
            bool shift = await page.EvaluateAsync<bool>("window.shift === true").ConfigureAwait(false);
            Assert.That(shift, Is.True);
        }

        [PlaywrightTest("tap.spec.ts", "TapAsync honors modifiers")]
        [Test]
        [Timeout(30_000)]
        public async Task TapAsyncShouldHonorModifiers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"t\" style=\"width:80px;height:40px;background:red\">tap</div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#t').addEventListener('touchstart', e => { window.shift = e.shiftKey; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.TapAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } }).ConfigureAwait(false);
            bool shift = await page.EvaluateAsync<bool>("window.shift === true").ConfigureAwait(false);
            Assert.That(shift, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "ClickAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<div id=\"t\" style=\"position:absolute;left:40px;top:40px;width:200px;height:200px;background:red\"></div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#t').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.ClickAsync(new() { Position = new Position { X = 12, Y = 18 } }).ConfigureAwait(false);

            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(12).Within(2));
            Assert.That(y, Is.EqualTo(18).Within(2));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "page ClickAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task PageClickAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<div id=\"t\" style=\"position:absolute;left:40px;top:40px;width:200px;height:200px;background:red\"></div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#t').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            await page.ClickAsync("#t", new() { Position = new Position { X = 15, Y = 22 } }).ConfigureAwait(false);

            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(15).Within(2));
            Assert.That(y, Is.EqualTo(22).Within(2));
        }

        [PlaywrightTest("page-fill.spec.ts", "FillAsync sets input value")]
        [Test]
        [Timeout(30_000)]
        public async Task FillAsyncSetsInputValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<input id='i' />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#i").ConfigureAwait(false);

            await handle.FillAsync("hello world").ConfigureAwait(false);

            string value = await page.EvaluateAsync<string>("document.querySelector('#i').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("hello world"));
        }

        [PlaywrightTest("page-fill.spec.ts", "FillAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task FillAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"h\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#h").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.FillAsync("nope", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-fill.spec.ts", "FillAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task FillAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"h\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#h").ConfigureAwait(false);
            Task fillTask = target.FillAsync("wave199", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#h').style.display = 'block'").ConfigureAwait(false);
            await fillTask.ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.querySelector('#h').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("wave199"));
        }

        [PlaywrightTest("page-fill.spec.ts", "FillAsync force fills while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task FillAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"h\" style=\"visibility:hidden\" />").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#h").ConfigureAwait(false);
            await hidden.FillAsync("forced", force: true).ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.querySelector('#h').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("forced"));
        }

        [PlaywrightTest("page-focus.spec.ts", "FocusAsync sets active element")]
        [Test]
        [Timeout(30_000)]
        public async Task FocusAsyncSetsActiveElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<input id='i' />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#i").ConfigureAwait(false);

            await handle.FocusAsync().ConfigureAwait(false);

            string activeId = await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false);
            Assert.That(activeId, Is.EqualTo("i"));
        }

        [PlaywrightTest("page-focus.spec.ts", "FocusAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task FocusAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"i\" style=\"display:none;width:120px;height:24px\" />")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#i").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.FocusAsync(timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-focus.spec.ts", "FocusAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task FocusAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"i\" style=\"display:none;width:120px;height:24px\" />")
                .ConfigureAwait(false);

            IElementHandle input = await page.QuerySelectorAsync("#i").ConfigureAwait(false);
            Task focusTask = input.FocusAsync(timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#i').style.display = 'inline-block'").ConfigureAwait(false);
            await focusTask.ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false),
                Is.EqualTo("i"));
        }

        [PlaywrightTest("elementhandle-type.spec.ts", "TypeAsync produces text")]
        [Test]
        [Timeout(30_000)]
        public async Task TypeAsyncProducesText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<input id='i' />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#i").ConfigureAwait(false);

            await handle.TypeAsync("hello").ConfigureAwait(false);

            string value = await page.EvaluateAsync<string>("document.querySelector('#i').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("hello"));
        }

        [PlaywrightTest("elementhandle-type.spec.ts", "TypeAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task TypeAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"i\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#i").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.TypeAsync("nope", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-type.spec.ts", "TypeAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task TypeAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"i\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#i").ConfigureAwait(false);
            Task typeTask = target.TypeAsync("wave212", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#i').style.display = 'inline-block'").ConfigureAwait(false);
            await typeTask.ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.querySelector('#i').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("wave212"));
        }

        [PlaywrightTest("elementhandle-type.spec.ts", "TypeAsync force types while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task TypeAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"i\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#i").ConfigureAwait(false);
            await hidden.TypeAsync("forced", force: true, timeout: 200).ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "PressAsync dispatches key")]
        [Test]
        [Timeout(30_000)]
        public async Task PressAsyncDispatchesKey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<textarea id='t'></textarea>").ConfigureAwait(false);
            await page.EvaluateAsync<bool>("window.lastKey = ''; document.querySelector('#t').addEventListener('keydown', e => { window.lastKey = e.key; }), true").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);

            await handle.PressAsync("Enter").ConfigureAwait(false);

            string lastKey = await page.EvaluateAsync<string>("window.lastKey").ConfigureAwait(false);
            Assert.That(lastKey, Is.EqualTo("Enter"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "PressAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task PressAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<textarea id=\"t\" style=\"display:none\"></textarea>").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.PressAsync("Enter", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "PressAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task PressAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<textarea id=\"t\" style=\"display:none\"></textarea>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "window.lastKey = ''; document.querySelector('#t').addEventListener('keydown', e => { window.lastKey = e.key; })")
                .ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            Task pressTask = target.PressAsync("Enter", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').style.display = 'block'").ConfigureAwait(false);
            await pressTask.ConfigureAwait(false);
            string lastKey = await page.EvaluateAsync<string>("window.lastKey").ConfigureAwait(false);
            Assert.That(lastKey, Is.EqualTo("Enter"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "PressAsync force presses while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task PressAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<textarea id=\"t\" style=\"display:none\"></textarea>").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await hidden.PressAsync("Enter", force: true, timeout: 200).ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "HoverAsync triggers mouseover")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverAsyncTriggersMouseover()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div id='d' style='width:50px;height:50px;background:red'>x</div>").ConfigureAwait(false);
            await page.EvaluateAsync<bool>("window.hovered = false; document.querySelector('#d').addEventListener('mouseover', () => window.hovered = true), true").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#d").ConfigureAwait(false);

            await handle.HoverAsync().ConfigureAwait(false);

            bool hovered = await page.EvaluateAsync<bool>("window.hovered").ConfigureAwait(false);
            Assert.That(hovered, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "HoverAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\" style=\"display:none;width:50px;height:50px;background:red\">x</div>").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#d").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.HoverAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "HoverAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\" style=\"display:none;width:50px;height:50px;background:red\">x</div>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "window.hovered = false; document.querySelector('#d').addEventListener('mouseover', () => window.hovered = true)")
                .ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#d").ConfigureAwait(false);
            Task hoverTask = target.HoverAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#d').style.display = 'block'").ConfigureAwait(false);
            await hoverTask.ConfigureAwait(false);
            bool hovered = await page.EvaluateAsync<bool>("window.hovered").ConfigureAwait(false);
            Assert.That(hovered, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "HoverAsync force hovers while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"d\" style=\"visibility:hidden;width:80px;height:40px;background:red\">x</div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.addEventListener('mousemove', e => {
                    const r = document.querySelector('#d').getBoundingClientRect();
                    window.hit = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
                })")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#d").ConfigureAwait(false);
            await hidden.HoverAsync(force: true).ConfigureAwait(false);
            bool hit = await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false);
            Assert.That(hit, Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "HoverAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<div id=\"t\" style=\"position:absolute;left:40px;top:40px;width:200px;height:200px;background:red\"></div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#t').addEventListener('mouseover', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.HoverAsync(new() { Position = new Position { X = 12, Y = 18 } }).ConfigureAwait(false);

            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(12).Within(2));
            Assert.That(y, Is.EqualTo(18).Within(2));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "page HoverAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task PageHoverAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<div id=\"t\" style=\"position:absolute;left:40px;top:40px;width:200px;height:200px;background:red\"></div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#t').addEventListener('mouseover', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            await page.HoverAsync("#t", new() { Position = new Position { X = 15, Y = 22 } }).ConfigureAwait(false);

            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(15).Within(2));
            Assert.That(y, Is.EqualTo(22).Within(2));
        }

        [PlaywrightTest("tap.spec.ts", "TapAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task TapAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<div id=\"t\" style=\"position:absolute;left:40px;top:40px;width:200px;height:200px;background:red\"></div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.querySelector('#t').addEventListener('touchstart', e => {
                    const touch = e.changedTouches[0];
                    const r = e.target.getBoundingClientRect();
                    window.px = touch.clientX - r.left;
                    window.py = touch.clientY - r.top;
                })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.TapAsync(new() { Position = new Position { X = 12, Y = 18 } }).ConfigureAwait(false);

            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(12).Within(2));
            Assert.That(y, Is.EqualTo(18).Within(2));
        }

        [PlaywrightTest("tap.spec.ts", "page TapAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTapAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<div id=\"t\" style=\"position:absolute;left:40px;top:40px;width:200px;height:200px;background:red\"></div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.querySelector('#t').addEventListener('touchstart', e => {
                    const touch = e.changedTouches[0];
                    const r = e.target.getBoundingClientRect();
                    window.px = touch.clientX - r.left;
                    window.py = touch.clientY - r.top;
                })")
                .ConfigureAwait(false);

            await page.TapAsync("#t", new() { Position = new Position { X = 15, Y = 22 } }).ConfigureAwait(false);

            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(15).Within(2));
            Assert.That(y, Is.EqualTo(22).Within(2));
        }

        [PlaywrightTest("tap.spec.ts", "TapAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task TapAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none;width:80px;height:80px;background:red\">tap</div>").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.TapAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("tap.spec.ts", "TapAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task TapAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"t\" style=\"display:none;width:80px;height:80px;background:red\">tap</div>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "window.tapped = false; document.querySelector('#t').addEventListener('touchstart', () => window.tapped = true)")
                .ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            Task tapTask = target.TapAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#t').style.display = 'block'").ConfigureAwait(false);
            await tapTask.ConfigureAwait(false);
            bool tapped = await page.EvaluateAsync<bool>("window.tapped").ConfigureAwait(false);
            Assert.That(tapped, Is.True);
        }

        [PlaywrightTest("tap.spec.ts", "TapAsync force taps while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task TapAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div id=\"t\" style=\"visibility:hidden;width:80px;height:40px;background:red\">tap</div>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.addEventListener('touchstart', e => {
                    const t = e.changedTouches[0];
                    const r = document.querySelector('#t').getBoundingClientRect();
                    window.hit = t.clientX >= r.left && t.clientX <= r.right && t.clientY >= r.top && t.clientY <= r.bottom;
                })")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await hidden.TapAsync(force: true).ConfigureAwait(false);
            bool hit = await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false);
            Assert.That(hit, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "CheckAsync checks box")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckAsyncChecksBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<input id='c' type='checkbox' />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#c").ConfigureAwait(false);

            await handle.CheckAsync().ConfigureAwait(false);

            bool isChecked = await handle.IsCheckedAsync().ConfigureAwait(false);
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "CheckAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.CheckAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-check.spec.ts", "CheckAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle box = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            Task checkTask = box.CheckAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').style.display = 'inline-block'").ConfigureAwait(false);
            await checkTask.ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "CheckAsync force clicks while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" style=\"visibility:hidden;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.addEventListener('click', e => {
                    const r = document.querySelector('#c').getBoundingClientRect();
                    window.hit = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
                })")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            try
            {
                await hidden.CheckAsync(force: true).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }

            bool hit = await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false);
            Assert.That(hit, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "CheckAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" style=\"position:absolute;left:40px;top:40px;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#c').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await handle.CheckAsync(new() { Position = new Position { X = 12, Y = 18 } }).ConfigureAwait(false);

            Assert.That(await handle.IsCheckedAsync().ConfigureAwait(false), Is.True);
            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(12).Within(2));
            Assert.That(y, Is.EqualTo(18).Within(2));
        }

        [PlaywrightTest("page-check.spec.ts", "page CheckAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task PageCheckAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" style=\"position:absolute;left:40px;top:40px;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#c').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            await page.CheckAsync("#c", new() { Position = new Position { X = 15, Y = 22 } }).ConfigureAwait(false);

            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(15).Within(2));
            Assert.That(y, Is.EqualTo(22).Within(2));
        }

        [PlaywrightTest("page-check.spec.ts", "SetCheckedAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task SetCheckedAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" style=\"position:absolute;left:40px;top:40px;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#c').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await handle.SetCheckedAsync(true, new() { Position = new Position { X = 12, Y = 18 } }).ConfigureAwait(false);

            Assert.That(await handle.IsCheckedAsync().ConfigureAwait(false), Is.True);
            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(12).Within(2));
            Assert.That(y, Is.EqualTo(18).Within(2));
        }

        [PlaywrightTest("page-check.spec.ts", "page SetCheckedAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSetCheckedAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" style=\"position:absolute;left:40px;top:40px;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#c').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            await page.SetCheckedAsync("#c", true, new() { Position = new Position { X = 15, Y = 22 } }).ConfigureAwait(false);

            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(15).Within(2));
            Assert.That(y, Is.EqualTo(22).Within(2));
        }

        [PlaywrightTest("page-check.spec.ts", "UncheckAsync unchecks box")]
        [Test]
        [Timeout(30_000)]
        public async Task UncheckAsyncUnchecksBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<input id='c' type='checkbox' checked />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#c").ConfigureAwait(false);

            await handle.UncheckAsync().ConfigureAwait(false);

            bool isChecked = await handle.IsCheckedAsync().ConfigureAwait(false);
            Assert.That(isChecked, Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "UncheckAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task UncheckAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.UncheckAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-check.spec.ts", "UncheckAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task UncheckAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked style=\"display:none\" />").ConfigureAwait(false);

            IElementHandle box = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            Task uncheckTask = box.UncheckAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#c').style.display = 'inline-block'").ConfigureAwait(false);
            await uncheckTask.ConfigureAwait(false);
            Assert.That(await box.IsCheckedAsync().ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "UncheckAsync force clicks while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task UncheckAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" checked style=\"visibility:hidden;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.addEventListener('click', e => {
                    const r = document.querySelector('#c').getBoundingClientRect();
                    window.hit = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
                })")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            try
            {
                await hidden.UncheckAsync(force: true).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }

            bool hit = await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false);
            Assert.That(hit, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "UncheckAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task UncheckAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" checked style=\"position:absolute;left:40px;top:40px;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#c').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await handle.UncheckAsync(new() { Position = new Position { X = 12, Y = 18 } }).ConfigureAwait(false);

            Assert.That(await handle.IsCheckedAsync().ConfigureAwait(false), Is.False);
            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(12).Within(2));
            Assert.That(y, Is.EqualTo(18).Within(2));
        }

        [PlaywrightTest("page-check.spec.ts", "page UncheckAsync honors Position")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUncheckAsyncShouldHonorPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(
                "<input id=\"c\" type=\"checkbox\" checked style=\"position:absolute;left:40px;top:40px;width:80px;height:80px;margin:0;padding:0;-webkit-appearance:none;appearance:none;background:red\" />")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#c').addEventListener('click', e => { window.px = e.offsetX; window.py = e.offsetY; })")
                .ConfigureAwait(false);

            await page.UncheckAsync("#c", new() { Position = new Position { X = 15, Y = 22 } }).ConfigureAwait(false);

            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.False);
            int x = await page.EvaluateAsync<int>("Math.round(window.px)").ConfigureAwait(false);
            int y = await page.EvaluateAsync<int>("Math.round(window.py)").ConfigureAwait(false);
            Assert.That(x, Is.EqualTo(15).Within(2));
            Assert.That(y, Is.EqualTo(22).Within(2));
        }

        [PlaywrightTest("page-select-option.spec.ts", "SelectOptionAsync selects by value")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectOptionAsyncSelectsByValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(@"data:text/html,<select id='s'>
                <option value='a'>Alpha</option>
                <option value='b'>Beta</option>
                <option value='c'>Gamma</option>
            </select>").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#s").ConfigureAwait(false);

            System.Collections.Generic.IReadOnlyList<string> result = await handle.SelectOptionAsync("b").ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(new[] { "b" }));
            string value = await page.EvaluateAsync<string>("document.querySelector('#s').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("b"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "SelectOptionAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectOptionAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<select id=\"s\" style=\"display:none\"><option value=\"a\">Alpha</option><option value=\"b\">Beta</option></select>")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#s").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.SelectOptionAsync("b", options: new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "SelectOptionAsync force selects while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectOptionAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<select id=\"s\" style=\"display:none\"><option value=\"a\">Alpha</option><option value=\"b\">Beta</option></select>")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#s").ConfigureAwait(false);
            System.Collections.Generic.IReadOnlyCollection<string> result = await hidden.SelectOptionAsync("b", force: true).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { "b" }));
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#s').value").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "SelectOptionAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectOptionAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<select id=\"s\" style=\"display:none\"><option value=\"a\">Alpha</option><option value=\"b\">Beta</option></select>")
                .ConfigureAwait(false);

            IElementHandle select = await page.QuerySelectorAsync("#s").ConfigureAwait(false);
            Task<System.Collections.Generic.IReadOnlyList<string>> selectTask = select.SelectOptionAsync("b", options: new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#s').style.display = 'inline-block'").ConfigureAwait(false);
            System.Collections.Generic.IReadOnlyList<string> result = await selectTask.ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { "b" }));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "SetInputFilesAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"f\" type=\"file\" style=\"display:none;width:200px;height:30px\" />")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#f").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.SetInputFilesAsync(new FilePayload
                {
                    Name = "wave.txt",
                    MimeType = "text/plain",
                    Buffer = new byte[] { 1, 2, 3 },
                }, new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "SetInputFilesAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"f\" type=\"file\" style=\"display:none;width:200px;height:30px\" />")
                .ConfigureAwait(false);

            IElementHandle input = await page.QuerySelectorAsync("#f").ConfigureAwait(false);
            Task setTask = input.SetInputFilesAsync(new FilePayload
            {
                Name = "wave.txt",
                MimeType = "text/plain",
                Buffer = new byte[] { 1, 2, 3 },
            }, new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#f').style.display = 'inline-block'").ConfigureAwait(false);
            await setTask.ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false),
                Is.EqualTo("wave.txt"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "SetInputFilesAsync force sets while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"f\" type=\"file\" style=\"display:none;width:200px;height:30px\" />")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#f").ConfigureAwait(false);
            await hidden.SetInputFilesAsync(new[]
                {
                    new FilePayload
                    {
                        Name = "wave.txt",
                        MimeType = "text/plain",
                        Buffer = new byte[] { 1, 2, 3 },
                    },
                }, force: true).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false),
                Is.EqualTo("wave.txt"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "DblClickAsync fires dbl click")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickAsyncFiresDblClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<button id='b'>dbl</button>").ConfigureAwait(false);
            await page.EvaluateAsync<bool>("window.dbl = 0; document.querySelector('#b').addEventListener('dblclick', () => window.dbl++), true").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#b").ConfigureAwait(false);

            await handle.DblClickAsync().ConfigureAwait(false);

            int count = await page.EvaluateAsync<int>("window.dbl").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "DblClickAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" style=\"display:none\">dbl</button>").ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => hidden.DblClickAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "DblClickAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\" style=\"display:none\">dbl</button>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "window.dbl = 0; document.querySelector('#b').addEventListener('dblclick', () => window.dbl++)")
                .ConfigureAwait(false);

            IElementHandle target = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            Task dblTask = target.DblClickAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#b').style.display = 'inline-block'").ConfigureAwait(false);
            await dblTask.ConfigureAwait(false);
            int count = await page.EvaluateAsync<int>("window.dbl").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "DblClickAsync force clicks while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickAsyncShouldHonorForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<button id=\"b\" style=\"visibility:hidden;width:80px;height:40px\">dbl</button>")
                .ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.addEventListener('click', e => {
                    const r = document.querySelector('#b').getBoundingClientRect();
                    window.hit = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
                })")
                .ConfigureAwait(false);

            IElementHandle hidden = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await hidden.DblClickAsync(force: true).ConfigureAwait(false);
            bool hit = await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false);
            Assert.That(hit, Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "InputValueAsync reads the input value")]
        [Test]
        [Timeout(30_000)]
        public async Task InputValueAsyncShouldReadTheValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"n\" value=\"old\" />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#n").ConfigureAwait(false);
            await handle.FillAsync("wave162").ConfigureAwait(false);

            Assert.That(await handle.InputValueAsync().ConfigureAwait(false), Is.EqualTo("wave162"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "InputValueAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task InputValueAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"hidden\" style=\"display:none\" />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#n").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => handle.InputValueAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "InputValueAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task InputValueAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"later\" style=\"display:none\" />").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("#n").ConfigureAwait(false);

            Task<string> valueTask = handle.InputValueAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#n').style.display = 'block'").ConfigureAwait(false);
            string value = await valueTask.ConfigureAwait(false);

            Assert.That(value, Is.EqualTo("later"));
        }
    }
}
