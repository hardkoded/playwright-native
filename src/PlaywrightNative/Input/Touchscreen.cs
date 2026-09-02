/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
