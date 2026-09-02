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

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Low-level keyboard transport. Mirrors upstream <c>input.RawKeyboard</c>: a per-browser
    /// implementation that translates the simulator's high-level requests into protocol commands
    /// (CDP for Chromium, WIP for WebKit). The shared <see cref="Keyboard"/> simulator owns the
    /// pressed-key/modifier state and drives this interface.
    /// </summary>
    internal interface IRawKeyboard
    {
        /// <summary>
        /// Dispatches a key-down event for <paramref name="key"/> while <paramref name="modifiers"/> are held.
        /// </summary>
        /// <param name="modifiers">The modifiers currently held down.</param>
        /// <param name="key">The resolved key definition to dispatch.</param>
        /// <param name="autoRepeat">Whether this is an auto-repeat (the key was already down).</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task KeyDownAsync(IReadOnlyCollection<KeyboardModifier> modifiers, KeyDefinition key, bool autoRepeat);

        /// <summary>
        /// Dispatches a key-up event for <paramref name="key"/> while <paramref name="modifiers"/> are held.
        /// </summary>
        /// <param name="modifiers">The modifiers currently held down.</param>
        /// <param name="key">The resolved key definition to dispatch.</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task KeyUpAsync(IReadOnlyCollection<KeyboardModifier> modifiers, KeyDefinition key);

        /// <summary>
        /// Inserts arbitrary text without simulating individual keystrokes.
        /// </summary>
        /// <param name="text">The text to insert.</param>
        /// <returns>A task that completes when the text has been inserted.</returns>
        Task InsertTextAsync(string text);
    }
}
