/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Public-facing mouse simulator. Tracks position and pressed buttons, composes
    /// Click / DoubleClick on top of Move + Down + Up.
    /// </summary>
    internal class Mouse
    {
        private readonly IRawMouse _raw;
        private readonly Keyboard _keyboard;
        private readonly HashSet<MouseButton> _pressedButtons = new();

        private double _x;
        private double _y;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mouse"/> class.
        /// </summary>
        /// <param name="raw">The low-level mouse transport.</param>
        /// <param name="keyboard">The shared keyboard (for modifier state).</param>
        public Mouse(IRawMouse raw, Keyboard keyboard)
        {
            _raw = raw ?? throw new ArgumentNullException(nameof(raw));
            _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        }

        /// <summary>
        /// Moves the cursor to (x, y), optionally interpolating in <paramref name="steps"/> segments.
        /// </summary>
        /// <param name="x">Target x coordinate.</param>
        /// <param name="y">Target y coordinate.</param>
        /// <param name="steps">Number of interpolation segments (minimum 1).</param>
        internal async Task MoveAsync(double x, double y, int steps = 1)
        {
            if (steps < 1)
            {
                steps = 1;
            }

            double fromX = _x;
            double fromY = _y;

            for (int i = 1; i <= steps; i++)
            {
                double ratio = (double)i / steps;
                double nextX = Math.Floor(fromX + ((x - fromX) * ratio));
                double nextY = Math.Floor(fromY + ((y - fromY) * ratio));

                await _raw.MoveAsync(
                    nextX,
                    nextY,
                    CurrentButton(),
                    _pressedButtons,
                    _keyboard.PressedModifiers).ConfigureAwait(false);
            }

            _x = x;
            _y = y;
        }

        /// <summary>
        /// Presses a mouse button at the current position.
        /// </summary>
        /// <param name="button">The button to press.</param>
        /// <param name="clickCount">CDP click count (1 for single, 2 for double, etc.).</param>
        internal async Task DownAsync(MouseButton button = MouseButton.Left, int clickCount = 1)
        {
            _pressedButtons.Add(button);
            await _raw.DownAsync(
                Math.Floor(_x),
                Math.Floor(_y),
                button,
                _pressedButtons,
                _keyboard.PressedModifiers,
                clickCount).ConfigureAwait(false);
        }

        /// <summary>
        /// Releases a previously-pressed mouse button at the current position.
        /// </summary>
        /// <param name="button">The button to release.</param>
        /// <param name="clickCount">CDP click count.</param>
        internal async Task UpAsync(MouseButton button = MouseButton.Left, int clickCount = 1)
        {
            _pressedButtons.Remove(button);
            await _raw.UpAsync(
                Math.Floor(_x),
                Math.Floor(_y),
                button,
                _pressedButtons,
                _keyboard.PressedModifiers,
                clickCount).ConfigureAwait(false);
        }

        /// <summary>
        /// Moves the cursor to (x, y) and performs <paramref name="clickCount"/> consecutive
        /// down+up sequences.
        /// </summary>
        /// <param name="x">Target x coordinate.</param>
        /// <param name="y">Target y coordinate.</param>
        /// <param name="button">The button to click.</param>
        /// <param name="clickCount">Number of down+up sequences.</param>
        /// <param name="delayMs">Delay between down and up, in milliseconds.</param>
        /// <param name="steps">Intermediate <c>mousemove</c> segments. Defaults to 1.</param>
        internal async Task ClickAsync(
            double x,
            double y,
            MouseButton button = MouseButton.Left,
            int clickCount = 1,
            int delayMs = 0,
            int steps = 1)
        {
            await MoveAsync(x, y, steps).ConfigureAwait(false);

            for (int i = 1; i <= clickCount; i++)
            {
                await DownAsync(button, i).ConfigureAwait(false);

                if (delayMs > 0)
                {
                    await Task.Delay(delayMs).ConfigureAwait(false);
                }

                await UpAsync(button, i).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Convenience: <see cref="ClickAsync"/> with <c>clickCount = 2</c>.
        /// </summary>
        /// <param name="x">Target x coordinate.</param>
        /// <param name="y">Target y coordinate.</param>
        /// <param name="button">The button to click.</param>
        /// <param name="delayMs">Delay between down and up, in milliseconds.</param>
        /// <param name="steps">Intermediate <c>mousemove</c> segments. Defaults to 1.</param>
        internal Task DoubleClickAsync(double x, double y, MouseButton button = MouseButton.Left, int delayMs = 0, int steps = 1)
            => ClickAsync(x, y, button, clickCount: 2, delayMs: delayMs, steps: steps);

        /// <summary>
        /// Scrolls at the current position.
        /// </summary>
        /// <param name="deltaX">Horizontal scroll amount.</param>
        /// <param name="deltaY">Vertical scroll amount.</param>
        internal Task WheelAsync(double deltaX, double deltaY)
        {
            return _raw.WheelAsync(
                Math.Floor(_x),
                Math.Floor(_y),
                _pressedButtons,
                _keyboard.PressedModifiers,
                deltaX,
                deltaY);
        }

        /// <summary>
        /// Performs a mouse-driven drag from <c>(fromX, fromY)</c> to <c>(toX, toY)</c> using
        /// the left button. Composes: <c>MoveAsync(from) → DownAsync() → MoveAsync(to, steps)
        /// → UpAsync()</c>. Uses <paramref name="steps"/> intermediate moves so HTML5 drag
        /// libraries see enough mousemove events to register the gesture.
        /// </summary>
        /// <param name="fromX">Starting X coordinate.</param>
        /// <param name="fromY">Starting Y coordinate.</param>
        /// <param name="toX">Ending X coordinate.</param>
        /// <param name="toY">Ending Y coordinate.</param>
        /// <param name="steps">Number of intermediate moves during the drag. Default 10.</param>
        internal async Task DragToAsync(double fromX, double fromY, double toX, double toY, int steps = 10)
        {
            await MoveAsync(fromX, fromY).ConfigureAwait(false);
            await DownAsync().ConfigureAwait(false);
            await MoveAsync(toX, toY, steps).ConfigureAwait(false);
            await UpAsync().ConfigureAwait(false);
        }

        private MouseButton CurrentButton()
        {
            // For move events, CDP expects a single "button" field. Prefer left if pressed;
            // otherwise the first pressed button; otherwise none.
            foreach (MouseButton b in _pressedButtons)
            {
                return b;
            }

            return MouseButton.None;
        }
    }
}
