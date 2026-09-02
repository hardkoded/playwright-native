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
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for page-level click/fill/focus/check and <see cref="IPage.TitleAsync"/>.
    /// </summary>
    [TestFixture]
    public class PageActionTests : PageTestEx
    {
        [PlaywrightTest("page-click.spec.ts", "should click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickShouldFireDomHandler()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);
            await page.ClickAsync("#b").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "page ClickAsync trial does not click")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickTrialShouldNotDispatchTheClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"b\" onclick=\"window.clicked=true\">Go</button>").ConfigureAwait(false);
            await page.ClickAsync("#b", new() { Trial = true }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.False);
            await page.ClickAsync("#b").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "page ClickAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageClickAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.ClickAsync("#b", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-click.spec.ts", "page ClickAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageClickAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task clickTask = page.ClickAsync("#b", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<button id=\"b\" onclick=\"window.clicked=true\">Go</button>')")
                .ConfigureAwait(false);
            await clickTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.clicked === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-fill.spec.ts", "should fill input")]
        [Test]
        [Timeout(30_000)]
        public async Task FillShouldSetInputValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);
            await page.FillAsync("#n", "Ada").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#n').value").ConfigureAwait(false), Is.EqualTo("Ada"));
        }

        [PlaywrightTest("page-fill.spec.ts", "page FillAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFillAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.FillAsync("#n", "Ada", timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-fill.spec.ts", "page FillAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFillAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task fillTask = page.FillAsync("#n", "Ada", timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=\"n\" />')")
                .ConfigureAwait(false);
            await fillTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#n').value").ConfigureAwait(false), Is.EqualTo("Ada"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "page PressAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PagePressAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.PressAsync("#n", "Enter", timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-press.spec.ts", "page PressAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PagePressAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task pressTask = page.PressAsync("#n", "Enter", timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"const host = document.getElementById('host');
                  host.insertAdjacentHTML('beforeend', '<input id=""n"" />');
                  document.querySelector('#n').addEventListener('keydown', e => { if (e.key === 'Enter') window.hit = true; });")
                .ConfigureAwait(false);
            await pressTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.hit === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-type.spec.ts", "page TypeAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTypeAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.TypeAsync("#n", "hi", timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-type.spec.ts", "page TypeAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTypeAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task typeTask = page.TypeAsync("#n", "hi", timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=\"n\" />')")
                .ConfigureAwait(false);
            await typeTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#n').value").ConfigureAwait(false), Is.EqualTo("hi"));
        }

        [PlaywrightTest("page-basic.spec.ts", "InputValueAsync reads the input value")]
        [Test]
        [Timeout(30_000)]
        public async Task InputValueShouldReadTheFilledValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);
            await page.FillAsync("#n", "wave163").ConfigureAwait(false);
            Assert.That(await page.InputValueAsync("#n").ConfigureAwait(false), Is.EqualTo("wave163"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page InputValueAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageInputValueAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.InputValueAsync("#n", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page InputValueAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageInputValueAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<string> valueTask = page.InputValueAsync("#n", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=\"n\" value=\"ok\" />')")
                .ConfigureAwait(false);
            Assert.That(await valueTask.ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("page-focus.spec.ts", "should focus")]
        [Test]
        [Timeout(30_000)]
        public async Task FocusShouldMoveActiveElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"n\" />").ConfigureAwait(false);
            await page.FocusAsync("#n").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("n"));
        }

        [PlaywrightTest("page-focus.spec.ts", "page FocusAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFocusAsyncShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"i\" style=\"display:none;width:120px;height:24px\" />")
                .ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.FocusAsync("#i", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-focus.spec.ts", "page FocusAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFocusAsyncShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"i\" style=\"display:none;width:120px;height:24px\" />")
                .ConfigureAwait(false);

            Task focusTask = page.FocusAsync("#i", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#i').style.display = 'inline-block'").ConfigureAwait(false);
            await focusTask.ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false),
                Is.EqualTo("i"));
        }

        [PlaywrightTest("page-focus.spec.ts", "page FocusAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFocusAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.FocusAsync("#i", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-focus.spec.ts", "page FocusAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageFocusAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task focusTask = page.FocusAsync("#i", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=""i"" style=""width:120px;height:24px"" />');")
                .ConfigureAwait(false);
            await focusTask.ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false),
                Is.EqualTo("i"));
        }

        [PlaywrightTest("page-basic.spec.ts", "should return the page title")]
        [Test]
        [Timeout(30_000)]
        public async Task TitleShouldReturnDocumentTitle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<html><head><title>Hello Wave</title></head><body></body></html>").ConfigureAwait(false);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("Hello Wave"));
        }

        [PlaywrightTest("page-check.spec.ts", "should check and uncheck the box")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckAndUncheckShouldToggleCheckbox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);
            await page.CheckAsync("#c").ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
            await page.UncheckAsync("#c").ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "page CheckAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageCheckAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.CheckAsync("#c", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-check.spec.ts", "page CheckAsync trial does not check")]
        [Test]
        [Timeout(30_000)]
        public async Task CheckTrialShouldNotCheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" />").ConfigureAwait(false);
            await page.CheckAsync("#c", new() { Trial = true }).ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.False);
            await page.CheckAsync("#c").ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "page CheckAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageCheckAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task checkTask = page.CheckAsync("#c", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=""c"" type=""checkbox"" style=""width:20px;height:20px"" />');")
                .ConfigureAwait(false);
            await checkTask.ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "page UncheckAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUncheckAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.UncheckAsync("#c", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-check.spec.ts", "page UncheckAsync trial does not uncheck")]
        [Test]
        [Timeout(30_000)]
        public async Task UncheckTrialShouldNotUncheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"c\" type=\"checkbox\" checked />").ConfigureAwait(false);
            await page.UncheckAsync("#c", new() { Trial = true }).ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.True);
            await page.UncheckAsync("#c").ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "page UncheckAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageUncheckAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task uncheckTask = page.UncheckAsync("#c", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=""c"" type=""checkbox"" checked style=""width:20px;height:20px"" />');")
                .ConfigureAwait(false);
            await uncheckTask.ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#c").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-click.spec.ts", "should double click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickShouldFireDblClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"b\" ondblclick=\"window.dbl=true\">Go</button>").ConfigureAwait(false);
            await page.DblClickAsync("#b").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.dbl === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "page DblClickAsync trial does not click")]
        [Test]
        [Timeout(30_000)]
        public async Task DblClickTrialShouldNotDispatchTheClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"b\" ondblclick=\"window.dbl=true\">Go</button>").ConfigureAwait(false);
            await page.DblClickAsync("#b", new() { Trial = true }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.dbl === true").ConfigureAwait(false), Is.False);
            await page.DblClickAsync("#b").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.dbl === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "page DblClickAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageDblClickAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.DblClickAsync("#b", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-click.spec.ts", "page DblClickAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageDblClickAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task dblTask = page.DblClickAsync("#b", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<button id=\"b\" ondblclick=\"window.dbl=true\">Go</button>')")
                .ConfigureAwait(false);
            await dblTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.dbl === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "page HoverAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageHoverAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.HoverAsync("#d", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-click.spec.ts", "page HoverAsync trial does not hover")]
        [Test]
        [Timeout(30_000)]
        public async Task HoverTrialShouldNotDispatchTheHover()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"d\" onmouseover=\"window.hov=true\">x</div>").ConfigureAwait(false);
            await page.HoverAsync("#d", new() { Trial = true }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.hov === true").ConfigureAwait(false), Is.False);
            await page.HoverAsync("#d").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.hov === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-click.spec.ts", "page HoverAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageHoverAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task hoverTask = page.HoverAsync("#d", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"const host = document.getElementById('host');
                  host.insertAdjacentHTML('beforeend', '<div id=""d"" style=""width:80px;height:40px;background:red"">x</div>');
                  document.querySelector('#d').addEventListener('mouseover', () => { window.hovered = true; });")
                .ConfigureAwait(false);
            await hoverTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.hovered === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("tap.spec.ts", "page TapAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTapAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.TapAsync("#t", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("tap.spec.ts", "page TapAsync trial does not tap")]
        [Test]
        [Timeout(30_000)]
        public async Task TapTrialShouldNotDispatchTheTap()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"t\" ontouchstart=\"window.tapped=true\" style=\"width:80px;height:40px;background:red\">tap</div>").ConfigureAwait(false);
            await page.TapAsync("#t", new() { Trial = true }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.tapped === true").ConfigureAwait(false), Is.False);
            await page.TapAsync("#t").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.tapped === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("tap.spec.ts", "page TapAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTapAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task tapTask = page.TapAsync("#t", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                @"const host = document.getElementById('host');
                  host.insertAdjacentHTML('beforeend', '<div id=""t"" style=""width:80px;height:40px;background:red"">tap</div>');
                  document.querySelector('#t').addEventListener('touchstart', () => { window.tapped = true; });")
                .ConfigureAwait(false);
            await tapTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.tapped === true").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select the option")]
        [Test]
        [Timeout(30_000)]
        public async Task SelectOptionShouldSelectByValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<select id=\"s\"><option value=\"a\">A</option><option value=\"b\">B</option></select>").ConfigureAwait(false);
            IReadOnlyCollection<string> selected = await page.SelectOptionAsync("#s", "b").ConfigureAwait(false);
            Assert.That(selected, Is.EqualTo(new[] { "b" }));
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#s').value").ConfigureAwait(false), Is.EqualTo("b"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "page SelectOptionAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSelectOptionAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.SelectOptionAsync("#s", "b", timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "page SelectOptionAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSelectOptionAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<IReadOnlyCollection<string>> selectTask = page.SelectOptionAsync("#s", "b", timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<select id=\"s\"><option value=\"a\">A</option><option value=\"b\">B</option></select>')")
                .ConfigureAwait(false);
            IReadOnlyCollection<string> selected = await selectTask.ConfigureAwait(false);
            Assert.That(selected, Is.EqualTo(new[] { "b" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "page SelectOptionAsync params waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSelectOptionAsyncParamsShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<IReadOnlyCollection<string>> selectTask = page.SelectOptionAsync("#s", "a", "b");
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<select id=\"s\" multiple><option value=\"a\">A</option><option value=\"b\">B</option><option value=\"c\">C</option></select>')")
                .ConfigureAwait(false);
            IReadOnlyCollection<string> selected = await selectTask.ConfigureAwait(false);
            Assert.That(selected, Is.EquivalentTo(new[] { "a", "b" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "page SelectOptionAsync SelectOptionValue params waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSelectOptionAsyncValueParamsShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<IReadOnlyCollection<string>> selectTask = page.SelectOptionAsync(
                "#s",
                new SelectOptionValue { Value = "a" },
                new SelectOptionValue { Value = "c" });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<select id=\"s\" multiple><option value=\"a\">A</option><option value=\"b\">B</option><option value=\"c\">C</option></select>')")
                .ConfigureAwait(false);
            IReadOnlyCollection<string> selected = await selectTask.ConfigureAwait(false);
            Assert.That(selected, Is.EquivalentTo(new[] { "a", "c" }));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "should set input files")]
        [Test]
        [Timeout(30_000)]
        public async Task SetInputFilesShouldAssignFilePayload()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"f\" type=\"file\" />").ConfigureAwait(false);
            await page.SetInputFilesAsync("#f", new FilePayload
            {
                Name = "wave.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello"),
            }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false), Is.EqualTo("wave.txt"));
            Assert.That(await page.EvaluateAsync<int>("document.querySelector('#f').files[0].size").ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "page SetInputFilesAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSetInputFilesAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.SetInputFilesAsync("#f", new FilePayload
                {
                    Name = "wave.txt",
                    MimeType = "text/plain",
                    Buffer = Encoding.UTF8.GetBytes("hello"),
                }, timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-set-input-files.spec.ts", "page SetInputFilesAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSetInputFilesAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task setTask = page.SetInputFilesAsync("#f", new FilePayload
            {
                Name = "wave.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello"),
            }, timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=\"f\" type=\"file\" />')")
                .ConfigureAwait(false);
            await setTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.querySelector('#f').files[0].name").ConfigureAwait(false), Is.EqualTo("wave.txt"));
        }

        [PlaywrightTest("browsercontext-viewport.spec.ts", "should report viewport size")]
        [Test]
        [Timeout(30_000)]
        public async Task ViewportSizeShouldMatchSetViewportSize()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetViewportSizeAsync(512, 384).ConfigureAwait(false);
            Assert.That(page.ViewportSize.Width, Is.EqualTo(512));
            Assert.That(page.ViewportSize.Height, Is.EqualTo(384));
        }

        [PlaywrightTest("page-basic.spec.ts", "should store default timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task DefaultTimeoutShouldRoundTrip()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.SetDefaultTimeout(1234);
            page.SetDefaultNavigationTimeout(2345);
            Assert.That(page.DefaultTimeout, Is.EqualTo(1234f));
            Assert.That(page.DefaultNavigationTimeout, Is.EqualTo(2345f));
            await page.SetExtraHttpHeadersAsync(new[] { new KeyValuePair<string, string>("X-Wave", "batch") }).ConfigureAwait(false);
        }
    }
}
