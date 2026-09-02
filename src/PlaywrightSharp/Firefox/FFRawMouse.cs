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

namespace PlaywrightSharp.Firefox
{
    /// <summary>
    /// Implements mouse input for Firefox using the Juggler <c>Page.dispatchMouseEvent</c>
    /// and <c>Page.dispatchWheelEvent</c> protocol commands.
    /// </summary>
    internal class FFRawMouse
    {
        private readonly FFSession _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFRawMouse"/> class.
        /// </summary>
        /// <param name="client">The Juggler session.</param>
        public FFRawMouse(FFSession client) => _client = client;

        /// <summary>
        /// Moves the mouse pointer to the given coordinates.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="buttons">Bitmask of currently pressed buttons.</param>
        /// <param name="modifiers">Active modifier bitmask.</param>
        internal Task MoveAsync(double x, double y, int buttons, int modifiers)
            => _client.SendAsync("Page.dispatchMouseEvent", new
            {
                type = "mousemove",
                x,
                y,
                button = 0,
                buttons,
                modifiers,
                clickCount = 0,
            });

        /// <summary>
        /// Sends a mousedown event.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="button">The button number (0=left, 1=middle, 2=right).</param>
        /// <param name="buttons">Bitmask of currently pressed buttons.</param>
        /// <param name="modifiers">Active modifier bitmask.</param>
        /// <param name="clickCount">Click count for double/triple click.</param>
        internal Task DownAsync(double x, double y, int button, int buttons, int modifiers, int clickCount)
            => _client.SendAsync("Page.dispatchMouseEvent", new
            {
                type = "mousedown",
                x,
                y,
                button,
                buttons,
                modifiers,
                clickCount,
            });

        /// <summary>
        /// Sends a mouseup event.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="button">The button number.</param>
        /// <param name="buttons">Bitmask of currently pressed buttons.</param>
        /// <param name="modifiers">Active modifier bitmask.</param>
        /// <param name="clickCount">Click count.</param>
        internal Task UpAsync(double x, double y, int button, int buttons, int modifiers, int clickCount)
            => _client.SendAsync("Page.dispatchMouseEvent", new
            {
                type = "mouseup",
                x,
                y,
                button,
                buttons,
                modifiers,
                clickCount,
            });

        /// <summary>
        /// Sends a wheel (scroll) event.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="deltaX">Horizontal scroll delta.</param>
        /// <param name="deltaY">Vertical scroll delta.</param>
        /// <param name="modifiers">Active modifier bitmask.</param>
        internal Task WheelAsync(double x, double y, double deltaX, double deltaY, int modifiers)
            => _client.SendAsync("Page.dispatchWheelEvent", new { x, y, deltaX, deltaY, modifiers });
    }
}
