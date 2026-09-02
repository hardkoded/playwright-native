using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace PlaywrightNative.TestServer
{
    public class SimpleServer
    {
        const int MaxMessageSize = 256 * 1024;

        private readonly IDictionary<string, Action<HttpContext>> _subscribers;
        private readonly IDictionary<string, Action<HttpContext>> _requestWaits;
        private readonly IDictionary<string, RequestDelegate> _routes;
        private readonly IDictionary<string, (string username, string password)> _auths;
        private readonly IDictionary<string, string> _csp;
        private readonly IWebHost _webHost;
        private static int counter;
        private readonly Dictionary<int, WebSocket> _clients = new Dictionary<int, WebSocket>();
        private const string WebSocketMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly List<UpgradeConnection> _activeUpgrades = new List<UpgradeConnection>();
        private readonly SemaphoreSlim _webSocketAcceptGate = new SemaphoreSlim(1, 1);
        private Action<WebSocket> _onceWebSocket;
        private Func<WebSocket, Task> _onceWebSocketAsync;
        private string _sendOnWebSocketConnection;
        private TaskCompletionSource<UpgradeConnection> _upgradeWait;
        private TaskCompletionSource<OfficialServerWebSocket> _webSocketWait;
        private TaskCompletionSource<HttpRequest> _webSocketRequestWait;

        internal IList<string> GzipRoutes { get; }

        public event EventHandler<RequestReceivedEventArgs> RequestReceived;

        public static SimpleServer Create(int port, string contentRoot) => new SimpleServer(port, contentRoot, isHttps: false);

        public static SimpleServer CreateHttps(int port, string contentRoot) => new SimpleServer(port, contentRoot, isHttps: true);

        public SimpleServer(int port, string contentRoot, bool isHttps)
        {
            _subscribers = new ConcurrentDictionary<string, Action<HttpContext>>();
            _requestWaits = new ConcurrentDictionary<string, Action<HttpContext>>();
            _routes = new ConcurrentDictionary<string, RequestDelegate>();
            _auths = new ConcurrentDictionary<string, (string username, string password)>();
            _csp = new ConcurrentDictionary<string, string>();
            GzipRoutes = new List<string>();

            _webHost = new WebHostBuilder()
                .ConfigureAppConfiguration((context, builder) => builder
                    .SetBasePath(context.HostingEnvironment.ContentRootPath)
                    .AddEnvironmentVariables()
                )
                .Configure(app => app
#if NETCOREAPP
                    .UseWebSockets()
#endif
                    .UseMiddleware<SimpleCompressionMiddleware>(this)
                    .Use(async (context, next) =>
                    {
                        RequestReceived?.Invoke(this, new RequestReceivedEventArgs { Request = context.Request });

                        if (context.Request.Headers.ContainsKey("Upgrade"))
                        {
                            if (_upgradeWait != null)
                            {
                                TaskCompletionSource<UpgradeConnection> waiter = _upgradeWait;
                                _upgradeWait = null;
                                UpgradeConnection upgrade = new UpgradeConnection(context, this);
                                _activeUpgrades.Add(upgrade);
                                waiter.TrySetResult(upgrade);
                                await upgrade.Completed.ConfigureAwait(false);
                                return;
                            }

                            string pathname = context.Request.Path.Value ?? string.Empty;
                            if (pathname == "/ws-401")
                            {
                                context.Response.StatusCode = 401;
                                await context.Response.WriteAsync("Unauthorized body").ConfigureAwait(false);
                                return;
                            }

                            if (pathname == "/ws-slow")
                            {
                                await Task.Delay(2000).ConfigureAwait(false);
                            }

                            if (pathname == "/ws" || pathname == "/ws-slow")
                            {
                                if (context.WebSockets.IsWebSocketRequest)
                                {
                                    await AcceptAndDispatchWebSocketAsync(context).ConfigureAwait(false);
                                }
                                else if (!context.Response.HasStarted)
                                {
                                    context.Response.StatusCode = 400;
                                }

                                return;
                            }

                            if (!_routes.ContainsKey(pathname)
                                && !_routes.ContainsKey(context.Request.Path.Value ?? string.Empty))
                            {
                                if (!context.Response.HasStarted)
                                {
                                    context.Response.StatusCode = 400;
                                }

                                return;
                            }
                        }

                        if (_auths.TryGetValue(context.Request.Path, out var auth) && !Authenticate(auth.username, auth.password, context))
                        {
                            context.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"Secure Area\"");

                            if (!context.Response.HasStarted)
                            {
                                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            }

                            if (_subscribers.TryGetValue(context.Request.Path, out var unauthorizedSubscriber))
                            {
                                unauthorizedSubscriber(context);
                            }

                            if (TryGetRequestWait(context, out var unauthorizedWait))
                            {
                                unauthorizedWait(context);
                            }

                            await context.Response.WriteAsync("HTTP Error 401 Unauthorized: Access is denied");
                            return;
                        }
                        string requestMethod = context.Request.Method;
                        bool mayHaveBody = context.Request.ContentLength.GetValueOrDefault() > 0
                            || HttpMethods.IsPost(requestMethod)
                            || HttpMethods.IsPut(requestMethod)
                            || HttpMethods.IsPatch(requestMethod);
                        if (mayHaveBody)
                        {
                            context.Request.EnableBuffering();
                            await context.Request.Body.CopyToAsync(Stream.Null).ConfigureAwait(false);
                            if (context.Request.Body.CanSeek)
                            {
                                context.Request.Body.Position = 0;
                            }
                        }

                        if (_subscribers.TryGetValue(context.Request.Path, out var subscriber))
                        {
                            subscriber(context);
                        }
                        if (TryGetRequestWait(context, out var requestWait))
                        {
                            requestWait(context);
                        }
                        string routeKey = (context.Request.Path.Value ?? string.Empty)
                            + (context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty);
                        if (_routes.TryGetValue(routeKey, out var handler)
                            || _routes.TryGetValue(context.Request.Path.Value ?? string.Empty, out handler))
                        {
                            await handler(context);
                            return;
                        }

                        if (
                            context.Request.Path.ToString().Contains("/cached/") &&
                            !string.IsNullOrEmpty(context.Request.Headers["if-modified-since"]) &&
                            !context.Response.HasStarted)
                        {
                            context.Response.StatusCode = StatusCodes.Status304NotModified;
                        }

                        string originalMethod = context.Request.Method;
                        bool serveAsGet = !HttpMethods.IsGet(originalMethod)
                            && !HttpMethods.IsHead(originalMethod);
                        if (serveAsGet)
                        {
                            context.Request.Method = HttpMethods.Get;
                        }

                        try
                        {
                            await next();
                        }
                        finally
                        {
                            if (serveAsGet)
                            {
                                context.Request.Method = originalMethod;
                            }
                        }
                    })
                    .UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(Path.Combine(contentRoot, "wwwroot")),
                        OnPrepareResponse = fileResponseContext =>
                        {
                            if (_csp.TryGetValue(fileResponseContext.Context.Request.Path, out string csp))
                            {
                                fileResponseContext.Context.Response.Headers["Content-Security-Policy"] = csp;
                            }

                            if (fileResponseContext.Context.Request.Path.ToString().EndsWith(".json"))
                            {
                                fileResponseContext.Context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
                            }

                            if (fileResponseContext.Context.Request.Path.ToString().EndsWith(".css"))
                            {
                                fileResponseContext.Context.Response.Headers["Content-Type"] = "text/css; charset=utf-8";
                            }

                            if (fileResponseContext.Context.Request.Path.ToString().EndsWith(".html"))
                            {
                                fileResponseContext.Context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";

                                if (fileResponseContext.Context.Request.Path.ToString().Contains("/cached/"))
                                {
                                    fileResponseContext.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, no-cache";
                                    fileResponseContext.Context.Response.Headers["Last-Modified"] = DateTime.Now.ToString("s");
                                }
                                else
                                {
                                    fileResponseContext.Context.Response.Headers["Cache-Control"] = "no-cache, no-store";
                                }
                            }
                        }
                    })
                    .Use(async (HttpContext context, RequestDelegate next) =>
                    {
                        // Official Playwright testserver writes a 404 body so Chromium
                        // commits the document instead of net::ERR_HTTP_RESPONSE_CODE_FAILURE.
                        if (!context.Response.HasStarted)
                        {
                            context.Response.StatusCode = StatusCodes.Status404NotFound;
                            context.Response.ContentType = "text/plain";
                            await context.Response.WriteAsync("File not found: " + context.Request.Path).ConfigureAwait(false);
                        }
                    }))
                .UseKestrel(options =>
                {
                    options.AllowSynchronousIO = true;
                    options.Limits.MaxRequestBodySize = 512L * 1024 * 1024;
                    if (isHttps)
                    {
                        options.ListenLocalhost(port, listenOptions =>
                        {
                            string certificatePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH");
                            if (!string.IsNullOrEmpty(certificatePath))
                            {
                                string certificatePassword = Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD");
                                listenOptions.UseHttps(Path.GetFullPath(certificatePath), certificatePassword);
                            }
                            else
                            {
                                listenOptions.UseHttps("testCert.cer");
                            }
                        });
                    }
                    else
                    {
                        options.ListenLocalhost(port);
                    }
                })
                .UseContentRoot(contentRoot)
                .Build();
        }

        public void SetAuth(string path, string username, string password) => _auths.Add(path, (username, password));

        public void SetCSP(string path, string csp) => _csp.Add(path, csp);

        public Task StartAsync() => _webHost.StartAsync();

        public async Task StopAsync()
        {
            Reset();

            await _webHost.StopAsync();
        }

        public void Reset()
        {
            _routes.Clear();
            _auths.Clear();
            _csp.Clear();
            _subscribers.Clear();
            _requestWaits.Clear();
            GzipRoutes.Clear();
            _onceWebSocket = null;
            _onceWebSocketAsync = null;
            _sendOnWebSocketConnection = null;
            _upgradeWait?.TrySetCanceled();
            _upgradeWait = null;
            _webSocketWait?.TrySetCanceled();
            _webSocketWait = null;
            _webSocketRequestWait?.TrySetCanceled();
            _webSocketRequestWait = null;
            foreach (UpgradeConnection upgrade in _activeUpgrades)
            {
                upgrade.Destroy();
            }

            _activeUpgrades.Clear();
            foreach (var subscriber in _subscribers.Values)
            {
                subscriber(null);
            }
            _subscribers.Clear();
        }

        public void EnableGzip(string path) => GzipRoutes.Add(path);

        public void OnceWebSocketConnection(Action<WebSocket> handler)
            => _onceWebSocket = handler;

        public void OnceWebSocketConnection(Func<WebSocket, Task> handler)
            => _onceWebSocketAsync = handler;

        /// <summary>
        /// Official <c>server.sendOnWebSocketConnection(data)</c>. Sends
        /// <paramref name="data"/> on every accepted socket that is not claimed
        /// by <see cref="WaitForWebSocketAsync"/> or
        /// <see cref="OnceWebSocketConnection(Action{WebSocket})"/>.
        /// </summary>
        /// <param name="data">Text payload, typically <c>incoming</c>.</param>
        public void SendOnWebSocketConnection(string data)
            => _sendOnWebSocketConnection = data;

        public Task<UpgradeConnection> WaitForUpgradeAsync()
        {
            _upgradeWait = new TaskCompletionSource<UpgradeConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _upgradeWait.Task;
        }

        public Task<OfficialServerWebSocket> WaitForWebSocketAsync()
        {
            _webSocketWait = new TaskCompletionSource<OfficialServerWebSocket>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _webSocketWait.Task;
        }

        internal void NotifyWebSocket(OfficialServerWebSocket socket)
        {
            TaskCompletionSource<OfficialServerWebSocket> waiter = _webSocketWait;
            _webSocketWait = null;
            waiter?.TrySetResult(socket);
        }

        internal async Task AcceptAndDispatchWebSocketAsync(HttpContext context)
        {
            await _webSocketAcceptGate.WaitAsync().ConfigureAwait(false);
            WebSocket webSocket;
            Stream raw;
            Action<WebSocket> once;
            Func<WebSocket, Task> onceAsync;
            bool waiting;
            try
            {
                TaskCompletionSource<HttpRequest> requestWaiter = _webSocketRequestWait;
                _webSocketRequestWait = null;
                requestWaiter?.TrySetResult(context.Request);
                waiting = _webSocketWait != null;
                (webSocket, raw) = await UpgradeToWebSocketAsync(context).ConfigureAwait(false);
                NotifyWebSocket(new OfficialServerWebSocket(webSocket, raw));
                once = _onceWebSocket;
                onceAsync = _onceWebSocketAsync;
                _onceWebSocket = null;
                _onceWebSocketAsync = null;
                if (once != null)
                {
                    once(webSocket);
                }
            }
            finally
            {
                _webSocketAcceptGate.Release();
            }

            if (onceAsync != null)
            {
                await onceAsync(webSocket).ConfigureAwait(false);
                if (webSocket.State == WebSocketState.Open)
                {
                    await ReceiveLoopAsync(webSocket, sendCloseMessage: false, CancellationToken.None).ConfigureAwait(false);
                }

                return;
            }

            if (once != null)
            {
                await WaitUntilDisconnectedAsync(webSocket).ConfigureAwait(false);
                return;
            }

            if (waiting)
            {
                await WaitUntilDisconnectedAsync(webSocket).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrEmpty(_sendOnWebSocketConnection))
            {
                await webSocket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(_sendOnWebSocketConnection)),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None).ConfigureAwait(false);
            }

            await ReceiveLoopAsync(
                webSocket,
                context.Request.Headers["User-Agent"].ToString().Contains("Firefox"),
                CancellationToken.None).ConfigureAwait(false);
        }

        internal async Task<(WebSocket Socket, Stream Stream)> UpgradeToWebSocketAsync(HttpContext context)
        {
            string subProtocol = FirstRequestedProtocol(context);
            IHttpUpgradeFeature upgrade = context.Features.Get<IHttpUpgradeFeature>();
            if (upgrade != null && upgrade.IsUpgradableRequest)
            {
                string key = context.Request.Headers["Sec-WebSocket-Key"];
                context.Response.Headers["Connection"] = "Upgrade";
                context.Response.Headers["Upgrade"] = "websocket";
                context.Response.Headers["Sec-WebSocket-Accept"] = ComputeAcceptKey(key);
                if (!string.IsNullOrEmpty(subProtocol))
                {
                    context.Response.Headers["Sec-WebSocket-Protocol"] = subProtocol;
                }

                Stream stream = await upgrade.UpgradeAsync().ConfigureAwait(false);
                WebSocket socket = WebSocket.CreateFromStream(
                    stream,
                    isServer: true,
                    string.IsNullOrEmpty(subProtocol) ? null : subProtocol,
                    TimeSpan.FromSeconds(30));
                return (socket, stream);
            }

            WebSocket accepted = string.IsNullOrEmpty(subProtocol)
                ? await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false)
                : await context.WebSockets.AcceptWebSocketAsync(subProtocol).ConfigureAwait(false);
            return (accepted, null);
        }

        internal static string FirstRequestedProtocol(HttpContext context)
        {
            if (context?.WebSockets?.WebSocketRequestedProtocols != null)
            {
                return context.WebSockets.WebSocketRequestedProtocols.FirstOrDefault();
            }

            string header = context?.Request.Headers["Sec-WebSocket-Protocol"];
            if (string.IsNullOrEmpty(header))
            {
                return null;
            }

            return header.Split(',')[0].Trim();
        }

        internal static string ComputeAcceptKey(string secWebSocketKey)
        {
            byte[] hash = SHA1.HashData(Encoding.ASCII.GetBytes((secWebSocketKey ?? string.Empty) + WebSocketMagic));
            return Convert.ToBase64String(hash);
        }

        public void SetRoute(string path, RequestDelegate handler) => _routes[path] = handler;

        public void SetRedirect(string from, string to) => SetRoute(from, context =>
        {
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers["Location"] = to;
            return Task.CompletedTask;
        });

        public void Subscribe(string path, Action<HttpContext> action)
            => _subscribers.Add(path, action);

        public async Task<T> WaitForRequest<T>(string path, Func<HttpRequest, T> selector)
        {
            var taskCompletion = new TaskCompletionSource<T>();
            _requestWaits[path] = context =>
            {
                taskCompletion.SetResult(selector(context.Request));
            };

            var request = await taskCompletion.Task;
            _requestWaits.Remove(path);

            return request;
        }

        public Task WaitForRequest(string path) => WaitForRequest(path, _ => true);

        /// <summary>
        /// Official <c>server.waitForWebSocketConnectionRequest()</c>.
        /// Completes with the HTTP upgrade request for <c>/ws</c>.
        /// </summary>
        /// <returns>The handshake <see cref="HttpRequest"/>.</returns>
        public Task<HttpRequest> WaitForWebSocketConnectionRequest()
        {
            _webSocketRequestWait = new TaskCompletionSource<HttpRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _webSocketRequestWait.Task;
        }

        private bool TryGetRequestWait(HttpContext context, out Action<HttpContext> waiter)
        {
            string path = context.Request.Path.Value ?? string.Empty;
            string pathAndQuery = path
                + (context.Request.QueryString.HasValue ? context.Request.QueryString.Value.ToString() : string.Empty);
            if (_requestWaits.TryGetValue(pathAndQuery, out waiter)
                || _requestWaits.TryGetValue(path, out waiter))
            {
                return true;
            }

            waiter = null;
            return false;
        }

        private static bool Authenticate(string username, string password, HttpContext context)
        {
            string authHeader = context.Request.Headers["Authorization"];
            if (authHeader != null && authHeader.StartsWith("Basic", StringComparison.Ordinal))
            {
                string encodedUsernamePassword = authHeader.Substring("Basic ".Length).Trim();
                var encoding = Encoding.GetEncoding("iso-8859-1");
                string auth = encoding.GetString(Convert.FromBase64String(encodedUsernamePassword));

                return auth == $"{username}:{password}";
            }
            return false;
        }

        private async Task ReceiveLoopAsync(WebSocket webSocket, bool sendCloseMessage, CancellationToken token)
        {
            int connectionId = NextConnectionId();
            _clients.Add(connectionId, webSocket);

            byte[] buffer = new byte[MaxMessageSize];

            try
            {
                while (true)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (sendCloseMessage)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None);
                        }
                        break;
                    }

                    var data = await ReadFrames(result, webSocket, buffer, token);

                    if (data.Count == 0)
                    {
                        break;
                    }
                }
            }
            finally
            {
                _clients.Remove(connectionId);
            }
        }

        private async Task<ArraySegment<byte>> ReadFrames(WebSocketReceiveResult result, WebSocket webSocket, byte[] buffer, CancellationToken token)
        {
            int count = result.Count;

            while (!result.EndOfMessage)
            {
                if (count >= MaxMessageSize)
                {
                    string closeMessage = string.Format("Maximum message size: {0} bytes.", MaxMessageSize);
                    await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                    return new ArraySegment<byte>();
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, count, MaxMessageSize - count), CancellationToken.None);
                count += result.Count;

            }
            return new ArraySegment<byte>(buffer, 0, count);
        }


        private static async Task WaitUntilDisconnectedAsync(WebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open
                || webSocket.State == WebSocketState.Connecting
                || webSocket.State == WebSocketState.CloseReceived
                || webSocket.State == WebSocketState.CloseSent)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        private static int NextConnectionId()
        {
            int id = Interlocked.Increment(ref counter);

            if (id == int.MaxValue)
            {
                throw new Exception("connection id limit reached: " + id);
            }

            return id;
        }

        /// <summary>
        /// Raw HTTP upgrade captured by <see cref="WaitForUpgradeAsync"/>.
        /// </summary>
        public sealed class UpgradeConnection
        {
            private readonly HttpContext _context;
            private readonly SimpleServer _server;
            private readonly TaskCompletionSource<bool> _done =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private OfficialServerWebSocket _socket;

            internal UpgradeConnection(HttpContext context, SimpleServer server)
            {
                _context = context;
                _server = server;
            }

            internal Task Completed => _done.Task;

            /// <summary>
            /// Official <c>socket</c> after <see cref="DoUpgradeAsync"/>, or this
            /// connection before the handshake.
            /// </summary>
            public OfficialServerWebSocket Socket => _socket;

            /// <summary>
            /// Completes the WebSocket handshake (official <c>doUpgrade</c>).
            /// </summary>
            /// <returns>A task that completes when the 101 handshake is written.</returns>
            public async Task DoUpgradeAsync()
            {
                if (_socket != null)
                {
                    return;
                }

                if (_context.WebSockets.IsWebSocketRequest
                    || _context.Features.Get<IHttpUpgradeFeature>()?.IsUpgradableRequest == true)
                {
                    (WebSocket webSocket, Stream stream) = await _server.UpgradeToWebSocketAsync(_context)
                        .ConfigureAwait(false);
                    _socket = new OfficialServerWebSocket(webSocket, stream);
                    _server.NotifyWebSocket(_socket);
                    return;
                }

                await _server.AcceptAndDispatchWebSocketAsync(_context).ConfigureAwait(false);
                _done.TrySetResult(true);
            }

            /// <summary>
            /// Writes an HTTP response status line and headers, then finishes the response.
            /// </summary>
            /// <param name="raw">A raw HTTP/1.1 response, including the status line.</param>
            /// <returns>A task that completes when the response has been written.</returns>
            public async Task WriteAsync(string raw)
            {
                string[] lines = (raw ?? string.Empty).Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length > 0)
                {
                    string[] parts = lines[0].Split(new[] { ' ' }, 3, StringSplitOptions.None);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int status))
                    {
                        _context.Response.StatusCode = status;
                    }
                }

                _context.Response.Headers.ContentLength = 0;
                _context.Response.Headers["Connection"] = "close";
                await _context.Response.CompleteAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// Writes raw bytes on the upgraded stream (official <c>socket.write</c>).
            /// </summary>
            /// <param name="data">Raw payload.</param>
            public Task WriteRawAsync(string data)
            {
                if (_socket != null)
                {
                    return _socket.WriteRawAsync(data);
                }

                try
                {
                    _context.Abort();
                }
                catch (ObjectDisposedException)
                {
                }

                return Task.CompletedTask;
            }

            /// <summary>
            /// Finishes the intercepted upgrade after the HTTP response has been written.
            /// Official <c>socket.destroy()</c>.
            /// </summary>
            public void Destroy()
            {
                try
                {
                    _socket?.Destroy();
                    _context.Abort();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (IOException)
                {
                }

                _done.TrySetResult(true);
            }
        }
    }
}
