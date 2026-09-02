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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>fetch-proxy</c> HTTPS proxy that records the client ALPN
    /// offer, then closes so <c>request.get</c> rejects.
    /// </summary>
    internal sealed class OfficialHttpsAlpnProxy : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _cert;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly object _lock = new();
        private string[] _offeredProtocols = Array.Empty<string>();

        private OfficialHttpsAlpnProxy(TcpListener listener, X509Certificate2 cert)
        {
            _listener = listener;
            _cert = cert;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port { get; }

        internal string[] OfferedProtocols
        {
            get
            {
                lock (_lock)
                {
                    return _offeredProtocols;
                }
            }
        }

        internal static OfficialHttpsAlpnProxy Start()
        {
            using RSA key = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=localhost",
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
            SubjectAlternativeNameBuilder san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
            using X509Certificate2 created = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(7));
            X509Certificate2 cert = X509CertificateLoader.LoadPkcs12(
                created.Export(X509ContentType.Pfx),
                string.Empty,
                X509KeyStorageFlags.Exportable);
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new OfficialHttpsAlpnProxy(listener, cert);
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

            _cert.Dispose();
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
                await using (NetworkStream network = client.GetStream())
                {
                    byte[] hello = await ReadTlsRecordAsync(network).ConfigureAwait(false);
                    string[] offered = ParseAlpn(hello);
                    using PrefixStream prepended = new PrefixStream(hello, network);
                    using SslStream ssl = new SslStream(prepended, leaveInnerStreamOpen: true);
                    SslServerAuthenticationOptions options = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _cert,
                        ClientCertificateRequired = false,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        ApplicationProtocols = new List<SslApplicationProtocol>
                        {
                            SslApplicationProtocol.Http11,
                            SslApplicationProtocol.Http2,
                        },
                    };
                    await ssl.AuthenticateAsServerAsync(options).ConfigureAwait(false);
                    if (offered.Length == 0 && !ssl.NegotiatedApplicationProtocol.Equals(default(SslApplicationProtocol)))
                    {
                        offered = new[]
                        {
                            Encoding.ASCII.GetString(ssl.NegotiatedApplicationProtocol.Protocol.Span),
                        };
                    }

                    lock (_lock)
                    {
                        _offeredProtocols = offered;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (AuthenticationException)
            {
            }
        }

        private static async Task<byte[]> ReadTlsRecordAsync(Stream stream)
        {
            byte[] header = new byte[5];
            await ReadExactAsync(stream, header, 0, 5).ConfigureAwait(false);
            int length = (header[3] << 8) | header[4];
            byte[] record = new byte[5 + length];
            Buffer.BlockCopy(header, 0, record, 0, 5);
            await ReadExactAsync(stream, record, 5, length).ConfigureAwait(false);
            return record;
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(offset + read, count - read)).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new IOException("Unexpected EOF.");
                }

                read += n;
            }
        }

        private static string[] ParseAlpn(byte[] record)
        {
            if (record == null || record.Length < 9 || record[0] != 0x16 || record[5] != 0x01)
            {
                return Array.Empty<string>();
            }

            int handshakeLen = (record[6] << 16) | (record[7] << 8) | record[8];
            int body = 9;
            int end = Math.Min(record.Length, body + handshakeLen);
            if (body + 34 > end)
            {
                return Array.Empty<string>();
            }

            int offset = body + 34;
            if (offset >= end)
            {
                return Array.Empty<string>();
            }

            int sessionIdLen = record[offset];
            offset += 1 + sessionIdLen;
            if (offset + 2 > end)
            {
                return Array.Empty<string>();
            }

            int cipherLen = (record[offset] << 8) | record[offset + 1];
            offset += 2 + cipherLen;
            if (offset + 1 > end)
            {
                return Array.Empty<string>();
            }

            int compLen = record[offset];
            offset += 1 + compLen;
            if (offset + 2 > end)
            {
                return Array.Empty<string>();
            }

            int extLen = (record[offset] << 8) | record[offset + 1];
            offset += 2;
            int extEnd = Math.Min(end, offset + extLen);
            while (offset + 4 <= extEnd)
            {
                int type = (record[offset] << 8) | record[offset + 1];
                int len = (record[offset + 2] << 8) | record[offset + 3];
                offset += 4;
                if (offset + len > extEnd)
                {
                    break;
                }

                if (type == 16)
                {
                    return ReadAlpnList(record, offset, len);
                }

                offset += len;
            }

            return Array.Empty<string>();
        }

        private static string[] ReadAlpnList(byte[] record, int offset, int length)
        {
            if (length < 2)
            {
                return Array.Empty<string>();
            }

            int listLen = (record[offset] << 8) | record[offset + 1];
            int pos = offset + 2;
            int listEnd = Math.Min(offset + length, pos + listLen);
            List<string> names = new List<string>();
            while (pos < listEnd)
            {
                int nameLen = record[pos];
                pos += 1;
                if (pos + nameLen > listEnd)
                {
                    break;
                }

                names.Add(Encoding.ASCII.GetString(record, pos, nameLen));
                pos += nameLen;
            }

            return names.ToArray();
        }

        private sealed class PrefixStream : Stream
        {
            private readonly Stream _inner;
            private ReadOnlyMemory<byte> _prefix;

            internal PrefixStream(byte[] prefix, Stream inner)
            {
                _prefix = prefix;
                _inner = inner;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => _inner.CanWrite;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_prefix.Length > 0)
                {
                    int n = Math.Min(count, _prefix.Length);
                    _prefix.Span.Slice(0, n).CopyTo(buffer.AsSpan(offset, n));
                    _prefix = _prefix.Slice(n);
                    return n;
                }

                return _inner.Read(buffer, offset, count);
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_prefix.Length > 0)
                {
                    int n = Math.Min(buffer.Length, _prefix.Length);
                    _prefix.Span.Slice(0, n).CopyTo(buffer.Span);
                    _prefix = _prefix.Slice(n);
                    return n;
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
