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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/page-close.spec.ts</c> parity for
    /// <see cref="IPage.CloseAsync(bool?, string)"/>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryPageCloseParityTests : PageTestEx
    {
        private const string TargetClosed = "Target page, context or browser has been closed";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19826;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    Prefix = "http://localhost:" + portText;
                    EmptyPage = Prefix + "/empty.html";
                    CrossProcessPrefix = "http://127.0.0.1:" + portText;
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
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
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
                    await RecycleBrowserAsync().ConfigureAwait(false);
                }
                else
                {
                    _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                }
            }

            try
            {
                _context = await NewContextOrRecycleAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                _context = await _browser.NewContextAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        private IPage Page => _page;

        [PlaywrightTest("page-close.spec.ts", "should close page with active dialog")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClosePageWithActiveDialog()
        {
            await Page.EvaluateAsync("\"trigger builtins.setTimeout\"").ConfigureAwait(false);
            await Page.SetContentAsync("<button onclick=\"setTimeout(() => alert(1))\">alert</button>")
                .ConfigureAwait(false);
            Task click = Page.ClickAsync("button");
            await Page.WaitForEventAsync(PageEvent.Dialog).ConfigureAwait(false);
            await Page.CloseAsync().ConfigureAwait(false);
            try
            {
                await click.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("page-close.spec.ts", "expect should not print timed out error message when page closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ExpectShouldNotPrintTimedOutErrorMessageWhenPageCloses()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            Task expectTask = Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("hey", new() { Timeout = 100000 });
            await Page.CloseAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(expectTask).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("expect(locator).toHaveText(expected)").Or.Contain("toHaveText"));
            Assert.That(error.Message, Does.Not.Contain("Timed out"));
        }

        [PlaywrightTest("page-close.spec.ts", "addLocatorHandler should throw when page closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AddLocatorHandlerShouldThrowWhenPageCloses()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/input/handle-locator.html").ConfigureAwait(false);
            await Page.AddLocatorHandlerAsync(
                Page.GetByText("This interstitial covers the button"),
                async () =>
                {
                    await Page.CloseAsync(new() { Reason = "custom reason" }).ConfigureAwait(false);
                }).ConfigureAwait(false);

            await Page.Locator("#aside").HoverAsync().ConfigureAwait(false);
            await Page.EvaluateAsync(@"() => {
    window.clicked = 0;
    window.setupAnnoyingInterstitial('mouseover', 1);
  }").ConfigureAwait(false);
            Exception error = await CatchAsync(Page.Locator("#target").ClickAsync()).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("custom reason"));
        }

        [PlaywrightTest("page-close.spec.ts", "should reject all promises when page is closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectAllPromisesWhenPageIsClosed()
        {
            Task evalTask = Page.EvaluateAsync("() => new Promise(r => {})");
            await Page.CloseAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(evalTask).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(TargetClosed));
        }

        [PlaywrightTest("page-close.spec.ts", "should set the page close state")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetThePageCloseState()
        {
            Assert.That(Page.IsClosed, Is.False);
            await Page.CloseAsync().ConfigureAwait(false);
            Assert.That(Page.IsClosed, Is.True);
        }

        [PlaywrightTest("page-close.spec.ts", "should pass page to close event")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassPageToCloseEvent()
        {
            Task<IPage> closedTask = Page.WaitForEventAsync(PageEvent.Close);
            await Page.CloseAsync().ConfigureAwait(false);
            IPage closedPage = await closedTask.ConfigureAwait(false);
            Assert.That(closedPage, Is.SameAs(Page));
        }

        [PlaywrightTest("page-close.spec.ts", "should terminate network waiters")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTerminateNetworkWaiters()
        {
            EnsureServer();
            Task<IRequest> requestTask = Page.WaitForRequestAsync(EmptyPage);
            Task<IResponse> responseTask = Page.WaitForResponseAsync(EmptyPage);
            await Page.CloseAsync().ConfigureAwait(false);
            Exception requestError = await CatchAsync(requestTask).ConfigureAwait(false);
            Exception responseError = await CatchAsync(responseTask).ConfigureAwait(false);
            Assert.That(requestError.Message, Does.Contain(TargetClosed));
            Assert.That(requestError.Message, Does.Not.Contain("Timeout"));
            Assert.That(responseError.Message, Does.Contain(TargetClosed));
            Assert.That(responseError.Message, Does.Not.Contain("Timeout"));
        }

        [PlaywrightTest("page-close.spec.ts", "should be callable twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeCallableTwice()
        {
            Task first = Page.CloseAsync();
            Task second = Page.CloseAsync();
            await first.ConfigureAwait(false);
            await second.ConfigureAwait(false);
            await Page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-close.spec.ts", "should return null if parent page has been closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnNullIfParentPageHasBeenClosed()
        {
            Task<IPage> popupTask = Page.WaitForEventAsync(PageEvent.Popup);
            await Page.EvaluateAsync("() => window.open('about:blank')").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await Page.CloseAsync().ConfigureAwait(false);
            IPage opener = await popup.OpenerAsync().ConfigureAwait(false);
            Assert.That(opener, Is.Null);
        }

        [PlaywrightTest("page-close.spec.ts", "should fail with error upon disconnect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailWithErrorUponDisconnect()
        {
            Task<IDownload> waitTask = Page.WaitForEventAsync(PageEvent.Download);
            await Page.CloseAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(waitTask).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(TargetClosed));
        }

        [PlaywrightTest("page-close.spec.ts", "page.close should work with window.close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageCloseShouldWorkWithWindowClose()
        {
            TaskCompletionSource<IPage> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Page.Close += (_, p) => closed.TrySetResult(p);
            await Page.CloseAsync().ConfigureAwait(false);
            await closed.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("page-close.spec.ts", "should not throw UnhandledPromiseRejection when page closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowUnhandledPromiseRejectionWhenPageCloses()
        {
            try
            {
                await Task.WhenAll(Page.CloseAsync(), Page.Mouse.ClickAsync(1, 2)).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        [PlaywrightTest("page-close.spec.ts", "interrupt request.response() and request.allHeaders() on page.close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InterruptRequestResponseAndRequestAllHeadersOnPageClose()
        {
            EnsureServer();
            Server.SetRoute("/one-style.css", async http =>
            {
                http.Response.ContentType = "text/css";
                await Task.Delay(-1).ConfigureAwait(false);
            });
            Task<IRequest> reqTask = Page.WaitForRequestAsync("**/one-style.css");
            await Page.GoToAsync(Prefix + "/one-style.html", waitUntil: WaitUntilState.DOMContentLoaded)
                .ConfigureAwait(false);
            IRequest req = await reqTask.ConfigureAwait(false);
            Task<IResponse> respTask = req.ResponseAsync();
            Task<Dictionary<string, string>> headersTask = req.AllHeadersAsync();
            await Page.CloseAsync().ConfigureAwait(false);
            Exception respError = await CatchAsync(respTask).ConfigureAwait(false);
            Assert.That(respError, Is.Not.Null);
            Assert.That(respError.Message, Does.Contain(TargetClosed));
            if (TestConstants.IsFirefox)
            {
                Dictionary<string, string> headers = await headersTask.ConfigureAwait(false);
                Assert.That(headers["user-agent"], Is.Not.Null.And.Not.Empty);
            }
            else
            {
                Exception headersError = await CatchAsync(headersTask).ConfigureAwait(false);
                Assert.That(headersError, Is.Not.Null);
                Assert.That(headersError.Message, Does.Contain(TargetClosed));
            }
        }

        [PlaywrightTest("page-close.spec.ts", "should not treat navigations as new popups")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotTreatNavigationsAsNewPopups()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync("<a target=_blank rel=noopener href=\"/one-style.html\">yo</a>")
                .ConfigureAwait(false);
            Task<IPage> popupTask = Page.WaitForEventAsync(PageEvent.Popup);
            await Page.ClickAsync("a").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            bool badSecondPopup = false;
            Page.Popup += (_, _) => badSecondPopup = true;
            await popup.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            await Page.CloseAsync().ConfigureAwait(false);
            Assert.That(badSecondPopup, Is.False);
        }

        [PlaywrightTest("page-close.spec.ts", "should not result in unhandled rejection")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotResultInUnhandledRejection()
        {
            Task<IPage> closedTask = Page.WaitForEventAsync(PageEvent.Close);
            await Page.ExposeFunctionAsync("foo", async () =>
            {
                await Page.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            await Page.EvaluateAsync(@"() => {
    setTimeout(() => window.foo(), 0);
    return undefined;
  }").ConfigureAwait(false);
            await closedTask.ConfigureAwait(false);
            Exception error = null;
            try
            {
                await Page.EvaluateAsync("1 + 1").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.That(error, Is.InstanceOf<Exception>());
        }

        [PlaywrightTest("page-close.spec.ts", "should reject response.finished if page closes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectResponseFinishedIfPageCloses()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/get", async http =>
            {
                http.Response.ContentType = "text/plain; charset=utf-8";
                await http.Response.WriteAsync("hello ").ConfigureAwait(false);
                await Task.Delay(-1).ConfigureAwait(false);
            });
            Task<IResponse> responseTask = Page.WaitForEventAsync(PageEvent.Response);
            _ = Page.EvaluateAsync("() => fetch('./get', { method: 'GET' })");
            IResponse pageResponse = await responseTask.ConfigureAwait(false);
            Task<string> finishTask = pageResponse.FinishedAsync();
            await Page.CloseAsync().ConfigureAwait(false);
            Exception error = await CatchAsync(finishTask).ConfigureAwait(false);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("closed"));
        }

        [PlaywrightTest("page-close.spec.ts", "should not throw when continuing while page is closing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenContinuingWhilePageIsClosing()
        {
            EnsureServer();
            Task done = null;
            await Page.RouteAsync("**/*", async route =>
            {
                done = Task.WhenAll(route.ContinueAsync(), Page.CloseAsync());
                await done.ConfigureAwait(false);
            }).ConfigureAwait(false);
            try
            {
                await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            Assert.That(done, Is.Not.Null);
            await done.ConfigureAwait(false);
        }

        [PlaywrightTest("page-close.spec.ts", "should not throw when continuing after page is closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenContinuingAfterPageIsClosed()
        {
            EnsureServer();
            Task done = null;
            await Page.RouteAsync("**/*", async route =>
            {
                await Page.CloseAsync().ConfigureAwait(false);
                done = route.ContinueAsync();
                await done.ConfigureAwait(false);
            }).ConfigureAwait(false);
            Exception error = await CatchAsync(Page.GoToAsync(EmptyPage)).ConfigureAwait(false);
            for (int i = 0; done == null && i < 50; i++)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.That(done, Is.Not.Null);
            await done.ConfigureAwait(false);
            Assert.That(error, Is.InstanceOf<Exception>());
        }

        private static async Task<Exception> CatchAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static async Task<Exception> CatchAsync<T>(Task<T> task)
        {
            try
            {
                await task.ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private async Task<IBrowserContext> NewContextOrRecycleAsync()
        {
            Task<IBrowserContext> create = _browser.NewContextAsync();
            Task finished = await Task.WhenAny(create, Task.Delay(5000)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, create))
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                return await _browser.NewContextAsync().ConfigureAwait(false);
            }

            return await create.ConfigureAwait(false);
        }

        private async Task RecycleBrowserAsync()
        {
            IBrowser previous = _browser;
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (previous != null)
            {
                await DisposeQuietlyAsync(previous).ConfigureAwait(false);
            }
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
