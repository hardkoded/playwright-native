/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>elementhandle-convenience.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleConvenienceTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19170;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    EmptyPage = origin + "/empty.html";
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

        [PlaywrightTest("elementhandle-convenience.spec.ts", "should have a nice preview")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveANicePreview()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            IElementHandle outer = await page.QuerySelectorAsync("#outer").ConfigureAwait(false);
            IElementHandle inner = await page.QuerySelectorAsync("#inner").ConfigureAwait(false);
            IElementHandle check = await page.QuerySelectorAsync("#check").ConfigureAwait(false);
            IJSHandle text = await inner.EvaluateHandleAsync("e => e.firstChild").ConfigureAwait(false);
            await page.EvaluateAsync("(() => 1)()").ConfigureAwait(false);

            Assert.That(await WaitPreviewAsync(outer, "JSHandle@<div id=\"outer\" name=\"value\">…</div>").ConfigureAwait(false), Is.EqualTo("JSHandle@<div id=\"outer\" name=\"value\">…</div>"));
            Assert.That(await WaitPreviewAsync(inner, "JSHandle@<div id=\"inner\">Text,↵more text</div>").ConfigureAwait(false), Is.EqualTo("JSHandle@<div id=\"inner\">Text,↵more text</div>"));
            Assert.That(await WaitPreviewAsync(text, "JSHandle@#text=Text,↵more text").ConfigureAwait(false), Is.EqualTo("JSHandle@#text=Text,↵more text"));
            Assert.That(await WaitPreviewAsync(check, "JSHandle@<input checked id=\"check\" foo=\"bar\"\" type=\"checkbox\"/>").ConfigureAwait(false), Is.EqualTo("JSHandle@<input checked id=\"check\" foo=\"bar\"\" type=\"checkbox\"/>"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "should have a nice preview for non-ascii attributes/children")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHaveANicePreviewForNonAsciiAttributesChildren()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            string emoji = RepeatUtf16("😛", 100);
            await page.SetContentAsync("<div title=\"" + emoji + "\">" + emoji).ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("div").ConfigureAwait(false);

            string expected = "JSHandle@<div title=\"" + emoji + "\">" + RepeatUtf16("😛", 49) + "…</div>";
            Assert.That(await WaitPreviewAsync(handle, expected).ConfigureAwait(false), Is.EqualTo(expected));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "getAttribute should work")]
        [Test]
        [Timeout(30_000)]
        public async Task GetAttributeShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#outer").ConfigureAwait(false);
            Assert.That(await handle.GetAttributeAsync("name").ConfigureAwait(false), Is.EqualTo("value"));
            Assert.That(await handle.GetAttributeAsync("foo").ConfigureAwait(false), Is.Null);
            Assert.That(await page.GetAttributeAsync("#outer", "name").ConfigureAwait(false), Is.EqualTo("value"));
            Assert.That(await page.GetAttributeAsync("#outer", "foo").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "inputValue should work")]
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
            IElementHandle handle = await page.QuerySelectorAsync("#input").ConfigureAwait(false);
            Assert.That(await handle.InputValueAsync().ConfigureAwait(false), Is.EqualTo("input value"));

            Exception pageError = Assert.CatchAsync<Exception>(() => page.InputValueAsync("#inner"));
            Assert.That(pageError, Is.Not.Null);
            Assert.That(pageError.Message, Does.Contain("Node is not an <input>, <textarea> or <select> element"));

            IElementHandle handle2 = await page.QuerySelectorAsync("#inner").ConfigureAwait(false);
            Exception handleError = Assert.CatchAsync<Exception>(() => handle2.InputValueAsync());
            Assert.That(handleError, Is.Not.Null);
            Assert.That(handleError.Message, Does.Contain("Node is not an <input>, <textarea> or <select> element"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "innerHTML should work")]
        [Test]
        [Timeout(30_000)]
        public async Task InnerHTMLShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#outer").ConfigureAwait(false);
            Assert.That(await handle.InnerHTMLAsync().ConfigureAwait(false), Is.EqualTo("<div id=\"inner\">Text,\nmore text</div>"));
            Assert.That(await page.InnerHTMLAsync("#outer").ConfigureAwait(false), Is.EqualTo("<div id=\"inner\">Text,\nmore text</div>"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "innerText should work")]
        [Test]
        [Timeout(30_000)]
        public async Task InnerTextShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#inner").ConfigureAwait(false);
            Assert.That(await handle.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("Text, more text"));
            Assert.That(await page.InnerTextAsync("#inner").ConfigureAwait(false), Is.EqualTo("Text, more text"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "innerText should throw")]
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

            IElementHandle handle = await page.QuerySelectorAsync("svg").ConfigureAwait(false);
            Exception error2 = Assert.CatchAsync<Exception>(() => handle.InnerTextAsync());
            Assert.That(error2, Is.Not.Null);
            Assert.That(error2.Message, Does.Contain("Node is not an HTMLElement"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "textContent should work")]
        [Test]
        [Timeout(30_000)]
        public async Task TextContentShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/dom.html").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#inner").ConfigureAwait(false);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Text,\nmore text"));
            Assert.That(await page.TextContentAsync("#inner").ConfigureAwait(false), Is.EqualTo("Text,\nmore text"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "textContent should work on ShadowRoot")]
        [Test]
        [Timeout(30_000)]
        public async Task TextContentShouldWorkOnShadowRoot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div></div>
    <script>
      document.querySelector('div').attachShadow({ mode: 'open' }).innerHTML = '<div>hello</div>';
    </script>
  ").ConfigureAwait(false);

            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            IJSHandle root = await div.EvaluateHandleAsync("div => div.shadowRoot").ConfigureAwait(false);
            IElementHandle rootElement = root.AsElement();
            Assert.That(rootElement, Is.Not.Null);
            Assert.That(await rootElement.TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello"));
            IReadOnlyList<IElementHandle> scoped = await rootElement.QuerySelectorAllAsync(":scope div").ConfigureAwait(false);
            Assert.That(scoped, Is.Empty);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "isVisible and isHidden should work")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleAndIsHiddenShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Hi</div><span></span>").ConfigureAwait(false);

            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Assert.That(await div.IsVisibleAsync().ConfigureAwait(false), Is.True);
            Assert.That(await div.IsHiddenAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsVisibleAsync("div").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsHiddenAsync("div").ConfigureAwait(false), Is.False);

            IElementHandle span = await page.QuerySelectorAsync("span").ConfigureAwait(false);
            Assert.That(await span.IsVisibleAsync().ConfigureAwait(false), Is.False);
            Assert.That(await span.IsHiddenAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsVisibleAsync("span").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("span").ConfigureAwait(false), Is.True);

            Assert.That(await page.IsVisibleAsync("no-such-element").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsHiddenAsync("no-such-element").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "isVisible should not throw when the DOM element is not connected")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleShouldNotThrowWhenTheDomElementIsNotConnected()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"root\"></div>").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
    function insert() {
      document.getElementById('root').innerHTML = '<div id=""problem"">Problem</div>';
      window.requestAnimationFrame(remove);
    }
    function remove() {
      const node = document.getElementById('problem');
      node && node.parentNode && node.parentNode.removeChild(node);
      window.requestAnimationFrame(insert);
    }
    window.requestAnimationFrame(insert);
  })()").ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
            {
                await page.IsVisibleAsync("#problem").ConfigureAwait(false);
            }
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "isEnabled and isDisabled should work")]
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

            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Assert.That(await div.IsEnabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await div.IsDisabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEnabledAsync("div").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsDisabledAsync("div").ConfigureAwait(false), Is.False);

            IElementHandle button1 = await page.QuerySelectorAsync(":text(\"button1\")").ConfigureAwait(false);
            Assert.That(await button1.IsEnabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await button1.IsDisabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsEnabledAsync(":text(\"button1\")").ConfigureAwait(false), Is.False);
            Assert.That(await page.IsDisabledAsync(":text(\"button1\")").ConfigureAwait(false), Is.True);

            IElementHandle button2 = await page.QuerySelectorAsync(":text(\"button2\")").ConfigureAwait(false);
            Assert.That(await button2.IsEnabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await button2.IsDisabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEnabledAsync(":text(\"button2\")").ConfigureAwait(false), Is.True);
            Assert.That(await page.IsDisabledAsync(":text(\"button2\")").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "isEnabled and isDisabled should work with <select/> option/optgroup correctly")]
        [Test]
        [Timeout(30_000)]
        public async Task IsEnabledAndIsDisabledShouldWorkWithSelectOptionOptgroupCorrectly()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <select name=""select"">
      <option id=""enabled1"" value=""1"">Enabled</option>
      <option id=""disabled1"" value=""2"" disabled>Disabled</option>
      <optgroup label=""Foo1"">
        <option value=""mercedes"">Mercedes</option>
      </optgroup>
      <optgroup label=""Foo2"" disabled>
        <option value=""mercedes"">Mercedes</option>
      </optgroup>
    </select>
  ").ConfigureAwait(false);

            Assert.That(await (await page.QuerySelectorAsync("#enabled1").ConfigureAwait(false)).IsEnabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await (await page.QuerySelectorAsync("#enabled1").ConfigureAwait(false)).IsDisabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await (await page.QuerySelectorAsync("#disabled1").ConfigureAwait(false)).IsEnabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await (await page.QuerySelectorAsync("#disabled1").ConfigureAwait(false)).IsDisabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await (await page.QuerySelectorAsync("optgroup >> nth=0").ConfigureAwait(false)).IsEnabledAsync().ConfigureAwait(false), Is.True);
            Assert.That(await (await page.QuerySelectorAsync("optgroup >> nth=0").ConfigureAwait(false)).IsDisabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await (await page.QuerySelectorAsync("optgroup >> nth=1").ConfigureAwait(false)).IsEnabledAsync().ConfigureAwait(false), Is.False);
            Assert.That(await (await page.QuerySelectorAsync("optgroup >> nth=1").ConfigureAwait(false)).IsDisabledAsync().ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "isEditable should work")]
        [Test]
        [Timeout(30_000)]
        public async Task IsEditableShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=input1 disabled><textarea></textarea><input id=input2>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<bool>("textarea", "t => { t.readOnly = true; return true; }").ConfigureAwait(false);

            IElementHandle input1 = await page.QuerySelectorAsync("#input1").ConfigureAwait(false);
            Assert.That(await input1.IsEditableAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEditableAsync("#input1").ConfigureAwait(false), Is.False);
            IElementHandle input2 = await page.QuerySelectorAsync("#input2").ConfigureAwait(false);
            Assert.That(await input2.IsEditableAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsEditableAsync("#input2").ConfigureAwait(false), Is.True);
            IElementHandle textarea = await page.QuerySelectorAsync("textarea").ConfigureAwait(false);
            Assert.That(await textarea.IsEditableAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsEditableAsync("textarea").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "isChecked should work")]
        [Test]
        [Timeout(30_000)]
        public async Task IsCheckedShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='checkbox' checked><div>Not a checkbox</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            Assert.That(await handle.IsCheckedAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.IsCheckedAsync("input").ConfigureAwait(false), Is.True);
            await handle.EvaluateAsync<bool>("input => { input.checked = false; return true; }").ConfigureAwait(false);
            Assert.That(await handle.IsCheckedAsync().ConfigureAwait(false), Is.False);
            Assert.That(await page.IsCheckedAsync("input").ConfigureAwait(false), Is.False);

            Exception error = Assert.CatchAsync<Exception>(() => page.IsCheckedAsync("div"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Not a checkbox or radio button"));
        }

        private string RepeatUtf16(string unit, int count)
        {
            StringBuilder builder = new StringBuilder(unit.Length * count);
            for (int i = 0; i < count; i++)
            {
                builder.Append(unit);
            }

            return builder.ToString();
        }

        private async Task<string> WaitPreviewAsync(IJSHandle handle, string expected)
        {
            string preview = handle.ToString();
            for (int i = 0; i < 50 && preview != expected; i++)
            {
                await Task.Delay(50).ConfigureAwait(false);
                preview = handle.ToString();
            }

            return preview;
        }
    }
}
