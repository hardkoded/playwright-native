/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>to-match-aria-snapshot.spec.ts</c> parity.
    /// JS-only: template-literal regex interpolation is written as
    /// <c>/pattern/</c>. YAML parse-error call logs use the C# expect
    /// default of 30000ms (official Playwright Test is 10000ms).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ToMatchAriaSnapshotParityTests : PageTestEx
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

        private static Exception CatchExpect(AsyncTestDelegate code)
        {
            return Assert.CatchAsync(code);
        }

        private static void AssertYamlKeyError(AsyncTestDelegate code, string expectedBody)
        {
            Exception error = CatchExpect(code);
            string message = MessageOf(error);
            Assert.That(message, Does.StartWith("expect(page).toMatchAriaSnapshot(expected) failed\n\n"));
            Assert.That(message, Does.Contain(expectedBody.Replace("\r\n", "\n", StringComparison.Ordinal)));
            Assert.That(message, Does.Contain("Call log:\n  - Expect \"toMatchAriaSnapshot\" with timeout 30000ms\n"));
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

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchPage()
        {
            await Page.SetContentAsync("<h1>title</h1>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""title""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match page complex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchPageComplex()
        {
            await Page.SetContentAsync(@"
    <h1>Microsoft</h1>
    <div>Open source projects and samples from Microsoft</div>
    <ul>
      <li>
        <a href=""about:blank"">Playwright</a>
      </li>
    </ul>").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""Microsoft""
    - text: Open source projects and samples from Microsoft
    - list:
      - listitem:
        - link ""Playwright""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match page with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchPageWithNot()
        {
            await Page.SetContentAsync("<h1>title</h1>").ConfigureAwait(false);
            await Assertions.Expect(Page).Not.ToMatchAriaSnapshotAsync(@"
    - heading ""wrong""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should fail page with timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailPageWithTimeout()
        {
            await Page.SetContentAsync("<h1>title</h1>").ConfigureAwait(false);
            Exception e = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""wrong""
  ", new() { Timeout = 2000 }));
            string message = MessageOf(e);
            Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
            Assert.That(message, Does.Contain("Timeout:  2000ms"));
            Assert.That(message, Does.Not.Contain("Locator"));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatch()
        {
            await Page.SetContentAsync("<h1>title</h1>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""title""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match in list")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchInList()
        {
            await Page.SetContentAsync(@"
    <h1>title</h1>
    <h1>title 2</h1>
  ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""title""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match list with accessible name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchListWithAccessibleName()
        {
            await Page.SetContentAsync(@"
    <ul aria-label=""my list"">
      <li>one</li>
      <li>two</li>
    </ul>
  ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list ""my list"":
      - listitem: ""one""
      - listitem: ""two""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match deep item")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchDeepItem()
        {
            await Page.SetContentAsync(@"
    <div>
      <h1>title</h1>
      <h1>title 2</h1>
    </div>
  ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""title""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match complex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchComplex()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li>
        <a href='about:blank'>link</a>
      </li>
    </ul>
  ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - listitem:
        - link ""link""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match regex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchRegex()
        {
            await Page.SetContentAsync("<h1>Issues 12</h1>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading /Issues \d+/
    ").ConfigureAwait(false);

            await Page.SetContentAsync("<h1>Issues 1/2</h1>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading /Issues 1[/]2/
    ").ConfigureAwait(false);

            await Page.SetContentAsync("<h1>Issues 1[</h1>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading /Issues 1\[/
    ").ConfigureAwait(false);

            await Page.SetContentAsync("<h1>Issues 1]]2</h1>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading /Issues 1[\]]]2/
    ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should allow text nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowTextNodes()
        {
            await Page.SetContentAsync(@"
    <h1>Microsoft</h1>
    <div>Open source projects and samples from Microsoft</div>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""Microsoft""
    - text: ""Open source projects and samples from Microsoft""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "details visibility")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DetailsVisibility()
        {
            await Page.SetContentAsync(@"
    <details>
      <summary>Summary</summary>
      <div>Details</div>
    </details>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - group: ""Summary""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "checked attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CheckedAttribute()
        {
            await Page.SetContentAsync(@"
    <input type='checkbox' checked />
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - checkbox
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - checkbox [checked]
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - checkbox [checked=true]
  ").ConfigureAwait(false);

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - checkbox [checked=false]
    ", new() { Timeout = 1000 }));
                string message = MessageOf(e);
                Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
                Assert.That(message, Does.Contain("Timeout:  1000ms"));
            }

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - checkbox [checked=mixed]
    ", new() { Timeout = 1000 }));
                string message = MessageOf(e);
                Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
                Assert.That(message, Does.Contain("Timeout:  1000ms"));
            }

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - checkbox [checked=5]
    ", new() { Timeout = 1000 }));
                Assert.That(MessageOf(e), Does.Contain(" attribute must be a boolean or \"mixed\""));
            }
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "disabled attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DisabledAttribute()
        {
            await Page.SetContentAsync(@"
    <button disabled>Click me</button>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button [disabled]
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button [disabled=true]
  ").ConfigureAwait(false);

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [disabled=false]
    ", new() { Timeout = 1000 }));
                string message = MessageOf(e);
                Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
                Assert.That(message, Does.Contain("Timeout:  1000ms"));
            }

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [disabled=invalid]
    ", new() { Timeout = 1000 }));
                Assert.That(MessageOf(e), Does.Contain(" attribute must be a boolean"));
            }
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "expanded attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ExpandedAttribute()
        {
            await Page.SetContentAsync(@"
    <button aria-expanded=""true"">Toggle</button>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button [expanded]
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button [expanded=true]
  ").ConfigureAwait(false);

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [expanded=false]
    ", new() { Timeout = 1000 }));
                string message = MessageOf(e);
                Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
                Assert.That(message, Does.Contain("Timeout:  1000ms"));
            }

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [expanded=invalid]
    ", new() { Timeout = 1000 }));
                Assert.That(MessageOf(e), Does.Contain(" attribute must be a boolean"));
            }
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "level attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LevelAttribute()
        {
            await Page.SetContentAsync(@"
    <h2>Section Title</h2>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading [level=2]
  ").ConfigureAwait(false);

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading [level=3]
    ", new() { Timeout = 1000 }));
                string message = MessageOf(e);
                Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
                Assert.That(message, Does.Contain("Timeout:  1000ms"));
            }

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading [level=two]
    ", new() { Timeout = 1000 }));
                Assert.That(MessageOf(e), Does.Contain(" attribute must be a number"));
            }
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "pressed attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PressedAttribute()
        {
            await Page.SetContentAsync(@"
    <button aria-pressed=""true"">Like</button>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button [pressed]
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button [pressed=true]
  ").ConfigureAwait(false);

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [pressed=false]
    ", new() { Timeout = 1000 }));
                string message = MessageOf(e);
                Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
                Assert.That(message, Does.Contain("Timeout:  1000ms"));
            }

            await Page.SetContentAsync(@"
    <button aria-pressed=""mixed"">Like</button>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - button [pressed=mixed]
  ").ConfigureAwait(false);

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [pressed=true]
    ", new() { Timeout = 1000 }));
                string message = MessageOf(e);
                Assert.That(message, Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
                Assert.That(message, Does.Contain("Timeout:  1000ms"));
            }

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [pressed=5]
    ", new() { Timeout = 1000 }));
                Assert.That(MessageOf(e), Does.Contain(" attribute must be a boolean or \"mixed\""));
            }
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "selected attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SelectedAttribute()
        {
            await Page.SetContentAsync(@"
    <table>
      <tr aria-selected=""true"">
        <td>Row</td>
      </tr>
      <tr>
        <td>Row 2</td>
      </tr>
    </table>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - row
    - row
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - row [selected]
    - row
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - row [selected=true]
    - row [selected=false]
  ").ConfigureAwait(false);

            await Assertions.Expect(Page.Locator("table")).ToMatchAriaSnapshotAsync(@"
    - table:
      - rowgroup:
        - row [selected=true]
        - row [selected=false]
  ").ConfigureAwait(false);

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - row [selected=false]
      - row [selected=false]
    ", new() { Timeout = 1000 }));
                Assert.That(MessageOf(e), Does.Contain(Lines(@"expect(page).toMatchAriaSnapshot(expected) failed

Timeout: 1000ms
- Expected  - 2
+ Received  + 6

- - row [selected=false]
- - row [selected=false]
+ - table:
+   - rowgroup:
+     - row ""Row"" [selected]:
+       - cell ""Row""
+     - row ""Row 2"":
+       - cell ""Row 2""

Call log:
")));
            }

            {
                Exception e = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - row [selected=invalid]
    ", new() { Timeout = 1000 }));
                Assert.That(MessageOf(e), Does.Contain(" attribute must be a boolean"));
            }
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "integration test")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IntegrationTest()
        {
            await Page.SetContentAsync(@"
    <h1>Microsoft</h1>
    <div>Open source projects and samples from Microsoft</div>
    <ul>
      <li>
        <details>
          <summary>
            Verified
          </summary>
          <div>
            <div>
              <p>
                We've verified that the organization <strong>microsoft</strong> controls the domain:
              </p>
              <ul>
                <li class=""mb-1"">
                  <strong>opensource.microsoft.com</strong>
                </li>
              </ul>
              <div>
                <a href=""about: blank"">Learn more about verified organizations</a>
              </div>
            </div>
          </div>
        </details>
      </li>
      <li>
        <a href=""about:blank"">
          <summary title=""Label: GitHub Sponsor"">Sponsor</summary>
        </a>
      </li>
    </ul>").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""Microsoft""
    - text: Open source projects and samples from Microsoft
    - list:
      - listitem:
        - group: Verified
      - listitem:
        - link ""Sponsor""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "integration test 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IntegrationTest2()
        {
            await Page.SetContentAsync(@"
    <div>
      <header>
        <h1>todos</h1>
        <input placeholder=""What needs to be done?"">
      </header>
    </div>").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""todos""
    - textbox ""What needs to be done?""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "expected formatter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ExpectedFormatter()
        {
            await Page.SetContentAsync(@"
    <div>
      <header>
        <h1>todos</h1>
        <input placeholder=""What needs to be done?"">
        <button>Time 15:30</button>
      </header>
    </div>").ConfigureAwait(false);
            Exception error = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""todos""
    - textbox ""Wrong text""
  ", new() { Timeout = 1 }));

            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(page).toMatchAriaSnapshot(expected) failed

Timeout: 1ms
- Expected  - 2
+ Received  + 4

- - heading ""todos""
- - textbox ""Wrong text""
+ - banner:
+   - heading ""todos"" [level=1]
+   - textbox ""What needs to be done?""
+   - button ""Time 15:30""

Call log:
")));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should unpack escaped names")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUnpackEscapedNames()
        {
            await Page.SetContentAsync(@"
      <button>Click: me</button>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - 'button ""Click: me""'
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - 'button /Click: me/'
    ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
      <button>Click / me</button>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button ""Click / me""
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button /Click \/ me/
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - 'button /Click \/ me/'
    ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
      <button>Click "" me</button>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button ""Click \"" me""
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button /Click "" me/
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button /Click \"" me/
    ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
      <button>Click \ me</button>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button ""Click \\ me""
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button /Click \\ me/
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - 'button /Click \\ me/'
    ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
      <button>Click ' me</button>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - 'button ""Click '' me""'
    ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
      <h1>heading ""name"" [level=1]</h1>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading ""heading \""name\"" [level=1]"" [level=1]
    ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
      <h1>heading \"" [level=2]</h1>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - |
          heading    ""heading \\\"" [level=2]"" [
             level  =   1   ]
    ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should report error in YAML")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportErrorInYaml()
        {
            await Page.SetContentAsync("<h1>title</h1>").ConfigureAwait(false);

            {
                Exception error = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      heading ""title""
    "));
                Assert.That(MessageOf(error), Is.EqualTo(Lines(@"expect(page).toMatchAriaSnapshot(expected) failed

Expected: ""heading \""title\""""
Error: Aria snapshot must be a YAML sequence, elements starting with "" -""

Call log:
  - Expect ""toMatchAriaSnapshot"" with timeout 30000ms
")));
            }

            {
                Exception error = CatchExpect(
                    () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading: a:
    "));
                Assert.That(MessageOf(error), Is.EqualTo(Lines(@"expect(page).toMatchAriaSnapshot(expected) failed

Expected: ""- heading: a:""
Error: Nested mappings are not allowed in compact mappings at line 1, column 12:

- heading: a:
           ^

Call log:
  - Expect ""toMatchAriaSnapshot"" with timeout 30000ms
")));
            }
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should report error in YAML keys")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportErrorInYamlKeys()
        {
            await Page.SetContentAsync("<h1>title</h1>").ConfigureAwait(false);

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading ""title
    "),
                @"Expected: ""- heading \""title""
Error: Unterminated string:

heading ""title
              ^");

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading /title
    "),
                @"Expected: ""- heading /title""
Error: Unterminated regex:

heading /title
              ^");

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading [level=a]
    "),
                @"Expected: ""- heading [level=a]""
Error: Value of ""level"" attribute must be a number:

heading [level=a]
               ^");

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading [expanded=FALSE]
    "),
                @"Expected: ""- heading [expanded=FALSE]""
Error: Value of ""expanded"" attribute must be a boolean:

heading [expanded=FALSE]
                  ^");

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading [checked=foo]
    "),
                @"Expected: ""- heading [checked=foo]""
Error: Value of ""checked"" attribute must be a boolean or ""mixed"":

heading [checked=foo]
                 ^");

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading [level=]
    "),
                @"Expected: ""- heading [level=]""
Error: Value of ""level"" attribute must be a number:

heading [level=]
               ^");

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading [bogus]
    "),
                @"Expected: ""- heading [bogus]""
Error: Unsupported attribute [bogus]:

heading [bogus]
         ^");

            AssertYamlKeyError(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - heading invalid
    "),
                @"Expected: ""- heading invalid""
Error: Unexpected input:

heading invalid
        ^");
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "call log should contain actual snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CallLogShouldContainActualSnapshot()
        {
            await Page.SetContentAsync("<h1>todos</h1>").ConfigureAwait(false);
            Exception error = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - heading ""wrong""
  ", new() { Timeout = 3000 }));

            Assert.That(MessageOf(error), Does.Contain("unexpected value \"- heading \"todos\" [level=1]\""));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should parse attributes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldParseAttributes()
        {
            await Page.SetContentAsync(@"
      <button aria-pressed=""mixed"">hello world</button>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
      - button [pressed=mixed ]
    ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
      <h2>hello world</h2>
    ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("body")).Not.ToMatchAriaSnapshotAsync(@"
      - heading [level =  -3 ]
    ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should not unshift actual template text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotUnshiftActualTemplateText()
        {
            await Page.SetContentAsync(@"
    <h1>title</h1>
    <h1>title 2</h1>
  ").ConfigureAwait(false);
            Exception error = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
        - heading ""title"" [level=1]
    - heading ""title 2"" [level=1]
  ", new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"
    - heading ""title"" [level=1]
- heading ""title 2"" [level=1]")));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should not match what is not matched")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldNotMatchWhatIsNotMatched()
        {
            await Page.SetContentAsync("<p>Text</p>").ConfigureAwait(false);
            Exception error = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - paragraph:
      - button ""bogus""
  "));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"
- - paragraph:
-   - button ""bogus""
+ - paragraph: Text")));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should match url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMatchUrl()
        {
            await Page.SetContentAsync(@"
    <a href='https://example.com'>Link</a>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - link:
      - /url: /.*example.com/
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should detect unexpected children: equal")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectUnexpectedChildrenEqual()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li>One</li>
      <li>Two</li>
      <li>Three</li>
    </ul>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - listitem: ""One""
      - listitem: ""Three""
  ").ConfigureAwait(false);

            Exception e = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - /children: equal
      - listitem: ""One""
      - listitem: ""Three""
  ", new() { Timeout = 1000 }));

            Assert.That(MessageOf(e), Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
            Assert.That(MessageOf(e), Does.Contain("+   - listitem: Two"));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should detect unexpected children: deep-equal")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectUnexpectedChildrenDeepEqual()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li>
        <ul>
          <li>1.1</li>
          <li>1.2</li>
        </ul>
      </li>
    </ul>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - listitem:
        - list:
          - listitem: 1.1
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - /children: equal
      - listitem:
        - list:
          - listitem: 1.1
  ").ConfigureAwait(false);

            Exception e = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - /children: deep-equal
      - listitem:
        - list:
          - listitem: 1.1
  ", new() { Timeout = 1000 }));

            Assert.That(MessageOf(e), Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
            Assert.That(MessageOf(e), Does.Contain("+       - listitem: \"1.2\""));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "should allow restoring contain mode inside deep-equal")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowRestoringContainModeInsideDeepEqual()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li>
        <ul>
          <li>1.1</li>
          <li>1.2</li>
        </ul>
      </li>
    </ul>
  ").ConfigureAwait(false);

            Exception e = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - /children: deep-equal
      - listitem:
        - list:
          - listitem: 1.1
  ", new() { Timeout = 1000 }));

            Assert.That(MessageOf(e), Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
            Assert.That(MessageOf(e), Does.Contain("+       - listitem: \"1.2\""));

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - list:
      - /children: deep-equal
      - listitem:
        - list:
          - /children: contain
          - listitem: 1.1
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "top-level deep-equal")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TopLevelDeepEqual()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li>
        <ul>
          <li>1.1</li>
          <li>1.2</li>
        </ul>
      </li>
    </ul>
  ").ConfigureAwait(false);

            Exception error = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - /children: deep-equal
    - list
  ", new() { Timeout = 1000 }));

            Assert.That(MessageOf(error), Does.Contain(Lines(@"
- - /children: deep-equal
- - list
+ - list:
+   - listitem:
+     - list:
+       - listitem: ""1.1""
+       - listitem: ""1.2""
").Trim()));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "generated snapshot includes text children when name is longer than text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GeneratedSnapshotIncludesTextChildrenWhenNameIsLongerThanText()
        {
            await Page.SetContentAsync(@"
    <section>
      <div role=""progressbar"" aria-label=""Alpha Beta"" aria-valuenow=""7"" aria-valuemin=""0"" aria-valuemax=""10"">
        <span>Alpha</span>
        <span>7</span>
      </div>
    </section>
  ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("section")).ToMatchAriaSnapshotAsync(@"
    - /children: deep-equal
    - progressbar ""Alpha Beta"": Alpha 7
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "treat bad regex as a string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TreatBadRegexAsAString()
        {
            await Page.SetContentAsync("<a href=\"/foo\">Log in</a>").ConfigureAwait(false);
            Exception error = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - link ""Log in"":
      - /url: /[a/
  ", new() { Timeout = 1 }));
            Assert.That(MessageOf(error), Does.Contain("expect(page).toMatchAriaSnapshot(expected) failed"));
            Assert.That(MessageOf(error), Does.Contain("-   - /url: /[a/"));
            Assert.That(MessageOf(error), Does.Contain("+   - /url: /foo"));
        }

        [PlaywrightTest("to-match-aria-snapshot.spec.ts", "invalid attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InvalidAttribute()
        {
            await Page.SetContentAsync(@"
    <input type=""text"" aria-label=""Email"" aria-invalid=""true"" value=""not-an-email"">
    <input type=""text"" aria-label=""Name"" value=""Alice"">
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - textbox ""Email"" [invalid]: not-an-email
    - textbox ""Name"": Alice
  ").ConfigureAwait(false);

            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - textbox ""Email"" [invalid=true]: not-an-email
    - textbox ""Name"" [invalid=false]: Alice
  ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
    <input type=""text"" aria-label=""Bio"" aria-invalid=""grammar"">
    <input type=""text"" aria-label=""Note"" aria-invalid=""spelling"">
  ").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - textbox ""Bio"" [invalid=grammar]
    - textbox ""Note"" [invalid=spelling]
  ").ConfigureAwait(false);

            Exception error = CatchExpect(
                () => Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - textbox ""Bio"" [invalid]
  ", new() { Timeout = 1 }));
            Assert.That(MessageOf(error), Does.Contain("[invalid=grammar]"));

            await Page.SetContentAsync("<input type=\"text\" aria-label=\"Zip\" aria-invalid=\"garbage\">").ConfigureAwait(false);
            await Assertions.Expect(Page).ToMatchAriaSnapshotAsync(@"
    - textbox ""Zip"" [invalid]
  ").ConfigureAwait(false);
        }
    }
}
