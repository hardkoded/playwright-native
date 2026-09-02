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
    /// Official <c>expect-to-have-text.spec.ts</c> parity for toHaveText
    /// and toContainText (string, regex, and array).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ExpectToHaveTextParityTests : PageTestEx
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

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithRegexPass()
        {
            await Page.SetContentAsync("<div id=node>Text   content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToHaveTextAsync(new Regex("Text")).ConfigureAwait(false);

            // Should not normalize whitespace.
            await Assertions.Expect(locator).ToHaveTextAsync(new Regex("Text   content")).ConfigureAwait(false);
            // Should respect ignoreCase.
            await Assertions.Expect(locator).ToHaveTextAsync(new Regex("text   content"), new() { IgnoreCase = true }).ConfigureAwait(false);
            // Should override regex flag with ignoreCase.
            await Assertions.Expect(locator).Not.ToHaveTextAsync(new Regex("text   content", RegexOptions.IgnoreCase), new() { IgnoreCase = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithRegexFail()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveTextAsync(new Regex("Text 2"), timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator: locator('#node')
Expected pattern: /Text 2/
Received string:  ""Text content""
Timeout: 1000ms

Call log:
")));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainTextWithRegexPass()
        {
            await Page.SetContentAsync("<div id=node>Text   content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToContainTextAsync(new Regex("ex")).ConfigureAwait(false);

            // Should not normalize whitespace.
            await Assertions.Expect(locator).ToContainTextAsync(new Regex("ext   cont")).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainTextWithRegexFail()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToContainTextAsync(new Regex("ex2"), timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("Expected pattern: /ex2/"));
            Assert.That(MessageOf(error), Does.Contain("Received string:  \"Text content\""));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainTextWithStringPass()
        {
            await Page.SetContentAsync("<div id=node>Text   content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToContainTextAsync("content").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainTextWithStringFail()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToContainTextAsync("foo", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toContainText(expected) failed

Locator: locator('#node')
Expected substring: ""foo""
Received string:    ""Text content""
Timeout: 1000ms

Call log:
")));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextPass()
        {
            await Page.SetContentAsync("<div id=node><span></span>Text \ncontent&nbsp;    </div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            // Should normalize whitespace.
            await Assertions.Expect(locator).ToHaveTextAsync("Text                        content").ConfigureAwait(false);
            // Should normalize zero width whitespace.
            await Assertions.Expect(locator).ToHaveTextAsync("T\u200be\u200bx\u200bt content").ConfigureAwait(false);
            // Should support ignoreCase.
            await Assertions.Expect(locator).ToHaveTextAsync("text CONTENT", new() { IgnoreCase = true }).ConfigureAwait(false);
            // Should support falsy ignoreCase.
            await Assertions.Expect(locator).Not.ToHaveTextAsync("TEXT", new() { IgnoreCase = false }).ConfigureAwait(false);
            // Should normalize soft hyphens.
            await Assertions.Expect(locator).ToHaveTextAsync("T\u00ade\u00adxt content").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass contain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextPassContain()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToContainTextAsync("Text").ConfigureAwait(false);
            // Should normalize whitespace.
            await Assertions.Expect(locator).ToContainTextAsync("   ext        cont\n  ").ConfigureAwait(false);
            // Should support ignoreCase.
            await Assertions.Expect(locator).ToContainTextAsync("EXT", new() { IgnoreCase = true }).ConfigureAwait(false);
            // Should support falsy ignoreCase.
            await Assertions.Expect(locator).Not.ToContainTextAsync("TEXT", new() { IgnoreCase = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextFail()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveTextAsync("Text", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("Expected: \"Text\""));
            Assert.That(MessageOf(error), Does.Contain("Received: \"Text content\""));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass eventually")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextPassEventually()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            Task expect = Assertions.Expect(locator).ToHaveTextAsync(new Regex("Text 2"));
            async Task MutateAsync()
            {
                await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
                await locator.EvaluateAsync<object>("element => { element.textContent = 'Text 2 content'; }")
                    .ConfigureAwait(false);
            }

            await Task.WhenAll(expect, MutateAsync()).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "with userInnerText")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextWithUserInnerText()
        {
            await Page.SetContentAsync("<div id=node>Text <span hidden>garbage</span> content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).ToHaveTextAsync("Text content", new() { UseInnerText = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "in shadow dom")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextInShadowDom()
        {
            await Page.SetContentAsync(@"
      <div></div>
      <script>
        const div = document.querySelector('div');
        const span = document.createElement('span');
        span.textContent = 'some text';
        div.attachShadow({ mode: 'open' }).appendChild(span);
      </script>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("span")).ToHaveTextAsync("some text").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("span")).ToContainTextAsync("text").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("some text").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).ToContainTextAsync("text").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("span")).ToHaveTextAsync("some text", new() { UseInnerText = true }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("span")).ToContainTextAsync("text", new() { UseInnerText = true }).ConfigureAwait(false);
            // Playwright intentionally does not perform innerText piercing on shadow dom.
            await Assertions.Expect(Page.Locator("div")).Not.ToHaveTextAsync("some text", new() { UseInnerText = true }).ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("div")).Not.ToContainTextAsync("text", new() { UseInnerText = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail with impossible timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextFailWithImpossibleTimeout()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("#node")).ToHaveTextAsync("Text", new() { Timeout = 1 }));
            Assert.That(MessageOf(error), Does.Contain("Expected: \"Text\""));
            Assert.That(MessageOf(error), Does.Contain("Received: \"Text content\""));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "do not show \"element(s) not found\" when the real failure is a string mismatch")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithTextDoNotShowElementsNotFoundWhenTheRealFailureIsAStringMismatch()
        {
            await Page.SetContentAsync(@"
      <div>Initial</div>
      <script>
        const field = document.querySelector('div');
        setTimeout(() => {
          field.id = 'field';
          field.textContent = 'Final value';
        }, 1000);
      </script>
    ").ConfigureAwait(false);

            ILocator cell = Page.Locator("#field");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(cell).ToHaveTextAsync("Something", new() { Timeout = 3000 }));
            Assert.That(MessageOf(error), Does.Contain("Expected: \"Something\""));
            Assert.That(MessageOf(error), Does.Contain("Received: \"Final value\""));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NotToHaveTextPass()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await Assertions.Expect(locator).Not.ToHaveTextAsync("Text2").ConfigureAwait(false);
            // Should be case-sensitive by default.
            await Assertions.Expect(locator).Not.ToHaveTextAsync("TEXT").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NotToHaveTextFail()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToHaveTextAsync("Text content", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("Expected: not \"Text content\""));
            Assert.That(MessageOf(error), Does.Contain("Received: \"Text content"));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "should work when selector does not match")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NotToHaveTextShouldWorkWhenSelectorDoesNotMatch()
        {
            await Page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("span")).Not.ToHaveTextAsync("hello", timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("Expected: not \"hello\""));
            Assert.That(MessageOf(error), Does.Contain("Error: element(s) not found"));
            Assert.That(MessageOf(error), Does.Contain("waiting for locator('span')"));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayPass()
        {
            await Page.SetContentAsync("<div>Text    \n1</div><div>Text   2a</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            // Should only normalize whitespace in the first item.
            await Assertions.Expect(locator).ToHaveTextAsync(new object[] { "Text  1", new Regex(@"Text   \d+a") }).ConfigureAwait(false);
            // Should support ignoreCase.
            await Assertions.Expect(locator).ToHaveTextAsync(new[] { "tEXT 1", "TExt 2A" }, new() { IgnoreCase = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass lazy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayPassLazy()
        {
            await Page.SetContentAsync("<div id=div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("p");
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                try
                {
                    await Page.EvaluateAsync<object>(@"() => {
                        document.querySelector('div').innerHTML = '<p>Text 1</p><p>Text 2</p>';
                    }").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).ToHaveTextAsync(new[] { "Text 1", "Text 2" }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass empty")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayPassEmpty()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("p");
            await Assertions.Expect(locator).ToHaveTextAsync(Array.Empty<string>()).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass not empty")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayPassNotEmpty()
        {
            await Page.SetContentAsync("<div><p>Test</p></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("p");
            await Assertions.Expect(locator).Not.ToHaveTextAsync(Array.Empty<string>()).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass on empty")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayPassOnEmpty()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("p");
            await Assertions.Expect(locator).Not.ToHaveTextAsync(new[] { "Test" }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail on not+empty")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayFailOnNotEmpty()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("p");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToHaveTextAsync(Array.Empty<string>(), timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("expect(locator).not.toHaveText(expected)"));
            Assert.That(MessageOf(error), Does.Contain("Timeout:  1000ms"));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"not toHaveText\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass eventually empty")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayPassEventuallyEmpty()
        {
            await Page.SetContentAsync("<div id=div><p>Text</p></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("p");
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                try
                {
                    await Page.EvaluateAsync<object>("() => { document.querySelector('div').innerHTML = ''; }")
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).Not.ToHaveTextAsync(Array.Empty<string>()).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayFail()
        {
            await Page.SetContentAsync("<div>Text 1</div><div>Text 3</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveTextAsync(new object[] { "Text 1", new Regex(@"Text \d"), "Extra" }, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator: locator('div')
Timeout: 1000ms
- Expected  - 1
+ Received  + 0

  Array [
    ""Text 1"",
    ""Text 3"",
-   ""Extra"",
  ]

Call log:
  - Expect ""toHaveText"" with timeout 1000ms
  - waiting for locator('div')
")));
            Assert.That(MessageOf(error), Does.Contain("locator resolved to 2 elements"));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail on repeating array matchers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayFailOnRepeatingArrayMatchers()
        {
            await Page.SetContentAsync("<div>KekFoo</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToContainTextAsync(new[] { "KekFoo", "KekFoo", "KekFoo" }, timeout: 1000f));
            Assert.That(error.Message, Does.Contain("locator resolved to 1 element"));
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "pass")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainTextWithArrayPass()
        {
            await Page.SetContentAsync("<div>Text \n1</div><div>Text2</div><div>Text3</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            await Assertions.Expect(locator).ToContainTextAsync(new object[] { "ext     1", new Regex("ext3") }).ConfigureAwait(false);
            // Should support ignoreCase.
            await Assertions.Expect(locator).ToContainTextAsync(new[] { "EXT 1", "eXt3" }, new() { IgnoreCase = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-text.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainTextWithArrayFail()
        {
            await Page.SetContentAsync("<div>Text 1</div><div>Text 3</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToContainTextAsync(new[] { "Text 2" }, timeout: 1000f));
            Assert.That(MessageOf(error), Does.Contain("-   \"Text 2\""));
        }
    }
}
