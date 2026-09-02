/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System.Threading.Tasks;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Implements keyboard input for Firefox using the Juggler <c>Page.dispatchKeyEvent</c>
    /// and <c>Page.insertText</c> protocol commands.
    /// </summary>
    internal class FFRawKeyboard
    {
        private readonly FFSession _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFRawKeyboard"/> class.
        /// </summary>
        /// <param name="client">The Juggler session.</param>
        public FFRawKeyboard(FFSession client) => _client = client;

        /// <summary>
        /// Sends a keydown event.
        /// </summary>
        /// <param name="key">The key name (e.g. "Enter", "a").</param>
        /// <param name="code">The physical key code (e.g. "KeyA", "Enter").</param>
        /// <param name="keyCode">The numeric key code.</param>
        /// <param name="modifiers">The active modifier bitmask.</param>
        /// <param name="text">The text to insert (for printable keys).</param>
        /// <param name="autoRepeat">Whether this is an auto-repeat event.</param>
        internal Task KeydownAsync(string key, string code, int keyCode, int modifiers, string text, bool autoRepeat)
            => _client.SendAsync("Page.dispatchKeyEvent", new
            {
                type = "keydown",
                key,
                code,
                keyCode,
                modifiers,
                text = text ?? string.Empty,
                repeat = autoRepeat,
            });

        /// <summary>
        /// Sends a keyup event.
        /// </summary>
        /// <param name="key">The key name.</param>
        /// <param name="code">The physical key code.</param>
        /// <param name="keyCode">The numeric key code.</param>
        /// <param name="modifiers">The active modifier bitmask.</param>
        internal Task KeyupAsync(string key, string code, int keyCode, int modifiers)
            => _client.SendAsync("Page.dispatchKeyEvent", new
            {
                type = "keyup",
                key,
                code,
                keyCode,
                modifiers,
                text = string.Empty,
                repeat = false,
            });

        /// <summary>
        /// Inserts text directly into the focused element.
        /// </summary>
        /// <param name="text">The text to insert.</param>
        internal Task InsertTextAsync(string text)
            => _client.SendAsync("Page.insertText", new { text });
    }
}
