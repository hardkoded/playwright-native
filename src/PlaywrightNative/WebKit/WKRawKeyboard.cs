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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlaywrightNative.Input;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// WebKit raw keyboard. Sends WIP <c>Input.dispatchKeyEvent</c> on the page-proxy session
    /// and inserts text via <c>Page.insertText</c> on the inner target session. Mirrors
    /// upstream <c>wkInput.ts</c> <c>RawKeyboardImpl</c>.
    /// </summary>
    internal sealed class WKRawKeyboard : IRawKeyboard
    {
        private readonly WKPage _page;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKRawKeyboard"/> class.
        /// </summary>
        /// <param name="page">The owning WebKit page.</param>
        public WKRawKeyboard(WKPage page)
        {
            _page = page;
        }

        /// <summary>
        /// Translates a set of modifiers into the WebKit modifier bitmask
        /// (Shift=1, Control=2, Alt=4, Meta=8 — from <c>Source/WebKit/Shared/WebEvent.h</c>).
        /// </summary>
        /// <param name="modifiers">The held modifiers.</param>
        /// <returns>The WebKit modifier bitmask.</returns>
        public static int ToWebKitModifiersMask(IReadOnlyCollection<Input.KeyboardModifier> modifiers)
        {
            int mask = 0;
            if (modifiers != null)
            {
                foreach (Input.KeyboardModifier modifier in modifiers)
                {
                    mask |= modifier switch
                    {
                        Input.KeyboardModifier.Shift => 1,
                        Input.KeyboardModifier.Control => 2,
                        Input.KeyboardModifier.Alt => 4,
                        Input.KeyboardModifier.Meta => 8,
                        _ => 0,
                    };
                }
            }

            return mask;
        }

        /// <summary>
        /// Builds the macEditingCommands shortcut string for a key press: held modifiers
        /// (in <c>Shift+Control+Alt+Meta</c> order) followed by the physical key code,
        /// joined with <c>+</c>. Mirrors upstream <c>wkInput.ts</c>.
        /// </summary>
        /// <param name="modifiers">The held modifiers.</param>
        /// <param name="code">The physical <c>event.code</c> of the key.</param>
        /// <returns>The shortcut string used to look up macOS editing commands.</returns>
        public static string BuildShortcut(IReadOnlyCollection<Input.KeyboardModifier> modifiers, string code)
        {
            List<string> parts = new();
            if (modifiers != null)
            {
                if (modifiers.Contains(Input.KeyboardModifier.Shift))
                {
                    parts.Add("Shift");
                }

                if (modifiers.Contains(Input.KeyboardModifier.Control))
                {
                    parts.Add("Control");
                }

                if (modifiers.Contains(Input.KeyboardModifier.Alt))
                {
                    parts.Add("Alt");
                }

                if (modifiers.Contains(Input.KeyboardModifier.Meta))
                {
                    parts.Add("Meta");
                }
            }

            parts.Add(code);
            return string.Join("+", parts);
        }

        /// <inheritdoc/>
        public Task KeyDownAsync(IReadOnlyCollection<Input.KeyboardModifier> modifiers, Input.KeyDefinition key, bool autoRepeat)
        {
            string shortcut = BuildShortcut(modifiers, key.Code);

            // Linux/Windows WebKit uses native Ctrl+A select-all. The macOS table maps
            // Control+KeyA to moveToBeginningOfParagraph, which would steal selectAll.
            string[] macCommands = MacEditingCommands.Resolve(shortcut);
            if (!OperatingSystem.IsMacOS()
                && string.Equals(shortcut, "Control+KeyA", StringComparison.Ordinal))
            {
                macCommands = Array.Empty<string>();
            }

            return _page.Session.SendAsync("Input.dispatchKeyEvent", new
            {
                type = "keyDown",
                modifiers = ToWebKitModifiersMask(modifiers),
                windowsVirtualKeyCode = key.KeyCode == 0 ? key.KeyCodeWithoutLocation : key.KeyCode,
                code = key.Code,
                key = key.Key,
                text = key.Text,
                unmodifiedText = key.Text,
                autoRepeat,
                macCommands,
                location = key.Location,
                isKeypad = key.Location == 3,
            });
        }

        /// <inheritdoc/>
        public Task KeyUpAsync(IReadOnlyCollection<Input.KeyboardModifier> modifiers, Input.KeyDefinition key)
        {
            return _page.Session.SendAsync("Input.dispatchKeyEvent", new
            {
                type = "keyUp",
                modifiers = ToWebKitModifiersMask(modifiers),
                key = key.Key,
                windowsVirtualKeyCode = key.KeyCode == 0 ? key.KeyCodeWithoutLocation : key.KeyCode,
                code = key.Code,
                location = key.Location,
                isKeypad = key.Location == 3,
            });
        }

        /// <inheritdoc/>
        public Task InsertTextAsync(string text)
        {
            WKTargetSession target = _page.CurrentTargetSession
                ?? throw new PlaywrightNativeException("Cannot insert text: the page has no active target session.");

            return target.SendAsync("Page.insertText", new { text });
        }
    }
}
