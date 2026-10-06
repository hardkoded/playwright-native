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
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly bool _isMac;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRawKeyboard"/> class.
        /// </summary>
        /// <param name="session">The CDP session to send commands on.</param>
        /// <param name="dragManager">Chromium drag interceptor.</param>
        /// <param name="isMac">Whether the browser runs on macOS and needs editing commands.</param>
        public CRRawKeyboard(CRSession session, CRDragManager dragManager, bool isMac)
        {
            _session = session;
            _dragManager = dragManager;
            _isMac = isMac;
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
            string[] commands = CommandsForCode(key.Code, modifiers);

            await _session.SendAsync("Input.dispatchKeyEvent", new
            {
                type,
                modifiers = modifiers.ToCdpMask(),
                windowsVirtualKeyCode = key.KeyCodeWithoutLocation == 0 ? key.KeyCode : key.KeyCodeWithoutLocation,
                code = key.Code,
                commands,
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

        /// <summary>
        /// Returns the macOS editing commands Chromium should run for a key press. Chromium
        /// skips native key bindings for synthetic events, so shortcuts like Meta+A need them.
        /// Mirrors upstream <c>crInput.ts</c> <c>_commandsForCode</c>.
        /// </summary>
        private string[] CommandsForCode(string code, IReadOnlyCollection<Input.KeyboardModifier> modifiers)
        {
            if (!_isMac)
            {
                return Array.Empty<string>();
            }

            // Commands that insert text are not supported. Drop the trailing ':' to match the
            // Chromium command names.
            return MacEditingCommands.Resolve(MacEditingCommands.BuildShortcut(modifiers, code))
                .Where(command => !command.StartsWith("insert", StringComparison.Ordinal))
                .Select(command => command[..^1])
                .ToArray();
        }
    }
}
