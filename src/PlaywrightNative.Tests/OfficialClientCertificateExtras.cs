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
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Self-signed HTTPS origin with <c>CN=playwright-test</c>.
    /// </summary>
    internal sealed class OfficialPlaywrightTestHttpsServer : IAsyncDisposable
    {
        private readonly IWebHost _host;

        private OfficialPlaywrightTestHttpsServer(IWebHost host, string prefix)
        {
            _host = host;
            Prefix = prefix;
        }

        internal string Prefix { get; }

        internal string EmptyPage => Prefix + "/empty.html";

        internal static async Task<OfficialPlaywrightTestHttpsServer> StartAsync()
        {
            string certPath = Path.Combine(TestUtils.FindParentDirectory("PlaywrightNative.TestServer"), "playwright-test.pem");
            string keyPath = Path.Combine(TestUtils.FindParentDirectory("PlaywrightNative.TestServer"), "playwright-test-key.pem");
            X509Certificate2 cert = OfficialClientCertificateServer.LoadPem(certPath, keyPath);
            IWebHost host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 0, listen =>
                    {
                        listen.UseHttps(new HttpsConnectionAdapterOptions
                        {
                            ServerCertificate = cert,
                        });
                    });
                })
                .Configure(app =>
                {
                    app.Run(async context =>
                    {
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "text/html";
                        if (context.Request.Path.StartsWithSegments("/hello.html"))
                        {
                            await context.Response.WriteAsync(
                                "<html><body><div data-testid=\"message\">hello</div></body></html>")
                                .ConfigureAwait(false);
                            return;
                        }

                        await context.Response.WriteAsync("<html><body>empty</body></html>").ConfigureAwait(false);
                    });
                })
                .Build();
            await host.StartAsync().ConfigureAwait(false);
            int port = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
                .Addresses
                .SelectAddressPort();
            return new OfficialPlaywrightTestHttpsServer(
                host,
                "https://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _host.StopAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }

            _host.Dispose();
        }
    }

    /// <summary>
    /// Official TLS server that rejects the handshake from SNICallback.
    /// </summary>
    internal sealed class OfficialTlsSniRejectServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _cert;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly SslProtocols _protocol;

        private OfficialTlsSniRejectServer(TcpListener listener, X509Certificate2 cert, SslProtocols protocol)
        {
            _listener = listener;
            _cert = cert;
            _protocol = protocol;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port { get; }

        internal string Url => "https://localhost:" + Port.ToString(CultureInfo.InvariantCulture) + "/";

        internal static OfficialTlsSniRejectServer Start(SslProtocols protocol)
        {
            X509Certificate2 cert = OfficialClientCertificateServer.LoadPem(
                OfficialClientCertificateServer.Asset("client-certificates/server/server_cert.pem"),
                OfficialClientCertificateServer.Asset("client-certificates/server/server_key.pem"));
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            return new OfficialTlsSniRejectServer(listener, cert, protocol);
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

            _cts.Dispose();
            _cert.Dispose();
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

                _ = HandleAsync(client);
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            try
            {
                using (client)
                await using (NetworkStream stream = client.GetStream())
                await using (SslStream ssl = new(stream, leaveInnerStreamOpen: false))
                {
                    SslServerAuthenticationOptions options = new()
                    {
                        ServerCertificate = _cert,
                        ClientCertificateRequired = true,
                        EnabledSslProtocols = _protocol,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        ServerCertificateSelectionCallback = (_, _) =>
                            throw new IOException("Connection rejected"),
                    };
                    await ssl.AuthenticateAsServerAsync(options, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// HTTPS server that 302-redirects to a target URL.
    /// </summary>
    internal sealed class OfficialHttpsRedirectServer : IAsyncDisposable
    {
        private readonly IWebHost _host;

        private OfficialHttpsRedirectServer(IWebHost host, string url)
        {
            _host = host;
            Url = url;
        }

        internal string Url { get; }

        internal static async Task<OfficialHttpsRedirectServer> StartAsync(string target)
        {
            X509Certificate2 cert = OfficialClientCertificateServer.LoadPem(
                OfficialClientCertificateServer.Asset("client-certificates/server/server_cert.pem"),
                OfficialClientCertificateServer.Asset("client-certificates/server/server_key.pem"));
            IWebHost host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 0, listen =>
                    {
                        listen.UseHttps(new HttpsConnectionAdapterOptions { ServerCertificate = cert });
                    });
                })
                .Configure(app =>
                {
                    app.Run(context =>
                    {
                        context.Response.StatusCode = 302;
                        context.Response.Headers.Location = target;
                        return Task.CompletedTask;
                    });
                })
                .Build();
            await host.StartAsync().ConfigureAwait(false);
            int port = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
                .Addresses
                .SelectAddressPort();
            return new OfficialHttpsRedirectServer(
                host,
                "https://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/redir");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _host.StopAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }

            _host.Dispose();
        }
    }

    /// <summary>
    /// TLS 1.2 server that renegotiates for client certificates.
    /// </summary>
    internal sealed class OfficialTlsRenegotiationServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _cert;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;

        private OfficialTlsRenegotiationServer(TcpListener listener, X509Certificate2 cert)
        {
            _listener = listener;
            _cert = cert;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port { get; }

        internal string Url => "https://localhost:" + Port.ToString(CultureInfo.InvariantCulture);

        internal static OfficialTlsRenegotiationServer Start()
        {
            X509Certificate2 cert = OfficialClientCertificateServer.LoadPem(
                OfficialClientCertificateServer.Asset("client-certificates/server/server_cert.pem"),
                OfficialClientCertificateServer.Asset("client-certificates/server/server_key.pem"));
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            return new OfficialTlsRenegotiationServer(listener, cert);
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

            _cts.Dispose();
            _cert.Dispose();
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

                _ = HandleAsync(client);
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            try
            {
                using (client)
                await using (NetworkStream stream = client.GetStream())
                await using (SslStream ssl = new(stream, leaveInnerStreamOpen: false))
                {
                    SslServerAuthenticationOptions options = new()
                    {
                        ServerCertificate = _cert,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Tls12,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    };
#pragma warning disable CA5359
                    options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
                    await ssl.AuthenticateAsServerAsync(options, _cts.Token).ConfigureAwait(false);
                    string request = await ReadHttpAsync(ssl, _cts.Token).ConfigureAwait(false);
                    if (request.Contains(" /from-fetch-api ", StringComparison.Ordinal))
                    {
                        await WriteChunkedStartAsync(ssl, "text/plain", _cts.Token).ConfigureAwait(false);
                        string body = await ReadHttpBodyAsync(ssl, request, _cts.Token).ConfigureAwait(false);
                        await WriteChunkAsync(ssl, "server received: " + body + "\n", _cts.Token).ConfigureAwait(false);
                        await TryRenegotiateAsync(ssl).ConfigureAwait(false);
                        for (int i = 0; i < 4; i++)
                        {
                            await WriteChunkAsync(ssl, i.ToString(CultureInfo.InvariantCulture) + "-from-server\n", _cts.Token)
                                .ConfigureAwait(false);
                            await Task.Delay(100, _cts.Token).ConfigureAwait(false);
                        }

                        await WriteChunkAsync(ssl, "server closed the connection", _cts.Token).ConfigureAwait(false);
                        await WriteChunkAsync(ssl, string.Empty, _cts.Token).ConfigureAwait(false);
                    }
                    else if (request.Contains(" /style.css ", StringComparison.Ordinal))
                    {
                        await WriteRawAsync(
                            ssl,
                            "HTTP/1.1 200 OK\r\nContent-Type: text/css\r\nContent-Encoding: gzip\r\nTransfer-Encoding: chunked\r\n\r\n",
                            _cts.Token).ConfigureAwait(false);
                        await TryRenegotiateAsync(ssl).ConfigureAwait(false);
                        byte[] gzipped = Gzip("\n          button {\n            background-color: red;\n          }\n        ");
                        for (int i = 0; i < gzipped.Length; i += 100)
                        {
                            int take = Math.Min(100, gzipped.Length - i);
                            await WriteChunkBytesAsync(ssl, gzipped.AsMemory(i, take), _cts.Token).ConfigureAwait(false);
                            await Task.Delay(20, _cts.Token).ConfigureAwait(false);
                        }

                        await WriteChunkAsync(ssl, string.Empty, _cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await WriteRawAsync(
                            ssl,
                            "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nConnection: close\r\nContent-Length: 0\r\n\r\n",
                            _cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static async Task TryRenegotiateAsync(SslStream ssl)
        {
            try
            {
                await ssl.NegotiateClientCertificateAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static byte[] Gzip(string text)
        {
            using MemoryStream output = new();
            using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                byte[] raw = Encoding.UTF8.GetBytes(text);
                gzip.Write(raw, 0, raw.Length);
            }

            return output.ToArray();
        }

        private static async Task<string> ReadHttpAsync(Stream stream, CancellationToken token)
        {
            MemoryStream acc = new();
            byte[] buffer = new byte[4096];
            while (acc.Length < 64 * 1024)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                if (n == 0)
                {
                    break;
                }

                await acc.WriteAsync(buffer.AsMemory(0, n), token).ConfigureAwait(false);
                byte[] data = acc.ToArray();
                if (IndexOf(data, "\r\n\r\n") >= 0)
                {
                    return Encoding.ASCII.GetString(data);
                }
            }

            return Encoding.ASCII.GetString(acc.ToArray());
        }

        private static async Task<string> ReadHttpBodyAsync(Stream stream, string request, CancellationToken token)
        {
            int headerEnd = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string leftover = headerEnd >= 0 ? request.Substring(headerEnd + 4) : string.Empty;
            if (!string.IsNullOrEmpty(leftover))
            {
                return leftover;
            }

            byte[] buffer = new byte[4096];
            int n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
            return n <= 0 ? string.Empty : Encoding.UTF8.GetString(buffer, 0, n);
        }

        private static int IndexOf(byte[] data, string token)
        {
            string text = Encoding.ASCII.GetString(data);
            return text.IndexOf(token, StringComparison.Ordinal);
        }

        private static Task WriteChunkedStartAsync(Stream stream, string contentType, CancellationToken token)
            => WriteRawAsync(
                stream,
                "HTTP/1.1 200 OK\r\nContent-Type: " + contentType + "\r\nTransfer-Encoding: chunked\r\n\r\n",
                token);

        private static Task WriteChunkAsync(Stream stream, string text, CancellationToken token)
            => WriteChunkBytesAsync(stream, Encoding.UTF8.GetBytes(text), token);

        private static async Task WriteChunkBytesAsync(Stream stream, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            string size = data.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n";
            await WriteRawAsync(stream, size, token).ConfigureAwait(false);
            if (data.Length > 0)
            {
                await stream.WriteAsync(data, token).ConfigureAwait(false);
            }

            await WriteRawAsync(stream, "\r\n", token).ConfigureAwait(false);
        }

        private static async Task WriteRawAsync(Stream stream, string text, CancellationToken token)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
    }
}
