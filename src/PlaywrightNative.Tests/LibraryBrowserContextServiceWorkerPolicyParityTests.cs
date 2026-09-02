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
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-service-worker-policy.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextServiceWorkerTests</c> or
    /// <c>LaunchPersistentServiceWorkersTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextServiceWorkerPolicyParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19849;
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
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "should allow service workers by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowServiceWorkersByDefault()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/serviceworkers/empty/sw.html").ConfigureAwait(false);
            object registration = await page.EvaluateAsync<object>("(() => window[\"registrationPromise\"])()").ConfigureAwait(false);
            AssertTruthy(registration);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "blocks service worker registration")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BlocksServiceWorkerRegistration()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { ServiceWorkers = ServiceWorkerPolicy.Block }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await Task.WhenAll(
                page.WaitForEventAsync(
                    PageEvent.Console,
                    evt => evt.Text == "Service Worker registration blocked by Playwright"),
                page.GoToAsync(Prefix + "/serviceworkers/empty/sw.html")).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-service-worker-policy.spec.ts", "should not throw error on about:blank")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowErrorOnAboutBlank()
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { ServiceWorkers = ServiceWorkerPolicy.Block }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            List<string> errors = new List<string>();
            page.PageError += (_, error) => errors.Add(error);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            Assert.That(errors, Is.Empty);
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void AssertTruthy(object value)
        {
            if (value is JsonElement element)
            {
                Assert.That(element.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
                Assert.That(element.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
                Assert.That(element.ValueKind, Is.Not.EqualTo(JsonValueKind.False));
                return;
            }

            Assert.That(value, Is.Not.Null);
            if (value is bool flag)
            {
                Assert.That(flag, Is.True);
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
