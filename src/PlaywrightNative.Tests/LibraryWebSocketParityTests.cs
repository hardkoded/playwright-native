/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/web-socket.spec.ts</c> parity. Do not edit leftover
    /// <c>PageWebSocketTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryWebSocketParityTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        private static string Host => new Uri(TestConstants.ServerUrl).Authority;

        [TearDown]
        public void ClearWebSocketOnceHandlers()
        {
            Server?.OnceWebSocketConnection((Action<WebSocket>)null);
            Server?.OnceWebSocketConnection((Func<WebSocket, Task>)null);
        }

        [PlaywrightTest("web-socket.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>(
                @"host => {
                    let cb;
                    const result = new Promise(f => cb = f);
                    const ws = new WebSocket('ws://' + host + '/ws');
                    ws.addEventListener('message', data => { ws.close(); cb(data.data); });
                    return result;
                }",
                Host).ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("incoming"));
        }

        [PlaywrightTest("web-socket.spec.ts", "should emit close events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitCloseEvents()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> socketClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<string> log = new List<string>();
            IWebSocket webSocket = null;
            page.WebSocket += (_, ws) =>
            {
                log.Add("open<" + ws.Url + ">");
                webSocket = ws;
                ws.Close += (_, _) =>
                {
                    log.Add("close");
                    socketClosed.TrySetResult(true);
                };
            };
            await page.EvaluateAsync<object>(
                "host => { const ws = new WebSocket('ws://' + host + '/ws'); ws.addEventListener('open', () => ws.close()); }",
                Host).ConfigureAwait(false);
            await socketClosed.Task.ConfigureAwait(false);
            Assert.That(string.Join(":", log), Is.EqualTo("open<" + webSocket.Url + ">:close"));
            Assert.That(webSocket.IsClosed, Is.True);
        }

        [PlaywrightTest("web-socket.spec.ts", "should emit frame events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitFrameEvents()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(EchoIncomingAsync);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> socketClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<string> log = new List<string>();
            page.WebSocket += (_, ws) =>
            {
                log.Add("open");
                ws.FrameSent += (_, frame) => log.Add("sent<" + frame.Text + ">");
                ws.FrameReceived += (_, frame) => log.Add("received<" + frame.Text + ">");
                ws.Close += (_, _) =>
                {
                    log.Add("close");
                    socketClosed.TrySetResult(true);
                };
            };
            await page.EvaluateAsync<object>(
                @"host => {
                    const ws = new WebSocket('ws://' + host + '/ws');
                    ws.addEventListener('open', () => ws.send('outgoing'));
                    ws.addEventListener('message', () => { ws.close(); });
                    window.ws = ws;
                }",
                Host).ConfigureAwait(false);
            await socketClosed.Task.ConfigureAwait(false);
            Assert.That(log, Is.EqualTo(new[] { "open", "sent<outgoing>", "received<incoming>", "close" }));
        }

        [PlaywrightTest("web-socket.spec.ts", "should filter out the close events when the server closes with a message")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterOutTheCloseEventsWhenTheServerClosesWithAMessage()
        {
            EnsureServer();
            Server.OnceWebSocketConnection(CloseAfterOutgoingAsync);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> socketClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<string> log = new List<string>();
            page.WebSocket += (_, ws) =>
            {
                log.Add("open");
                ws.FrameSent += (_, frame) => log.Add("sent<" + frame.Text + ">");
                ws.FrameReceived += (_, frame) => log.Add("received<" + frame.Text + ">");
                ws.Close += (_, _) =>
                {
                    log.Add("close");
                    socketClosed.TrySetResult(true);
                };
            };
            await page.EvaluateAsync<object>(
                @"host => {
                    const ws = new WebSocket('ws://' + host + '/ws');
                    ws.addEventListener('message', () => ws.send('outgoing'));
                    window.ws = ws;
                }",
                Host).ConfigureAwait(false);
            await socketClosed.Task.ConfigureAwait(false);
            Assert.That(log, Is.EqualTo(new[] { "open", "received<incoming>", "sent<outgoing>", "close" }));
        }

        [PlaywrightTest("web-socket.spec.ts", "should pass self as argument to close event")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassSelfAsArgumentToCloseEvent()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<IWebSocket> socketClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            IWebSocket webSocket = null;
            page.WebSocket += (_, ws) =>
            {
                webSocket = ws;
                ws.Close += (_, closed) => socketClosed.TrySetResult(closed);
            };
            await page.EvaluateAsync<object>(
                "host => { const ws = new WebSocket('ws://' + host + '/ws'); ws.addEventListener('open', () => ws.close()); }",
                Host).ConfigureAwait(false);
            IWebSocket eventArg = await socketClosed.Task.ConfigureAwait(false);
            Assert.That(eventArg, Is.SameAs(webSocket));
        }

        [PlaywrightTest("web-socket.spec.ts", "should emit binary frame events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitBinaryFrameEvents()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<bool> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<object> sent = new List<object>();
            page.WebSocket += (_, ws) =>
            {
                ws.Close += (_, _) => done.TrySetResult(true);
                ws.FrameSent += (_, frame) =>
                {
                    if (frame.Binary != null && frame.Binary.Length > 0 && string.IsNullOrEmpty(frame.Text))
                    {
                        sent.Add(frame.Binary);
                    }
                    else
                    {
                        sent.Add(frame.Text);
                    }
                };
            };
            await page.EvaluateAsync<object>(
                @"host => {
                    const ws = new WebSocket('ws://' + host + '/ws');
                    ws.addEventListener('open', () => {
                        const binary = new Uint8Array(5);
                        for (let i = 0; i < 5; ++i)
                            binary[i] = i;
                        ws.send('text');
                        ws.send(binary);
                        ws.close();
                    });
                }",
                Host).ConfigureAwait(false);
            await done.Task.ConfigureAwait(false);
            Assert.That(sent[0], Is.EqualTo("text"));
            byte[] binary = (byte[])sent[1];
            for (int i = 0; i < 5; i++)
            {
                Assert.That(binary[i], Is.EqualTo(i));
            }
        }

        [PlaywrightTest("web-socket.spec.ts", "should emit error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitError()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            TaskCompletionSource<string> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.WebSocket += (_, ws) =>
            {
                ws.SocketError += (_, message) => result.TrySetResult(message);
            };
            await page.EvaluateAsync<object>(
                "host => { new WebSocket('ws://' + host + '/bogus-ws'); }",
                Host).ConfigureAwait(false);
            string message = await result.Task.ConfigureAwait(false);
            Assert.That(message, Does.Contain(": 400"));
        }

        [PlaywrightTest("web-socket.spec.ts", "should not have stray error events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHaveStrayErrorEvents()
        {
            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string error = null;
            page.WebSocket += (_, ws) =>
            {
                ws.SocketError += (_, message) => error = message;
            };
            Task<IWebSocket> socketTask = WaitForSocketAndIncomingFrameAsync(page);
            Task evalTask = page.EvaluateAsync<object>(
                "host => { window.ws = new WebSocket('ws://' + host + '/ws'); }",
                Host);
            await Task.WhenAll(socketTask, evalTask).ConfigureAwait(false);
            await page.EvaluateAsync("window.ws.close()").ConfigureAwait(false);
            Assert.That(error, Is.Null);
        }

        [PlaywrightTest("web-socket.spec.ts", "should reject waitForEvent on socket close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectWaitForEventOnSocketClose()
        {
            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWebSocket> socketTask = WaitForSocketAndIncomingFrameAsync(page);
            Task evalTask = page.EvaluateAsync<object>(
                "host => { window.ws = new WebSocket('ws://' + host + '/ws'); }",
                Host);
            await Task.WhenAll(socketTask, evalTask).ConfigureAwait(false);
            IWebSocket socket = await socketTask.ConfigureAwait(false);
            Task<object> errorTask = socket.WaitForEventAsync("framesent").ContinueWith(
                t => t.Exception?.GetBaseException() ?? (object)t.Result,
                TaskScheduler.Default);
            await page.EvaluateAsync("window.ws.close()").ConfigureAwait(false);
            object error = await errorTask.ConfigureAwait(false);
            Assert.That(error, Is.InstanceOf<Exception>());
            Assert.That(((Exception)error).Message, Does.Contain("Socket closed"));
        }

        [PlaywrightTest("web-socket.spec.ts", "should reject waitForEvent on page close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectWaitForEventOnPageClose()
        {
            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IWebSocket> socketTask = WaitForSocketAndIncomingFrameAsync(page);
            Task evalTask = page.EvaluateAsync<object>(
                "host => { window.ws = new WebSocket('ws://' + host + '/ws'); }",
                Host);
            await Task.WhenAll(socketTask, evalTask).ConfigureAwait(false);
            IWebSocket socket = await socketTask.ConfigureAwait(false);
            Task<object> errorTask = socket.WaitForEventAsync("framesent").ContinueWith(
                t => t.Exception?.GetBaseException() ?? (object)t.Result,
                TaskScheduler.Default);
            await page.CloseAsync().ConfigureAwait(false);
            object error = await errorTask.ConfigureAwait(false);
            Assert.That(error, Is.InstanceOf<Exception>());
            Assert.That(((Exception)error).Message, Does.Contain(DriverMessages.BrowserOrContextClosedExceptionMessage));
        }

        [PlaywrightTest("web-socket.spec.ts", "should not tear down the page when a WebSocket is opened inside a worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotTearDownThePageWhenAWebSocketIsOpenedInsideAWorker()
        {
            EnsureServer();
            Server.SendOnWebSocketConnection("incoming");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string received = await page.EvaluateAsync<string>(
                @"host => {
                    const code = `
                        const ws = new WebSocket(${JSON.stringify('ws://' + host + '/ws')});
                        ws.addEventListener('message', event => self.postMessage(event.data));
                    `;
                    const worker = new Worker(URL.createObjectURL(new Blob([code], { type: 'text/javascript' })));
                    return new Promise(resolve => worker.addEventListener('message', event => resolve(event.data)));
                }",
                Host).ConfigureAwait(false);
            Assert.That(received, Is.EqualTo("incoming"));
            Assert.That(await page.EvaluateAsync<int>("() => 1 + 1").ConfigureAwait(false), Is.EqualTo(2));
        }

        [PlaywrightTest("web-socket.spec.ts", "should turn off when offline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldTurnOffWhenOffline()
        {
            Assert.Ignore("Official it.fixme().");
        }

        [PlaywrightTest("web-socket.spec.ts", "should send extra HTTP headers on WebSocket handshake")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendExtraHttpHeadersOnWebSocketHandshake()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (TestConstants.IsChromium && ChromiumMajor(browser) < 151)
            {
                Assert.Ignore("Official fixme: Chromium before 151 does not send extra HTTP headers on WebSocket handshake.");
                return;
            }

            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string> { ["foo"] = "bar" }).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Task<string> requestTask = WaitForWebSocketHeaderAsync("foo");
            await page.EvaluateAsync<object>(
                "host => { new WebSocket('ws://' + host + '/ws'); }",
                Host).ConfigureAwait(false);
            Assert.That(await requestTask.ConfigureAwait(false), Is.EqualTo("bar"));
        }

        [PlaywrightTest("web-socket.spec.ts", "should send extra HTTP headers on WebSocket handshake from a worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendExtraHttpHeadersOnWebSocketHandshakeFromAWorker()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (TestConstants.IsChromium && ChromiumMajor(browser) < 151)
            {
                Assert.Ignore("Official fixme: Chromium before 151 does not send extra HTTP headers on WebSocket handshake.");
                return;
            }

            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string> { ["foo"] = "bar" }).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Task<string> requestTask = WaitForWebSocketHeaderAsync("foo");
            await page.EvaluateAsync<object>(
                @"host => {
                    const code = `new WebSocket(${JSON.stringify('ws://' + host + '/ws')});`;
                    new Worker(URL.createObjectURL(new Blob([code], { type: 'text/javascript' })));
                }",
                Host).ConfigureAwait(false);
            Assert.That(await requestTask.ConfigureAwait(false), Is.EqualTo("bar"));
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static Task<string> WaitForWebSocketHeaderAsync(string name)
        {
            TaskCompletionSource<string> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<RequestReceivedEventArgs> handler = null;
            handler = (_, args) =>
            {
                if (string.Equals(args.Request.Path, "/ws", StringComparison.Ordinal))
                {
                    tcs.TrySetResult(args.Request.Headers[name].ToString());
                    Server.RequestReceived -= handler;
                }
            };
            Server.RequestReceived += handler;
            return tcs.Task;
        }

        private static async Task<IWebSocket> WaitForSocketAndIncomingFrameAsync(IPage page)
        {
            IWebSocket socket = await page.WaitForEventAsync(PageEvent.WebSocket).ConfigureAwait(false);
            await socket.WaitForEventAsync(WebSocketEvent.FrameReceived).ConfigureAwait(false);
            return socket;
        }

        private static int ChromiumMajor(IBrowser browser)
        {
            string version = browser?.Version ?? string.Empty;
            int dot = version.IndexOf('.');
            if (dot <= 0)
            {
                return 0;
            }

            return int.TryParse(version.Substring(0, dot), out int major) ? major : 0;
        }

        private static async Task EchoIncomingAsync(WebSocket ws)
        {
            byte[] buffer = new byte[64 * 1024];
            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await ws.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                await ws.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes("incoming")),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static async Task CloseAfterOutgoingAsync(WebSocket ws)
        {
            await ws.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("incoming")),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None).ConfigureAwait(false);
            byte[] buffer = new byte[4096];
            if (ws.State == WebSocketState.Open)
            {
                await ws.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            }

            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.InvalidPayloadData,
                    "closed by Playwright test-server",
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
