/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>tests/config/proxy.ts</c> TestProxy: records absolute-form
    /// request URLs / CONNECT hosts and forwards to a local test server.
    /// Dual-stack so IPv6 <c>[::1]:port</c> works.
    /// </summary>
    internal sealed class OfficialTestProxy : IAsyncDisposable
    {
        private static readonly HashSet<string> ConnectHostsToIgnore = new(StringComparer.OrdinalIgnoreCase)
        {
            "www.bing.com:443",
            "www.google.com:443",
        };

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<string> _requestUrls = new();
        private readonly List<string> _requestHosts = new();
        private readonly List<string> _connectHosts = new();
        private readonly List<string> _wsUrls = new();
        private readonly Task _acceptLoop;
        private readonly object _lock = new();
        private int _forwardPort;
        private bool _allowConnect;
        private string _removePrefix;
        private Func<OfficialTestProxyAuthRequest, bool> _authHandler;

        public OfficialTestProxy()
        {
            _listener = new TcpListener(IPAddress.IPv6Any, 0);
            _listener.Server.DualMode = true;
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Host = "localhost:" + Port.ToString(CultureInfo.InvariantCulture);
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port { get; }

        internal string Host { get; }

        internal string[] RequestUrls
        {
            get
            {
                lock (_lock)
                {
                    return _requestUrls.ToArray();
                }
            }

            set
            {
                lock (_lock)
                {
                    _requestUrls.Clear();
                    if (value != null)
                    {
                        _requestUrls.AddRange(value);
                    }
                }
            }
        }

        internal string[] RequestHosts
        {
            get
            {
                lock (_lock)
                {
                    return _requestHosts.ToArray();
                }
            }

            set
            {
                lock (_lock)
                {
                    _requestHosts.Clear();
                    if (value != null)
                    {
                        _requestHosts.AddRange(value);
                    }
                }
            }
        }

        internal string[] ConnectHosts
        {
            get
            {
                lock (_lock)
                {
                    return _connectHosts.ToArray();
                }
            }

            set
            {
                lock (_lock)
                {
                    _connectHosts.Clear();
                    if (value != null)
                    {
                        _connectHosts.AddRange(value);
                    }
                }
            }
        }

        internal string[] WsUrls
        {
            get
            {
                lock (_lock)
                {
                    return _wsUrls.ToArray();
                }
            }

            set
            {
                lock (_lock)
                {
                    _wsUrls.Clear();
                    if (value != null)
                    {
                        _wsUrls.AddRange(value);
                    }
                }
            }
        }

        internal void ForwardTo(int port, bool allowConnectRequests = false, string removePrefix = null)
        {
            _forwardPort = port;
            _allowConnect = allowConnectRequests;
            _removePrefix = removePrefix;
        }

        internal void SetAuthHandler(Func<string, bool> handler)
        {
            _authHandler = handler == null
                ? null
                : request => handler(request?.ProxyAuthorization);
        }

        internal void SetAuthHandler(Func<OfficialTestProxyAuthRequest, bool> handler)
        {
            _authHandler = handler;
        }

        internal void Reset()
        {
            lock (_lock)
            {
                _requestUrls.Clear();
                _requestHosts.Clear();
                _connectHosts.Clear();
                _wsUrls.Clear();
            }

            _authHandler = null;
            _forwardPort = 0;
            _allowConnect = false;
            _removePrefix = null;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }

            _cts.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    await using NetworkStream stream = client.GetStream();
                    while (!_cts.IsCancellationRequested)
                    {
                        ParsedHttpRequest request = await ReadRequestAsync(stream, _cts.Token).ConfigureAwait(false);
                        if (request == null)
                        {
                            return;
                        }

                        if (string.Equals(request.Method, "CONNECT", StringComparison.OrdinalIgnoreCase))
                        {
                            await HandleConnectAsync(stream, request).ConfigureAwait(false);
                            return;
                        }

                        if (IsWebSocketUpgrade(request))
                        {
                            await HandleUpgradeAsync(stream, request).ConfigureAwait(false);
                            return;
                        }

                        bool keepAlive = await HandleHttpAsync(stream, request).ConfigureAwait(false);
                        if (!keepAlive)
                        {
                            return;
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private async Task HandleConnectAsync(NetworkStream client, ParsedHttpRequest request)
        {
            if (ConnectHostsToIgnore.Contains(request.Target))
            {
                await WriteStatusAsync(client, 502, "Bad Gateway").ConfigureAwait(false);
                return;
            }

            lock (_lock)
            {
                _connectHosts.Add(request.Target);
            }

            if (!IsAuthorized(request))
            {
                await WriteProxyAuthRequiredAsync(client).ConfigureAwait(false);
                return;
            }

            if (!_allowConnect || _forwardPort <= 0)
            {
                await WriteStatusAsync(client, 502, "Bad Gateway").ConfigureAwait(false);
                return;
            }

            using TcpClient origin = new TcpClient();
            await origin.ConnectAsync(IPAddress.Loopback, _forwardPort).ConfigureAwait(false);
            await using NetworkStream originStream = origin.GetStream();
            byte[] established = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
            await client.WriteAsync(established).ConfigureAwait(false);
            await PipeBidirectionalAsync(client, originStream).ConfigureAwait(false);
        }

        private async Task<bool> HandleHttpAsync(NetworkStream client, ParsedHttpRequest request)
        {
            lock (_lock)
            {
                _requestUrls.Add(request.Target);
                if (!string.IsNullOrEmpty(request.Host))
                {
                    _requestHosts.Add(request.Host);
                }
            }

            if (!IsAuthorized(request))
            {
                await WriteProxyAuthRequiredAsync(client).ConfigureAwait(false);
                return false;
            }

            if (_forwardPort <= 0)
            {
                await WriteStatusAsync(client, 502, "Bad Gateway").ConfigureAwait(false);
                return false;
            }

            if (!TryParseTarget(request.Target, request.Host, out string path, out _))
            {
                await WriteStatusAsync(client, 400, "Bad Request").ConfigureAwait(false);
                return false;
            }

            path = ApplyRemovePrefix(path);

            using TcpClient origin = new TcpClient();
            await origin.ConnectAsync(IPAddress.Loopback, _forwardPort).ConfigureAwait(false);
            await using NetworkStream originStream = origin.GetStream();
            string host = "127.0.0.1:" + _forwardPort.ToString(CultureInfo.InvariantCulture);
            StringBuilder outgoing = new StringBuilder();
            outgoing.Append(request.Method);
            outgoing.Append(' ');
            outgoing.Append(path);
            outgoing.Append(' ');
            outgoing.Append(request.Version);
            outgoing.Append("\r\n");
            outgoing.Append("Host: ");
            outgoing.Append(host);
            outgoing.Append("\r\n");
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                outgoing.Append(header.Key);
                outgoing.Append(": ");
                outgoing.Append(header.Value);
                outgoing.Append("\r\n");
            }

            outgoing.Append("Connection: close\r\n\r\n");
            byte[] headerBytes = Encoding.ASCII.GetBytes(outgoing.ToString());
            await originStream.WriteAsync(headerBytes).ConfigureAwait(false);
            if (request.Body != null && request.Body.Length > 0)
            {
                await originStream.WriteAsync(request.Body).ConfigureAwait(false);
            }

            await originStream.CopyToAsync(client).ConfigureAwait(false);
            return false;
        }

        private async Task HandleUpgradeAsync(NetworkStream client, ParsedHttpRequest request)
        {
            lock (_lock)
            {
                _wsUrls.Add(request.Target);
            }

            if (!IsAuthorized(request))
            {
                await WriteProxyAuthRequiredAsync(client).ConfigureAwait(false);
                return;
            }

            if (_forwardPort <= 0)
            {
                await WriteStatusAsync(client, 502, "Bad Gateway").ConfigureAwait(false);
                return;
            }

            if (!TryParseTarget(request.Target, request.Host, out string path, out _))
            {
                await WriteStatusAsync(client, 400, "Bad Request").ConfigureAwait(false);
                return;
            }

            path = ApplyRemovePrefix(path);
            using TcpClient origin = new TcpClient();
            await origin.ConnectAsync(IPAddress.Loopback, _forwardPort).ConfigureAwait(false);
            await using NetworkStream originStream = origin.GetStream();
            string host = "127.0.0.1:" + _forwardPort.ToString(CultureInfo.InvariantCulture);
            StringBuilder outgoing = new StringBuilder();
            outgoing.Append(request.Method);
            outgoing.Append(' ');
            outgoing.Append(path);
            outgoing.Append(' ');
            outgoing.Append(request.Version);
            outgoing.Append("\r\n");
            outgoing.Append("Host: ");
            outgoing.Append(host);
            outgoing.Append("\r\n");
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                outgoing.Append(header.Key);
                outgoing.Append(": ");
                outgoing.Append(header.Value);
                outgoing.Append("\r\n");
            }

            outgoing.Append("\r\n");
            byte[] headerBytes = Encoding.ASCII.GetBytes(outgoing.ToString());
            await originStream.WriteAsync(headerBytes).ConfigureAwait(false);
            if (request.Body != null && request.Body.Length > 0)
            {
                await originStream.WriteAsync(request.Body).ConfigureAwait(false);
            }

            await PipeBidirectionalAsync(client, originStream).ConfigureAwait(false);
        }

        private bool IsAuthorized(ParsedHttpRequest request)
        {
            Func<OfficialTestProxyAuthRequest, bool> handler = _authHandler;
            if (handler == null)
            {
                return true;
            }

            try
            {
                return handler(new OfficialTestProxyAuthRequest
                {
                    Method = request.Method,
                    Host = string.IsNullOrEmpty(request.Host) ? request.Target : request.Host,
                    Target = request.Target,
                    ProxyAuthorization = request.ProxyAuthorization,
                });
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string ApplyRemovePrefix(string path)
        {
            if (string.IsNullOrEmpty(_removePrefix) || string.IsNullOrEmpty(path))
            {
                return path;
            }

            int index = path.IndexOf(_removePrefix, StringComparison.Ordinal);
            if (index < 0)
            {
                return path;
            }

            string rewritten = path.Remove(index, _removePrefix.Length);
            return string.IsNullOrEmpty(rewritten) ? "/" : rewritten;
        }

        private static async Task WriteProxyAuthRequiredAsync(NetworkStream stream)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 407 Proxy Authentication Required\r\n" +
                "Proxy-Authenticate: Basic realm=\"Playwright\"\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }

        private static async Task WriteStatusAsync(NetworkStream stream, int status, string reason)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + reason + "\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }

        private static bool TryParseTarget(string target, string hostHeader, out string path, out string host)
        {
            path = "/";
            host = hostHeader;
            if (string.IsNullOrEmpty(target))
            {
                return false;
            }

            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(target, UriKind.Absolute, out Uri uri))
                {
                    return false;
                }

                path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
                host = uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            path = target.StartsWith('/') ? target : "/" + target;
            return true;
        }

        private static async Task<ParsedHttpRequest> ReadRequestAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] headerBytes = await ReadUntilHeaderEndAsync(stream, token).ConfigureAwait(false);
            if (headerBytes == null || headerBytes.Length == 0)
            {
                return null;
            }

            string text = Encoding.ASCII.GetString(headerBytes);
            string[] lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return null;
            }

            string[] parts = lines[0].Split(' ');
            if (parts.Length < 3)
            {
                return null;
            }

            ParsedHttpRequest request = new ParsedHttpRequest
            {
                Method = parts[0],
                Target = parts[1],
                Version = parts[2],
            };

            int contentLength = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    request.ProxyAuthorization = value;
                }
                else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    request.Host = value;
                }
                else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int length))
                {
                    contentLength = length;
                }

                request.Headers.Add(new KeyValuePair<string, string>(name, value));
            }

            if (contentLength > 0)
            {
                byte[] body = new byte[contentLength];
                int offset = 0;
                while (offset < contentLength)
                {
                    int n = await stream.ReadAsync(body.AsMemory(offset, contentLength - offset), token).ConfigureAwait(false);
                    if (n == 0)
                    {
                        break;
                    }

                    offset += n;
                }

                request.Body = body;
            }

            return request;
        }

        private static async Task<byte[]> ReadUntilHeaderEndAsync(NetworkStream stream, CancellationToken token)
        {
            MemoryStream buffer = new MemoryStream();
            byte[] one = new byte[1];
            int matched = 0;
            while (buffer.Length < 64 * 1024)
            {
                int n = await stream.ReadAsync(one.AsMemory(0, 1), token).ConfigureAwait(false);
                if (n == 0)
                {
                    return buffer.Length == 0 ? null : buffer.ToArray();
                }

                buffer.WriteByte(one[0]);
                if (one[0] == (matched == 0 || matched == 2 ? (byte)'\r' : (byte)'\n'))
                {
                    matched++;
                    if (matched == 4)
                    {
                        return buffer.ToArray();
                    }
                }
                else
                {
                    matched = one[0] == (byte)'\r' ? 1 : 0;
                }
            }

            return buffer.ToArray();
        }

        private static async Task PipeBidirectionalAsync(NetworkStream a, NetworkStream b)
        {
            Task aToB = CopyQuietlyAsync(a, b);
            Task bToA = CopyQuietlyAsync(b, a);
            await Task.WhenAny(aToB, bToA).ConfigureAwait(false);
        }

        private static async Task CopyQuietlyAsync(NetworkStream from, NetworkStream to)
        {
            try
            {
                await from.CopyToAsync(to).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static bool IsWebSocketUpgrade(ParsedHttpRequest request)
        {
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                if (header.Key.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)
                    && header.Value.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ParsedHttpRequest
        {
            internal string Method { get; set; }

            internal string Target { get; set; }

            internal string Version { get; set; }

            internal string Host { get; set; }

            internal string ProxyAuthorization { get; set; }

            internal List<KeyValuePair<string, string>> Headers { get; } = new();

            internal byte[] Body { get; set; }
        }
    }

    /// <summary>
    /// Official <c>IncomingMessage</c> subset for <c>TestProxy.setAuthHandler</c>.
    /// </summary>
    internal sealed class OfficialTestProxyAuthRequest
    {
        internal string Method { get; set; }

        internal string Host { get; set; }

        internal string Target { get; set; }

        internal string ProxyAuthorization { get; set; }
    }
}
