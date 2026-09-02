/*
 * Copyright (c) Microsoft Corporation.
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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Chromium <c>screenOrientation</c> for
    /// <c>Emulation.setDeviceMetricsOverride</c>.
    /// </summary>
    internal static class EmulatedViewport
    {
        /// <summary>
        /// Desktop stays <c>portraitPrimary</c>. Mobile follows screen aspect.
        /// </summary>
        /// <param name="isMobile">Context <c>isMobile</c>.</param>
        /// <param name="screenWidth">Reported screen width.</param>
        /// <param name="screenHeight">Reported screen height.</param>
        /// <returns>CDP <c>screenOrientation</c>.</returns>
        internal static Dictionary<string, object> ScreenOrientation(bool isMobile, int screenWidth, int screenHeight)
        {
            if (!isMobile)
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "landscapePrimary",
                    ["angle"] = 0,
                };
            }

            if (screenWidth > screenHeight)
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "landscapePrimary",
                    ["angle"] = 90,
                };
            }

            return new Dictionary<string, object>
            {
                ["type"] = "portraitPrimary",
                ["angle"] = 0,
            };
        }
    }
}
