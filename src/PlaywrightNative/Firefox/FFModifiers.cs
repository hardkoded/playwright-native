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
    /// Provides modifier key bitmask helpers for the Firefox Juggler protocol.
    /// Modifier values: Alt=1, Control=2, Shift=4, Meta=8.
    /// </summary>
    internal static class FFModifiers
    {
        /// <summary>Alt modifier bit.</summary>
        internal const int Alt = 1;

        /// <summary>Control modifier bit.</summary>
        internal const int Control = 2;

        /// <summary>Shift modifier bit.</summary>
        internal const int Shift = 4;

        /// <summary>Meta (Command/Windows) modifier bit.</summary>
        internal const int Meta = 8;

        /// <summary>
        /// Converts a <see cref="MouseButton"/> name to its Juggler button number.
        /// </summary>
        /// <param name="button">The button name.</param>
        /// <returns>0=left, 1=middle, 2=right.</returns>
        internal static int ToButtonNumber(MouseButton button) => button switch
        {
            MouseButton.Left => 0,
            MouseButton.Middle => 1,
            MouseButton.Right => 2,
            _ => 0,
        };

        /// <summary>
        /// Converts a set of pressed mouse buttons to a Juggler bitmask.
        /// left=1, right=2, middle=4.
        /// </summary>
        /// <param name="buttons">The set of pressed buttons.</param>
        /// <returns>The combined bitmask.</returns>
        internal static int ToButtonsMask(System.Collections.Generic.IEnumerable<MouseButton> buttons)
        {
            int mask = 0;
            foreach (MouseButton b in buttons)
            {
                mask |= b switch
                {
                    MouseButton.Left => 1,
                    MouseButton.Right => 2,
                    MouseButton.Middle => 4,
                    _ => 0,
                };
            }

            return mask;
        }
    }
}
