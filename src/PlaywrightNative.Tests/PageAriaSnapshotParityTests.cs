/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-aria-snapshot.spec.ts</c> parity.
    /// Do not edit leftover <c>PageAriaSnapshotTests.cs</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageAriaSnapshotParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19835;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = Prefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }
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

        private static string Unshift(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            string[] raw = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
            for (int i = 0; i < raw.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(raw[i]))
                {
                    lines.Add(raw[i].TrimEnd());
                }
            }

            int common = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                int pad = 0;
                while (pad < lines[i].Length && lines[i][pad] == ' ')
                {
                    pad++;
                }

                if (common < 0 || pad < common)
                {
                    common = pad;
                }
            }

            if (common > 0)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i] = lines[i].Length >= common ? lines[i].Substring(common) : lines[i];
                }
            }

            return string.Join("\n", lines);
        }

        private static string NormalizeYaml(string text)
        {
            return Unshift(text ?? string.Empty);
        }

        private async Task CheckAndMatchSnapshotAsync(ILocator locator, string snapshot)
        {
            string actual = await locator.AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(NormalizeYaml(actual), Is.EqualTo(Unshift(snapshot)));
            await Assertions.Expect(locator).ToMatchAriaSnapshotAsync(snapshot).ConfigureAwait(false);
        }

        private static void AssertContainsYaml(string actual, string expected)
        {
            Assert.That(NormalizeYaml(actual), Does.Contain(Unshift(expected)));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshot()
        {
            await Page.SetContentAsync("<h1>title</h1>").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - heading ""title"" [level=1]
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot list")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotList()
        {
            await Page.SetContentAsync(@"
    <h1>title</h1>
    <h1>title 2</h1>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - heading ""title"" [level=1]
    - heading ""title 2"" [level=1]
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot list with accessible name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotListWithAccessibleName()
        {
            await Page.SetContentAsync(@"
    <ul aria-label=""my list"">
      <li>one</li>
      <li>two</li>
    </ul>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - list ""my list"":
      - listitem: one
      - listitem: two
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot complex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotComplex()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li>
        <a href='about:blank'>link</a>
      </li>
    </ul>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - list:
      - listitem:
        - link ""link"":
          - /url: about:blank
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should allow text nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowTextNodes()
        {
            await Page.SetContentAsync(@"
    <h1>Microsoft</h1>
    <div>Open source projects and samples from Microsoft</div>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - heading ""Microsoft"" [level=1]
    - text: Open source projects and samples from Microsoft
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot details visibility")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotDetailsVisibility()
        {
            await Page.SetContentAsync(@"
    <details>
      <summary>Summary</summary>
      <div>Details</div>
    </details>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - group: Summary
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot integration")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotIntegration()
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
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - heading ""Microsoft"" [level=1]
    - text: Open source projects and samples from Microsoft
    - list:
      - listitem:
        - group: Verified
      - listitem:
        - link ""Sponsor"":
          - /url: about:blank
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should support multiline text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultilineText()
        {
            await Page.SetContentAsync(@"
    <p>
      Line 1
      Line 2
      Line 3
    </p>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - paragraph: Line 1 Line 2 Line 3
  ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("body")).ToMatchAriaSnapshotAsync(@"
    - paragraph: |
          Line 1
          Line 2
          Line 3
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should concatenate span text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConcatenateSpanText()
        {
            await Page.SetContentAsync(@"
    <span>One</span> <span>Two</span> <span>Three</span>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - text: One Two Three
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should concatenate span text 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConcatenateSpanText2()
        {
            await Page.SetContentAsync(@"
    <span>One </span><span>Two </span><span>Three</span>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - text: One Two Three
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should concatenate div text with spaces")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConcatenateDivTextWithSpaces()
        {
            await Page.SetContentAsync(@"
    <div>One</div><div>Two</div><div>Three</div>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - text: One Two Three
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should include pseudo in text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludePseudoInText()
        {
            await Page.SetContentAsync(@"
    <style>
      span:before {
        content: 'world';
      }
      div:after {
        content: 'bye';
      }
    </style>
    <a href=""about:blank"">
      <span>hello</span>
      <div>hello</div>
    </a>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""worldhello hellobye"":
      - /url: about:blank
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should not include hidden pseudo in text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotIncludeHiddenPseudoInText()
        {
            await Page.SetContentAsync(@"
    <style>
      span:before {
        content: 'world';
        display: none;
      }
      div:after {
        content: 'bye';
        visibility: hidden;
      }
    </style>
    <a href=""about:blank"">
      <span>hello</span>
      <div>hello</div>
    </a>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""hello hello"":
      - /url: about:blank
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should include new line for block pseudo")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeNewLineForBlockPseudo()
        {
            await Page.SetContentAsync(@"
    <style>
      span:before {
        content: 'world';
        display: block;
      }
      div:after {
        content: 'bye';
        display: block;
      }
    </style>
    <a href=""about:blank"">
      <span>hello</span>
      <div>hello</div>
    </a>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""world hello hello bye"":
      - /url: about:blank
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should work with slots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithSlots()
        {
            await Page.SetContentAsync(@"
    <button><div>foo</div></button>
    <script>
      (() => {
        const container = document.querySelector('div');
        const shadow = container.attachShadow({ mode: 'open' });
        const slot = document.createElement('slot');
        shadow.appendChild(slot);
      })();
    </script>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - button ""foo""
  ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
    <div>foo</div>
    <script>
      (() => {
        const container = document.querySelector('div');
        const shadow = container.attachShadow({ mode: 'open' });
        const button = document.createElement('button');
        shadow.appendChild(button);
        const slot = document.createElement('slot');
        button.appendChild(slot);
        const span = document.createElement('span');
        span.textContent = 'pre';
        slot.appendChild(span);
      })();
    </script>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - button ""foo""
  ").ConfigureAwait(false);

            await Page.SetContentAsync(@"
    <div></div>
    <script>
      (() => {
        const container = document.querySelector('div');
        const shadow = container.attachShadow({ mode: 'open' });
        const button = document.createElement('button');
        shadow.appendChild(button);
        const slot = document.createElement('slot');
        button.appendChild(slot);
        const span = document.createElement('span');
        span.textContent = 'pre';
        slot.appendChild(span);
      })();
    </script>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - button ""pre""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot inner text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotInnerText()
        {
            await Page.SetContentAsync(@"
    <div role=""listitem"">
      <div>
        <div>
          <span title=""a.test.ts"">a.test.ts</span>
        </div>
        <div>
          <button title=""Run""></button>
          <button title=""Show source""></button>
          <button title=""Watch""></button>
        </div>
      </div>
    </div>
    <div role=""listitem"">
      <div>
        <div>
          <span title=""snapshot"">snapshot</span>
        </div>
        <div class=""ui-mode-list-item-time"">30ms</div>
        <div>
          <button title=""Run""></button>
          <button title=""Show source""></button>
          <button title=""Watch""></button>
        </div>
      </div>
    </div>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - listitem:
      - text: a.test.ts
      - button ""Run""
      - button ""Show source""
      - button ""Watch""
    - listitem:
      - text: snapshot 30ms
      - button ""Run""
      - button ""Show source""
      - button ""Watch""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should include pseudo codepoints")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludePseudoCodepoints()
        {
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync(@"
    <link href=""codicon.css"" rel=""stylesheet"" />
    <p class='codicon codicon-check'>hello</p>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), "- paragraph: " + "\ueab2hello").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "check aria-hidden text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CheckAriaHiddenText()
        {
            await Page.SetContentAsync(@"
    <p>
      <span>hello</span>
      <span aria-hidden=""true"">world</span>
    </p>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - paragraph: hello
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should ignore presentation and none roles")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnorePresentationAndNoneRoles()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li role='presentation'>hello</li>
      <li role='none'>world</li>
    </ul>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - list: hello world
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should treat input value as text in templates, but not for checkbox/radio/file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTreatInputValueAsTextInTemplatesButNotForCheckboxRadioFile()
        {
            await Page.SetContentAsync(@"
    <input value='hello world'>
    <input type=file>
    <input type=checkbox checked>
    <input type=radio checked>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - textbox: hello world
    - button ""Choose File""
    - checkbox [checked]
    - radio [checked]
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should not use on as checkbox value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotUseOnAsCheckboxValue()
        {
            await Page.SetContentAsync(@"
    <input type='checkbox'>
    <input type='radio'>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - checkbox
    - radio
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should respect aria-owns")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectAriaOwns()
        {
            await Page.SetContentAsync(@"
    <a href='about:blank' aria-owns='input p'>
      <div role='region'>Link 1</div>
    </a>
    <a href='about:blank' aria-owns='input p'>
      <div role='region'>Link 2</div>
    </a>
    <input id='input' value='Value'>
    <p id='p'>Paragraph</p>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""Link 1 Value Paragraph"":
      - /url: about:blank
      - region: Link 1
      - textbox: Value
      - paragraph: Paragraph
    - link ""Link 2 Value Paragraph"":
      - /url: about:blank
      - region: Link 2
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should be ok with circular ownership")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeOkWithCircularOwnership()
        {
            await Page.SetContentAsync(@"
    <a href='about:blank' id='parent'>
      <div role='region' aria-owns='parent'>Hello</div>
    </a>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""Hello"":
      - /url: about:blank
      - region: Hello
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should escape yaml text in text nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEscapeYamlTextInTextNodes()
        {
            await Page.SetContentAsync(@"
    <details>
      <summary>one: <a href=""#"">link1</a> ""two <a href=""#"">link2</a> 'three <a href=""#"">link3</a> `four</summary>
    </details>
    <ul>
      <a href=""#"">one</a>,<a href=""#"">two</a>
      (<a href=""#"">three</a>)
      {<a href=""#"">four</a>}
      [<a href=""#"">five</a>]
    </ul>
    <div>[Select all]</div>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - group:
      - text: ""one:""
      - link ""link1"":
        - /url: ""#""
      - text: ""\""two""
      - link ""link2"":
        - /url: ""#""
      - text: ""'three""
      - link ""link3"":
        - /url: ""#""
      - text: ""`four""
    - list:
      - link ""one"":
        - /url: ""#""
      - text: "",""
      - link ""two"":
        - /url: ""#""
      - text: (
      - link ""three"":
        - /url: ""#""
      - text: "") {""
      - link ""four"":
        - /url: ""#""
      - text: ""} [""
      - link ""five"":
        - /url: ""#""
      - text: ""]""
    - text: ""[Select all]""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should normalize whitespace")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNormalizeWhitespace()
        {
            await Page.SetContentAsync(@"
    <details>
      <summary> one  \n two <a href=""#""> link &nbsp;\n  1 </a> </summary>
    </details>
    <input value='  hello   &nbsp; world '>
    <button>hello\u00ad\u200bworld</button>
  ".Replace("\\n", "\n", StringComparison.Ordinal).Replace("\\u00ad", "\u00ad", StringComparison.Ordinal).Replace("\\u200b", "\u200b", StringComparison.Ordinal)).ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - group:
      - text: one two
      - link ""link 1"":
        - /url: ""#""
    - textbox: hello world
    - button ""helloworld""
  ").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("body")).ToMatchAriaSnapshotAsync(@"
    - group:
      - text: |
          one
          two
      - link ""  link     1 "":
        - /url: ""#""
    - textbox:        hello  world
    - button ""he" + "\u00ad" + @"lloworld" + "\u200b" + @"""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should handle long strings")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleLongStrings()
        {
            string s = new string('a', 10000);
            await Page.SetContentAsync(@"
    <a href='about:blank'>
      <div role='region'>" + s + @"</div>
    </a>
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link:
      - /url: about:blank
      - region: " + s + @"
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should escape special yaml characters")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEscapeSpecialYamlCharacters()
        {
            await Page.SetContentAsync(@"
    <a href=""#"">@hello</a>@hello
    <a href=""#"">]hello</a>]hello
    <a href=""#"">hello
</a>
    hello
<a href=""#"">
 hello</a>
 hello
    <a href=""#"">#hello</a>#hello
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""@hello"":
      - /url: ""#""
    - text: ""@hello""
    - link ""]hello"":
      - /url: ""#""
    - text: ""]hello""
    - link ""hello"":
      - /url: ""#""
    - text: hello
    - link ""hello"":
      - /url: ""#""
    - text: hello
    - link ""#hello"":
      - /url: ""#""
    - text: ""#hello""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should escape special yaml values")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEscapeSpecialYamlValues()
        {
            await Page.SetContentAsync(@"
    <a href=""#"">true</a>False
    <a href=""#"">NO</a>yes
    <a href=""#"">y</a>N
    <a href=""#"">on</a>Off
    <a href=""#"">null</a>NULL
    <a href=""#"">123</a>123
    <a href=""#"">-1.2</a>-1.2
    <a href=""#"">-</a>-
    <input type=text value=""555"">
  ").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""true"":
      - /url: ""#""
    - text: ""False""
    - link ""NO"":
      - /url: ""#""
    - text: ""yes""
    - link ""y"":
      - /url: ""#""
    - text: ""N""
    - link ""on"":
      - /url: ""#""
    - text: ""Off""
    - link ""null"":
      - /url: ""#""
    - text: ""NULL""
    - link ""123"":
      - /url: ""#""
    - text: ""123""
    - link ""-1.2"":
      - /url: ""#""
    - text: ""-1.2""
    - link ""-"":
      - /url: ""#""
    - text: ""-""
    - textbox: ""555""
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should not report textarea textContent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotReportTextareaTextContent()
        {
            await Page.SetContentAsync("<textarea>Before</textarea>").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - textbox: Before
  ").ConfigureAwait(false);
            await Page.EvaluateAsync("() => { document.querySelector('textarea').value = 'After'; }").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - textbox: After
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should not show visible children of hidden elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotShowVisibleChildrenOfHiddenElements()
        {
            await Page.SetContentAsync(@"
    <div style=""visibility: hidden;"">
      <div style=""visibility: visible;"">
        <button>Button</button>
      </div>
    </div>
  ").ConfigureAwait(false);
            Assert.That(await Page.Locator("body").AriaSnapshotAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should not show unhidden children of aria-hidden elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotShowUnhiddenChildrenOfAriaHiddenElements()
        {
            await Page.SetContentAsync(@"
    <div aria-hidden=""true"">
      <div aria-hidden=""false"">
        <button>Button</button>
      </div>
    </div>
  ").ConfigureAwait(false);
            Assert.That(await Page.Locator("body").AriaSnapshotAsync().ConfigureAwait(false), Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot placeholder when different from the name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotPlaceholderWhenDifferentFromTheName()
        {
            await Page.SetContentAsync(@"
    <input placeholder=""Placeholder"">
  ").ConfigureAwait(false);
            AssertContainsYaml(await Page.Locator("body").AriaSnapshotAsync().ConfigureAwait(false), @"
    - textbox ""Placeholder""
  ");
            await Page.SetContentAsync(@"
    <input placeholder=""Placeholder"" aria-label=""Label"">
  ").ConfigureAwait(false);
            AssertContainsYaml(await Page.Locator("body").AriaSnapshotAsync().ConfigureAwait(false), @"
    - textbox ""Label"":
      - /placeholder: Placeholder
  ");
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "match values both against regex and string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MatchValuesBothAgainstRegexAndString()
        {
            await Page.SetContentAsync("<a href=\"/auth?r=/\">Log in</a>").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.Locator("body"), @"
    - link ""Log in"":
      - /url: /auth?r=/
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "auto-waits the locator and does not include iframes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AutoWaitsTheLocatorAndDoesNotIncludeIframes()
        {
            await Page.SetContentAsync(@"
    <div>Hello</div>
  ").ConfigureAwait(false);
            Task<string> snapshotPromise = Page.Locator("#target").AriaSnapshotAsync();
            await Page.WaitForTimeoutAsync(2000).ConfigureAwait(false);
            await Page.SetContentAsync(@"
    <div id=target>
      Hello
      <iframe srcdoc=""<ul><li>Item 1</li><li>Item 2</li></ul>""></iframe>
    </div>
  ").ConfigureAwait(false);
            AssertContainsYaml(await snapshotPromise.ConfigureAwait(false), @"
    - text: Hello
  ");
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should limit depth")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLimitDepth()
        {
            await Page.SetContentAsync(@"
    <ul id=target>
      <li>item2</li>
      <li>
        <ul>
          <li>item3</li>
        </ul>
      </li>
    </ul>
  ").ConfigureAwait(false);
            string snapshot = await Page.Locator("#target").AriaSnapshotAsync(new() { Depth = 1 }).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - list:
      - listitem: item2
      - listitem
  ");
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot a locator inside an iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotALocatorInsideAnIframe()
        {
            await Page.SetContentAsync(@"
    <h1>Main Page</h1>
    <iframe srcdoc=""<ul><li>Item 1</li><li>Item 2</li></ul>""></iframe>
  ").ConfigureAwait(false);
            ILocator list = new List<IFrame>(Page.Frames)[1].Locator("ul");
            string snapshot = await list.AriaSnapshotAsync().ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - list:
      - listitem: Item 1
      - listitem: Item 2
  ");
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should include frames on frameset pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeFramesOnFramesetPages()
        {
            Server.SetRoute("/frameset.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<frameset cols=\"50%,50%\"><frame src=\"/frames/frame.html\"><frame src=\"/frames/frame.html\"></frameset>");
            });
            await Page.GoToAsync(Prefix + "/frameset.html").ConfigureAwait(false);
            Assert.That(
                await Page.AriaSnapshotAsync(new() { Timeout = 3000 }).ConfigureAwait(false),
                Is.EqualTo(Unshift(@"
    - iframe
    - iframe
  ")));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot a locator inside a frameset frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotALocatorInsideAFramesetFrame()
        {
            await Page.GoToAsync(Prefix + "/frames/frameset.html").ConfigureAwait(false);
            await CheckAndMatchSnapshotAsync(Page.FrameLocator("frame").First.Locator("body"), @"
    - text: Hi, I'm frame
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot with box from page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotWithBoxFromPage()
        {
            await Page.SetContentAsync(@"
    <button style=""position:absolute;left:100px;top:50px;width:80px;height:40px;margin:0;padding:0;border:0;"">click</button>
  ").ConfigureAwait(false);
            string snapshot = await Page.AriaSnapshotAsync(new() { Boxes = true }).ConfigureAwait(false);
            Assert.That(snapshot, Is.EqualTo("- button \"click\" [box=100,50,80,40]"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should snapshot with box from locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotWithBoxFromLocator()
        {
            await Page.SetContentAsync(@"
    <div style=""position:absolute;left:10px;top:20px;width:200px;height:100px;"">
      <button style=""position:absolute;left:5px;top:5px;width:60px;height:30px;margin:0;padding:0;border:0;"">ok</button>
    </div>
  ").ConfigureAwait(false);
            string snapshot = await Page.Locator("div").AriaSnapshotAsync(new() { Boxes = true }).ConfigureAwait(false);
            Assert.That(snapshot, Is.EqualTo("- button \"ok\" [box=15,25,60,30]"));
        }

        [PlaywrightTest("page-aria-snapshot.spec.ts", "should not include box when option is omitted")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotIncludeBoxWhenOptionIsOmitted()
        {
            await Page.SetContentAsync("<button>click</button>").ConfigureAwait(false);
            string snapshot = await Page.AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(snapshot, Does.Not.Match(new Regex(@"\[box=")));
        }
    }
}

