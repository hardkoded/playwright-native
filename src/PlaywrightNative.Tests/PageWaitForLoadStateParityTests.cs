/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-wait-for-load-state.spec.ts</c> parity for
    /// <see cref="IPage.WaitForLoadStateAsync"/> and
    /// <see cref="IFrame.WaitForLoadStateAsync"/>.
    /// Official string <c>waitForLoadState('bad')</c> is
    /// <see cref="IPage.WaitForLoadStateAsync(string, float?)"/>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageWaitForLoadStateParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19442;
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

        private static async Task<bool> FixtureReachableAsync(string prefix)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2),
                };
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(prefix + "/empty.html").ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static IFrame FrameAt(IPage page, int index)
        {
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(index));
            return frames[index];
        }

        private static async Task<string> ReadyStateAsync(IPage page)
        {
            return await page.EvaluateAsync<string>("(() => document.readyState)()").ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should throw for bad state")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForBadState()
        {
            EnsureServer();
            Server.Reset();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => page.WaitForLoadStateAsync(new PageWaitForLoadStateOptions { State = "bad" }));
            Assert.That(error.Message, Does.Contain("state: expected one of (load|domcontentloaded|networkidle|commit)"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should pick up ongoing navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldPickUpOngoingNavigation()
        {
            EnsureServer();
            Server.Reset();
            TaskCompletionSource<bool> releaseCss = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task waitForCss = Server.WaitForRequest("/one-style.css");
            Server.SetRoute("/one-style.css", async http =>
            {
                await releaseCss.Task.ConfigureAwait(false);
                http.Response.StatusCode = 404;
                await http.Response.WriteAsync("Not found").ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IResponse> gotoTask = page.GoToAsync(Prefix + "/one-style.html", waitUntil: WaitUntilState.DOMContentLoaded);
            await waitForCss.ConfigureAwait(false);
            Task waitPromise = page.WaitForLoadStateAsync();
            releaseCss.TrySetResult(true);
            await waitPromise.ConfigureAwait(false);
            await gotoTask.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectTimeout()
        {
            EnsureServer();
            Server.Reset();
            Server.SetRoute("/one-style.css", _ => Task.Delay(Timeout.Infinite));

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/one-style.html", waitUntil: WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            TimeoutException ex = Assert.ThrowsAsync<TimeoutException>(
                async () => await page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 1 }).ConfigureAwait(false));
            Assert.That(ex.Message, Does.Contain("page.waitForLoadState: Timeout 1ms exceeded."));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should resolve immediately if loaded")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyIfLoaded()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            await page.WaitForLoadStateAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should resolve immediately if load state matches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveImmediatelyIfLoadStateMatches()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Server.SetRoute("/one-style.css", _ => Task.Delay(Timeout.Infinite));
            await page.GoToAsync(Prefix + "/one-style.html", waitUntil: WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should work with pages that have loaded before being connected to")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithPagesThatHaveLoadedBeforeBeingConnectedTo()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => { window['_popup'] = window.open(document.location.href); })()").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(popup.Url, Is.EqualTo(EmptyPage));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of empty url popup")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadStateOfEmptyUrlPopup()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task<string> readyStateTask = page.EvaluateAsync<string>(
                "(() => { const popup = window.open(''); return popup.document.readyState; })()");
            await Task.WhenAll(popupTask, readyStateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            string readyState = await readyStateTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(readyState, Is.EqualTo("complete"));
            Assert.That(await ReadyStateAsync(popup).ConfigureAwait(false), Is.EqualTo("complete"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of about:blank popup ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadStateOfAboutBlankPopup()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => window.open('about:blank') && 1)()").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(await ReadyStateAsync(popup).ConfigureAwait(false), Is.EqualTo("complete"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of about:blank popup with noopener ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadStateOfAboutBlankPopupWithNoopener()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => window.open('about:blank', null, 'noopener') && 1)()").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(await ReadyStateAsync(popup).ConfigureAwait(false), Is.EqualTo("complete"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of popup with network url ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadStateOfPopupWithNetworkUrl()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string urlJson = JsonSerializer.Serialize(EmptyPage);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => window.open(" + urlJson + ") && 1)()").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(await ReadyStateAsync(popup).ConfigureAwait(false), Is.EqualTo("complete"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of popup with network url and noopener ")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadStateOfPopupWithNetworkUrlAndNoopener()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string urlJson = JsonSerializer.Serialize(EmptyPage);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(() => window.open(" + urlJson + ", null, 'noopener') && 1)()").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(await ReadyStateAsync(popup).ConfigureAwait(false), Is.EqualTo("complete"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should work with clicking target=_blank")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithClickingTargetBlank()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a target=_blank rel=\"opener\" href=\"/one-style.html\">yo</a>").ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.ClickAsync("a").ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            await popup.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(await ReadyStateAsync(popup).ConfigureAwait(false), Is.EqualTo("complete"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should wait for load state of newPage")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForLoadStateOfNewPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IPage> waitTask = page.Context.WaitForPageAsync();
            Task<IPage> newPageTask = page.Context.NewPageAsync();
            IPage newPage = await waitTask.ConfigureAwait(false);
            await newPageTask.ConfigureAwait(false);
            await newPage.WaitForLoadStateAsync().ConfigureAwait(false);
            Assert.That(await ReadyStateAsync(newPage).ConfigureAwait(false), Is.EqualTo("complete"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should resolve after popup load")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldResolveAfterPopupLoad()
        {
            EnsureServer();
            Server.Reset();
            TaskCompletionSource<bool> releaseCss = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Server.SetRoute("/one-style.css", async http =>
            {
                await releaseCss.Task.ConfigureAwait(false);
                http.Response.StatusCode = 200;
                await http.Response.WriteAsync(string.Empty).ConfigureAwait(false);
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string urlJson = JsonSerializer.Serialize(Prefix + "/one-style.html");
            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task waitForCss = Server.WaitForRequest("/one-style.css");
            Task evalTask = page.EvaluateAsync("(() => { window['popup'] = window.open(" + urlJson + "); })()");
            await Task.WhenAll(popupTask, waitForCss, evalTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            bool resolved = false;
            async Task MarkPopupLoadedAsync()
            {
                await popup.WaitForLoadStateAsync().ConfigureAwait(false);
                resolved = true;
            }

            Task loadStatePromise = MarkPopupLoadedAsync();

            for (int i = 0; i < 5; i++)
            {
                // Upstream uses evaluate('window') as an IPC round-trip. Direct
                // Runtime.evaluate with returnByValue cannot serialize window.
                await page.EvaluateAsync("1").ConfigureAwait(false);
            }

            Assert.That(resolved, Is.False);
            releaseCss.TrySetResult(true);
            await loadStatePromise.ConfigureAwait(false);
            Assert.That(resolved, Is.True);
            Assert.That(popup.Url, Is.EqualTo(Prefix + "/one-style.html"));
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should work for frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForFrame()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            IFrame frame = FrameAt(page, 1);

            TaskCompletionSource<IRoute> requestTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(Prefix + "/one-style.css", route =>
            {
                requestTcs.TrySetResult(route);
            }).ConfigureAwait(false);

            await frame.GoToAsync(Prefix + "/one-style.html", waitUntil: WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            IRoute request = await requestTcs.Task.ConfigureAwait(false);

            bool resolved = false;
            async Task MarkFrameLoadedAsync()
            {
                await frame.WaitForLoadStateAsync().ConfigureAwait(false);
                resolved = true;
            }

            Task loadPromise = MarkFrameLoadedAsync();

            await page.EvaluateAsync("1").ConfigureAwait(false);
            Assert.That(resolved, Is.False);
            await request.ContinueAsync().ConfigureAwait(false);
            await loadPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should work with javascript: iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithJavascriptIframe()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<iframe src=\"javascript:false\"></iframe>", new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should work with broken data-url iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithBrokenDataUrlIframe()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<iframe src=\"data:text/html\"></iframe>", new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
        }

        [PlaywrightTest("page-wait-for-load-state.spec.ts", "should work with broken blob-url iframe")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithBrokenBlobUrlIframe()
        {
            EnsureServer();
            Server.Reset();

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<iframe src=\"blob:\"></iframe>", new() { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.Load).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
        }
    }
}
