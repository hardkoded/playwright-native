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
using System;
using System.Collections.Generic;
using System.IO;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>validateScreenshotOptions</c> / <c>determineScreenshotType</c>
    /// / <c>trimClipToSize</c> checks for <see cref="IPage.ScreenshotAsync"/>.
    /// </summary>
    internal static class ScreenshotValidate
    {
        private static readonly Dictionary<string, string> MimeByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".txt"] = "text/plain",
        };

        /// <summary>
        /// Resolves <paramref name="type"/> from <paramref name="path"/> when the
        /// caller omitted it. Official <c>determineScreenshotType</c>.
        /// </summary>
        /// <param name="path">Optional output path.</param>
        /// <param name="type">Caller type, or <see cref="EnumCompat.UndefinedScreenshotType"/>.</param>
        /// <returns>The format to encode.</returns>
        internal static ScreenshotType ResolveType(string path, ScreenshotType type)
        {
            if (type != EnumCompat.UndefinedScreenshotType)
            {
                return type;
            }

            if (string.IsNullOrEmpty(path))
            {
                return ScreenshotType.Png;
            }

            string mime = MimeForPath(path);
            if (mime == "image/png")
            {
                return ScreenshotType.Png;
            }

            if (mime == "image/jpeg")
            {
                return ScreenshotType.Jpeg;
            }

            if (mime == "image/webp")
            {
                return ScreenshotType.Webp;
            }

            throw new PlaywrightNativeException("path: unsupported mime type \"" + (mime ?? "null") + "\"");
        }

        /// <summary>
        /// Official <c>options.quality is unsupported for the png</c> and
        /// <c>Expected options.quality to be between 0 and 100</c>.
        /// </summary>
        /// <param name="type">Resolved image type.</param>
        /// <param name="quality">Optional quality.</param>
        internal static void EnsureQuality(ScreenshotType type, int? quality)
        {
            if (!quality.HasValue)
            {
                return;
            }

            string format = ScreenshotFormat.ToProtocol(type);
            if (!ScreenshotFormat.SupportsQuality(format))
            {
                throw new PlaywrightNativeException("options.quality is unsupported for the " + format + " screenshots");
            }

            if (quality.Value < 0 || quality.Value > 100)
            {
                throw new PlaywrightNativeException(
                    "Expected options.quality to be between 0 and 100 (inclusive), got " + quality.Value);
            }
        }

        /// <summary>
        /// Official screenshotter quality: JPEG defaults to 80, WebP to 100
        /// (lossless).
        /// </summary>
        /// <param name="type">Resolved image type.</param>
        /// <param name="quality">Caller quality, if any.</param>
        /// <returns>The quality to send to the encoder, or <see langword="null"/> for PNG.</returns>
        internal static int? ResolvedQuality(ScreenshotType type, int? quality)
        {
            if (quality.HasValue)
            {
                return quality;
            }

            if (type == ScreenshotType.Jpeg)
            {
                return 80;
            }

            if (type == ScreenshotType.Webp)
            {
                return 100;
            }

            return null;
        }

        /// <summary>
        /// Official clip size and outside-viewport checks.
        /// </summary>
        /// <param name="clip">Optional clip.</param>
        /// <param name="fullPage">Whether this is a full-page capture.</param>
        /// <param name="viewport">Current viewport, or <see langword="null"/>.</param>
        internal static void EnsureClip(Clip clip, bool fullPage, PageViewportSizeResult viewport)
        {
            if (clip == null)
            {
                return;
            }

            if (clip.Width <= 0)
            {
                throw new PlaywrightNativeException("Expected options.clip.width to be greater than 0");
            }

            if (clip.Height <= 0)
            {
                throw new PlaywrightNativeException("Expected options.clip.height to be greater than 0");
            }

            if (fullPage || viewport == null || viewport.Width <= 0 || viewport.Height <= 0)
            {
                return;
            }

            // Element screenshots may clip a box larger than the viewport
            // (official captureBeyondViewport). Only reject a user clip that
            // sits entirely outside a same-size-or-smaller viewport.
            if (clip.Width > viewport.Width || clip.Height > viewport.Height)
            {
                return;
            }

            float x1 = Math.Max(0, Math.Min(clip.X, viewport.Width));
            float y1 = Math.Max(0, Math.Min(clip.Y, viewport.Height));
            float x2 = Math.Max(0, Math.Min(clip.X + clip.Width, viewport.Width));
            float y2 = Math.Max(0, Math.Min(clip.Y + clip.Height, viewport.Height));
            if (x2 - x1 <= 0 || y2 - y1 <= 0)
            {
                throw new PlaywrightNativeException("Clipped area is either empty or outside the resulting image");
            }
        }

        /// <summary>
        /// Official <c>helper.enclosingIntRect</c> used for element clips.
        /// </summary>
        /// <param name="x">Left in CSS pixels.</param>
        /// <param name="y">Top in CSS pixels.</param>
        /// <param name="width">Width in CSS pixels.</param>
        /// <param name="height">Height in CSS pixels.</param>
        /// <returns>The integer clip.</returns>
        internal static Clip EnclosingIntRect(double x, double y, double width, double height)
        {
            int left = (int)Math.Floor(x + 1e-3);
            int top = (int)Math.Floor(y + 1e-3);
            int right = (int)Math.Ceiling(x + width - 1e-3);
            int bottom = (int)Math.Ceiling(y + height - 1e-3);
            return new Clip
            {
                X = left,
                Y = top,
                Width = right - left,
                Height = bottom - top,
            };
        }

        private static string MimeForPath(string path)
        {
            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
            {
                return null;
            }

            return MimeByExtension.TryGetValue(extension, out string mime) ? mime : null;
        }
    }
}
