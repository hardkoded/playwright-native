using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.TestServer
{
    /// <summary>
    /// Official Playwright <c>server.waitForWebSocket()</c> socket: send/close
    /// plus raw writes used by handshake-error titles.
    /// </summary>
    public sealed class OfficialServerWebSocket
    {
        private readonly WebSocket _socket;
        private readonly Stream _stream;
        private readonly object _lock = new object();
        private Action<string> _onMessage;
        private Action<int, byte[]> _onClose;
        private readonly List<Action<string>> _messageListeners = new List<Action<string>>();
        private readonly List<Action<int, byte[]>> _closeListeners = new List<Action<int, byte[]>>();
        private readonly List<string> _bufferedMessages = new List<string>();
        private bool _receiveStarted;
        private bool _closed;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        internal OfficialServerWebSocket(WebSocket socket, Stream stream = null)
        {
            _socket = socket;
            _stream = stream;
        }

        internal OfficialServerWebSocket(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <summary>
        /// Registers a one-shot text-or-binary message listener. Binary frames
        /// are decoded as UTF-8, matching Node <c>data.toString()</c>.
        /// </summary>
        /// <param name="handler">Receives the payload as a string.</param>
        public void OnMessage(Action<string> handler)
        {
            if (handler == null)
            {
                return;
            }

            List<string> pending;
            lock (_lock)
            {
                _messageListeners.Add(handler);
                pending = new List<string>(_bufferedMessages);
                _bufferedMessages.Clear();
            }

            foreach (string text in pending)
            {
                handler(text);
            }

            EnsureReceive();
        }

        public void OnceMessage(Action<string> handler)
        {
            lock (_lock)
            {
                _onMessage = handler;
            }

            EnsureReceive();
        }

        /// <summary>
        /// Registers a one-shot close listener.
        /// </summary>
        /// <param name="handler">Receives the close code and reason bytes.</param>
        public void OnceClose(Action<int, byte[]> handler)
        {
            lock (_lock)
            {
                _onClose = handler;
            }

            EnsureReceive();
        }

        /// <summary>
        /// Sends a text frame.
        /// </summary>
        /// <param name="text">UTF-8 payload.</param>
        public void Send(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            if (_socket != null)
            {
                _ = _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                return;
            }

            WriteFrame(opcode: 1, bytes);
        }

        /// <summary>
        /// Sends a binary frame.
        /// </summary>
        /// <param name="payload">Binary payload.</param>
        public void Send(byte[] payload)
        {
            byte[] bytes = payload ?? Array.Empty<byte>();
            if (_socket != null)
            {
                _ = _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, CancellationToken.None);
                return;
            }

            WriteFrame(opcode: 2, bytes);
        }

        /// <summary>
        /// Sends a close frame.
        /// </summary>
        /// <param name="code">WebSocket close code.</param>
        /// <param name="reason">Close reason.</param>
        public void Close(int code, string reason)
        {
            string text = reason ?? string.Empty;
            if (_socket != null)
            {
                WebSocketCloseStatus status = Enum.IsDefined(typeof(WebSocketCloseStatus), code)
                    ? (WebSocketCloseStatus)code
                    : WebSocketCloseStatus.NormalClosure;
                try
                {
                    status = (WebSocketCloseStatus)code;
                    _ = _socket.CloseAsync(status, text, CancellationToken.None);
                }
                catch (ArgumentException)
                {
                    _ = _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, text, CancellationToken.None);
                }

                return;
            }

            byte[] reasonBytes = Encoding.UTF8.GetBytes(text);
            byte[] payload = new byte[2 + reasonBytes.Length];
            payload[0] = (byte)((code >> 8) & 0xFF);
            payload[1] = (byte)(code & 0xFF);
            Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
            WriteFrame(opcode: 8, payload);
        }

        /// <summary>
        /// Writes raw bytes on the upgraded connection (official <c>socket.write</c>).
        /// </summary>
        /// <param name="data">Raw payload, typically invalid WebSocket frames.</param>
        public Task WriteRawAsync(string data)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(data ?? string.Empty);
            if (_stream != null)
            {
                return WriteRawAsync(bytes);
            }

            try
            {
                _socket?.Abort();
            }
            catch (ObjectDisposedException)
            {
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Aborts the underlying connection (official <c>socket.destroy</c>).
        /// </summary>
        public void Destroy()
        {
            try
            {
                _stream?.Dispose();
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _socket?.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void EnsureReceive()
        {
            lock (_lock)
            {
                if (_receiveStarted)
                {
                    return;
                }

                _receiveStarted = true;
            }

            _ = Task.Run(ReceiveLoopAsync);
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                if (_socket != null)
                {
                    await ReceiveSocketAsync().ConfigureAwait(false);
                    return;
                }

                if (_stream != null)
                {
                    await ReceiveStreamAsync().ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                NotifyClose(1006, Array.Empty<byte>());
            }
            catch (WebSocketException)
            {
                NotifyClose(1006, Array.Empty<byte>());
            }
            catch (ObjectDisposedException)
            {
                NotifyClose(1006, Array.Empty<byte>());
            }
        }

        private async Task ReceiveSocketAsync()
        {
            byte[] buffer = new byte[64 * 1024];
            using MemoryStream accumulator = new MemoryStream();
            while (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
            {
                WebSocketReceiveResult result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                    .ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    int code = result.CloseStatus.HasValue ? (int)result.CloseStatus.Value : 1005;
                    byte[] reason = Encoding.UTF8.GetBytes(result.CloseStatusDescription ?? string.Empty);
                    try
                    {
                        await _socket.CloseAsync(
                            result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            result.CloseStatusDescription,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (WebSocketException)
                    {
                    }
                    catch (ArgumentException)
                    {
                    }

                    NotifyClose(code, reason);
                    return;
                }

                await accumulator.WriteAsync(buffer.AsMemory(0, result.Count), CancellationToken.None).ConfigureAwait(false);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                string text = Encoding.UTF8.GetString(accumulator.ToArray());
                accumulator.SetLength(0);
                NotifyMessage(text);
            }
        }

        private async Task ReceiveStreamAsync()
        {
            while (true)
            {
                (int opcode, byte[] payload) = await ReadFrameAsync().ConfigureAwait(false);
                if (opcode == 8)
                {
                    int code = payload.Length >= 2 ? (payload[0] << 8) | payload[1] : 1005;
                    byte[] reason = payload.Length > 2
                        ? payload.AsSpan(2).ToArray()
                        : Array.Empty<byte>();
                    WriteFrame(opcode: 8, payload);
                    NotifyClose(code, reason);
                    return;
                }

                if (opcode == 1 || opcode == 2)
                {
                    NotifyMessage(Encoding.UTF8.GetString(payload));
                }
            }
        }

        private async Task<(int Opcode, byte[] Payload)> ReadFrameAsync()
        {
            byte[] header = await ReadExactAsync(2).ConfigureAwait(false);
            int opcode = header[0] & 0x0F;
            bool masked = (header[1] & 0x80) != 0;
            long length = header[1] & 0x7F;
            if (length == 126)
            {
                byte[] ext = await ReadExactAsync(2).ConfigureAwait(false);
                length = (ext[0] << 8) | ext[1];
            }
            else if (length == 127)
            {
                byte[] ext = await ReadExactAsync(8).ConfigureAwait(false);
                length = 0;
                for (int i = 0; i < 8; i++)
                {
                    length = (length << 8) | ext[i];
                }
            }

            byte[] mask = masked ? await ReadExactAsync(4).ConfigureAwait(false) : Array.Empty<byte>();
            byte[] payload = length == 0 ? Array.Empty<byte>() : await ReadExactAsync((int)length).ConfigureAwait(false);
            if (masked)
            {
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] ^= mask[i % 4];
                }
            }

            return (opcode, payload);
        }

        private async Task<byte[]> ReadExactAsync(int count)
        {
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = await _stream.ReadAsync(buffer.AsMemory(read, count - read)).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new IOException("WebSocket stream closed.");
                }

                read += n;
            }

            return buffer;
        }

        private void WriteFrame(int opcode, byte[] payload)
        {
            if (_stream == null)
            {
                return;
            }

            using MemoryStream output = new MemoryStream();
            output.WriteByte((byte)(0x80 | (opcode & 0x0F)));
            if (payload.Length < 126)
            {
                output.WriteByte((byte)payload.Length);
            }
            else if (payload.Length <= 0xFFFF)
            {
                output.WriteByte(126);
                output.WriteByte((byte)((payload.Length >> 8) & 0xFF));
                output.WriteByte((byte)(payload.Length & 0xFF));
            }
            else
            {
                output.WriteByte(127);
                long length = payload.Length;
                for (int shift = 56; shift >= 0; shift -= 8)
                {
                    output.WriteByte((byte)((length >> shift) & 0xFF));
                }
            }

            output.Write(payload, 0, payload.Length);
            WriteBytes(output.ToArray());
        }

        private void WriteBytes(byte[] bytes)
        {
            if (_stream == null || bytes == null || bytes.Length == 0)
            {
                return;
            }

            _writeLock.Wait();
            try
            {
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task WriteRawAsync(byte[] bytes)
        {
            if (_stream == null || bytes == null || bytes.Length == 0)
            {
                return;
            }

            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(bytes).ConfigureAwait(false);
                await _stream.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private void NotifyMessage(string text)
        {
            Action<string> once;
            List<Action<string>> listeners;
            lock (_lock)
            {
                once = _onMessage;
                _onMessage = null;
                listeners = new List<Action<string>>(_messageListeners);
                if (once == null && listeners.Count == 0)
                {
                    _bufferedMessages.Add(text);
                    return;
                }
            }

            once?.Invoke(text);
            foreach (Action<string> listener in listeners)
            {
                listener(text);
            }
        }

        private void NotifyClose(int code, byte[] reason)
        {
            Action<int, byte[]> handler;
            List<Action<int, byte[]>> listeners;
            lock (_lock)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                handler = _onClose;
                _onClose = null;
                listeners = new List<Action<int, byte[]>>(_closeListeners);
            }

            handler?.Invoke(code, reason ?? Array.Empty<byte>());
            foreach (Action<int, byte[]> listener in listeners)
            {
                listener(code, reason ?? Array.Empty<byte>());
            }
        }
    }
}
