/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>startCCServer</c>: HTTPS (or HTTP/2) origin that reports
    /// the presented client certificate.
    /// </summary>
    internal sealed class OfficialClientCertificateServer : IAsyncDisposable
    {
        private readonly IWebHost _host;

        private OfficialClientCertificateServer(IWebHost host, int port, string url)
        {
            _host = host;
            Port = port;
            Url = url;
        }

        internal int Port { get; }

        internal string Url { get; }

        internal static async Task<OfficialClientCertificateServer> StartAsync(
            bool http2 = false,
            bool enableHttp1Fallback = false)
        {
            X509Certificate2 cert = LoadPem(
                Asset("client-certificates/server/server_cert.pem"),
                Asset("client-certificates/server/server_key.pem"));
            X509Certificate2 ca = X509CertificateLoader.LoadCertificateFromFile(
                Asset("client-certificates/server/server_cert.pem"));
            ConcurrentDictionary<string, string> sni = new();
            int port = 0;
            IWebHost host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 0, listen =>
                    {
                        listen.Protocols = http2
                            ? (enableHttp1Fallback ? HttpProtocols.Http1AndHttp2 : HttpProtocols.Http2)
                            : HttpProtocols.Http1;
                        listen.UseHttps(new HttpsConnectionAdapterOptions
                        {
                            ServerCertificate = cert,
                            ServerCertificateSelector = (context, name) =>
                            {
                                if (context != null)
                                {
                                    sni[context.ConnectionId] = name ?? string.Empty;
                                }

                                return cert;
                            },
                            ClientCertificateMode = ClientCertificateMode.AllowCertificate,
                            ClientCertificateValidation = (_, _, _) => true,
                        });
                    });
                })
                .Configure(app =>
                {
                    app.Run(async context =>
                    {
                        string alpn = ReadAlpn(context);
                        sni.TryGetValue(context.Connection.Id, out string serverName);
                        X509Certificate2 peer = context.Connection.ClientCertificate;
                        bool authorized = IsAuthorized(peer, ca);
                        string message;
                        int status;
                        if (authorized && peer != null)
                        {
                            status = 200;
                            message = "Hello " + SubjectCn(peer) + ", your certificate was issued by " + IssuerCn(peer) + "!";
                        }
                        else if (peer != null && !string.IsNullOrEmpty(SubjectCn(peer)))
                        {
                            status = 403;
                            message = "Sorry " + SubjectCn(peer) + ", certificates from " + IssuerCn(peer) + " are not welcome here.";
                        }
                        else
                        {
                            status = 401;
                            message = "Sorry, but you need to provide a client certificate to continue.";
                        }

                        context.Response.StatusCode = status;
                        context.Response.ContentType = "text/html";
                        string html = string.Concat(
                            "<div data-testid=\"alpn-protocol\">",
                            WebUtility.HtmlEncode(alpn),
                            "</div>",
                            "<div data-testid=\"servername\">",
                            WebUtility.HtmlEncode(serverName ?? string.Empty),
                            "</div>",
                            "<div data-testid=\"message\">",
                            WebUtility.HtmlEncode(message),
                            "</div>");
                        await context.Response.WriteAsync(html).ConfigureAwait(false);
                    });
                })
                .Build();
            await host.StartAsync().ConfigureAwait(false);
            port = host.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
                .Addresses
                .SelectAddressPort();
            return new OfficialClientCertificateServer(
                host,
                port,
                "https://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
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

        internal static string Asset(string relative)
        {
            string fromSource = Path.Combine(TestUtils.FindParentDirectory("PlaywrightNative.Tests"), "Assets", relative);
            if (File.Exists(fromSource))
            {
                return fromSource;
            }

            return Path.Combine(AppContext.BaseDirectory, "Assets", relative);
        }

        internal static X509Certificate2 LoadPem(string certPath, string keyPath)
        {
            string certPem = File.ReadAllText(certPath);
            string keyPem = File.ReadAllText(keyPath);
            using X509Certificate2 loaded = X509Certificate2.CreateFromPem(certPem, keyPem);
            return X509CertificateLoader.LoadPkcs12(
                loaded.Export(X509ContentType.Pfx),
                string.Empty,
                X509KeyStorageFlags.Exportable);
        }

        private static string ReadAlpn(HttpContext context)
        {
            if (string.Equals(context.Request.Protocol, "HTTP/2", StringComparison.Ordinal))
            {
                return "h2";
            }

            return "http/1.1";
        }

        private static bool IsAuthorized(X509Certificate2 peer, X509Certificate2 ca)
        {
            if (peer == null || ca == null)
            {
                return false;
            }

            string issuer = IssuerCn(peer);
            string caName = SubjectCn(ca);
            return !string.IsNullOrEmpty(issuer)
                && (string.Equals(issuer, caName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(issuer, "localhost", StringComparison.OrdinalIgnoreCase));
        }

        private static string SubjectCn(X509Certificate2 cert)
        {
            string name = cert?.GetNameInfo(X509NameType.SimpleName, false);
            return string.IsNullOrEmpty(name) ? string.Empty : name;
        }

        private static string IssuerCn(X509Certificate2 cert)
        {
            string name = cert?.GetNameInfo(X509NameType.SimpleName, true);
            return string.IsNullOrEmpty(name) ? string.Empty : name;
        }
    }

    internal static class OfficialClientCertificateServerExtensions
    {
        internal static int SelectAddressPort(this System.Collections.Generic.ICollection<string> addresses)
        {
            foreach (string address in addresses)
            {
                if (Uri.TryCreate(address, UriKind.Absolute, out Uri uri))
                {
                    return uri.Port;
                }
            }

            throw new InvalidOperationException("Kestrel did not report a listen address.");
        }
    }
}
