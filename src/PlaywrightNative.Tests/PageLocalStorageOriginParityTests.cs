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
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-localstorage.spec.ts</c> remaining title
    /// <c>storage methods are scoped to the current origin</c>.
    /// Do not edit leftover <c>PageLocalStorageTests</c> /
    /// <c>PageSessionStorageTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageLocalStorageOriginParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19825;
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

        [PlaywrightTest("page-localstorage.spec.ts", "storage methods are scoped to the current origin")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StorageMethodsAreScopedToTheCurrentOrigin()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await Page.LocalStorage.SetItemAsync("k", "origin-1").ConfigureAwait(false);

            await Page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await Page.LocalStorage.ItemsAsync().ConfigureAwait(false), Is.Empty);
            await Page.LocalStorage.SetItemAsync("k", "origin-2").ConfigureAwait(false);

            await Page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await Page.LocalStorage.GetItemAsync("k").ConfigureAwait(false), Is.EqualTo("origin-1"));
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
