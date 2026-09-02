/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
