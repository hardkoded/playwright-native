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
    /// Official <c>page-cache-storage.spec.ts</c> parity for CacheStorage
    /// surviving <see cref="IPage.ReloadAsync"/>.
    /// Upstream <c>test.fail</c>s WebKit
    /// (<c>Ephemeral CacheStorage is not persisted across reload in WebKit, consistent with Safari</c>)
    /// and <c>test.skip</c>s Android (<c>not supported on Android</c>); both map to
    /// <see cref="Assert.Ignore(string)"/> with those exact reasons.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageCacheStorageParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19757;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("page-cache-storage.spec.ts", "CacheStorage entry should survive page.reload()")]
        [Test]
        [Timeout(30_000)]
        public async Task CacheStorageEntryShouldSurvivePageReload()
        {
            if (OperatingSystem.IsAndroid())
            {
                Assert.Ignore("not supported on Android");
            }

            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Ephemeral CacheStorage is not persisted across reload in WebKit, consistent with Safari");
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync<object>(@"(async () => {
                const cache = await caches.open('repro-cache');
                await cache.put('/meta', new Response('payload'));
            })()").ConfigureAwait(false);

            await page.ReloadAsync().ConfigureAwait(false);

            string after = await page.EvaluateAsync<string>(@"(async () => {
                const cache = await caches.open('repro-cache');
                const resp = await cache.match('/meta');
                return resp ? await resp.text() : null;
            })()").ConfigureAwait(false);
            Assert.That(after, Is.EqualTo("payload"));
        }
    }
}
