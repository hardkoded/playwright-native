/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
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
