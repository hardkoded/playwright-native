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
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/page-event-crash.spec.ts</c> parity. Official
    /// Chromium skip on Ubuntu 24.04 (crash event never dispatches). Official
    /// <c>test.fixme</c> on in-flight worker.evaluate. Official Android skip
    /// on context-close is omitted (no Android). Do not edit leftover
    /// <c>PageCrashTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryPageEventCrashParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19886;
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

        [SetUp]
        public async Task SetUpAsync()
        {
            if (TestConstants.IsChromium && IsUbuntu2404())
            {
                Assert.Ignore("official skip: never dispatches the crash event");
            }

            Server?.Reset();
            await DisposeSessionAsync().ConfigureAwait(false);
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            await DisposeSessionAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should emit crash event when page crashes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitCrashEventWhenPageCrashes()
        {
            await _page.SetContentAsync("<div>This page should crash</div>").ConfigureAwait(false);
            Task<IPage> wait = _page.WaitForCrashAsync();
            Crash();
            IPage crashed = await wait.ConfigureAwait(false);
            Assert.That(crashed, Is.SameAs(_page));
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should throw on any action after page crashes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnAnyActionAfterPageCrashes()
        {
            EnsureServer();
            await _page.SetContentAsync("<div>This page should crash</div>").ConfigureAwait(false);
            Crash();
            await _page.WaitForCrashAsync().ConfigureAwait(false);
            await ExpectCrashErrorAsync(() => _page.EvaluateAsync("() => {}")).ConfigureAwait(false);
            await ExpectCrashErrorAsync(() => _page.GoToAsync(EmptyPage)).ConfigureAwait(false);
            await ExpectCrashErrorAsync(() => _page.ReloadAsync()).ConfigureAwait(false);
        }

        [PlaywrightTest("page-event-crash.spec.ts", "expect should not hang when page crashed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ExpectShouldNotHangWhenPageCrashed()
        {
            Task visible = Assertions.Expect(_page.GetByText("child")).ToBeVisibleAsync();
            Crash();
            Assert.CatchAsync(async () => await visible.ConfigureAwait(false));
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should cancel waitForEvent when page crashes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCancelWaitForEventWhenPageCrashes()
        {
            await _page.SetContentAsync("<div>This page should crash</div>").ConfigureAwait(false);
            Task<IResponse> wait = _page.WaitForEventAsync(PageEvent.Response);
            Crash();
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await wait.ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("Page crashed"));
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should cancel navigation when page crashes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCancelNavigationWhenPageCrashes()
        {
            EnsureServer();
            await _page.SetContentAsync("<div>This page should crash</div>").ConfigureAwait(false);
            Server.SetRoute("/one-style.css", _ => new TaskCompletionSource<bool>().Task);
            Task<IResponse> gotoTask = _page.GoToAsync(Prefix + "/one-style.html");
            await _page.WaitForNavigationAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
            Crash();
            Exception error = Assert.CatchAsync(async () => await gotoTask.ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("page.goto: Page crashed"));
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should be able to close context when page crashes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToCloseContextWhenPageCrashes()
        {
            await _page.SetContentAsync("<div>This page should crash</div>").ConfigureAwait(false);
            Crash();
            await _page.WaitForCrashAsync().ConfigureAwait(false);
            await _page.Context.CloseAsync().ConfigureAwait(false);
            _context = null;
            _page = null;
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should be able to close page after crash")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToClosePageAfterCrash()
        {
            await _page.SetContentAsync("<div>This page should crash</div>").ConfigureAwait(false);
            Crash();
            await _page.WaitForCrashAsync().ConfigureAwait(false);
            await _page.CloseAsync().ConfigureAwait(false);
            Assert.That(_page.IsClosed, Is.True);
        }

        [PlaywrightTest("page-event-crash.spec.ts", "should reject in-flight worker.evaluate when page crashes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectInFlightWorkerEvaluateWhenPageCrashes()
        {
            Assert.Ignore("official test.fixme");
            EnsureServer();
            await _page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IWorker> workerTask = _page.WaitForEventAsync(PageEvent.Worker);
            await _page.EvaluateAsync(
                    "() => new Worker(URL.createObjectURL(new Blob(['self.onmessage = () => {}'], { type: 'application/javascript' })))")
                .ConfigureAwait(false);
            IWorker worker = await workerTask.ConfigureAwait(false);
            Task<object> evalTask = worker.EvaluateAsync<object>("() => new Promise(() => {})");
            Crash();
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await evalTask.ConfigureAwait(false));
            Assert.That(error.Message, Does.Contain("crash"));
        }

        private void Crash()
        {
            if (TestConstants.IsChromium)
            {
                _ = _page.GoToAsync("chrome://crash");
                return;
            }

            if (TestConstants.IsWebKit)
            {
                _ = ((WKPage)_page).CrashForTestsAsync();
            }
        }

        private async Task ExpectCrashErrorAsync(Func<Task> action)
        {
            PlaywrightNativeException error = Assert.ThrowsAsync<PlaywrightNativeException>(
                async () => await action().ConfigureAwait(false));
            Assert.That(error, Is.Not.Null, "action should reject after crash");
            if (TestConstants.IsFirefox)
            {
                Assert.That(
                    error.Message.Contains("has been closed", StringComparison.Ordinal)
                    || error.Message.Contains("crashed", StringComparison.Ordinal),
                    Is.True,
                    error.Message);
                return;
            }

            Assert.That(error.Message, Does.Contain("crashed"));
        }

        private bool IsUbuntu2404()
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            try
            {
                string text = File.ReadAllText("/etc/os-release");
                return text.Contains("VERSION_ID=\"24.04\"", StringComparison.Ordinal)
                    || text.Contains("VERSION_ID=24.04", StringComparison.Ordinal);
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private async Task DisposeSessionAsync()
        {
            if (_context != null)
            {
                try
                {
                    await _context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _context = null;
                _page = null;
            }

            if (_browser != null)
            {
                try
                {
                    await _browser.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _browser = null;
            }
        }
    }
}
