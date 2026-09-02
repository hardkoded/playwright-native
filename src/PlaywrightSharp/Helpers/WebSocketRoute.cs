/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official page-side <c>WebSocketRoute</c>: mock or
    /// <see cref="ConnectToServer"/> native bridge.
    /// </summary>
    internal sealed partial class WebSocketRoute : IWebSocketRoute, IDisposable
    {
        private readonly IPage _page;
        private readonly string _id;
        private readonly object _lock = new object();
        private readonly Queue<(string Data, bool Binary)> _earlyPage = new Queue<(string Data, bool Binary)>();
        private readonly Queue<(string Data, bool Binary)> _earlyServer = new Queue<(string Data, bool Binary)>();
        private readonly List<Task> _pendingDispatch = new List<Task>();
        private Action<IWebSocketFrame> _onMessage;
        private Action<int?, string> _onClose;
        private ServerRoute _server;
        private IFrame _frame;
        private Task _dispatchTail = Task.CompletedTask;
        private bool _connected;
        private bool _closed;
        private bool _subscribed;

        internal WebSocketRoute(IPage page, string id, string url, IReadOnlyList<string> protocols = null, bool createdInMainFrame = true, IFrame frame = null)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _id = id ?? throw new ArgumentNullException(nameof(id));
            Url = url ?? string.Empty;
            Protocols = protocols ?? Array.Empty<string>();
            CreatedInMainFrame = createdInMainFrame;
            _frame = frame;
            Subscribe();
        }

        /// <inheritdoc/>
        public string Url { get; }

        /// <inheritdoc/>
        public IReadOnlyList<string> Protocols { get; }

        internal bool CreatedInMainFrame { get; }

        /// <inheritdoc/>
        public void OnMessage(Action<IWebSocketFrame> handler)
        {
            lock (_lock)
            {
                _onMessage = handler;
            }

            FlushEarlyPage();
        }

        /// <inheritdoc/>
        public void OnClose(Action<int?, string> handler)
        {
            lock (_lock)
            {
                _onClose = handler;
            }
        }

        /// <inheritdoc/>
        public void Send(string message)
            => Dispatch(new Dictionary<string, object>
            {
                ["type"] = "sendToPage",
                ["id"] = _id,
                ["data"] = new Dictionary<string, object>
                {
                    ["data"] = message ?? string.Empty,
                    ["isBase64"] = false,
                },
            });

        /// <inheritdoc/>
        public void Send(byte[] message)
        {
            byte[] payload = message ?? Array.Empty<byte>();
            Dispatch(new Dictionary<string, object>
            {
                ["type"] = "sendToPage",
                ["id"] = _id,
                ["data"] = new Dictionary<string, object>
                {
                    ["data"] = Convert.ToBase64String(payload),
                    ["isBase64"] = true,
                },
            });
        }

        /// <inheritdoc/>
        public async Task CloseAsync(int? code = null, string reason = null)
        {
            await FlushPendingAsync().ConfigureAwait(false);
            await DispatchAsync(new Dictionary<string, object>
            {
                ["type"] = "closePage",
                ["id"] = _id,
                ["code"] = code ?? 1000,
                ["reason"] = reason ?? string.Empty,
                ["wasClean"] = true,
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Unsubscribe();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public IWebSocketRoute ConnectToServer()
        {
            lock (_lock)
            {
                if (_connected)
                {
                    throw new PlaywrightSharpException("Already connected to the server");
                }

                _connected = true;
                _server = new ServerRoute(this);
            }

            Dispatch(new Dictionary<string, object>
            {
                ["type"] = "connect",
                ["id"] = _id,
            });
            return _server;
        }

        internal Task FlushAfterHandlerAsync()
        {
            bool connected;
            lock (_lock)
            {
                connected = _connected;
            }

            if (!connected)
            {
                return DispatchAsync(new Dictionary<string, object>
                {
                    ["type"] = "ensureOpened",
                    ["id"] = _id,
                });
            }

            FlushEarlyPage();
            _server?.FlushEarly();
            return Task.CompletedTask;
        }

        internal void ReceiveFromPage(string data, bool binary)
        {
            Action<IWebSocketFrame> handler;
            ServerRoute server;
            lock (_lock)
            {
                handler = _onMessage;
                server = _server;
                if (handler == null && server == null)
                {
                    _earlyPage.Enqueue((data, binary));
                    return;
                }
            }

            IWebSocketFrame frame = ToFrame(data, binary);
            if (handler != null)
            {
                handler(frame);
                return;
            }

            server?.ForwardFromPage(frame);
        }

        internal void ReceiveFromServer(string data, bool binary)
        {
            if (_server == null)
            {
                lock (_lock)
                {
                    _earlyServer.Enqueue((data, binary));
                }

                return;
            }

            _server.ReceiveFromServer(data, binary);
        }

        internal void ClosedFromPage(int? code, string reason)
        {
            Action<int?, string> handler;
            ServerRoute server;
            bool alreadyClosed;
            lock (_lock)
            {
                alreadyClosed = _closed;
                _closed = true;
                handler = _onClose;
                server = _server;
            }

            if (alreadyClosed)
            {
                return;
            }

            handler?.Invoke(code, reason);
            if (server != null)
            {
                server.CloseFromPage(code, reason);
                return;
            }

            _ = ClosePageAsync(code ?? 1000, reason, wasClean: true);
        }

        internal void ClosedFromServer(int? code, string reason)
            => ClosedFromServer(code, reason, wasClean: code != 1006);

        internal void ClosedFromServer(int? code, string reason, bool wasClean)
        {
            if (_server != null)
            {
                _server.ClosedFromServer(code, reason, wasClean);
                return;
            }

            _ = ClosePageAsync(code, reason, wasClean);
        }

        internal Task ClosePageAsync(int? code, string reason, bool wasClean)
            => DispatchAsync(new Dictionary<string, object>
            {
                ["type"] = "closePage",
                ["id"] = _id,
                ["code"] = code ?? 1000,
                ["reason"] = reason ?? string.Empty,
                ["wasClean"] = wasClean,
            });

        internal void NotifyExecutionContextGone()
        {
            Action<int?, string> pageClose;
            ServerRoute server;
            lock (_lock)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                pageClose = _onClose;
                server = _server;
            }

            pageClose?.Invoke(null, null);
            server?.ClosedFromServer(null, null);
        }

        internal void FlushEarlyServer()
        {
            _server?.FlushEarly();
        }

        /// <summary>
        /// Official route delivery is asynchronous on the protocol, so the creating
        /// evaluate can attach <c>addEventListener</c> before <c>connectToServer</c>
        /// or <c>send</c> run.
        /// </summary>
        internal Task WaitUntilPageReadyAsync()
            => Task.Delay(1);

        /// <summary>
        /// Official dispatcher evaluates in the creating frame. Resolve it after the
        /// binding returns so we do not deadlock inside <c>exposeFunction</c>.
        /// </summary>
        internal async Task ResolveFrameAsync()
        {
            if (_frame != null && !_frame.IsDetached)
            {
                return;
            }

            IPage page = _page;
            if (page == null)
            {
                return;
            }

            foreach (IFrame candidate in page.Frames)
            {
                if (candidate == null || candidate.IsDetached)
                {
                    continue;
                }

                try
                {
                    bool has = await candidate.EvaluateAsync<bool>(
                        "(id) => typeof globalThis.__pwWebSocketHas === 'function' && globalThis.__pwWebSocketHas(id)",
                        _id).ConfigureAwait(false);
                    if (has)
                    {
                        _frame = candidate;
                        return;
                    }
                }
                catch (PlaywrightSharpException)
                {
                }
            }

            _frame = CreatedInMainFrame ? page.MainFrame : _frame;
        }

        private static IWebSocketFrame ToFrame(string data, bool binary)
        {
            if (binary)
            {
                return new WebSocketFrame(string.Empty, DecodeBinary(data));
            }

            return new WebSocketFrame(data ?? string.Empty, Array.Empty<byte>());
        }

        private static byte[] DecodeBinary(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return Array.Empty<byte>();
            }

            try
            {
                return Convert.FromBase64String(data);
            }
            catch (FormatException)
            {
                return Encoding.UTF8.GetBytes(data);
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _page == null)
            {
                return;
            }

            _subscribed = true;
            _page.FrameNavigated += OnFrameNavigated;
            _page.FrameDetached += OnFrameDetached;
            _page.Close += OnPageGone;
            _page.Crash += OnPageGone;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _page == null)
            {
                return;
            }

            _subscribed = false;
            _page.FrameNavigated -= OnFrameNavigated;
            _page.FrameDetached -= OnFrameDetached;
            _page.Close -= OnPageGone;
            _page.Crash -= OnPageGone;
        }

        private void OnFrameNavigated(object sender, IFrame frame)
        {
            if (IsCreatingFrame(frame))
            {
                NotifyExecutionContextGone();
            }
        }

        private void OnFrameDetached(object sender, IFrame frame)
        {
            if (IsCreatingFrame(frame))
            {
                NotifyExecutionContextGone();
            }
        }

        private bool IsCreatingFrame(IFrame frame)
        {
            if (frame == null)
            {
                return false;
            }

            if (_frame != null)
            {
                return ReferenceEquals(frame, _frame);
            }

            return CreatedInMainFrame
                ? frame.ParentFrame == null
                : frame.ParentFrame != null;
        }

        private void OnPageGone(object sender, IPage page)
            => NotifyExecutionContextGone();

        private void FlushEarlyPage()
        {
            List<(string Data, bool Binary)> pending;
            Action<IWebSocketFrame> handler;
            ServerRoute server;
            lock (_lock)
            {
                if (_earlyPage.Count == 0 || (_onMessage == null && _server == null))
                {
                    return;
                }

                pending = new List<(string Data, bool Binary)>(_earlyPage);
                _earlyPage.Clear();
                handler = _onMessage;
                server = _server;
            }

            foreach ((string data, bool binary) in pending)
            {
                IWebSocketFrame frame = ToFrame(data, binary);
                if (handler != null)
                {
                    handler(frame);
                }
                else
                {
                    server?.ForwardFromPage(frame);
                }
            }
        }

        private void Dispatch(Dictionary<string, object> request)
        {
            _ = WebSocketRouter.EnqueueDispatchAsync(_page, request);
            Task task;
            lock (_lock)
            {
                Task previous = _dispatchTail;
                task = ContinueDispatchAsync(previous, request);
                _dispatchTail = task;
                _pendingDispatch.Add(task);
            }
        }

        private async Task ContinueDispatchAsync(Task previous, Dictionary<string, object> request)
        {
            if (previous != null)
            {
                try
                {
                    await previous.ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            _ = WebSocketRouter.EvaluateDispatchAsync(_page, request);
            FlushEarlyPage();
            _server?.FlushEarly();
        }

        private async Task FlushPendingAsync()
        {
            Task[] pending;
            lock (_lock)
            {
                pending = _pendingDispatch.ToArray();
                _pendingDispatch.Clear();
            }

            if (pending.Length > 0)
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
        }

        private Task DispatchAsync(Dictionary<string, object> request)
        {
            Dispatch(request);
            return Task.CompletedTask;
        }

        private void DispatchServer(string type, string data, bool binary, int? code = null, string reason = null)
        {
            Dictionary<string, object> request = new Dictionary<string, object>
            {
                ["type"] = type,
                ["id"] = _id,
            };
            if (data != null)
            {
                request["data"] = new Dictionary<string, object>
                {
                    ["data"] = data,
                    ["isBase64"] = binary,
                };
            }

            if (code.HasValue)
            {
                request["code"] = code.Value;
                request["reason"] = reason ?? string.Empty;
                request["wasClean"] = true;
            }

            Dispatch(request);
        }

        private sealed class ServerRoute : IWebSocketRoute
        {
            private readonly WebSocketRoute _owner;
            private readonly object _lock = new object();
            private Action<IWebSocketFrame> _onMessage;
            private Action<int?, string> _onClose;

            internal ServerRoute(WebSocketRoute owner)
            {
                _owner = owner;
            }

            public string Url => _owner.Url;

            public IReadOnlyList<string> Protocols => _owner.Protocols;

            public void OnMessage(Action<IWebSocketFrame> handler)
            {
                lock (_lock)
                {
                    _onMessage = handler;
                }

                FlushEarly();
            }

            public void OnClose(Action<int?, string> handler)
            {
                lock (_lock)
                {
                    _onClose = handler;
                }
            }

            public void Send(string message)
                => _owner.DispatchServer("sendToServer", message ?? string.Empty, binary: false);

            public void Send(byte[] message)
                => _owner.DispatchServer("sendToServer", Convert.ToBase64String(message ?? Array.Empty<byte>()), binary: true);

            public IWebSocketRoute ConnectToServer()
                => throw new PlaywrightSharpException("connectToServer must be called on the page-side WebSocketRoute");

            public Task CloseAsync(int? code = null, string reason = null)
            {
                _owner.DispatchServer("closeServer", null, binary: false, code ?? 1000, reason ?? string.Empty);
                return Task.CompletedTask;
            }

            public Task CloseAsync(Microsoft.Playwright.WebSocketRouteCloseOptions options)
            {
                int? code = options?.Code;
                string reason = options?.Reason;
                return CloseAsync(code, reason);
            }

            internal void ForwardFromPage(IWebSocketFrame frame)
            {
                if (frame == null)
                {
                    return;
                }

                if (frame.Binary != null && frame.Binary.Length > 0)
                {
                    Send(frame.Binary);
                    return;
                }

                Send(frame.Text ?? string.Empty);
            }

            internal void ReceiveFromServer(string data, bool binary)
            {
                Action<IWebSocketFrame> handler;
                lock (_lock)
                {
                    handler = _onMessage;
                }

                IWebSocketFrame frame = ToFrame(data, binary);
                if (handler != null)
                {
                    handler(frame);
                    return;
                }

                if (binary)
                {
                    _owner.Send(DecodeBinary(data));
                    return;
                }

                _owner.Send(data ?? string.Empty);
            }

            internal void CloseFromPage(int? code, string reason)
                => _owner.DispatchServer("closeServer", null, binary: false, code, reason);

            internal void ClosedFromServer(int? code, string reason)
                => ClosedFromServer(code, reason, wasClean: code != 1006);

            internal void ClosedFromServer(int? code, string reason, bool wasClean)
            {
                Action<int?, string> handler;
                lock (_lock)
                {
                    handler = _onClose;
                }

                if (handler != null)
                {
                    handler(code, reason);
                    return;
                }

                _ = _owner.ClosePageAsync(code, reason, wasClean);
            }

            internal void FlushEarly()
            {
                List<(string Data, bool Binary)> pending;
                lock (_owner._lock)
                {
                    if (_owner._earlyServer.Count == 0)
                    {
                        return;
                    }

                    pending = new List<(string Data, bool Binary)>(_owner._earlyServer);
                    _owner._earlyServer.Clear();
                }

                foreach ((string data, bool binary) in pending)
                {
                    ReceiveFromServer(data, binary);
                }
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IWebSocketRoute.CloseAsync(WebSocketRouteCloseOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
