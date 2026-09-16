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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Rewrites WebSocket handshake <c>Accept-Language</c> and extra HTTP
    /// headers for browsers that ignore those on upgrades (Chromium before
    /// 151, WebKit 2276). Regular HTTP is forwarded unchanged so user
    /// <c>fetch</c> headers win.
    /// </summary>
    internal sealed class LocaleHandshakeProxy : IDisposable
    {
        private static readonly string[] HeaderSeparators = ["\r\n"];

        /// <summary>
        /// Byte-preserving 0–255 mapping. <see cref="Encoding.ASCII"/> replaces
        /// bytes ≥ 128 with <c>?</c> (63), which corrupts binary/UTF-8 bodies on
        /// loopback HTTP when WebKit traffic is forced through this proxy.
        /// </summary>
        private static readonly Encoding Latin1 = Encoding.Latin1;

        private readonly string _locale;
        private readonly bool _useSocks;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private volatile IReadOnlyDictionary<string, string> _extraHeaders;
        private int _disposed;

        private LocaleHandshakeProxy(string locale, bool useSocks)
        {
            _locale = locale;
            _useSocks = useSocks;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        /// <summary>
        /// Listening port on 127.0.0.1.
        /// </summary>
        public int Port { get; }

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
        /// Starts a handshake proxy when <paramref name="locale"/> is set and
        /// the caller did not already supply a proxy.
        /// </summary>
        /// <param name="locale">Context locale, or <see langword="null"/>.</param>
        /// <param name="userProxy">Caller-supplied proxy, or <see langword="null"/>.</param>
        /// <param name="effectiveProxy">Proxy to pass to createContext.</param>
        /// <returns>The proxy to dispose with the context, or <see langword="null"/>.</returns>
        internal static LocaleHandshakeProxy TryStart(string locale, Proxy userProxy, out Proxy effectiveProxy)
            => TryStart(locale, userProxy, force: false, out effectiveProxy);

        /// <summary>
        /// Starts a handshake proxy when <paramref name="locale"/> is set,
        /// <paramref name="force"/> is <see langword="true"/>, and the caller
        /// did not already supply a proxy.
        /// </summary>
        /// <param name="locale">Context locale, or <see langword="null"/>.</param>
        /// <param name="userProxy">Caller-supplied proxy, or <see langword="null"/>.</param>
        /// <param name="force">Start even when no locale is configured.</param>
        /// <param name="effectiveProxy">Proxy to pass to createContext.</param>
        /// <returns>The proxy to dispose with the context, or <see langword="null"/>.</returns>
        internal static LocaleHandshakeProxy TryStart(string locale, Proxy userProxy, bool force, out Proxy effectiveProxy)
            => TryStart(locale, userProxy, force, bypassLoopback: true, out effectiveProxy);

        /// <summary>
        /// Starts a handshake proxy when <paramref name="locale"/> is set,
        /// <paramref name="force"/> is <see langword="true"/>, and the caller
        /// did not already supply a proxy.
        /// </summary>
        /// <param name="locale">Context locale, or <see langword="null"/>.</param>
        /// <param name="userProxy">Caller-supplied proxy, or <see langword="null"/>.</param>
        /// <param name="force">Start even when no locale is configured.</param>
        /// <param name="bypassLoopback">
        /// When <see langword="true"/>, localhost bypasses the proxy (Chromium uses
        /// Fetch to rewrite loopback WebSocket handshakes). WebKit Network
        /// interception does not rewrite WebSocket upgrades, so WebKit must pass
        /// <see langword="false"/> or localhost WS keeps the browser default
        /// <c>Accept-Language</c>.
        /// </param>
        /// <param name="effectiveProxy">Proxy to pass to createContext.</param>
        /// <returns>The proxy to dispose with the context, or <see langword="null"/>.</returns>
        internal static LocaleHandshakeProxy TryStart(
            string locale,
            Proxy userProxy,
            bool force,
            bool bypassLoopback,
            out Proxy effectiveProxy)
            => TryStart(locale, userProxy, force, bypassLoopback, useSocks: false, out effectiveProxy);

        /// <summary>
        /// Starts a handshake proxy when <paramref name="locale"/> is set,
        /// <paramref name="force"/> is <see langword="true"/>, and the caller
        /// did not already supply a proxy.
        /// </summary>
        /// <param name="locale">Context locale, or <see langword="null"/>.</param>
        /// <param name="userProxy">Caller-supplied proxy, or <see langword="null"/>.</param>
        /// <param name="force">Start even when no locale is configured.</param>
        /// <param name="bypassLoopback">
        /// When <see langword="true"/>, localhost bypasses the proxy.
        /// </param>
        /// <param name="useSocks">
        /// When <see langword="true"/>, expose <c>socks5://</c> so WebKit keeps
        /// HTTP/2 ALPN (HTTP CONNECT proxies disable it on Linux libsoup).
        /// </param>
        /// <param name="effectiveProxy">Proxy to pass to createContext.</param>
        /// <returns>The proxy to dispose with the context, or <see langword="null"/>.</returns>
        internal static LocaleHandshakeProxy TryStart(
            string locale,
            Proxy userProxy,
            bool force,
            bool bypassLoopback,
            bool useSocks,
            out Proxy effectiveProxy)
        {
            effectiveProxy = userProxy;
            if (userProxy != null || (string.IsNullOrEmpty(locale) && !force))
            {
                return null;
            }

            LocaleHandshakeProxy handshake = new(locale, useSocks);
            string scheme = useSocks ? "socks5://" : "http://";
            effectiveProxy = new Proxy
            {
                Server = scheme + "127.0.0.1:" + handshake.Port.ToString(CultureInfo.InvariantCulture),

                // <loopback> expands in ProxySettings.ShouldBypass (shim / MITM).
                // Keep <-loopback> as a synonym there for older callers.
                Bypass = bypassLoopback ? "<loopback>" : null,
            };
            return handshake;
        }

        /// <summary>
        /// Updates extra HTTP headers stamped onto later WebSocket handshakes.
        /// </summary>
        /// <param name="headers">Merged extra headers, or <see langword="null"/>.</param>
        internal void SetExtraHeaders(IReadOnlyDictionary<string, string> headers)
        {
            if (headers == null || headers.Count == 0)
            {
                _extraHeaders = null;
                return;
            }

            // Snapshot so later page/context map replacement cannot clear the
            // proxy mid-handshake (macOS WebKit ignores Network.setExtraHTTPHeaders
            // on upgrades and relies solely on this stamp).
            Dictionary<string, string> copy = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                copy[header.Key] = header.Value ?? string.Empty;
            }

            _extraHeaders = copy.Count == 0 ? null : copy;
        }

        private static async Task<byte[]> ReadHttpMessageAsync(HttpIO io, CancellationToken token)
        {
            MemoryStream buffer = new();
            while (true)
            {
                byte[] data = buffer.ToArray();
                int headerEnd = IndexOfHeaderEnd(data);
                if (headerEnd < 0)
                {
                    if (data.Length > 1024 * 1024)
                    {
                        throw new IOException("HTTP header too large.");
                    }

                    int n = await io.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (n == 0)
                    {
                        return data.Length == 0 ? null : data;
                    }

                    continue;
                }

                int contentLength = ParseContentLength(data, headerEnd);
                int total = headerEnd + 4 + Math.Max(contentLength, 0);
                while (data.Length < total)
                {
                    int n = await io.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (n == 0)
                    {
                        break;
                    }

                    data = buffer.ToArray();
                }

                data = buffer.ToArray();
                if (data.Length > total)
                {
                    io.Unread(data, total, data.Length - total);
                    byte[] exact = new byte[total];
                    Buffer.BlockCopy(data, 0, exact, 0, total);
                    return exact;
                }

                return data;
            }
        }

        private static int IndexOfHeaderEnd(byte[] data)
        {
            for (int i = 0; i + 3 < data.Length; i++)
            {
                if (data[i] == (byte)'\r'
                    && data[i + 1] == (byte)'\n'
                    && data[i + 2] == (byte)'\r'
                    && data[i + 3] == (byte)'\n')
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ParseContentLength(byte[] data, int headerEnd)
        {
            string headers = Latin1.GetString(data, 0, headerEnd);
            foreach (string line in headers.Split(HeaderSeparators, StringSplitOptions.None))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line.AsSpan(15).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int length)
                    && length > 0)
                {
                    return length;
                }
            }

            return 0;
        }

        /// <summary>
        /// <see cref="ReadHttpMessageAsync"/> stops at headers when there is no
        /// Content-Length (typical chunked Kestrel responses). Remaining body
        /// bytes must be tunneled.
        /// </summary>
        private static bool IsChunkedOrUnsized(string responseText)
        {
            if (string.IsNullOrEmpty(responseText))
            {
                return false;
            }

            int headerEnd = responseText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string headers = headerEnd < 0 ? responseText : responseText.Substring(0, headerEnd);
            if (headers.Contains("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase)
                && headers.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !headers.Contains("Content-Length:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsConnect(byte[] message)
            => message != null
                && message.Length >= 8
                && message[0] == (byte)'C'
                && message[1] == (byte)'O'
                && message[2] == (byte)'N'
                && message[3] == (byte)'N';

        private static bool IsWebSocketUpgrade(byte[] message)
        {
            if (message == null)
            {
                return false;
            }

            string text = Latin1.GetString(message);
            return text.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLoopbackHost(string host)
            => !string.IsNullOrEmpty(host)
                && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("127.0.0.1", StringComparison.Ordinal)
                    || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("::1", StringComparison.Ordinal));

        private static bool TryParseAuthority(string target, out string host, out int port)
        {
            host = null;
            port = 80;
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

                host = uri.Host;
                port = uri.IsDefaultPort
                    ? (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                        || uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
                    : uri.Port;
                return !string.IsNullOrEmpty(host);
            }

            int colon = target.LastIndexOf(':');
            if (colon > 0
                && int.TryParse(target.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                && parsed > 0)
            {
                host = target.Substring(0, colon).Trim('[', ']');
                port = parsed;
                return !string.IsNullOrEmpty(host);
            }

            host = target;
            return !string.IsNullOrEmpty(host);
        }

        private static bool TryParseRequestTarget(byte[] message, out string target)
        {
            target = null;
            if (message == null)
            {
                return false;
            }

            string text = Latin1.GetString(message);
            int lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
            string requestLine = lineEnd < 0 ? text : text.Substring(0, lineEnd);
            string[] parts = requestLine.Split(' ');
            if (parts.Length < 2)
            {
                return false;
            }

            target = parts[1];
            if (target.StartsWith('/'))
            {
                int hostIndex = text.IndexOf("\r\nHost:", StringComparison.OrdinalIgnoreCase);
                if (hostIndex < 0)
                {
                    return false;
                }

                int valueStart = hostIndex + 7;
                int valueEnd = text.IndexOf("\r\n", valueStart, StringComparison.Ordinal);
                if (valueEnd < 0)
                {
                    return false;
                }

                target = text.Substring(valueStart, valueEnd - valueStart).Trim() + target;
            }

            return !string.IsNullOrEmpty(target);
        }

        private static byte[] ToOriginForm(byte[] message)
        {
            string text = Latin1.GetString(message);
            int lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
            if (lineEnd < 0)
            {
                return message;
            }

            string requestLine = text.Substring(0, lineEnd);
            string[] parts = requestLine.Split(' ');
            if (parts.Length < 2)
            {
                return message;
            }

            string uri = parts[1];
            if (!uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !uri.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                && !uri.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return StripProxyHeaders(message);
            }

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri parsed))
            {
                return StripProxyHeaders(message);
            }

            string path = string.IsNullOrEmpty(parsed.PathAndQuery) ? "/" : parsed.PathAndQuery;
            string rewritten = parts[0] + " " + path;
            if (parts.Length > 2)
            {
                rewritten += " " + string.Join(" ", parts, 2, parts.Length - 2);
            }

            return StripProxyHeaders(Latin1.GetBytes(string.Concat(rewritten, text.AsSpan(lineEnd))));
        }

        private static byte[] StripProxyHeaders(byte[] message)
        {
            string text = Latin1.GetString(message);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return message;
            }

            StringBuilder builder = new();
            foreach (string line in text.Substring(0, headerEnd).Split(HeaderSeparators, StringSplitOptions.None))
            {
                if (line.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("\r\n");
                }

                builder.Append(line);
            }

            builder.Append(text.AsSpan(headerEnd));
            return Latin1.GetBytes(builder.ToString());
        }

        private static byte[] RewriteAcceptLanguage(byte[] message, string locale)
        {
            string text = Latin1.GetString(message);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return message;
            }

            string[] lines = text.Substring(0, headerEnd).Split(HeaderSeparators, StringSplitOptions.None);
            bool found = false;
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }

                if (!lines[i].Substring(0, colon).Trim().Equals("Accept-Language", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string existing = lines[i].Substring(colon + 1).Trim();
                if (existing.Contains(locale, StringComparison.OrdinalIgnoreCase))
                {
                    return message;
                }

                lines[i] = "Accept-Language: " + locale;
                found = true;
                break;
            }

            StringBuilder builder = new();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("\r\n");
                }

                builder.Append(lines[i]);
            }

            if (!found)
            {
                builder.Append("\r\nAccept-Language: ").Append(locale);
            }

            builder.Append(text.AsSpan(headerEnd));
            return Latin1.GetBytes(builder.ToString());
        }

        private static byte[] RewriteExtraHeaders(byte[] message, IReadOnlyDictionary<string, string> extra)
        {
            if (extra == null || extra.Count == 0 || message == null)
            {
                return message;
            }

            string text = Latin1.GetString(message);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return message;
            }

            string[] lines = text.Substring(0, headerEnd).Split(HeaderSeparators, StringSplitOptions.None);
            HashSet<string> replaced = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in extra)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    int colon = lines[i].IndexOf(':');
                    if (colon < 0)
                    {
                        continue;
                    }

                    if (!lines[i].Substring(0, colon).Trim().Equals(header.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    lines[i] = header.Key + ": " + (header.Value ?? string.Empty);
                    replaced.Add(header.Key);
                    break;
                }
            }

            StringBuilder builder = new();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("\r\n");
                }

                builder.Append(lines[i]);
            }

            foreach (KeyValuePair<string, string> header in extra)
            {
                if (string.IsNullOrEmpty(header.Key) || replaced.Contains(header.Key))
                {
                    continue;
                }

                builder.Append("\r\n").Append(header.Key).Append(": ").Append(header.Value ?? string.Empty);
            }

            builder.Append(text.AsSpan(headerEnd));
            return Latin1.GetBytes(builder.ToString());
        }

        private static async Task TunnelAsync(HttpIO client, HttpIO server, CancellationToken token)
        {
            await client.FlushUnreadAsync(server.Stream, token).ConfigureAwait(false);
            await server.FlushUnreadAsync(client.Stream, token).ConfigureAwait(false);

            // Half-close aware tunnel: when one side EOFs (e.g. macOS cfNetwork after a
            // client WebSocket close frame), shut down only that write direction and keep
            // copying the opposite way so the close echo can still reach the browser.
            // Task.WhenAny + dispose aborted application close codes as 1006.
            using CancellationTokenSource tunnelCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            Task copyA = CopyAndShutdownAsync(client.Stream, server.Stream, tunnelCts.Token);
            Task copyB = CopyAndShutdownAsync(server.Stream, client.Stream, tunnelCts.Token);
            try
            {
                await Task.WhenAll(copyA, copyB).ConfigureAwait(false);
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

        private static async Task<bool> SocksHandshakeAsync(Stream stream, CancellationToken token)
        {
            int ver = await ReadSocksByteAsync(stream, token).ConfigureAwait(false);
            int nmethods = await ReadSocksByteAsync(stream, token).ConfigureAwait(false);
            if (ver != 0x05 || nmethods < 0)
            {
                return false;
            }

            for (int i = 0; i < nmethods; i++)
            {
                if (await ReadSocksByteAsync(stream, token).ConfigureAwait(false) < 0)
                {
                    return false;
                }
            }

            await stream.WriteAsync(new byte[] { 0x05, 0x00 }, token).ConfigureAwait(false);
            return true;
        }

        private static async Task<(string Host, int Port)?> TryReadSocksConnectAsync(
            Stream stream,
            CancellationToken token)
        {
            int ver = await ReadSocksByteAsync(stream, token).ConfigureAwait(false);
            int cmd = await ReadSocksByteAsync(stream, token).ConfigureAwait(false);
            int rsv = await ReadSocksByteAsync(stream, token).ConfigureAwait(false);
            int atyp = await ReadSocksByteAsync(stream, token).ConfigureAwait(false);
            if (ver != 0x05 || cmd != 0x01 || rsv != 0x00)
            {
                return null;
            }

            string host;
            if (atyp == 0x01)
            {
                byte[] addr = new byte[4];
                if (!await ReadSocksExactAsync(stream, addr, token).ConfigureAwait(false))
                {
                    return null;
                }

                host = new IPAddress(addr).ToString();
            }
            else if (atyp == 0x04)
            {
                byte[] addr = new byte[16];
                if (!await ReadSocksExactAsync(stream, addr, token).ConfigureAwait(false))
                {
                    return null;
                }

                host = new IPAddress(addr).ToString();
            }
            else if (atyp == 0x03)
            {
                int len = await ReadSocksByteAsync(stream, token).ConfigureAwait(false);
                if (len <= 0)
                {
                    return null;
                }

                byte[] name = new byte[len];
                if (!await ReadSocksExactAsync(stream, name, token).ConfigureAwait(false))
                {
                    return null;
                }

                host = Encoding.ASCII.GetString(name);
            }
            else
            {
                return null;
            }

            byte[] portBytes = new byte[2];
            if (!await ReadSocksExactAsync(stream, portBytes, token).ConfigureAwait(false))
            {
                return null;
            }

            int port = (portBytes[0] << 8) | portBytes[1];
            return (host, port);
        }

        private static async Task WriteSocksSuccessAsync(Stream stream, CancellationToken token)
        {
            byte[] reply =
            {
                0x05, 0x00, 0x00, 0x01,
                127, 0, 0, 1,
                0x00, 0x00,
            };
            await stream.WriteAsync(reply, token).ConfigureAwait(false);
        }

        private static async Task WriteSocksFailureAsync(Stream stream, CancellationToken token)
        {
            byte[] refused =
            {
                0x05, 0x05, 0x00, 0x01,
                127, 0, 0, 1,
                0x00, 0x00,
            };
            try
            {
                await stream.WriteAsync(refused, token).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
        }

        private static async Task<int> ReadSocksByteAsync(Stream stream, CancellationToken token)
        {
            byte[] one = new byte[1];
            int n = await stream.ReadAsync(one.AsMemory(0, 1), token).ConfigureAwait(false);
            return n == 0 ? -1 : one[0];
        }

        private static async Task<bool> ReadSocksExactAsync(Stream stream, byte[] buffer, CancellationToken token)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token)
                    .ConfigureAwait(false);
                if (n == 0)
                {
                    return false;
                }

                offset += n;
            }

            return true;
        }

        private static async Task<TcpClient> ConnectAsync(string host, int port, CancellationToken token)
        {
            // macOS WebKit routes loopback WS via local.playwright (see
            // WebKitMacLocaleWebSocketShim); map back to localhost for the
            // real test-server socket, matching ClientCertificatesProxy.
            string connectHost = ClientCertificatesProxy.RewriteToLocalhostIfNeeded(host);
            if (string.Equals(connectHost, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(connectHost, "127.0.0.1", StringComparison.Ordinal))
            {
                TcpClient ipv4 = new(AddressFamily.InterNetwork) { NoDelay = true };
                try
                {
                    await ipv4.ConnectAsync(IPAddress.Loopback, port, token).ConfigureAwait(false);
                    return ipv4;
                }
                catch
                {
                    ipv4.Dispose();
                    throw;
                }
            }

            TcpClient server = new() { NoDelay = true };
            try
            {
                await server.ConnectAsync(connectHost, port, token).ConfigureAwait(false);
                return server;
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        /// <summary>
        /// After <see cref="ConnectAsync"/> maps <c>local.playwright*</c> to
        /// loopback, rewrite the <c>Host</c> header to the public loopback host
        /// so origin servers see localhost / 127.0.0.1 / ::1.
        /// </summary>
        /// <param name="message">HTTP request bytes.</param>
        /// <returns>Request with Host rewritten when needed.</returns>
        private static byte[] RewriteFakeLoopbackHostHeader(byte[] message)
        {
            if (message == null || message.Length == 0)
            {
                return message;
            }

            string text = Latin1.GetString(message);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return message;
            }

            string[] lines = text.Substring(0, headerEnd).Split(HeaderSeparators, StringSplitOptions.None);
            bool changed = false;
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }

                if (!lines[i].Substring(0, colon).Trim().Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = lines[i].Substring(colon + 1).Trim();
                string host;
                int port = 0;
                if (TryParseAuthority(value, out host, out port))
                {
                    // Host: name:port
                }
                else
                {
                    host = value.Trim('[', ']');
                }

                if (!WebKitMacLocaleWebSocketShim.IsFakeLoopbackHost(host))
                {
                    break;
                }

                string publicHost = WebKitMacLocaleWebSocketShim.ToPublicHost(host);
                string newHost = port > 0
                    ? publicHost + ":" + port.ToString(CultureInfo.InvariantCulture)
                    : publicHost;
                lines[i] = "Host: " + newHost;
                changed = true;
                break;
            }

            if (!changed)
            {
                return message;
            }

            StringBuilder builder = new();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("\r\n");
                }

                builder.Append(lines[i]);
            }

            builder.Append(text.AsSpan(headerEnd));
            return Latin1.GetBytes(builder.ToString());
        }

        private byte[] RewriteHandshake(byte[] request)
        {
            if (!string.IsNullOrEmpty(_locale))
            {
                request = RewriteAcceptLanguage(request, _locale);
            }

            request = RewriteFakeLoopbackHostHeader(request);
            return RewriteExtraHeaders(request, _extraHeaders);
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

                client.NoDelay = true;
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream clientStream = client.GetStream())
                {
                    if (_useSocks)
                    {
                        await HandleSocksClientAsync(clientStream).ConfigureAwait(false);
                        return;
                    }

                    await HandleStreamAsync(new HttpIO(clientStream), predetermined: null).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task HandleSocksClientAsync(NetworkStream clientStream)
        {
            CancellationToken token = _cts.Token;
            if (!await SocksHandshakeAsync(clientStream, token).ConfigureAwait(false))
            {
                return;
            }

            (string Host, int Port)? target = await TryReadSocksConnectAsync(clientStream, token)
                .ConfigureAwait(false);
            if (target == null)
            {
                await WriteSocksFailureAsync(clientStream, token).ConfigureAwait(false);
                return;
            }

            TcpClient server = null;
            try
            {
                server = await ConnectAsync(target.Value.Host, target.Value.Port, token).ConfigureAwait(false);
            }
            catch (IOException)
            {
                await WriteSocksFailureAsync(clientStream, token).ConfigureAwait(false);
                return;
            }
            catch (SocketException)
            {
                await WriteSocksFailureAsync(clientStream, token).ConfigureAwait(false);
                return;
            }

            try
            {
                await WriteSocksSuccessAsync(clientStream, token).ConfigureAwait(false);
                HttpIO client = new HttpIO(clientStream);
                HttpIO serverIo = new HttpIO(server.GetStream());

                // HTTPS / WSS: first byte is TLS handshake (0x16). Tunnel opaquely
                // so HTTP/2 ALPN stays between browser and origin. Cleartext WS
                // upgrades are rewritten like the HTTP-proxy path.
                int first = await ReadSocksByteAsync(clientStream, token).ConfigureAwait(false);
                if (first < 0)
                {
                    return;
                }

                client.Unread(new byte[] { (byte)first }, 0, 1);
                if (first == 0x16)
                {
                    await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                    return;
                }

                // Drop the SOCKS-origin socket; HandleStreamAsync opens its own
                // for the cleartext rewrite path.
                server.Dispose();
                server = null;
                await HandleStreamAsync(
                        client,
                        predetermined: Tuple.Create(target.Value.Host, target.Value.Port))
                    .ConfigureAwait(false);
            }
            finally
            {
                server?.Dispose();
            }
        }

        private async Task HandleStreamAsync(HttpIO client, Tuple<string, int> predetermined)
        {
            CancellationToken token = _cts.Token;
            TcpClient server = null;
            HttpIO serverIo = null;
            string serverHost = predetermined?.Item1;
            int serverPort = predetermined?.Item2 ?? 80;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    byte[] request = await ReadHttpMessageAsync(client, token).ConfigureAwait(false);
                    if (request == null || request.Length == 0)
                    {
                        return;
                    }

                    if (IsConnect(request))
                    {
                        if (!TryParseRequestTarget(request, out string connectTarget)
                            || !TryParseAuthority(connectTarget, out string host, out int port))
                        {
                            return;
                        }

                        server?.Dispose();
                        server = await ConnectAsync(host, port, token).ConfigureAwait(false);
                        serverIo = new HttpIO(server.GetStream());
                        byte[] established = Latin1.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                        await client.Stream.WriteAsync(established, token).ConfigureAwait(false);

                        // Plain ws:// via Mac shim uses CONNECT to local.playwright*
                        // then an HTTP Upgrade on the tunnel. Rewrite Host on that
                        // first request so the origin sees localhost/127.0.0.1.
                        // TLS (wss/https) ClientHello must stay an opaque tunnel —
                        // parsing it as HTTP hangs (IgnoreHTTPSErrors / cookies).
                        if (WebKitMacLocaleWebSocketShim.IsFakeLoopbackHost(host))
                        {
                            // Peek one byte: TLS handshake records start with 0x16.
                            byte[] peek = new byte[1];
                            int peeked = await client.Stream.ReadAsync(peek.AsMemory(0, 1), token)
                                .ConfigureAwait(false);
                            if (peeked == 0)
                            {
                                return;
                            }

                            if (peek[0] == 0x16)
                            {
                                client.Unread(peek, 0, 1);
                                await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                                return;
                            }

                            client.Unread(peek, 0, 1);
                            byte[] tunneled = await ReadHttpMessageAsync(client, token).ConfigureAwait(false);
                            if (tunneled == null || tunneled.Length == 0)
                            {
                                return;
                            }

                            if (IsWebSocketUpgrade(tunneled))
                            {
                                tunneled = RewriteHandshake(tunneled);
                            }
                            else
                            {
                                tunneled = RewriteFakeLoopbackHostHeader(tunneled);
                            }

                            await serverIo.Stream.WriteAsync(tunneled, token).ConfigureAwait(false);
                            await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                            return;
                        }

                        // CONNECT is always an opaque tunnel (https/wss), including
                        // loopback on non-443 test-server ports. Parsing the post-CONNECT
                        // bytes as HTTP hangs on the TLS ClientHello (WebKit HTTPS
                        // cookie / IgnoreHTTPSErrors tests).
                        await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                        return;
                    }

                    if (IsWebSocketUpgrade(request))
                    {
                        request = RewriteHandshake(request);
                    }

                    if (predetermined == null)
                    {
                        if (!TryParseRequestTarget(request, out string target)
                            || !TryParseAuthority(target, out string host, out int port))
                        {
                            return;
                        }

                        if (server == null || !string.Equals(serverHost, host, StringComparison.OrdinalIgnoreCase) || serverPort != port)
                        {
                            server?.Dispose();
                            server = await ConnectAsync(host, port, token).ConfigureAwait(false);
                            serverIo = new HttpIO(server.GetStream());
                            serverHost = host;
                            serverPort = port;
                        }

                        if (!IsLoopbackHost(host))
                        {
                            byte[] remote = ToOriginForm(request);
                            await serverIo.Stream.WriteAsync(remote, token).ConfigureAwait(false);
                            await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                            return;
                        }
                    }
                    else if (server == null)
                    {
                        server = await ConnectAsync(serverHost, serverPort, token).ConfigureAwait(false);
                        serverIo = new HttpIO(server.GetStream());
                    }

                    byte[] forwarded = predetermined == null ? ToOriginForm(request) : StripProxyHeaders(request);

                    // SOCKS predetermined targets keep the wire Host (local.playwright*).
                    // Always rewrite to the public loopback host before the origin hop.
                    forwarded = RewriteFakeLoopbackHostHeader(forwarded);

                    await serverIo.Stream.WriteAsync(forwarded, token).ConfigureAwait(false);
                    if (IsWebSocketUpgrade(request))
                    {
                        await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                        return;
                    }

                    byte[] response = await ReadHttpMessageAsync(serverIo, token).ConfigureAwait(false);
                    if (response == null)
                    {
                        return;
                    }

                    await client.Stream.WriteAsync(response, token).ConfigureAwait(false);
                    string responseText = Latin1.GetString(response);

                    // ReadHttpMessageAsync only honors Content-Length. Chunked (or
                    // otherwise unsized) bodies remain on the socket — tunnel them
                    // instead of closing after headers (Connection: close + chunked
                    // was surfacing as APIRequest "socket hang up").
                    if (IsChunkedOrUnsized(responseText))
                    {
                        await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                        return;
                    }

                    if (responseText.Contains("Connection: close", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
            }
            finally
            {
                server?.Dispose();
            }
        }

        private sealed class HttpIO
        {
            private byte[] _unread;
            private int _unreadOffset;
            private int _unreadCount;

            internal HttpIO(NetworkStream stream)
            {
                Stream = stream;
            }

            internal NetworkStream Stream { get; }

            internal void Unread(byte[] data, int offset, int count)
            {
                _unread = data;
                _unreadOffset = offset;
                _unreadCount = count;
            }

            internal async Task<int> ReadAsync(MemoryStream destination, CancellationToken token)
            {
                if (_unreadCount > 0)
                {
                    destination.Write(_unread.AsSpan(_unreadOffset, _unreadCount));
                    int n = _unreadCount;
                    _unread = null;
                    _unreadOffset = 0;
                    _unreadCount = 0;
                    return n;
                }

                byte[] chunk = new byte[4096];
                int read = await Stream.ReadAsync(chunk.AsMemory(0, chunk.Length), token).ConfigureAwait(false);
                if (read > 0)
                {
                    destination.Write(chunk.AsSpan(0, read));
                }

                return read;
            }

            internal async Task FlushUnreadAsync(NetworkStream destination, CancellationToken token)
            {
                if (_unreadCount <= 0)
                {
                    return;
                }

                await destination.WriteAsync(_unread.AsMemory(_unreadOffset, _unreadCount), token).ConfigureAwait(false);
                _unread = null;
                _unreadOffset = 0;
                _unreadCount = 0;
            }
        }
    }
}
