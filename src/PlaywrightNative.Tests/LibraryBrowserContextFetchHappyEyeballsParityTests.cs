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
