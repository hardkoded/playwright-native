/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for page wait helpers on popup, dialog, worker,
    /// and WebSocket events.
    /// </summary>
    [TestFixture]
    public class WaitForPopupTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("page-event-popup.spec.ts", "WaitForPopup resolves on window.open")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForPopupShouldResolveOnWindowOpen()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IPage> waitTask = page.WaitForPopupAsync();
            await page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);
            IPage popup = await waitTask.ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
            Assert.That(popup, Is.Not.SameAs(page));
            Assert.That(context.Pages, Does.Contain(popup));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "RunAndWaitForPopupAsync waits for window.open")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForPopupAsyncShouldReturnThePopup()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IPage popup = await page.RunAndWaitForPopupAsync(
                () => page.EvaluateAsync<bool>("window.open('about:blank'), true")).ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
            Assert.That(popup, Is.Not.SameAs(page));
            Assert.That(context.Pages, Does.Contain(popup));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "WaitForDialog resolves on alert")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForDialogShouldResolveOnAlert()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IDialog> waitTask = page.WaitForDialogAsync();
            _ = page.GoToAsync("data:text/html,<script>alert('wave122');</script>");
            IDialog dialog = await waitTask.ConfigureAwait(false);

            Assert.That(dialog, Is.Not.Null);
            Assert.That(dialog.Message, Is.EqualTo("wave122"));
            await dialog.AcceptAsync(null).ConfigureAwait(false);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "RunAndWaitForDialogAsync waits for alert")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForDialogAsyncShouldReturnTheDialog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IDialog dialog = await page.RunAndWaitForDialogAsync(
                () => page.EvaluateAsync<object>("setTimeout(() => alert('wave302'), 0)")).ConfigureAwait(false);

            Assert.That(dialog, Is.Not.Null);
            Assert.That(dialog.Message, Is.EqualTo("wave302"));
            await dialog.AcceptAsync(null).ConfigureAwait(false);
        }

        [PlaywrightTest("page-event-popup.spec.ts", "WaitForWorker resolves on Worker")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForWorkerShouldResolveOnDedicatedWorker()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> waitTask = page.WaitForWorkerAsync();
            await page.EvaluateAsync<object>(@"
                window.__w = new Worker(URL.createObjectURL(new Blob(['// wave122'], { type: 'application/javascript' })));
            ").ConfigureAwait(false);
            IWorker worker = await waitTask.ConfigureAwait(false);

            Assert.That(worker, Is.Not.Null);
            Assert.That(page.Workers, Does.Contain(worker));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "RunAndWaitForWorkerAsync waits for Worker")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForWorkerAsyncShouldReturnTheWorker()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IWorker worker = await page.RunAndWaitForWorkerAsync(() => page.EvaluateAsync<object>(@"
                window.__w = new Worker(URL.createObjectURL(new Blob(['// wave296'], { type: 'application/javascript' })));
            ")).ConfigureAwait(false);

            Assert.That(worker, Is.Not.Null);
            Assert.That(page.Workers, Does.Contain(worker));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "WaitForWebSocket resolves on WebSocket")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForWebSocketShouldResolveOnConnect()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Task<IWebSocket> waitTask = page.WaitForWebSocketAsync();
            await page.EvaluateAsync<object>(
                "url => { window.__ws = new WebSocket(url); }",
                TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws").ConfigureAwait(false);
            IWebSocket socket = await waitTask.ConfigureAwait(false);

            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.Url, Does.Contain("/ws"));
        }

        [PlaywrightTest("page-event-popup.spec.ts", "RunAndWaitForWebSocketAsync waits for WebSocket")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForWebSocketAsyncShouldReturnTheSocket()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            IWebSocket socket = await page.RunAndWaitForWebSocketAsync(
                () => page.EvaluateAsync<object>(
                    "url => { window.__ws = new WebSocket(url); }",
                    TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws")).ConfigureAwait(false);

            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.Url, Does.Contain("/ws"));
        }
    }
}
