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
    /// Official <c>elementhandle-scroll-into-view.spec.ts</c>.
    /// Android <c>it.fixme</c> / Chromium &lt; 105 <c>it.skip</c> are Node-only
    /// and are not applied.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleScrollIntoViewParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private static async Task TestWaitingAsync(IPage page, string after)
        {
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            bool done = false;
            Task promise = MarkDoneAsync();
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await div.EvaluateAsync(after).ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);

            async Task MarkDoneAsync()
            {
                await div.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                done = true;
            }
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19867;
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

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should work")]
        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/offscreenbuttons.html").ConfigureAwait(false);
            for (int i = 0; i < 11; i++)
            {
                IElementHandle button = await page.QuerySelectorAsync("#btn" + i.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                double before = await button.EvaluateAsync<double>(
                    "button => button.getBoundingClientRect().right - window.innerWidth").ConfigureAwait(false);
                Assert.That(before, Is.EqualTo(10d * i));
                await button.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                double after = await button.EvaluateAsync<double>(
                    "button => button.getBoundingClientRect().right - window.innerWidth").ConfigureAwait(false);
                Assert.That(after <= 0, Is.True);
                await page.EvaluateAsync("(() => window.scrollTo(0, 0))()").ConfigureAwait(false);
            }
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should throw for detached element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForDetachedElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Hello</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            await div.EvaluateAsync("div => div.remove()").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.ThrowsAsync<PlaywrightSharpException>(
                () => div.ScrollIntoViewIfNeededAsync());
            Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should wait for display:none to become visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForDisplayNoneToBecomeVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"display:none\">Hello</div>").ConfigureAwait(false);
            await TestWaitingAsync(page, "div => div.style.display = 'block'").ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should scroll display:contents into view")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldScrollDisplayContentsIntoView()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      html, body { margin: 0; padding: 0; width: 100%; height: 100%; }
      ::-webkit-scrollbar { display: none; }
      * { scrollbar-width: none; }
    </style>
    <div id=container style=""width:200px;height:200px;overflow:scroll;border:1px solid black;"">
      <div style=""margin-top:500px;background:red;"">
        <div style=""height:50px;width:100px;background:cyan;"">
          <div id=target style=""display:contents"">Hello</div>
        </div>
      <div>
    </div>
  ").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("#target").ConfigureAwait(false);
            await div.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            double scrollTop = await page.EvalOnSelectorAsync<double>("#container", "e => e.scrollTop").ConfigureAwait(false);
            Assert.That(Math.Abs(scrollTop - 350), Is.LessThan(1));
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should work for visibility:hidden element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForVisibilityHiddenElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"visibility:hidden\">Hello</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            await div.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should work for zero-sized element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForZeroSizedElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"height:0\">Hello</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            await div.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should wait for nested display:none to become visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForNestedDisplayNoneToBecomeVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<span style=\"display:none\"><div>Hello</div></span>").ConfigureAwait(false);
            await TestWaitingAsync(page, "div => div.parentElement.style.display = 'block'").ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should wait for element to stop moving")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForElementToStopMoving()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <style>
      @keyframes move {
        from { margin-left: 0; }
        to { margin-left: 200px; }
      }
      div.animated {
        animation: 2s linear 0s infinite alternate move;
      }
    </style>
    <div class=animated>moving</div>
    ").ConfigureAwait(false);
            await TestWaitingAsync(page, "div => div.classList.remove('animated')").ConfigureAwait(false);
        }

        [PlaywrightTest("elementhandle-scroll-into-view.spec.ts", "should timeout waiting for visible")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTimeoutWaitingForVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div style=\"display:none\">Hello</div>").ConfigureAwait(false);
            IElementHandle div = await page.QuerySelectorAsync("div").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => div.ScrollIntoViewIfNeededAsync(new() { Timeout = 3000 }));
            Assert.That(error.Message, Does.Contain("element is not visible"));
            Assert.That(error.Message, Does.Contain("retrying scroll into view action"));
        }
    }
}
