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

namespace PlaywrightNative.Helpers
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
