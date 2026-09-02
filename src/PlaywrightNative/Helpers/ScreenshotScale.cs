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
#pragma warning disable SA1649
using System;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Resolves Playwright screenshot <c>scale</c> (<c>css</c> vs <c>device</c>)
    /// into a CDP clip scale or WebKit <c>omitDeviceScaleFactor</c>.
    /// </summary>
    internal static class ScreenshotScaleHelper
    {
        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="scale"/> is <c>css</c>.
        /// </summary>
        /// <param name="scale">The screenshot scale option.</param>
        /// <returns>Whether CSS-pixel capture was requested.</returns>
        internal static bool IsCss(string scale)
            => string.Equals(scale, "css", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Clip scale for <c>Page.captureScreenshot</c>. CSS scale is
        /// <c>1 / deviceScaleFactor</c>; device scale is <c>1</c>.
        /// </summary>
        /// <param name="scale">The screenshot scale option.</param>
        /// <param name="deviceScaleFactor">The context device scale factor.</param>
        /// <returns>The clip scale.</returns>
        internal static double ClipScale(string scale, double deviceScaleFactor)
            => IsCss(scale) ? 1.0 / Math.Max(deviceScaleFactor, 0.01) : 1.0;

        /// <summary>
        /// Builds a viewport clip when CSS scale is requested and no explicit clip
        /// was provided. Returns <see langword="null"/> for device scale.
        /// </summary>
        /// <param name="viewport">The current viewport, or <see langword="null"/>.</param>
        /// <param name="clipScale">The resolved clip scale.</param>
        /// <param name="scale">The screenshot scale option.</param>
        /// <returns>The clip, or <see langword="null"/>.</returns>
        internal static ScreenshotClip ViewportClip(PageViewportSizeResult viewport, double clipScale, string scale)
        {
            if (!IsCss(scale) || viewport == null)
            {
                return null;
            }

            return new ScreenshotClip
            {
                X = 0,
                Y = 0,
                Width = viewport.Width,
                Height = viewport.Height,
                Scale = clipScale,
            };
        }
    }
}
