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
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>MockSocksServer</c>: SOCKS5 CONNECT that replies with
    /// <c>Served by the SOCKS proxy</c> HTML.
    /// </summary>
    internal sealed class MockSocksProxy : IAsyncDisposable
    {
        private static readonly byte[] SocksResponse = BuildSocksResponse();

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;

        public MockSocksProxy()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port { get; }

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
                    if (!await HandshakeAsync(stream).ConfigureAwait(false))
                    {
                        return;
                    }

                    if (!await ReadConnectAsync(stream).ConfigureAwait(false))
                    {
                        return;
                    }

                    byte[] reply =
                    {
                        0x05, 0x00, 0x00, 0x01,
                        127, 0, 0, 1,
                        0x00, 0x00,
                    };
                    await stream.WriteAsync(reply).ConfigureAwait(false);

                    byte[] peek = new byte[1];
                    int n = await stream.ReadAsync(peek).ConfigureAwait(false);
                    if (n == 0)
                    {
                        return;
                    }

                    await stream.WriteAsync(SocksResponse).ConfigureAwait(false);
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

        private static async Task<bool> HandshakeAsync(NetworkStream stream)
        {
            int ver = await ReadByteAsync(stream).ConfigureAwait(false);
            int nmethods = await ReadByteAsync(stream).ConfigureAwait(false);
            if (ver != 0x05 || nmethods < 0)
            {
                return false;
            }

            byte[] methods = new byte[nmethods];
            if (!await ReadExactAsync(stream, methods).ConfigureAwait(false))
            {
                return false;
            }

            byte[] choice = { 0x05, 0x00 };
            await stream.WriteAsync(choice).ConfigureAwait(false);
            return true;
        }

        private static async Task<bool> ReadConnectAsync(NetworkStream stream)
        {
            int ver = await ReadByteAsync(stream).ConfigureAwait(false);
            int cmd = await ReadByteAsync(stream).ConfigureAwait(false);
            int rsv = await ReadByteAsync(stream).ConfigureAwait(false);
            int atyp = await ReadByteAsync(stream).ConfigureAwait(false);
            if (ver != 0x05 || cmd != 0x01 || rsv != 0x00)
            {
                return false;
            }

            int addrLen;
            if (atyp == 0x01)
            {
                addrLen = 4;
            }
            else if (atyp == 0x04)
            {
                addrLen = 16;
            }
            else if (atyp == 0x03)
            {
                int len = await ReadByteAsync(stream).ConfigureAwait(false);
                if (len < 0)
                {
                    return false;
                }

                addrLen = len;
            }
            else
            {
                return false;
            }

            byte[] addr = new byte[addrLen + 2];
            return await ReadExactAsync(stream, addr).ConfigureAwait(false);
        }

        private static byte[] BuildSocksResponse()
        {
            string body = "<!DOCTYPE html><title>Served by the SOCKS proxy</title>";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string header =
                "HTTP/1.1 200 OK\r\n" +
                "Connection: close\r\n" +
                "Content-Type: text/html\r\n" +
                "Content-Length: " + bodyBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            byte[] response = new byte[headerBytes.Length + bodyBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, response, 0, headerBytes.Length);
            Buffer.BlockCopy(bodyBytes, 0, response, headerBytes.Length, bodyBytes.Length);
            return response;
        }

        private static async Task<int> ReadByteAsync(NetworkStream stream)
        {
            byte[] one = new byte[1];
            int n = await stream.ReadAsync(one).ConfigureAwait(false);
            return n == 0 ? -1 : one[0];
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset)).ConfigureAwait(false);
                if (n == 0)
                {
                    return false;
                }

                offset += n;
            }

            return true;
        }
    }
}
