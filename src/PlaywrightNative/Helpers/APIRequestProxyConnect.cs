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
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright <c>createProxyAgent</c> for APIRequest: HTTP
    /// proxies always CONNECT (even for <c>http://</c> URLs), SOCKS5 uses
    /// hostname form, and HTTPS proxies offer ALPN <c>http/1.1</c>.
    /// </summary>
    internal static class APIRequestProxyConnect
    {
        internal static void Apply(SocketsHttpHandler handler, Proxy proxy, bool ignoreTls)
        {
            if (handler == null || proxy == null || string.IsNullOrEmpty(proxy.Server))
            {
                return;
            }

            string server = proxy.Server;
            if (server.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                server = "http://" + server;
            }

            Uri proxyUri = new Uri(server);
            handler.UseProxy = false;
            handler.ConnectCallback = (context, cancellationToken) =>
                ConnectAsync(context, proxy, proxyUri, ignoreTls, cancellationToken);
        }

        private static async ValueTask<Stream> ConnectAsync(
            SocketsHttpConnectionContext context,
            Proxy proxy,
            Uri proxyUri,
            bool ignoreTls,
            CancellationToken cancellationToken)
        {
            string targetHost = context.DnsEndPoint.Host;
            int targetPort = context.DnsEndPoint.Port;
            if (ProxySettings.ShouldBypass(ProxySettings.RequestHost(targetHost, targetPort), proxy.Bypass))
            {
                return await ConnectDirectAsync(targetHost, targetPort, cancellationToken).ConfigureAwait(false);
            }

            Stream stream = await ConnectDirectAsync(proxyUri.IdnHost, ResolvePort(proxyUri), cancellationToken)
                .ConfigureAwait(false);
            try
            {
                if (IsSocks(proxyUri))
                {
                    await Socks5ConnectAsync(stream, targetHost, targetPort, cancellationToken).ConfigureAwait(false);
                    Stream result = stream;
                    stream = null;
                    return result;
                }

                if (string.Equals(proxyUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    stream = await TlsToProxyAsync(stream, proxyUri.IdnHost, ignoreTls, cancellationToken)
                        .ConfigureAwait(false);
                }

                await HttpConnectAsync(stream, targetHost, targetPort, proxy, cancellationToken).ConfigureAwait(false);
                Stream tunneled = stream;
                stream = null;
                return tunneled;
            }
            finally
            {
                if (stream != null)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task<Stream> ConnectDirectAsync(string host, int port, CancellationToken cancellationToken)
        {
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await socket.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                    }
                    catch (SocketException)
                    {
                        socket.Dispose();
                        socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                        {
                            NoDelay = true,
                        };
                        await socket.ConnectAsync(IPAddress.IPv6Loopback, port, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await socket.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
                }

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static async Task<Stream> TlsToProxyAsync(
            Stream inner,
            string targetHost,
            bool ignoreTls,
            CancellationToken cancellationToken)
        {
            SslStream ssl = new SslStream(inner, leaveInnerStreamOpen: false);
            try
            {
                SslClientAuthenticationOptions options = new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost,
                    ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http11 },
                };
                if (ignoreTls)
                {
                    options.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
#pragma warning disable CA5359
                    options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
                }

                await ssl.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
                SslStream result = ssl;
                ssl = null;
                return result;
            }
            finally
            {
                if (ssl != null)
                {
                    await ssl.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task HttpConnectAsync(
            Stream stream,
            string targetHost,
            int targetPort,
            Proxy proxy,
            CancellationToken cancellationToken)
        {
            string target = targetHost + ":" + targetPort.ToString(CultureInfo.InvariantCulture);
            StringBuilder request = new StringBuilder();
            request.Append("CONNECT ");
            request.Append(target);
            request.Append(" HTTP/1.1\r\nHost: ");
            request.Append(target);
            request.Append("\r\n");
            if (ProxySettings.HasCredentials(proxy))
            {
                string token = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(proxy.Username + ":" + (proxy.Password ?? string.Empty)));
                request.Append("Proxy-Authorization: Basic ");
                request.Append(token);
                request.Append("\r\n");
            }

            request.Append("\r\n");
            byte[] bytes = Encoding.ASCII.GetBytes(request.ToString());
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

            byte[] buffer = new byte[4096];
            MemoryStream acc = new MemoryStream();
            while (acc.Length < 64 * 1024)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (n == 0)
                {
                    throw new HttpRequestException("Proxy CONNECT closed before a response.");
                }

                await acc.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
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

                string status = header.Split('\r')[0];
                throw new HttpRequestException("Proxy CONNECT failed: " + status);
            }

            throw new HttpRequestException("Proxy CONNECT failed.");
        }

        private static async Task Socks5ConnectAsync(
            Stream stream,
            string targetHost,
            int targetPort,
            CancellationToken cancellationToken)
        {
            await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, cancellationToken).ConfigureAwait(false);
            byte[] greet = await ReadExactAsync(stream, 2, cancellationToken).ConfigureAwait(false);
            if (greet[0] != 0x05 || greet[1] != 0x00)
            {
                throw new HttpRequestException("SOCKS5 proxy rejected the handshake.");
            }

            byte[] hostBytes = Encoding.ASCII.GetBytes(targetHost);
            if (hostBytes.Length == 0 || hostBytes.Length > 255)
            {
                throw new HttpRequestException("SOCKS5 target host is invalid.");
            }

            byte[] request = new byte[7 + hostBytes.Length];
            request[0] = 0x05;
            request[1] = 0x01;
            request[2] = 0x00;
            request[3] = 0x03;
            request[4] = (byte)hostBytes.Length;
            Buffer.BlockCopy(hostBytes, 0, request, 5, hostBytes.Length);
            request[5 + hostBytes.Length] = (byte)(targetPort >> 8);
            request[6 + hostBytes.Length] = (byte)targetPort;
            await stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            byte[] head = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
            if (head[0] != 0x05 || head[1] != 0x00)
            {
                throw new HttpRequestException("SOCKS5 CONNECT failed.");
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
                int len = await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
                if (len < 0)
                {
                    throw new HttpRequestException("SOCKS5 CONNECT failed.");
                }

                addrLen = len;
            }
            else
            {
                throw new HttpRequestException("SOCKS5 CONNECT failed.");
            }

            await ReadExactAsync(stream, addrLen + 2, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken)
                    .ConfigureAwait(false);
                if (n == 0)
                {
                    throw new HttpRequestException("SOCKS5 proxy closed the connection.");
                }

                offset += n;
            }

            return buffer;
        }

        private static async Task<int> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] one = new byte[1];
            int n = await stream.ReadAsync(one.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            return n == 0 ? -1 : one[0];
        }

        private static int IndexOfHeaderEnd(byte[] data)
        {
            for (int i = 0; i + 3 < data.Length; i++)
            {
                if (data[i] == (byte)'\r' && data[i + 1] == (byte)'\n'
                    && data[i + 2] == (byte)'\r' && data[i + 3] == (byte)'\n')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsSocks(Uri proxyUri)
            => string.Equals(proxyUri.Scheme, "socks5", StringComparison.OrdinalIgnoreCase)
                || string.Equals(proxyUri.Scheme, "socks5h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(proxyUri.Scheme, "socks", StringComparison.OrdinalIgnoreCase);

        private static int ResolvePort(Uri proxyUri)
        {
            if (proxyUri.Port > 0)
            {
                return proxyUri.Port;
            }

            if (string.Equals(proxyUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return 443;
            }

            return 80;
        }
    }
}
