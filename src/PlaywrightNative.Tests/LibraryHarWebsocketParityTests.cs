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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    /// Official <c>library/har-websocket.spec.ts</c> parity.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryHarWebsocketParityTests : PageTestEx
    {
        private const int ClockSkewMs = 100;

        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;
        private static int ServerPort = TestConstants.Port;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19880;
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
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
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
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
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
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HAR_NO_WEBSOCKET_FRAMES", null);
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("har-websocket.spec.ts", "should only have one websocket entry")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOnlyHaveOneWebsocketEntry()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(CloseAfterPingServerAsync);
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = WsUrl("/ws");
            await CloseAfterPingAsync(session.Page, wsUrl).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            List<JsonElement> wsEntries = WebSocketEntries(log, wsUrl);
            Assert.That(wsEntries.Count, Is.EqualTo(1));
            Assert.That(wsEntries[0].GetProperty("_resourceType").GetString(), Is.EqualTo("websocket"));
        }

        [PlaywrightTest("har-websocket.spec.ts", "should include websocket handshake headers and status")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeWebsocketHandshakeHeadersAndStatus()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(CloseAfterPingServerAsync);
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            long beforeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string wsUrl = WsUrl("/ws");
            await CloseAfterPingAsync(session.Page, wsUrl).ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            long afterMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            JsonElement wsEntry = FindEntry(log, wsUrl);
            Assert.That(wsEntry.GetProperty("_resourceType").GetString(), Is.EqualTo("websocket"));
            Assert.That(
                wsEntry.GetProperty("request").GetProperty("headersSize").GetInt32(),
                Is.EqualTo(RequestHeadersSize(wsEntry.GetProperty("request").GetProperty("headers"))));
            Assert.That(wsEntry.GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(101));
            Assert.That(wsEntry.GetProperty("response").GetProperty("statusText").GetString(), Is.EqualTo("Switching Protocols"));
            Assert.That(
                wsEntry.GetProperty("response").GetProperty("headersSize").GetInt32(),
                Is.EqualTo(ResponseHeadersSize(wsEntry.GetProperty("response").GetProperty("headers"))));

            long wallTimeMs = DateTimeOffset.Parse(
                wsEntry.GetProperty("startedDateTime").GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).ToUnixTimeMilliseconds();
            Assert.That(wallTimeMs, Is.GreaterThanOrEqualTo(beforeMs - ClockSkewMs));
            Assert.That(wallTimeMs, Is.LessThanOrEqualTo(afterMs + ClockSkewMs));

            List<string> requestNames = HeaderNames(wsEntry.GetProperty("request").GetProperty("headers"));
            Assert.That(requestNames, Does.Contain("upgrade"));
            Assert.That(requestNames, Does.Contain("connection"));
            Assert.That(requestNames, Does.Contain("sec-websocket-key"));
            Assert.That(requestNames, Does.Contain("sec-websocket-version"));
            Assert.That(FindHeader(wsEntry.GetProperty("request").GetProperty("headers"), "upgrade")?.ToLowerInvariant(), Is.EqualTo("websocket"));

            List<string> responseNames = HeaderNames(wsEntry.GetProperty("response").GetProperty("headers"));
            Assert.That(responseNames, Does.Contain("upgrade"));
            Assert.That(responseNames, Does.Contain("connection"));
            Assert.That(responseNames, Does.Contain("sec-websocket-accept"));
        }

        [PlaywrightTest("har-websocket.spec.ts", "should embed websocket messages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldEmbedWebsocketMessages()
            => TestWebSocketMessagesAsync(HarContentPolicy.Embed);

        [PlaywrightTest("har-websocket.spec.ts", "should attach websocket messages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldAttachWebsocketMessages()
            => TestWebSocketMessagesAsync(HarContentPolicy.Attach);

        [PlaywrightTest("har-websocket.spec.ts", "should attach websocket messages for a still open websocket after stopping")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAttachWebsocketMessagesForAStillOpenWebsocketAfterStopping()
        {
            EnsureServer();
            string incomingText = "incoming";
            byte[] incomingBinary = { 0x01, 0x02, 0x03, 0x04 };
            string outgoingText = "outgoing";
            byte[] outgoingBinary = { 0x05, 0x06, 0x07, 0x08 };
            Server.OnceWebSocketConnection(async ws =>
            {
                int count = 0;
                await EchoIncomingAsync(ws, incomingText, incomingBinary, () => ++count).ConfigureAwait(false);
            });

            await using HarSession session = await PageWithHarAsync("test.har.zip", HarContentPolicy.Attach).ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            long beforeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string wsUrl = WsUrl("/ws");
            TaskCompletionSource<bool> framesDone =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int sent = 0;
            int received = 0;
            void OnCreated(object sender, IWebSocket socket)
            {
                socket.FrameSent += (_, _) =>
                {
                    if (Interlocked.Increment(ref sent) >= 2 && Volatile.Read(ref received) >= 2)
                    {
                        framesDone.TrySetResult(true);
                    }
                };
                socket.FrameReceived += (_, _) =>
                {
                    if (Interlocked.Increment(ref received) >= 2 && Volatile.Read(ref sent) >= 2)
                    {
                        framesDone.TrySetResult(true);
                    }
                };
            }

            session.Page.WebSocket += OnCreated;
            Task evaluatePromise = session.Page.EvaluateAsync(
                @"({ url, outgoingText, outgoingBinary }) => {
                    const ws = new WebSocket(url);
                    window.ws = ws;
                    let count = 0;
                    ws.addEventListener('open', () => ws.send(outgoingText));
                    ws.addEventListener('message', () => {
                        if (++count < 2)
                            ws.send(new Uint8Array(outgoingBinary));
                    });
                }",
                new { url = wsUrl, outgoingText, outgoingBinary });
            try
            {
                await evaluatePromise.ConfigureAwait(false);
                await framesDone.Task.ConfigureAwait(false);
            }
            finally
            {
                session.Page.WebSocket -= OnCreated;
            }
            long afterMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(await session.Page.EvaluateAsync<int>("() => window.ws.readyState").ConfigureAwait(false), Is.EqualTo(1));

            IReadOnlyDictionary<string, byte[]> zip = await session.GetZipAsync().ConfigureAwait(false);
            JsonElement log = LogFromZip(zip);
            JsonElement wsEntry = FindEntry(log, wsUrl);
            Assert.That(
                wsEntry.GetProperty("response").GetProperty("_transferSize").GetInt32(),
                Is.EqualTo(ResponseHeadersSize(wsEntry.GetProperty("response").GetProperty("headers")) + MessageSize(incomingText) + MessageSize(incomingBinary)));
            Assert.That(wsEntry.GetProperty("time").GetDouble(), Is.LessThanOrEqualTo(afterMs - beforeMs));
            Assert.That(wsEntry.TryGetProperty("_webSocketMessages", out _), Is.False);
            string file = wsEntry.GetProperty("response").GetProperty("content").GetProperty("_file").GetString();
            Assert.That(file, Does.Match("^[0-9a-f]+\\.jsonl$"));
            List<JsonElement> messages = ReadJsonl(zip[file]);
            AssertMessages(
                messages,
                new ExpectedMessage("send", 1, outgoingText),
                new ExpectedMessage("receive", 1, incomingText),
                new ExpectedMessage("send", 2, outgoingBinary),
                new ExpectedMessage("receive", 2, incomingBinary));
            AssertMessageTimes(messages, beforeMs, afterMs);
            Assert.That(MessageTime(messages[0]), Is.LessThanOrEqualTo(MessageTime(messages[1])));
            Assert.That(
                wsEntry.GetProperty("time").GetDouble(),
                Is.GreaterThanOrEqualTo(MessageTime(messages[messages.Count - 1]) - MessageTime(messages[0])));
        }

        [PlaywrightTest("har-websocket.spec.ts", "should omit websocket messages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldOmitWebsocketMessages()
            => TestWebSocketMessagesAsync(HarContentPolicy.Omit);

        [PlaywrightTest("har-websocket.spec.ts", "should record websocket connection failure")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordWebsocketConnectionFailure()
        {
            EnsureServer();
            int port;
            TcpListener reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();

            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = "ws://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/ws-connect-fail";
            Task<IWebSocket> wsPromise = session.Page.WaitForEventAsync(PageEvent.WebSocket);
            await session.Page.EvaluateAsync(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('close', () => resolve());
                    ws.addEventListener('error', () => resolve());
                })",
                wsUrl).ConfigureAwait(false);
            await wsPromise.ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement wsEntry = FindEntry(log, wsUrl);
            Assert.That(wsEntry.GetProperty("_resourceType").GetString(), Is.EqualTo("websocket"));
            Assert.That(wsEntry.GetProperty("response").GetProperty("_failureText").GetString(), Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("har-websocket.spec.ts", "should record websocket handshake failure")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordWebsocketHandshakeFailure()
        {
            EnsureServer();
            Server.SetRoute("/ws-handshake-fail", async context =>
            {
                context.Response.StatusCode = 403;
                context.Response.Headers["Connection"] = "close";
                await context.Response.CompleteAsync().ConfigureAwait(false);
            });
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = WsUrl("/ws-handshake-fail");
            Task<SimpleServer.UpgradeConnection> upgradePromise = Server.WaitForUpgradeAsync();
            Task wsClose = session.Page.EvaluateAsync(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('close', () => resolve());
                    ws.addEventListener('error', () => resolve());
                })",
                wsUrl);
            SimpleServer.UpgradeConnection socket = await upgradePromise.ConfigureAwait(false);
            await socket.WriteAsync("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
            socket.Destroy();
            await wsClose.ConfigureAwait(false);
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            JsonElement wsEntry = FindEntry(log, wsUrl);
            Assert.That(wsEntry.GetProperty("_resourceType").GetString(), Is.EqualTo("websocket"));
            if (!PlaywrightTestAttribute.IsChromium)
            {
                Assert.That(wsEntry.GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(403));
                Assert.That(wsEntry.GetProperty("response").GetProperty("statusText").GetString(), Is.EqualTo("Forbidden"));
            }

            Assert.That(wsEntry.GetProperty("response").GetProperty("_failureText").GetString(), Is.Not.Null.And.Not.Empty);
        }

        [PlaywrightTest("har-websocket.spec.ts", "should still capture websocket when route passes messages through")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStillCaptureWebsocketWhenRoutePassesMessagesThrough()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(ws => EchoTextAsync(ws, "incoming"));
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            int routeHandlerCalled = 0;
            await session.Page.RouteWebSocketAsync(new Regex("/ws$"), ws =>
            {
                routeHandlerCalled++;
                IWebSocketRoute serverRoute = ws.ConnectToServer();
                ws.OnMessage(message => serverRoute.Send(message.Text));
                serverRoute.OnMessage(message => ws.Send(message.Text));
            }).ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = WsUrl("/ws");
            string[] messages = await session.Page.EvaluateAsync<string[]>(
                @"url => new Promise(resolve => {
                    const seen = [];
                    const ws = new WebSocket(url);
                    ws.addEventListener('open', () => ws.send('outgoing'));
                    ws.addEventListener('message', event => {
                        seen.push(event.data);
                        ws.close();
                    });
                    ws.addEventListener('close', () => resolve(seen));
                })",
                wsUrl).ConfigureAwait(false);
            Assert.That(routeHandlerCalled, Is.EqualTo(1));
            Assert.That(messages, Is.EqualTo(new[] { "incoming" }));
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            List<JsonElement> wsEntries = WebSocketEntries(log, wsUrl);
            Assert.That(wsEntries.Count, Is.EqualTo(1));
            Assert.That(wsEntries[0].GetProperty("_resourceType").GetString(), Is.EqualTo("websocket"));
            Assert.That(wsEntries[0].GetProperty("response").GetProperty("status").GetInt32(), Is.EqualTo(101));
            AssertMessagePairs(
                wsEntries[0].GetProperty("_webSocketMessages"),
                ("send", "outgoing"),
                ("receive", "incoming"));
        }

        [PlaywrightTest("har-websocket.spec.ts", "should still allow routeWebSocket to fully mock the connection when capturing HAR")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStillAllowRouteWebSocketToFullyMockTheConnectionWhenCapturingHar()
        {
            EnsureServer();
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            int routeHandlerCalled = 0;
            await session.Page.RouteWebSocketAsync(new Regex("/ws$"), ws =>
            {
                routeHandlerCalled++;
                ws.OnMessage(message =>
                {
                    if (message.Text == "ping")
                    {
                        ws.Send("pong");
                    }
                });
            }).ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = WsUrl("/ws");
            string[] messages = await session.Page.EvaluateAsync<string[]>(
                @"url => new Promise(resolve => {
                    const seen = [];
                    const ws = new WebSocket(url);
                    ws.addEventListener('open', () => ws.send('ping'));
                    ws.addEventListener('message', event => {
                        seen.push(event.data);
                        ws.close();
                    });
                    ws.addEventListener('close', () => resolve(seen));
                })",
                wsUrl).ConfigureAwait(false);
            Assert.That(routeHandlerCalled, Is.EqualTo(1));
            Assert.That(messages, Is.EqualTo(new[] { "pong" }));
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            Assert.That(WebSocketEntries(log, wsUrl), Is.Empty);
        }

        [PlaywrightTest("har-websocket.spec.ts", "should still allow routeWebSocket to modify messages when capturing HAR")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStillAllowRouteWebSocketToModifyMessagesWhenCapturingHar()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(ws => EchoPrefixedAsync(ws, "server-saw-"));
            await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
            int routeHandlerCalled = 0;
            await session.Page.RouteWebSocketAsync(new Regex("/ws$"), ws =>
            {
                routeHandlerCalled++;
                IWebSocketRoute serverRoute = ws.ConnectToServer();
                ws.OnMessage(message => serverRoute.Send("modified-" + message.Text));
                serverRoute.OnMessage(message => ws.Send("page-got-" + message.Text));
            }).ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            string wsUrl = WsUrl("/ws");
            string[] messages = await session.Page.EvaluateAsync<string[]>(
                @"url => new Promise(resolve => {
                    const seen = [];
                    const ws = new WebSocket(url);
                    ws.addEventListener('open', () => ws.send('hello'));
                    ws.addEventListener('message', event => {
                        seen.push(event.data);
                        ws.close();
                    });
                    ws.addEventListener('close', () => resolve(seen));
                })",
                wsUrl).ConfigureAwait(false);
            Assert.That(routeHandlerCalled, Is.EqualTo(1));
            Assert.That(messages, Is.EqualTo(new[] { "page-got-server-saw-modified-hello" }));
            JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
            List<JsonElement> wsEntries = WebSocketEntries(log, wsUrl);
            Assert.That(wsEntries.Count, Is.EqualTo(1));
            AssertMessagePairs(
                wsEntries[0].GetProperty("_webSocketMessages"),
                ("send", "modified-hello"),
                ("receive", "server-saw-modified-hello"));
        }

        [PlaywrightTest("har-websocket.spec.ts", "should respect PLAYWRIGHT_HAR_NO_WEBSOCKET_FRAMES")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectPlaywrightHarNoWebsocketFrames()
        {
            EnsureServer();
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HAR_NO_WEBSOCKET_FRAMES", "1");
            try
            {
                Server.OnceWebSocketConnection(CloseAfterPingServerAsync);
                await using HarSession session = await PageWithHarAsync().ConfigureAwait(false);
                await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
                string wsUrl = WsUrl("/ws");
                await CloseAfterPingAsync(session.Page, wsUrl).ConfigureAwait(false);
                JsonElement log = await session.GetLogAsync().ConfigureAwait(false);
                JsonElement wsEntry = FindEntry(log, wsUrl);
                Assert.That(wsEntry.GetProperty("_resourceType").GetString(), Is.EqualTo("websocket"));
                Assert.That(wsEntry.TryGetProperty("_webSocketMessages", out _), Is.False);
                Assert.That(
                    wsEntry.GetProperty("response").GetProperty("content").TryGetProperty("_file", out _),
                    Is.False);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_HAR_NO_WEBSOCKET_FRAMES", null);
            }
        }

        private async Task TestWebSocketMessagesAsync(HarContentPolicy content)
        {
            EnsureServer();
            string[] incomingText = { new string('x', 125), new string('x', 126), new string('x', 65536) };
            int[][] incomingBinary =
            {
                Enumerable.Repeat(0x01, 125).ToArray(),
                Enumerable.Repeat(0x01, 126).ToArray(),
                Enumerable.Repeat(0x01, 65536).ToArray(),
            };
            string[] outgoingText = { new string('y', 125), new string('y', 126), new string('y', 65536) };
            int[][] outgoingBinary =
            {
                Enumerable.Repeat(0x02, 125).ToArray(),
                Enumerable.Repeat(0x02, 126).ToArray(),
                Enumerable.Repeat(0x02, 65536).ToArray(),
            };
            int incomingCount = incomingText.Length + incomingBinary.Length;
            const int delayMs = 100;

            Server.OnceWebSocketConnection(async ws =>
            {
                foreach (string text in incomingText)
                {
                    await ws.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)),
                        System.Net.WebSockets.WebSocketMessageType.Text,
                        true,
                        default).ConfigureAwait(false);
                    await Task.Delay(delayMs).ConfigureAwait(false);
                }

                foreach (int[] binary in incomingBinary)
                {
                    await Task.Delay(delayMs).ConfigureAwait(false);
                    await ws.SendAsync(
                        new ArraySegment<byte>(binary.Select(value => (byte)value).ToArray()),
                        System.Net.WebSockets.WebSocketMessageType.Binary,
                        true,
                        default).ConfigureAwait(false);
                }
            });

            string outputName = content == HarContentPolicy.Embed ? "test.har" : "test.har.zip";
            await using HarSession session = await PageWithHarAsync(outputName, content).ConfigureAwait(false);
            await session.Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            long beforeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string wsUrl = WsUrl("/ws");
            await session.Page.EvaluateAsync(
                @"({ url, incomingCount, outgoingText, outgoingBinary, delayMs }) => new Promise(resolve => {
                    let count = 0;
                    const ws = new WebSocket(url);
                    ws.addEventListener('message', async () => {
                        if (++count < incomingCount)
                            return;
                        for (const text of outgoingText) {
                            ws.send(text);
                            await new Promise(done => setTimeout(done, delayMs));
                        }
                        for (const binary of outgoingBinary) {
                            await new Promise(done => setTimeout(done, delayMs));
                            ws.send(new Uint8Array(binary));
                        }
                        ws.close();
                    });
                    ws.addEventListener('close', () => resolve());
                })",
                new { url = wsUrl, incomingCount, outgoingText, outgoingBinary, delayMs }).ConfigureAwait(false);
            long afterMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            JsonElement log;
            IReadOnlyDictionary<string, byte[]> zip = null;
            if (content != HarContentPolicy.Embed)
            {
                zip = await session.GetZipAsync().ConfigureAwait(false);
                log = LogFromZip(zip);
            }
            else
            {
                log = await session.GetLogAsync().ConfigureAwait(false);
            }

            JsonElement wsEntry = FindEntry(log, wsUrl);
            int expectedTransfer = ResponseHeadersSize(wsEntry.GetProperty("response").GetProperty("headers"));
            foreach (string text in incomingText)
            {
                expectedTransfer += MessageSize(text);
            }

            foreach (int[] binary in incomingBinary)
            {
                expectedTransfer += MessageSize(binary.Select(value => (byte)value).ToArray());
            }

            Assert.That(wsEntry.GetProperty("response").GetProperty("_transferSize").GetInt32(), Is.EqualTo(expectedTransfer));
            Assert.That(wsEntry.GetProperty("time").GetDouble(), Is.LessThanOrEqualTo(afterMs - beforeMs));

            if (content == HarContentPolicy.Omit)
            {
                Assert.That(
                    wsEntry.GetProperty("response").GetProperty("content").TryGetProperty("_file", out _),
                    Is.False);
                return;
            }

            List<JsonElement> messages;
            if (content == HarContentPolicy.Attach)
            {
                Assert.That(wsEntry.TryGetProperty("_webSocketMessages", out _), Is.False);
                string file = wsEntry.GetProperty("response").GetProperty("content").GetProperty("_file").GetString();
                Assert.That(file, Does.Match("^[0-9a-f]+\\.jsonl$"));
                messages = ReadJsonl(zip[file]);
            }
            else
            {
                messages = wsEntry.GetProperty("_webSocketMessages").EnumerateArray().ToList();
            }

            List<ExpectedMessage> expected = new List<ExpectedMessage>();
            expected.AddRange(incomingText.Select(text => new ExpectedMessage("receive", 1, text)));
            expected.AddRange(incomingBinary.Select(bytes => new ExpectedMessage("receive", 2, bytes.Select(value => (byte)value).ToArray())));
            expected.AddRange(outgoingText.Select(text => new ExpectedMessage("send", 1, text)));
            expected.AddRange(outgoingBinary.Select(bytes => new ExpectedMessage("send", 2, bytes.Select(value => (byte)value).ToArray())));
            AssertMessages(messages, expected.ToArray());
            AssertMessageTimes(messages, beforeMs, afterMs);
            Assert.That(MessageTime(messages[0]), Is.LessThanOrEqualTo(MessageTime(messages[1])));
            Assert.That(
                wsEntry.GetProperty("time").GetDouble(),
                Is.GreaterThanOrEqualTo(MessageTime(messages[messages.Count - 1]) - MessageTime(messages[0])));
        }

        private async Task<HarSession> PageWithHarAsync(
            string outputName = "test.har",
            HarContentPolicy content = default)
        {
            string harPath = TempHarPath(Path.GetFileNameWithoutExtension(outputName), Path.GetExtension(outputName));
            IBrowserContext context = await _browser.NewContextAsync(new() { RecordHarPath = harPath, RecordHarContent = content, IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            return new HarSession(context, page, harPath);
        }

        private static async Task CloseAfterPingServerAsync(System.Net.WebSockets.WebSocket ws)
        {
            byte[] buffer = new byte[256];
            try
            {
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token).ConfigureAwait(false);
                if (ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    await ws.CloseOutputAsync(
                        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                        "Close",
                        default).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }

            if (ws.State == System.Net.WebSockets.WebSocketState.Open
                || ws.State == System.Net.WebSockets.WebSocketState.CloseSent)
            {
                try
                {
                    ws.Abort();
                }
                catch (Exception)
                {
                }
            }
        }

        private static async Task CloseAfterPingAsync(IPage page, string wsUrl)
        {
            await page.EvaluateAsync(
                @"url => new Promise(resolve => {
                    const ws = new WebSocket(url);
                    ws.addEventListener('open', () => {
                        ws.send('ping');
                        ws.close();
                    });
                    ws.addEventListener('close', () => resolve());
                    ws.addEventListener('error', () => resolve());
                })",
                wsUrl).ConfigureAwait(false);
        }

        private static async Task EchoIncomingAsync(
            System.Net.WebSockets.WebSocket ws,
            string incomingText,
            byte[] incomingBinary,
            Func<int> next)
        {
            byte[] buffer = new byte[256];
            try
            {
                while (ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token).ConfigureAwait(false);
                    int count = next();
                    if (count < 2)
                    {
                        await ws.SendAsync(
                            new ArraySegment<byte>(Encoding.UTF8.GetBytes(incomingText)),
                            System.Net.WebSockets.WebSocketMessageType.Text,
                            true,
                            default).ConfigureAwait(false);
                    }
                    else
                    {
                        await ws.SendAsync(
                            new ArraySegment<byte>(incomingBinary),
                            System.Net.WebSockets.WebSocketMessageType.Binary,
                            true,
                            default).ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static async Task EchoTextAsync(System.Net.WebSockets.WebSocket ws, string reply)
        {
            byte[] buffer = new byte[256];
            try
            {
                await ws.ReceiveAsync(new ArraySegment<byte>(buffer), default).ConfigureAwait(false);
                await ws.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(reply)),
                    System.Net.WebSockets.WebSocketMessageType.Text,
                    true,
                    default).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static async Task EchoPrefixedAsync(System.Net.WebSockets.WebSocket ws, string prefix)
        {
            byte[] buffer = new byte[1024];
            try
            {
                System.Net.WebSockets.WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), default).ConfigureAwait(false);
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ws.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(prefix + message)),
                    System.Net.WebSockets.WebSocketMessageType.Text,
                    true,
                    default).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static string WsUrl(string path)
            => "ws://localhost:" + ServerPort.ToString(CultureInfo.InvariantCulture) + path;

        private static List<JsonElement> WebSocketEntries(JsonElement log, string wsUrl)
        {
            List<JsonElement> matches = new List<JsonElement>();
            foreach (JsonElement entry in log.GetProperty("entries").EnumerateArray())
            {
                string url = entry.GetProperty("request").GetProperty("url").GetString();
                if (string.Equals(url, wsUrl, StringComparison.Ordinal)
                    || (url != null && url.EndsWith("://localhost:" + ServerPort.ToString(CultureInfo.InvariantCulture) + "/ws", StringComparison.Ordinal)))
                {
                    matches.Add(entry);
                }
            }

            return matches;
        }

        private static JsonElement FindEntry(JsonElement log, string wsUrl)
        {
            List<JsonElement> matches = WebSocketEntries(log, wsUrl);
            Assert.That(matches, Is.Not.Empty, "missing websocket HAR entry for " + wsUrl);
            return matches[0];
        }

        private static int RequestHeadersSize(JsonElement headers)
            => "GET /ws HTTP/1.1\r\n".Length + HeadersSize(headers);

        private static int ResponseHeadersSize(JsonElement headers)
            => "HTTP/1.1 101 Switching Protocols\r\n".Length + HeadersSize(headers) + "\r\n".Length;

        private static int HeadersSize(JsonElement headers)
        {
            int result = 0;
            foreach (JsonElement header in headers.EnumerateArray())
            {
                result += (header.GetProperty("name").GetString() ?? string.Empty).Length
                    + 2
                    + (header.GetProperty("value").GetString() ?? string.Empty).Length
                    + 2;
            }

            return result;
        }

        private static int MessageSize(string message)
            => MessageSize(Encoding.UTF8.GetByteCount(message ?? string.Empty));

        private static int MessageSize(byte[] message)
            => MessageSize(message == null ? 0 : message.Length);

        private static int MessageSize(int length)
        {
            if (length <= 125)
            {
                return 6 + length;
            }

            if (length < 65536)
            {
                return 8 + length;
            }

            return 14 + length;
        }

        private static List<string> HeaderNames(JsonElement headers)
        {
            List<string> names = new List<string>();
            foreach (JsonElement header in headers.EnumerateArray())
            {
                names.Add((header.GetProperty("name").GetString() ?? string.Empty).ToLowerInvariant());
            }

            return names;
        }

        private static string FindHeader(JsonElement headers, string name)
        {
            foreach (JsonElement header in headers.EnumerateArray())
            {
                if (string.Equals(header.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.GetProperty("value").GetString();
                }
            }

            return null;
        }

        private static List<JsonElement> ReadJsonl(byte[] bytes)
        {
            List<JsonElement> messages = new List<JsonElement>();
            foreach (string line in Encoding.UTF8.GetString(bytes).Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(line);
                messages.Add(document.RootElement.Clone());
            }

            return messages;
        }

        private static JsonElement LogFromZip(IReadOnlyDictionary<string, byte[]> zip)
        {
            using JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetString(zip["har.har"]));
            return document.RootElement.GetProperty("log").Clone();
        }

        private static void AssertMessages(IReadOnlyList<JsonElement> actual, params ExpectedMessage[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].GetProperty("type").GetString(), Is.EqualTo(expected[i].Type));
                Assert.That(actual[i].GetProperty("opcode").GetInt32(), Is.EqualTo(expected[i].Opcode));
                if (expected[i].Opcode == 1)
                {
                    Assert.That(actual[i].GetProperty("data").GetString(), Is.EqualTo(expected[i].Text));
                }
                else
                {
                    byte[] bytes = Convert.FromBase64String(actual[i].GetProperty("data").GetString() ?? string.Empty);
                    Assert.That(bytes, Is.EqualTo(expected[i].Binary));
                }
            }
        }

        private static void AssertMessagePairs(JsonElement messages, params (string Type, string Data)[] expected)
        {
            Assert.That(messages.GetArrayLength(), Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(messages[i].GetProperty("type").GetString(), Is.EqualTo(expected[i].Type));
                Assert.That(messages[i].GetProperty("data").GetString(), Is.EqualTo(expected[i].Data));
            }
        }

        private static void AssertMessageTimes(IReadOnlyList<JsonElement> messages, long beforeMs, long afterMs)
        {
            foreach (JsonElement message in messages)
            {
                double time = MessageTime(message);
                Assert.That(time, Is.GreaterThanOrEqualTo(beforeMs - ClockSkewMs));
                Assert.That(time, Is.LessThanOrEqualTo(afterMs + ClockSkewMs));
            }
        }

        private static double MessageTime(JsonElement message)
            => message.GetProperty("time").GetDouble();

        private static JsonElement ReadLog(string harPath)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(harPath));
            return document.RootElement.GetProperty("log").Clone();
        }

        private static string TempHarPath(string prefix, string extension = ".har")
        {
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".har";
            }

            if (!extension.StartsWith('.'))
            {
                extension = "." + extension;
            }

            return Path.Combine(
                Path.GetTempPath(),
                "pwsharp-wave880-" + prefix + "-" + Guid.NewGuid().ToString("N") + extension);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
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

        private sealed class HarSession : IAsyncDisposable
        {
            private readonly string _harPath;
            private bool _closed;

            internal HarSession(IBrowserContext context, IPage page, string harPath)
            {
                Context = context;
                Page = page;
                _harPath = harPath;
            }

            internal IBrowserContext Context { get; }

            internal IPage Page { get; }

            internal async Task<JsonElement> GetLogAsync()
            {
                await CloseAsync().ConfigureAwait(false);
                return ReadLog(_harPath);
            }

            internal async Task<IReadOnlyDictionary<string, byte[]>> GetZipAsync()
            {
                await CloseAsync().ConfigureAwait(false);
                Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                using ZipArchive archive = ZipFile.OpenRead(_harPath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    using Stream stream = entry.Open();
                    using MemoryStream memory = new MemoryStream();
                    stream.CopyTo(memory);
                    files[entry.FullName.Replace('\\', '/')] = memory.ToArray();
                }

                return files;
            }

            private async Task CloseAsync()
            {
                if (!_closed)
                {
                    await Context.CloseAsync().ConfigureAwait(false);
                    _closed = true;
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (!_closed)
                {
                    try
                    {
                        await Context.CloseAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    _closed = true;
                }

                TryDelete(_harPath);
            }
        }

        private sealed class ExpectedMessage
        {
            internal ExpectedMessage(string type, int opcode, string text)
            {
                Type = type;
                Opcode = opcode;
                Text = text;
            }

            internal ExpectedMessage(string type, int opcode, byte[] binary)
            {
                Type = type;
                Opcode = opcode;
                Binary = binary;
            }

            internal string Type { get; }

            internal int Opcode { get; }

            internal string Text { get; }

            internal byte[] Binary { get; }
        }
    }
}
