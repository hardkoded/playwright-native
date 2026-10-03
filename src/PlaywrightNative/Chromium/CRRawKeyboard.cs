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
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightNative.Input;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Sends CDP <c>Input.dispatchKeyEvent</c> and <c>Input.insertText</c> commands.
    /// Cancels an intercepted HTML5 drag on Escape (upstream <c>crInput.RawKeyboardImpl</c>).
    /// </summary>
    internal class CRRawKeyboard : IRawKeyboard
    {
        private readonly CRSession _session;
        private readonly CRDragManager _dragManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRawKeyboard"/> class.
        /// </summary>
        /// <param name="session">The CDP session to send commands on.</param>
        /// <param name="dragManager">Chromium drag interceptor.</param>
        public CRRawKeyboard(CRSession session, CRDragManager dragManager)
        {
            _session = session;
            _dragManager = dragManager;
        }

        /// <summary>
        /// Dispatches a CDP <c>Input.dispatchKeyEvent</c> <c>keyDown</c> (or <c>rawKeyDown</c>
        /// when there is no text to emit). Escape cancels an in-flight drag.
        /// </summary>
        public async Task KeyDownAsync(IReadOnlyCollection<Input.KeyboardModifier> modifiers, Input.KeyDefinition key, bool autoRepeat)
        {
            if (key.Code == "Escape" && await _dragManager.CancelDragAsync().ConfigureAwait(false))
            {
                return;
            }

            string type = string.IsNullOrEmpty(key.Text) ? "rawKeyDown" : "keyDown";

            await _session.SendAsync("Input.dispatchKeyEvent", new
            {
                type,
                modifiers = modifiers.ToCdpMask(),
                windowsVirtualKeyCode = key.KeyCodeWithoutLocation == 0 ? key.KeyCode : key.KeyCodeWithoutLocation,
                code = key.Code,
                key = key.Key,
                text = key.Text,
                unmodifiedText = key.Text,
                autoRepeat,
                location = key.Location,
                isKeypad = key.Location == 3,
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispatches a CDP <c>Input.dispatchKeyEvent</c> <c>keyUp</c>.
        /// </summary>
        public Task KeyUpAsync(IReadOnlyCollection<Input.KeyboardModifier> modifiers, Input.KeyDefinition key)
        {
            return _session.SendAsync("Input.dispatchKeyEvent", new
            {
                type = "keyUp",
                modifiers = modifiers.ToCdpMask(),
                key = key.Key,
                windowsVirtualKeyCode = key.KeyCodeWithoutLocation == 0 ? key.KeyCode : key.KeyCodeWithoutLocation,
                code = key.Code,
                location = key.Location,
            });
        }

        /// <summary>
        /// Dispatches a CDP <c>Input.insertText</c> command — used for characters that are
        /// not in the US keyboard layout or when inserting literal text.
        /// </summary>
        public Task InsertTextAsync(string text)
        {
            return _session.SendAsync("Input.insertText", new { text });
        }
    }
}
