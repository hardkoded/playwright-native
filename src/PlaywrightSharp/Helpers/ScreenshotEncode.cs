/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official WebKit <c>takeScreenshot</c> helpers: dimension cap, data-URL
    /// decode, and PNG→JPEG recode when the protocol still returns PNG.
    /// </summary>
    internal static class ScreenshotEncode
    {
        internal const int MaxDimension = 32767;

        /// <summary>
        /// Official <c>validateScreenshotDimension</c> for Cairo-based WebKit.
        /// </summary>
        /// <param name="side">CSS pixels on one axis.</param>
        /// <param name="deviceScale">Context device scale factor.</param>
        /// <param name="cssScale">Whether the capture uses <c>scale: css</c>.</param>
        internal static void EnsureDimension(int side, double deviceScale, bool cssScale)
        {
            int pixels = cssScale || deviceScale <= 0
                ? side
                : (int)Math.Ceiling(side * deviceScale);
            if (pixels > MaxDimension)
            {
                throw new PlaywrightSharpException(
                    "Cannot take screenshot larger than 32767 pixels on any dimension");
            }
        }

        /// <summary>
        /// Strips the <c>data:image/...;base64,</c> prefix from
        /// <c>Page.snapshotRect</c>.
        /// </summary>
        /// <param name="dataUrl">The protocol data URL.</param>
        /// <returns>Decoded image bytes.</returns>
        internal static byte[] FromDataUrl(string dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl))
            {
                return Array.Empty<byte>();
            }

            int comma = dataUrl.IndexOf(',');
            string base64 = comma >= 0 ? dataUrl.Substring(comma + 1) : dataUrl;
            return string.IsNullOrEmpty(base64) ? Array.Empty<byte>() : Convert.FromBase64String(base64);
        }

        /// <summary>
        /// Recodes a PNG buffer as JPEG when WebKit still returned PNG.
        /// </summary>
        /// <param name="bytes">Protocol image bytes.</param>
        /// <param name="type">Requested screenshot type.</param>
        /// <param name="quality">Resolved JPEG/WebP quality.</param>
        /// <returns>Bytes in the requested format.</returns>
        internal static byte[] RecodeIfNeeded(byte[] bytes, ScreenshotType type, int? quality)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return bytes ?? Array.Empty<byte>();
            }

            if (type == ScreenshotType.Jpeg && IsPng(bytes))
            {
                return PngToJpeg.Convert(bytes, quality ?? 80);
            }

            if (type == ScreenshotType.Webp && IsPng(bytes))
            {
                return PngToWebp.Convert(bytes, quality);
            }

            return bytes;
        }

        private static bool IsPng(byte[] bytes)
            => bytes.Length >= 8
                && bytes[0] == 0x89
                && bytes[1] == 0x50
                && bytes[2] == 0x4E
                && bytes[3] == 0x47;
    }
}
