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
    /// Official <c>library/browsercontext-dsf.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextDeviceTests</c> or
    /// <c>LaunchPersistentDeviceScaleFactorTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextDsfParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19838;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
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

        [PlaywrightTest("browsercontext-dsf.spec.ts", "should fetch lodpi assets @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFetchLodpiAssets()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { DeviceScaleFactor = 1 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForRequestAsync("**/image*");
            await page.GoToAsync(Prefix + "/highdpi.html").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("image1x"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-dsf.spec.ts", "should fetch hidpi assets")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFetchHidpiAssets()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { DeviceScaleFactor = 2 }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IRequest> requestTask = page.WaitForRequestAsync("**/image*");
            await page.GoToAsync(Prefix + "/highdpi.html").ConfigureAwait(false);
            IRequest request = await requestTask.ConfigureAwait(false);
            Assert.That(request.Url, Does.Contain("image2x"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task CloseLeftoverContextsAsync()
        {
            if (_browser == null)
            {
                return;
            }

            foreach (IBrowserContext context in new System.Collections.Generic.List<IBrowserContext>(_browser.Contexts))
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
