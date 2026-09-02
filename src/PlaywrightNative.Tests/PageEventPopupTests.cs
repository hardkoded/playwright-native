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
    /// Official <c>page-event-popup.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class PageEventPopupTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static SimpleServer _server;
        private static string _prefix = TestConstants.ServerUrl;
        private static string _emptyPage = TestConstants.EmptyPage;

        private static void EnsureServer()
        {
            if (_server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                _server = TestServerSetup.Server;
                _prefix = TestConstants.ServerUrl;
                _emptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19336;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    _server = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    _prefix = origin;
                    _emptyPage = origin + "/empty.html";
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
                _server = null;
            }
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work")]
        [PlaywrightTest("page-event-popup.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                "(() => { window['__popup'] = window.open('about:blank'); })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with window features")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithWindowFeatures()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(_emptyPage).ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                @"(() => {
                    window['__popup'] = window.open(window.location.href, 'Title', 'toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=yes,resizable=yes,width=780,height=200,top=0,left=0');
                })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should emit for immediately closed popups")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitForImmediatelyClosedPopups()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                @"(() => {
                    const win = window.open('about:blank');
                    win.close();
                })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should emit for immediately closed popups 2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitForImmediatelyClosedPopups2()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(_emptyPage).ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                @"(() => {
                    const win = window.open(window.location.href);
                    win.close();
                })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should be able to capture alert")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToCaptureAlert()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task evaluateTask = page.EvaluateAsync<object>(
                @"(() => {
                    const win = window.open('');
                    win.alert('hello');
                })()");
            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task<IDialog> dialogTask = context.WaitForDialogAsync();
            await Task.WhenAll(popupTask, dialogTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            IDialog dialog = await dialogTask.ConfigureAwait(false);

            Assert.That(dialog.Message, Is.EqualTo("hello"));
            Assert.That(dialog.Page, Is.SameAs(popup));
            await dialog.DismissAsync().ConfigureAwait(false);
            await evaluateTask.ConfigureAwait(false);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with empty url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithEmptyUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                "(() => { window['__popup'] = window.open(''); })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with noopener and no url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNoopenerAndNoUrl()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                "(() => { window['__popup'] = window.open(undefined, null, 'noopener'); })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            // Chromium reports `about:blank#blocked` here.
            Assert.That(popup.Url.Split('#')[0], Is.EqualTo("about:blank"));
            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with noopener and about:blank")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNoopenerAndAboutBlank()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                "(() => { window['__popup'] = window.open('about:blank', null, 'noopener'); })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with noopener and url")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNoopenerAndUrl()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(_emptyPage).ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync<object>(
                "url => { window['__popup'] = window.open(url, null, 'noopener'); }",
                _emptyPage);
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with clicking target=_blank")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithClickingTargetBlank()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(_emptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a target=_blank rel=\"opener\" href=\"/one-style.html\">yo</a>").ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task clickTask = page.ClickAsync("a");
            await Task.WhenAll(popupTask, clickTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.True);
            Assert.That(popup.MainFrame.Page, Is.SameAs(popup));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with fake-clicking target=_blank and rel=noopener")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithFakeClickingTargetBlankAndRelNoopener()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(_emptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a target=_blank rel=noopener href=\"/one-style.html\">yo</a>").ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task clickTask = page.EvalOnSelectorAsync<object>("a", "a => a.click()");
            await Task.WhenAll(popupTask, clickTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should work with clicking target=_blank and rel=noopener")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithClickingTargetBlankAndRelNoopener()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(_emptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a target=_blank rel=noopener href=\"/one-style.html\">yo</a>").ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task clickTask = page.ClickAsync("a");
            await Task.WhenAll(popupTask, clickTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
            Assert.That(await popup.EvaluateAsync<bool>("(() => !!window.opener)()").ConfigureAwait(false), Is.False);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "should report popup opened from iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportPopupOpenedFromIframes()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(_prefix + "/frames/two-frames.html").ConfigureAwait(false);

            List<IFrame> frames = new List<IFrame>(page.Frames);
            IFrame frame = frames[1];
            Assert.That(frame, Is.Not.Null);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = frame.EvaluateAsync<object>("(() => { window.open(''); })()");
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
        }
    }
}
