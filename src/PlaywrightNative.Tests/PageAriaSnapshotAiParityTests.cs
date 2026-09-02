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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-aria-snapshot-ai.spec.ts</c> parity.
    /// Do not edit leftover <c>PageAriaSnapshotTests.cs</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageAriaSnapshotAiParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

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
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19855;
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

        private async Task<string> SnapshotForAIAsync(IPage page, int? depth = null, float? timeout = null)
        {
            return await page.AriaSnapshotAsync(new() { Timeout = timeout, Mode = AriaSnapshotMode.Ai, Depth = depth }).ConfigureAwait(false);
        }

        private static string Unshift(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            string[] raw = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            List<string> lines = new List<string>();
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

        private static void AssertContainsYaml(string actual, string expected)
        {
            Assert.That(NormalizeYaml(actual), Does.Contain(Unshift(expected)));
        }

        private IFrame FrameAt(int index)
        {
            return new List<IFrame>(Page.Frames)[index];
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should generate refs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGenerateRefs()
        {
            await Page.SetContentAsync(@"
    <button>One</button>
    <button>Two</button>
    <button>Three</button>
  ").ConfigureAwait(false);

            string snapshot1 = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot1, @"
    - generic [active] [ref=e1]:
      - button ""One"" [ref=e2]
      - button ""Two"" [ref=e3]
      - button ""Three"" [ref=e4]
  ");
            await Assertions.Expect(Page.Locator("aria-ref=e2")).ToHaveTextAsync("One").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("aria-ref=e3")).ToHaveTextAsync("Two").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("aria-ref=e4")).ToHaveTextAsync("Three").ConfigureAwait(false);

            await Page.Locator("aria-ref=e3").EvaluateAsync<object>("e => { e.textContent = 'Not Two'; }").ConfigureAwait(false);

            string snapshot2 = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot2, @"
    - generic [active] [ref=e1]:
      - button ""One"" [ref=e2]
      - button ""Not Two"" [ref=e5]
      - button ""Three"" [ref=e4]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should list iframes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldListIframes()
        {
            await Page.SetContentAsync(@"
    <h1>Hello</h1>
    <iframe name=""foo"" src=""data:text/html,<h1>World</h1>"">
  ").ConfigureAwait(false);

            string snapshot1 = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            Assert.That(snapshot1, Does.Contain("- iframe"));

            string frameSnapshot = await Page.FrameLocator("iframe").Locator("body").AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(NormalizeYaml(frameSnapshot), Is.EqualTo("- heading \"World\" [level=1]"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should snapshot a locator inside an iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotALocatorInsideAnIframe()
        {
            await Page.SetContentAsync(@"
    <h1>Main Page</h1>
    <iframe srcdoc=""<ul><li>Item 1</li><li>Item 2</li></ul>""></iframe>
  ").ConfigureAwait(false);

            ILocator list = FrameAt(1).Locator("ul");
            string snapshot = await list.AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai }).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - list [ref=f1e1]:
      - listitem [ref=f1e2]: Item 1
      - listitem [ref=f1e3]: Item 2
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should limit depth across iframe boundary")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLimitDepthAcrossIframeBoundary()
        {
            await Page.SetContentAsync(@"
    <nav>
      <iframe srcdoc=""<ul><li><button>Deep</button></li></ul>""></iframe>
    </nav>
  ").ConfigureAwait(false);

            string snapshot = await SnapshotForAIAsync(Page, depth: 3).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - navigation [ref=e2]:
      - iframe [ref=e3]:
        - list [ref=f1e2]:
          - listitem [ref=f1e3]
  ");
            Assert.That(snapshot, Does.Not.Contain("button"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should stitch all frame snapshots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStitchAllFrameSnapshots()
        {
            await Page.GoToAsync(Prefix + "/frames/nested-frames.html").ConfigureAwait(false);
            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - generic [active] [ref=e1]:
      - iframe [ref=e2]:
        - generic [ref=f1e1]:
          - iframe [ref=f1e2]:
            - generic [ref=f3e1]: Hi, I'm frame
          - iframe [ref=f1e3]:
            - generic [ref=f4e1]: Hi, I'm frame
      - iframe [ref=e3]:
        - generic [ref=f2e1]: Hi, I'm frame
  ");

            string href = await Page.Locator("aria-ref=e1").EvaluateAsync<string>("e => e.ownerDocument.defaultView.location.href").ConfigureAwait(false);
            Assert.That(href, Is.EqualTo(Prefix + "/frames/nested-frames.html"));

            string href2 = await Page.Locator("aria-ref=f1e2").EvaluateAsync<string>("e => e.ownerDocument.defaultView.location.href").ConfigureAwait(false);
            Assert.That(href2, Is.EqualTo(Prefix + "/frames/two-frames.html"));

            string href3 = await Page.Locator("aria-ref=f4e2").EvaluateAsync<string>("e => e.ownerDocument.defaultView.location.href").ConfigureAwait(false);
            Assert.That(href3, Is.EqualTo(Prefix + "/frames/frame.html"));

            ILocator resolved = await Page.Locator("aria-ref=e1").NormalizeAsync().ConfigureAwait(false);
            Assert.That(resolved.ToString(), Is.EqualTo("locator('body')"));

            ILocator resolved2 = await Page.Locator("aria-ref=f4e2").NormalizeAsync().ConfigureAwait(false);
            Assert.That(resolved2.ToString(), Is.EqualTo("locator('iframe[name=\"2frames\"]').contentFrame().locator('iframe[name=\"dos\"]').contentFrame().getByText('Hi, I\\'m frame')"));

            ILocator resolved3 = await Page.Locator("aria-ref=f3e2").Describe("foo bar").NormalizeAsync().ConfigureAwait(false);
            Assert.That(resolved3.ToString(), Is.EqualTo("locator('iframe[name=\"2frames\"]').contentFrame().locator('iframe[name=\"uno\"]').contentFrame().getByText('Hi, I\\'m frame')"));

            Exception error = Assert.CatchAsync(() => Page.Locator("aria-ref=e1000").NormalizeAsync());
            Assert.That(error.Message, Does.Contain("No element matching aria-ref=e1000"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should re-number refs across navigations but not same-document navigations")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReNumberRefsAcrossNavigationsButNotSameDocumentNavigations()
        {
            Server.SetRoute("/one.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<button>One</button>").ConfigureAwait(false);
            });
            Server.SetRoute("/two.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<button>Two</button>").ConfigureAwait(false);
            });

            await Page.GoToAsync(Prefix + "/one.html").ConfigureAwait(false);
            Match oneMatch = Regex.Match(await SnapshotForAIAsync(Page).ConfigureAwait(false), "button \"One\" \\[ref=(e\\d+)\\]");
            Assert.That(oneMatch.Success, Is.True);
            string oneRef = oneMatch.Groups[1].Value;
            await Assertions.Expect(Page.Locator("aria-ref=" + oneRef)).ToHaveTextAsync("One").ConfigureAwait(false);

            await Page.GoToAsync(Prefix + "/two.html").ConfigureAwait(false);
            Match twoMatch = Regex.Match(await SnapshotForAIAsync(Page).ConfigureAwait(false), "button \"Two\" \\[ref=(f\\d+e\\d+)\\]");
            Assert.That(twoMatch.Success, Is.True);
            string twoRef = twoMatch.Groups[1].Value;
            await Assertions.Expect(Page.Locator("aria-ref=" + twoRef)).ToHaveTextAsync("Two").ConfigureAwait(false);

            Exception error = Assert.CatchAsync(() => Page.Locator("aria-ref=" + oneRef).NormalizeAsync());
            Assert.That(error.Message, Does.Contain("No element matching aria-ref=" + oneRef));

            await Page.EvaluateAsync("() => history.pushState({}, '', '/pushed.html')").ConfigureAwait(false);
            Assert.That(await SnapshotForAIAsync(Page).ConfigureAwait(false), Does.Contain("button \"Two\" [ref=" + twoRef + "]"));
            await Assertions.Expect(Page.Locator("aria-ref=" + twoRef)).ToHaveTextAsync("Two").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should persist iframe references")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPersistIframeReferences()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li><iframe srcdoc=""<button>button1</button>""></iframe></li>
      <li><iframe srcdoc=""<button>button2</button>""></iframe></li>
    </ul>
  ").ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - list [ref=e2]:
      - listitem [ref=e3]:
        - iframe [ref=e4]:
          - button ""button1"" [ref=f1e2]
      - listitem [ref=e5]:
        - iframe [ref=e6]:
          - button ""button2"" [ref=f2e2]
  ");

            await Page.EvaluateAsync("() => document.querySelector('iframe').remove()").ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - list [ref=e2]:
      - listitem [ref=e3]
      - listitem [ref=e5]:
        - iframe [ref=e6]:
          - button ""button2"" [ref=f2e2]
  ");
            await Assertions.Expect(Page.Locator("aria-ref=f2e2")).ToHaveTextAsync("button2").ConfigureAwait(false);

            await Page.EvaluateAsync(@"() => {
    const frame = document.createElement('iframe');
    frame.setAttribute('srcdoc', '<button>button1</button>');
    document.querySelector('li').appendChild(frame);
  }").ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - list [ref=e2]:
      - listitem [ref=e3]:
        - iframe [ref=e7]:
          - button ""button1"" [ref=f3e2]
      - listitem [ref=e5]:
        - iframe [ref=e6]:
          - button ""button2"" [ref=f2e2]
  ");
            await Assertions.Expect(Page.Locator("aria-ref=f3e2")).ToHaveTextAsync("button1").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("aria-ref=f2e2")).ToHaveTextAsync("button2").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should not generate refs for elements with pointer-events:none")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotGenerateRefsForElementsWithPointerEventsNone()
        {
            await Page.SetContentAsync(@"
    <button style=""pointer-events: none"">no-ref</button>
    <div style=""pointer-events: none"">
      <button style=""pointer-events: auto"">with-ref</button>
    </div>
    <div style=""pointer-events: none"">
      <div style=""pointer-events: initial"">
        <button>with-ref</button>
      </div>
    </div>
    <div style=""pointer-events: none"">
      <div style=""pointer-events: auto"">
        <button>with-ref</button>
      </div>
    </div>
    <div style=""pointer-events: auto"">
      <div style=""pointer-events: none"">
        <button>no-ref</button>
      </div>
    </div>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - button ""no-ref""
      - button ""with-ref"" [ref=e2]
      - button ""with-ref"" [ref=e4]
      - button ""with-ref"" [ref=e6]
      - generic [ref=e7]:
        - generic:
          - button ""no-ref""
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "emit generic roles for nodes w/o roles")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task EmitGenericRolesForNodesWoRoles()
        {
            await Page.SetContentAsync(@"
    <style>
    input {
      width: 0;
      height: 0;
      opacity: 0;
    }
    </style>
    <div>
      <label>
        <span>
          <input type=""radio"" value=""Apple"" checked="""">
        </span>
        <span>Apple</span>
      </label>
      <label>
        <span>
          <input type=""radio"" value=""Pear"">
        </span>
        <span>Pear</span>
      </label>
      <label>
        <span>
          <input type=""radio"" value=""Orange"">
        </span>
        <span>Orange</span>
      </label>
    </div>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [ref=e2]:
      - generic [ref=e3]:
        - generic [ref=e4]:
          - radio ""Apple"" [checked]
        - text: Apple
      - generic [ref=e5]:
        - generic [ref=e6]:
          - radio ""Pear""
        - text: Pear
      - generic [ref=e7]:
        - generic [ref=e8]:
          - radio ""Orange""
        - text: Orange
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should collapse generic nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollapseGenericNodes()
        {
            await Page.SetContentAsync(@"
    <div>
      <div>
        <div>
          <button>Button</button>
        </div>
      </div>
    </div>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - button ""Button"" [ref=e5]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should include cursor pointer hint")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeCursorPointerHint()
        {
            await Page.SetContentAsync(@"
    <button style=""cursor: pointer"">Button</button>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - button ""Button"" [ref=e2] [cursor=pointer]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should not nest cursor pointer hints")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotNestCursorPointerHints()
        {
            await Page.SetContentAsync(@"
    <a style=""cursor: pointer"" href=""about:blank"">
      Link with a button
      <button style=""cursor: pointer"">Button</button>
    </a>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - link [ref=e2] [cursor=pointer]:
      - /url: about:blank
      - text: Link with a button
      - button ""Button"" [ref=e3]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should omit names that just repeat printed descendant nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitNamesThatJustRepeatPrintedDescendantNodes()
        {
            await Page.SetContentAsync(@"
    <h3><a style=""cursor: pointer"" href=""/issues/1"">Clipboard API</a></h3>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - heading [level=3] [ref=e2]:
      - link ""Clipboard API"" [ref=e3] [cursor=pointer]:
        - /url: /issues/1
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should omit redundant name when a contributing wrapper is collapsed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitRedundantNameWhenAContributingWrapperIsCollapsed()
        {
            await Page.SetContentAsync(@"
    <h3><span style=""display: flex""><a style=""cursor: pointer"" href=""/issues/1"">Clipboard API</a></span></h3>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - heading [level=3] [ref=e2]:
      - link ""Clipboard API"" [ref=e4] [cursor=pointer]:
        - /url: /issues/1
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should omit redundant name when a contributor is a skipped leaf generic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitRedundantNameWhenAContributorIsASkippedLeafGeneric()
        {
            await Page.SetContentAsync(@"
    <h3><a style=""cursor: pointer"" href=""/issues/1""><span><span>Clipboard API</span></span></a></h3>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - heading [level=3] [ref=e2]:
      - link ""Clipboard API"" [ref=e3] [cursor=pointer]:
        - /url: /issues/1
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should keep the name when the contributing wrapper collapses into repeating text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepTheNameWhenTheContributingWrapperCollapsesIntoRepeatingText()
        {
            await Page.SetContentAsync(@"
    <button>
      <span>
        <span>
          <svg focusable=""false"" tabindex=""-1"" aria-hidden=""true"" viewBox=""0 0 448 512"">
            <path d=""M416 208H272V64c0-17.67-14.33-32-32-32h-32c-17.67 0-32 14.33-32 32v144H32c-17.67 0-32 14.33-32 32v32c0 17.67 14.33 32 32 32h144v144c0 17.67 14.33 32 32 32h32c17.67 0 32-14.33 32-32V304h144c17.67 0 32-14.33 32-32v-32c0-17.67-14.33-32-32-32z""/>
          </svg>
        </span>
        <span>Add New Item</span>
      </span>
    </button>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - button ""Add New Item"" [ref=e2]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should keep names not derived from printed nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepNamesNotDerivedFromPrintedNodes()
        {
            await Page.SetContentAsync(@"
    <h3 aria-label=""Clipboard API issue""><a style=""cursor: pointer"" href=""/issues/1"">Clipboard API</a></h3>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - heading ""Clipboard API issue"" [level=3] [ref=e2]:
      - link ""Clipboard API"" [ref=e3] [cursor=pointer]:
        - /url: /issues/1
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should omit images without an accessible name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitImagesWithoutAnAccessibleName()
        {
            await Page.SetContentAsync(@"
    <img src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA="">
    <img alt=""A cat"" src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA="">
    <img style=""cursor: pointer"" src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA="">
  ").ConfigureAwait(false);

            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - generic [active] [ref=e1]:
      - img ""A cat"" [ref=e3]
      - img [ref=e4] [cursor=pointer]
  ");
            Assert.That(snapshot, Does.Not.Contain("[ref=e2]"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should omit a nameless image nested inside a link")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitANamelessImageNestedInsideALink()
        {
            await Page.SetContentAsync(@"
    <a style=""cursor: pointer"" href=""/issue/1"">Open issue <img src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA=""></a>
  ").ConfigureAwait(false);

            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - link ""Open issue"" [ref=e2] [cursor=pointer]:
      - /url: /issue/1
  ");
            Assert.That(snapshot, Does.Not.Contain("img"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should keep icon-only clickable elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldKeepIconOnlyClickableElements()
        {
            string icon = "<svg viewBox=\"0 0 1024 1024\"><use xlink:href=\"#icon\"></use></svg>";
            await Page.SetContentAsync(@"
    <div style=""cursor: pointer"" aria-haspopup=""true""><i>" + icon + @"</i></div>
    <img style=""cursor: pointer"" src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA="">
    <a style=""cursor: pointer"" href=""/target"">" + icon + @"</a>
    <div onclick=""void 0"">" + icon + @"</div>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - generic [ref=e2] [cursor=pointer]
      - img [ref=e5] [cursor=pointer]
      - link [ref=e6] [cursor=pointer]:
        - /url: /target
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should omit leaf generic whose text is already in an ancestor name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitLeafGenericWhoseTextIsAlreadyInAnAncestorName()
        {
            await Page.SetContentAsync(@"
    <a style=""cursor: pointer"" href=""/issues/15860""><div>[Feature] a dedicated clipboard API</div></a>
  ").ConfigureAwait(false);

            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - link ""[Feature] a dedicated clipboard API"" [ref=e2] [cursor=pointer]:
      - /url: /issues/15860
  ");
            Assert.That(snapshot, Does.Not.Contain("[ref=e3]"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should omit name-repeating generic behind a wrapper")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOmitNameRepeatingGenericBehindAWrapper()
        {
            await Page.SetContentAsync(@"
    <a style=""cursor: pointer"" href=""/labels""><span style=""display: inline-block""><span style=""display: inline-block""><span>P3-collecting-feedback</span></span></span></a>
  ").ConfigureAwait(false);

            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - link ""P3-collecting-feedback"" [ref=e2] [cursor=pointer]:
      - /url: /labels
  ");
            Assert.That(snapshot.Split("P3-collecting-feedback", StringSplitOptions.None).Length, Is.EqualTo(2));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should resolve refs of distilled-away nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveRefsOfDistilledAwayNodes()
        {
            await Page.SetContentAsync(@"
    <a style=""cursor: pointer"" href=""/issues/15860""><div>[Feature] a dedicated clipboard API</div></a>
  ").ConfigureAwait(false);

            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            Assert.That(snapshot, Does.Not.Contain("[ref=e3]"));
            await Assertions.Expect(Page.Locator("aria-ref=e3")).ToHaveTextAsync("[Feature] a dedicated clipboard API").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should not distill snapshots outside of ai mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotDistillSnapshotsOutsideOfAiMode()
        {
            await Page.SetContentAsync(@"
    <h3><a href=""/issues/1"">Clipboard API</a></h3>
  ").ConfigureAwait(false);

            await Assertions.Expect(Page.Locator("body")).ToMatchAriaSnapshotAsync(@"
    - heading ""Clipboard API"" [level=3]:
      - link ""Clipboard API"":
        - /url: /issues/1
  ").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should truncate data url in link")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTruncateDataUrlInLink()
        {
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("<p>hello</p>"));
            await Page.SetContentAsync("<a href=\"data:text/html;base64," + base64 + "\">a link</a>").ConfigureAwait(false);
            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            Assert.That(snapshot, Does.Contain("/url: data:text/html;base64,…"));
            Assert.That(snapshot, Does.Not.Contain(base64));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should gracefully fallback when child frame cant be captured")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGracefullyFallbackWhenChildFrameCantBeCaptured()
        {
            await Page.SetContentAsync(@"
    <p>Test</p>
    <iframe src=""" + Prefix + @"/redirectloop1.html#depth=100000""></iframe>
  ", new() { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - paragraph [ref=e2]: Test
      - iframe [ref=e3]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should auto-wait for navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAutoWaitForNavigation()
        {
            await Page.GoToAsync(Prefix + "/frames/frame.html").ConfigureAwait(false);
            Task reload = Page.EvaluateAsync<object>("() => window.location.reload()");
            Task<string> snapshotTask = SnapshotForAIAsync(Page);
            await Task.WhenAll(reload, snapshotTask).ConfigureAwait(false);
            Assert.That(snapshotTask.Result, Does.Match("- generic \\[active\\] \\[ref=(?:f\\d+)?e\\d+\\]: Hi, I'm frame"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should auto-wait for blocking CSS")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAutoWaitForBlockingCSS()
        {
            Server.SetRoute("/css", async context =>
            {
                context.Response.ContentType = "text/css";
                await Task.Delay(1000).ConfigureAwait(false);
                await context.Response.WriteAsync("body { monospace }").ConfigureAwait(false);
            });
            await Page.SetContentAsync(@"
    <script src=""" + Prefix + @"/css""></script>
    <p>Hello World</p>
  ", new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), "Hello World");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should show visible children of hidden elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldShowVisibleChildrenOfHiddenElements()
        {
            await Page.SetContentAsync(@"
    <div style=""visibility: hidden"">
      <div style=""visibility: visible"">
        <button>Visible</button>
      </div>
      <div style=""visibility: hidden"">
        <button style=""visibility: visible"">Visible</button>
      </div>
      <div>
        <div style=""visibility: visible"">
          <button style=""visibility: hidden"">Hidden</button>
        </div>
        <button>Hidden</button>
      </div>
    </div>
  ").ConfigureAwait(false);

            Assert.That(NormalizeYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false)), Is.EqualTo(Unshift(@"
    - generic [active] [ref=e1]:
      - button ""Visible"" [ref=e3]
      - button ""Visible"" [ref=e4]
  ")));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should include active element information")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeActiveElementInformation()
        {
            await Page.SetContentAsync(@"
    <button id=""btn1"">Button 1</button>
    <button id=""btn2"" autofocus>Button 2</button>
    <div>Not focusable</div>
  ").ConfigureAwait(false);

            await Page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.id === 'btn2'", null, "raf").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [ref=e1]:
      - button ""Button 1"" [ref=e2]
      - button ""Button 2"" [active] [ref=e3]
      - generic [ref=e4]: Not focusable
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should update active element on focus")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUpdateActiveElementOnFocus()
        {
            await Page.SetContentAsync(@"
    <input id=""input1"" placeholder=""First input"">
    <input id=""input2"" placeholder=""Second input"">
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - textbox ""First input"" [ref=e2]
      - textbox ""Second input"" [ref=e3]
  ");

            await Page.Locator("#input2").FocusAsync().ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [ref=e1]:
      - textbox ""First input"" [ref=e2]
      - textbox ""Second input"" [active] [ref=e3]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should mark iframe as active when it contains focused element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMarkIframeAsActiveWhenItContainsFocusedElement()
        {
            await Page.SetContentAsync(@"
    <input id=""regular-input"" placeholder=""Regular input"">
    <iframe src=""data:text/html,<input id='iframe-input' placeholder='Input in iframe'>"" tabindex=""0""></iframe>
  ").ConfigureAwait(false);

            await Page.FrameLocator("iframe").Locator("#iframe-input").FocusAsync().ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [ref=e1]:
      - textbox ""Regular input"" [ref=e2]
      - iframe [active] [ref=e3]:
        - textbox ""Input in iframe"" [active] [ref=f1e2]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "return empty snapshot when iframe is not loaded")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ReturnEmptySnapshotWhenIframeIsNotLoaded()
        {
            await Page.SetContentAsync(@"
    <div style=""height: 5000px;"">Test</div>
    <iframe loading=""lazy"" src=""" + Prefix + @"/frame.html""></iframe>
  ").ConfigureAwait(false);

            await Page.WaitForSelectorAsync("iframe").ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page, timeout: 3000).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - generic [ref=e2]: Test
      - iframe [ref=e3]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should support many properties on iframes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportManyPropertiesOnIframes()
        {
            await Page.SetContentAsync(@"
    <input id=""regular-input"" placeholder=""Regular input"">
    <iframe style='cursor: pointer' src=""data:text/html,<input id='iframe-input' placeholder='Input in iframe'/>"" tabindex=""0""></iframe>
  ").ConfigureAwait(false);

            await Page.FrameLocator("iframe").Locator("#iframe-input").FocusAsync().ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [ref=e1]:
      - textbox ""Regular input"" [ref=e2]
      - iframe [active] [ref=e3] [cursor=pointer]:
        - textbox ""Input in iframe"" [active] [ref=f1e2]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should snapshot frameset pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotFramesetPages()
        {
            Server.SetRoute("/frameset.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<frameset rows=\"50%,50%\"><frameset cols=\"50%,50%\"><frame src=\"/frame-one.html\"><frame src=\"/frame-two.html\"></frameset><frame src=\"/frame-three.html\"></frameset>").ConfigureAwait(false);
            });
            foreach (string name in new[] { "one", "two", "three" })
            {
                string captured = name;
                Server.SetRoute("/frame-" + captured + ".html", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("<button>Button " + captured + "</button>").ConfigureAwait(false);
                });
            }

            await Page.GoToAsync(Prefix + "/frameset.html").ConfigureAwait(false);
            AssertContainsYaml(await SnapshotForAIAsync(Page, timeout: 3000).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - generic [ref=e2]:
        - iframe [ref=e3]:
          - button ""Button one"" [ref=f1e2]
        - iframe [ref=e4]:
          - button ""Button two"" [ref=f2e2]
      - iframe [ref=e5]:
        - button ""Button three"" [ref=f3e2]
  ");
            await Assertions.Expect(Page.Locator("aria-ref=f2e2")).ToHaveTextAsync("Button two").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should snapshot a locator inside a frameset frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotALocatorInsideAFramesetFrame()
        {
            await Page.GoToAsync(Prefix + "/frames/frameset.html").ConfigureAwait(false);

            string snapshot = await FrameAt(1).Locator("body").AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai }).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - generic [ref=f1e1]: Hi, I'm frame
  ");

            await Assertions.Expect(Page.Locator("aria-ref=f1e1")).ToHaveTextAsync("Hi, I'm frame").ConfigureAwait(false);
            ILocator resolved = await Page.Locator("aria-ref=f1e1").NormalizeAsync().ConfigureAwait(false);
            Assert.That(resolved.ToString(), Is.EqualTo("locator('frame').first().contentFrame().locator('body')"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should stitch iframes inside a frameset frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStitchIframesInsideAFramesetFrame()
        {
            Server.SetRoute("/frameset-with-iframe.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<frameset><frame src=\"/frame-with-iframe.html\"></frameset>").ConfigureAwait(false);
            });
            Server.SetRoute("/frame-with-iframe.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<button>In frame</button><iframe srcdoc=\"<button>In iframe</button>\"></iframe>").ConfigureAwait(false);
            });
            await Page.GoToAsync(Prefix + "/frameset-with-iframe.html").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - iframe [ref=e2]:
      - generic [ref=f1e1]:
        - button ""In frame"" [ref=f1e2]
        - iframe [ref=f1e3]:
          - button ""In iframe"" [ref=f2e2]
  ");
            await Assertions.Expect(Page.Locator("aria-ref=f1e2")).ToHaveTextAsync("In frame").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("aria-ref=f2e2")).ToHaveTextAsync("In iframe").ConfigureAwait(false);

            ILocator resolved = await Page.Locator("aria-ref=f2e2").NormalizeAsync().ConfigureAwait(false);
            Assert.That(resolved.ToString(), Is.EqualTo("locator('frame').contentFrame().locator('iframe').contentFrame().getByRole('button', { name: 'In iframe' })"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should stitch nested frameset documents")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStitchNestedFramesetDocuments()
        {
            Server.SetRoute("/outer-frameset.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<frameset><frame src=\"/inner-frameset.html\"></frameset>").ConfigureAwait(false);
            });
            Server.SetRoute("/inner-frameset.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<frameset><frameset><frame src=\"/leaf.html\"></frameset></frameset>").ConfigureAwait(false);
            });
            Server.SetRoute("/leaf.html", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<button>Leaf button</button>").ConfigureAwait(false);
            });
            await Page.GoToAsync(Prefix + "/outer-frameset.html").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - iframe [ref=e2]:
      - iframe [ref=f1e3]:
        - button ""Leaf button"" [ref=f2e2]
  ");
            await Assertions.Expect(Page.Locator("aria-ref=f2e2")).ToHaveTextAsync("Leaf button").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should collapse inline generic nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollapseInlineGenericNodes()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li><b>3</b> <abbr>bds</abbr></li>
      <li><b>2</b> <abbr>ba</abbr></li>
      <li><b>1,200</b> <abbr>sqft</abbr></li>
    </ul>
    <ul>
      <li><div>3</div></li>
      <li><div>2</div></li>
      <li><div>1,200</div></li>
    </ul>").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - list [ref=e2]:
        - listitem [ref=e3]: 3 bds
        - listitem [ref=e4]: 2 ba
        - listitem [ref=e5]: 1,200 sqft
      - list [ref=e6]:
        - listitem [ref=e7]:
          - generic [ref=e8]: ""3""
        - listitem [ref=e9]:
          - generic [ref=e10]: ""2""
        - listitem [ref=e11]:
          - generic [ref=e12]: 1,200
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should inline single leaf generic child into parent generic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInlineSingleLeafGenericChildIntoParentGeneric()
        {
            await Page.SetContentAsync(@"
    <div><img src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA=""><div>Status: Open.</div></div>
    <div><img src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA=""><div><img src=""data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA=""><div>Nested twice.</div></div></div>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - generic [ref=e2]: ""Status: Open.""
      - generic [ref=e5]: Nested twice.
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should inline a deeply nested generic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInlineADeeplyNestedGeneric()
        {
            string img = "<img src=\"data:image/gif;base64,R0lGODlhAQABAAAAACwAAAAAAQABAAA=\">";
            await Page.SetContentAsync(@"
    <div>" + img + "<div>" + img + "<div>" + img + "<div>" + img + @"<div>Deeply nested.</div></div></div></div></div>
  ").ConfigureAwait(false);

            string snapshot = await SnapshotForAIAsync(Page).ConfigureAwait(false);
            AssertContainsYaml(snapshot, @"
    - generic [active] [ref=e1]: Deeply nested.
  ");
            Assert.That(snapshot, Does.Not.Contain("img"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should not remove generic nodes with title")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRemoveGenericNodesWithTitle()
        {
            await Page.SetContentAsync("<div title=\"Element title\">Element content</div>").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic ""Element title"" [ref=e2]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should limit depth")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLimitDepth()
        {
            await Page.SetContentAsync(@"
    <ul>
      <li>item1</li>
      <a href=""about:blank"" style=""cursor:pointer"">link</a>
      <li>
        <ul id=target>
          <li>item2</li>
          <li>
            <ul>
              <li>item3</li>
            </ul>
          </li>
        </ul>
      </li>
    </ul>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page, depth: 1).ConfigureAwait(false), @"
    - list [ref=e2]:
      - listitem [ref=e3]: item1
      - link ""link"" [ref=e4] [cursor=pointer]:
        - /url: about:blank
      - listitem [ref=e5]
  ");

            AssertContainsYaml(await SnapshotForAIAsync(Page, depth: 3).ConfigureAwait(false), @"
    - list [ref=e2]:
      - listitem [ref=e3]: item1
      - link ""link"" [ref=e4] [cursor=pointer]:
        - /url: about:blank
      - listitem [ref=e5]:
        - list [ref=e6]:
          - listitem [ref=e7]: item2
          - listitem [ref=e8]
  ");

            AssertContainsYaml(await SnapshotForAIAsync(Page, depth: 100).ConfigureAwait(false), @"
    - list [ref=e2]:
      - listitem [ref=e3]: item1
      - link ""link"" [ref=e4] [cursor=pointer]:
        - /url: about:blank
      - listitem [ref=e5]:
        - list [ref=e6]:
          - listitem [ref=e7]: item2
          - listitem [ref=e8]:
            - list [ref=e9]:
              - listitem [ref=e10]: item3
  ");

            AssertContainsYaml(await Page.Locator("#target").AriaSnapshotAsync(new() { Mode = AriaSnapshotMode.Ai, Depth = 1 }).ConfigureAwait(false), @"
    - list [ref=e6]:
      - listitem [ref=e7]: item2
      - listitem [ref=e8]
  ");
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should annotate aria-hidden elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAnnotateAriaHiddenElements()
        {
            await Page.SetContentAsync(@"
    <h2>Visible heading</h2>
    <div aria-hidden=""true"">
      <h1>Hidden heading</h1>
      <p>Hidden content</p>
    </div>
    <h2>After hidden</h2>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [active] [ref=e1]:
      - heading ""Visible heading"" [level=2] [ref=e2]
      - generic [aria-hidden] [ref=e3]:
        - heading [level=1] [ref=e4]: Hidden heading
        - paragraph [ref=e5]: Hidden content
      - heading ""After hidden"" [level=2] [ref=e6]
  ");

            string defaultSnapshot = await Page.Locator("body").AriaSnapshotAsync().ConfigureAwait(false);
            Assert.That(defaultSnapshot, Does.Not.Contain("Hidden content"));
        }

        [PlaywrightTest("page-aria-snapshot-ai.spec.ts", "should only annotate the top element in a hidden subtree")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOnlyAnnotateTheTopElementInAHiddenSubtree()
        {
            await Page.SetContentAsync(@"
    <div aria-hidden=""true"">
      <h1>Heading</h1>
      <p>Paragraph</p>
    </div>
  ").ConfigureAwait(false);

            AssertContainsYaml(await SnapshotForAIAsync(Page).ConfigureAwait(false), @"
    - generic [aria-hidden] [ref=e2]:
      - heading [level=1] [ref=e3]: Heading
      - paragraph [ref=e4]: Paragraph
  ");
        }
    }
}
