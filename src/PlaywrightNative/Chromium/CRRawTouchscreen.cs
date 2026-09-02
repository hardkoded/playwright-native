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
    /// Sends CDP <c>Input.dispatchTouchEvent</c> commands.
    /// </summary>
    internal class CRRawTouchscreen : IRawTouchscreen
    {
        private readonly CRSession _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRawTouchscreen"/> class.
        /// </summary>
        /// <param name="session">The CDP session to send commands on.</param>
        public CRRawTouchscreen(CRSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Dispatches a tap as concurrent <c>touchStart</c> + <c>touchEnd</c> events.
        /// </summary>
        public async Task TapAsync(double x, double y, IReadOnlyCollection<Input.KeyboardModifier> modifiers)
        {
            int modifiersMask = modifiers.ToCdpMask();

            // Upstream dispatches these concurrently via Promise.all. Do the same.
            Task start = _session.SendAsync("Input.dispatchTouchEvent", new
            {
                type = "touchStart",
                modifiers = modifiersMask,
                touchPoints = new[] { new { x, y } },
            });

            Task end = _session.SendAsync("Input.dispatchTouchEvent", new
            {
                type = "touchEnd",
                modifiers = modifiersMask,
                touchPoints = System.Array.Empty<object>(),
            });

            await Task.WhenAll(start, end).ConfigureAwait(false);
        }
    }
}
