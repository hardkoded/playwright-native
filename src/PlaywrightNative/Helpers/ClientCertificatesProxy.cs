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
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>socksClientCertificatesInterceptor</c>: a SOCKS5 MITM
    /// that injects matching client certificates into the outbound TLS
    /// handshake and reports TLS failures as an HTML error page.
    /// </summary>
    internal sealed class ClientCertificatesProxy : IDisposable
    {
        private static readonly object DummyLock = new();
        private static X509Certificate2 _dummyCert;

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly Dictionary<string, X509Certificate2> _certs;
        private readonly bool _ignoreHttpsErrors;
        private readonly Proxy _userProxy;
        private int _disposed;

        private ClientCertificatesProxy(
            Dictionary<string, X509Certificate2> certs,
            bool ignoreHttpsErrors,
            Proxy userProxy)
        {
            _certs = certs;
            _ignoreHttpsErrors = ignoreHttpsErrors;
            _userProxy = userProxy;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BrowserProxy = new Proxy
            {
                Server = "socks5://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture),
            };
            _acceptLoop = AcceptLoopAsync();
        }

        private enum BrowserProxyKind
        {
            Socks,
            HttpsConnect,
            HttpForward,
        }

        /// <summary>
        /// Listening port on 127.0.0.1.
        /// </summary>
        internal int Port { get; }

        /// <summary>
        /// Official <c>proxyOverride</c> passed to the browser (SOCKS5).
        /// </summary>
        internal Proxy BrowserProxy { get; }

        /// <summary>
        /// HTTP CONNECT view of the same listener. Darwin CFNetwork excludes
        /// loopback from SOCKS5; macOS WebKit uses this instead of
        /// <see cref="BrowserProxy"/>. Linux keeps SOCKS so HTTP/2 ALPN works.
        /// </summary>
        internal Proxy HttpBrowserProxy => new Proxy
        {
            Server = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture),
        };

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
            HashSet<X509Certificate2> unique = new();
            foreach (X509Certificate2 cert in _certs.Values)
            {
                if (unique.Add(cert))
                {
                    cert.Dispose();
                }
            }

            _certs.Clear();
        }

        /// <summary>
        /// Starts the interceptor when <paramref name="certificates"/> is
        /// non-empty. Throws official validation / load errors.
        /// </summary>
        /// <param name="certificates">Configured certificates.</param>
        /// <param name="ignoreHttpsErrors">User <c>ignoreHTTPSErrors</c>.</param>
        /// <param name="userProxy">Caller proxy used for the outbound hop.</param>
        /// <returns>The started proxy.</returns>
        internal static ClientCertificatesProxy Create(
            IEnumerable<ClientCertificate> certificates,
            bool ignoreHttpsErrors,
            Proxy userProxy)
        {
            ClientCertificateHelper.Verify(certificates);
            EnsureDummyCert();
            Dictionary<string, X509Certificate2> loaded =
                ClientCertificateHelper.LoadAllForBrowser(certificates);
            return new ClientCertificatesProxy(loaded, ignoreHttpsErrors, userProxy);
        }

        /// <summary>
        /// Starts the interceptor when certificates are present; otherwise
        /// leaves <paramref name="browserProxy"/> as the user proxy.
        /// </summary>
        /// <param name="certificates">Configured certificates.</param>
        /// <param name="ignoreHttpsErrors">User <c>ignoreHTTPSErrors</c>.</param>
        /// <param name="userProxy">Caller-supplied proxy, or <see langword="null"/>.</param>
        /// <param name="browserProxy">Proxy the browser should use.</param>
        /// <returns>The interceptor, or <see langword="null"/>.</returns>
        internal static ClientCertificatesProxy TryStart(
            IEnumerable<ClientCertificate> certificates,
            bool ignoreHttpsErrors,
            Proxy userProxy,
            out Proxy browserProxy)
        {
            browserProxy = userProxy;
            if (!ClientCertificateHelper.HasAny(certificates))
            {
                return null;
            }

            ClientCertificatesProxy proxy = Create(certificates, ignoreHttpsErrors, userProxy);
            browserProxy = proxy.BrowserProxy;
            return proxy;
        }

        internal static string RewriteToLocalhostIfNeeded(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return host;
            }

            // Mac WS shim uses distinct fake hosts so HAR / IWebSocket.Url can
            // restore localhost vs 127.0.0.1 vs ::1 after the proxy hop.
            // Map local.playwright → 127.0.0.1 (not "localhost"): test HTTPS
            // fixtures bind IPAddress.Loopback (IPv4 only), and Darwin
            // localhost often prefers ::1 first → connect fails → CFNetwork
            // reports "Could not connect" instead of the MITM error page
            // (BrowserShouldHaveIgnoreHttpsErrorsFalseByDefault).
            if (string.Equals(host, WebKitMacLocaleWebSocketShim.FakeLoopbackHost, StringComparison.OrdinalIgnoreCase))
            {
                return "127.0.0.1";
            }

            if (string.Equals(host, WebKitMacLocaleWebSocketShim.FakeIpv4LoopbackHost, StringComparison.OrdinalIgnoreCase))
            {
                return "127.0.0.1";
            }

            if (string.Equals(host, WebKitMacLocaleWebSocketShim.FakeIpv6LoopbackHost, StringComparison.OrdinalIgnoreCase))
            {
                return "::1";
            }

            return host;
        }

        internal static IReadOnlyList<string> ParseAlpnFromClientHello(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 6 || buffer[0] != 0x16)
            {
                return null;
            }

            int offset = 5;
            if (offset >= buffer.Length || buffer[offset] != 0x01)
            {
                return null;
            }

            offset += 4;
            offset += 2;
            offset += 32;
            if (offset >= buffer.Length)
            {
                return null;
            }

            int sessionIdLength = buffer[offset];
            offset += 1 + sessionIdLength;
            if (offset + 2 > buffer.Length)
            {
                return null;
            }

            int cipherSuitesLength = (buffer[offset] << 8) | buffer[offset + 1];
            offset += 2 + cipherSuitesLength;
            if (offset >= buffer.Length)
            {
                return null;
            }

            int compressionMethodsLength = buffer[offset];
            offset += 1 + compressionMethodsLength;
            if (offset + 2 > buffer.Length)
            {
                return null;
            }

            int extensionsLength = (buffer[offset] << 8) | buffer[offset + 1];
            offset += 2;
            int extensionsEnd = offset + extensionsLength;
            if (extensionsEnd > buffer.Length)
            {
                return null;
            }

            while (offset + 4 <= extensionsEnd)
            {
                int extensionType = (buffer[offset] << 8) | buffer[offset + 1];
                int extensionLength = (buffer[offset + 2] << 8) | buffer[offset + 3];
                offset += 4;
                if (offset + extensionLength > extensionsEnd)
                {
                    return null;
                }

                if (extensionType == 16)
                {
                    return ParseAlpnExtension(buffer, offset, extensionLength);
                }

                offset += extensionLength;
            }

            return null;
        }

        private static IReadOnlyList<string> ParseAlpnExtension(byte[] buffer, int offset, int length)
        {
            if (length < 2)
            {
                return null;
            }

            int listLength = (buffer[offset] << 8) | buffer[offset + 1];
            if (listLength != length - 2)
            {
                return null;
            }

            List<string> protocols = new();
            int cursor = offset + 2;
            int end = offset + length;
            while (cursor < end)
            {
                int protocolLength = buffer[cursor];
                cursor += 1;
                if (cursor + protocolLength > end)
                {
                    break;
                }

                protocols.Add(Encoding.ASCII.GetString(buffer, cursor, protocolLength));
                cursor += protocolLength;
            }

            return protocols.Count > 0 ? protocols : null;
        }

        private static void EnsureDummyCert()
        {
            if (_dummyCert != null)
            {
                return;
            }

            lock (DummyLock)
            {
                if (_dummyCert != null)
                {
                    return;
                }

                using RSA key = RSA.Create(2048);
                CertificateRequest request = new CertificateRequest(
                    "CN=localhost",
                    key,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                        critical: true));
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                        critical: true));
                SubjectAlternativeNameBuilder san = new();
                san.AddDnsName("localhost");
                san.AddDnsName("local.playwright");
                san.AddIpAddress(IPAddress.Loopback);
                request.CertificateExtensions.Add(san.Build(critical: false));
                using X509Certificate2 created = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddYears(1));
                _dummyCert = ClientCertificateHelper.LoadPkcs12(
                    created.Export(X509ContentType.Pfx),
                    string.Empty);
            }
        }

        private static bool IsIpAddress(string host)
            => IPAddress.TryParse(host, out _);

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&#39;", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal);
        }

        private static async Task<BrowserProxyRequest?> NegotiateBrowserProxyAsync(
            NetworkStream browser,
            CancellationToken token)
        {
            byte[] peek = new byte[1];
            int n = await browser.ReadAsync(peek.AsMemory(0, 1), token).ConfigureAwait(false);
            if (n <= 0)
            {
                return null;
            }

            // SOCKS5 version byte vs HTTP method ASCII.
            if (peek[0] == 0x05)
            {
                if (!await SocksHandshakeAfterVersionAsync(browser, peek[0], token).ConfigureAwait(false))
                {
                    return null;
                }

                (string Host, int Port)? socks = await TryReadSocksConnectAsync(browser, token)
                    .ConfigureAwait(false);
                return socks == null
                    ? null
                    : new BrowserProxyRequest(socks.Value.Host, socks.Value.Port, BrowserProxyKind.Socks, null);
            }

            return await TryReadHttpProxyRequestAsync(browser, peek[0], token).ConfigureAwait(false);
        }

        private static async Task<BrowserProxyRequest?> TryReadHttpProxyRequestAsync(
            Stream stream,
            byte firstByte,
            CancellationToken token)
        {
            using MemoryStream headerBuffer = new();
            headerBuffer.WriteByte(firstByte);
            byte[] chunk = new byte[1024];
            while (true)
            {
                byte[] soFar = headerBuffer.ToArray();
                string text = Encoding.ASCII.GetString(soFar);
                if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    break;
                }

                if (soFar.Length > 64 * 1024)
                {
                    return null;
                }

                int n = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), token).ConfigureAwait(false);
                if (n <= 0)
                {
                    return null;
                }

                await headerBuffer.WriteAsync(chunk.AsMemory(0, n), token).ConfigureAwait(false);
            }

            byte[] raw = headerBuffer.ToArray();
            string headerText = Encoding.ASCII.GetString(raw);
            int headerEnd = headerText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return null;
            }

            int lineEnd = headerText.IndexOf("\r\n", StringComparison.Ordinal);
            if (lineEnd <= 0)
            {
                return null;
            }

            string requestLine = headerText.Substring(0, lineEnd);
            string[] parts = requestLine.Split(' ');
            if (parts.Length < 2)
            {
                return null;
            }

            string method = parts[0];
            string target = parts[1];
            if (string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseHostPort(target, defaultPort: 443, out string host, out int port))
                {
                    return null;
                }

                byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                await stream.WriteAsync(ok, token).ConfigureAwait(false);
                return new BrowserProxyRequest(host, port, BrowserProxyKind.HttpsConnect, null);
            }

            // Cleartext absolute-form request: GET http://host/path HTTP/1.1
            if (!Uri.TryCreate(target, UriKind.Absolute, out Uri absolute)
                || (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            string pathAndQuery = string.IsNullOrEmpty(absolute.PathAndQuery) ? "/" : absolute.PathAndQuery;
            string version = parts.Length >= 3 ? parts[2] : "HTTP/1.1";
            string originLine = method + " " + pathAndQuery + " " + version;
            byte[] originRequest = Encoding.ASCII.GetBytes(string.Concat(originLine, headerText.AsSpan(lineEnd)));
            return new BrowserProxyRequest(
                absolute.IdnHost,
                absolute.IsDefaultPort ? (absolute.Scheme == Uri.UriSchemeHttps ? 443 : 80) : absolute.Port,
                BrowserProxyKind.HttpForward,
                originRequest);
        }

        private static bool TryParseHostPort(string target, int defaultPort, out string host, out int port)
        {
            host = null;
            port = defaultPort;
            if (string.IsNullOrEmpty(target))
            {
                return false;
            }

            if (target.StartsWith('['))
            {
                int close = target.IndexOf(']');
                if (close <= 1)
                {
                    return false;
                }

                host = target.Substring(1, close - 1);
                if (close + 1 < target.Length && target[close + 1] == ':')
                {
                    return int.TryParse(target.AsSpan(close + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out port);
                }

                return true;
            }

            int colon = target.LastIndexOf(':');
            if (colon <= 0)
            {
                host = target;
                return true;
            }

            host = target.Substring(0, colon);
            return int.TryParse(target.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port);
        }

        private static async Task<bool> SocksHandshakeAfterVersionAsync(
            Stream stream,
            byte version,
            CancellationToken token)
        {
            if (version != 0x05)
            {
                return false;
            }

            int nmethods = await ReadByteAsync(stream, token).ConfigureAwait(false);
            if (nmethods < 0)
            {
                return false;
            }

            for (int i = 0; i < nmethods; i++)
            {
                if (await ReadByteAsync(stream, token).ConfigureAwait(false) < 0)
                {
                    return false;
                }
            }

            await stream.WriteAsync(new byte[] { 0x05, 0x00 }, token).ConfigureAwait(false);
            return true;
        }

        private static async Task<bool> SocksHandshakeAsync(Stream stream, CancellationToken token)
        {
            int ver = await ReadByteAsync(stream, token).ConfigureAwait(false);
            int nmethods = await ReadByteAsync(stream, token).ConfigureAwait(false);
            if (ver != 0x05 || nmethods < 0)
            {
                return false;
            }

            byte[] methods = new byte[nmethods];
            if (!await ReadExactAsync(stream, methods, token).ConfigureAwait(false))
            {
                return false;
            }

            await stream.WriteAsync(new byte[] { 0x05, 0x00 }, token).ConfigureAwait(false);
            return true;
        }

        private static async Task<(string Host, int Port)?> TryReadSocksConnectAsync(
            Stream stream,
            CancellationToken token)
        {
            int ver = await ReadByteAsync(stream, token).ConfigureAwait(false);
            int cmd = await ReadByteAsync(stream, token).ConfigureAwait(false);
            int rsv = await ReadByteAsync(stream, token).ConfigureAwait(false);
            int atyp = await ReadByteAsync(stream, token).ConfigureAwait(false);
            if (ver != 0x05 || cmd != 0x01 || rsv != 0x00)
            {
                return null;
            }

            string host;
            if (atyp == 0x01)
            {
                byte[] addr = new byte[4];
                if (!await ReadExactAsync(stream, addr, token).ConfigureAwait(false))
                {
                    return null;
                }

                host = new IPAddress(addr).ToString();
            }
            else if (atyp == 0x04)
            {
                byte[] addr = new byte[16];
                if (!await ReadExactAsync(stream, addr, token).ConfigureAwait(false))
                {
                    return null;
                }

                host = new IPAddress(addr).ToString();
            }
            else if (atyp == 0x03)
            {
                int len = await ReadByteAsync(stream, token).ConfigureAwait(false);
                if (len <= 0)
                {
                    return null;
                }

                byte[] name = new byte[len];
                if (!await ReadExactAsync(stream, name, token).ConfigureAwait(false))
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
            if (!await ReadExactAsync(stream, portBytes, token).ConfigureAwait(false))
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

        private static async Task<int> ReadByteAsync(Stream stream, CancellationToken token)
        {
            byte[] one = new byte[1];
            int n = await stream.ReadAsync(one.AsMemory(0, 1), token).ConfigureAwait(false);
            return n == 0 ? -1 : one[0];
        }

        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
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

        /// <summary>
        /// Reads one complete TLS record (ClientHello) so AuthenticateAsServer
        /// never blocks on a truncated prefix through the Darwin HTTP CONNECT shim.
        /// </summary>
        private static async Task<byte[]> ReadTlsClientHelloAsync(Stream browser, CancellationToken token)
        {
            byte[] header = new byte[5];
            if (!await ReadExactAsync(browser, header, token).ConfigureAwait(false))
            {
                return null;
            }

            if (header[0] != 0x16)
            {
                // Not TLS — return whatever we have plus a small lookahead so
                // plaintext tunnels still see the first application bytes.
                byte[] extra = new byte[16 * 1024];
                int n = await browser.ReadAsync(extra.AsMemory(0, extra.Length), token).ConfigureAwait(false);
                if (n <= 0)
                {
                    return header;
                }

                byte[] combined = new byte[5 + n];
                Buffer.BlockCopy(header, 0, combined, 0, 5);
                Buffer.BlockCopy(extra, 0, combined, 5, n);
                return combined;
            }

            int length = (header[3] << 8) | header[4];
            if (length < 0 || length > 64 * 1024)
            {
                return header;
            }

            byte[] body = new byte[length];
            if (!await ReadExactAsync(browser, body, token).ConfigureAwait(false))
            {
                return null;
            }

            byte[] record = new byte[5 + length];
            Buffer.BlockCopy(header, 0, record, 0, 5);
            Buffer.BlockCopy(body, 0, record, 5, length);
            return record;
        }

        private static async Task PipeAsync(Stream a, Stream b, CancellationToken token)
        {
            Task copyA = a.CopyToAsync(b, token);
            Task copyB = b.CopyToAsync(a, token);
            try
            {
                await Task.WhenAny(copyA, copyB).ConfigureAwait(false);
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
        }

        private static async Task Socks5ConnectAsync(
            Stream stream,
            string host,
            int port,
            CancellationToken token)
        {
            await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, token).ConfigureAwait(false);
            byte[] greet = new byte[2];
            if (!await ReadExactAsync(stream, greet, token).ConfigureAwait(false)
                || greet[0] != 0x05
                || greet[1] != 0x00)
            {
                throw new IOException("SOCKS5 proxy rejected the handshake.");
            }

            byte[] hostBytes = Encoding.ASCII.GetBytes(host);
            byte[] request = new byte[7 + hostBytes.Length];
            request[0] = 0x05;
            request[1] = 0x01;
            request[2] = 0x00;
            request[3] = 0x03;
            request[4] = (byte)hostBytes.Length;
            Buffer.BlockCopy(hostBytes, 0, request, 5, hostBytes.Length);
            request[5 + hostBytes.Length] = (byte)(port >> 8);
            request[6 + hostBytes.Length] = (byte)port;
            await stream.WriteAsync(request, token).ConfigureAwait(false);
            byte[] head = new byte[4];
            if (!await ReadExactAsync(stream, head, token).ConfigureAwait(false)
                || head[0] != 0x05
                || head[1] != 0x00)
            {
                throw new IOException("SOCKS5 CONNECT failed.");
            }

            int addrLen;
            if (head[3] == 0x01)
            {
                addrLen = 4;
            }
            else if (head[3] == 0x04)
            {
                addrLen = 16;
            }
            else if (head[3] == 0x03)
            {
                int len = await ReadByteAsync(stream, token).ConfigureAwait(false);
                addrLen = len;
            }
            else
            {
                throw new IOException("SOCKS5 CONNECT failed.");
            }

            byte[] rest = new byte[addrLen + 2];
            if (!await ReadExactAsync(stream, rest, token).ConfigureAwait(false))
            {
                throw new IOException("SOCKS5 CONNECT failed.");
            }
        }

        private static async Task HttpConnectAsync(
            Stream stream,
            string host,
            int port,
            Proxy proxy,
            CancellationToken token)
        {
            string target = host + ":" + port.ToString(CultureInfo.InvariantCulture);
            StringBuilder request = new();
            request.Append("CONNECT ");
            request.Append(target);
            request.Append(" HTTP/1.1\r\nHost: ");
            request.Append(target);
            request.Append("\r\n");
            if (ProxySettings.HasCredentials(proxy))
            {
                string auth = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(proxy.Username + ":" + (proxy.Password ?? string.Empty)));
                request.Append("Proxy-Authorization: Basic ");
                request.Append(auth);
                request.Append("\r\n");
            }

            request.Append("\r\n");
            byte[] bytes = Encoding.ASCII.GetBytes(request.ToString());
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
            byte[] buffer = new byte[4096];
            MemoryStream acc = new();
            while (acc.Length < 64 * 1024)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new IOException("Proxy CONNECT closed before a response.");
                }

                await acc.WriteAsync(buffer.AsMemory(0, n), token).ConfigureAwait(false);
                byte[] data = acc.ToArray();
                int headerEnd = IndexOfHeaderEnd(data);
                if (headerEnd < 0)
                {
                    continue;
                }

                string header = Encoding.ASCII.GetString(data, 0, headerEnd);
                if (header.StartsWith("HTTP/1.1 200", StringComparison.Ordinal)
                    || header.StartsWith("HTTP/1.0 200", StringComparison.Ordinal))
                {
                    return;
                }

                throw new IOException("Proxy CONNECT failed: " + header.Split('\r')[0]);
            }

            throw new IOException("Proxy CONNECT failed.");
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

        private static async Task WriteHttp11ErrorAsync(Stream stream, string body, CancellationToken token)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body);
            string header = string.Concat(
                "HTTP/1.1 503 Internal Server Error\r\n",
                "Content-Type: text/html; charset=utf-8\r\n",
                "Content-Length: ",
                payload.Length.ToString(CultureInfo.InvariantCulture),
                "\r\nConnection: close\r\n\r\n");
            byte[] head = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(head, token).ConfigureAwait(false);
            await stream.WriteAsync(payload, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private static async Task WriteHttp2ErrorAsync(Stream stream, string body, CancellationToken token)
        {
            byte[] preface = new byte[24];
            if (!await ReadExactAsync(stream, preface, token).ConfigureAwait(false))
            {
                return;
            }

            byte[] settings = BuildFrame(0, 4, 0, 0, Array.Empty<byte>());
            byte[] settingsAck = BuildFrame(0, 4, 0x01, 0, Array.Empty<byte>());
            await stream.WriteAsync(settings, token).ConfigureAwait(false);
            await stream.WriteAsync(settingsAck, token).ConfigureAwait(false);

            int streamId = 1;
            using (CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                wait.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    while (!wait.IsCancellationRequested)
                    {
                        byte[] header = new byte[9];
                        if (!await ReadExactAsync(stream, header, wait.Token).ConfigureAwait(false))
                        {
                            break;
                        }

                        int length = (header[0] << 16) | (header[1] << 8) | header[2];
                        int type = header[3];
                        int flags = header[4];
                        int id = ((header[5] & 0x7f) << 24) | (header[6] << 16) | (header[7] << 8) | header[8];
                        byte[] payload = length > 0 ? new byte[length] : Array.Empty<byte>();
                        if (length > 0
                            && !await ReadExactAsync(stream, payload, wait.Token).ConfigureAwait(false))
                        {
                            break;
                        }

                        if (type == 0x01)
                        {
                            streamId = id;
                            break;
                        }

                        if (type == 0x04 && (flags & 0x01) == 0)
                        {
                            await stream.WriteAsync(settingsAck, wait.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }

            byte[] hpack = BuildStatusHeaders();
            byte[] headers = BuildFrame(hpack.Length, 0x01, 0x04, streamId, hpack);
            byte[] data = Encoding.UTF8.GetBytes(body);
            byte[] dataFrame = BuildFrame(data.Length, 0x00, 0x01, streamId, data);
            await stream.WriteAsync(headers, token).ConfigureAwait(false);
            await stream.WriteAsync(dataFrame, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private static byte[] BuildStatusHeaders()
        {
            MemoryStream hpack = new();
            WriteLiteral(hpack, ":status", "503");
            WriteLiteral(hpack, "content-type", "text/html");
            return hpack.ToArray();
        }

        private static void WriteLiteral(MemoryStream stream, string name, string value)
        {
            stream.WriteByte(0x00);
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            stream.WriteByte((byte)nameBytes.Length);
            stream.Write(nameBytes, 0, nameBytes.Length);
            byte[] valueBytes = Encoding.ASCII.GetBytes(value);
            stream.WriteByte((byte)valueBytes.Length);
            stream.Write(valueBytes, 0, valueBytes.Length);
        }

        private static byte[] BuildFrame(int length, byte type, byte flags, int streamId, byte[] payload)
        {
            byte[] frame = new byte[9 + (payload?.Length ?? 0)];
            frame[0] = (byte)((length >> 16) & 0xff);
            frame[1] = (byte)((length >> 8) & 0xff);
            frame[2] = (byte)(length & 0xff);
            frame[3] = type;
            frame[4] = flags;
            frame[5] = (byte)((streamId >> 24) & 0x7f);
            frame[6] = (byte)((streamId >> 16) & 0xff);
            frame[7] = (byte)((streamId >> 8) & 0xff);
            frame[8] = (byte)(streamId & 0xff);
            if (payload != null && payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, 9, payload.Length);
            }

            return frame;
        }

        private static List<SslApplicationProtocol> ToSslProtocols(IReadOnlyList<string> offered)
        {
            List<SslApplicationProtocol> list = new();
            if (offered == null)
            {
                list.Add(SslApplicationProtocol.Http11);
                return list;
            }

            foreach (string protocol in offered)
            {
                if (string.Equals(protocol, "h2", StringComparison.Ordinal))
                {
                    list.Add(SslApplicationProtocol.Http2);
                }
                else if (string.Equals(protocol, "http/1.1", StringComparison.Ordinal))
                {
                    list.Add(SslApplicationProtocol.Http11);
                }
                else if (!string.IsNullOrEmpty(protocol))
                {
                    list.Add(new SslApplicationProtocol(protocol));
                }
            }

            if (list.Count == 0)
            {
                list.Add(SslApplicationProtocol.Http11);
            }

            return list;
        }

        private static List<SslApplicationProtocol> ErrorPageAlpn(IReadOnlyList<string> offered)
        {
            // Upstream socksClientCertificatesInterceptor error path passes
            // `serverDecrypted.alpnProtocol` into the MITM upgrade; on handshake
            // failure that value is undefined, so the browser MITM offers only
            // ["http/1.1"]. Re-offering "h2" here makes WebKit negotiate HTTP/2
            // for the error page, and our lightweight HTTP/2 error writer then
            // trips "Broken pipe" / hangs instead of rendering the HTML.
            _ = offered;
            return new List<SslApplicationProtocol> { SslApplicationProtocol.Http11 };
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
            TcpClient server = null;
            try
            {
                using (client)
                using (NetworkStream browser = client.GetStream())
                {
                    BrowserProxyRequest? dest =
                        await NegotiateBrowserProxyAsync(browser, _cts.Token).ConfigureAwait(false);
                    if (dest == null)
                    {
                        return;
                    }

                    BrowserProxyRequest request = dest.Value;
                    try
                    {
                        server = await ConnectOutboundAsync(request.Host, request.Port, _cts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (request.Kind == BrowserProxyKind.Socks)
                        {
                            await WriteSocksFailureAsync(browser, _cts.Token).ConfigureAwait(false);
                            return;
                        }

                        if (request.Kind == BrowserProxyKind.HttpsConnect)
                        {
                            // CONNECT already returned 200 — wait for the browser
                            // ClientHello, then paint the TLS error page (same path as
                            // an origin handshake failure). Closing without a response
                            // makes Darwin WebKit report "Could not connect".
                            try
                            {
                                byte[] failedHello = await ReadTlsClientHelloAsync(browser, _cts.Token)
                                    .ConfigureAwait(false);
                                if (failedHello != null && failedHello.Length > 0 && failedHello[0] == 0x16)
                                {
                                    IReadOnlyList<string> failedAlpn =
                                        ParseAlpnFromClientHello(failedHello) ?? new[] { "http/1.1" };
                                    PrependStream prefixed = new(browser, failedHello);
                                    string message = ClientCertificateHelper.RewriteTlsMessage(ex);
                                    await WriteTlsErrorPageAsync(prefixed, failedAlpn, message)
                                        .ConfigureAwait(false);
                                    await HalfCloseAfterErrorPageAsync(client).ConfigureAwait(false);
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
                        }

                        return;
                    }

                    if (request.Kind == BrowserProxyKind.Socks)
                    {
                        await WriteSocksSuccessAsync(browser, _cts.Token).ConfigureAwait(false);
                    }

                    using NetworkStream origin = server.GetStream();
                    if (request.Kind == BrowserProxyKind.HttpForward)
                    {
                        await origin.WriteAsync(request.ForwardRequest, _cts.Token).ConfigureAwait(false);
                        await PipeAsync(browser, origin, _cts.Token).ConfigureAwait(false);
                        return;
                    }

                    // Darwin HTTP CONNECT can deliver a truncated ClientHello prefix
                    // that blocks AuthenticateAsServer — read a full TLS record there.
                    // Linux SOCKS keeps a single buffered read so SslStream can pull
                    // any remainder (HTTP/2 ClientHellos hung under ReadExact).
                    byte[] hello;
                    if (request.Kind == BrowserProxyKind.HttpsConnect)
                    {
                        hello = await ReadTlsClientHelloAsync(browser, _cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        byte[] first = new byte[16 * 1024];
                        int n = await browser.ReadAsync(first.AsMemory(0, first.Length), _cts.Token)
                            .ConfigureAwait(false);
                        if (n <= 0)
                        {
                            return;
                        }

                        hello = new byte[n];
                        Buffer.BlockCopy(first, 0, hello, 0, n);
                    }

                    if (hello == null || hello.Length == 0)
                    {
                        return;
                    }

                    if (hello[0] == 0x16
                        && TryGetClientCert(request.Host, request.Port, out X509Certificate2 clientCert))
                    {
                        await EstablishTlsTunnelAsync(
                            browser, origin, hello, request.Host, request.Port, clientCert, client)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await origin.WriteAsync(hello, _cts.Token).ConfigureAwait(false);
                        await PipeAsync(browser, origin, _cts.Token).ConfigureAwait(false);
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
            catch (AuthenticationException)
            {
            }
            finally
            {
                server?.Dispose();
            }
        }

        private async Task<TcpClient> ConnectOutboundAsync(string host, int port, CancellationToken token)
        {
            string connectHost = RewriteToLocalhostIfNeeded(host);
            Proxy outbound = ResolveOutboundProxy(connectHost, port);
            if (outbound == null || string.IsNullOrEmpty(outbound.Server))
            {
                TcpClient direct = new() { NoDelay = true };
                try
                {
                    await direct.ConnectAsync(connectHost, port, token).ConfigureAwait(false);
                    return direct;
                }
                catch
                {
                    direct.Dispose();
                    throw;
                }
            }

            string server = outbound.Server;
            if (server.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                server = "http://" + server;
            }

            Uri proxyUri = new Uri(server);
            int proxyPort = proxyUri.IsDefaultPort
                ? (string.Equals(proxyUri.Scheme, "socks5", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(proxyUri.Scheme, "socks5h", StringComparison.OrdinalIgnoreCase)
                    ? 1080
                    : 80)
                : proxyUri.Port;
            TcpClient via = new() { NoDelay = true };
            try
            {
                await via.ConnectAsync(proxyUri.IdnHost, proxyPort, token).ConfigureAwait(false);
                NetworkStream stream = via.GetStream();
                if (string.Equals(proxyUri.Scheme, "socks5", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(proxyUri.Scheme, "socks5h", StringComparison.OrdinalIgnoreCase))
                {
                    await Socks5ConnectAsync(stream, connectHost, port, token).ConfigureAwait(false);
                }
                else
                {
                    await HttpConnectAsync(stream, connectHost, port, outbound, token).ConfigureAwait(false);
                }

                return via;
            }
            catch
            {
                via.Dispose();
                throw;
            }
        }

        private Proxy ResolveOutboundProxy(string host, int port)
        {
            if (_userProxy != null && !string.IsNullOrEmpty(_userProxy.Server))
            {
                if (ProxySettings.ShouldBypass(ProxySettings.RequestHost(host, port), _userProxy.Bypass))
                {
                    return null;
                }

                return _userProxy;
            }

            string env = Environment.GetEnvironmentVariable("HTTPS_PROXY")
                ?? Environment.GetEnvironmentVariable("https_proxy")
                ?? Environment.GetEnvironmentVariable("HTTP_PROXY")
                ?? Environment.GetEnvironmentVariable("http_proxy");
            if (string.IsNullOrEmpty(env))
            {
                return null;
            }

            string noProxy = Environment.GetEnvironmentVariable("NO_PROXY")
                ?? Environment.GetEnvironmentVariable("no_proxy");
            if (ProxySettings.ShouldBypass(ProxySettings.RequestHost(host, port), noProxy))
            {
                return null;
            }

            return new Proxy { Server = env };
        }

        private async Task EstablishTlsTunnelAsync(
            NetworkStream browser,
            NetworkStream origin,
            byte[] clientHello,
            string host,
            int port,
            X509Certificate2 clientCert,
            TcpClient browserClient = null)
        {
            IReadOnlyList<string> offered = ParseAlpnFromClientHello(clientHello)
                ?? new[] { "http/1.1" };
            PrependStream browserPrefixed = new(browser, clientHello);
            SslStream serverTls = null;
            SslStream browserTls = null;
            try
            {
                serverTls = new SslStream(origin, leaveInnerStreamOpen: false);
                SslClientAuthenticationOptions clientOptions = new()
                {
                    TargetHost = IsIpAddress(host) ? string.Empty : host,
                    ClientCertificates = new X509CertificateCollection { clientCert },
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    EnabledSslProtocols = SslProtocols.None,
                    ApplicationProtocols = ToSslProtocols(offered),
                };
                X509Certificate2 selected = clientCert;
                clientOptions.LocalCertificateSelectionCallback = (_, _, _, _, _) => selected;
                if (_ignoreHttpsErrors)
                {
#pragma warning disable CA5359
                    clientOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
                }

                try
                {
                    // Bound the origin handshake: WebKit can sit forever when the
                    // server resets mid-TLS (SNI reject / TLS1.2 fixtures) or when
                    // certificate validation stalls. Upstream surfaces an error page
                    // instead of hanging page.goto.
                    using CancellationTokenSource handshakeCts =
                        CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    handshakeCts.CancelAfter(TimeSpan.FromSeconds(5));
                    await serverTls.AuthenticateAsClientAsync(clientOptions, handshakeCts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    string message = ClientCertificateHelper.RewriteTlsMessage(ex);

                    // Match upstream: destroy the origin socket before upgrading
                    // the browser side, so a mid-handshake RST cannot race the
                    // error-page MITM (Darwin CFNetwork reports "Could not connect"
                    // when the CONNECT tunnel dies during AuthenticateAsServer).
                    try
                    {
                        await serverTls.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    serverTls = null;
                    await WriteTlsErrorPageAsync(browserPrefixed, offered, message).ConfigureAwait(false);
                    await HalfCloseAfterErrorPageAsync(browserClient).ConfigureAwait(false);
                    return;
                }

                // Match upstream socksClientCertificatesInterceptor: MITM offers
                // only the origin-negotiated ALPN to the browser.
                browserTls = new SslStream(browserPrefixed, leaveInnerStreamOpen: false);
                SslApplicationProtocol negotiated = serverTls.NegotiatedApplicationProtocol;
                List<SslApplicationProtocol> browserAlpn = new()
                {
                    negotiated.Protocol.Length > 0 ? negotiated : SslApplicationProtocol.Http11,
                };

                SslServerAuthenticationOptions serverOptions = new()
                {
                    ServerCertificate = _dummyCert,
                    ClientCertificateRequired = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    EnabledSslProtocols = SslProtocols.None,
                    ApplicationProtocols = browserAlpn,
                };
#pragma warning disable CA5359
                serverOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
                await browserTls.AuthenticateAsServerAsync(serverOptions, _cts.Token).ConfigureAwait(false);
                await PipeAsync(browserTls, serverTls, _cts.Token).ConfigureAwait(false);
            }
            finally
            {
                if (browserTls != null)
                {
                    await browserTls.DisposeAsync().ConfigureAwait(false);
                }

                if (serverTls != null)
                {
                    await serverTls.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private bool TryGetClientCert(string host, int port, out X509Certificate2 clientCert)
        {
            clientCert = null;
            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            string portText = port.ToString(CultureInfo.InvariantCulture);
            if (TryGetCertForHost(host, portText, out clientCert))
            {
                return true;
            }

            // Darwin CFNetwork may CONNECT as 127.0.0.1/localhost while the fixture
            // registered https://local.playwright — look up that origin only when the
            // CONNECT host is the loopback alias, never the reverse (HTTP/2 fixtures
            // register 127.0.0.1 and intentionally omit the cert on local.playwright).
            if (string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
                || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return TryGetCertForHost("local.playwright", portText, out clientCert);
            }

            return false;
        }

        private bool TryGetCertForHost(string host, string portText, out X509Certificate2 clientCert)
        {
            string originKey = ClientCertificateHelper.NormalizeOrigin(
                "https://" + host + ":" + portText);
            return _certs.TryGetValue(originKey, out clientCert);
        }

        private async Task WriteTlsErrorPageAsync(
            Stream browser,
            IReadOnlyList<string> offered,
            string message)
        {
            string body = EscapeHtml("Playwright client-certificate error: " + message);

            // leaveInnerStreamOpen: the Darwin HTTP CONNECT shim must see a clean
            // TLS close_notify after the HTML is fully written; disposing the
            // NetworkStream underneath SslStream too early surfaces
            // "Could not connect to the server" instead of the error document.
            SslStream tls = new(browser, leaveInnerStreamOpen: true);
            try
            {
                // Origin handshake already failed — mirror upstream's error path
                // (ALPN http/1.1 only). Pin TLS 1.2|1.3 so AuthenticateAsServer
                // accepts a TLS 1.2-only ClientHello from WebKit on macOS
                // (BrowserShouldNotHangOnTlsErrorsDuringTls12Handshake).
#pragma warning disable CA5398
                SslServerAuthenticationOptions options = new()
                {
                    ServerCertificate = _dummyCert,
                    ClientCertificateRequired = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ApplicationProtocols = ErrorPageAlpn(offered),
                };
#pragma warning restore CA5398
#pragma warning disable CA5359
                options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
                await tls.AuthenticateAsServerAsync(options, _cts.Token).ConfigureAwait(false);

                if (tls.NegotiatedApplicationProtocol.Equals(SslApplicationProtocol.Http2))
                {
                    await WriteHttp2ErrorAsync(tls, body, _cts.Token).ConfigureAwait(false);
                }
                else
                {
                    await WriteHttp11ErrorAsync(tls, body, _cts.Token).ConfigureAwait(false);
                }

                await tls.FlushAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (AuthenticationException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                try
                {
                    // Send close_notify without tearing down the TCP socket the
                    // Darwin bypass shim is still piping toward WebKit.
                    await tls.DisposeAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        /// <summary>
        /// After the MITM error HTML is written, half-close the accepted socket
        /// and briefly drain so disposing <see cref="TcpClient"/> does not RST
        /// unread data still in the Darwin bypass shim pipe (CFNetwork then
        /// reports "Could not connect" instead of the error document).
        /// </summary>
        /// <param name="client">Accepted browser-side client, or null.</param>
        /// <returns>A task that completes when the half-close attempt finishes.</returns>
        private async Task HalfCloseAfterErrorPageAsync(TcpClient client)
        {
            if (client?.Client == null)
            {
                return;
            }

            try
            {
                client.Client.Shutdown(SocketShutdown.Send);
            }
            catch (SocketException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                NetworkStream stream = client.GetStream();
                byte[] sink = new byte[1024];
                using CancellationTokenSource drainCts = new(TimeSpan.FromMilliseconds(250));
                while (true)
                {
                    int n = await stream.ReadAsync(sink.AsMemory(0, sink.Length), drainCts.Token)
                        .ConfigureAwait(false);
                    if (n <= 0)
                    {
                        break;
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
        }

        private readonly struct BrowserProxyRequest
        {
            internal BrowserProxyRequest(string host, int port, BrowserProxyKind kind, byte[] forwardRequest)
            {
                Host = host;
                Port = port;
                Kind = kind;
                ForwardRequest = forwardRequest;
            }

            internal string Host { get; }

            internal int Port { get; }

            internal BrowserProxyKind Kind { get; }

            internal byte[] ForwardRequest { get; }
        }

        private sealed class PrependStream : Stream
        {
#pragma warning disable CA2213 // The inner socket is owned by the SOCKS accept loop.
            private readonly Stream _inner;
#pragma warning restore CA2213
            private byte[] _prefix;
            private int _offset;

            internal PrependStream(Stream inner, byte[] prefix)
            {
                _inner = inner;
                _prefix = prefix;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();

            public override Task FlushAsync(CancellationToken cancellationToken)
                => _inner.FlushAsync(cancellationToken);

            public override int Read(byte[] buffer, int offset, int count)
            {
                // SslStream.AuthenticateAsServer may read synchronously while
                // completing a TLS 1.2 error-page handshake; the ClientHello
                // prefix must still be replayed.
                if (_prefix != null && _offset < _prefix.Length)
                {
                    int remaining = _prefix.Length - _offset;
                    int take = Math.Min(remaining, count);
                    Buffer.BlockCopy(_prefix, _offset, buffer, offset, take);
                    _offset += take;
                    if (_offset >= _prefix.Length)
                    {
                        _prefix = null;
                    }

                    return take;
                }

                return _inner.Read(buffer, offset, count);
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_prefix != null && _offset < _prefix.Length)
                {
                    int remaining = _prefix.Length - _offset;
                    int take = Math.Min(remaining, buffer.Length);
                    _prefix.AsSpan(_offset, take).CopyTo(buffer.Span);
                    _offset += take;
                    if (_offset >= _prefix.Length)
                    {
                        _prefix = null;
                    }

                    return take;
                }

                return await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            public override void Write(byte[] buffer, int offset, int count)
                => _inner.Write(buffer, offset, count);

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
                => _inner.WriteAsync(buffer, cancellationToken);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
