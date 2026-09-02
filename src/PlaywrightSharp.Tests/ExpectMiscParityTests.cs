/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>expect-misc.spec.ts</c> parity. Official
    /// <c>toHaveURL({})</c> is <see cref="IPageAssertions.ToHaveURLAsync(object, float?)"/>.
    /// Skipped (JS-only): support URLPattern, should have good stack.
    /// Android <c>test.fixme</c> on viewport ratio is not applied.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ExpectMiscParityTests : PageTestEx
    {
        private static string MessageOf(Exception error)
        {
            string message = error == null ? string.Empty : error.Message ?? string.Empty;
            return message.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private static string Lines(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [SetUp]
        public async Task SetUpAsync()
        {
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            try
            {
                if (_context != null)
                {
                    await _context.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                if (_browser != null)
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private IPage Page => _page;

        private static DateTime JsDate(long epochMs)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
        }

        [PlaywrightTest("expect-misc.spec.ts", "toHaveCount pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountPass()
        {
            await Page.SetContentAsync("<select><option>One</option></select>").ConfigureAwait(false);
            ILocator locator = Page.Locator("option");
            bool done = false;
            Task promise = Assertions.Expect(locator).ToHaveCountAsync(2).ContinueWith(
                _ => { done = true; },
                TaskScheduler.Default);
            await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await Page.SetContentAsync("<select><option>One</option><option>Two</option></select>").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass zero")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountPassZero()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            await Assertions.Expect(locator).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "eventually pass zero")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountEventuallyPassZero()
        {
            await Page.SetContentAsync("<div><span>hello</span></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            _ = Task.Run(async () =>
            {
                await Task.Delay(200).ConfigureAwait(false);
                try
                {
                    await Page.EvaluateAsync<object>("() => { document.querySelector('div').textContent = ''; }")
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "eventually pass non-zero")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountEventuallyPassNonZero()
        {
            await Page.SetContentAsync("<ul></ul>").ConfigureAwait(false);
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                try
                {
                    await Page.SetContentAsync("<ul><li>one</li><li>two</li></ul>").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            ILocator locator = Page.Locator("li");
            await Assertions.Expect(locator).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "eventually pass not non-zero")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountEventuallyPassNotNonZero()
        {
            await Page.SetContentAsync("<ul><li>one</li><li>two</li></ul>").ConfigureAwait(false);
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                try
                {
                    await Page.SetContentAsync("<ul></ul>").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            ILocator locator = Page.Locator("li");
            await Assertions.Expect(locator).Not.ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail zero")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountFailZero()
        {
            await Page.SetContentAsync("<div><span></span></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveCountAsync(0, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveCount(expected) failed

Locator:  locator('span')
Expected: 0
Received: 1
Timeout:  1000ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveCount\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail zero 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountFailZero2()
        {
            await Page.SetContentAsync("<div><span></span></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToHaveCountAsync(1, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).not.toHaveCount(expected) failed

Locator:  locator('span')
Expected: not 1
Received: 1
Timeout:  1000ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"not toHaveCount\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPass()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = { a: 1, b: 'string', c: new Date(1627503992000) }; }").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            await Assertions.Expect(locator).ToHaveJSPropertyAsync("foo", new { a = 1, b = "string", c = JsDate(1627503992000) }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyFail()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = { a: 1, b: 'string', c: new Date(1627503992000) }; }").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveJSPropertyAsync("foo", new { a = 1, b = "string", c = JsDate(1627503992001) }, timeout: 1000f));
            Assert.That(error.Message, Does.Contain("-   \"c\""));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPassString()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = 'string'; }").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", "string").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyFailString()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = 'string'; }").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", "error", new() { Timeout = 200 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveJSProperty(expected) failed

Locator:  locator('div')
Expected: ""error""
Received: ""string""
Timeout:  200ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveJSProperty\" with timeout 200ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass number")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPassNumber()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = 2021; }").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", 2021).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail number")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyFailNumber()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = 2021; }").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", 1, new() { Timeout = 200 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveJSProperty(expected) failed

Locator:  locator('div')
Expected: 1
Received: 2021
Timeout:  200ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveJSProperty\" with timeout 200ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass boolean")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPassBoolean()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = true; }").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", true).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail boolean")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyFailBoolean()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = false; }").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", true, new() { Timeout = 200 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveJSProperty(expected) failed

Locator:  locator('div')
Expected: true
Received: false
Timeout:  200ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveJSProperty\" with timeout 200ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass boolean 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPassBoolean2()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = false; }").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", false).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail boolean 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyFailBoolean2()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = false; }").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", true, new() { Timeout = 200 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveJSProperty(expected) failed

Locator:  locator('div')
Expected: true
Received: false
Timeout:  200ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveJSProperty\" with timeout 200ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass undefined")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPassUndefined()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", null).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass null")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPassNull()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = null; }").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveJSPropertyAsync("foo", null).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass nested")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyPassNested()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = { nested: { a: 1, b: 'string', c: new Date(1627503992000) } }; }").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            await Assertions.Expect(locator).ToHaveJSPropertyAsync("foo.nested", new { a = 1, b = "string", c = JsDate(1627503992000) }).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveJSPropertyAsync("foo.nested.a", 1).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveJSPropertyAsync("foo.nested.b", "string").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveJSPropertyAsync("foo.nested.c", JsDate(1627503992000)).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail nested")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveJSPropertyFailNested()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            await Page.EvalOnSelectorAsync<object>("div", "e => { e.foo = { nested: { a: 1, b: 'string', c: new Date(1627503992000) } }; }").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            Exception error1 = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveJSPropertyAsync("foo.bar", new { a = 1, b = "string", c = JsDate(1627503992001) }, timeout: 1000f));
            Assert.That(MessageOf(error1), Does.Contain("Received: undefined"));
            Exception error2 = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveJSPropertyAsync("foo.nested.a", 2, timeout: 1000f));
            Assert.That(MessageOf(error2), Does.Contain("Received: 1"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveClassPass()
        {
            await Page.SetContentAsync("<div class=\"foo bar baz\"></div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveClassAsync("foo bar baz").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass with SVGs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveClassPassWithSvgs()
        {
            await Page.SetContentAsync("<svg class=\"c1 c2\" role=\"img\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\"></svg>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("svg")).ToHaveClassAsync(new Regex("c1")).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveClassFail()
        {
            await Page.SetContentAsync("<div class=\"bar baz\"></div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveClassAsync("foo bar baz", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveClass(expected) failed

Locator:  locator('div')
Expected: ""foo bar baz""
Received: ""bar baz""
Timeout:  1000ms

Call log:
")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveClass\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass with array")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveClassPassWithArray()
        {
            await Page.SetContentAsync("<div class=\"foo\"></div><div class=\"bar\"></div><div class=\"baz\"></div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveClassAsync(new object[] { "foo", "bar", new Regex("[a-z]az") }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail with array")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveClassFailWithArray()
        {
            await Page.SetContentAsync("<div class=\"foo\"></div><div class=\"bar\"></div><div class=\"bar\"></div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveClassAsync(new object[] { "foo", "bar", new Regex("[a-z]az") }, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("expect(locator).toHaveClass(expected) failed"));
            Assert.That(MessageOf(error), Does.Contain("Timeout: 1000ms"));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveClass\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainClassPass()
        {
            await Page.SetContentAsync("<div class=\"foo bar baz\"></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            await Assertions.Expect(locator).ToContainClassAsync(string.Empty).ConfigureAwait(false);
            await Assertions.Expect(locator).ToContainClassAsync("bar").ConfigureAwait(false);
            await Assertions.Expect(locator).ToContainClassAsync("baz bar").ConfigureAwait(false);
            await Assertions.Expect(locator).ToContainClassAsync("  bar   foo ").ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToContainClassAsync("  baz   not-matching ").ConfigureAwait(false);
            Exception error = Assert.Throws<ArgumentException>(() => Assertions.Expect(locator).ToContainClassAsync(new Regex("foo|bar")));
            Assert.That(error.Message, Does.Match("\"expected\" argument in toContainClass cannot be a RegExp value"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass with SVGs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainClassPassWithSvgs()
        {
            await Page.SetContentAsync("<svg class=\"c1 c2\" role=\"img\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\"></svg>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("svg")).ToContainClassAsync("c1").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainClassFail()
        {
            await Page.SetContentAsync("<div class=\"bar baz\"></div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToContainClassAsync("does-not-exist", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toContainClass(expected) failed

Locator:  locator('div')
Expected: ""does-not-exist""
Received: ""bar baz""
Timeout:  1000ms

Call log:
")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toContainClass\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass with array")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainClassPassWithArray()
        {
            await Page.SetContentAsync("<div class=\"foo\"></div><div class=\"hello bar\"></div><div class=\"baz\"></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            await Assertions.Expect(locator).ToContainClassAsync(new[] { "foo", "hello", "baz" }).ConfigureAwait(false);
            Exception error = Assert.Throws<ArgumentException>(() => Assertions.Expect(locator).ToContainClassAsync(new object[] { "foo", "hello", new Regex("baz") }));
            Assert.That(error.Message, Does.Match("\"expected\" argument in toContainClass cannot contain RegExp values"));
            await Assertions.Expect(locator).Not.ToHaveClassAsync(new[] { "not-there", "hello", "baz" }).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveClassAsync(new[] { "foo", "hello" }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail with array")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainClassFailWithArray()
        {
            await Page.SetContentAsync("<div class=\"foo\"></div><div class=\"bar\"></div><div class=\"bar\"></div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToContainClassAsync(new[] { "foo", "bar", "baz" }, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("expect(locator).toContainClass(expected) failed"));
            Assert.That(MessageOf(error), Does.Contain("Timeout: 1000ms"));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toContainClass\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTitlePass()
        {
            await Page.SetContentAsync("<title>  Hello     world</title>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToHaveTitleAsync("Hello  world").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTitleFail()
        {
            await Page.SetContentAsync("<title>Bye</title>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page).ToHaveTitleAsync("Hello", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(page).toHaveTitle(expected) failed

Expected: ""Hello""
Received: ""Bye""
Timeout:  1000ms

Call log:
")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveTitle\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLPass()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToHaveURLAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLFailString()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page).ToHaveURLAsync("wrong", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(page).toHaveURL(expected) failed

Expected: ""wrong""
Received: ""data:text/html,<div>A</div>""
Timeout:  1000ms

Call log:
")));
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail with invalid argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLFailWithInvalidArgument()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page).ToHaveURLAsync(new object()));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(page).toHaveURL(expected) failed

Error: expected value must be a string or regular expression
Expected has type:  object
Expected has value: {}
")));
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail with positive predicate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLFailWithPositivePredicate()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page).ToHaveURLAsync(_ => false, new() { Timeout = 10000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(page).toHaveURL(expected) failed

Expected: predicate to succeed
Received: ""data:text/html,<div>A</div>""
Timeout:  10000ms
")));
        }

        [PlaywrightTest("expect-misc.spec.ts", "fail with negative predicate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLFailWithNegativePredicate()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page).Not.ToHaveURLAsync(_ => true, new() { Timeout = 10000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(page).not.toHaveURL(expected) failed

Expected: predicate to fail
Received: ""data:text/html,<div>A</div>""
Timeout:  10000ms
")));
        }

        [PlaywrightTest("expect-misc.spec.ts", "resolve predicate on initial call")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLResolvePredicateOnInitialCall()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToHaveURLAsync(href => href == "data:text/html,<div>A</div>", timeout: 1000f).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "resolve predicate after retries")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLResolvePredicateAfterRetries()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            Task expect = Assertions.Expect(Page).ToHaveURLAsync(href => href == "data:text/html,<div>B</div>", timeout: 1000f);
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                try
                {
                    await Page.GoToAsync("data:text/html,<div>B</div>").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await expect.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "support ignoreCase")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveURLSupportIgnoreCase()
        {
            await Page.GoToAsync("data:text/html,<div>A</div>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToHaveURLAsync("DATA:teXT/HTml,<div>a</div>", new() { IgnoreCase = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAttributePass()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).ToHaveAttributeAsync("id", "node").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "should not match missing attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAttributeShouldNotMatchMissingAttribute()
        {
            await Page.SetContentAsync("<div checked id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveAttributeAsync("disabled", string.Empty, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveAttribute(expected) failed

Locator:  locator('#node')
Expected: """"
Received: serializes to the same string
Timeout:  1000ms

Call log:
")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toHaveAttribute\" with timeout 1000ms"));
            Exception error2 = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveAttributeAsync("disabled", new Regex(".*"), timeout: 1000f));
            Assert.That(MessageOf(error2), Does.Contain(Lines(@"expect(locator).toHaveAttribute(expected) failed

Locator: locator('#node')
Expected pattern: /.*/
Received string:  """"
Timeout: 1000ms

Call log:
")));
            Assert.That(MessageOf(error2), Does.Contain("- Expect \"toHaveAttribute\" with timeout 1000ms"));
            await Assertions.Expect(locator).Not.ToHaveAttributeAsync("disabled", string.Empty).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAttributeAsync("disabled", new Regex(".*")).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "should match boolean attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAttributeShouldMatchBooleanAttribute()
        {
            await Page.SetContentAsync("<div checked id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToHaveAttributeAsync("checked", string.Empty).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAttributeAsync("checked", new Regex(".*")).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToHaveAttributeAsync("checked", string.Empty, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).not.toHaveAttribute(expected) failed

Locator:  locator('#node')
Expected: not """"
Received: """"
Timeout:  1000ms

Call log:
")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"not toHaveAttribute\" with timeout 1000ms"));
            Exception error2 = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToHaveAttributeAsync("checked", new Regex(".*"), timeout: 1000f));
            Assert.That(MessageOf(error2), Does.Contain(Lines(@"expect(locator).not.toHaveAttribute(expected) failed

Locator: locator('#node')
Expected pattern: not /.*/
Received string: """"
Timeout: 1000ms")));
            Assert.That(MessageOf(error2), Does.Contain("- Expect \"not toHaveAttribute\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "should match attribute without value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAttributeShouldMatchAttributeWithoutValue()
        {
            await Page.SetContentAsync("<div checked id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToHaveAttributeAsync("id", new System.Text.RegularExpressions.Regex(".*")).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAttributeAsync("checked", new System.Text.RegularExpressions.Regex(".*")).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAttributeAsync("open", new System.Text.RegularExpressions.Regex(".*")).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "should support boolean attribute with options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAttributeShouldSupportBooleanAttributeWithOptions()
        {
            await Page.SetContentAsync("<div checked id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToHaveAttributeAsync("id", "", new() { Timeout = 5000 }).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAttributeAsync("checked", "", new() { Timeout = 5000 }).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAttributeAsync("open", "", new() { Timeout = 5000 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "support ignoreCase")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAttributeSupportIgnoreCase()
        {
            await Page.SetContentAsync("<div id=NoDe>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#NoDe");
            await Assertions.Expect(locator).ToHaveAttributeAsync("id", "node", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAttributeAsync("id", "node").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCSSPass()
        {
            await Page.SetContentAsync("<div id=node style=\"color: rgb(255, 0, 0)\">Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).ToHaveCSSAsync("color", "rgb(255, 0, 0)").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "should support pseudo element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCSSShouldSupportPseudoElement()
        {
            await Page.SetContentAsync(@"
      <style>
        #node::before {
          color: rgb(255, 0, 0);
          content: ""Text content"";
        }
      </style>
      <div id=node></div>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).ToHaveCSSAsync("color", "rgb(255, 0, 0)", new() { Pseudo = PseudoElement.Before }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "custom css properties")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCSSCustomCssProperties()
        {
            await Page.SetContentAsync("<div id=node style=\"--custom-color-property:#FF00FF;\">Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).ToHaveCSSAsync("--custom-color-property", "#FF00FF").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveIdPass()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).ToHaveIdAsync("node").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeInViewportShouldWork()
        {
            await Page.SetContentAsync(@"
      <div id=big style=""height: 10000px;""></div>
      <div id=small>foo</div>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#big")).ToBeInViewportAsync().ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#small")).Not.ToBeInViewportAsync().ConfigureAwait(false);
            await Page.Locator("#small").ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#small")).ToBeInViewportAsync().ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#small")).ToBeInViewportAsync(new() { Ratio = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "should respect ratio option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeInViewportShouldRespectRatioOption()
        {
            await Page.SetContentAsync(@"
      <style>body, div, html { padding: 0; margin: 0; }</style>
      <div id=big style=""height: 400vh;""></div>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToBeInViewportAsync().ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToBeInViewportAsync(new() { Ratio = 0.1f }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToBeInViewportAsync(new() { Ratio = 0.2f }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToBeInViewportAsync(new() { Ratio = 0.24f }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToBeInViewportAsync(new() { Ratio = 0.25f }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).Not.ToBeInViewportAsync(new() { Ratio = 0.26f }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).Not.ToBeInViewportAsync(new() { Ratio = 0.3f }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).Not.ToBeInViewportAsync(new() { Ratio = 0.7f }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).Not.ToBeInViewportAsync(new() { Ratio = 0.8f }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "should report intersection even if fully covered by other element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeInViewportShouldReportIntersectionEvenIfFullyCoveredByOtherElement()
        {
            await Page.SetContentAsync(@"
      <h1>hello</h1>
      <div style=""position: relative; height: 10000px; top: -5000px;></div>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("h1")).ToBeInViewportAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-misc.spec.ts", "toHaveCount should not produce logs twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountShouldNotProduceLogsTwice()
        {
            await Page.SetContentAsync("<select><option>One</option></select>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("option")).ToHaveCountAsync(2, new() { Timeout = 2000 }));
            string waitingForMessage = "waiting for locator('option')";
            Assert.That(error.Message, Does.Contain(waitingForMessage));
            Assert.That(error.Message, Does.Contain("locator resolved to 1 element"));
            Assert.That(error.Message, Does.Contain("unexpected value \"1\""));
            Assert.That(error.Message.Replace(waitingForMessage, "<redacted>", StringComparison.Ordinal), Does.Not.Contain(waitingForMessage));
        }

        [PlaywrightTest("expect-misc.spec.ts", "toHaveText should not produce logs twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextShouldNotProduceLogsTwice()
        {
            await Page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("world", new() { Timeout = 2000 }));
            string waitingForMessage = "waiting for locator('div')";
            Assert.That(error.Message, Does.Contain(waitingForMessage));
            Assert.That(error.Message, Does.Contain("locator resolved to <div>hello</div>"));
            Assert.That(error.Message, Does.Contain("unexpected value \"hello\""));
            Assert.That(error.Message.Replace(waitingForMessage, "<redacted>", StringComparison.Ordinal), Does.Not.Contain(waitingForMessage));
        }

        [PlaywrightTest("expect-misc.spec.ts", "toHaveText that does not match should not produce logs twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextThatDoesNotMatchShouldNotProduceLogsTwice()
        {
            await Page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("span")).ToHaveTextAsync("world", new() { Timeout = 2000 }));
            string waitingForMessage = "waiting for locator('span')";
            Assert.That(error.Message, Does.Contain(waitingForMessage));
            Assert.That(error.Message, Does.Not.Contain("locator resolved to"));
            Assert.That(error.Message.Replace(waitingForMessage, "<redacted>", StringComparison.Ordinal), Does.Not.Contain(waitingForMessage));
        }

        [PlaywrightTest("expect-misc.spec.ts", "strict mode violation error format")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StrictModeViolationErrorFormat()
        {
            await Page.SetContentAsync("<div>a</div><div>b</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("foo"));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator: locator('div')
Expected: ""foo""
Error: strict mode violation: locator('div') resolved to 2 elements:")));
            Assert.That(MessageOf(error), Does.Contain("<div>a</div>"));
            Assert.That(MessageOf(error), Does.Contain("<div>b</div>"));
            Assert.That(MessageOf(error), Does.Contain("Call log:"));
        }

        [PlaywrightTest("expect-misc.spec.ts", "invalid selector error format")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InvalidSelectorErrorFormat()
        {
            await Page.SetContentAsync("<div>a</div><div>b</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("##")).ToBeVisibleAsync());
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toBeVisible() failed

Locator: ##
Expected: visible
Error: Unexpected token ""#"" while parsing css selector ""##"". Did you mean to CSS.escape it?

Call log:
")));
        }

        [PlaywrightTest("expect-misc.spec.ts", "should report expect error details when page closes during expect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportExpectErrorDetailsWhenPageClosesDuringExpect()
        {
            await Page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            Task<Exception> promise = Task.Run(async () =>
            {
                try
                {
                    await Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("world", new() { Timeout = 10000 }).ConfigureAwait(false);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            });
            await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            await Page.CloseAsync().ConfigureAwait(false);
            Exception error = await promise.ConfigureAwait(false);
            Assert.That(MessageOf(error), Does.Contain("expect(locator).toHaveText(expected) failed"));
            Assert.That(MessageOf(error), Does.Contain("Expected: \"world\""));
            Assert.That(MessageOf(error), Does.Contain("Received: \"hello\""));
        }
    }
}
