/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Defines how a single keyboard key dispatches through CDP <c>Input.dispatchKeyEvent</c>.
    /// Mirrors the entries of upstream Playwright's USKeyboardLayout table.
    /// </summary>
    internal sealed class KeyDefinition
    {
        /// <summary>
        /// The Windows Virtual Key Code (e.g. 65 for 'A', 13 for 'Enter').
        /// </summary>
        public int KeyCode { get; init; }

        /// <summary>
        /// Keycode used for modifier lookups when location differs (e.g. both ShiftLeft and
        /// ShiftRight report <c>keyCodeWithoutLocation = 16</c>). Defaults to <see cref="KeyCode"/>.
        /// </summary>
        public int KeyCodeWithoutLocation { get; init; }

        /// <summary>
        /// The physical <c>event.code</c> value (e.g. "KeyA", "Digit1", "Enter", "ArrowLeft").
        /// Shifted variants keep the same code as their base key. Used to build the WebKit
        /// macEditingCommands shortcut.
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// The semantic <c>event.key</c> value (e.g. "a", "Enter", "ArrowUp", "Shift").
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// The text emitted on keyDown (single char, \r for Enter, or empty string for non-text keys).
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Key location: 0=standard, 1=left, 2=right, 3=numpad. Matches the CDP
        /// <c>Input.dispatchKeyEvent.location</c> field.
        /// </summary>
        public int Location { get; init; }

        /// <summary>
        /// The shifted variant. When the Shift modifier is active and this is non-null,
        /// dispatch the shifted key instead.
        /// </summary>
        public KeyDefinition Shifted { get; init; }
    }
}
