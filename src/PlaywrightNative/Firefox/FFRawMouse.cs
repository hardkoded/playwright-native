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
