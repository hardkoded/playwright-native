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
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>selectors-css.spec.ts</c> parity for CSS combinators,
    /// <c>:nth-child</c> / <c>:not</c> / <c>:is</c> / <c>:has</c> / <c>:scope</c>,
    /// comma lists, and handle-relative CSS.
    /// </summary>
    [TestFixture]
    public class SelectorsCssParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl.TrimEnd('/');

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19797;
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

        [SetUp]
        public void ResetOwnedRoutes()
        {
            _ownedServer?.Reset();
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with large DOM")]
        [PlaywrightTest("selectors-css.spec.ts", "should work with large DOM @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLargeDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"() => {
    let id = 0;
    const next = (tag) => {
      const e = document.createElement(tag);
      const eid = ++id;
      e.textContent = 'id' + eid;
      e.id = '' + eid;
      return e;
    };
    const generate = (depth) => {
      const div = next('div');
      const span1 = next('span');
      const span2 = next('span');
      div.appendChild(span1);
      div.appendChild(span2);
      if (depth > 0) {
        div.appendChild(generate(depth - 1));
        div.appendChild(generate(depth - 1));
      }
      return div;
    };
    document.body.appendChild(generate(12));
}").ConfigureAwait(false);

            string[] selectors =
            {
                "div div div span",
                "div > div div > span",
                "div + div div div span + span",
                "div ~ div div > span ~ span",
                "div > div > div + div > div + div > span ~ span",
                "div div div div div div div div div div span",
                "div > div > div > div > div > div > div > div > div > div > span",
                "div ~ div div ~ div div ~ div div ~ div div ~ div span",
                "span",
            };

            for (int s = 0; s < selectors.Length; s++)
            {
                string selector = selectors[s];
                int[] counts1 = { await page.EvalOnSelectorAllAsync<int>(selector, "els => els.length").ConfigureAwait(false) };
                int[] counts2 = { await page.EvaluateAsync<int>("selector => document.querySelectorAll(selector).length", selector).ConfigureAwait(false) };
                Assert.That(counts1, Is.EqualTo(counts2));
            }
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work for open shadow roots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForOpenShadowRoots()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("css=span", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root1"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=[attr=""value\ space""]", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3 #2"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=[attr='value\ \space']", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3 #2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=div div span", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=div span + span", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3 #2"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=span + [attr*=""value""]", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3 #2"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=[data-testid=""foo""] + [attr*=""value""]", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3 #2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=#target", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=div #target", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=div div #target", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.QuerySelectorAsync("css=div div div #target").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("css=section > div div span", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=section > div div span:nth-child(2)", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3 #2"));
            Assert.That(await page.QuerySelectorAsync("css=section div div div div").ConfigureAwait(false), Is.Null);

            IElementHandle root2 = await page.QuerySelectorAsync("css=div div").ConfigureAwait(false);
            Assert.That(await root2.EvalOnSelectorAsync<string>("css=#target", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await root2.QuerySelectorAsync("css:light=#target").ConfigureAwait(false), Is.Null);
            IElementHandle root2Shadow = (IElementHandle)await root2.EvaluateHandleAsync("r => r.shadowRoot").ConfigureAwait(false);
            Assert.That(await root2Shadow.EvalOnSelectorAsync<string>("css:light=#target", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            IElementHandle root3 = (await page.QuerySelectorAllAsync("css=div div").ConfigureAwait(false))[1];
            Assert.That(await root3.EvalOnSelectorAsync<string>("text=root3", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3"));
            Assert.That(await root3.EvalOnSelectorAsync<string>(@"css=[attr*=""value""]", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3 #2"));
            Assert.That(await root3.QuerySelectorAsync(@"css:light=[attr*=""value""]").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with > combinator and spaces")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithGreaterCombinatorAndSpaces()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"<div foo=""bar"" bar=""baz""><span></span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""] > span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""]> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""] >span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""]>span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""]   >    span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""]>    span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""]     >span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""][bar=""baz""] > span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""][bar=""baz""]> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""][bar=""baz""] >span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""][bar=""baz""]>span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""][bar=""baz""]   >    span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""][bar=""baz""]>    span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"div[foo=""bar""][bar=""baz""]     >span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with comma separated list")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCommaSeparatedList()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span,section #root1", "els => els.length").ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=section #root1, div span", "els => els.length").ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=doesnotexist , section #root1", "e => e.id").ConfigureAwait(false), Is.EqualTo("root1"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=doesnotexist ,section #root1", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span,div span", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span,div span,div div span", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>(@"css=#target,[attr=""value\ space""]", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>(@"css=#target,[data-testid=""foo""],[attr=""value\ space""]", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>(@"css=#target,[data-testid=""foo""],[attr=""value\ space""],span", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should keep dom order with comma separated list")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepDomOrderWithCommaSeparatedList()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><span><div><x></x><y></y></div></span></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=span,div", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("SPAN,DIV"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=div,span", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("SPAN,DIV"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=span div, div", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("DIV"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("*css=section >> css=div,span", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("SECTION"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> *css=div >> css=x,y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("DIV"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> *css=div,span >> css=x,y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("SPAN,DIV"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> *css=div,span >> css=y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("SPAN,DIV"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should return multiple captures for the same node")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnMultipleCapturesForTheSameNode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><div><div><span></span></div></div></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string>("*css=div >> span", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("DIV,DIV,DIV"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should return multiple captures when going up the hierarchy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnMultipleCapturesWhenGoingUpTheHierarchy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section>Hello<ul><li></li><li></li></ul></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string>("*css=li >> ../.. >> text=Hello", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("LI,LI"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with comma separated list in various positions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCommaSeparatedListInVariousPositions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><span><div><x></x><y></y></div></span></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=span,div >> css=x,y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X,Y"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=span,div >> css=x", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=div >> css=x,y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X,Y"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=div >> css=x", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> css=div >> css=x", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> css=span >> css=div >> css=y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("Y"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> css=div >> css=x,y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X,Y"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> css=div,span >> css=x,y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X,Y"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("css=section >> css=span >> css=x,y", "els => els.map(e => e.nodeName).join(',')").ConfigureAwait(false), Is.EqualTo("X,Y"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with comma inside text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCommaInsideText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"<span></span><div attr=""hello,world!""></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=div[attr=""hello,world!""]", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo(@"<div attr=""hello,world!""></div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=[attr=""hello,world!""]", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo(@"<div attr=""hello,world!""></div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=div[attr='hello,world!']", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo(@"<div attr=""hello,world!""></div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=[attr='hello,world!']", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo(@"<div attr=""hello,world!""></div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(@"css=div[attr=""hello,world!""],span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with attribute selectors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAttributeSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"<div attr=""hello world"" attr2=""hello-''>>foo=bar[]"" attr3=""] span""><span></span></div>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"() => { window.div = document.querySelector('div'); }").ConfigureAwait(false);
            string[] selectors =
            {
                @"[attr=""hello world""]",
                @"[attr = ""hello world""]",
                "[attr ~= world]",
                "[attr ^=hello ]",
                "[attr $= world ]",
                @"[attr *= ""llo wor"" ]",
                "[attr2 |= hello]",
                @"[attr = ""Hello World"" i ]",
                @"[attr *= ""llo WOR""i]",
                "[attr $= woRLD i]",
                @"[attr2 = ""hello-''>>foo=bar[]""]",
                @"[attr2 $=""foo=bar[]""]",
            };
            for (int i = 0; i < selectors.Length; i++)
            {
                Assert.That(await page.EvalOnSelectorAsync<bool>(selectors[i], "e => e === window.div").ConfigureAwait(false), Is.True);
            }

            Assert.That(await page.EvalOnSelectorAsync<bool>("[attr*=hello] span", "e => e.parentNode === window.div").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvalOnSelectorAsync<bool>("[attr*=hello] >> span", "e => e.parentNode === window.div").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvalOnSelectorAsync<bool>(@"[attr3=""] span""] >> span", "e => e.parentNode === window.div").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("selectors-css.spec.ts", "should not match root after >>")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotMatchRootAfter()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><div>test</div></section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("css=section >> css=section").ConfigureAwait(false);
            Assert.That(element, Is.Null);
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with numerical id")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNumericalId()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"<section id=""123""></section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync(@"#\31\32\33").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with wrong-case id")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithWrongCaseId()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"<section id=""Hello""></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("#Hello", "e => e.tagName").ConfigureAwait(false), Is.EqualTo("SECTION"));
            Assert.That(await page.EvalOnSelectorAsync<string>("#hello", "e => e.tagName").ConfigureAwait(false), Is.EqualTo("SECTION"));
            Assert.That(await page.EvalOnSelectorAsync<string>("#HELLO", "e => e.tagName").ConfigureAwait(false), Is.EqualTo("SECTION"));
            Assert.That(await page.EvalOnSelectorAsync<string>("#helLO", "e => e.tagName").ConfigureAwait(false), Is.EqualTo("SECTION"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with *")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithStar()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=div1></div><div id=div2><span><span></span></span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*", "els => els.length").ConfigureAwait(false), Is.EqualTo(7));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*#div1", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*:not(#div1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(6));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*:not(div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*:not(span)", "els => els.length").ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*:not(*)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*:is(*)", "els => els.length").ConfigureAwait(false), Is.EqualTo(7));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("* *", "els => els.length").ConfigureAwait(false), Is.EqualTo(6));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("* *:not(span)", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div > *", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div *", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("* > *", "els => els.length").ConfigureAwait(false), Is.EqualTo(6));

            IElementHandle body = await page.QuerySelectorAsync("body").ConfigureAwait(false);
            Assert.That(await body.EvalOnSelectorAllAsync<int>("*", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("*#div1", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("*:not(#div1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("*:not(div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("*:not(span)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("*:not(*)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("*:is(*)", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("div > *", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("div *", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("* > *", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await body.EvalOnSelectorAllAsync<int>(":scope * > *", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("* *", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await body.EvalOnSelectorAllAsync<int>("* *:not(span)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with :nth-child")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNthChild()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(odd)", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(even)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(n+1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(n+2)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(2n)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(2n+1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(-n)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(-n+1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(-n+2)", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(23n+2)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with :nth-child(of) notation with nested functions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNthChildOfNotationWithNestedFunctions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div>
      <span>span1</span>
      <span class=foo>span2<dd></dd></span>
      <span class=foo>span3<dd class=marker></dd></span>
      <span class=foo>span4<dd class=marker></dd></span>
      <span class=foo>span5<dd></dd></span>
      <span>span6</span>
    </div>
  ").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-child(1)", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "span1" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-child(1 of .foo)", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "span2" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-child(1 of .foo:has(dd.marker))", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "span3" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-last-child(1 of .foo:has(dd.marker))", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "span4" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-last-child(1 of .foo)", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "span5" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-last-child(  1  )", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "span6" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-child(1 of .foo:nth-child(3))", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "span3" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("css=span:nth-child(1 of .foo:nth-child(6))", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with :not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNot()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:not(#root1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=body :not(span)", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div > :not(span):not(div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with ~")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithTilde()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div id=div1></div>
    <div id=div2></div>
    <div id=div3></div>
    <div id=div4></div>
    <div id=div5></div>
    <div id=div6></div>
  ").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> :scope ~ div", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div4", "div5", "div6" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> :scope ~ *", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div4", "div5", "div6" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> ~ div", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div4", "div5", "div6" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> ~ *", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div4", "div5", "div6" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> #div1 ~ :scope", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div3" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> #div4 ~ :scope", "els => els.map(e => e.id)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div1 ~ div ~ #div6", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div1 ~ div ~ div", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div3 ~ div ~ div", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div4 ~ div ~ div", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div5 ~ div ~ div", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div3 ~ #div2 ~ #div6", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div3 ~ #div4 ~ #div5", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with +")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithPlus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <section>
      <div id=div1></div>
      <div id=div2></div>
      <div id=div3></div>
      <div id=div4></div>
      <div id=div5></div>
      <div id=div6></div>
    </section>
  ").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div1 >> :scope+div", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div2" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div1 >> :scope+*", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div2" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div1 >> + div", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div2" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div1 >> + *", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div2" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> div + :scope", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "div3" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#div3 >> #div1 + :scope", "els => els.map(e => e.id)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div1 ~ div + #div6", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div1 ~ div + div", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div3 + div + div", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div4 ~ #div5 + div", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div5 + div + div", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div3 ~ #div2 + #div6", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#div3 + #div4 + #div5", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div + #div1", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=section > div + div ~ div", "els => els.length").ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=section > div + #div4 ~ div", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=section:has(:scope > div + #div2)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=section:has(:scope > div + #div1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=div:has(:scope + #div5)", "e => e.id").ConfigureAwait(false), Is.EqualTo("div4"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with spaces in :nth-child and :not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithSpacesInNthChildAndNot()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(23n +2)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child(23n+ 2)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:nth-child( 23n + 2 )", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:not(#root1 #target)", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:not(:not(#root1 #target))", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span:not(span:not(#root1 #target))", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div > :not(span)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=body :not(span, div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=span, section:not(span, div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("span:nth-child(23n+ 2) >> xpath=.", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with :is")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithIs()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:is(#root1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:is(#root1, #target)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:is(span, #target)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:is(span, #root1 > *)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:is(section div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=:is(div, span)", "els => els.length").ConfigureAwait(false), Is.EqualTo(7));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=section:is(section) div:is(section div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=:is(div, span) > *", "els => els.length").ConfigureAwait(false), Is.EqualTo(6));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#root1:has(:is(#root1))", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=#root1:has(:is(:scope, #root1))", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with :has")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithHas()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:has(#target)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:has([data-testid=foo])", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:has([attr*=value])", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));

            await page.SetContentAsync("<section><span></span><div></div></section><section><br></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section:has(span, div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section:has(span, div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section:has(br)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section:has(span, br)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section:has(span, br, div)", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with :scope")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithScope()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:is(:scope#root1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:is(:scope #root1)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=div:has(:scope > #target)", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));

            IElementHandle handle = await page.QuerySelectorAsync("css=span").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=:scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=* :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=* + :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=* > :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("css=* ~ :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await handle.EvalOnSelectorAllAsync<int>("css=:scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await handle.EvalOnSelectorAllAsync<int>("css=* :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await handle.EvalOnSelectorAllAsync<int>("css=* + :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await handle.EvalOnSelectorAllAsync<int>("css=* > :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await handle.EvalOnSelectorAllAsync<int>("css=* ~ :scope", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));

            await page.SetContentAsync("<article><div class=target>hello<span></span></div></article>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> :scope.target", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> :scope:nth-child(1)", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> :scope.target:has(span)", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await page.EvalOnSelectorAsync<string>("html:scope", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("HTML"));

            await page.SetContentAsync("<section><span id=span1><span id=inner></span></span><span id=span2></span></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#span1 >> span:not(:has(:scope > div))", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "inner" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#span1 >> #inner,:scope", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "span1", "inner" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#span1 >> span,:scope", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "span1", "inner" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("#span1 >> span:not(:scope)", "els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "inner" }));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should work with :scope and class")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithScopeAndClass()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div class=\"apple\"></div>\n                         <div class=\"apple selected\"></div>").ConfigureAwait(false);
            ILocator apples = page.Locator(".apple");
            ILocator selectedApples = apples.Locator(":scope.selected");
            await Assertions.Expect(selectedApples).ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-css.spec.ts", "should absolutize relative selectors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbsolutizeRelativeSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><span>Hi</span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> >span", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hi"));
            Assert.That(await page.Locator("div").Locator(">span").TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hi"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has(> span)", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div><span>Hi</span></div>"));
            Assert.That(await page.QuerySelectorAsync("div:has(> div)").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("selectors-css.spec.ts", "css on the handle should be relative")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CssOnTheHandleShouldBeRelative()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<span class=\"find-me\" id=target1>1</span>\n    <div>\n      <span class=\"find-me\" id=target2>2</span>\n    </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(".find-me", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));

            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Assert.That(await div.EvalOnSelectorAsync<string>(".find-me", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> .find-me", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
        }

        [PlaywrightTest("selectors-css.spec.ts", "should use light DOM structure for child combinator with slotted content")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseLightDomStructureForChildCombinatorWithSlottedContent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <my-button>
      <template shadowrootmode=""open"">
        <button><slot></slot></button>
      </template>
      <div class=""content"">Foo</div>
    </my-button>
  ").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("my-button > div", "e => e.className").ConfigureAwait(false), Is.EqualTo("content"));
            Assert.That(await page.EvalOnSelectorAsync<string>("my-button > .content", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Foo"));
        }
    }
}
