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

namespace PlaywrightNative.Input
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
