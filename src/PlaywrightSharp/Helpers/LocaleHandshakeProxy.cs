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

namespace PlaywrightSharp.Helpers
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

        private readonly string _locale;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private volatile IReadOnlyDictionary<string, string> _extraHeaders;
        private int _disposed;

        private LocaleHandshakeProxy(string locale)
        {
            _locale = locale;
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
        {
            effectiveProxy = userProxy;
            if (userProxy != null || (string.IsNullOrEmpty(locale) && !force))
            {
                return null;
            }

            LocaleHandshakeProxy handshake = new(locale);
            effectiveProxy = new Proxy
            {
                Server = "http://127.0.0.1:" + handshake.Port.ToString(CultureInfo.InvariantCulture),
                Bypass = "<-loopback>",
            };
            return handshake;
        }

        /// <summary>
        /// Updates extra HTTP headers stamped onto later WebSocket handshakes.
        /// </summary>
        /// <param name="headers">Merged extra headers, or <see langword="null"/>.</param>
        internal void SetExtraHeaders(IReadOnlyDictionary<string, string> headers)
            => _extraHeaders = headers;

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
            string headers = Encoding.ASCII.GetString(data, 0, headerEnd);
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

            string text = Encoding.ASCII.GetString(message);
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

            string text = Encoding.ASCII.GetString(message);
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
            string text = Encoding.ASCII.GetString(message);
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

            return StripProxyHeaders(Encoding.ASCII.GetBytes(string.Concat(rewritten, text.AsSpan(lineEnd))));
        }

        private static byte[] StripProxyHeaders(byte[] message)
        {
            string text = Encoding.ASCII.GetString(message);
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
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static byte[] RewriteAcceptLanguage(byte[] message, string locale)
        {
            string text = Encoding.ASCII.GetString(message);
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
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static byte[] RewriteExtraHeaders(byte[] message, IReadOnlyDictionary<string, string> extra)
        {
            if (extra == null || extra.Count == 0 || message == null)
            {
                return message;
            }

            string text = Encoding.ASCII.GetString(message);
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
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static async Task TunnelAsync(HttpIO client, HttpIO server, CancellationToken token)
        {
            await client.FlushUnreadAsync(server.Stream, token).ConfigureAwait(false);
            await server.FlushUnreadAsync(client.Stream, token).ConfigureAwait(false);
            Task copyA = client.Stream.CopyToAsync(server.Stream, token);
            Task copyB = server.Stream.CopyToAsync(client.Stream, token);
            try
            {
                await Task.WhenAny(copyA, copyB).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static async Task<TcpClient> ConnectAsync(string host, int port, CancellationToken token)
        {
            TcpClient server = new() { NoDelay = true };
            try
            {
                await server.ConnectAsync(host, port, token).ConfigureAwait(false);
                return server;
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        private byte[] RewriteHandshake(byte[] request)
        {
            if (!string.IsNullOrEmpty(_locale))
            {
                request = RewriteAcceptLanguage(request, _locale);
            }

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
                        byte[] established = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                        await client.Stream.WriteAsync(established, token).ConfigureAwait(false);

                        // TLS (wss/https) is tunneled; ws:// handshakes stay HTTP.
                        if (port == 443 || !IsLoopbackHost(host))
                        {
                            await TunnelAsync(client, serverIo, token).ConfigureAwait(false);
                            return;
                        }

                        await HandleStreamAsync(client, Tuple.Create(host, port)).ConfigureAwait(false);
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
                    string responseText = Encoding.ASCII.GetString(response);
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
