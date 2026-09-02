/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Keyboard modifier key. Values match CDP <c>Input.dispatchKeyEvent.modifiers</c> bitmask.
    /// </summary>
    [Flags]
    internal enum KeyboardModifier
    {
        /// <summary>No modifier.</summary>
        None = 0,

        /// <summary>Alt (Option on macOS).</summary>
        Alt = 1,

        /// <summary>Control key.</summary>
        Control = 2,

        /// <summary>Meta (Command on macOS, Windows key on Windows).</summary>
        Meta = 4,

        /// <summary>Shift key.</summary>
        Shift = 8,
    }
}
