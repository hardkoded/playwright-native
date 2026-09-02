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
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Public-facing touchscreen simulator. Single-finger <see cref="TapAsync"/> only;
    /// multi-touch gestures land in later phases.
    /// </summary>
    internal class Touchscreen
    {
        private readonly IRawTouchscreen _raw;
        private readonly Keyboard _keyboard;

        /// <summary>
        /// Initializes a new instance of the <see cref="Touchscreen"/> class.
        /// </summary>
        /// <param name="raw">The low-level touch transport.</param>
        /// <param name="keyboard">The shared keyboard (for modifier state).</param>
        public Touchscreen(IRawTouchscreen raw, Keyboard keyboard)
        {
            _raw = raw ?? throw new ArgumentNullException(nameof(raw));
            _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        }

        /// <summary>
        /// Dispatches a tap (touchStart + touchEnd) at the given coordinates.
        /// </summary>
        /// <param name="x">Tap x coordinate.</param>
        /// <param name="y">Tap y coordinate.</param>
        internal Task TapAsync(double x, double y)
        {
            return _raw.TapAsync(x, y, _keyboard.PressedModifiers);
        }
    }
}
