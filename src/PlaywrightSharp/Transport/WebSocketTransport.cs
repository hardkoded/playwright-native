using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.Transport.Protocol;

namespace PlaywrightSharp.Transport
{
    /// <summary>
    /// WebSocket-based transport for communicating with browser debug endpoints.
    /// </summary>
    internal class WebSocketTransport : IConnectionTransport
    {
        private const int DefaultBufferSize = 256 * 1024;

        private readonly ClientWebSocket _webSocket;
        private readonly TaskQueue _sendQueue = new();
        private readonly CancellationTokenSource _readerCancellationSource = new();
        private bool _isClosed;

        private WebSocketTransport(ClientWebSocket webSocket)
        {
            _webSocket = webSocket;

            // Fire-and-forget: schedule the long-running read loop.
            _ = Task.Factory.StartNew(
                () => ReceiveLoopAsync(_readerCancellationSource.Token),
                _readerCancellationSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        ~WebSocketTransport() => Dispose(false);

        /// <summary>
        /// Gets or sets the callback invoked when a protocol message is received.
        /// </summary>
        public Action<ProtocolResponse> OnMessage { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the transport is closed.
        /// </summary>
        public Action<string> OnClose { get; set; }

        /// <summary>
        /// Connects to a browser WebSocket debug endpoint.
        /// </summary>
        /// <param name="url">The WebSocket URL to connect to.</param>
        /// <param name="headers">Optional additional headers to send during the WebSocket handshake.</param>
        /// <param name="timeout">Connection timeout in milliseconds. Defaults to 30000.</param>
        /// <returns>A connected <see cref="WebSocketTransport"/> instance.</returns>
        public static async Task<WebSocketTransport> ConnectAsync(
            string url,
            IEnumerable<KeyValuePair<string, string>> headers = null,
            int timeout = 30000)
        {
            var webSocket = new ClientWebSocket();

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    webSocket.Options.SetRequestHeader(header.Key, header.Value);
                }
            }

            using var timeoutCts = new CancellationTokenSource(timeout);

            try
            {
                await webSocket.ConnectAsync(new Uri(url), timeoutCts.Token).ConfigureAwait(false);
            }
            catch
            {
                webSocket.Dispose();
                throw;
            }

            return new WebSocketTransport(webSocket);
        }

        /// <summary>
        /// Sends a protocol request message over the WebSocket connection.
        /// </summary>
        /// <param name="request">The protocol request to send.</param>
        /// <returns>A task that completes when the message has been sent.</returns>
        public Task SendAsync(ProtocolRequest request)
        {
            return _sendQueue.EnqueueAsync(async () =>
            {
                if (_isClosed)
                {
                    return;
                }

                try
                {
                    string json = JsonSerializer.Serialize(request);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        _readerCancellationSource.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    CloseWithReason(ex.Message);
                }
            });
        }

        /// <summary>
        /// Closes the WebSocket connection gracefully.
        /// </summary>
        /// <returns>A task that completes when the connection is closed.</returns>
        public async Task CloseAsync()
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;

            try
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
                {
                    using CancellationTokenSource closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Close requested",
                        closeCts.Token).ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore close errors -- the socket may already be in a faulted state.
            }
            finally
            {
#pragma warning disable VSTHRD103 // CancelAsync not available on netstandard2.1
                _readerCancellationSource.Cancel();
#pragma warning restore VSTHRD103
                OnClose?.Invoke("Close requested");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (!_readerCancellationSource.IsCancellationRequested)
            {
#pragma warning disable VSTHRD103 // CancelAsync not available on netstandard2.1
                _readerCancellationSource.Cancel();
#pragma warning restore VSTHRD103
            }

            _webSocket?.Dispose();
            _sendQueue?.Dispose();
            _readerCancellationSource.Dispose();
        }

        private void CloseWithReason(string reason)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
#pragma warning disable VSTHRD103 // CancelAsync not available on netstandard2.1
            _readerCancellationSource.Cancel();
#pragma warning restore VSTHRD103
            OnClose?.Invoke(reason);
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[DefaultBufferSize];
            StringBuilder messageBuffer = new StringBuilder();

            try
            {
                while (!token.IsCancellationRequested
                    && _webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _webSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            token).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            CloseWithReason(result.CloseStatusDescription ?? "WebSocket closed");
                            return;
                        }

                        messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    string json = messageBuffer.ToString();
                    messageBuffer.Clear();

                    if (json.Length > 0)
                    {
                        ProtocolResponse response = JsonSerializer.Deserialize<ProtocolResponse>(json);
                        OnMessage?.Invoke(response);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Expected during shutdown -- do not treat as an error.
            }
            catch (WebSocketException ex)
            {
                CloseWithReason(ex.Message);
            }
            catch (Exception ex)
            {
                CloseWithReason(ex.Message);
            }
        }
    }
}
