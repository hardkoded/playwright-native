/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Helpers for mouse button CDP representations.
    /// </summary>
    internal static class MouseButtonExtensions
    {
        /// <summary>
        /// Returns the CDP <c>button</c> string for a single mouse button.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <returns>"left", "right", "middle", or "none".</returns>
        internal static string ToCdpName(this MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => "left",
                MouseButton.Right => "right",
                MouseButton.Middle => "middle",
                _ => "none",
            };
        }

        /// <summary>
        /// Combines a set of currently-pressed buttons into the CDP <c>buttons</c> bitmask.
        /// </summary>
        /// <param name="buttons">Pressed buttons.</param>
        /// <returns>Bitwise OR of button flags.</returns>
        internal static int ToCdpMask(this IEnumerable<MouseButton> buttons)
        {
            int mask = 0;
            if (buttons != null)
            {
                foreach (MouseButton button in buttons)
                {
                    mask |= (int)button;
                }
            }

            return mask;
        }
    }
}
