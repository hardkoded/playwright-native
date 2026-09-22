/*
 * Copyright (c) Microsoft Corporation.
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// WebKit (CFNetwork on Darwin, libsoup on Linux) stops proxying localhost /
    /// link-local once any <c>proxyBypassList</c> / <c>--ignore-host</c> entry is
    /// set. This shim is the browser proxy with an empty bypass list so those
    /// hosts stay proxied; listed bypass hosts are connected directly here.
    /// </summary>
    internal sealed class WebKitMacProxyBypassShim : IDisposable
    {
        private static readonly string[] HeaderLineSeparators = { "\r\n" };

        private static readonly Encoding Latin1 = Encoding.Latin1;

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly string _upstreamHost;
        private readonly int _upstreamPort;
        private readonly string _bypass;
        private readonly string _proxyAuthorization;
        private int _disposed;

        private WebKitMacProxyBypassShim(
            string upstreamHost,
            int upstreamPort,
            string bypass,
            string proxyAuthorization,
            string username,
            string password)
        {
            _upstreamHost = upstreamHost;
            _upstreamPort = upstreamPort;
            _bypass = bypass;
            _proxyAuthorization = proxyAuthorization;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            // Pass credentials through to WebKit so CFNetwork retries CONNECT after
            // a 407/close with Proxy-Authorization. CONNECT hops forward WebKit's
            // auth header (do not always inject — that skips the 407 challenge).
            BrowserProxy = new Proxy
            {
                Server = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture),
                Username = username,
                Password = password,
            };
            _acceptLoop = AcceptLoopAsync();
        }

        /// <summary>
        /// Listening port on 127.0.0.1.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Proxy settings passed to WebKit (no bypass list).
        /// </summary>
        public Proxy BrowserProxy { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
            }

            (_listener as IDisposable)?.Dispose();
            _cts.Dispose();
            GC.KeepAlive(_acceptLoop);
        }

        /// <summary>
        /// Starts a WebKit HTTP(S) proxy shim on Darwin/Linux when needed.
        /// CFNetwork refuses to proxy <c>localhost</c> even with an empty
        /// bypass list (<c>kCFErrorHTTPProxyConnectionFailure</c> / 306), so
        /// Darwin always wraps HTTP(S) proxies. Linux wraps when a non-loopback
        /// bypass list is present (libsoup then also drops localhost /
        /// link-local); a sole <c>&lt;-loopback&gt;</c> bypass is left to
        /// libsoup so HTTP/2 ALPN on localhost survives. SOCKS is not framed
        /// here. Windows uses curl's noproxy and skips this.
        /// </summary>
        /// <param name="userProxy">Caller proxy, or <see langword="null"/>.</param>
        /// <param name="browserProxy">Proxy to pass to WebKit.</param>
        /// <returns>The shim to dispose with the context/browser, or <see langword="null"/>.</returns>
        internal static WebKitMacProxyBypassShim TryStart(Proxy userProxy, out Proxy browserProxy)
        {
            browserProxy = userProxy;
            if (userProxy == null
                || string.IsNullOrEmpty(userProxy.Server))
            {
                return null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && Environment.GetEnvironmentVariable("PW_FORCE_MAC_PROXY_BYPASS_SHIM") != "1")
            {
                return null;
            }

            string bypass = ProxySettings.NormalizeBypass(userProxy.Bypass);
            bool isDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            if (string.IsNullOrEmpty(bypass) && !isDarwin)
            {
                return null;
            }

            // Linux libsoup honors <loopback> / <-loopback> without needing this shim.
            // Wrapping would present an empty-bypass HTTP proxy to WebKit, so
            // even localhost navigations use CONNECT and lose HTTP/2 ALPN
            // ("No supported application protocol" on h2-only origins).
            if (!isDarwin
                && (string.Equals(bypass, "<-loopback>", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(bypass, "<loopback>", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            string formatted = ProxySettings.FormatServer(userProxy, includeCredentials: true);
            if (!Uri.TryCreate(formatted, UriKind.Absolute, out Uri upstream)
                || string.IsNullOrEmpty(upstream.Host))
            {
                return null;
            }

            // This shim speaks HTTP absolute-form / CONNECT only.
            if (upstream.Scheme.StartsWith("socks", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int port = upstream.IsDefaultPort
                ? (string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
                : upstream.Port;
            string proxyAuthorization = null;
            if (!string.IsNullOrEmpty(upstream.UserInfo))
            {
                string decoded = Uri.UnescapeDataString(upstream.UserInfo);
                proxyAuthorization = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(decoded));
            }

            WebKitMacProxyBypassShim shim = new(
                upstream.Host,
                port,
                bypass,
                proxyAuthorization,
                userProxy.Username,
                userProxy.Password);
            browserProxy = shim.BrowserProxy;
            return shim;
        }

        private static async Task ConnectWithTimeoutAsync(TcpClient client, string host, int port)
        {
            using CancellationTokenSource connectCts = new(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);
        }

        private static async Task PipeBidirectionalAsync(NetworkStream a, NetworkStream b)
        {
            // Half-close aware tunnel (same pattern as LocaleHandshakeProxy): when
            // one side EOFs after a client WebSocket close frame, shut down only
            // that write direction and keep copying the opposite way so the close
            // echo reaches the browser. Task.WhenAny + dispose aborted application
            // close codes as 1006 (ShouldWorkWithClientSideClose).
            try
            {
                a.Socket.NoDelay = true;
                b.Socket.NoDelay = true;
                a.Socket.LingerState = new LingerOption(true, 2);
                b.Socket.LingerState = new LingerOption(true, 2);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            using CancellationTokenSource tunnelCts = new();
            Task aToB = CopyAndShutdownAsync(a, b, tunnelCts.Token);
            Task bToA = CopyAndShutdownAsync(b, a, tunnelCts.Token);
            try
            {
                await Task.WhenAll(aToB, bToA).ConfigureAwait(false);
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
            finally
            {
                try
                {
                    await tunnelCts.CancelAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                }

                // Give dual-hop Darwin proxies time to flush the close echo
                // before TcpClient.Dispose RSTs the browser-facing socket.
                try
                {
                    await Task.Delay(400).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static async Task CopyAndShutdownAsync(
            NetworkStream source,
            NetworkStream destination,
            CancellationToken token)
        {
            byte[] buffer = new byte[81920];
            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), token)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    await destination.FlushAsync(token).ConfigureAwait(false);
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
            finally
            {
                try
                {
                    try
                    {
                        destination.Socket.LingerState = new LingerOption(true, 10);
                    }
                    catch (SocketException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    // Separate the last flushed write from TCP FIN so CFNetwork
                    // can process a WebSocket close frame before half-close
                    // (ShouldWorkWithClientSideClose → 3002 vs 1006).
                    try
                    {
                        await Task.Delay(80, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    destination.Socket?.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static async Task<(byte[] Headers, byte[] Leftover)> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
        {
            using MemoryStream buffer = new();
            byte[] chunk = new byte[4096];
            while (buffer.Length < 1024 * 1024)
            {
                int n = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), token).ConfigureAwait(false);
                if (n == 0)
                {
                    break;
                }

                await buffer.WriteAsync(chunk.AsMemory(0, n), token).ConfigureAwait(false);
                byte[] data = buffer.ToArray();
                for (int i = 0; i + 3 < data.Length; i++)
                {
                    if (data[i] == (byte)'\r'
                        && data[i + 1] == (byte)'\n'
                        && data[i + 2] == (byte)'\r'
                        && data[i + 3] == (byte)'\n')
                    {
                        int headerLen = i + 4;
                        byte[] headers = new byte[headerLen];
                        Buffer.BlockCopy(data, 0, headers, 0, headerLen);
                        int leftoverLen = data.Length - headerLen;
                        byte[] leftover = leftoverLen == 0 ? Array.Empty<byte>() : new byte[leftoverLen];
                        if (leftoverLen > 0)
                        {
                            Buffer.BlockCopy(data, headerLen, leftover, 0, leftoverLen);
                        }

                        return (headers, leftover);
                    }
                }
            }

            return buffer.Length == 0
                ? (null, Array.Empty<byte>())
                : (buffer.ToArray(), Array.Empty<byte>());
        }

        private static bool TryParseHostPort(string target, int defaultPort, out string host, out int port)
        {
            host = null;
            port = defaultPort;
            if (string.IsNullOrEmpty(target))
            {
                return false;
            }

            if (target.StartsWith('[')
                && target.IndexOf(']') is int end
                && end > 1)
            {
                host = target.Substring(1, end - 1);
                if (end + 1 < target.Length && target[end + 1] == ':')
                {
                    return int.TryParse(target.AsSpan(end + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out port);
                }

                return true;
            }

            int colon = target.LastIndexOf(':');
            if (colon > 0
                && int.TryParse(target.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
            {
                host = target.Substring(0, colon);
                return !string.IsNullOrEmpty(host);
            }

            host = target;
            port = defaultPort;
            return true;
        }

        private static byte[] RewriteRequestTarget(byte[] headerBytes, string method, string originForm, bool forceConnectionClose)
        {
            string text = Latin1.GetString(headerBytes);
            int lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
            if (lineEnd < 0)
            {
                return headerBytes;
            }

            string rest = text.Substring(lineEnd);
            int versionIdx = text.LastIndexOf(' ', lineEnd - 1);
            string version = versionIdx > 0 ? text.Substring(versionIdx + 1, lineEnd - versionIdx - 1) : "HTTP/1.1";
            string rewritten = method + " " + originForm + " " + version + rest;
            byte[] rewrittenBytes = Latin1.GetBytes(rewritten);
            return forceConnectionClose ? ForceConnectionClose(rewrittenBytes) : rewrittenBytes;
        }

        /// <summary>
        /// Removes <c>Proxy-Connection</c> / <c>Proxy-Authorization</c> before an
        /// origin hop so the test server's view matches <c>Request.AllHeaders</c>.
        /// </summary>
        /// <param name="headerBytes">Origin-form request headers.</param>
        /// <returns>Headers without proxy hop fields.</returns>
        private static byte[] StripProxyHopHeaders(byte[] headerBytes)
        {
            if (headerBytes == null || headerBytes.Length == 0)
            {
                return headerBytes;
            }

            string text = Latin1.GetString(headerBytes);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return headerBytes;
            }

            string[] lines = text.Substring(0, headerEnd).Split(HeaderLineSeparators, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return headerBytes;
            }

            StringBuilder rebuilt = new();
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0
                    && (lines[i].StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase)
                        || lines[i].StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase)))
                {
                    changed = true;
                    continue;
                }

                if (rebuilt.Length > 0)
                {
                    rebuilt.Append("\r\n");
                }

                rebuilt.Append(lines[i]);
            }

            if (!changed)
            {
                return headerBytes;
            }

            rebuilt.Append(text.AsSpan(headerEnd));
            return Latin1.GetBytes(rebuilt.ToString());
        }

        /// <summary>
        /// Reads <c>Proxy-Authorization</c> from WebKit's CONNECT headers, if any.
        /// </summary>
        private static string ExtractProxyAuthorization(byte[] headerBytes)
        {
            if (headerBytes == null || headerBytes.Length == 0)
            {
                return null;
            }

            string text = Latin1.GetString(headerBytes);
            string[] lines = text.Split(HeaderLineSeparators, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring("Proxy-Authorization:".Length).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Returns whether <paramref name="headerBytes"/> is a WebSocket upgrade
        /// handshake (absolute-form or origin-form).
        /// </summary>
        /// <param name="headerBytes">HTTP request headers from the browser.</param>
        /// <returns><see langword="true"/> when the request upgrades to WebSocket.</returns>
        private static bool IsWebSocketUpgrade(byte[] headerBytes)
        {
            if (headerBytes == null || headerBytes.Length == 0)
            {
                return false;
            }

            string text = Latin1.GetString(headerBytes);
            return text.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Forces <c>Connection: close</c> so the shim handles one HTTP exchange
        /// per TCP connection. Blind bidirectional piping with keep-alive would
        /// forward a later request (e.g. a bypassed host) to the upstream proxy
        /// without re-running <see cref="ProxySettings.ShouldBypass"/>.
        /// </summary>
        /// <param name="headerBytes">Absolute-form or origin-form request headers.</param>
        /// <returns>Headers with a single Connection: close directive.</returns>
        private static byte[] ForceConnectionClose(byte[] headerBytes)
        {
            string text = Latin1.GetString(headerBytes);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return headerBytes;
            }

            string head = text.Substring(0, headerEnd);
            string[] lines = head.Split(HeaderLineSeparators, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return headerBytes;
            }

            StringBuilder rebuilt = new();
            rebuilt.Append(lines[0]).Append("\r\n");
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)
                    || lines[i].StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                rebuilt.Append(lines[i]).Append("\r\n");
            }

            rebuilt.Append("Connection: close\r\n\r\n");
            return Latin1.GetBytes(rebuilt.ToString());
        }

        private static async Task WriteAsciiAsync(NetworkStream stream, string text)
        {
            await stream.WriteAsync(Latin1.GetBytes(text)).ConfigureAwait(false);

            // Flush before the accept-loop disposes the client socket. Darwin
            // CFNetwork otherwise intermittently misses a proxied 407 and never
            // reconnects with credentials (ShouldReconnectWithCredentialsAfterConnect407…).
            await stream.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Copies a single framed HTTP response (upstream → client). Must not
        /// wait for upstream EOF: the test / upstream proxy keeps connections
        /// alive, so <see cref="Stream.CopyToAsync(Stream)"/> would hang after
        /// the first document response.
        /// </summary>
        private static async Task CopyOneHttpResponseAsync(NetworkStream upstream, NetworkStream client, CancellationToken token)
        {
            (byte[] headerBytes, byte[] leftover) = await ReadHeadersAsync(upstream, token).ConfigureAwait(false);
            if (headerBytes == null || headerBytes.Length == 0)
            {
                return;
            }

            // Tell WebKit not to reuse this proxy connection so the next
            // navigation opens a fresh socket and re-runs ShouldBypass.
            byte[] clientHeaders = ForceConnectionClose(headerBytes);
            await client.WriteAsync(clientHeaders, token).ConfigureAwait(false);

            LeftoverReader body = new(upstream, leftover);
            string headerText = Latin1.GetString(headerBytes);
            if (headerText.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase)
                || headerText.Contains("Transfer-Encoding:chunked", StringComparison.OrdinalIgnoreCase))
            {
                await CopyChunkedBodyAsync(body, client, token).ConfigureAwait(false);
            }
            else
            {
                int contentLength = ParseContentLength(headerText);
                if (contentLength < 0)
                {
                    // No length and not chunked — read until upstream closes.
                    await body.CopyToAsync(client, token).ConfigureAwait(false);
                }
                else if (contentLength > 0)
                {
                    await CopyExactAsync(body, client, contentLength, token).ConfigureAwait(false);
                }
            }

            // Ensure the framed response reaches WebKit before HandleClientAsync
            // disposes the socket (empty-title flakes on proxied navigations).
            await client.FlushAsync(token).ConfigureAwait(false);
        }

        private static int ParseContentLength(string headerText)
        {
            const string key = "Content-Length:";
            int idx = headerText.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return -1;
            }

            int start = idx + key.Length;
            int end = headerText.IndexOf("\r\n", start, StringComparison.Ordinal);
            if (end < 0)
            {
                end = headerText.Length;
            }

            return int.TryParse(
                headerText.AsSpan(start, end - start).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int length)
                ? Math.Max(length, 0)
                : -1;
        }

        private static async Task CopyExactAsync(LeftoverReader source, NetworkStream dest, int length, CancellationToken token)
        {
            byte[] buffer = new byte[Math.Min(length, 8192)];
            int remaining = length;
            while (remaining > 0)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), token)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                await dest.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                remaining -= read;
            }
        }

        private static async Task CopyExactFromStreamAsync(
            NetworkStream source,
            NetworkStream dest,
            int length,
            CancellationToken token)
        {
            byte[] buffer = new byte[Math.Min(length, 8192)];
            int remaining = length;
            while (remaining > 0)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), token)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                await dest.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                remaining -= read;
            }
        }

        private static async Task CopyChunkedBodyAsync(LeftoverReader source, NetworkStream dest, CancellationToken token)
        {
            // Relay chunk-size lines + payload + trailers until the 0-chunk.
            while (true)
            {
                string sizeLine = await source.ReadLineAsync(token).ConfigureAwait(false);
                if (sizeLine == null)
                {
                    return;
                }

                byte[] sizeBytes = Latin1.GetBytes(sizeLine + "\r\n");
                await dest.WriteAsync(sizeBytes, token).ConfigureAwait(false);

                int semi = sizeLine.IndexOf(';');
                string hex = semi >= 0 ? sizeLine.Substring(0, semi) : sizeLine;
                if (!int.TryParse(hex.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int size))
                {
                    return;
                }

                if (size > 0)
                {
                    await CopyExactAsync(source, dest, size, token).ConfigureAwait(false);

                    // Trailing CRLF after chunk data.
                    await CopyExactAsync(source, dest, 2, token).ConfigureAwait(false);
                    continue;
                }

                // Final chunk: copy trailers until blank line.
                while (true)
                {
                    string trailer = await source.ReadLineAsync(token).ConfigureAwait(false);
                    if (trailer == null)
                    {
                        return;
                    }

                    byte[] trailerBytes = Latin1.GetBytes(trailer + "\r\n");
                    await dest.WriteAsync(trailerBytes, token).ConfigureAwait(false);
                    if (trailer.Length == 0)
                    {
                        return;
                    }
                }
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        return;
                    }

                    continue;
                }

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.NoDelay = true;
                    try
                    {
                        client.Client.LingerState = new LingerOption(true, 5);
                    }
                    catch (SocketException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    NetworkStream clientStream = client.GetStream();
                    (byte[] headerBytes, byte[] requestLeftover) = await ReadHeadersAsync(clientStream, _cts.Token)
                        .ConfigureAwait(false);
                    if (headerBytes == null || headerBytes.Length == 0)
                    {
                        return;
                    }

                    string headerText = Latin1.GetString(headerBytes);
                    int lineEnd = headerText.IndexOf("\r\n", StringComparison.Ordinal);
                    string requestLine = lineEnd >= 0 ? headerText.Substring(0, lineEnd) : headerText;
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        return;
                    }

                    string method = parts[0];
                    string target = parts[1];
                    if (string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleConnectAsync(clientStream, target, headerBytes).ConfigureAwait(false);
                        return;
                    }

                    await HandleHttpAsync(clientStream, headerBytes, requestLeftover, method, target)
                        .ConfigureAwait(false);
                }
#pragma warning disable RCS1075
                catch (Exception)
#pragma warning restore RCS1075
                {
                }
            }
        }

        private async Task HandleConnectAsync(NetworkStream clientStream, string target, byte[] clientHeaders)
        {
            if (!TryParseHostPort(target, 443, out string host, out int port))
            {
                await WriteAsciiAsync(clientStream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n")
                    .ConfigureAwait(false);
                return;
            }

            TcpClient upstream = new();
            try
            {
                upstream.NoDelay = true;
                if (ProxySettings.ShouldBypass(ProxySettings.RequestHost(host, port), _bypass))
                {
                    await ConnectWithTimeoutAsync(upstream, host, port).ConfigureAwait(false);
                    await WriteAsciiAsync(clientStream, "HTTP/1.1 200 Connection Established\r\n\r\n")
                        .ConfigureAwait(false);
                    await PipeBidirectionalAsync(clientStream, upstream.GetStream()).ConfigureAwait(false);
                    return;
                }

                await ConnectWithTimeoutAsync(upstream, _upstreamHost, _upstreamPort).ConfigureAwait(false);
                NetworkStream upStream = upstream.GetStream();

                // Forward only the Proxy-Authorization WebKit sent. Always injecting
                // stored credentials makes the first CONNECT look authenticated to
                // the upstream proxy; CFNetwork then often skips the 407 retry
                // (ShouldReconnectWithCredentialsAfterConnect407… count stays 1).
                // BrowserProxy Username/Password drive WebKit's challenge response.
                string clientAuth = ExtractProxyAuthorization(clientHeaders);
                string connectRequest = "CONNECT " + host + ":" + port.ToString(CultureInfo.InvariantCulture)
                    + " HTTP/1.1\r\nHost: " + host + ":" + port.ToString(CultureInfo.InvariantCulture)
                    + "\r\n";
                if (!string.IsNullOrEmpty(clientAuth))
                {
                    connectRequest += "Proxy-Authorization: " + clientAuth + "\r\n";
                }
                else if (!string.IsNullOrEmpty(_proxyAuthorization)
                    && string.IsNullOrEmpty(BrowserProxy.Username))
                {
                    // Credentials lived only in the proxy URL (no Username field) —
                    // WebKit will not challenge-reply; inject on every hop.
                    connectRequest += "Proxy-Authorization: " + _proxyAuthorization + "\r\n";
                }

                connectRequest += "\r\n";
                await WriteAsciiAsync(upStream, connectRequest).ConfigureAwait(false);
                (byte[] responseHeaders, byte[] leftover) = await ReadHeadersAsync(upStream, _cts.Token)
                    .ConfigureAwait(false);
                if (responseHeaders == null)
                {
                    await WriteAsciiAsync(clientStream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n")
                        .ConfigureAwait(false);
                    return;
                }

                await clientStream.WriteAsync(responseHeaders).ConfigureAwait(false);
                if (leftover.Length > 0)
                {
                    // Bytes that arrived with the CONNECT response (TLS ClientHello
                    // from upstream is rare here; more often empty) must not be
                    // discarded before the bidirectional tunnel starts.
                    await clientStream.WriteAsync(leftover).ConfigureAwait(false);
                }

                string status = Latin1.GetString(responseHeaders);
                if (status.StartsWith("HTTP/1.1 200", StringComparison.Ordinal)
                    || status.StartsWith("HTTP/1.0 200", StringComparison.Ordinal))
                {
                    await PipeBidirectionalAsync(clientStream, upStream).ConfigureAwait(false);
                }
                else
                {
                    // Non-200 (typically 407) must reach WebKit before this socket
                    // is closed by HandleClientAsync — otherwise CFNetwork may not
                    // retry CONNECT with Proxy-Authorization.
                    await clientStream.FlushAsync().ConfigureAwait(false);
                    try
                    {
                        await Task.Delay(150).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
            finally
            {
                upstream.Dispose();
            }
        }

        private async Task HandleHttpAsync(
            NetworkStream clientStream,
            byte[] headerBytes,
            byte[] requestLeftover,
            string method,
            string target)
        {
            if (!Uri.TryCreate(target, UriKind.Absolute, out Uri uri)
                || string.IsNullOrEmpty(uri.Host))
            {
                await WriteAsciiAsync(clientStream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n")
                    .ConfigureAwait(false);
                return;
            }

            int port = uri.IsDefaultPort ? 80 : uri.Port;
            string requestHost = ProxySettings.RequestHost(uri.Host, port);
            bool webSocketUpgrade = IsWebSocketUpgrade(headerBytes);
            TcpClient upstream = new();
            try
            {
                upstream.NoDelay = true;
                NetworkStream upStream;
                byte[] outbound;
                if (ProxySettings.ShouldBypass(requestHost, _bypass))
                {
                    await ConnectWithTimeoutAsync(upstream, uri.Host, port).ConfigureAwait(false);
                    upStream = upstream.GetStream();
                    string originForm = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

                    // Direct origin connect: keep the browser's Connection header.
                    // Forcing close here made AllHeaders report keep-alive while the
                    // test server saw Connection: close (ShouldReportRawHeaders).
                    // Still strip Proxy-* so the origin does not see hop headers that
                    // Network.allHeaders never reports (ShouldGetTheSameHeadersAsTheServer).
                    outbound = StripProxyHopHeaders(
                        RewriteRequestTarget(headerBytes, method, originForm, forceConnectionClose: false));
                }
                else
                {
                    await ConnectWithTimeoutAsync(upstream, _upstreamHost, _upstreamPort).ConfigureAwait(false);
                    upStream = upstream.GetStream();
                    byte[] authorized = InjectProxyAuthorization(headerBytes);
                    outbound = webSocketUpgrade ? authorized : ForceConnectionClose(authorized);
                }

                await upStream.WriteAsync(outbound).ConfigureAwait(false);
                if (requestLeftover != null && requestLeftover.Length > 0)
                {
                    await upStream.WriteAsync(requestLeftover).ConfigureAwait(false);
                }

                // WebKit often sends interceptWithRequest POST bodies after the
                // headers (separate TCP write). ReadHeadersAsync only returns
                // bytes already buffered with the headers — drain the rest of
                // Content-Length before waiting on the upstream response, or
                // both sides hang (Darwin Fallback amend postData timeouts).
                if (!webSocketUpgrade)
                {
                    int contentLength = ParseContentLength(Latin1.GetString(headerBytes));
                    int already = requestLeftover?.Length ?? 0;
                    if (contentLength > already)
                    {
                        await CopyExactFromStreamAsync(
                            clientStream,
                            upStream,
                            contentLength - already,
                            _cts.Token).ConfigureAwait(false);
                    }
                }

                // WebSocket upgrades must stay a bidirectional tunnel after the
                // 101 response. One-shot HTTP framing + Connection: close (used
                // for normal navigations so keep-alive cannot skip ShouldBypass)
                // aborts the handshake and hangs page WebSocket evaluates.
                if (webSocketUpgrade)
                {
                    await PipeBidirectionalAsync(clientStream, upStream).ConfigureAwait(false);
                    return;
                }

                // One framed response — do not pipe until EOF (upstream keep-alive
                // would hang), and do not keep-alive pipe (a later absolute-form
                // request would skip ShouldBypass and hang in upstream DNS).
                await CopyOneHttpResponseAsync(upStream, clientStream, _cts.Token).ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                // For bypassed hosts, close without an HTTP response so WebKit
                // surfaces a connection failure (DNS / refused), matching a
                // direct browser connect. A 502 would make page.goto succeed.
                if (!ProxySettings.ShouldBypass(requestHost, _bypass))
                {
                    try
                    {
                        await WriteAsciiAsync(clientStream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n")
                            .ConfigureAwait(false);
                    }
#pragma warning disable RCS1075
                    catch (Exception)
#pragma warning restore RCS1075
                    {
                    }
                }
            }
            finally
            {
                upstream.Dispose();
            }
        }

        /// <summary>
        /// Injects upstream <c>Proxy-Authorization</c> when the shim wraps a
        /// credentialed proxy. WebKit authenticates to the shim (no credentials);
        /// the shim must attach credentials on the hop to the real proxy.
        /// </summary>
        /// <param name="headerBytes">Request headers from WebKit.</param>
        /// <returns>Headers including Proxy-Authorization when configured.</returns>
        private byte[] InjectProxyAuthorization(byte[] headerBytes)
        {
            if (string.IsNullOrEmpty(_proxyAuthorization) || headerBytes == null || headerBytes.Length == 0)
            {
                return headerBytes;
            }

            string text = Latin1.GetString(headerBytes);
            if (text.Contains("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
            {
                return headerBytes;
            }

            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return headerBytes;
            }

            string injected = string.Concat(
                text.AsSpan(0, headerEnd),
                "\r\nProxy-Authorization: ",
                _proxyAuthorization,
                "\r\n\r\n");
            return Latin1.GetBytes(injected);
        }

        /// <summary>
        /// Reads from a <see cref="NetworkStream"/> after an optional prefix that
        /// was already pulled while scanning for HTTP header terminators.
        /// </summary>
        private sealed class LeftoverReader
        {
            private readonly NetworkStream _stream;
            private byte[] _leftover;
            private int _offset;

            internal LeftoverReader(NetworkStream stream, byte[] leftover)
            {
                _stream = stream;
                _leftover = leftover == null || leftover.Length == 0 ? null : leftover;
            }

            internal async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
            {
                if (_leftover != null)
                {
                    int available = _leftover.Length - _offset;
                    int take = Math.Min(available, buffer.Length);
                    _leftover.AsSpan(_offset, take).CopyTo(buffer.Span);
                    _offset += take;
                    if (_offset >= _leftover.Length)
                    {
                        _leftover = null;
                    }

                    return take;
                }

                return await _stream.ReadAsync(buffer, token).ConfigureAwait(false);
            }

            internal async Task CopyToAsync(NetworkStream dest, CancellationToken token)
            {
                if (_leftover != null)
                {
                    await dest.WriteAsync(_leftover.AsMemory(_offset), token).ConfigureAwait(false);
                    _leftover = null;
                }

                await _stream.CopyToAsync(dest, token).ConfigureAwait(false);
            }

            internal async Task<string> ReadLineAsync(CancellationToken token)
            {
                using MemoryStream buffer = new();
                byte[] one = new byte[1];
                while (buffer.Length < 64 * 1024)
                {
                    int n = await ReadAsync(one.AsMemory(0, 1), token).ConfigureAwait(false);
                    if (n == 0)
                    {
                        return buffer.Length == 0 ? null : Latin1.GetString(buffer.ToArray());
                    }

                    if (one[0] == (byte)'\n')
                    {
                        byte[] data = buffer.ToArray();
                        int len = data.Length;
                        if (len > 0 && data[len - 1] == (byte)'\r')
                        {
                            len--;
                        }

                        return Latin1.GetString(data, 0, len);
                    }

                    await buffer.WriteAsync(one.AsMemory(0, 1), token).ConfigureAwait(false);
                }

                return Latin1.GetString(buffer.ToArray());
            }
        }
    }
}
