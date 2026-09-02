/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>selectors-text.spec.ts</c> parity for the text engine,
    /// <c>:text</c> / <c>:text-is</c> / <c>:text-matches</c> / <c>:has-text</c>,
    /// quoted vs unquoted matching, and getByText. Do not edit leftover
    /// <c>GetByTests.cs</c>.
    /// </summary>
    [TestFixture]
    public class SelectorsTextParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl.TrimEnd('/');

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19799;
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

        [PlaywrightTest("selectors-text.spec.ts", "should work")]
        [PlaywrightTest("selectors-text.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>yo</div><div>ya</div><div>\nye  </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=ya", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"ya\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/^[ay]+$/", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/Ya/i", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=ye", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\nye  </div>"));
            Assert.That(await page.GetByText("ye").EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false), Does.Contain(">\nye  </div>"));

            await page.SetContentAsync("<div> ye </div><div>ye</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"ye\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div> ye </div>"));
            Assert.That(await page.GetByText("ye", exact: true).First.EvaluateAsync<string>("e => e.outerHTML").ConfigureAwait(false), Does.Contain("> ye </div>"));

            await page.SetContentAsync("<div>yo</div><div>\"ya</div><div> hello world! </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"\\\"ya\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\"ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/hello/", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div> hello world! </div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/^\\s*heLLo/i", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div> hello world! </div>"));

            await page.SetContentAsync("<div>yo<div>ya</div>hey<div>hey</div></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=hey", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>hey</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=yo>>text=\"ya\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=yo>> text=\"ya\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=yo >>text='ya'", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=yo >> text='ya'", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("'yo'>>\"ya\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("\"yo\" >> 'ya'", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));

            await page.SetContentAsync("<div>yo<span id=\"s1\"></span></div><div>yo<span id=\"s2\"></span><span id=\"s3\"></span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<string>("text=yo", "es => es.map(e => e.outerHTML).join('\\n')").ConfigureAwait(false), Is.EqualTo("<div>yo<span id=\"s1\"></span></div>\n<div>yo<span id=\"s2\"></span><span id=\"s3\"></span></div>"));

            await page.SetContentAsync("<div>'</div><div>\"</div><div>\\</div><div>x</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text='\\''", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>'</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text='\"'", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\"</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"\\\"\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\"</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"'\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>'</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"\\x\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>x</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text='\\x'", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>x</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text='\\\\'", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\\</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"\\\\\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\\</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\"</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text='", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>'</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("\"x\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>x</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("'x'", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>x</div>"));
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(() => page.QuerySelectorAsync("\""));
            Assert.That(error, Is.InstanceOf<PlaywrightSharpException>());
            error = Assert.CatchAsync<PlaywrightSharpException>(() => page.QuerySelectorAsync("'"));
            Assert.That(error, Is.InstanceOf<PlaywrightSharpException>());

            await page.SetContentAsync("<div> ' </div><div> \" </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div> \" </div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text='", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div> ' </div>"));

            await page.SetContentAsync("<div>Hi''&gt;&gt;foo=bar</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"Hi''>>foo=bar\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>Hi''&gt;&gt;foo=bar</div>"));
            await page.SetContentAsync("<div>Hi'\"&gt;&gt;foo=bar</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"Hi'\\\">>foo=bar\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>Hi'\"&gt;&gt;foo=bar</div>"));

            await page.SetContentAsync("<div>Hi&gt;&gt;<span></span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"Hi>>\">>span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/Hi\\>\\>/ >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span></span>"));

            await page.SetContentAsync("<div>a<br>b</div><div>a</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=a", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>a<br>b</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=b", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>a<br>b</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=ab", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>a<br>b</div>"));
            Assert.That(await page.QuerySelectorAsync("text=abc").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=a", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=b", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=ab", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=abc", "els => els.length").ConfigureAwait(false), Is.EqualTo(0));

            await page.SetContentAsync("<div></div><span></span>").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("div", @"div => {
    div.appendChild(document.createTextNode('hello'));
    div.appendChild(document.createTextNode('world'));
}").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("span", @"span => {
    span.appendChild(document.createTextNode('hello'));
    span.appendChild(document.createTextNode('world'));
}").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=lowo", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>helloworld</div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<string>("text=lowo", "els => els.map(e => e.outerHTML).join('')").ConfigureAwait(false), Is.EqualTo("<div>helloworld</div><span>helloworld</span>"));

            await page.SetContentAsync("<span>Sign&nbsp;in</span><span>Hello\n \nworld</span>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=Sign in", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>Sign&nbsp;in</span>"));
            Assert.That((await page.QuerySelectorAllAsync("text=Sign \tin").ConfigureAwait(false)).Count, Is.EqualTo(1));
            Assert.That((await page.QuerySelectorAllAsync("text=\"Sign in\"").ConfigureAwait(false)).Count, Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=lo wo", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>Hello\n \nworld</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"Hello world\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>Hello\n \nworld</span>"));
            Assert.That(await page.QuerySelectorAsync("text=\"lo wo\"").ConfigureAwait(false), Is.Null);
            Assert.That((await page.QuerySelectorAllAsync("text=lo \nwo").ConfigureAwait(false)).Count, Is.EqualTo(1));
            Assert.That((await page.QuerySelectorAllAsync("text=\"lo \nwo\"").ConfigureAwait(false)).Count, Is.EqualTo(0));

            await page.SetContentAsync("<div>let's<span>hello</span></div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/let's/i >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>hello</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/let\\'s/i >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>hello</span>"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with :text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>yo</div><div>ya</div><div>\nHELLO   \n world  </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"ya\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-is(\"ya\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"y\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>yo</div>"));
            Assert.That(await page.QuerySelectorAsync(":text-is(\"Y\")").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"hello world\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\nHELLO   \n world  </div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-is(\"HELLO world\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\nHELLO   \n world  </div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"lo wo\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>\nHELLO   \n world  </div>"));
            Assert.That(await page.QuerySelectorAsync(":text-is(\"lo wo\")").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-matches(\"^[ay]+$\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-matches(\"y\", \"g\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>yo</div>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-matches(\"Y\", \"i\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>yo</div>"));
            Assert.That(await page.QuerySelectorAsync(":text-matches(\"^y$\")").ConfigureAwait(false), Is.Null);

            PlaywrightSharpException error1 = Assert.CatchAsync<PlaywrightSharpException>(() => page.QuerySelectorAsync(":text(\"foo\", \"bar\")"));
            Assert.That(error1.Message, Does.Contain("\"text\" engine expects a single string"));
            PlaywrightSharpException error2 = Assert.CatchAsync<PlaywrightSharpException>(() => page.QuerySelectorAsync(":text(foo > bar)"));
            Assert.That(error2.Message, Does.Contain("\"text\" engine expects a single string"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should support empty string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportEmptyString()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div></div><div>ya</div><div>\nHELLO   \n world  </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("div:text-is(\"\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div></div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div:text-is(\"\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:text(\"\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div></div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div:text(\"\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> text=\"\"", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div></div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> text=\"\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> text=/^$/", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div></div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div >> text=/^$/", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:text-matches(\"\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div></div>"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("div:text-matches(\"\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work across nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkAcrossNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=target1>Hello<i>,</i> <span id=target2>world</span><b>!</b></div>").ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"Hello, world!\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"Hello\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"world\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>(":text(\"world\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.QuerySelectorAsync(":text(\"hello world\")").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("div:text(\"world\")").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=Hello, world!", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=Hello", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=world", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=world", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.QuerySelectorAsync("text=hello world").ConfigureAwait(false), Is.Null);

            Assert.That(await page.QuerySelectorAsync(":text-is(\"Hello, world!\")").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-is(\"Hello\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-is(\"world\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>(":text-is(\"world\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.QuerySelectorAsync("text=\"Hello, world!\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"Hello\"", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"world\"", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=\"world\"", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));

            Assert.That(await page.EvalOnSelectorAsync<string>(":text-matches(\".*\")", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("I"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-matches(\"world?\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>(":text-matches(\"world\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
            Assert.That(await page.QuerySelectorAsync("div:text(\".*\")").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/.*/", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("I"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=/world?/", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=/world/", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-text.spec.ts", "text-is() should ignore comments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TextIsShouldIgnoreComments()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=me>hel<!-- comment -->lo\n  <!-- comment -->\n  world</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(":text-is(\"hello world\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("me"));
            Assert.That(await page.Locator("div", new() { HasText = "hello world" }).GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("me"));
            Assert.That(await page.GetByText("hello world", exact: true).GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("me"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with text nodes in quoted mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithTextNodesInQuotedMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=target1>Hello<span id=target2>wo  rld  </span>  Hi again  </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"Hello\"", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"Hi again\"", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=\"wo rld\"", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));
            Assert.That(await page.QuerySelectorAsync("text=\"Hellowo rld Hi again\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text=\"Hellowo\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text=\"Hellowo rld\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text=\"wo rld Hi ag\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text=\"again\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text=\"hi again\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=hi again", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should clear caches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClearCaches()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=target1>text</div><div id=target2>text</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("#target1").ConfigureAwait(false);

            await div.EvaluateAsync<object>("div => div.textContent = 'text'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=text", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            await div.EvaluateAsync<object>("div => div.textContent = 'foo'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=text", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));

            await div.EvaluateAsync<object>("div => div.textContent = 'text'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"text\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target1"));
            await div.EvaluateAsync<object>("div => div.textContent = 'foo'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(":text(\"text\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("target2"));

            await div.EvaluateAsync<object>("div => div.textContent = 'text'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=text", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            await div.EvaluateAsync<object>("div => div.textContent = 'foo'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("text=text", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));

            await div.EvaluateAsync<object>("div => div.textContent = 'text'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>(":text(\"text\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
            await div.EvaluateAsync<object>("div => div.textContent = 'foo'").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>(":text(\"text\")", "els => els.length").ConfigureAwait(false), Is.EqualTo(1));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with :has-text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithHasText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <input id=input2>
    <div id=div1>
      <span>  Find me  </span>
      or
      <wrap><span id=span2>maybe me  </span></wrap>
      <div><input id=input1></div>
    </div>
  ").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>(":has-text(\"find me\")", "e => e.tagName").ConfigureAwait(false), Is.EqualTo("HTML"));
            Assert.That(await page.EvalOnSelectorAsync<string>("span:has-text(\"find me\")", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>  Find me  </span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has-text(\"find me\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("div1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has-text(\"find me\") input", "e => e.id").ConfigureAwait(false), Is.EqualTo("input1"));
            Assert.That(await page.EvalOnSelectorAsync<string>(":has-text(\"find me\") input", "e => e.id").ConfigureAwait(false), Is.EqualTo("input2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has-text(\"find me or maybe me\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("div1"));
            Assert.That(await page.QuerySelectorAsync("div:has-text(\"find noone\")").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAllAsync<string>(":is(div,span):has-text(\"maybe\")", "els => els.map(e => e.id).join(';')").ConfigureAwait(false), Is.EqualTo("div1;span2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has-text(\"find me\") :has-text(\"maybe me\")", "e => e.tagName").ConfigureAwait(false), Is.EqualTo("WRAP"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has-text(\"find me\") span:has-text(\"maybe me\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("span2"));

            await page.SetContentAsync("<div id=me>hello\n  wo\"r>>ld</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has-text(\"hello wo\\\"r>>ld\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("me"));
            Assert.That(await page.EvalOnSelectorAsync<string>("div:has-text(\"hello\\a wo\\\"r>>ld\")", "e => e.id").ConfigureAwait(false), Is.EqualTo("me"));
            Assert.That(await page.Locator("div", new() { HasText = "hello\nwo\"r>>ld" }).GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("me"));

            PlaywrightSharpException error1 = Assert.CatchAsync<PlaywrightSharpException>(() => page.QuerySelectorAsync(":has-text(\"foo\", \"bar\")"));
            Assert.That(error1.Message, Does.Contain("\"has-text\" engine expects a single string"));
            PlaywrightSharpException error2 = Assert.CatchAsync<PlaywrightSharpException>(() => page.QuerySelectorAsync(":has-text(foo > bar)"));
            Assert.That(error2.Message, Does.Contain("\"has-text\" engine expects a single string"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with large DOM")]
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
      e.id = 'id' + eid;
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
                ":has-text(\"id18\")",
                ":has-text(\"id12345\")",
                ":has-text(\"id\")",
                ":text(\"id18\")",
                ":text(\"id12345\")",
                ":text(\"id\")",
                ":text-matches(\"id12345\", \"i\")",
                "text=id18",
                "text=id12345",
                "text=id",
                "#id18",
                "#id12345",
                "*",
            };

            foreach (string selector in selectors)
            {
                await page.EvalOnSelectorAllAsync<int>(selector, "els => els.length").ConfigureAwait(false);
            }
        }

        [PlaywrightTest("selectors-text.spec.ts", "should be case sensitive if quotes are specified")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeCaseSensitiveIfQuotesAreSpecified()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>yo</div><div>ya</div><div>\nye  </div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=yA", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>ya</div>"));
            Assert.That(await page.QuerySelectorAsync("text=\"yA\"").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text= \"ya\"").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("selectors-text.spec.ts", "should search for a substring without quotes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSearchForASubstringWithoutQuotes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>textwithsubstring</div>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=with", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<div>textwithsubstring</div>"));
            Assert.That(await page.QuerySelectorAsync("text=\"with\"").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("selectors-text.spec.ts", "should skip head, script and style")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSkipHeadScriptAndStyle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <head>
      <title>title</title>
      <script>var script</script>
      <style>.style {}</style>
    </head>
    <body>
      <script>var script</script>
      <style>.style {}</style>
      <div>title script style</div>
    </body>").ConfigureAwait(false);
            IElementHandle head = await page.QuerySelectorAsync("head").ConfigureAwait(false);
            IElementHandle title = await page.QuerySelectorAsync("title").ConfigureAwait(false);
            IElementHandle script = await page.QuerySelectorAsync("body script").ConfigureAwait(false);
            IElementHandle style = await page.QuerySelectorAsync("body style").ConfigureAwait(false);
            foreach (string text in new[] { "title", "script", "style" })
            {
                Assert.That(await page.EvalOnSelectorAsync<string>("text=" + text, "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("DIV"));
                Assert.That(await page.EvalOnSelectorAllAsync<string>("text=" + text, "els => els.map(e => e.nodeName).join('|')").ConfigureAwait(false), Is.EqualTo("DIV"));
                foreach (IElementHandle root in new[] { head, title, script, style })
                {
                    Assert.That(await root.QuerySelectorAsync("text=" + text).ConfigureAwait(false), Is.Null);
                    Assert.That(await root.EvalOnSelectorAllAsync<int>("text=" + text, "els => els.length").ConfigureAwait(false), Is.EqualTo(0));
                }
            }
        }

        [PlaywrightTest("selectors-text.spec.ts", "should match input[type=button|submit|reset]")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchInputTypeButtonSubmitReset()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type=\"submit\" value=\"hello\"><input type=\"button\" value=\"world\"><input type=\"reset\" value=\"clear\">").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=hello", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input type=\"submit\" value=\"hello\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=world", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input type=\"button\" value=\"world\">"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=clear", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<input type=\"reset\" value=\"clear\">"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work for open shadow roots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForOpenShadowRoots()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/deep-shadow.html").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=root1", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root1"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=root2", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=root3", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3"));
            Assert.That(await page.EvalOnSelectorAsync<string>("#root1 >> text=from root3", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root3"));
            Assert.That(await page.EvalOnSelectorAsync<string>("#target >> text=from root2", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from root2"));
            Assert.That(await page.QuerySelectorAsync("text:light=root1").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text:light=root2").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text:light=root3").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("selectors-text.spec.ts", "should prioritize light dom over shadow dom in the same parent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrioritizeLightDomOverShadowDomInTheSameParent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.EvaluateAsync<object>(@"() => {
    const div = document.createElement('div');
    document.body.appendChild(div);

    div.attachShadow({ mode: 'open' });
    const shadowSpan = document.createElement('span');
    shadowSpan.textContent = 'Hello from shadow';
    div.shadowRoot.appendChild(shadowSpan);

    const lightSpan = document.createElement('span');
    lightSpan.textContent = 'Hello from light';
    div.appendChild(lightSpan);
}").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("div >> text=Hello", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("Hello from light"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should waitForSelector with distributed elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForSelectorWithDistributedElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IElementHandle> promise = page.WaitForSelectorAsync("div >> text=Hello");
            await page.EvaluateAsync<object>(@"() => {
    const div = document.createElement('div');
    document.body.appendChild(div);

    div.attachShadow({ mode: 'open' });
    const shadowSpan = document.createElement('span');
    shadowSpan.textContent = 'Hello from shadow';
    div.shadowRoot.appendChild(shadowSpan);
    div.shadowRoot.appendChild(document.createElement('slot'));

    const lightSpan = document.createElement('span');
    lightSpan.textContent = 'Hello from light';
    div.appendChild(lightSpan);
}").ConfigureAwait(false);
            IElementHandle handle = await promise.ConfigureAwait(false);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("Hello from light"));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should match root after >>")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchRootAfter()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section>test</section>").ConfigureAwait(false);
            IElementHandle element = await page.QuerySelectorAsync("css=section >> text=test").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
            IElementHandle element2 = await page.QuerySelectorAsync("text=test >> text=test").ConfigureAwait(false);
            Assert.That(element2, Is.Not.Null);
        }

        [PlaywrightTest("selectors-text.spec.ts", "should match root after >> with *")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchRootAfterWithStar()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button> hello world </button> <button> hellow <span> world </span> </button>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("*css=button >> text=hello >> text=world", "els => els.length").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with leading and trailing spaces")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLeadingAndTrailingSpaces()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button> Add widget </button>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("text=Add widget")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("text= Add widget ")).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with unpaired quotes when not at the start")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithUnpairedQuotesWhenNotAtTheStart()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div>hello""world<span>yay</span></div>
    <div>hello'world<span>nay</span></div>
    <div>hello`world<span>oh</span></div>
    <div>hello`world<span>oh2</span></div>
  ").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("text=lo\" >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>yay</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("  text=lo\" >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>yay</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text  =lo\" >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>yay</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=  lo\" >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>yay</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>(" text = lo\" >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>yay</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=o\"wor >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>yay</span>"));

            Assert.That(await page.EvalOnSelectorAsync<string>("text=lo'wor >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>nay</span>"));
            Assert.That(await page.EvalOnSelectorAsync<string>("text=o' >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>nay</span>"));

            Assert.That(await page.EvalOnSelectorAsync<string>("text=ello`wor >> span", "e => e.outerHTML").ConfigureAwait(false), Is.EqualTo("<span>oh</span>"));
            await Assertions.Expect(page.Locator("text=ello`wor").Locator("span").First).ToHaveTextAsync("oh").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("text=ello`wor").Locator("span").Nth(1)).ToHaveTextAsync("oh2").ConfigureAwait(false);

            Assert.That(await page.QuerySelectorAsync("text='wor >> span").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text=\" >> span").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("text=` >> span").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("selectors-text.spec.ts", "should work with paired quotes in the middle of selector")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithPairedQuotesInTheMiddleOfSelector()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>pattern \"^-?\\d+$\"</div>").ConfigureAwait(false);
            Assert.That(await page.Locator("div >> text=pattern \"^-?\\d+$").IsVisibleAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("div >> text=pattern \"^-?\\d+$\"").IsVisibleAsync().ConfigureAwait(false), Is.True);
            Assert.That(await page.Locator("div >> text='pattern \"^-?\\\\d+$\"'").IsVisibleAsync().ConfigureAwait(false), Is.True);
            await Assertions.Expect(page.Locator("div >> text=pattern \"^-?\\d+$")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div >> text=pattern \"^-?\\d+$\"")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div >> text='pattern \"^-?\\\\d+$\"'")).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-text.spec.ts", "hasText and internal:text should match full node text in strict mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HasTextAndInternalTextShouldMatchFullNodeTextInStrictMode()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div id=div1>hello<span>world</span></div>
    <div id=div2>hello</div>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("helloworld", exact: true)).ToHaveIdAsync("div1").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("hello", exact: true)).ToHaveIdAsync("div2").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("^helloworld$") })).ToHaveIdAsync("div1").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("^hello$") })).ToHaveIdAsync("div2").ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div id=div1><span id=span1>hello</span>world</div>
    <div id=div2><span id=span2>hello</span></div>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("helloworld", exact: true)).ToHaveIdAsync("div1").ConfigureAwait(false);
            Assert.That(await page.GetByText("hello", exact: true).EvaluateAllAsync<string[]>("els => els.map(e => e.id)").ConfigureAwait(false), Is.EqualTo(new[] { "span1", "span2" }));
            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("^helloworld$") })).ToHaveIdAsync("div1").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div", new() { HasTextRegex = new Regex("^hello$") })).ToHaveIdAsync("div2").ConfigureAwait(false);
        }
    }
}
