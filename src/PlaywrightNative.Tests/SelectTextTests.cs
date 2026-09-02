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
    /// Direct-connection tests for <see cref="IElementHandle.SelectTextAsync"/>.
    /// </summary>
    [TestFixture]
    public class SelectTextTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-select-text.spec.ts", "SelectTextAsync selects input text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectInputText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"hello\" />").ConfigureAwait(false);

            IElementHandle input = await page.QuerySelectorAsync("#n").ConfigureAwait(false);
            await input.SelectTextAsync().ConfigureAwait(false);

            Assert.That(await input.EvaluateAsync<int>("el => el.selectionStart").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await input.EvaluateAsync<int>("el => el.selectionEnd").ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "page SelectTextAsync selects textarea text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectTextareaFromPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<textarea id=\"t\">abcde</textarea>").ConfigureAwait(false);

            await page.SelectTextAsync("#t").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<int>("#t", "el => el.selectionStart").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAsync<int>("#t", "el => el.selectionEnd").ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "SelectTextAsync selects contenteditable text")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectContentEditableText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"c\" contenteditable=\"true\">xyz</div>").ConfigureAwait(false);

            await page.SelectTextAsync("#c").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("window.getSelection().toString()").ConfigureAwait(false), Is.EqualTo("xyz"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "frame SelectTextAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectTextOnMainFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"frame\" />").ConfigureAwait(false);

            await page.MainFrame.SelectTextAsync("#n").ConfigureAwait(false);
            Assert.That(await page.MainFrame.EvalOnSelectorAsync<int>("#n", "el => el.selectionEnd").ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "SelectTextAsync throws on a non-text element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnNonTextElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"d\">nope</div>").ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => page.SelectTextAsync("#d"));
            Assert.That(ex.Message, Does.Contain("not an <input>"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "page SelectTextAsync times out while missing")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSelectTextAsyncShouldTimeoutWhileMissing()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<p>only</p>").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => page.SelectTextAsync(".nope", timeout: 200));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "page SelectTextAsync waits until attached")]
        [Test]
        [Timeout(30_000)]
        public async Task PageSelectTextAsyncShouldWaitUntilAttached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task selectTask = page.SelectTextAsync("#n", timeout: 5000);
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.getElementById('host').insertAdjacentHTML('beforeend', '<input id=\"n\" value=\"hello\" />')")
                .ConfigureAwait(false);
            await selectTask.ConfigureAwait(false);

            int end = await page.EvaluateAsync<int>("document.querySelector('#n').selectionEnd").ConfigureAwait(false);
            Assert.That(end, Is.EqualTo(5));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "SelectTextAsync times out while hidden")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWhileHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"hello\" style=\"display:none\" />").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("#n").ConfigureAwait(false);

            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                () => input.SelectTextAsync(new() { Timeout = 200 }));
            Assert.That(ex.Message, Does.Contain("Timeout"));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "SelectTextAsync force selects hidden input")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHonorForceOnHiddenInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"hello\" style=\"display:none\" />").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("#n").ConfigureAwait(false);

            await input.SelectTextAsync(new() { Timeout = 200, Force = true }).ConfigureAwait(false);
            Assert.That(await input.EvaluateAsync<int>("el => el.selectionEnd").ConfigureAwait(false), Is.EqualTo(5));
        }

        [PlaywrightTest("elementhandle-select-text.spec.ts", "SelectTextAsync waits until visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitUntilVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"n\" value=\"hello\" style=\"display:none\" />").ConfigureAwait(false);
            IElementHandle input = await page.QuerySelectorAsync("#n").ConfigureAwait(false);

            Task selectTask = input.SelectTextAsync(new() { Timeout = 5000 });
            await Task.Delay(200).ConfigureAwait(false);
            await page.EvaluateAsync<object>("document.querySelector('#n').style.display = 'block'").ConfigureAwait(false);
            await selectTask.ConfigureAwait(false);

            Assert.That(await input.EvaluateAsync<int>("el => el.selectionEnd").ConfigureAwait(false), Is.EqualTo(5));
        }
    }
}
