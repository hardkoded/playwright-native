/*
 * Copyright (c) 2020 Darío Kondratiuk
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
