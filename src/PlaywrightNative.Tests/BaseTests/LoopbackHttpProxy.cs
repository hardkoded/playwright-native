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

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Minimal HTTP proxy that records absolute-form request targets and returns
    /// a small HTML document so navigations complete. Optional Basic proxy auth.
    /// </summary>
    internal sealed class LoopbackHttpProxy : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<string> _targets = new();
        private readonly Task _acceptLoop;
        private readonly string _username;
        private readonly string _password;

        public LoopbackHttpProxy(string username = null, string password = null)
        {
            _username = username;
            _password = password;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Server = "127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            _acceptLoop = AcceptLoopAsync();
        }

        public int Port { get; }

        public string Server { get; }

        public string[] Targets
        {
            get
            {
                lock (_targets)
                {
                    return _targets.ToArray();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
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
                {
                    await using NetworkStream stream = client.GetStream();
                    using StreamReader reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                    string requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(requestLine))
                    {
                        return;
                    }

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length >= 2)
                    {
                        lock (_targets)
                        {
                            _targets.Add(parts[1]);
                        }
                    }

                    string proxyAuthorization = null;
                    string headerLine;
                    while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync().ConfigureAwait(false)))
                    {
                        if (headerLine.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
                        {
                            proxyAuthorization = headerLine.Substring("Proxy-Authorization:".Length).Trim();
                        }
                    }

                    if (!string.IsNullOrEmpty(_username) && !IsAuthorized(proxyAuthorization))
                    {
                        string challenge =
                            "HTTP/1.1 407 Proxy Authentication Required\r\n" +
                            "Proxy-Authenticate: Basic realm=\"proxy\"\r\n" +
                            "Content-Length: 0\r\n" +
                            "Connection: close\r\n\r\n";
                        byte[] challengeBytes = Encoding.ASCII.GetBytes(challenge);
                        await stream.WriteAsync(challengeBytes).ConfigureAwait(false);
                        return;
                    }

                    string body = "<html><title>proxied</title><body>from-proxy</body></html>";
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    string header =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: text/html; charset=utf-8\r\n" +
                        "Content-Length: " + bodyBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                        "Connection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(headerBytes).ConfigureAwait(false);
                    await stream.WriteAsync(bodyBytes).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private bool IsAuthorized(string proxyAuthorization)
        {
            if (string.IsNullOrEmpty(proxyAuthorization) ||
                !proxyAuthorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string encoded = proxyAuthorization.Substring("Basic ".Length).Trim();
            string expected = Convert.ToBase64String(Encoding.ASCII.GetBytes(_username + ":" + (_password ?? string.Empty)));
            return string.Equals(encoded, expected, StringComparison.Ordinal);
        }
    }
}
