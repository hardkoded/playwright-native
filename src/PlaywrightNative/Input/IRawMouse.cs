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
using System.Threading.Tasks;

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Low-level mouse transport. Mirrors upstream <c>input.RawMouse</c>: a per-browser
    /// implementation that translates the simulator's requests into protocol commands. The
    /// shared <see cref="Mouse"/> simulator owns position and pressed-button state and drives
    /// this interface.
    /// </summary>
    internal interface IRawMouse
    {
        /// <summary>
        /// Dispatches a mouse-move event.
        /// </summary>
        /// <param name="x">Target x coordinate.</param>
        /// <param name="y">Target y coordinate.</param>
        /// <param name="button">The button considered active for the move (or <see cref="MouseButton.None"/>).</param>
        /// <param name="buttons">The set of currently-pressed buttons.</param>
        /// <param name="modifiers">The set of currently-held modifiers.</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task MoveAsync(double x, double y, MouseButton button, IReadOnlyCollection<MouseButton> buttons, IReadOnlyCollection<KeyboardModifier> modifiers);

        /// <summary>
        /// Dispatches a mouse-button-press event.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="button">The button being pressed.</param>
        /// <param name="buttons">The set of currently-pressed buttons (including this one).</param>
        /// <param name="modifiers">The set of currently-held modifiers.</param>
        /// <param name="clickCount">The click count (1 for single, 2 for double, etc.).</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task DownAsync(double x, double y, MouseButton button, IReadOnlyCollection<MouseButton> buttons, IReadOnlyCollection<KeyboardModifier> modifiers, int clickCount);

        /// <summary>
        /// Dispatches a mouse-button-release event.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="button">The button being released.</param>
        /// <param name="buttons">The set of currently-pressed buttons (after release).</param>
        /// <param name="modifiers">The set of currently-held modifiers.</param>
        /// <param name="clickCount">The click count.</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task UpAsync(double x, double y, MouseButton button, IReadOnlyCollection<MouseButton> buttons, IReadOnlyCollection<KeyboardModifier> modifiers, int clickCount);

        /// <summary>
        /// Dispatches a mouse-wheel event.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="buttons">The set of currently-pressed buttons.</param>
        /// <param name="modifiers">The set of currently-held modifiers.</param>
        /// <param name="deltaX">Horizontal scroll amount.</param>
        /// <param name="deltaY">Vertical scroll amount.</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        Task WheelAsync(double x, double y, IReadOnlyCollection<MouseButton> buttons, IReadOnlyCollection<KeyboardModifier> modifiers, double deltaX, double deltaY);
    }
}
