/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>selectors-misc.spec.ts</c> parity for shadow, visible,
    /// nth, layout, xpath, and internal has/and/or/chain selectors.
    /// </summary>
    [TestFixture]
    public class SelectorsMiscParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl.TrimEnd('/');

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19796;
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

        [PlaywrightTest("selectors-misc.spec.ts", "should work for open shadow roots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForOpenShadowRoots()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("id=target", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("data-testid=foo", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root1"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("data-testid=foo", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.QuerySelectorAsync("id:light=target").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("data-testid:light=foo").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAllAsync("data-testid:light=foo").ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should click on links in shadow dom")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickOnLinksInShadowDom()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/shadow-dom-link.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.clickCount").ConfigureAwait(false), Is.EqualTo(0));
            await page.ClickAsync("#inner-link").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.clickCount").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with :visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><div id=target1></div><div id=target2></div></section>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("div:visible").ConfigureAwait(false), Is.Null);

            TimeoutException timeout = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForSelectorAsync("div:visible", new() { Timeout = 1000 }));
            Assert.That(timeout.Message, Does.Contain("1000ms"));

            Task<IElementHandle> promise = page.WaitForSelectorAsync("div:visible", WaitForSelectorState.Attached);
            await page.EvalOnSelectorAsync<object>("#target2", "div => { div.textContent = 'Now visible'; }").ConfigureAwait(false);
            IElementHandle element = await promise.ConfigureAwait(false);
            Assert.That(await element.EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:visible", "div => div.id").ConfigureAwait(false), Is.EqualTo("target2"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with >> visible=")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithVisibleEquals()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><div id=target1></div><div id=target2></div></section>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("div >> visible=true").ConfigureAwait(false), Is.Null);

            TimeoutException timeout = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForSelectorAsync("div >> visible=true", new() { Timeout = 1000 }));
            Assert.That(timeout.Message, Does.Contain("1000ms"));

            Task<IElementHandle> promise = page.WaitForSelectorAsync("div >> visible=true", WaitForSelectorState.Attached);
            await page.EvalOnSelectorAsync<object>("#target2", "div => { div.textContent = 'Now visible'; }").ConfigureAwait(false);
            IElementHandle element = await promise.ConfigureAwait(false);
            Assert.That(await element.EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> visible=true", "div => div.id").ConfigureAwait(false), Is.EqualTo("target2"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with >> visible=false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithVisibleEqualsFalse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><div id=target1></div><div id=target2></div></section>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div >> visible=false")).ToHaveCountAsync(2).ConfigureAwait(false);
            await page.Locator("#target2").EvaluateAsync<object>("div => { div.textContent = 'Now visible'; }").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div >> visible=false")).ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with :nth-match")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNthMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><div id=target1></div><div id=target2></div></section>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync(":nth-match(div, 3)").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>(":nth-match(div, 1)", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":nth-match(div, 2)", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":nth-match(section > div, 2)", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":nth-match(section, div, 2)", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":nth-match(div, section, 3)", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>(":is(:nth-match(div, 1), :nth-match(div, 2))", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync(":nth-match(div, bar, 0)"));
            Assert.That(error.Message, Does.Contain("\"nth-match\" engine expects a one-based index as the last argument"));

            error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync(":nth-match(2)"));
            Assert.That(error.Message, Does.Contain("\"nth-match\" engine expects non-empty selector list and an index argument"));

            error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync(":nth-match(div, bar, foo)"));
            Assert.That(error.Message, Does.Contain("\"nth-match\" engine expects a one-based index as the last argument"));

            Task<IElementHandle> promise = page.WaitForSelectorAsync(":nth-match(div, 3)", WaitForSelectorState.Attached);
            await page.EvalOnSelectorAsync<object>("section", @"section => {
    const div = document.createElement('div');
    div.setAttribute('id', 'target3');
    section.appendChild(div);
}").ConfigureAwait(false);
            IElementHandle element = await promise.ConfigureAwait(false);
            Assert.That(await element.EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target3"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with nth=")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNthEquals()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><div id=target1></div><div id=target2></div></section>").ConfigureAwait(false);
            Assert.That(await page.QuerySelectorAsync("div >> nth=2").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> nth=0", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> nth=1", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("section > div >> nth=1", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("section, div >> nth=1", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div, section >> nth=2", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));

            Task<IElementHandle> promise = page.WaitForSelectorAsync("div >> nth=2", WaitForSelectorState.Attached);
            await page.EvalOnSelectorAsync<object>("section", @"section => {
    const div = document.createElement('div');
    div.setAttribute('id', 'target3');
    section.appendChild(div);
}").ConfigureAwait(false);
            IElementHandle element = await promise.ConfigureAwait(false);
            Assert.That(await element.EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("target3"));

            await page.SetContentAsync("<div><div><div><span>hi</span><span>hello</span></div></div></div>").ConfigureAwait(false);
            Assert.That(await page.Locator("div >> div >> span >> nth=1").TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with strict mode and chaining")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithStrictModeAndChaining()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><div><div><span>hi</span></div></div></div>").ConfigureAwait(false);
            Assert.That(await page.Locator("div >> div >> span").TextContentAsync().ConfigureAwait(false), Is.EqualTo("hi"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with layout selectors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLayoutSelectors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            int[][] boxes =
            {
                new[] { 0, 0, 150, 150 },
                new[] { 100, 200, 50, 50 },
                new[] { 200, 200, 50, 50 },
                new[] { 100, 150, 50, 50 },
                new[] { 201, 150, 50, 50 },
                new[] { 200, 100, 50, 50 },
                new[] { 50, 50, 50, 50 },
                new[] { 150, 50, 50, 50 },
                new[] { 150, -51, 50, 50 },
                new[] { 201, -101, 50, 50 },
            };
            await page.SetContentAsync("<container style=\"width: 500px; height: 500px; position: relative;\"></container>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>(
                "container",
                @"(container, boxes) => {
    for (let i = 0; i < boxes.length; i++) {
      const div = document.createElement('div');
      div.style.position = 'absolute';
      div.style.overflow = 'hidden';
      div.style.boxSizing = 'border-box';
      div.style.border = '1px solid black';
      div.id = 'id' + i;
      div.textContent = 'id' + i;
      const box = boxes[i];
      div.style.left = box[0] + 'px';
      div.style.top = (250 - box[1] - box[3]) + 'px';
      div.style.width = box[2] + 'px';
      div.style.height = box[3] + 'px';
      container.appendChild(div);
      const span = document.createElement('span');
      span.textContent = '' + i;
      div.appendChild(span);
    }
}",
                boxes).ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<string>("div:right-of(#id6)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id7"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:right-of(#id1)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:right-of(#id3)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id4"));
            Assert.That(await page.QuerySelectorAsync("div:right-of(#id4)").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("div:right-of(#id0)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id7"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:right-of(#id8)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id9"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:right-of(#id3)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id4,id2,id5,id7,id8,id9"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:right-of(#id3, 50)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id2,id5,id7,id8"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:right-of(#id3, 49)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id7,id8"));

            Assert.That(await page.EvalOnSelectorAsync<string>("div:left-of(#id2)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id1"));
            Assert.That(await page.QuerySelectorAsync("div:left-of(#id0)").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("div:left-of(#id5)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id0"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:left-of(#id9)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id8"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:left-of(#id4)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id3"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:left-of(#id5)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id0,id7,id3,id1,id6,id8"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:left-of(#id5, 3)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id7,id8"));

            Assert.That(await page.EvalOnSelectorAsync<string>("div:above(#id0)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id3"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:above(#id5)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id4"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:above(#id7)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id5"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:above(#id8)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id0"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:above(#id9)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id8"));
            Assert.That(await page.QuerySelectorAsync("div:above(#id2)").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:above(#id5)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id4,id2,id3,id1"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:above(#id5, 20)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id4,id3"));

            Assert.That(await page.EvalOnSelectorAsync<string>("div:below(#id4)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id5"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:below(#id3)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id0"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:below(#id2)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id4"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:below(#id6)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id8"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:below(#id7)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id8"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:below(#id8)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id9"));
            Assert.That(await page.QuerySelectorAsync("div:below(#id9)").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:below(#id3)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id0,id5,id6,id7,id8,id9"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:below(#id3, 105)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id0,id5,id6,id7"));

            Assert.That(await page.EvalOnSelectorAsync<string>("div:near(#id0)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id3"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:near(#id7)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id0,id5,id3,id6"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:near(#id0)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id3,id6,id7,id8,id1,id5"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:near(#id6)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id0,id3,id7"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:near(#id6, 10)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id0"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:near(#id0, 100)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id3,id6,id7,id8,id1,id5,id4,id2"));

            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:below(#id5):above(#id8)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id7,id6"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:below(#id5):above(#id8)", "e => e.id").ConfigureAwait(false), Is.EqualTo("id7"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("div:right-of(#id0) + div:above(#id8)", "els => els.map(e => e.id).join(',')").ConfigureAwait(false), Is.EqualTo("id5,id6,id3"));

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync(":near(50)"));
            Assert.That(error.Message, Does.Contain("\"near\" engine expects a selector list and optional maximum distance in pixels"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should escape the scope with >>")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEscapeTheScopeWith()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><label>Test</label><input id='myinput'></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("label >> xpath=.. >> input", "e => e.id").ConfigureAwait(false), Is.EqualTo("myinput"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "xpath should be relative")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task XpathShouldBeRelative()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<span class=\"find-me\" id=target1>1</span><div><span class=\"find-me\" id=target2>2</span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("//*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));

            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Assert.That(await div.EvalOnSelectorAsync<string>("xpath=./*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await div.EvalOnSelectorAsync<string>("xpath=.//*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await div.EvalOnSelectorAsync<string>("//*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await div.EvalOnSelectorAsync<string>("xpath=/*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));

            Assert.That(await page.EvalOnSelectorAsync<string>("div >> xpath=./*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> xpath=.//*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> //*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> xpath=/*[@class=\"find-me\"]", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with pipe in xpath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithPipeInXpath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<span class=\"find-me\" id=t1>1</span><div><span class=\"find-me\" id=t2>2</span></div><div id=t3>3</span>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("//*[@id=\"t1\"]|//*[@id=\"t3\"]", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));

            IElementHandle e1 = await page.WaitForSelectorAsync("//*[@id=\"t1\"]|//*[@id=\"t3\"]").ConfigureAwait(false);
            Assert.That(e1, Is.Not.Null);
            Assert.That(await e1.EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("t1"));

            IElementHandle e2 = await page.WaitForSelectorAsync("//*[@id=\"unknown\"]|//*[@id=\"t2\"]").ConfigureAwait(false);
            Assert.That(e2, Is.Not.Null);
            Assert.That(await e2.EvaluateAsync<string>("e => e.id").ConfigureAwait(false), Is.EqualTo("t2"));

            await page.ClickAsync("//code|//span[@id=\"t2\"]").ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should print original xpath in error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintOriginalXpathInError()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator("//*[contains(@Class, 'foo']").IsVisibleAsync());
            Assert.That(error.Message, Does.Contain("//*[contains(@Class, \\'foo\\']"));
            Assert.That(error.Message, Does.Not.Contain(".//*[contains(@Class, 'foo']"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "data-testid on the handle should be relative")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DataTestidOnTheHandleShouldBeRelative()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<span data-testid=\"find-me\" id=target1>1</span><div><span data-testid=\"find-me\" id=target2>2</span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("data-testid=find-me", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));

            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            Assert.That(await div.EvalOnSelectorAsync<string>("data-testid=find-me", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> data-testid=find-me", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should properly determine visibility of display:contents elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlyDetermineVisibilityOfDisplayContentsElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div><p style=\"display:contents\">DISPLAY CONTENTS</p></div>").ConfigureAwait(false);
            await page.WaitForSelectorAsync("\"DISPLAY CONTENTS\"").ConfigureAwait(false);

            await page.SetContentAsync("<div><article style=\"display:contents\"><div>DISPLAY CONTENTS</div></article></div>").ConfigureAwait(false);
            await page.WaitForSelectorAsync("article").ConfigureAwait(false);

            await page.SetContentAsync("<div><article style=\"display:contents\"><div style=\"display:contents\">DISPLAY CONTENTS</div></article></div>").ConfigureAwait(false);
            await page.WaitForSelectorAsync("article").ConfigureAwait(false);

            await page.SetContentAsync("<div><article style=\"display:contents\"><div></div>DISPLAY CONTENTS<span></span></article></div>").ConfigureAwait(false);
            await page.WaitForSelectorAsync("article").ConfigureAwait(false);

            await page.SetContentAsync("<div><article style=\"display:contents\"><div></div></article></div>").ConfigureAwait(false);
            await page.WaitForSelectorAsync("article", WaitForSelectorState.Hidden).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with internal:has=")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithInternalHas()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"#target\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"[data-testid=foo]\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"[attr*=value]\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));

            await page.SetContentAsync("<section><span></span><div></div></section><section><br></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has=\"span, div\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has=\"span, div\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has=\"br\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has=\"span, br\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has=\"span, br, div\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));

            await page.SetContentAsync("<div><span>hello</span></div><div><span>world</span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"text=world\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> internal:has=\"text=world\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div><span>world</span></div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"text=\\\"hello\\\"\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> internal:has=\"text=\\\"hello\\\"\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div><span>hello</span></div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"xpath=./span\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"span\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> internal:has=\"span >> text=wor\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> internal:has=\"span >> text=wor\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div><span>world</span></div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> internal:has=\"span >> text=wor\" >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>world</span>"));

            PlaywrightSharpException error1 = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync("div >> internal:has=abc"));
            Assert.That(error1.Message, Does.Contain("Malformed selector: internal:has=abc"));
            PlaywrightSharpException error2 = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync("internal:has=\"div\""));
            Assert.That(error2.Message, Does.Contain("\"internal:has\" selector cannot be first"));
            PlaywrightSharpException error3 = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync("div >> internal:has=33"));
            Assert.That(error3.Message, Does.Contain("Malformed selector: internal:has=33"));
            PlaywrightSharpException error4 = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.QuerySelectorAsync("div >> internal:has=\"span!\""));
            Assert.That(error4.Message, Does.Contain("Unexpected token \"!\" while parsing css selector \"span!\""));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with internal:has-not=")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithInternalHasNot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section><span></span><div></div></section><section><br></section>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has-not=\"span\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has-not=\"span, div, br\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has-not=\"br\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has-not=\"span, div\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("section >> internal:has-not=\"article\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with internal:and=")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithInternalAnd()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div class=foo>hello</div><div class=bar>world</div><span class=foo>hello2</span><span class=bar>world2</span>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("div >> internal:and=\"span\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("div >> internal:and=\".foo\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "hello" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("div >> internal:and=\".bar\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "world" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("span >> internal:and=\"span\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "hello2", "world2" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>(".foo >> internal:and=\"div\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "hello" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>(".bar >> internal:and=\"span\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "world2" }));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with internal:or=")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithInternalOr()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>hello</div><span>world</span>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("div >> internal:or=\"span\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "hello", "world" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("span >> internal:or=\"div\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "hello", "world" }));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("article >> internal:or=\"something\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await page.Locator("article >> internal:or=\"div\"").TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await page.Locator("article >> internal:or=\"span\"").TextContentAsync().ConfigureAwait(false), Is.EqualTo("world"));
            Assert.That(await page.Locator("div >> internal:or=\"article\"").TextContentAsync().ConfigureAwait(false), Is.EqualTo("hello"));
            Assert.That(await page.Locator("span >> internal:or=\"article\"").TextContentAsync().ConfigureAwait(false), Is.EqualTo("world"));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "should work with internal:chain=")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithInternalChain()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>one <span>two</span> <button>three</button> </div><span>four</span><button>five</button>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("div >> internal:chain=\"button\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "three" }));
            Assert.That(await page.EvalOnSelectorAllAsync<string[]>("div >> internal:chain=\"span >> internal:or=\\\"button\\\"\"", "els => els.map(e => e.textContent)").ConfigureAwait(false), Is.EqualTo(new[] { "two", "three" }));
        }

        [PlaywrightTest("selectors-misc.spec.ts", "chaining should work with large DOM")]
        [PlaywrightTest("selectors-misc.spec.ts", "chaining should work with large DOM @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ChainingShouldWorkWithLargeDom()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"() => {
    let last = document.body;
    for (let i = 0; i < 100; i++) {
      const e = document.createElement('div');
      last.appendChild(e);
      last = e;
    }
    const target = document.createElement('span');
    target.textContent = 'Found me!';
    last.appendChild(target);
}").ConfigureAwait(false);

            string[] selectors =
            {
                "div >> div >> div >> div >> div >> div >> div >> div >> span",
                "div div div div div div div div span",
                "div div >> div div >> div div >> div div >> span",
            };
            int[] counts = new int[3];
            for (int i = 0; i < selectors.Length; i++)
            {
                counts[i] = await page.EvalOnSelectorAllAsync<int>(selectors[i], "els => els.length").ConfigureAwait(false);
            }

            Assert.That(counts, Is.EqualTo(new[] { 1, 1, 1 }));
        }
    }
}
