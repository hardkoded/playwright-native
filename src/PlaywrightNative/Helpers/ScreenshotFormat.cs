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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Maps <see cref="ScreenshotType"/> onto protocol format strings.
    /// </summary>
    internal static class ScreenshotFormat
    {
        /// <summary>
        /// CDP / Juggler format for <paramref name="type"/>. Defaults to PNG.
        /// </summary>
        /// <param name="type">Requested image type.</param>
        /// <returns><c>png</c>, <c>jpeg</c>, or <c>webp</c>.</returns>
        internal static string ToProtocol(ScreenshotType type)
        {
            if (type == ScreenshotType.Jpeg)
            {
                return "jpeg";
            }

            if (type == ScreenshotType.Webp)
            {
                return "webp";
            }

            return "png";
        }

        /// <summary>
        /// Whether <paramref name="format"/> accepts a quality parameter.
        /// </summary>
        /// <param name="format">Protocol format string.</param>
        /// <returns><see langword="true"/> for JPEG and WebP.</returns>
        internal static bool SupportsQuality(string format)
            => format == "jpeg" || format == "webp";

        /// <summary>
        /// Throws when <paramref name="type"/> cannot be encoded on
        /// <paramref name="browser"/>. Official WebKit Linux encodes WebP
        /// natively via <c>Page.snapshotRect</c>; Chromium uses CDP.
        /// </summary>
        /// <param name="type">Requested image type.</param>
        /// <param name="browser">Browser name for the error.</param>
        internal static void EnsureSupported(ScreenshotType type, string browser)
        {
            if (type == ScreenshotType.Webp && browser == "Firefox")
            {
                throw new PlaywrightNativeException(
                    "WebP screenshots are not supported on " + browser + ".");
            }
        }
    }
}
