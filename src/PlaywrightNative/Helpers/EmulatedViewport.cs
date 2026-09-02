/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
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
