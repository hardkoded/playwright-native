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
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>matchers.misc.spec.ts</c> parity.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MatchersMiscParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static string MessageOf(Exception error)
        {
            string message = error == null ? string.Empty : error.Message ?? string.Empty;
            return message.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

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
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = Prefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }
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
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            try
            {
                if (_context != null)
                {
                    await _context.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                if (_browser != null)
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private IPage Page => _page;

        [PlaywrightTest("matchers.misc.spec.ts", "should outlive frame navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOutliveFrameNavigation()
        {
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000).ConfigureAwait(false);
                await Page.GoToAsync(Prefix + "/grid.html").ConfigureAwait(false);
            });
            await Assertions.Expect(Page.Locator(".box").First).ToBeEmptyAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("matchers.misc.spec.ts", "should print no-locator-resolved error when locator matcher did not resolve to any element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintNoLocatorResolvedErrorWhenLocatorMatcherDidNotResolveToAnyElement()
        {
            ILocator myLocator = Page.Locator(".nonexisting");
            Func<Task>[] locatorMatchers = new Func<Task>[]
            {
                () => Assertions.Expect(myLocator).ToBeAttachedAsync(new() { Timeout = 10 }),
                () => Assertions.Expect(myLocator).ToHaveJSPropertyAsync("abc", "abc", new() { Timeout = 10 }),
                () => Assertions.Expect(myLocator).Not.ToHaveTextAsync("abc", new() { Timeout = 10 }),
                () => Assertions.Expect(myLocator).Not.ToHaveTextAsync(new Regex("abc"), new() { Timeout = 10 }),
                () => Assertions.Expect(myLocator).ToContainTextAsync("abc", new() { Timeout = 10 }),
                () => Assertions.Expect(myLocator).ToContainTextAsync(new Regex("abc"), new() { Timeout = 10 }),
            };

            for (int i = 0; i < locatorMatchers.Length; i++)
            {
                Exception error = Assert.CatchAsync(() => locatorMatchers[i]());
                Assert.That(error, Is.InstanceOf<Exception>());
                Assert.That(MessageOf(error), Does.Contain("waiting for locator('.nonexisting')"));
                Assert.That(MessageOf(error), Does.Contain("Error: element(s) not found"));
            }
        }
    }
}
