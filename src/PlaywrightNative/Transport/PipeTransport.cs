using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.Transport
{
    /// <summary>
    /// Pipe-based transport that communicates with a browser process via stdin/stdout
    /// using null-delimited ('\0') JSON messages.
    /// </summary>
    internal class PipeTransport : IConnectionTransport
    {
        private const int DefaultBufferSize = 32 * 1024;
        private readonly Stream _pipeWrite;
        private readonly Stream _pipeRead;
        private readonly ILogger<PipeTransport> _logger;
        private readonly CancellationTokenSource _readerCancellationSource = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly List<byte> _pendingBytes = new();
        private bool _closed;
        private bool _disposed;

        internal PipeTransport(Stream pipeWrite, Stream pipeRead, ILoggerFactory loggerFactory = null, TransportTaskScheduler scheduler = null)
        {
            _pipeWrite = pipeWrite ?? throw new ArgumentNullException(nameof(pipeWrite));
            _pipeRead = pipeRead ?? throw new ArgumentNullException(nameof(pipeRead));
            _logger = loggerFactory?.CreateLogger<PipeTransport>();
            scheduler ??= ScheduleTransportTask;

            scheduler(ReadLoopAsync, _readerCancellationSource.Token);
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        ~PipeTransport() => Dispose(false);

        /// <inheritdoc/>
        public Action<ProtocolResponse> OnMessage { get; set; }

        /// <inheritdoc/>
        public Action<string> OnClose { get; set; }

        /// <inheritdoc/>
        public async Task SendAsync(ProtocolRequest request)
        {
            try
            {
                if (_readerCancellationSource.IsCancellationRequested)
                {
                    return;
                }

                string json = JsonSerializer.Serialize(request);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                byte[] nullTerminator = new byte[] { 0 };

                await _writeLock.WaitAsync(_readerCancellationSource.Token).ConfigureAwait(false);
                try
                {
#if NETSTANDARD
#pragma warning disable CA1835 // We can't use ReadOnlyMemory on netstandard
                    await _pipeWrite.WriteAsync(bytes, 0, bytes.Length, _readerCancellationSource.Token).ConfigureAwait(false);
                    await _pipeWrite.WriteAsync(nullTerminator, 0, 1, _readerCancellationSource.Token).ConfigureAwait(false);
#pragma warning restore CA1835
#else
                    await _pipeWrite.WriteAsync(bytes.AsMemory(), _readerCancellationSource.Token).ConfigureAwait(false);
                    await _pipeWrite.WriteAsync(nullTerminator.AsMemory(0, 1), _readerCancellationSource.Token).ConfigureAwait(false);
#endif
                    await _pipeWrite.FlushAsync(_readerCancellationSource.Token).ConfigureAwait(false);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PipeTransport send error");
                await CloseAsync("Send error: " + ex.Message).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task CloseAsync() => CloseAsync("Connection closed");

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private static void ScheduleTransportTask(Func<CancellationToken, Task> func, CancellationToken cancellationToken)
            => Task.Factory.StartNew(() => func(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

        private Task CloseAsync(string reason)
        {
            if (!_closed)
            {
                _closed = true;
#pragma warning disable VSTHRD103 // CancelAsync not available on netstandard2.1
                _readerCancellationSource.Cancel();
#pragma warning restore VSTHRD103
                OnClose?.Invoke(reason);
            }

            return Task.CompletedTask;
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!disposing)
            {
                return;
            }

            if (!_closed)
            {
                _closed = true;
#pragma warning disable VSTHRD103 // CancelAsync not available on netstandard2.1
                _readerCancellationSource.Cancel();
#pragma warning restore VSTHRD103
            }

            _readerCancellationSource.Dispose();
            _writeLock.Dispose();

            // Parent ends of the anonymous pipes must be closed or each failed
            // browser launch/dispose cycle leaks FDs (WebKit CI hits EMFILE).
            try
            {
                _pipeWrite.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _pipeRead.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            try
            {
                byte[] buffer = new byte[DefaultBufferSize];

                while (!token.IsCancellationRequested)
                {
#if NETSTANDARD
#pragma warning disable CA1835 // We can't use ReadOnlyMemory on netstandard
                    int read = await _pipeRead.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
#pragma warning restore CA1835
#else
                    int read = await _pipeRead.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
#endif

                    if (read == 0)
                    {
                        // Stream closed (EOF).
                        await CloseAsync("Pipe stream closed").ConfigureAwait(false);
                        return;
                    }

                    if (!token.IsCancellationRequested)
                    {
                        ProcessBuffer(buffer, read);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — ignore.
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PipeTransport read error");
                await CloseAsync("Read error: " + ex.Message).ConfigureAwait(false);
            }
        }

        private void ProcessBuffer(byte[] buffer, int length)
        {
            int start = 0;

            for (int i = 0; i < length; i++)
            {
                if (buffer[i] == '\0')
                {
                    // Found a null delimiter — extract the complete message.
                    int segmentLength = i - start;
                    if (segmentLength > 0)
                    {
                        _pendingBytes.AddRange(new ReadOnlySpan<byte>(buffer, start, segmentLength).ToArray());
                    }

                    if (_pendingBytes.Count > 0)
                    {
                        string json = Encoding.UTF8.GetString(_pendingBytes.ToArray());
                        _pendingBytes.Clear();

                        ProtocolResponse response = JsonSerializer.Deserialize<ProtocolResponse>(json);
                        OnMessage?.Invoke(response);
                    }

                    start = i + 1;
                }
            }

            // Accumulate any remaining bytes after the last null delimiter (partial message).
            if (start < length)
            {
                _pendingBytes.AddRange(new ReadOnlySpan<byte>(buffer, start, length - start).ToArray());
            }
        }
    }
}
