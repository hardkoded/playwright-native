/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-events.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextDialogEventTests</c>,
    /// <c>ContextWebErrorEventTests</c>,
    /// <c>ContextDownloadEventTests</c>,
    /// <c>ContextPageLoadEventTests</c>,
    /// <c>ContextPageCloseEventTests</c>,
    /// <c>ContextPageEventTests</c>,
    /// <c>ContextFrameAttachedEventTests</c>,
    /// <c>ContextFrameDetachedEventTests</c>,
    /// <c>ContextFrameNavigatedEventTests</c>,
    /// <c>ContextCloseEventTests</c>,
    /// <c>ContextNetworkEventTests</c>, or
    /// <c>ContextBackgroundPageEventTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextEventsParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19839;
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

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser == null || !_browser.IsConnected)
            {
                if (_browser != null)
                {
                    await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                }

                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }

            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            await CloseLeftoverContextsAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "console event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ConsoleEventShouldWork()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = context.WaitForConsoleMessageAsync();
            await page.EvaluateAsync("(() => console.log('hello'))()").ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);

            Assert.That(message.Text, Is.EqualTo("hello"));
            Assert.That(message.Page, Is.SameAs(page));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "console event should work with element handles")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ConsoleEventShouldWorkWithElementHandles()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<body>hello</body>").ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = context.WaitForConsoleMessageAsync();
            await page.EvaluateAsync("(() => console.log(document.body))()").ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);
            IJSHandle body = message.Args.First();
            Assert.That(await body.EvaluateAsync<string>("x => x.nodeName").ConfigureAwait(false), Is.EqualTo("BODY"));
            await body.AsElement().ClickAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "console event should work in popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ConsoleEventShouldWorkInPopup()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = context.WaitForConsoleMessageAsync();
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync(@"(() => {
                const win = window.open('');
                win.console.log('hello');
            })()").ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(message.Text, Is.EqualTo("hello"));
            Assert.That(message.Page, Is.SameAs(popup));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "console event should work in popup 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ConsoleEventShouldWorkInPopup2()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = context.WaitForConsoleMessageAsync(msg => msg.Type == "log");
            Task<IPage> popupTask = context.WaitForPageAsync();
            await page.EvaluateAsync(@"(() => {
                const win = window.open('javascript:console.log(""hello"")');
                return new Promise(f => setTimeout(() => { win.close(); f(); }, 0));
            })()").ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(message.Text, Is.EqualTo("hello"));
            Assert.That(message.Page, Is.SameAs(popup));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "console event should work in immediately closed popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ConsoleEventShouldWorkInImmediatelyClosedPopup()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = context.WaitForConsoleMessageAsync();
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync(@"(() => {
                const win = window.open();
                win.console.log('hello');
                win.close();
            })()").ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(message.Text, Is.EqualTo("hello"));
            Assert.That(message.Page, Is.SameAs(popup));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "dialog event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DialogEventShouldWork()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IDialog> dialog1Task = context.WaitForDialogAsync();
            Task<IDialog> dialog2Task = page.WaitForDialogAsync();
            Task<string> promise = page.EvaluateAsync<string>("(() => prompt('hey?'))()");
            IDialog dialog1 = await dialog1Task.ConfigureAwait(false);
            IDialog dialog2 = await dialog2Task.ConfigureAwait(false);

            Assert.That(dialog1, Is.SameAs(dialog2));
            Assert.That(dialog1.Message, Is.EqualTo("hey?"));
            Assert.That(dialog1.Page, Is.SameAs(page));
            await dialog1.AcceptAsync("hello").ConfigureAwait(false);
            Assert.That(await promise.ConfigureAwait(false), Is.EqualTo("hello"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "dialogclosed event should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DialogClosedEventShouldWork()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IDialog> dialogTask = context.WaitForDialogAsync();
            Task<string> promise = page.EvaluateAsync<string>("(() => prompt('hey?'))()");
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            Task<IDialog> closed1Task = context.WaitForDialogClosedAsync();
            Task<IDialog> closed2Task = page.WaitForDialogClosedAsync();
            await dialog.AcceptAsync("hello").ConfigureAwait(false);
            IDialog closed1 = await closed1Task.ConfigureAwait(false);
            IDialog closed2 = await closed2Task.ConfigureAwait(false);

            Assert.That(closed1, Is.SameAs(dialog));
            Assert.That(closed2, Is.SameAs(dialog));
            Assert.That(await promise.ConfigureAwait(false), Is.EqualTo("hello"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "dialog event should work in popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DialogEventShouldWorkInPopup()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IDialog> dialogTask = context.WaitForDialogAsync();
            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task<string> promise = page.EvaluateAsync<string>(@"(() => {
                const win = window.open('');
                return win.prompt('hey?');
            })()");
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(dialog.Message, Is.EqualTo("hey?"));
            Assert.That(dialog.Page, Is.SameAs(popup));
            await dialog.AcceptAsync("hello").ConfigureAwait(false);
            Assert.That(await promise.ConfigureAwait(false), Is.EqualTo("hello"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "dialog event should work in popup 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DialogEventShouldWorkInPopup2()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IDialog> dialogTask = context.WaitForDialogAsync();
            Task promise = page.EvaluateAsync(@"(() => {
                window.open('javascript:prompt(""hey?"")');
            })()");
            IDialog dialog = await dialogTask.ConfigureAwait(false);

            Assert.That(dialog.Message, Is.EqualTo("hey?"));
            Assert.That(dialog.Page, Is.Null);
            await dialog.AcceptAsync("hello").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "dialog event should work in immediately closed popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DialogEventShouldWorkInImmediatelyClosedPopup()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IDialog> dialogTask = context.WaitForDialogAsync();
            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task<string> promise = page.EvaluateAsync<string>(@"(() => {
                const win = window.open();
                const result = win.prompt('hey?');
                win.close();
                return result;
            })()");
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(dialog.Message, Is.EqualTo("hey?"));
            Assert.That(dialog.Page, Is.SameAs(popup));
            await dialog.AcceptAsync("hello").ConfigureAwait(false);
            Assert.That(await promise.ConfigureAwait(false), Is.EqualTo("hello"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "dialog event should work with inline script tag")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DialogEventShouldWorkWithInlineScriptTag()
        {
            EnsureServer();
            Server.SetRoute("/popup.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script>window.result = prompt('hey?')</script>");
            });

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a href='popup.html' target=_blank>Click me</a>").ConfigureAwait(false);

            Task promise = page.ClickAsync("a");
            Task<IDialog> dialogTask = context.WaitForDialogAsync();
            Task<IPage> popupTask = context.WaitForPageAsync();
            IDialog dialog = await dialogTask.ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            Assert.That(dialog.Message, Is.EqualTo("hey?"));
            Assert.That(dialog.Page, Is.SameAs(popup));
            await dialog.AcceptAsync("hello").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            await PollEqualAsync(() => popup.EvaluateAsync<string>("window.result"), "hello").ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "weberror event should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WebErrorEventShouldWork()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWebError> errorTask = context.WaitForWebErrorAsync();
            await page.SetContentAsync("<script>throw new Error(\"boom\")</script>").ConfigureAwait(false);
            IWebError webError = await errorTask.ConfigureAwait(false);
            Assert.That(webError.Page, Is.SameAs(page));
            Assert.That(webError.Error, Does.Contain("boom"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "weberror event should include location")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WebErrorEventShouldIncludeLocation()
        {
            EnsureServer();
            Server.SetRoute("/error.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync(@"
      function foo() {
        throw new Error('boom');
      }
      foo();
    ");
            });
            Server.SetRoute("/error.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<script src=\"/error.js\"></script>");
            });

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWebError> errorTask = context.WaitForWebErrorAsync();
            await page.GoToAsync(Prefix + "/error.html").ConfigureAwait(false);
            IWebError webError = await errorTask.ConfigureAwait(false);

            WebErrorLocation location = webError.Location;
            Assert.That(location.Url, Is.EqualTo(Prefix + "/error.js"));
            Assert.That(location.Line, Is.EqualTo(2));
            Assert.That(location.Column, Is.GreaterThan(0));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "pageload event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageLoadEventShouldWork()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> loadTask = context.WaitForPageLoadAsync();
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IPage eventPage = await loadTask.ConfigureAwait(false);
            Assert.That(eventPage, Is.SameAs(page));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "framenavigated event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FrameNavigatedEventShouldWork()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IFrame> frameTask = context.WaitForFrameNavigatedAsync();
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            IFrame frame = await frameTask.ConfigureAwait(false);
            Assert.That(frame, Is.SameAs(page.MainFrame));
            Assert.That(frame.Url, Is.EqualTo(EmptyPage));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "pageclose event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageCloseEventShouldWork()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IPage> closeTask = context.WaitForPageCloseAsync();
            await page.CloseAsync().ConfigureAwait(false);
            IPage closed = await closeTask.ConfigureAwait(false);
            Assert.That(closed, Is.SameAs(page));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "frameattached event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FrameAttachedEventShouldWork()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IFrame> frameTask = context.WaitForFrameAttachedAsync();
            await page.EvaluateAsync(@"(() => {
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
            })()").ConfigureAwait(false);
            IFrame frame = await frameTask.ConfigureAwait(false);
            Assert.That(frame.ParentFrame, Is.SameAs(page.MainFrame));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "framedetached event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task FrameDetachedEventShouldWork()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
                const iframe = document.createElement('iframe');
                iframe.id = 'x';
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
            })()").ConfigureAwait(false);
            await page.WaitForSelectorAsync("iframe").ConfigureAwait(false);
            Task<IFrame> frameTask = context.WaitForFrameDetachedAsync();
            await page.EvaluateAsync("(() => document.getElementById('x').remove())()").ConfigureAwait(false);
            IFrame frame = await frameTask.ConfigureAwait(false);
            Assert.That(frame.ParentFrame, Is.SameAs(page.MainFrame));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-events.spec.ts", "download event should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DownloadEventShouldWork()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.ContentType = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment; filename=file.txt";
                return http.Response.WriteAsync("Hello world");
            });

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<a href=\"" + Prefix + "/download\">download</a>").ConfigureAwait(false);
            Task<IDownload> downloadTask = context.WaitForDownloadAsync();
            await page.ClickAsync("a").ConfigureAwait(false);
            IDownload download = await downloadTask.ConfigureAwait(false);
            Assert.That(download.SuggestedFilename, Is.EqualTo("file.txt"));
            Assert.That(download.Page, Is.SameAs(page));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new List<IBrowserContext>(_browser.Contexts))
            {
                try
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task PollEqualAsync<T>(Func<Task<T>> getValue, T expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            T last = default;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    last = await getValue().ConfigureAwait(false);
                    if (Equals(last, expected))
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(last, Is.EqualTo(expected));
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
