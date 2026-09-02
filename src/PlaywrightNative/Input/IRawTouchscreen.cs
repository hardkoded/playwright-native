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
