/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp
{
    /// <summary>
    /// A WebSocket frame payload.
    /// </summary>
    internal sealed partial class WebSocketFrame : IWebSocketFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketFrame"/> class.
        /// </summary>
        /// <param name="text">Text payload, or <see langword="null"/> for binary frames.</param>
        /// <param name="binary">Binary payload, or empty for text frames.</param>
        /// <param name="opcode">WebSocket opcode. <c>1</c> text, <c>2</c> binary.</param>
        /// <param name="wallTimeMs">Browser wall time for the frame, or <c>0</c>.</param>
        internal WebSocketFrame(string text, byte[] binary, int opcode = 1, double wallTimeMs = 0)
        {
            Text = text ?? string.Empty;
            Binary = binary ?? System.Array.Empty<byte>();
            Opcode = opcode;
            WallTimeMs = wallTimeMs;
        }

        /// <inheritdoc/>
        public string Text { get; }

        /// <inheritdoc/>
        public byte[] Binary { get; }

        /// <summary>
        /// Official HAR <c>opcode</c>.
        /// </summary>
        internal int Opcode { get; }

        /// <summary>
        /// Official HAR message <c>time</c> in milliseconds since epoch.
        /// </summary>
        internal double WallTimeMs { get; }
    }
}
