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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WebSocket"/>.
    /// </summary>
    [TestFixture]
    public class PageWebSocketTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [SetUp]
        public void SendOfficialIncomingOnConnect()
        {
            Server?.SendOnWebSocketConnection("incoming");
        }

        [PlaywrightTest("web-socket.spec.ts", "WebSocket event reports the socket URL")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportWebSocketUrl()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Task<IWebSocket> waitTask = page.WaitForEventAsync(PageEvent.WebSocket);
            await page.EvaluateAsync<object>(
                "url => { window.__ws = new WebSocket(url); }",
                TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws").ConfigureAwait(false);
            IWebSocket socket = await waitTask.ConfigureAwait(false);

            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.Url, Does.Contain("/ws"));
        }

        [PlaywrightTest("web-socket.spec.ts", "WebSocket receives a server frame")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReceiveServerFrame()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Task<IWebSocket> waitTask = page.WaitForEventAsync(PageEvent.WebSocket);
            await page.EvaluateAsync<object>(
                "url => { window.__ws = new WebSocket(url); }",
                TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws").ConfigureAwait(false);
            IWebSocket socket = await waitTask.ConfigureAwait(false);

            IWebSocketFrame frame = await socket.WaitForFrameReceivedAsync(timeout: 10_000).ConfigureAwait(false);
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.Text, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("web-socket.spec.ts", "WaitForWebSocketAsync matches a URL glob")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForWebSocketShouldMatchUrlGlob()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Task<IWebSocket> waitTask = page.WaitForWebSocketAsync("**/ws");
            await page.EvaluateAsync<object>(
                "url => { window.__ws = new WebSocket(url); }",
                TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws").ConfigureAwait(false);
            IWebSocket socket = await waitTask.ConfigureAwait(false);

            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.Url, Does.Contain("/ws"));
        }

        [PlaywrightTest("web-socket.spec.ts", "WaitForWebSocketAsync matches a URL regex")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForWebSocketShouldMatchUrlRegex()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Task<IWebSocket> waitTask = page.WaitForWebSocketAsync(new System.Text.RegularExpressions.Regex("/ws$"));
            await page.EvaluateAsync<object>(
                "url => { window.__ws = new WebSocket(url); }",
                TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws").ConfigureAwait(false);
            IWebSocket socket = await waitTask.ConfigureAwait(false);

            Assert.That(socket, Is.Not.Null);
            Assert.That(socket.Url, Does.Contain("/ws"));
        }

        [PlaywrightTest("web-socket.spec.ts", "WaitForEventAsync resolves on a received frame")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForEventShouldResolveOnFrameReceived()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Task<IWebSocket> waitTask = page.WaitForEventAsync(PageEvent.WebSocket);
            await page.EvaluateAsync<object>(
                "url => { window.__ws = new WebSocket(url); }",
                TestConstants.ServerUrl.Replace("http://", "ws://", System.StringComparison.Ordinal) + "/ws").ConfigureAwait(false);
            IWebSocket socket = await waitTask.ConfigureAwait(false);

            IWebSocketFrame frame = await socket.WaitForEventAsync(WebSocketEvent.FrameReceived, timeout: 10_000).ConfigureAwait(false);
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.Text, Is.EqualTo("incoming"));

            Task<object> closeTask = socket.WaitForEventAsync("close", timeout: 10_000);
            await page.EvaluateAsync<object>("window.__ws.close()").ConfigureAwait(false);
            Assert.That(await closeTask.ConfigureAwait(false), Is.Not.Null);
        }
    }
}
