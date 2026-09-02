/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-wait-for-selector-2.spec.ts</c> parity for
    /// <see cref="IPage.WaitForSelectorAsync"/> visibility, hidden/detached,
    /// xpath, and element-handle waits.
    /// Official string/bool <c>state</c> and <c>visibility</c> are
    /// <see cref="IPage.WaitForSelectorAsync(string, string, float?, bool?)"/>
    /// and
    /// <see cref="IPage.WaitForSelectorAsync(string, WaitForSelectorState, float?, bool?, string, string)"/>.
    /// Skipped (Node-only internals):
    /// <c>should work when navigating before node adoption</c>
    /// (<c>__testHookBeforeAdoptNode</c>),
    /// <c>should fail when navigating while on handle</c>
    /// (<c>__testHookBeforeAdoptNode</c>).
    /// </summary>
    [TestFixture]
    public class PageWaitForSelector2ParityTests : PageTestEx
    {
        private const string AddElement = "tag => document.body.appendChild(document.createElement(tag))";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19793;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = origin + "/empty.html";
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

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should survive cross-process navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveCrossProcessNavigation()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool boxFound = false;
            Task<bool> waitForSelector = page.WaitForSelectorAsync(".box")
                .ContinueWith(_ => boxFound = true, TaskScheduler.Default);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(boxFound, Is.False);
            await page.ReloadAsync().ConfigureAwait(false);
            Assert.That(boxFound, Is.False);
            await page.GoToAsync(CrossProcessPrefix + "/grid.html").ConfigureAwait(false);
            await waitForSelector.ConfigureAwait(false);
            Assert.That(boxFound, Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should wait for visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool divFound = false;
            Task<bool> waitForSelector = page.WaitForSelectorAsync("div")
                .ContinueWith(_ => divFound = true, TaskScheduler.Default);
            await page.SetContentAsync("<div style='display: none; visibility: hidden;'>1</div>").ConfigureAwait(false);
            Assert.That(divFound, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.removeProperty('display')").ConfigureAwait(false);
            Assert.That(divFound, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.removeProperty('visibility')").ConfigureAwait(false);
            Assert.That(await waitForSelector.ConfigureAwait(false), Is.True);
            Assert.That(divFound, Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should not consider visible when zero-sized")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotConsiderVisibleWhenZeroSized()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div style='width: 0; height: 0;'>1</div>").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForSelectorAsync("div", new() { Timeout = 1000 }));
            Assert.That(error.Message, Does.Contain("page.waitForSelector: Timeout 1000ms exceeded"));
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.width = '10px'").ConfigureAwait(false);
            error = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForSelectorAsync("div", new() { Timeout = 1000 }));
            Assert.That(error.Message, Does.Contain("page.waitForSelector: Timeout 1000ms exceeded"));
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.height = '10px'").ConfigureAwait(false);
            Assert.That(await page.WaitForSelectorAsync("div", new() { Timeout = 1000 }).ConfigureAwait(false), Is.Not.Null);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should wait for visible recursively")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForVisibleRecursively()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool divVisible = false;
            Task<bool> waitForSelector = page.WaitForSelectorAsync("div#inner")
                .ContinueWith(_ => divVisible = true, TaskScheduler.Default);
            await page.SetContentAsync("<div style='display: none; visibility: hidden;'><div id=\"inner\">hi</div></div>").ConfigureAwait(false);
            Assert.That(divVisible, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.removeProperty('display')").ConfigureAwait(false);
            Assert.That(divVisible, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.removeProperty('visibility')").ConfigureAwait(false);
            Assert.That(await waitForSelector.ConfigureAwait(false), Is.True);
            Assert.That(divVisible, Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should consider outside of viewport visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldConsiderOutsideOfViewportVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <style>
      .cover {
        position: fixed;
        left: 0;
        top: 0;
        width: 100px;
        height: 100px;
        background-color: red;
        transform: translateX(-200px);
      }
    </style>
    <div class=""cover"">cover</div>
  ").ConfigureAwait(false);

            ILocator cover = page.Locator(".cover");
            await cover.WaitForAsync(new() { State = WaitForSelectorState.Visible }).ConfigureAwait(false);
            await Assertions.Expect(cover).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "hidden should wait for hidden")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HiddenShouldWaitForHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool divHidden = false;
            await page.SetContentAsync("<div style='display: block;'>content</div>").ConfigureAwait(false);
            Task<bool> waitForSelector = page.WaitForSelectorAsync("div", WaitForSelectorState.Hidden)
                .ContinueWith(_ => divHidden = true, TaskScheduler.Default);
            await page.WaitForSelectorAsync("div").ConfigureAwait(false);
            Assert.That(divHidden, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.setProperty('visibility', 'hidden')").ConfigureAwait(false);
            Assert.That(await waitForSelector.ConfigureAwait(false), Is.True);
            Assert.That(divHidden, Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "hidden should wait for display: none")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HiddenShouldWaitForDisplayNone()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool divHidden = false;
            await page.SetContentAsync("<div style='display: block;'>content</div>").ConfigureAwait(false);
            Task<bool> waitForSelector = page.WaitForSelectorAsync("div", WaitForSelectorState.Hidden)
                .ContinueWith(_ => divHidden = true, TaskScheduler.Default);
            await page.WaitForSelectorAsync("div").ConfigureAwait(false);
            Assert.That(divHidden, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').style.setProperty('display', 'none')").ConfigureAwait(false);
            Assert.That(await waitForSelector.ConfigureAwait(false), Is.True);
            Assert.That(divHidden, Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "hidden should wait for removal")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HiddenShouldWaitForRemoval()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            bool divRemoved = false;
            Task<bool> waitForSelector = page.WaitForSelectorAsync("div", WaitForSelectorState.Hidden)
                .ContinueWith(_ => divRemoved = true, TaskScheduler.Default);
            await page.WaitForSelectorAsync("div").ConfigureAwait(false);
            Assert.That(divRemoved, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').remove()").ConfigureAwait(false);
            Assert.That(await waitForSelector.ConfigureAwait(false), Is.True);
            Assert.That(divRemoved, Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should return null if waiting to hide non-existing element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnNullIfWaitingToHideNonExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IElementHandle handle = await page.WaitForSelectorAsync("non-existing", WaitForSelectorState.Hidden).ConfigureAwait(false);
            Assert.That(handle, Is.Null);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForSelectorAsync("div", WaitForSelectorState.Attached, 3000));
            Assert.That(error.Message, Does.Contain("page.waitForSelector: Timeout 3000ms exceeded"));
            Assert.That(error.Message, Does.Contain("waiting for locator('div')"));
            Assert.That(error, Is.InstanceOf<TimeoutException>());
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should have an error message specifically for awaiting an element to be hidden")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveAnErrorMessageSpecificallyForAwaitingAnElementToBeHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForSelectorAsync("div", WaitForSelectorState.Hidden, 1000));
            Assert.That(error.Message, Does.Contain("page.waitForSelector: Timeout 1000ms exceeded"));
            Assert.That(error.Message, Does.Contain("waiting for locator('div') to be hidden"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should respond to node attribute mutation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespondToNodeAttributeMutation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool divFound = false;
            Task<bool> waitForSelector = page.WaitForSelectorAsync(".zombo", WaitForSelectorState.Attached)
                .ContinueWith(_ => divFound = true, TaskScheduler.Default);
            await page.SetContentAsync("<div class='notZombo'></div>").ConfigureAwait(false);
            Assert.That(divFound, Is.False);
            await page.EvaluateAsync<object>("() => document.querySelector('div').className = 'zombo'").ConfigureAwait(false);
            Assert.That(await waitForSelector.ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should return the element handle")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnTheElementHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IElementHandle> waitForSelector = page.WaitForSelectorAsync(".zombo");
            await page.SetContentAsync("<div class='zombo'>anything</div>").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("x => x.textContent", await waitForSelector.ConfigureAwait(false)).ConfigureAwait(false),
                Is.EqualTo("anything"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should have correct stack trace for timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveCorrectStackTraceForTimeout()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Exception error = null;
            try
            {
                await page.WaitForSelectorAsync(".zombo", new() { Timeout = 10 }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.ToString(), Does.Contain("WaitForSelector2"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should throw for unknown state option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForUnknownStateOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>test</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.WaitForSelectorAsync("section", new PageWaitForSelectorOptions { State = "foo" }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("state: expected one of (attached|detached|visible|hidden)"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should throw for visibility option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForVisibilityOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>test</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.WaitForSelectorAsync("section", new PageWaitForSelectorOptions { Visibility = "hidden" }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("options.visibility is not supported, did you mean options.state?"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should throw for true state option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForTrueStateOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>test</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.WaitForSelectorAsync("section", new PageWaitForSelectorOptions { State = true }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("state: expected one of (attached|detached|visible|hidden)"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should throw for false state option")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForFalseStateOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>test</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.WaitForSelectorAsync("section", new PageWaitForSelectorOptions { State = false }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("state: expected one of (attached|detached|visible|hidden)"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should support >> selector syntax")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportSelectorSyntax()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = page.MainFrame;
            Task<IElementHandle> watchdog = frame.WaitForSelectorAsync("css=div >> css=span", WaitForSelectorState.Attached);
            await frame.EvaluateAsync<object>(AddElement, "br").ConfigureAwait(false);
            await frame.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            await frame.EvaluateAsync<object>("() => document.querySelector('div').appendChild(document.createElement('span'))").ConfigureAwait(false);
            IElementHandle eHandle = await watchdog.ConfigureAwait(false);
            IJSHandle tagProperty = await eHandle.GetPropertyAsync("tagName").ConfigureAwait(false);
            string tagName = await tagProperty.JsonValueAsync<string>().ConfigureAwait(false);
            Assert.That(tagName, Is.EqualTo("SPAN"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should wait for detached if already detached")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForDetachedIfAlreadyDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section id=\"testAttribute\">43543</section>").ConfigureAwait(false);
            Assert.That(await page.WaitForSelectorAsync("css=div", WaitForSelectorState.Detached).ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should wait for detached")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<section id=\"testAttribute\"><div>43543</div></section>").ConfigureAwait(false);
            bool done = false;
            Task<bool> waitFor = page.WaitForSelectorAsync("css=div", WaitForSelectorState.Detached)
                .ContinueWith(_ => done = true, TaskScheduler.Default);
            Assert.That(done, Is.False);
            await page.WaitForSelectorAsync("css=section").ConfigureAwait(false);
            Assert.That(done, Is.False);
            await page.EvalOnSelectorAsync<object>("div", "div => div.remove()").ConfigureAwait(false);
            Assert.That(await waitFor.ConfigureAwait(false), Is.True);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should support some fancy xpath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportSomeFancyXpath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<p>red herring</p><p>hello  world  </p>").ConfigureAwait(false);
            Task<IElementHandle> waitForXPath = page.WaitForSelectorAsync("//p[normalize-space(.)=\"hello world\"]");
            Assert.That(
                await page.EvaluateAsync<string>("x => x.textContent", await waitForXPath.ConfigureAwait(false)).ConfigureAwait(false),
                Is.EqualTo("hello  world  "));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should respect timeout xpath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectTimeoutXpath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.WaitForSelectorAsync("//div", WaitForSelectorState.Attached, 3000));
            Assert.That(error.Message, Does.Contain("page.waitForSelector: Timeout 3000ms exceeded"));
            Assert.That(error.Message, Does.Contain("waiting for locator('//div')"));
            Assert.That(error, Is.InstanceOf<TimeoutException>());
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should run in specified frame xpath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunInSpecifiedFrameXPath()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame2", EmptyPage).ConfigureAwait(false);
            IFrame frame1 = page.Frame("frame1");
            IFrame frame2 = page.Frame("frame2");
            Task<IElementHandle> waitForXPathPromise = frame2.WaitForSelectorAsync("//div", WaitForSelectorState.Attached);
            await frame1.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            await frame2.EvaluateAsync<object>(AddElement, "div").ConfigureAwait(false);
            IElementHandle eHandle = await waitForXPathPromise.ConfigureAwait(false);
            Assert.That(await eHandle.OwnerFrameAsync().ConfigureAwait(false), Is.SameAs(frame2));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should throw when frame is detached xpath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenFrameIsDetachedXPath()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = page.Frame("frame1");
            Exception waitError = null;
            Task waitPromise = frame.WaitForSelectorAsync("//*[@class=\"box\"]").ContinueWith(
                t =>
                {
                    waitError = t.Exception?.GetBaseException();
                    return t.Exception;
                },
                TaskScheduler.Default);
            await DetachFrameAsync(page, "frame1").ConfigureAwait(false);
            await waitPromise.ConfigureAwait(false);
            Assert.That(waitError, Is.Not.Null);
            Assert.That(waitError.Message, Does.Contain("frame.waitForSelector: Frame was detached"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should return the element handle xpath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnTheElementHandleXPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IElementHandle> waitForXPath = page.WaitForSelectorAsync("//*[@class=\"zombo\"]");
            await page.SetContentAsync("<div class='zombo'>anything</div>").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("x => x.textContent", await waitForXPath.ConfigureAwait(false)).ConfigureAwait(false),
                Is.EqualTo("anything"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should allow you to select an element with single slash xpath")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowYouToSelectAnElementWithSingleSlashXPath()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div>some text</div>").ConfigureAwait(false);
            Task<IElementHandle> waitForXPath = page.WaitForSelectorAsync("//html/body/div");
            Assert.That(
                await page.EvaluateAsync<string>("x => x.textContent", await waitForXPath.ConfigureAwait(false)).ConfigureAwait(false),
                Is.EqualTo("some text"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should correctly handle hidden shadow host")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCorrectlyHandleHiddenShadowHost()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <x-host hidden></x-host>
    <script>
      const host = document.querySelector('x-host');
      const root = host.attachShadow({ mode: 'open' });
      const style = document.createElement('style');
      style.textContent = ':host([hidden]) { display: none; }';
      root.appendChild(style);
      const child = document.createElement('div');
      child.textContent = 'Find me';
      root.appendChild(child);
    </script>
  ").ConfigureAwait(false);
            Assert.That(await page.TextContentAsync("div").ConfigureAwait(false), Is.EqualTo("Find me"));
            await page.WaitForSelectorAsync("div", WaitForSelectorState.Hidden).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should fail if element handle was detached while waiting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailIfElementHandleWasDetachedWhileWaiting()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            Task<Exception> promise = button.WaitForSelectorAsync("something").ContinueWith(
                t => t.Exception?.GetBaseException(),
                TaskScheduler.Default);
            await page.WaitForTimeoutAsync(100).ConfigureAwait(false);
            await page.EvaluateAsync<object>("() => document.body.innerText = ''").ConfigureAwait(false);
            Exception error = await promise.ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should succeed if element handle was detached while waiting for hidden")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSucceedIfElementHandleWasDetachedWhileWaitingForHidden()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            Task<IElementHandle> promise = button.WaitForSelectorAsync("something", WaitForSelectorState.Hidden);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            await page.EvaluateAsync<object>("() => document.body.innerText = ''").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-selector-2.spec.ts", "should succeed if element handle was detached while waiting for detached")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSucceedIfElementHandleWasDetachedWhileWaitingForDetached()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            Task<IElementHandle> promise = button.WaitForSelectorAsync("something", WaitForSelectorState.Detached);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            await page.EvaluateAsync<object>("() => document.body.innerText = ''").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson +
                "; f.id = " +
                nameJson +
                "; f.src = " +
                urlJson +
                "; document.body.appendChild(f); })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named != null)
                {
                    try
                    {
                        await named.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        private static async Task DetachFrameAsync(IPage page, string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.getElementById(" + nameJson + "); if (f) f.remove(); })()")
                .ConfigureAwait(false);
        }
    }
}
