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
using PlaywrightNative.Input;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Sends CDP <c>Input.dispatchMouseEvent</c> commands.
    /// </summary>
    internal class CRRawMouse : IRawMouse
    {
        private readonly CRSession _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRawMouse"/> class.
        /// </summary>
        /// <param name="session">The CDP session to send commands on.</param>
        public CRRawMouse(CRSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Dispatches <c>mouseMoved</c>.
        /// </summary>
        public Task MoveAsync(double x, double y, Input.MouseButton button, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers)
        {
            int buttonsMask = buttons.ToCdpMask();

            return _session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mouseMoved",
                button = button.ToCdpName(),
                buttons = buttonsMask,
                x,
                y,
                modifiers = modifiers.ToCdpMask(),
                force = buttonsMask > 0 ? 0.5 : 0.0,
            });
        }

        /// <summary>
        /// Dispatches <c>mousePressed</c>.
        /// </summary>
        public Task DownAsync(double x, double y, Input.MouseButton button, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers, int clickCount)
        {
            int buttonsMask = buttons.ToCdpMask();

            return _session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mousePressed",
                button = button.ToCdpName(),
                buttons = buttonsMask,
                x,
                y,
                modifiers = modifiers.ToCdpMask(),
                clickCount,
                force = buttonsMask > 0 ? 0.5 : 0.0,
            });
        }

        /// <summary>
        /// Dispatches <c>mouseReleased</c>.
        /// </summary>
        public Task UpAsync(double x, double y, Input.MouseButton button, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers, int clickCount)
        {
            return _session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mouseReleased",
                button = button.ToCdpName(),
                buttons = buttons.ToCdpMask(),
                x,
                y,
                modifiers = modifiers.ToCdpMask(),
                clickCount,
            });
        }

        /// <summary>
        /// Dispatches <c>mouseWheel</c>. Enables focus emulation first so Chromium's
        /// compositor still ACKs the wheel after a popup steals window focus
        /// (upstream <c>crPage</c> does this at init).
        /// </summary>
        public async Task WheelAsync(double x, double y, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers, double deltaX, double deltaY)
        {
            await _session.SendAsync("Emulation.setFocusEmulationEnabled", new { enabled = true }).ConfigureAwait(false);
            await _session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "mouseWheel",
                x,
                y,
                modifiers = modifiers.ToCdpMask(),
                deltaX,
                deltaY,
            }).ConfigureAwait(false);
        }
    }
}
