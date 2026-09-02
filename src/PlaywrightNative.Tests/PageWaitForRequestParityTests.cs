/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-wait-for-request.spec.ts</c> parity for
    /// <see cref="IPage.WaitForRequestAsync(string, float?)"/> and
    /// <see cref="IPage.WaitForEventAsync{T}(PlaywrightEvent{T}, Func{T, bool}, float?)"/>.
    /// Do not edit leftover <c>WaitForRequestTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageWaitForRequestParityTests : PageTestEx
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
            int basePort = 19824;
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

        [PlaywrightTest("page-wait-for-request.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = Page.WaitForRequestAsync(Prefix + "/digits/2.png");
            await Page.EvaluateAsync(@"() => {
      void fetch('/digits/1.png');
      void fetch('/digits/2.png');
      void fetch('/digits/3.png');
    }").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(Prefix + "/digits/2.png"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should work with predicate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithPredicate()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = Page.WaitForEventAsync(
                PageEvent.Request,
                request => string.Equals(request.Url, Prefix + "/digits/2.png", StringComparison.Ordinal));
            await Page.EvaluateAsync(@"() => {
      void fetch('/digits/1.png');
      void fetch('/digits/2.png');
      void fetch('/digits/3.png');
    }").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(Prefix + "/digits/2.png"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should respect timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRespectTimeout()
        {
            TimeoutException error = Assert.CatchAsync<TimeoutException>(
                () => Page.WaitForEventAsync(PageEvent.Request, _ => false, timeout: 1));
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Is.InstanceOf<TimeoutException>());
            Assert.That(error.Message, Does.Contain("Timeout 1ms exceeded while waiting for event \"request\""));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should respect default timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRespectDefaultTimeout()
        {
            Page.SetDefaultTimeout(1);
            TimeoutException error = Assert.CatchAsync<TimeoutException>(
                () => Page.WaitForEventAsync(PageEvent.Request, _ => false));
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Is.InstanceOf<TimeoutException>());
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should log the url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldLogTheUrl()
        {
            TimeoutException error = Assert.CatchAsync<TimeoutException>(
                () => Page.WaitForRequestAsync("long-long-long-long-long-long-long-long-long-long-long-long-long-long.css", timeout: 1000));
            Assert.That(error, Is.Not.Null);
            Assert.That(
                error.Message,
                Does.Contain("waiting for request \"long-long-long-long-long-long-long-long-long-long\u2026\""));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should work with no timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNoTimeout()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = Page.WaitForRequestAsync(Prefix + "/digits/2.png", timeout: 0);
            await Page.EvaluateAsync(@"() => setTimeout(() => {
      void fetch('/digits/1.png');
      void fetch('/digits/2.png');
      void fetch('/digits/3.png');
    }, 50)").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(Prefix + "/digits/2.png"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should work with url match")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithUrlMatch()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = Page.WaitForRequestAsync(new Regex(@"digits/\d\.png"));
            await Page.EvaluateAsync(@"() => {
      void fetch('/digits/1.png');
    }").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(Prefix + "/digits/1.png"));
        }

        [PlaywrightTest("page-wait-for-request.spec.ts", "should work with url match regular expression from a different context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithUrlMatchRegularExpressionFromADifferentContext()
        {
            EnsureServer();
            Regex regexp = new Regex(@"digits/\d\.png");
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IRequest> requestTask = Page.WaitForRequestAsync(regexp);
            await Page.EvaluateAsync(@"() => {
      void fetch('/digits/1.png');
    }").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Is.EqualTo(Prefix + "/digits/1.png"));
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
