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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-wait-for-response.spec.ts</c> parity for
    /// <see cref="IPage.WaitForResponseAsync(string, float?)"/> and
    /// <see cref="IPage.WaitForEventAsync{T}(PlaywrightEvent{T}, Func{T, bool}, float?)"/>.
    /// Do not edit leftover <c>WaitForRequestTests</c> response titles.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageWaitForResponseParityTests : PageTestEx
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
            int basePort = 19823;
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
            if (_browser == null)
            {
                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
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
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        private IPage Page => _page;

        [PlaywrightTest("page-wait-for-response.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = Page.WaitForResponseAsync(Prefix + "/digits/2.png");
            await Page.EvaluateAsync(@"() => {
      void fetch('/digits/1.png');
      void fetch('/digits/2.png');
      void fetch('/digits/3.png');
    }").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/digits/2.png"));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRespectTimeout()
        {
            TimeoutException error = Assert.CatchAsync<TimeoutException>(
                () => Page.WaitForEventAsync(PageEvent.Response, _ => false, timeout: 1));
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Is.InstanceOf<TimeoutException>());
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should respect default timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRespectDefaultTimeout()
        {
            Page.SetDefaultTimeout(1);
            TimeoutException error = Assert.CatchAsync<TimeoutException>(
                () => Page.WaitForResponseAsync(_ => false));
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Is.InstanceOf<TimeoutException>());
            Assert.That(error.Message, Does.Contain("page.waitForResponse"));
            Assert.That(error.Message, Does.Contain("Timeout 1ms exceeded"));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should log the url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldLogTheUrl()
        {
            TimeoutException error1 = Assert.CatchAsync<TimeoutException>(
                () => Page.WaitForResponseAsync("foo.css", timeout: 1000));
            Assert.That(error1, Is.Not.Null);
            Assert.That(error1.Message, Does.Contain("waiting for response \"foo.css\""));

            TimeoutException error2 = Assert.CatchAsync<TimeoutException>(
                () => Page.WaitForResponseAsync(new Regex("foo.css", RegexOptions.IgnoreCase), new() { Timeout = 1000 }));
            Assert.That(error2, Is.Not.Null);
            Assert.That(error2.Message, Does.Contain("waiting for response /foo.css/i"));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should work with predicate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithPredicate()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = Page.WaitForEventAsync(
                PageEvent.Response,
                response => string.Equals(response.Url, Prefix + "/digits/2.png", StringComparison.Ordinal));
            await Page.EvaluateAsync(@"() => {
      void fetch('/digits/1.png');
      void fetch('/digits/2.png');
      void fetch('/digits/3.png');
    }").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/digits/2.png"));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should work with async predicate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAsyncPredicate()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> response1Task = PageWaitForEventHelper.WaitAsync(
                Page,
                PageEvent.Response,
                async response =>
                {
                    string text = await response.TextAsync().ConfigureAwait(false);
                    return text.Contains("contents of the file", StringComparison.Ordinal);
                },
                timeout: null);
            Task<IResponse> response2Task = Page.WaitForResponseAsync(
                response =>
                {
                    string text = response.TextAsync().GetAwaiter().GetResult();
                    return text.Contains("bar", StringComparison.Ordinal);
                });
            await Page.EvaluateAsync(@"() => {
      void fetch('/simple.json').then(r => r.json());
      void fetch('/file-to-upload.txt').then(r => r.text());
    }").ConfigureAwait(false);
            IResponse response1 = await response1Task.ConfigureAwait(false);
            IResponse response2 = await response2Task.ConfigureAwait(false);
            Assert.That(response1.Url, Is.EqualTo(Prefix + "/file-to-upload.txt"));
            Assert.That(response2.Url, Is.EqualTo(Prefix + "/simple.json"));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "sync predicate should be only called once")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SyncPredicateShouldBeOnlyCalledOnce()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            int counter = 0;
            Task<IResponse> responseTask = Page.WaitForEventAsync(
                PageEvent.Response,
                response =>
                {
                    counter++;
                    return string.Equals(response.Url, Prefix + "/digits/1.png", StringComparison.Ordinal);
                });
            await Page.EvaluateAsync(@"async () => {
      await fetch('/digits/1.png');
      await fetch('/digits/2.png');
      await fetch('/digits/3.png');
    }").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/digits/1.png"));
            Assert.That(counter, Is.EqualTo(1));
        }

        [PlaywrightTest("page-wait-for-response.spec.ts", "should work with no timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNoTimeout()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IResponse> responseTask = Page.WaitForResponseAsync(Prefix + "/digits/2.png", timeout: 0);
            await Page.EvaluateAsync(@"() => setTimeout(() => {
      void fetch('/digits/1.png');
      void fetch('/digits/2.png');
      void fetch('/digits/3.png');
    }, 50)").ConfigureAwait(false);
            IResponse response = await responseTask.ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(Prefix + "/digits/2.png"));
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
