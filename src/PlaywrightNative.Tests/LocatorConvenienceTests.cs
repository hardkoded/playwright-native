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
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator-convenience.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class LocatorConvenienceTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19140;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [PlaywrightTest("locator-convenience.spec.ts", "should have a nice preview")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveANicePreview()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            ILocator outer = page.Locator("#outer");
            ILocator inner = outer.Locator("#inner");
            ILocator check = page.Locator("#check");
            IJSHandle text = await inner.EvaluateHandleAsync("e => e.firstChild").ConfigureAwait(false);
            await page.EvaluateAsync("() => 1").ConfigureAwait(false);

            string textPreview = text.ToString();
            for (int i = 0; i < 50 && textPreview != "JSHandle@#text=Text,↵more text"; i++)
            {
                await Task.Delay(50).ConfigureAwait(false);
                textPreview = text.ToString();
            }

            Assert.That(outer.ToString(), Is.EqualTo("locator('#outer')"));
            Assert.That(inner.ToString(), Is.EqualTo("locator('#outer').locator('#inner')"));
            Assert.That(textPreview, Is.EqualTo("JSHandle@#text=Text,↵more text"));
            Assert.That(check.ToString(), Is.EqualTo("locator('#check')"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "getAttribute should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetAttributeShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            ILocator locator = page.Locator("#outer");
            Assert.That(await locator.GetAttributeAsync("name").ConfigureAwait(false), Is.EqualTo("value"));
            Assert.That(await locator.GetAttributeAsync("foo").ConfigureAwait(false), Is.Null);
            Assert.That(await page.GetAttributeAsync("#outer", "name").ConfigureAwait(false), Is.EqualTo("value"));
            Assert.That(await page.GetAttributeAsync("#outer", "foo").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "inputValue should work")]
        [Test]
        [Timeout(30_000)]
        public async Task InputValueShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            await page.SelectOptionAsync("#select", "foo").ConfigureAwait(false);
            Assert.That(await page.InputValueAsync("#select").ConfigureAwait(false), Is.EqualTo("foo"));

            await page.FillAsync("#textarea", "text value").ConfigureAwait(false);
            Assert.That(await page.InputValueAsync("#textarea").ConfigureAwait(false), Is.EqualTo("text value"));

            await page.FillAsync("#input", "input value").ConfigureAwait(false);
            Assert.That(await page.InputValueAsync("#input").ConfigureAwait(false), Is.EqualTo("input value"));
            ILocator locator = page.Locator("#input");
            Assert.That(await locator.InputValueAsync().ConfigureAwait(false), Is.EqualTo("input value"));

            Exception pageError = Assert.CatchAsync<Exception>(() => page.InputValueAsync("#inner"));
            Assert.That(pageError, Is.Not.Null);
            Assert.That(pageError.Message, Does.Contain("Node is not an <input>, <textarea> or <select> element"));

            ILocator locator2 = page.Locator("#inner");
            Exception locatorError = Assert.CatchAsync<Exception>(() => locator2.InputValueAsync());
            Assert.That(locatorError, Is.Not.Null);
            Assert.That(locatorError.Message, Does.Contain("Node is not an <input>, <textarea> or <select> element"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "innerHTML should work")]
        [Test]
        [Timeout(30_000)]
        public async Task InnerHTMLShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            ILocator locator = page.Locator("#outer");
            Assert.That(await locator.InnerHTMLAsync().ConfigureAwait(false), Is.EqualTo("<div id=\"inner\">Text,\nmore text</div>"));
            Assert.That(await page.InnerHTMLAsync("#outer").ConfigureAwait(false), Is.EqualTo("<div id=\"inner\">Text,\nmore text</div>"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "innerText should work")]
        [Test]
        [Timeout(30_000)]
        public async Task InnerTextShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            ILocator locator = page.Locator("#inner");
            Assert.That(await locator.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Text, more text"));
            Assert.That(await page.InnerTextAsync("#inner").ConfigureAwait(false), Is.EqualTo("Text, more text"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "innerText should throw")]
        [Test]
        [Timeout(30_000)]
        public async Task InnerTextShouldThrow()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<svg>text</svg>").ConfigureAwait(false);

            Exception error1 = Assert.CatchAsync<Exception>(() => page.InnerTextAsync("svg"));
            Assert.That(error1, Is.Not.Null);
            Assert.That(error1.Message, Does.Contain("Node is not an HTMLElement"));

            ILocator locator = page.Locator("svg");
            Exception error2 = Assert.CatchAsync<Exception>(() => locator.InnerTextAsync());
            Assert.That(error2, Is.Not.Null);
            Assert.That(error2.Message, Does.Contain("Node is not an HTMLElement"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "innerText should produce log")]
        [Test]
        [Timeout(30_000)]
        public async Task InnerTextShouldProduceLog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Hello</div>").ConfigureAwait(false);

            ILocator locator = page.Locator("span");
            Exception error = null;
            try
            {
                await locator.InnerTextAsync(new() { Timeout = 1000f }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("waiting for locator('span')"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "textContent should work")]
        [Test]
        [Timeout(30_000)]
        public async Task TextContentShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            ILocator locator = page.Locator("#inner");
            Assert.That(await locator.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Text,\nmore text"));
            Assert.That(await page.TextContentAsync("#inner").ConfigureAwait(false), Is.EqualTo("Text,\nmore text"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "isEnabled and isDisabled should work")]
        [Test]
        [Timeout(30_000)]
        public async Task IsEnabledAndIsDisabledShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button disabled>button1</button>
    <button>button2</button>
    <div>div</div>
  ").ConfigureAwait(false);

            ILocator div = page.Locator("div");
            Assert.That(await div.IsEnabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await div.IsDisabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEnabledAsync("div").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsDisabledAsync("div").ConfigureAwait(false), Is.False);

            ILocator button1 = page.Locator(":text(\"button1\")");
            Assert.That(await button1.IsEnabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await button1.IsDisabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsEnabledAsync(":text(\"button1\")").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsDisabledAsync(":text(\"button1\")").ConfigureAwait(false), Is.True);

            ILocator button2 = page.Locator(":text(\"button2\")");
            Assert.That(await button2.IsEnabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await button2.IsDisabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEnabledAsync(":text(\"button2\")").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsDisabledAsync(":text(\"button2\")").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "isEditable should work")]
        [Test]
        [Timeout(30_000)]
        public async Task IsEditableShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <input id=input1 disabled>
    <textarea></textarea>
    <input id=input2>
    <div contenteditable=""true""></div>
    <span id=span1 role=textbox aria-readonly=true></span>
    <span id=span2 role=textbox></span>
    <button>button</button>
  ").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<bool>("textarea", "t => { t.readOnly = true; return true; }").ConfigureAwait(false);

            ILocator input1 = page.Locator("#input1");
            Assert.That(await input1.IsEditableAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEditableAsync("#input1").ConfigureAwait(false), Is.False);
            ILocator input2 = page.Locator("#input2");
            Assert.That(await input2.IsEditableAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsEditableAsync("#input2").ConfigureAwait(false), Is.True);
            ILocator textarea = page.Locator("textarea");
            Assert.That(await textarea.IsEditableAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEditableAsync("textarea").ConfigureAwait(false), Is.False);
            Assert.That(await page.Locator("div").IsEditableAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("#span1").IsEditableAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.Locator("#span2").IsEditableAsync().ConfigureAwait(false), Is.True);

            Exception error = Assert.CatchAsync<Exception>(() => page.Locator("button").IsEditableAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(
                error.Message,
                Does.Contain("Element is not an <input>, <textarea>, <select> or [contenteditable] and does not have a role allowing [aria-readonly]"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "isChecked should work")]
        [Test]
        [Timeout(30_000)]
        public async Task IsCheckedShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='checkbox' checked><div>Not a checkbox</div>").ConfigureAwait(false);

            ILocator element = page.Locator("input");
            Assert.That(await element.IsCheckedAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsCheckedAsync("input").ConfigureAwait(false), Is.True);
            await element.EvaluateAsync<bool>("input => { input.checked = false; return true; }").ConfigureAwait(false);
            Assert.That(await element.IsCheckedAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsCheckedAsync("input").ConfigureAwait(false), Is.False);

            Exception error = Assert.CatchAsync<Exception>(() => page.IsCheckedAsync("div"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Not a checkbox or radio button"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "isChecked should work for indeterminate input")]
        [Test]
        [Timeout(30_000)]
        public async Task IsCheckedShouldWorkForIndeterminateInput()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type=\"checkbox\" checked>").ConfigureAwait(false);
            await page.Locator("input").EvaluateAsync<bool>("e => { e.indeterminate = true; return true; }").ConfigureAwait(false);

            Assert.That(await page.Locator("input").IsCheckedAsync().ConfigureAwait(false), Is.True);
            await Assertions.Expect(page.Locator("input")).ToBeCheckedAsync().ConfigureAwait(false);

            await page.Locator("input").UncheckAsync().ConfigureAwait(false);

            Assert.That(await page.Locator("input").IsCheckedAsync().ConfigureAwait(false), Is.False);
            await Assertions.Expect(page.Locator("input")).Not.ToBeCheckedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "allTextContents should work")]
        [Test]
        [Timeout(30_000)]
        public async Task AllTextContentsShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>A</div><div>B</div><div>C</div>").ConfigureAwait(false);

            IReadOnlyList<string> texts = await page.Locator("div").AllTextContentsAsync().ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "allInnerTexts should work")]
        [Test]
        [Timeout(30_000)]
        public async Task AllInnerTextsShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>A</div><div>B</div><div>C</div>").ConfigureAwait(false);

            IReadOnlyList<string> texts = await page.Locator("div").AllInnerTextsAsync().ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "should return page")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/two-frames.html").ConfigureAwait(false);

            ILocator outer = page.Locator("#outer");
            Assert.That(outer.Page, Is.SameAs(page));

            ILocator inner = outer.Locator("#inner");
            Assert.That(inner.Page, Is.SameAs(page));

            IFrame[] frames = new List<IFrame>(page.Frames).ToArray();
            Assert.That(frames.Length, Is.GreaterThan(1));
            ILocator inFrame = frames[1].Locator("div");
            Assert.That(inFrame.Page, Is.SameAs(page));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "description should return null for locator without description")]
        [Test]
        [Timeout(30_000)]
        public async Task DescriptionShouldReturnNullForLocatorWithoutDescription()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator locator = page.Locator("button");
            Assert.That(locator.Description, Is.Null);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "description should return description for locator with simple description")]
        [Test]
        [Timeout(30_000)]
        public async Task DescriptionShouldReturnDescriptionForLocatorWithSimpleDescription()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator locator = page.Locator("button").Describe("Submit button");
            Assert.That(locator.Description, Is.EqualTo("Submit button"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "description should return description with special characters")]
        [Test]
        [Timeout(30_000)]
        public async Task DescriptionShouldReturnDescriptionWithSpecialCharacters()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator locator = page.Locator("div").Describe("Button with \"quotes\" and 'apostrophes'");
            Assert.That(locator.Description, Is.EqualTo("Button with \"quotes\" and 'apostrophes'"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "description should return description for chained locators")]
        [Test]
        [Timeout(30_000)]
        public async Task DescriptionShouldReturnDescriptionForChainedLocators()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator locator = page.Locator("form").Locator("input").Describe("Form input field");
            Assert.That(locator.Description, Is.EqualTo("Form input field"));
        }

        [PlaywrightTest("locator-convenience.spec.ts", "description should return description for locator with multiple describe calls")]
        [Test]
        [Timeout(30_000)]
        public async Task DescriptionShouldReturnDescriptionForLocatorWithMultipleDescribeCalls()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator locator1 = page.Locator("foo").Describe("First description");
            Assert.That(locator1.Description, Is.EqualTo("First description"));
            ILocator locator2 = locator1.Locator("button").Describe("Second description");
            Assert.That(locator2.Description, Is.EqualTo("Second description"));
            ILocator locator3 = locator2.Locator("button");
            Assert.That(locator3.Description, Is.Null);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "toString() returns formatted locator")]
        [Test]
        [Timeout(30_000)]
        public async Task ToStringReturnsFormattedLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator locator = page.GetByRole("button", name: "Submit");
            Assert.That(locator.ToString(), Is.EqualTo("getByRole('button', { name: 'Submit' })"));
            Assert.That(locator.Description, Is.Null);
        }

        [PlaywrightTest("locator-convenience.spec.ts", "toString() prefers description")]
        [Test]
        [Timeout(30_000)]
        public async Task ToStringPrefersDescription()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator locator = page.GetByRole("button", name: "Submit").Describe("Submit button");
            Assert.That(locator.ToString(), Is.EqualTo("Submit button"));
            Assert.That(locator.ToString(), Is.EqualTo(locator.Description));
        }
    }
}
