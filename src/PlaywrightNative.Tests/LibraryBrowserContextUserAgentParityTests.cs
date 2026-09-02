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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-user-agent.spec.ts</c> parity.
    /// Do not edit leftover <c>LaunchPersistentUserAgentTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextUserAgentParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19855;
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

        [PlaywrightTest("browsercontext-user-agent.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false),
                    Does.Contain("Mozilla"));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { UserAgent = "foobar" }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Task<string> requestTask = Server.WaitForRequest("/empty.html", request => request.Headers["user-agent"].ToString());
                await Task.WhenAll(page.GoToAsync(EmptyPage), requestTask).ConfigureAwait(false);
                Assert.That(requestTask.Result, Is.EqualTo("foobar"));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-user-agent.spec.ts", "should work for subframes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForSubframes()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false),
                    Does.Contain("Mozilla"));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { UserAgent = "foobar" }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Task<string> requestTask = Server.WaitForRequest("/empty.html", request => request.Headers["user-agent"].ToString());
                await Task.WhenAll(AttachFrameAsync(page, "frame1", EmptyPage), requestTask).ConfigureAwait(false);
                Assert.That(requestTask.Result, Is.EqualTo("foobar"));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-user-agent.spec.ts", "should emulate device user-agent")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmulateDeviceUserAgent()
        {
            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false),
                    Does.Not.Contain("iPhone"));
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(new() { UserAgent = Playwright.Devices["iPhone 6"].UserAgent }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(Prefix + "/mobile.html").ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<string>("() => navigator.userAgent").ConfigureAwait(false),
                    Does.Contain("iPhone"));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("browsercontext-user-agent.spec.ts", "should make a copy of default options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMakeACopyOfDefaultOptions()
        {
            EnsureServer();
            BrowserContextOptions options = new BrowserContextOptions { UserAgent = "foobar" };
            IBrowserContext context = await _browser.NewContextAsync(options).ConfigureAwait(false);
            options.UserAgent = "wrong";
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<string> requestTask = Server.WaitForRequest("/empty.html", request => request.Headers["user-agent"].ToString());
            await Task.WhenAll(page.GoToAsync(EmptyPage), requestTask).ConfigureAwait(false);
            Assert.That(requestTask.Result, Is.EqualTo("foobar"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-user-agent.spec.ts", "custom user agent for download")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CustomUserAgentForDownload()
        {
            EnsureServer();
            Server.SetRoute("/download", http =>
            {
                http.Response.Headers["Content-Type"] = "application/octet-stream";
                http.Response.Headers["Content-Disposition"] = "attachment";
                return http.Response.WriteAsync("Hello world");
            });

            IBrowserContext context = await _browser.NewContextAsync(new() { UserAgent = "MyCustomUA" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.SetContentAsync("<a id=\"download\" download=\"name\" href=\"/download\">Download</a>").ConfigureAwait(false);
            Task<string> serverRequest = Server.WaitForRequest("/download", request => request.Headers["user-agent"].ToString());
            Task clickTask = page.ClickAsync("#download");
            string userAgent = await serverRequest.ConfigureAwait(false);
            try
            {
                await clickTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
            Assert.That(userAgent, Is.EqualTo("MyCustomUA"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-user-agent.spec.ts", "should work for navigator.userAgentData and sec-ch-ua headers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForNavigatorUserAgentDataAndSecChUaHeaders()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("This API is Chromium-only");
            }

            EnsureServer();
            {
                IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Task<(string Ua, string Mobile, string Platform)> requestTask = Server.WaitForRequest(
                    "/empty.html",
                    request => (
                        request.Headers["sec-ch-ua"].ToString(),
                        request.Headers["sec-ch-ua-mobile"].ToString(),
                        request.Headers["sec-ch-ua-platform"].ToString()));
                await Task.WhenAll(page.GoToAsync(EmptyPage), requestTask).ConfigureAwait(false);
                Assert.That(requestTask.Result.Ua, Does.Contain("\"Chromium\""));
                Assert.That(requestTask.Result.Mobile, Is.EqualTo("?0"));
                Assert.That(requestTask.Result.Platform, Is.Not.Null.And.Not.Empty);
                JsonElement data = await page.EvaluateAsync<JsonElement>(
                    "() => window.navigator.userAgentData.toJSON()").ConfigureAwait(false);
                Assert.That(data.GetProperty("mobile").GetBoolean(), Is.False);
                await context.CloseAsync().ConfigureAwait(false);
            }

            {
                IBrowserContext context = await _browser.NewContextAsync(Playwright.Devices["Pixel 7"]).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Task<(string Ua, string Mobile, string Platform)> requestTask = Server.WaitForRequest(
                    "/empty.html",
                    request => (
                        request.Headers["sec-ch-ua"].ToString(),
                        request.Headers["sec-ch-ua-mobile"].ToString(),
                        request.Headers["sec-ch-ua-platform"].ToString()));
                await Task.WhenAll(page.GoToAsync(EmptyPage), requestTask).ConfigureAwait(false);
                Assert.That(requestTask.Result.Ua, Does.Contain("\"Chromium\""));
                Assert.That(requestTask.Result.Mobile, Is.EqualTo("?1"));
                Assert.That(requestTask.Result.Platform, Is.EqualTo("\"Android\""));
                JsonElement data = await page.EvaluateAsync<JsonElement>(
                    "() => window.navigator.userAgentData.toJSON()").ConfigureAwait(false);
                Assert.That(data.GetProperty("mobile").GetBoolean(), Is.True);
                Assert.That(data.GetProperty("platform").GetString(), Is.EqualTo("Android"));
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        private static async Task AttachFrameAsync(IPage page, string frameId, string url)
        {
            string frameIdJson = JsonSerializer.Serialize(frameId);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(async () => { const frame = document.createElement('iframe'); frame.src = " +
                urlJson + "; frame.id = " + frameIdJson + "; document.body.appendChild(frame); await new Promise(x => frame.onload = x); })()")
                .ConfigureAwait(false);
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
