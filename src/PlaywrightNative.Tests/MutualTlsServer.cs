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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Local HTTPS server that reports whether a client certificate was presented.
    /// </summary>
    internal sealed class MutualTlsServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _serverCert;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _disposed;

        private MutualTlsServer(TcpListener listener, X509Certificate2 serverCert, byte[] clientCertPem, byte[] clientKeyPem)
        {
            _listener = listener;
            _serverCert = serverCert;
            ClientCertPem = clientCertPem;
            ClientKeyPem = clientKeyPem;
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Origin = "https://localhost:" + port.ToString(CultureInfo.InvariantCulture);
        }

        internal string Origin { get; }

        internal byte[] ClientCertPem { get; }

        internal byte[] ClientKeyPem { get; }

        internal static Task<MutualTlsServer> StartAsync()
        {
            using RSA serverKey = RSA.Create(2048);
            using RSA clientKey = RSA.Create(2048);
            X509Certificate2 serverCert = CreateCertificate(serverKey, "CN=localhost", includeServerAuth: true);
            X509Certificate2 clientCert = CreateCertificate(clientKey, "CN=Alice", includeServerAuth: false);
            byte[] clientCertPem = Encoding.ASCII.GetBytes(PemEncode("CERTIFICATE", clientCert.RawData));
            byte[] clientKeyPem = Encoding.ASCII.GetBytes(clientKey.ExportPkcs8PrivateKeyPem());
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            MutualTlsServer server = new MutualTlsServer(listener, serverCert, clientCertPem, clientKeyPem);
            _ = server.AcceptLoopAsync();
            return Task.FromResult(server);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Cancel();
            _listener.Stop();
            _serverCert.Dispose();
            _cts.Dispose();
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static X509Certificate2 CreateCertificate(RSA key, string subject, bool includeServerAuth)
        {
            CertificateRequest request = new CertificateRequest(
                subject,
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new Oid(includeServerAuth ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2"),
                },
                false));
            if (includeServerAuth)
            {
                SubjectAlternativeNameBuilder san = new SubjectAlternativeNameBuilder();
                san.AddDnsName("localhost");
                san.AddIpAddress(IPAddress.Loopback);
                request.CertificateExtensions.Add(san.Build());
            }

            using X509Certificate2 created = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(7));
            return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), string.Empty, X509KeyStorageFlags.Exportable);
        }

        private static string PemEncode(string label, byte[] data)
        {
            return "-----BEGIN " + label + "-----\n"
                + Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks)
                + "\n-----END " + label + "-----\n";
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
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (OperationCanceledException)
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
                using (SslStream ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false))
                {
                    SslServerAuthenticationOptions options = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _serverCert,
                        ClientCertificateRequired = true,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        RemoteCertificateValidationCallback = static (_, _, _, _) => true,
                    };
                    await ssl.AuthenticateAsServerAsync(options).ConfigureAwait(false);
                    await DrainRequestAsync(ssl).ConfigureAwait(false);

                    string body;
                    if (ssl.RemoteCertificate == null)
                    {
                        body = "Sorry, but you need to provide a client certificate to continue.";
                    }
                    else
                    {
                        using X509Certificate2 presented = X509CertificateLoader.LoadCertificate(ssl.RemoteCertificate.GetRawCertData());
                        body = "Hello CN=" + presented.GetNameInfo(X509NameType.SimpleName, false);
                    }

                    int status = ssl.RemoteCertificate == null ? 401 : 200;
                    byte[] payload = Encoding.UTF8.GetBytes(body);
                    string head = "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture)
                        + " OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: "
                        + payload.Length.ToString(CultureInfo.InvariantCulture)
                        + "\r\nConnection: close\r\n\r\n";
                    await ssl.WriteAsync(Encoding.ASCII.GetBytes(head)).ConfigureAwait(false);
                    await ssl.WriteAsync(payload).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
            }
            catch (AuthenticationException)
            {
            }
        }

        private static async Task DrainRequestAsync(Stream stream)
        {
            byte[] buffer = new byte[4096];
            MemoryStream collected = new MemoryStream();
            while (collected.Length < 64 * 1024)
            {
                int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                collected.Write(buffer, 0, read);
                byte[] bytes = collected.ToArray();
                if (Encoding.ASCII.GetString(bytes).Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
    }
}
