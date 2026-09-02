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
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/route-web-socket.spec.ts</c> parity. Do not edit
    /// leftover <c>RouteWebSocketTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryRouteWebSocketParityTests : PageTestEx
    {
        private const string NoMock = "no-mock";
        private const string NoMatch = "no-match";
        private const string PassThrough = "pass-through";

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static int ServerPort = TestConstants.Port;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static string Host =>
            "localhost:" + ServerPort.ToString(CultureInfo.InvariantCulture);

        private static string WsOrigin => "ws://" + Host;

        private static bool IsLinux => !TestConstants.IsWindows && !TestConstants.IsMacOSX;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19891;
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
                    ServerPort = port;
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
                ServerPort = TestConstants.Port;
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
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            Server?.Reset();
            await DisposeBrowserAsync().ConfigureAwait(false);
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            Server?.Reset();
            await DisposeBrowserAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with text message")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithTextMessage(string mock)
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            Task<SimpleServer.UpgradeConnection> upgradePromise = Server.WaitForUpgradeAsync();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false), Is.EqualTo(0));
            SimpleServer.UpgradeConnection upgrade = await upgradePromise.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await PageLogAsync(page).ConfigureAwait(false), Is.Empty);
            await upgrade.DoUpgradeAsync().ConfigureAwait(false);
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 1,
                "readyState OPEN").ConfigureAwait(false);
            Assert.That(await PageLogAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "open" }));
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            ws.Send("hello");
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "page got hello").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "message: data=hello origin=" + WsOrigin + " lastEventId=" }));
            Assert.That(await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false), Is.EqualTo(1));
            TaskCompletionSource<string> message = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            ws.OnceMessage(data => message.TrySetResult(data));
            await page.EvaluateAsync("() => window.ws.send('hi')").ConfigureAwait(false);
            Assert.That(await message.Task.ConfigureAwait(false), Is.EqualTo("hi"));
            ws.Close(1008, "oops");
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 3,
                "readyState CLOSED").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "open",
                    "message: data=hello origin=" + WsOrigin + " lastEventId=",
                    "close code=1008 reason=oops wasClean=true",
                }));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with binaryType=blob")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBinaryTypeBlob(string mock)
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            ws.Send(Encoding.UTF8.GetBytes("hi"));
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "page got blob").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "message: data=blob:hi origin=" + WsOrigin + " lastEventId=" }));
            TaskCompletionSource<string> message = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            ws.OnceMessage(data => message.TrySetResult(data));
            await page.EvaluateAsync("() => window.ws.send(new Blob([new Uint8Array(['h'.charCodeAt(0), 'i'.charCodeAt(0)])]))").ConfigureAwait(false);
            Assert.That(await message.Task.ConfigureAwait(false), Is.EqualTo("hi"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with binaryType=arraybuffer")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBinaryTypeArrayBuffer(string mock)
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            await SetupWsAsync(page, "arraybuffer").ConfigureAwait(false);
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            ws.Send(Encoding.UTF8.GetBytes("hi"));
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "page got arraybuffer").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "message: data=arraybuffer:hi origin=" + WsOrigin + " lastEventId=" }));
            TaskCompletionSource<string> message = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            ws.OnceMessage(data => message.TrySetResult(data));
            await page.EvaluateAsync("() => window.ws.send(new Uint8Array(['h'.charCodeAt(0), 'i'.charCodeAt(0)]).buffer)").ConfigureAwait(false);
            Assert.That(await message.Task.ConfigureAwait(false), Is.EqualTo("hi"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work when connection errors out")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWhenConnectionErrorsOut(string mock)
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit ignores the connection error and fires no events!");
            }

            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            Task<SimpleServer.UpgradeConnection> upgradePromise = Server.WaitForUpgradeAsync();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            SimpleServer.UpgradeConnection upgrade = await upgradePromise.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await PageLogAsync(page).ConfigureAwait(false), Is.Empty);
            upgrade.Destroy();
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 3,
                "readyState CLOSED after destroy").ConfigureAwait(false);
            string[] log = await PageLogAsync(page).ConfigureAwait(false);
            Assert.That(log.Length, Is.EqualTo(2));
            Assert.That(log[0], Is.EqualTo("error"));
            Assert.That(log[1], Does.Match(@"close code=\d+ reason= wasClean=false"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with error after successful open")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithErrorAfterSuccessfulOpen(string mock)
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("Firefox does not close the websocket upon a bad frame");
            }

            if (TestConstants.IsWebKit && (IsLinux || TestConstants.IsWindows))
            {
                Assert.Ignore("WebKit linux does not close the websocket upon a bad frame");
            }

            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            Task<SimpleServer.UpgradeConnection> upgradePromise = Server.WaitForUpgradeAsync();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            SimpleServer.UpgradeConnection upgrade = await upgradePromise.ConfigureAwait(false);
            await upgrade.DoUpgradeAsync().ConfigureAwait(false);
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 1,
                "readyState OPEN").ConfigureAwait(false);
            Assert.That(await PageLogAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "open" }));
            await upgrade.WriteRawAsync("garbage").ConfigureAwait(false);
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 3,
                "readyState CLOSED after garbage").ConfigureAwait(false);
            string[] log = await PageLogAsync(page).ConfigureAwait(false);
            Assert.That(log.Length, Is.EqualTo(3));
            Assert.That(log[0], Is.EqualTo("open"));
            Assert.That(log[1], Is.EqualTo("error"));
            Assert.That(log[2], Does.Match(@"close code=\d+ reason= wasClean=false"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with client-side close")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithClientSideClose(string mock)
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            Task<SimpleServer.UpgradeConnection> upgradePromise = Server.WaitForUpgradeAsync();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false), Is.EqualTo(0));
            SimpleServer.UpgradeConnection upgrade = await upgradePromise.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false), Is.EqualTo(0));
            Assert.That(await PageLogAsync(page).ConfigureAwait(false), Is.Empty);
            await upgrade.DoUpgradeAsync().ConfigureAwait(false);
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 1,
                "readyState OPEN").ConfigureAwait(false);
            Assert.That(await PageLogAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "open" }));
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            TaskCompletionSource<(int Code, string Reason)> closed = new TaskCompletionSource<(int, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
            ws.OnceClose((code, reason) => closed.TrySetResult((code, Encoding.UTF8.GetString(reason))));
            int readyState = await page.EvaluateAsync<int>(
                @"() => {
                    window.ws.close(3002, 'oops');
                    return window.ws.readyState;
                }").ConfigureAwait(false);
            Assert.That(readyState, Is.EqualTo(2));
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 3,
                "readyState CLOSED").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "close code=3002 reason=oops wasClean=true" }));
            (int code, string reason) = await closed.Task.ConfigureAwait(false);
            Assert.That(code, Is.EqualTo(3002));
            Assert.That(reason, Is.EqualTo("oops"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should pass through the required protocol")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassThroughTheRequiredProtocol(string mock)
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            await SetupWsAsync(page, "blob", "my-custom-protocol").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.wsOpened").ConfigureAwait(false);
            string protocol = await page.EvaluateAsync<string>("() => window.ws.protocol").ConfigureAwait(false);
            Assert.That(protocol, Is.EqualTo("my-custom-protocol"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with relative WebSocket URL")]
        [TestCase(NoMock)]
        [TestCase(NoMatch)]
        [TestCase(PassThrough)]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithRelativeWebSocketUrl(string mock)
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await InstallMockAsync(page, mock).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            await SetupWsAsync(page, "blob", relativeUrl: true).ConfigureAwait(false);
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            ws.Send(Encoding.UTF8.GetBytes("hi"));
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "page got relative blob").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "message: data=blob:hi origin=" + WsOrigin + " lastEventId=" }));
            TaskCompletionSource<string> message = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            ws.OnceMessage(data => message.TrySetResult(data));
            await page.EvaluateAsync("() => window.ws.send(new Blob([new Uint8Array(['h'.charCodeAt(0), 'i'.charCodeAt(0)])]))").ConfigureAwait(false);
            Assert.That(await message.Task.ConfigureAwait(false), Is.EqualTo("hi"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with ws.close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithWsClose()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IWebSocketRoute> opened = new TaskCompletionSource<IWebSocketRoute>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), async ws =>
            {
                ws.ConnectToServer();
                opened.TrySetResult(ws);
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            IWebSocketRoute route = await opened.Task.ConfigureAwait(false);
            route.Send("hello");
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "page got hello").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "message: data=hello origin=" + WsOrigin + " lastEventId=" }));
            TaskCompletionSource<(int Code, string Reason)> closed = new TaskCompletionSource<(int, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
            ws.OnceClose((code, reason) => closed.TrySetResult((code, Encoding.UTF8.GetString(reason))));
            await route.CloseAsync(3009, "oops").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 3,
                "page got close").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "open",
                    "message: data=hello origin=" + WsOrigin + " lastEventId=",
                    "close code=3009 reason=oops wasClean=true",
                }));
            (int code, string reason) = await closed.Task.ConfigureAwait(false);
            Assert.That(code, Is.EqualTo(3009));
            Assert.That(reason, Is.EqualTo("oops"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should observe upstream handshake failure when connectToServer is used")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldObserveUpstreamHandshakeFailureWhenConnectToServerIsUsed()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            List<(int? Code, string Reason)> serverCloses = new List<(int?, string)>();
            int routeHandlerInvoked = 0;
            await page.RouteWebSocketAsync(new Regex(".*"), ws =>
            {
                routeHandlerInvoked++;
                IWebSocketRoute serverRoute = ws.ConnectToServer();
                serverRoute.OnClose((code, reason) =>
                {
                    serverCloses.Add((code, reason));
                    _ = ws.CloseAsync();
                });
            }).ConfigureAwait(false);
            Task<SimpleServer.UpgradeConnection> upgradePromise = Server.WaitForUpgradeAsync();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            SimpleServer.UpgradeConnection socket = await upgradePromise.ConfigureAwait(false);
            await socket.WriteAsync("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
            socket.Destroy();
            await PollUntilAsync(() => Task.FromResult(serverCloses.Count == 1), "server close").ConfigureAwait(false);
            Assert.That(serverCloses[0].Code.GetValueOrDefault(), Is.GreaterThanOrEqualTo(1000));
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 3,
                "page closed").ConfigureAwait(false);
            Assert.That(routeHandlerInvoked, Is.EqualTo(1));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should observe multiple concurrent routed WebSockets with connectToServer")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldObserveMultipleConcurrentRoutedWebSocketsWithConnectToServer()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            int routedConnections = 0;
            await page.RouteWebSocketAsync(new Regex(".*"), ws =>
            {
                routedConnections++;
                IWebSocketRoute serverRoute = ws.ConnectToServer();
                ws.OnMessage(message => Forward(serverRoute, message));
                serverRoute.OnMessage(message => Forward(ws, message));
            }).ConfigureAwait(false);
            Action<WebSocket> handleConnection = null;
            handleConnection = ws =>
            {
                _ = EchoOnceAsync(ws);
                Server.OnceWebSocketConnection(handleConnection);
            };
            Server.OnceWebSocketConnection(handleConnection);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string[] results = await page.EvaluateAsync<string[]>(
                @"async host => {
                    const collect = tag => new Promise(resolve => {
                        const ws = new WebSocket('ws://' + host + '/ws');
                        ws.addEventListener('open', () => ws.send('hi-' + tag));
                        ws.addEventListener('message', event => {
                            resolve(event.data);
                            ws.close();
                        });
                    });
                    return Promise.all([collect('a'), collect('b')]);
                }",
                Host).ConfigureAwait(false);
            Array.Sort(results, StringComparer.Ordinal);
            Assert.That(results, Is.EqualTo(new[] { "echo-hi-a", "echo-hi-b" }));
            Assert.That(routedConnections, Is.EqualTo(2));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should pattern match")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPatternMatch()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.RouteWebSocketAsync(new Regex(".*/ws$"), async ws =>
            {
                ws.ConnectToServer();
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            await page.RouteWebSocketAsync("**/mock-ws", ws =>
            {
                ws.OnMessage(message => ws.Send("mock-response"));
            }).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async ({ host }) => {
                    window.log = [];
                    window.ws1 = new WebSocket('ws://' + host + '/ws');
                    window.ws1.addEventListener('message', event => window.log.push('ws1:' + event.data));
                    window.ws2 = new WebSocket('ws://' + host + '/something/something/mock-ws');
                    window.ws2.addEventListener('message', event => window.log.push('ws2:' + event.data));
                    await Promise.all([
                        new Promise(f => window.ws1.addEventListener('open', f)),
                        new Promise(f => window.ws2.addEventListener('open', f)),
                    ]);
                }",
                new { host = Host }).ConfigureAwait(false);
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            ws.OnceMessage(_ => ws.Send("response"));
            await page.EvaluateAsync("() => window.ws1.send('request')").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).SequenceEqual(new[] { "ws1:response" }),
                "ws1 response").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.ws2.send('request')").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).SequenceEqual(new[] { "ws1:response", "ws2:mock-response" }),
                "ws2 mock").ConfigureAwait(false);
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with server")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithServer()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IWebSocketRoute> opened = new TaskCompletionSource<IWebSocketRoute>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), async ws =>
            {
                IWebSocketRoute server = ws.ConnectToServer();
                ws.OnMessage(message =>
                {
                    switch (message.Text)
                    {
                        case "to-respond":
                            ws.Send("response");
                            return;
                        case "to-block":
                            return;
                        case "to-modify":
                            server.Send("modified");
                            return;
                    }

                    server.Send(message.Text);
                });
                server.OnMessage(message =>
                {
                    switch (message.Text)
                    {
                        case "to-block":
                            return;
                        case "to-modify":
                            ws.Send("modified");
                            return;
                    }

                    ws.Send(message.Text);
                });
                server.Send("fake");
                opened.TrySetResult(ws);
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            Task<OfficialServerWebSocket> wsPromise = Server.WaitForWebSocketAsync();
            List<string> log = new List<string>();
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            OfficialServerWebSocket ws = await wsPromise.ConfigureAwait(false);
            ws.OnMessage(data => log.Add("message: " + data));
            ws.OnceClose((code, reason) => log.Add(
                "close: code=" + code.ToString(CultureInfo.InvariantCulture) + " reason=" + Encoding.UTF8.GetString(reason)));
            await PollUntilAsync(() => Task.FromResult(log.Count == 1 && log[0] == "message: fake"), "server got fake").ConfigureAwait(false);

            ws.Send("to-modify");
            ws.Send("to-block");
            ws.Send("pass-server");
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 3,
                "page got modified + pass-server").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "open",
                    "message: data=modified origin=" + WsOrigin + " lastEventId=",
                    "message: data=pass-server origin=" + WsOrigin + " lastEventId=",
                }));

            await page.EvaluateAsync(
                @"() => {
                    window.ws.send('to-respond');
                    window.ws.send('to-modify');
                    window.ws.send('to-block');
                    window.ws.send('pass-client');
                }").ConfigureAwait(false);
            await PollUntilAsync(
                () => Task.FromResult(log.Count == 3 && log[2] == "message: pass-client"),
                "server got modified + pass-client").ConfigureAwait(false);
            Assert.That(log, Is.EqualTo(new[] { "message: fake", "message: modified", "message: pass-client" }));
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 4,
                "page got response").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "open",
                    "message: data=modified origin=" + WsOrigin + " lastEventId=",
                    "message: data=pass-server origin=" + WsOrigin + " lastEventId=",
                    "message: data=response origin=" + WsOrigin + " lastEventId=",
                }));

            IWebSocketRoute route = await opened.Task.ConfigureAwait(false);
            route.Send("another");
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 5,
                "page got another").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "open",
                    "message: data=modified origin=" + WsOrigin + " lastEventId=",
                    "message: data=pass-server origin=" + WsOrigin + " lastEventId=",
                    "message: data=response origin=" + WsOrigin + " lastEventId=",
                    "message: data=another origin=" + WsOrigin + " lastEventId=",
                }));

            await page.EvaluateAsync("() => { window.ws.send('pass-client-2'); }").ConfigureAwait(false);
            await PollUntilAsync(
                () => Task.FromResult(log.Count == 4 && log[3] == "message: pass-client-2"),
                "pass-client-2").ConfigureAwait(false);

            await page.EvaluateAsync("() => { window.ws.close(3009, 'problem'); }").ConfigureAwait(false);
            await PollUntilAsync(
                () => Task.FromResult(log.Count == 5 && log[4].StartsWith("close: code=3009", StringComparison.Ordinal)),
                "server close").ConfigureAwait(false);
            Assert.That(log, Is.EqualTo(new[]
            {
                "message: fake",
                "message: modified",
                "message: pass-client",
                "message: pass-client-2",
                "close: code=3009 reason=problem",
            }));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work without server")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithoutServer()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IWebSocketRoute> opened = new TaskCompletionSource<IWebSocketRoute>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), ws =>
            {
                ws.OnMessage(message =>
                {
                    if (message.Text == "to-respond")
                    {
                        ws.Send("response");
                    }
                });
                opened.TrySetResult(ws);
            }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async () => {
                    await window.wsOpened;
                    window.ws.send('to-respond');
                    window.ws.send('to-block');
                    window.ws.send('to-respond');
                }").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 3,
                "two responses").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "open",
                    "message: data=response origin=" + WsOrigin + " lastEventId=",
                    "message: data=response origin=" + WsOrigin + " lastEventId=",
                }));
            IWebSocketRoute route = await opened.Task.ConfigureAwait(false);
            route.Send("another");
            await route.CloseAsync(3008, "oops").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 5,
                "close after another").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[]
                {
                    "open",
                    "message: data=response origin=" + WsOrigin + " lastEventId=",
                    "message: data=response origin=" + WsOrigin + " lastEventId=",
                    "message: data=another origin=" + WsOrigin + " lastEventId=",
                    "close code=3008 reason=oops wasClean=true",
                }));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should emit close upon frame navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitCloseUponFrameNavigation()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IWebSocketRoute> opened = new TaskCompletionSource<IWebSocketRoute>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), async ws =>
            {
                ws.ConnectToServer();
                opened.TrySetResult(ws);
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            IWebSocketRoute route = await opened.Task.ConfigureAwait(false);
            route.Send("hello");
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "hello delivered").ConfigureAwait(false);
            TaskCompletionSource<bool> closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            route.OnClose((_, __) => closed.TrySetResult(true));
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await closed.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should emit close upon frame detach")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitCloseUponFrameDetach()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IWebSocketRoute> opened = new TaskCompletionSource<IWebSocketRoute>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), async ws =>
            {
                ws.ConnectToServer();
                opened.TrySetResult(ws);
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            IFrame frame = await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            await SetupWsAsync(frame, "blob").ConfigureAwait(false);
            IWebSocketRoute route = await opened.Task.ConfigureAwait(false);
            route.Send("hello");
            await PollUntilAsync(
                async () => (await FrameLogAsync(frame).ConfigureAwait(false)).Length == 2,
                "iframe hello").ConfigureAwait(false);
            TaskCompletionSource<bool> closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            route.OnClose((_, __) => closed.TrySetResult(true));
            await DetachFrameAsync(page, "frame1").ConfigureAwait(false);
            await closed.Task.ConfigureAwait(false);
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should route on context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRouteOnContext()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.RouteWebSocketAsync(new Regex("ws1"), ws =>
            {
                ws.OnMessage(message => ws.Send("page-mock-1"));
            }).ConfigureAwait(false);
            await page.RouteWebSocketAsync(new Regex("ws1"), ws =>
            {
                ws.OnMessage(message => ws.Send("page-mock-2"));
            }).ConfigureAwait(false);
            await page.Context.RouteWebSocketAsync(new Regex(".*"), ws =>
            {
                ws.OnMessage(message => ws.Send("context-mock-1"));
                ws.OnMessage(message => ws.Send("context-mock-2"));
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async ({ host }) => {
                    window.log = [];
                    window.ws1 = new WebSocket('ws://' + host + '/ws1');
                    window.ws1.addEventListener('message', event => window.log.push('ws1:' + event.data));
                    window.ws2 = new WebSocket('ws://' + host + '/ws2');
                    window.ws2.addEventListener('message', event => window.log.push('ws2:' + event.data));
                    await Promise.all([
                        new Promise(f => window.ws1.addEventListener('open', f)),
                        new Promise(f => window.ws2.addEventListener('open', f)),
                    ]);
                }",
                new { host = Host }).ConfigureAwait(false);
            await page.EvaluateAsync("() => window.ws1.send('request')").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).SequenceEqual(new[] { "ws1:page-mock-2" }),
                "page-mock-2").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.ws2.send('request')").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).SequenceEqual(new[] { "ws1:page-mock-2", "ws2:context-mock-2" }),
                "context-mock-2").ConfigureAwait(false);
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should not throw after page closure")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowAfterPageClosure()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IWebSocketRoute> opened = new TaskCompletionSource<IWebSocketRoute>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), async ws =>
            {
                ws.ConnectToServer();
                opened.TrySetResult(ws);
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            IWebSocketRoute route = await opened.Task.ConfigureAwait(false);
            await Task.WhenAll(page.CloseAsync(), Task.Run(() => route.Send("hello"))).ConfigureAwait(false);
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should not throw with empty handler")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWithEmptyHandler()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            await page.RouteWebSocketAsync(new Regex(".*"), _ => { }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).SequenceEqual(new[] { "open" }),
                "open only").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.ws.send('hi')").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.ws.send('hi2')").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).SequenceEqual(new[] { "open" }),
                "still open only").ConfigureAwait(false);
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should throw when connecting twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenConnectingTwice()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<Exception> error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), ws =>
            {
                ws.ConnectToServer();
                try
                {
                    ws.ConnectToServer();
                }
                catch (Exception ex)
                {
                    error.TrySetResult(ex);
                }
            }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            Exception thrown = await error.Task.ConfigureAwait(false);
            Assert.That(thrown.Message, Does.Contain("Already connected to the server"));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with no trailing slash")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNoTrailingSlash()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            List<string> log = new List<string>();
            await page.RouteWebSocketAsync("ws://" + Host, ws =>
            {
                ws.OnMessage(message =>
                {
                    log.Add(message.Text);
                    ws.Send("response");
                });
            }).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"({ host }) => {
                    window.log = [];
                    window.ws = new WebSocket('ws://' + host);
                    window.ws.addEventListener('message', event => window.log.push(event.data));
                }",
                new { host = Host }).ConfigureAwait(false);
            await PollUntilAsync(
                async () => await page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false) == 1,
                "no-slash OPEN").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.ws.send('query')").ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(log.Count == 1 && log[0] == "query"), "query").ConfigureAwait(false);
            Assert.That(await PageLogAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "response" }));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with baseURL")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBaseUrl()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new BrowserContextOptions
            {
                BaseURL = "http://" + Host,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteWebSocketAsync("/ws", ws =>
            {
                ws.OnMessage(message => ws.Send(message.Text));
            }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async () => {
                    await window.wsOpened;
                    window.ws.send('echo');
                }").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "echo").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "message: data=echo origin=" + WsOrigin + " lastEventId=" }));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should work with baseURL regardless of scheme casing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBaseUrlRegardlessOfSchemeCasing()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new BrowserContextOptions
            {
                BaseURL = "HTTP://" + Host,
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.RouteWebSocketAsync("/ws", ws =>
            {
                ws.OnMessage(message => ws.Send(message.Text));
            }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob").ConfigureAwait(false);
            await page.EvaluateAsync(
                @"async () => {
                    await window.wsOpened;
                    window.ws.send('echo');
                }").ConfigureAwait(false);
            await PollUntilAsync(
                async () => (await PageLogAsync(page).ConfigureAwait(false)).Length == 2,
                "echo casing").ConfigureAwait(false);
            Assert.That(
                await PageLogAsync(page).ConfigureAwait(false),
                Is.EqualTo(new[] { "open", "message: data=echo origin=" + WsOrigin + " lastEventId=" }));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should expose protocols to the route handler")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeProtocolsToTheRouteHandler()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            List<IWebSocketRoute> routes = new List<IWebSocketRoute>();
            await page.RouteWebSocketAsync(new Regex(".*"), ws => routes.Add(ws)).ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync(
                @"({ host }) => {
                    window.wsNone = new WebSocket('ws://' + host + '/ws-none');
                    window.wsString = new WebSocket('ws://' + host + '/ws-string', 'chat.v1');
                    window.wsArray = new WebSocket('ws://' + host + '/ws-array', ['chat.v2', 'chat.v1']);
                }",
                new { host = Host }).ConfigureAwait(false);
            await PollUntilAsync(() => Task.FromResult(routes.Count == 3), "3 routes").ConfigureAwait(false);
            Dictionary<string, IWebSocketRoute> byUrl = routes.ToDictionary(
                route => new Uri(route.Url).AbsolutePath,
                route => route,
                StringComparer.Ordinal);
            Assert.That(byUrl["/ws-none"].Protocols, Is.EqualTo(Array.Empty<string>()));
            Assert.That(byUrl["/ws-string"].Protocols, Is.EqualTo(new[] { "chat.v1" }));
            Assert.That(byUrl["/ws-array"].Protocols, Is.EqualTo(new[] { "chat.v2", "chat.v1" }));
        }

        [PlaywrightTest("route-web-socket.spec.ts", "should expose protocols on server-side route")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExposeProtocolsOnServerSideRoute()
        {
            EnsureServer();
            IPage page = await NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<(IWebSocketRoute Page, IWebSocketRoute Server)> opened =
                new TaskCompletionSource<(IWebSocketRoute, IWebSocketRoute)>(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteWebSocketAsync(new Regex(".*"), ws =>
            {
                IWebSocketRoute serverRoute = ws.ConnectToServer();
                opened.TrySetResult((ws, serverRoute));
            }).ConfigureAwait(false);
            await SetupWsAsync(page, "blob", new[] { "chat.v2", "chat.v1" }).ConfigureAwait(false);
            (IWebSocketRoute pageRoute, IWebSocketRoute serverRoute) = await opened.Task.ConfigureAwait(false);
            Assert.That(pageRoute.Protocols, Is.EqualTo(new[] { "chat.v2", "chat.v1" }));
            Assert.That(serverRoute.Protocols, Is.EqualTo(new[] { "chat.v2", "chat.v1" }));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static void Forward(IWebSocketRoute dest, IWebSocketFrame message)
        {
            if (message.Binary != null && message.Binary.Length > 0)
            {
                dest.Send(message.Binary);
                return;
            }

            dest.Send(message.Text ?? string.Empty);
        }

        private static async Task EchoOnceAsync(WebSocket ws)
        {
            if (ws == null)
            {
                return;
            }

            byte[] buffer = new byte[256];
            try
            {
                while (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                {
                    WebSocketReceiveResult result = await ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ws.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes("echo-" + text)),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static Task PollUntilAsync(Func<Task<bool>> ready, string message)
            => PollUntilAsync(ready, () => Task.FromResult(message));

        private static async Task PollUntilAsync(Func<Task<bool>> ready, Func<Task<string>> message)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                if (await ready().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail(await message().ConfigureAwait(false));
        }

        private static async Task<string[]> PageLogAsync(IPage page)
            => await page.EvaluateAsync<string[]>("() => window.log || []").ConfigureAwait(false)
                ?? Array.Empty<string>();

        private static async Task<string[]> FrameLogAsync(IFrame frame)
            => await frame.EvaluateAsync<string[]>("() => window.log || []").ConfigureAwait(false)
                ?? Array.Empty<string>();

        private static async Task AttachMockListenersAsync(IPage page, string wsUrl, string binaryType, object protocols)
        {
            await page.EvaluateAsync(
                @"({ wsUrl, binaryType, protocols }) => {
                    window.log = [];
                    window.ws = protocols === undefined || protocols === null
                        ? new WebSocket(wsUrl)
                        : new WebSocket(wsUrl, protocols);
                    window.ws.binaryType = binaryType;
                    window.ws.addEventListener('open', () => window.log.push('open'));
                    window.ws.addEventListener('close', event => window.log.push('close code=' + event.code + ' reason=' + event.reason + ' wasClean=' + event.wasClean));
                    window.ws.addEventListener('error', () => window.log.push('error'));
                    window.ws.addEventListener('message', async event => {
                        let data;
                        if (typeof event.data === 'string')
                            data = event.data;
                        else if (event.data instanceof Blob)
                            data = 'blob:' + await event.data.text();
                        else
                            data = 'arraybuffer:' + await (new Blob([event.data])).text();
                        window.log.push('message: data=' + data + ' origin=' + event.origin + ' lastEventId=' + event.lastEventId);
                    });
                    window.wsOpened = new Promise(f => window.ws.addEventListener('open', () => f()));
                }",
                new { wsUrl, binaryType, protocols }).ConfigureAwait(false);
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson + "; f.id = " + nameJson + "; f.src = " + urlJson +
                "; document.body.appendChild(f); })()").ConfigureAwait(false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named != null && !named.IsDetached)
                {
                    return named;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail("iframe " + name + " was not attached");
            return null;
        }

        private static async Task DetachFrameAsync(IPage page, string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.getElementById(" + nameJson + "); if (f) f.remove(); })()").ConfigureAwait(false);
        }

        private async Task<IPage> NewPageAsync()
        {
            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            return await context.NewPageAsync().ConfigureAwait(false);
        }

        private static async Task InstallMockAsync(IPage page, string mock)
        {
            if (mock == NoMatch)
            {
                await page.RouteWebSocketAsync(new Regex("zzz"), _ => { }).ConfigureAwait(false);
                return;
            }

            if (mock == PassThrough)
            {
                await page.RouteWebSocketAsync(new Regex(".*"), ws =>
                {
                    IWebSocketRoute server = ws.ConnectToServer();
                    ws.OnMessage(message => Forward(server, message));
                    server.OnMessage(message => Forward(ws, message));
                }).ConfigureAwait(false);
            }
        }

        private async Task SetupWsAsync(IPage page, string binaryType, object protocols = null, bool relativeUrl = false)
        {
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = relativeUrl ? "/ws" : "ws://" + Host + "/ws";
            await AttachMockListenersAsync(page, wsUrl, binaryType, protocols).ConfigureAwait(false);
        }

        private async Task SetupWsAsync(IFrame frame, string binaryType, object protocols = null, bool relativeUrl = false)
        {
            await frame.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = relativeUrl ? "/ws" : "ws://" + Host + "/ws";
            await frame.EvaluateAsync(
                @"({ wsUrl, binaryType, protocols }) => {
                    window.log = [];
                    window.ws = protocols === undefined || protocols === null
                        ? new WebSocket(wsUrl)
                        : new WebSocket(wsUrl, protocols);
                    window.ws.binaryType = binaryType;
                    window.ws.addEventListener('open', () => window.log.push('open'));
                    window.ws.addEventListener('close', event => window.log.push('close code=' + event.code + ' reason=' + event.reason + ' wasClean=' + event.wasClean));
                    window.ws.addEventListener('error', () => window.log.push('error'));
                    window.ws.addEventListener('message', async event => {
                        let data;
                        if (typeof event.data === 'string')
                            data = event.data;
                        else if (event.data instanceof Blob)
                            data = 'blob:' + await event.data.text();
                        else
                            data = 'arraybuffer:' + await (new Blob([event.data])).text();
                        window.log.push('message: data=' + data + ' origin=' + event.origin + ' lastEventId=' + event.lastEventId);
                    });
                    window.wsOpened = new Promise(f => window.ws.addEventListener('open', () => f()));
                }",
                new { wsUrl, binaryType, protocols }).ConfigureAwait(false);
        }

        private async Task DisposeBrowserAsync()
        {
            if (_browser != null)
            {
                try
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _browser = null;
            }
        }
    }
}
