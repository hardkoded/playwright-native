/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Minimal HTTPS origin for official <c>should use proxy for https urls</c>.
    /// </summary>
    internal sealed class OfficialHttpsTargetServer : IAsyncDisposable
    {
        private static readonly byte[] Body = Encoding.UTF8.GetBytes(
            "<html><title>Served by https server via proxy</title></html>");

        private readonly TcpListener _listener;
        private readonly X509Certificate2 _cert;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;

        private OfficialHttpsTargetServer(TcpListener listener, X509Certificate2 cert)
        {
            _listener = listener;
            _cert = cert;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port { get; }

        internal static OfficialHttpsTargetServer Start()
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
            san.AddDnsName("non-existent.com");
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
            return new OfficialHttpsTargetServer(listener, cert);
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
                using (SslStream ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false))
                {
                    SslServerAuthenticationOptions options = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _cert,
                        ClientCertificateRequired = false,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    };
                    await ssl.AuthenticateAsServerAsync(options).ConfigureAwait(false);
                    await DrainRequestAsync(ssl).ConfigureAwait(false);
                    string header =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: text/html; charset=utf-8\r\n" +
                        "Content-Length: " + Body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                        "Connection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    await ssl.WriteAsync(headerBytes).ConfigureAwait(false);
                    await ssl.WriteAsync(Body).ConfigureAwait(false);
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

        private static async Task DrainRequestAsync(Stream stream)
        {
            byte[] buffer = new byte[4096];
            MemoryStream acc = new MemoryStream();
            while (acc.Length < 64 * 1024)
            {
                int n = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (n == 0)
                {
                    return;
                }

                acc.Write(buffer, 0, n);
                byte[] data = acc.ToArray();
                if (IndexOfHeaderEnd(data) >= 0)
                {
                    return;
                }
            }
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
    }
}
