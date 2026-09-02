/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>setupSocksForwardingServer</c>: SOCKS5 CONNECT that
    /// forwards allowed localhost targets to a local HTTP server.
    /// </summary>
    internal sealed class OfficialSocksForwardingProxy : IAsyncDisposable
    {
        private static readonly string[] AllowedHosts =
        {
            "127.0.0.1",
            "localhost",
            "fake-localhost-127-0-0-1.nip.io",
        };

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly int _forwardPort;
        private readonly int _allowedTargetPort;
        private readonly List<string> _connectHosts = new();
        private readonly object _lock = new();

        public OfficialSocksForwardingProxy(int forwardPort, int allowedTargetPort)
        {
            _forwardPort = forwardPort;
            _allowedTargetPort = allowedTargetPort;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port { get; }

        internal string Server => "socks5://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);

        internal IReadOnlyList<string> ConnectHosts
        {
            get
            {
                lock (_lock)
                {
                    return _connectHosts.ToArray();
                }
            }
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

                    (string Host, int Port)? dest = await TryReadConnectAsync(stream).ConfigureAwait(false);
                    if (dest == null)
                    {
                        return;
                    }

                    if (!IsAllowed(dest.Value.Host, dest.Value.Port))
                    {
                        byte[] refused =
                        {
                            0x05, 0x05, 0x00, 0x01,
                            127, 0, 0, 1,
                            0x00, 0x00,
                        };
                        await stream.WriteAsync(refused).ConfigureAwait(false);
                        return;
                    }

                    lock (_lock)
                    {
                        _connectHosts.Add(dest.Value.Host + ":" + dest.Value.Port.ToString(CultureInfo.InvariantCulture));
                    }

                    using TcpClient origin = new TcpClient();
                    await origin.ConnectAsync(IPAddress.Loopback, _forwardPort).ConfigureAwait(false);
                    byte[] reply =
                    {
                        0x05, 0x00, 0x00, 0x01,
                        127, 0, 0, 1,
                        0x00, 0x00,
                    };
                    await stream.WriteAsync(reply).ConfigureAwait(false);
                    await using NetworkStream originStream = origin.GetStream();
                    await PipeBidirectionalAsync(stream, originStream).ConfigureAwait(false);
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
        }

        private bool IsAllowed(string host, int port)
        {
            if (port != _allowedTargetPort || string.IsNullOrEmpty(host))
            {
                return false;
            }

            foreach (string allowed in AllowedHosts)
            {
                if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

        private static async Task<(string Host, int Port)?> TryReadConnectAsync(NetworkStream stream)
        {
            int ver = await ReadByteAsync(stream).ConfigureAwait(false);
            int cmd = await ReadByteAsync(stream).ConfigureAwait(false);
            int rsv = await ReadByteAsync(stream).ConfigureAwait(false);
            int atyp = await ReadByteAsync(stream).ConfigureAwait(false);
            if (ver != 0x05 || cmd != 0x01 || rsv != 0x00)
            {
                return null;
            }

            string host;
            if (atyp == 0x01)
            {
                byte[] addr = new byte[4];
                if (!await ReadExactAsync(stream, addr).ConfigureAwait(false))
                {
                    return null;
                }

                host = new IPAddress(addr).ToString();
            }
            else if (atyp == 0x04)
            {
                byte[] addr = new byte[16];
                if (!await ReadExactAsync(stream, addr).ConfigureAwait(false))
                {
                    return null;
                }

                host = new IPAddress(addr).ToString();
            }
            else if (atyp == 0x03)
            {
                int len = await ReadByteAsync(stream).ConfigureAwait(false);
                if (len < 0)
                {
                    return null;
                }

                byte[] name = new byte[len];
                if (!await ReadExactAsync(stream, name).ConfigureAwait(false))
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
            if (!await ReadExactAsync(stream, portBytes).ConfigureAwait(false))
            {
                return null;
            }

            int port = (portBytes[0] << 8) | portBytes[1];
            return (host, port);
        }

        private static async Task PipeBidirectionalAsync(NetworkStream a, NetworkStream b)
        {
            Task aToB = CopyQuietlyAsync(a, b);
            Task bToA = CopyQuietlyAsync(b, a);
            await Task.WhenAny(aToB, bToA).ConfigureAwait(false);
        }

        private static async Task CopyQuietlyAsync(NetworkStream from, NetworkStream to)
        {
            try
            {
                await from.CopyToAsync(to).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
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
