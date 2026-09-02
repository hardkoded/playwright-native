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
    /// Implements touchscreen input for Firefox using the Juggler <c>Page.dispatchTapEvent</c>
    /// protocol command.
    /// </summary>
    internal class FFRawTouchscreen
    {
        private readonly FFSession _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFRawTouchscreen"/> class.
        /// </summary>
        /// <param name="client">The Juggler session.</param>
        public FFRawTouchscreen(FFSession client) => _client = client;

        /// <summary>
        /// Sends a tap event at the given coordinates.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="modifiers">Active modifier bitmask.</param>
        internal Task TapAsync(double x, double y, int modifiers)
            => _client.SendAsync("Page.dispatchTapEvent", new { x, y, modifiers });
    }
}
