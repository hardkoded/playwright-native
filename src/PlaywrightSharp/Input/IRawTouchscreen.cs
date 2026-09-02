/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Low-level touchscreen transport. Mirrors upstream <c>input.RawTouchscreen</c>: a
    /// per-browser implementation that translates the simulator's tap request into protocol
    /// commands. The shared <see cref="Touchscreen"/> simulator drives this interface.
    /// </summary>
    internal interface IRawTouchscreen
    {
        /// <summary>
        /// Dispatches a tap at the given coordinates.
        /// </summary>
        /// <param name="x">Tap x coordinate.</param>
        /// <param name="y">Tap y coordinate.</param>
        /// <param name="modifiers">The set of currently-held modifiers.</param>
        /// <returns>A task that completes when the tap has been dispatched.</returns>
        Task TapAsync(double x, double y, IReadOnlyCollection<KeyboardModifier> modifiers);
    }
}
