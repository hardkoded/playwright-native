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
    /// Official recordVideo default size: fit the viewport into 800x800,
    /// even dimensions. Null viewport is 800x600.
    /// </summary>
    internal static class VideoSize
    {
        /// <summary>
        /// Resolves <paramref name="requested"/> or the official default from
        /// <paramref name="viewport"/>.
        /// </summary>
        /// <param name="requested">Explicit <c>recordVideo.size</c>.</param>
        /// <param name="viewport">The page viewport, or <see langword="null"/>.</param>
        /// <returns>Even-width/height video size.</returns>
        internal static RecordVideoSize Resolve(RecordVideoSize requested, ViewportSize viewport)
        {
            if (requested != null && requested.Width > 0 && requested.Height > 0)
            {
                return new RecordVideoSize
                {
                    Width = requested.Width & ~1,
                    Height = requested.Height & ~1,
                };
            }

            ViewportSize resolved = ViewportSizeHelper.Resolve(viewport);
            if (resolved == null || resolved.Width <= 0 || resolved.Height <= 0)
            {
                return new RecordVideoSize { Width = 800, Height = 600 };
            }

            int width = resolved.Width;
            int height = resolved.Height;
            double scale = Math.Min(1.0, 800.0 / Math.Max(width, height));
            return new RecordVideoSize
            {
                Width = (int)Math.Floor(width * scale / 2) * 2,
                Height = (int)Math.Floor(height * scale / 2) * 2,
            };
        }
    }
}
