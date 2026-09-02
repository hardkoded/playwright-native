/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
    /// Official <c>library/hit-target.spec.ts</c> parity. Skip Node-only
    /// <c>library/heap.spec.ts</c> (<c>node:inspector</c>). Skip Node-only
    /// <c>browsertype-connect*</c>, <c>browsertype-launch-server</c>,
    /// <c>browsertype-launch-selenium</c>, <c>browsers-path.spec.ts</c>,
    /// <c>channels.spec.ts</c>, <c>library/browsercontext-reuse.spec.ts</c>
    /// (<c>_newContextForReuse</c>), and
    /// <c>library/browsercontext-fetch-happy-eyeballs.spec.ts</c>
    /// (<c>__testHookLookup</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryHitTargetParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19882;
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

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
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

        [PlaywrightTest("hit-target.spec.ts", "should block all events when hit target is wrong")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBlockAllEventsWhenHitTargetIsWrong()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
                const blocker = document.createElement('div');
                blocker.style.position = 'absolute';
                blocker.style.width = '400px';
                blocker.style.height = '400px';
                blocker.style.left = '0';
                blocker.style.top = '0';
                document.body.appendChild(blocker);

                const allEvents = [];
                window.allEvents = allEvents;
                for (const name of ['mousedown', 'mouseup', 'click', 'dblclick', 'auxclick', 'contextmenu', 'pointerdown', 'pointerup']) {
                    window.addEventListener(name, e => allEvents.push(e.type));
                    blocker.addEventListener(name, e => allEvents.push(e.type));
                }
            }").ConfigureAwait(false);

            Exception error = Assert.CatchAsync(() => page.ClickAsync("button", new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("page.click: Timeout 3000ms exceeded."));

            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            string[] allEvents = await page.EvaluateAsync<string[]>("() => window.allEvents").ConfigureAwait(false);
            Assert.That(allEvents, Is.Empty);
        }

        [PlaywrightTest("hit-target.spec.ts", "should block click when mousedown fails")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBlockClickWhenMousedownFails()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", @"button => {
                button.addEventListener('mousemove', () => {
                    button.style.marginLeft = '100px';
                });

                const allEvents = [];
                window.allEvents = allEvents;
                for (const name of ['mousemove', 'mousedown', 'mouseup', 'click', 'dblclick', 'auxclick', 'contextmenu', 'pointerdown', 'pointerup'])
                    button.addEventListener(name, e => allEvents.push(e.type));
            }").ConfigureAwait(false);

            await page.ClickAsync("button").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            string[] allEvents = await page.EvaluateAsync<string[]>("() => window.allEvents").ConfigureAwait(false);
            Assert.That(allEvents, Is.EqualTo(new[]
            {
                "mousemove",
                "mousemove", "pointerdown", "mousedown", "pointerup", "mouseup", "click",
            }));
        }

        [PlaywrightTest("hit-target.spec.ts", "should click when element detaches in mousedown")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickWhenElementDetachesInMousedown()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", @"button => {
                button.addEventListener('mousedown', () => {
                    window.result = 'Mousedown';
                    button.remove();
                });
            }").ConfigureAwait(false);

            await page.ClickAsync("button", new() { Timeout = 15000 }).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Mousedown"));
        }

        [PlaywrightTest("hit-target.spec.ts", "should block all events when hit target is wrong and element detaches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBlockAllEventsWhenHitTargetIsWrongAndElementDetaches()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", @"button => {
                const blocker = document.createElement('div');
                blocker.style.position = 'absolute';
                blocker.style.width = '400px';
                blocker.style.height = '400px';
                blocker.style.left = '0';
                blocker.style.top = '0';
                document.body.appendChild(blocker);

                window.addEventListener('mousemove', () => button.remove());

                const allEvents = [];
                window.allEvents = allEvents;
                for (const name of ['mousedown', 'mouseup', 'click', 'dblclick', 'auxclick', 'contextmenu', 'pointerdown', 'pointerup']) {
                    window.addEventListener(name, e => allEvents.push(e.type));
                    blocker.addEventListener(name, e => allEvents.push(e.type));
                }
            }").ConfigureAwait(false);

            Exception error = Assert.CatchAsync(() => page.ClickAsync("button", new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("page.click: Timeout 3000ms exceeded."));

            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            string[] allEvents = await page.EvaluateAsync<string[]>("() => window.allEvents").ConfigureAwait(false);
            Assert.That(allEvents, Is.Empty);
        }

        [PlaywrightTest("hit-target.spec.ts", "should not block programmatic events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotBlockProgrammaticEvents()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("button", @"button => {
                button.addEventListener('mousemove', () => {
                    button.style.marginLeft = '100px';
                    button.dispatchEvent(new MouseEvent('click'));
                });

                const allEvents = [];
                window.allEvents = allEvents;
                button.addEventListener('click', e => {
                    if (!e.isTrusted)
                        allEvents.push(e.type);
                });
            }").ConfigureAwait(false);

            await page.ClickAsync("button").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
            string[] allEvents = await page.EvaluateAsync<string[]>("() => window.allEvents").ConfigureAwait(false);
            Assert.That(allEvents, Is.EqualTo(new[] { "click", "click" }));
        }

        [PlaywrightTest("hit-target.spec.ts", "should click the button again after document.write")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButtonAgainAfterDocumentWrite()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));

            await page.EvaluateAsync(@"() => {
                document.open();
                document.write('<button onclick=""window.result2 = true""></button>');
                document.close();
            }").ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("result2").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("hit-target.spec.ts", "should work with mui select")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithMuiSelect()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/mui.html").ConfigureAwait(false);
            await page.EvaluateAsync(@"() => {
                renderComponent(e(MaterialUI.FormControl, { fullWidth: true }, [
                    e(MaterialUI.InputLabel, { id: 'demo-simple-select-label' }, ['Age']),
                    e(MaterialUI.Select, {
                        labelId: 'demo-simple-select-label',
                        id: 'demo-simple-select',
                        value: 10,
                        label: 'Age',
                    }, [
                        e(MaterialUI.MenuItem, { value: 10 }, ['Ten']),
                        e(MaterialUI.MenuItem, { value: 20 }, ['Twenty']),
                        e(MaterialUI.MenuItem, { value: 30 }, ['Thirty']),
                    ]),
                ]));
            }").ConfigureAwait(false);
            await page.ClickAsync("div.MuiFormControl-root:has-text(\"Age\")").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("text=Thirty")).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("hit-target.spec.ts", "should work with drag and drop that moves the element under cursor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithDragAndDropThatMovesTheElementUnderCursor()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/drag-n-drop-manual.html").ConfigureAwait(false);
            await page.DragAndDropAsync("#from", "#to").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#to")).ToHaveTextAsync("Dropped").ConfigureAwait(false);
        }

        [PlaywrightTest("hit-target.spec.ts", "should work with block inside inline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBlockInsideInline()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div>
      <span>
        <div id=""target"" onclick=""window._clicked=true"">
          Romimine
        </div>
      </span>
    </div>
  ").ConfigureAwait(false);
            await page.Locator("#target").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("hit-target.spec.ts", "should work with block-block-block inside inline-inline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBlockBlockBlockInsideInlineInline()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div>
      <a href=""#ney"">
        <div>
          <span>
            <a href=""#yay"">
              <div>
                <h3 id=""target"">
                  Romimine
                </h3>
              </div>
            </a>
          </span>
        </div>
      </a>
    </div>
  ").ConfigureAwait(false);
            await page.Locator("#target").ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(EmptyPage + "#yay").ConfigureAwait(false);
        }

        [PlaywrightTest("hit-target.spec.ts", "should work with block inside inline in shadow dom")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBlockInsideInlineInShadowDom()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div>
    </div>
    <script>
      const root = document.querySelector('div');
      const shadowRoot = root.attachShadow({ mode: 'open' });
      const span = document.createElement('span');
      shadowRoot.appendChild(span);
      const div = document.createElement('div');
      span.appendChild(div);
      div.id = 'target';
      div.addEventListener('click', () => window._clicked = true);
      div.textContent = 'Hello';
    </script>
  ").ConfigureAwait(false);
            await page.Locator("#target").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("hit-target.spec.ts", "should not click iframe overlaying the target")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotClickIframeOverlayingTheTarget()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button style=""position: absolute; left: 250px;bottom: 0;height: 40px;width: 200px;"" onclick=""window._clicked=1"">
      click-me
    </button>
    <div style=""background: transparent; bottom: 0px; left: 0px; margin: 0px; padding: 0px; position: fixed; z-index: 2147483647;"">
      <iframe srcdoc=""<body onclick='window.top._clicked=2' style='background-color:red;height:40px;'></body>"" style=""display: block; border: 0px; width: 100vw; height: 48px;""></iframe>
    </div>
  ").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.ClickAsync("text=click-me", new() { Timeout = 3000 }));
            Assert.That(await page.EvaluateAsync<object>("window._clicked").ConfigureAwait(false), Is.Null);
            Assert.That(
                error.Message,
                Does.Contain("<iframe srcdoc=\"<body onclick='window.top._clicked=2' style='background-color:red;height:40px;'></body>\"></iframe> from <div>…</div> subtree intercepts pointer events"));
        }

        [PlaywrightTest("hit-target.spec.ts", "should not click an element overlaying iframe with the target")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotClickAnElementOverlayingIframeWithTheTarget()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div onclick='window.top._clicked=1'>padding</div>
    <iframe width=600 height=600 srcdoc=""<iframe srcdoc='<div onclick=&quot;window.top._clicked=2&quot;>padding</div><div onclick=&quot;window.top._clicked=3&quot;>inner</div>'></iframe><div onclick='window.top._clicked=4'>outer</div>""></iframe>
    <div onclick='window.top._clicked=5' style=""position: absolute; left: 0; right: 0; top: 0; bottom: 0; background: rgba(255, 0, 0, 0.1); padding: 200px;"">PINK OVERLAY</div>
  ").ConfigureAwait(false);

            ILocator target = page.FrameLocator("iframe").FrameLocator("iframe").Locator("text=inner");
            Exception error = Assert.CatchAsync(() => target.ClickAsync(new() { Timeout = 3000 }));
            Assert.That(await page.EvaluateAsync<object>("window._clicked").ConfigureAwait(false), Is.Null);
            Assert.That(error.Message, Does.Contain("<div onclick=\"window.top._clicked=5\">PINK OVERLAY</div> intercepts pointer events"));

            await page.Locator("text=overlay").EvaluateAsync<object>("e => e.style.display = 'none'").ConfigureAwait(false);

            await target.ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window._clicked").ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("hit-target.spec.ts", "should click into frame inside closed shadow root")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickIntoFrameInsideClosedShadowRoot()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=framecontainer>
    </div>
    <script>
      const iframe = document.createElement('iframe');
      iframe.setAttribute('name', 'myframe');
      iframe.setAttribute('srcdoc', '<div onclick=""window.top.__clicked = true"">click me</div>');
      const div = document.getElementById('framecontainer');
      const host = div.attachShadow({ mode: 'closed' });
      host.appendChild(iframe);
    </script>
  ").ConfigureAwait(false);

            IFrame frame = new List<IFrame>(page.Frames)[1];
            await frame.Locator("text=click me").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("hit-target.spec.ts", "should click an element inside closed shadow root")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickAnElementInsideClosedShadowRoot()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id=container>
    </div>
    <script>
      const span = document.createElement('span');
      span.textContent = 'click me';
      span.addEventListener('click', () => window.__clicked = true);
      const div = document.getElementById('container');
      const host = div.attachShadow({ mode: 'closed' });
      host.appendChild(span);
      window.__target = span;
    </script>
  ").ConfigureAwait(false);

            IJSHandle handle = await page.EvaluateHandleAsync("window.__target").ConfigureAwait(false);
            await handle.AsElement().ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("hit-target.spec.ts", "should detect overlay from another shadow root")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectOverlayFromAnotherShadowRoot()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      div > div {
        position: absolute;
        top: 0;
        left: 0;
        width: 10px;
        height: 10px;
      }
      span {
        display: block;
        position: absolute;
        left: 0;
        top: 0;
        width: 300px;
        height: 300px;
      }
    </style>
    <div style=""position:relative; width:300px; height:300px"">
      <div id=container1></div>
      <div id=container2></div>
    </div>
    <script>
      for (const id of ['container1', 'container2']) {
        const span = document.createElement('span');
        span.id = id + '-span';
        span.textContent = 'click me';
        span.style.display = 'block';
        span.style.position = 'absolute';
        span.style.left = '20px';
        span.style.top = '20px';
        span.style.width = '300px';
        span.style.height = '300px';
        span.addEventListener('click', () => window.__clicked = id);
        const div = document.getElementById(id);
        const host = div.attachShadow({ mode: 'open' });
        host.appendChild(span);
      }
    </script>
  ").ConfigureAwait(false);

            Exception error = Assert.CatchAsync(() => page.Locator("#container1 >> text=click me").ClickAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("<div id=\"container2\"></div> intercepts pointer events"));
        }

        [PlaywrightTest("hit-target.spec.ts", "should detect overlaid element in a transformed iframe")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectOverlaidElementInATransformedIframe()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; border: none; }
      iframe {
        border: 4px solid black;
        background: gray;
        margin-left: 33px;
        margin-top: 24px;
        width: 400px;
        height: 400px;
        transform: scale(1.2);
      }
    </style>
    <iframe srcdoc=""
      <style>
        body, html { margin: 0; padding: 0; }
        div { margin-left: 10px; margin-top: 20px; width: 2px; height: 2px; }
        section { position: absolute; top: 0; left: 0; bottom: 0; right: 0; }
      </style>
      <div>Target</div>
      <section>Overlay</section>
      <script>
        document.querySelector('div').addEventListener('click', () => window.top._clicked = true);
      </script>
    ""></iframe>
  ").ConfigureAwait(false);
            ILocator locator = page.FrameLocator("iframe").Locator("div");
            Exception error = Assert.CatchAsync(() => locator.ClickAsync(new() { Timeout = 5000 }));
            Assert.That(error.Message, Does.Contain("<section>Overlay</section> intercepts pointer events"));
        }

        [PlaywrightTest("hit-target.spec.ts", "should click in iframe with padding")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickInIframeWithPadding()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; border: none; box-sizing: border-box; }
      iframe { background: gray; width: 200px; height: 200px; padding-top: 100px; }
    </style>
    <iframe srcdoc=""
      <style>
        body, html { margin: 0; padding: 0; }
        div { height: 100px; }
      </style>
      <div>Non-target</div>
      <div id=target>Target</div>
      <div>Non-target</div>
      <script>
        document.querySelector('#target').addEventListener('click', () => window.top._clicked = true);
      </script>
    ""></iframe>
  ").ConfigureAwait(false);
            ILocator locator = page.FrameLocator("iframe").Locator("#target");
            await locator.ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("hit-target.spec.ts", "should click in iframe with padding 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickInIframeWithPadding2()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      body, html, iframe { margin: 0; padding: 0; border: none; box-sizing: content-box; }
      iframe { background: gray; width: 200px; height: 200px; padding-top: 100px; }
    </style>
    <iframe srcdoc=""
      <style>
        body, html { margin: 0; padding: 0; }
        div { height: 100px; }
      </style>
      <div>Non-target</div>
      <div id=target>Target</div>
      <div>Non-target</div>
      <script>
        document.querySelector('#target').addEventListener('click', () => window.top._clicked = true);
      </script>
    ""></iframe>
  ").ConfigureAwait(false);
            ILocator locator = page.FrameLocator("iframe").Locator("#target");
            await locator.ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("hit-target.spec.ts", "should click in custom element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickInCustomElement()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <html>
      <body>
        <my-input></my-input>
        <script>
          class MyInput extends HTMLElement {
            connectedCallback() {
              this.attachShadow({mode:'open'});
              this.shadowRoot.innerHTML = '<div><span><input type=""text"" /></span></div>';
              this.shadowRoot.querySelector('input').addEventListener('click', () => window.__clicked = true);
            }
          }
          customElements.define('my-input', MyInput);
        </script>
      </body>
    </html>
  ").ConfigureAwait(false);
            await page.Locator("input").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("window.__clicked").ConfigureAwait(false), Is.True);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }
    }
}
