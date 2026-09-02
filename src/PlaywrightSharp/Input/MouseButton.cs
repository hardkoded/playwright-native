/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Mouse button. String values correspond to CDP <c>Input.dispatchMouseEvent.button</c>
    /// enum ("none", "left", "right", "middle").
    /// </summary>
    internal enum MouseButton
    {
        /// <summary>No button.</summary>
        None = 0,

        /// <summary>Left button.</summary>
        Left = 1,

        /// <summary>Right button.</summary>
        Right = 2,

        /// <summary>Middle button.</summary>
        Middle = 4,
    }
}
