/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>network-post-data.spec.ts</c> parity for
    /// <see cref="IRequest.PostDataBuffer"/> and <see cref="IRequest.PostDataJSON"/>.
    /// Skipped (<c>it.fail</c> Chromium and WebKit):
    /// <c>should get post data for file/blob</c>,
    /// <c>should get post data for navigator.sendBeacon api calls</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class NetworkPostDataParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19778;
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

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
        }

        [PlaywrightTest("network-post-data.spec.ts", "should return correct postData buffer for utf-8 body")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnCorrectPostDataBufferForUtf8Body()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                const string value = "baẞ";
                Task<IRequest> waitTask = page.WaitForRequestAsync("**");
                await page.EvaluateAsync(
                    "({ url, value }) => {\n" +
                    "  const request = new Request(url, {\n" +
                    "    method: 'POST',\n" +
                    "    body: JSON.stringify(value),\n" +
                    "  });\n" +
                    "  request.headers.set('content-type', 'application/json;charset=UTF-8');\n" +
                    "  return fetch(request);\n" +
                    "}",
                    new { url = Prefix + "/title.html", value }).ConfigureAwait(false);
                IRequest request = await waitTask.ConfigureAwait(false);
                byte[] expected = Encoding.UTF8.GetBytes("\"" + value + "\"");
                Assert.That(request.PostDataBuffer, Is.EqualTo(expected));
                using JsonDocument json = request.GetPayloadAsJson();
                Assert.That(json.RootElement.GetString(), Is.EqualTo(value));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("network-post-data.spec.ts", "should return post data w/o content-type")]
        [PlaywrightTest("network-post-data.spec.ts", "should return post data w/o content-type @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnPostDataWOContentType()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IRequest> waitTask = page.WaitForRequestAsync("**");
                await page.EvaluateAsync(
                    "({ url }) => {\n" +
                    "  const request = new Request(url, {\n" +
                    "    method: 'POST',\n" +
                    "    body: JSON.stringify({ value: 42 }),\n" +
                    "  });\n" +
                    "  request.headers.set('content-type', '');\n" +
                    "  return fetch(request);\n" +
                    "}",
                    new { url = Prefix + "/title.html" }).ConfigureAwait(false);
                IRequest request = await waitTask.ConfigureAwait(false);
                using JsonDocument json = request.GetPayloadAsJson();
                Assert.That(json.RootElement.GetProperty("value").GetInt32(), Is.EqualTo(42));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("network-post-data.spec.ts", "should throw on invalid JSON in post data")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnInvalidJsonInPostData()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IRequest> waitTask = page.WaitForRequestAsync("**");
                await page.EvaluateAsync(
                    "({ url }) => {\n" +
                    "  const request = new Request(url, {\n" +
                    "    method: 'POST',\n" +
                    "    body: '<not a json>',\n" +
                    "  });\n" +
                    "  return fetch(request);\n" +
                    "}",
                    new { url = Prefix + "/title.html" }).ConfigureAwait(false);
                IRequest request = await waitTask.ConfigureAwait(false);
                Exception error = Assert.Catch(() => request.GetPayloadAsJson());
                Assert.That(error.Message, Does.Contain("POST data is not a valid JSON object: <not a json>"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("network-post-data.spec.ts", "should return post data for PUT requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnPostDataForPutRequests()
        {
            EnsureServer();
            await WithPageAsync(async page =>
            {
                await page.GoToAsync(EmptyPage).ConfigureAwait(false);
                Task<IRequest> waitTask = page.WaitForRequestAsync("**");
                await page.EvaluateAsync(
                    "({ url }) => {\n" +
                    "  const request = new Request(url, {\n" +
                    "    method: 'PUT',\n" +
                    "    body: JSON.stringify({ value: 42 }),\n" +
                    "  });\n" +
                    "  return fetch(request);\n" +
                    "}",
                    new { url = Prefix + "/title.html" }).ConfigureAwait(false);
                IRequest request = await waitTask.ConfigureAwait(false);
                using JsonDocument json = request.GetPayloadAsJson();
                Assert.That(json.RootElement.GetProperty("value").GetInt32(), Is.EqualTo(42));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("network-post-data.spec.ts", "should get post data for file/blob")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldGetPostDataForFileBlob()
        {
            Assert.Ignore("it.fail(browserName === 'webkit' || browserName === 'chromium')");
        }

        [PlaywrightTest("network-post-data.spec.ts", "should get post data for navigator.sendBeacon api calls")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldGetPostDataForNavigatorSendBeaconApiCalls()
        {
            Assert.Ignore("it.fail(chromium/webkit): postData is empty");
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
        }

        private static async Task WithPageAsync(Func<IPage, Task> body)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await body(page).ConfigureAwait(false);
        }
    }
}
