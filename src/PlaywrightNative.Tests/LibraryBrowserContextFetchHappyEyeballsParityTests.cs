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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-fetch-happy-eyeballs.spec.ts</c>
    /// titles that do not need Node <c>__testHookLookup</c>. Skipped
    /// (Node-only internals): <c>get should work</c>,
    /// <c>get should work on request fixture</c>,
    /// <c>https post should work with ignoreHTTPSErrors option</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextFetchHappyEyeballsParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static int ServerPort = TestConstants.Port;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                ServerPort = TestConstants.Port;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19912;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    ServerPort = port;
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

        [PlaywrightTest("browsercontext-fetch-happy-eyeballs.spec.ts", "should work with ip6 and port as the host")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithIp6AndPortAsTheHost()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INSIDE_DOCKER")))
            {
                Assert.Ignore("docker does not support IPv6 by default");
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            string url = "http://[::1]:" + ServerPort.ToString(CultureInfo.InvariantCulture) + "/simple.json";
            await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            IAPIResponse response = await request.GetAsync(url).ConfigureAwait(false);
            Assert.That(response.Url, Is.EqualTo(url));
            await Assertions.Expect(response).ToBeOKAsync().ConfigureAwait(false);
        }
    }
}
