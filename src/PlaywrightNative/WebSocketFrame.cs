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
namespace PlaywrightNative
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
