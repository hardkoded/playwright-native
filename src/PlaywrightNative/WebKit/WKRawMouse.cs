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
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightNative.Input;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// WebKit raw mouse. Sends WIP <c>Input.dispatchMouseEvent</c> / <c>Input.dispatchWheelEvent</c>
    /// on the page-proxy session. Mirrors upstream <c>wkInput.ts</c> <c>RawMouseImpl</c>.
    /// </summary>
    internal sealed class WKRawMouse : IRawMouse
    {
        private readonly WKPage _page;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKRawMouse"/> class.
        /// </summary>
        /// <param name="page">The owning WebKit page.</param>
        public WKRawMouse(WKPage page)
        {
            _page = page;
        }

        /// <summary>
        /// Returns the WebKit <c>button</c> string for a single mouse button.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <returns>"left", "right", "middle", or "none".</returns>
        public static string ToWebKitButton(Input.MouseButton button)
            => button switch
            {
                Input.MouseButton.Left => "left",
                Input.MouseButton.Right => "right",
                Input.MouseButton.Middle => "middle",
                _ => "none",
            };

        /// <summary>
        /// Combines a set of currently-pressed buttons into the WebKit <c>buttons</c> bitmask
        /// (left=1, right=2, middle=4).
        /// </summary>
        /// <param name="buttons">The pressed buttons.</param>
        /// <returns>The WebKit buttons bitmask.</returns>
        public static int ToWebKitButtonsMask(IReadOnlyCollection<Input.MouseButton> buttons)
        {
            int mask = 0;
            if (buttons != null)
            {
                foreach (Input.MouseButton button in buttons)
                {
                    mask |= button switch
                    {
                        Input.MouseButton.Left => 1,
                        Input.MouseButton.Right => 2,
                        Input.MouseButton.Middle => 4,
                        _ => 0,
                    };
                }
            }

            return mask;
        }

        /// <inheritdoc/>
        public Task MoveAsync(double x, double y, Input.MouseButton button, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers)
        {
            return _page.Session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "move",
                button = ToWebKitButton(button),
                buttons = ToWebKitButtonsMask(buttons),
                x,
                y,
                modifiers = WKRawKeyboard.ToWebKitModifiersMask(modifiers),
            });
        }

        /// <inheritdoc/>
        public Task DownAsync(double x, double y, Input.MouseButton button, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers, int clickCount)
        {
            return _page.Session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "down",
                button = ToWebKitButton(button),
                buttons = ToWebKitButtonsMask(buttons),
                x,
                y,
                modifiers = WKRawKeyboard.ToWebKitModifiersMask(modifiers),
                clickCount,
            });
        }

        /// <inheritdoc/>
        public Task UpAsync(double x, double y, Input.MouseButton button, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers, int clickCount)
        {
            return _page.Session.SendAsync("Input.dispatchMouseEvent", new
            {
                type = "up",
                button = ToWebKitButton(button),
                buttons = ToWebKitButtonsMask(buttons),
                x,
                y,
                modifiers = WKRawKeyboard.ToWebKitModifiersMask(modifiers),
                clickCount,
            });
        }

        /// <inheritdoc/>
        public async Task WheelAsync(double x, double y, IReadOnlyCollection<Input.MouseButton> buttons, IReadOnlyCollection<Input.KeyboardModifier> modifiers, double deltaX, double deltaY)
        {
            if (_page.EmulatesMobile)
            {
                throw new PlaywrightNativeException("Mouse wheel is not supported in mobile WebKit");
            }

            // Matches upstream wkInput.ts RawMouseImpl.wheel: sync compositor state,
            // wait one animation frame, then dispatch on the page-proxy session.
            WKTargetSession target = _page.CurrentTargetSession;
            if (target != null)
            {
                await target.SendAsync("Page.updateScrollingState").ConfigureAwait(false);
            }

            await _page.EvaluateAsync("new Promise(requestAnimationFrame)").ConfigureAwait(false);
            await _page.Session.SendAsync("Input.dispatchWheelEvent", new
            {
                x,
                y,
                deltaX,
                deltaY,
                modifiers = WKRawKeyboard.ToWebKitModifiersMask(modifiers),
            }).ConfigureAwait(false);
        }
    }
}
