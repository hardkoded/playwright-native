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
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/global-fetch-cookie.spec.ts</c> parity. Skip
    /// Node-only <c>__testHookLookup</c> titles:
    /// <c>should store cookie from Set-Cookie header</c>,
    /// <c>should filter outgoing cookies by domain</c>,
    /// <c>should do case-insensitive match of cookie domain</c>,
    /// <c>should do case-insensitive match of request domain</c>,
    /// <c>should send secure cookie over http for subdomains of localhost</c>,
    /// <c>should store cookie from Set-Cookie header even if it contains equal signs</c>,
    /// <c>should export cookies to storage state</c>, and
    /// <c>should send cookies from storage state</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryGlobalFetchCookieParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19878;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string portText = port.ToString(CultureInfo.InvariantCulture);
                    Prefix = "http://localhost:" + portText;
                    EmptyPage = Prefix + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }

            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [TearDown]
        public void TearDown()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should filter outgoing cookies by path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterOutgoingCookiesByPath()
        {
            EnsureServer();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=v; path=/input/subfolder");
                    http.Response.Headers.Append("Set-Cookie", "b=v; path=/input");
                    http.Response.Headers.Append("Set-Cookie", "c=v;");
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest(
                    "/input/button.html",
                    req => req.Headers["Cookie"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(Prefix + "/input/button.html");
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("b=v; c=v"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should send secure cookie over https")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendSecureCookieOverHttps()
        {
            EnsureServer();
            await using CookieCaptureHttpsServer https = CookieCaptureHttpsServer.Start();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=v; secure");
                    http.Response.Headers.Append("Set-Cookie", "b=v");
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                Task<string> serverRequest = https.WaitForRequest("/empty.html");
                Task<IAPIResponse> responseTask = request.GetAsync(https.EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("a=v; b=v"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should send secure cookie over http for localhost")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendSecureCookieOverHttpForLocalhost()
        {
            EnsureServer();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=v; secure");
                    http.Response.Headers.Append("Set-Cookie", "b=v");
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    req => req.Headers["Cookie"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("a=v; b=v"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should send not expired cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendNotExpiredCookies()
        {
            EnsureServer();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                DateTimeOffset tomorrow = DateTimeOffset.Now.AddDays(1);
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=v");
                    http.Response.Headers.Append("Set-Cookie", "b=v; expires=" + ToUtcString(tomorrow));
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    req => req.Headers["Cookie"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("a=v; b=v"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should remove expired cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveExpiredCookies()
        {
            EnsureServer();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=v");
                    http.Response.Headers.Append("Set-Cookie", "b=v; expires=" + ToUtcString(DateTimeOffset.Now));
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    req => req.Headers["Cookie"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("a=v"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should remove cookie with negative max-age")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookieWithNegativeMaxAge()
        {
            EnsureServer();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=v; max-age=100000");
                    http.Response.Headers.Append("Set-Cookie", "b=v; max-age=100000");
                    http.Response.Headers.Append("Set-Cookie", "c=v");
                    return Task.CompletedTask;
                });
                Server.SetRoute("/removecookie.html", http =>
                {
                    long maxAge = -2L * DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    http.Response.Headers.Append(
                        "Set-Cookie",
                        "a=v; max-age=" + maxAge.ToString(CultureInfo.InvariantCulture));
                    http.Response.Headers.Append("Set-Cookie", "b=v; max-age=-1");
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                await request.GetAsync(Prefix + "/removecookie.html").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    req => req.Headers["Cookie"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.EqualTo("c=v"));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should remove cookie with expires far in the past")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveCookieWithExpiresFarInThePast()
        {
            EnsureServer();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=v; max-age=1000000");
                    return Task.CompletedTask;
                });
                Server.SetRoute("/removecookie.html", http =>
                {
                    http.Response.Headers.Append(
                        "Set-Cookie",
                        "a=v; expires=1 Jan 1000 00:00:00 +0000 (UTC)");
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                await request.GetAsync(Prefix + "/removecookie.html").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    req => req.Headers["Cookie"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.Null.Or.Empty);
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should override cookie from Set-Cookie header")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverrideCookieFromSetCookieHeader()
        {
            EnsureServer();
            DateTimeOffset tomorrow = DateTimeOffset.Now.AddDays(1);
            DateTimeOffset dayAfterTomorrow = tomorrow.AddDays(1);
            long dayAfterTomorrowInSeconds = dayAfterTomorrow.ToUnixTimeSeconds();
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=old; expires=" + ToUtcString(tomorrow));
                    return Task.CompletedTask;
                });
                Server.SetRoute("/updatecookie.html", http =>
                {
                    http.Response.Headers.Append(
                        "Set-Cookie",
                        "a=new; expires=" + ToUtcString(dayAfterTomorrow));
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                await request.GetAsync(Prefix + "/updatecookie.html").ConfigureAwait(false);
                string json = await request.StorageStateAsync().ConfigureAwait(false);
                StorageState state = StorageStateHelper.Load(json, null);
                Assert.That(state.Cookies, Has.Count.EqualTo(1));
                Cookie cookie = null;
                foreach (Cookie item in state.Cookies)
                {
                    cookie = item;
                    break;
                }

                Assert.That(cookie, Is.Not.Null);
                Assert.That(cookie.Name, Is.EqualTo("a"));
                Assert.That(cookie.Value, Is.EqualTo("new"));
                Assert.That(cookie.Expires, Is.EqualTo(dayAfterTomorrowInSeconds));
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should override cookie from Set-Cookie header even if it expired")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverrideCookieFromSetCookieHeaderEvenIfItExpired()
        {
            EnsureServer();
            DateTimeOffset tomorrow = DateTimeOffset.Now.AddDays(1);
            DateTime past = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
            string pastDateString = past.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
            IAPIRequestContext request = await NewRequestAsync().ConfigureAwait(false);
            try
            {
                Server.SetRoute("/setcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=ok");
                    http.Response.Headers.Append("Set-Cookie", "b=ok; expires=" + ToUtcString(tomorrow));
                    return Task.CompletedTask;
                });
                Server.SetRoute("/unsetsetcookie.html", http =>
                {
                    http.Response.Headers.Append("Set-Cookie", "a=; expires=" + pastDateString);
                    http.Response.Headers.Append("Set-Cookie", "b=; expires=" + pastDateString);
                    return Task.CompletedTask;
                });
                await request.GetAsync(Prefix + "/setcookie.html").ConfigureAwait(false);
                await request.GetAsync(Prefix + "/unsetsetcookie.html").ConfigureAwait(false);
                Task<string> serverRequest = Server.WaitForRequest(
                    "/empty.html",
                    req => req.Headers["Cookie"].ToString());
                Task<IAPIResponse> responseTask = request.GetAsync(EmptyPage);
                await Task.WhenAll(serverRequest, responseTask).ConfigureAwait(false);
                Assert.That(serverRequest.Result, Is.Null.Or.Empty);
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should preserve local storage on import/export of storage state")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPreserveLocalStorageOnImportExportOfStorageState()
        {
            EnsureServer();
            const string storageState =
                "{\"cookies\":[{\"name\":\"a\",\"value\":\"b\",\"domain\":\"a.b.one.com\",\"path\":\"/\",\"expires\":-1,\"httpOnly\":false,\"secure\":false,\"sameSite\":\"Lax\"}],\"origins\":[{\"origin\":\"https://www.example.com\",\"localStorage\":[{\"name\":\"name1\",\"value\":\"value1\"}],\"indexedDB\":[{\"name\":\"db\",\"version\":5,\"stores\":[{\"name\":\"store\",\"keyPath\":\"id\",\"autoIncrement\":false,\"indexes\":[],\"records\":[{\"value\":{\"id\":\"foo\",\"name\":\"John Doe\"}}]}]}]}]}";
            IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageState = storageState }).ConfigureAwait(false);
            try
            {
                await request.GetAsync(EmptyPage).ConfigureAwait(false);
                string exported = await request.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false);
                AssertJsonEqual(storageState, exported);
            }
            finally
            {
                await request.DisposeAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "storage state should round-trip through file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task StorageStateShouldRoundTripThroughFile()
        {
            const string storageState =
                "{\"cookies\":[{\"name\":\"a\",\"value\":\"b\",\"domain\":\"a.b.one.com\",\"path\":\"/\",\"expires\":-1,\"httpOnly\":false,\"secure\":false,\"sameSite\":\"Lax\"}],\"origins\":[]}";
            IAPIRequestContext request1 = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageState = storageState }).ConfigureAwait(false);
            string path = Path.Combine(
                Path.GetTempPath(),
                "pwsharp-wave878-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                string state1 = await request1.StorageStateAsync(path).ConfigureAwait(false);
                AssertJsonEqual(storageState, state1);
                string written = File.ReadAllText(path);
                Assert.That(StorageStateHelper.PrettyPrint(state1), Is.EqualTo(written));

                IAPIRequestContext request2 = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageStatePath = path }).ConfigureAwait(false);
                try
                {
                    string state2 = await request2.StorageStateAsync().ConfigureAwait(false);
                    AssertJsonEqual(storageState, state2);
                }
                finally
                {
                    await request2.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await request1.DisposeAsync().ConfigureAwait(false);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [PlaywrightTest("global-fetch-cookie.spec.ts", "should work with empty storage state")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithEmptyStorageState()
        {
            EnsureServer();
            string storageState = Path.Combine(
                Path.GetTempPath(),
                "pwsharp-wave878-empty-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(storageState, "{}");
            IAPIRequestContext request1 = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true, StorageStatePath = storageState }).ConfigureAwait(false);
            try
            {
                string state1 = await request1.StorageStateAsync().ConfigureAwait(false);
                AssertJsonEqual("{\"cookies\":[],\"origins\":[]}", state1);
                IAPIResponse response = await request1.GetAsync(EmptyPage).ConfigureAwait(false);
                Assert.That(response.Ok, Is.True);
            }
            finally
            {
                await request1.DisposeAsync().ConfigureAwait(false);
                if (File.Exists(storageState))
                {
                    File.Delete(storageState);
                }
            }
        }

        private static Task<IAPIRequestContext> NewRequestAsync()
            => Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true });

        private static string ToUtcString(DateTimeOffset value)
            => value.UtcDateTime.ToString("r", CultureInfo.InvariantCulture);

        private static void AssertJsonEqual(string expected, string actual)
        {
            JsonNode expectedNode = JsonNode.Parse(expected);
            JsonNode actualNode = JsonNode.Parse(actual);
            Assert.That(JsonNode.DeepEquals(expectedNode, actualNode), Is.True, actual);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        /// <summary>
        /// Minimal HTTPS origin that records the Cookie header, used when
        /// official <c>httpsServer</c> / <c>testCert.cer</c> is unavailable.
        /// </summary>
        private sealed class CookieCaptureHttpsServer : IAsyncDisposable
        {
            private static readonly byte[] Body = Encoding.UTF8.GetBytes("<html></html>");

            private readonly TcpListener _listener;
            private readonly X509Certificate2 _cert;
            private readonly CancellationTokenSource _cts = new();
            private readonly Task _acceptLoop;
            private readonly object _gate = new();
            private TaskCompletionSource<(string Path, string Cookie)> _wait;

            private CookieCaptureHttpsServer(TcpListener listener, X509Certificate2 cert)
            {
                _listener = listener;
                _cert = cert;
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                _acceptLoop = AcceptLoopAsync();
            }

            internal int Port { get; }

            internal string EmptyPage => "https://localhost:" + Port.ToString(CultureInfo.InvariantCulture) + "/empty.html";

            internal static CookieCaptureHttpsServer Start()
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
                return new CookieCaptureHttpsServer(listener, cert);
            }

            internal Task<string> WaitForRequest(string path)
            {
                TaskCompletionSource<(string Path, string Cookie)> wait =
                    new TaskCompletionSource<(string Path, string Cookie)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_gate)
                {
                    _wait = wait;
                }

                return wait.Task.ContinueWith(
                    task =>
                    {
                        Assert.That(task.Result.Path, Is.EqualTo(path));
                        return task.Result.Cookie;
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
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
                        string raw = await DrainRequestAsync(ssl).ConfigureAwait(false);
                        string path = ReadRequestPath(raw);
                        string cookie = ReadHeader(raw, "Cookie");
                        TaskCompletionSource<(string Path, string Cookie)> wait;
                        lock (_gate)
                        {
                            wait = _wait;
                            _wait = null;
                        }

                        wait?.TrySetResult((path, cookie));
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

            private static async Task<string> DrainRequestAsync(Stream stream)
            {
                byte[] buffer = new byte[4096];
                MemoryStream acc = new MemoryStream();
                while (acc.Length < 64 * 1024)
                {
                    int n = await stream.ReadAsync(buffer).ConfigureAwait(false);
                    if (n == 0)
                    {
                        break;
                    }

                    acc.Write(buffer, 0, n);
                    byte[] data = acc.ToArray();
                    if (IndexOfHeaderEnd(data) >= 0)
                    {
                        return Encoding.ASCII.GetString(data);
                    }
                }

                return Encoding.ASCII.GetString(acc.ToArray());
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

            private static string ReadRequestPath(string raw)
            {
                int lineEnd = raw.IndexOf("\r\n", StringComparison.Ordinal);
                string line = lineEnd >= 0 ? raw.Substring(0, lineEnd) : raw;
                string[] parts = line.Split(' ');
                if (parts.Length < 2)
                {
                    return string.Empty;
                }

                string target = parts[1];
                int query = target.IndexOf('?');
                return query >= 0 ? target.Substring(0, query) : target;
            }

            private static string ReadHeader(string raw, string name)
            {
                string prefix = name + ":";
                using StringReader reader = new StringReader(raw);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring(prefix.Length).Trim();
                    }
                }

                return string.Empty;
            }
        }
    }
}
