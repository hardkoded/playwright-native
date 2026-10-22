/*
 * Copyright (c) Microsoft Corporation.
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Port of upstream <c>httpRequest</c> (<c>packages/utils/network.ts</c>):
    /// <c>socketTimeout</c> is a socket idle timeout that applies from socket
    /// creation, so it covers both a stalled TCP connect and a stalled response.
    /// </summary>
    internal static class HttpRequestHelper
    {
        /// <summary>Upstream <c>NET_DEFAULT_TIMEOUT</c>.</summary>
        internal const int NetDefaultTimeout = 30_000;

        internal static SocketsHttpHandler CreateHandler(int socketTimeout)
            => new SocketsHttpHandler
            {
                ConnectCallback = (context, cancellationToken) =>
                    ConnectAsync(context.DnsEndPoint, socketTimeout, cancellationToken),
            };

        internal static async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            HttpRequestMessage request,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (FindTimeout(ex) is TimeoutException timeout)
            {
                throw new HttpRequestException($"Request to {request.RequestUri} {timeout.Message}", ex);
            }
        }

        private static TimeoutException FindTimeout(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is TimeoutException timeout)
                {
                    return timeout;
                }
            }

            return null;
        }

        private static IOException TimedOut(int socketTimeout)
        {
            string message = $"timed out after {socketTimeout}ms";
            return new IOException(message, new TimeoutException(message));
        }

        private static async ValueTask<Stream> ConnectAsync(
            DnsEndPoint endPoint,
            int socketTimeout,
            CancellationToken cancellationToken)
        {
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(socketTimeout);
                    try
                    {
                        await socket.ConnectAsync(endPoint, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw TimedOut(socketTimeout);
                    }
                }

                Stream result = new SocketTimeoutStream(new NetworkStream(socket, ownsSocket: true), socketTimeout);
                socket = null;
                return result;
            }
            finally
            {
                socket?.Dispose();
            }
        }

        /// <summary>
        /// Fails a read or write that sees no socket activity for
        /// <c>socketTimeout</c> ms (Node <c>socket.setTimeout</c>).
        /// </summary>
        private sealed class SocketTimeoutStream : Stream
        {
            private readonly Stream _inner;
            private readonly int _socketTimeout;

            public SocketTimeoutStream(Stream inner, int socketTimeout)
            {
                _inner = inner;
                _socketTimeout = socketTimeout;
            }

            public override bool CanRead => _inner.CanRead;

            public override bool CanSeek => false;

            public override bool CanWrite => _inner.CanWrite;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();

            public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_socketTimeout);
                try
                {
                    return await _inner.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw TimedOut(_socketTimeout);
                }
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_socketTimeout);
                try
                {
                    await _inner.WriteAsync(buffer, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw TimedOut(_socketTimeout);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
