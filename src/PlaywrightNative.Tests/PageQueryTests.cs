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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for page-level element queries: GetAttribute, InnerText,
    /// TextContent, IsChecked, IsEnabled, IsDisabled, IsEditable.
    /// </summary>
    [TestFixture]
    public class PageQueryTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-convenience.spec.ts", "should get attribute and text")]
        [Test]
        [Timeout(30_000)]
        public async Task GetAttributeInnerTextAndTextContentShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=\"box\" data-x=\"1\">hello world</div>").ConfigureAwait(false);
            Assert.That(await page.GetAttributeAsync("#box", "data-x").ConfigureAwait(false), Is.EqualTo("1"));
            Assert.That(await page.InnerTextAsync("#box").ConfigureAwait(false), Does.Contain("hello world"));
            Assert.That(await page.TextContentAsync("#box").ConfigureAwait(false), Does.Contain("hello world"));
            Assert.That(await page.InnerHTMLAsync("#box").ConfigureAwait(false), Does.Contain("hello world"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page GetAttributeAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGetAttributeAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.GetAttributeAsync("#missing", "id", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page GetAttributeAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageGetAttributeAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<string> attrTask = page.GetAttributeAsync("#x", "data-v", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<span id=\"x\" data-v=\"ok\"></span>')")
                .ConfigureAwait(false);
            Assert.That(await attrTask.ConfigureAwait(false), Is.EqualTo("ok"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page InnerTextAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageInnerTextAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.InnerTextAsync("#missing", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page InnerTextAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageInnerTextAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<string> textTask = page.InnerTextAsync("#x", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<span id=\"x\">hello</span>')")
                .ConfigureAwait(false);
            Assert.That(await textTask.ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page InnerHTMLAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageInnerHTMLAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.InnerHTMLAsync("#missing", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page InnerHTMLAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageInnerHTMLAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<string> htmlTask = page.InnerHTMLAsync("#x", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<span id=\"x\"><b>ok</b></span>')")
                .ConfigureAwait(false);
            Assert.That(await htmlTask.ConfigureAwait(false), Does.Contain("<b>ok</b>"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page TextContentAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTextContentAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.TextContentAsync("#missing", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page TextContentAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageTextContentAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<string> textTask = page.TextContentAsync("#x", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<span id=\"x\">hello</span>')")
                .ConfigureAwait(false);
            Assert.That(await textTask.ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "should check enabled and disabled")]
        [Test]
        [Timeout(30_000)]
        public async Task IsEnabledAndIsDisabledShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button id=\"on\">Go</button><button id=\"off\" disabled>Stop</button>").ConfigureAwait(false);
            Assert.That(await page.IsEnabledAsync("#on").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsDisabledAsync("#on").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEnabledAsync("#off").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsDisabledAsync("#off").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "should check checked and editable")]
        [Test]
        [Timeout(30_000)]
        public async Task IsCheckedAndIsEditableShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"yes\" type=\"checkbox\" checked/><input id=\"no\" type=\"checkbox\"/><input id=\"ro\" value=\"x\" readonly/>").ConfigureAwait(false);
            Assert.That(await page.IsCheckedAsync("#yes").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsCheckedAsync("#no").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEditableAsync("#no").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsEditableAsync("#ro").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "page IsCheckedAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsCheckedAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.IsCheckedAsync("#c", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("page-check.spec.ts", "page IsCheckedAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsCheckedAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<bool> checkedTask = page.IsCheckedAsync("#c", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=\"c\" type=\"checkbox\" checked />')")
                .ConfigureAwait(false);
            Assert.That(await checkedTask.ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page IsDisabledAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsDisabledAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.IsDisabledAsync("#b", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page IsDisabledAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsDisabledAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<bool> disabledTask = page.IsDisabledAsync("#b", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<button id=\"b\" disabled>stop</button>')")
                .ConfigureAwait(false);
            Assert.That(await disabledTask.ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page IsEditableAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsEditableAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.IsEditableAsync("#i", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page IsEditableAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsEditableAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<bool> editableTask = page.IsEditableAsync("#i", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=\"i\" />')")
                .ConfigureAwait(false);
            Assert.That(await editableTask.ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page IsEnabledAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsEnabledAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div></div>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.IsEnabledAsync("#b", new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "page IsEnabledAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageIsEnabledAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<bool> enabledTask = page.IsEnabledAsync("#b", new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<button id=\"b\">go</button>')")
                .ConfigureAwait(false);
            Assert.That(await enabledTask.ConfigureAwait(false), Is.True);
        }
    }
}
