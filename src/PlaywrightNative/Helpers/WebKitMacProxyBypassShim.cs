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
    /// macOS WebKit CFNetwork treats any <c>proxyBypassList</c> as also
    /// excluding localhost / link-local from the proxy (same reason upstream
    /// skips those hosts when bypass rules are set). This shim is the browser
    /// proxy with an empty bypass list so loopback and link-local stay
    /// proxied; listed bypass hosts are connected directly here instead.
    /// </summary>
    internal sealed class WebKitMacProxyBypassShim : IDisposable
    {
        private static readonly Encoding Latin1 = Encoding.Latin1;

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly string _upstreamHost;
        private readonly int _upstreamPort;
        private readonly string _bypass;
        private int _disposed;

        private WebKitMacProxyBypassShim(string upstreamHost, int upstreamPort, string bypass)
        {
            _upstreamHost = upstreamHost;
            _upstreamPort = upstreamPort;
            _bypass = bypass;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BrowserProxy = new Proxy
            {
                Server = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture),
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
        /// Starts a Darwin bypass shim when <paramref name="userProxy"/> has a
        /// non-empty bypass list. Otherwise returns <see langword="null"/> and
        /// leaves <paramref name="browserProxy"/> unchanged.
        /// </summary>
        /// <param name="userProxy">Caller proxy, or <see langword="null"/>.</param>
        /// <param name="browserProxy">Proxy to pass to WebKit.</param>
        /// <returns>The shim to dispose with the context/browser, or <see langword="null"/>.</returns>
        internal static WebKitMacProxyBypassShim TryStart(Proxy userProxy, out Proxy browserProxy)
        {
            browserProxy = userProxy;
            if (userProxy == null
                || string.IsNullOrEmpty(userProxy.Server)
                || string.IsNullOrEmpty(ProxySettings.NormalizeBypass(userProxy.Bypass)))
            {
                return null;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                && Environment.GetEnvironmentVariable("PW_FORCE_MAC_PROXY_BYPASS_SHIM") != "1")
            {
                return null;
            }

            string formatted = ProxySettings.FormatServer(userProxy, includeCredentials: true);
            if (!Uri.TryCreate(formatted, UriKind.Absolute, out Uri upstream)
                || string.IsNullOrEmpty(upstream.Host))
            {
                return null;
            }

            int port = upstream.IsDefaultPort
                ? (string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
                : upstream.Port;
            WebKitMacProxyBypassShim shim = new(upstream.Host, port, ProxySettings.NormalizeBypass(userProxy.Bypass));
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
            Task aToB = a.CopyToAsync(b);
            Task bToA = b.CopyToAsync(a);
            try
            {
                await Task.WhenAny(aToB, bToA).ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }
        }

        private static async Task<byte[]> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
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
                        return headers;
                    }
                }
            }

            return buffer.Length == 0 ? null : buffer.ToArray();
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

        private static byte[] RewriteRequestTarget(byte[] headerBytes, string method, string originForm)
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
            return Latin1.GetBytes(rewritten);
        }

        private static Task WriteAsciiAsync(NetworkStream stream, string text)
            => stream.WriteAsync(Latin1.GetBytes(text)).AsTask();

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
                    NetworkStream clientStream = client.GetStream();
                    byte[] headerBytes = await ReadHeadersAsync(clientStream, _cts.Token).ConfigureAwait(false);
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
                        await HandleConnectAsync(clientStream, target).ConfigureAwait(false);
                        return;
                    }

                    await HandleHttpAsync(clientStream, headerBytes, method, target).ConfigureAwait(false);
                }
#pragma warning disable RCS1075
                catch (Exception)
#pragma warning restore RCS1075
                {
                }
            }
        }

        private async Task HandleConnectAsync(NetworkStream clientStream, string target)
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
                string connectRequest = "CONNECT " + host + ":" + port.ToString(CultureInfo.InvariantCulture)
                    + " HTTP/1.1\r\nHost: " + host + ":" + port.ToString(CultureInfo.InvariantCulture)
                    + "\r\n\r\n";
                await WriteAsciiAsync(upStream, connectRequest).ConfigureAwait(false);
                byte[] responseHeaders = await ReadHeadersAsync(upStream, _cts.Token).ConfigureAwait(false);
                if (responseHeaders == null)
                {
                    await WriteAsciiAsync(clientStream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n")
                        .ConfigureAwait(false);
                    return;
                }

                await clientStream.WriteAsync(responseHeaders).ConfigureAwait(false);
                string status = Latin1.GetString(responseHeaders);
                if (status.StartsWith("HTTP/1.1 200", StringComparison.Ordinal)
                    || status.StartsWith("HTTP/1.0 200", StringComparison.Ordinal))
                {
                    await PipeBidirectionalAsync(clientStream, upStream).ConfigureAwait(false);
                }
            }
            finally
            {
                upstream.Dispose();
            }
        }

        private async Task HandleHttpAsync(NetworkStream clientStream, byte[] headerBytes, string method, string target)
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
                    outbound = RewriteRequestTarget(headerBytes, method, originForm);
                }
                else
                {
                    await ConnectWithTimeoutAsync(upstream, _upstreamHost, _upstreamPort).ConfigureAwait(false);
                    upStream = upstream.GetStream();
                    outbound = headerBytes;
                }

                await upStream.WriteAsync(outbound).ConfigureAwait(false);
                await PipeBidirectionalAsync(clientStream, upStream).ConfigureAwait(false);
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
    }
}
