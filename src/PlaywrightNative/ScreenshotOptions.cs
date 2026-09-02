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

namespace PlaywrightNative
{
    /// <summary>
    /// Options for <c>CRPage.ScreenshotAsync</c>.
    /// </summary>
    internal sealed class ScreenshotOptions
    {
        /// <summary>Gets the image format. "png" (default, lossless) or "jpeg".</summary>
        public string Format { get; init; } = "png";

        /// <summary>Gets the JPEG quality (0-100). Only used for JPEG format.</summary>
        public int? Quality { get; init; }

        /// <summary>Gets a value indicating whether to capture the full scrollable page instead of just the viewport.</summary>
        public bool FullPage { get; init; }

        /// <summary>
        /// Gets a value indicating whether to capture past the viewport. Official
        /// element screenshots set this when the box does not fit.
        /// </summary>
        public bool CaptureBeyondViewport { get; init; }

        /// <summary>Gets a value indicating whether to hide the default white background (PNG only).</summary>
        public bool OmitBackground { get; init; }

        /// <summary>Gets the optional clip rectangle within the page.</summary>
        public ScreenshotClip Clip { get; init; }

        /// <summary>
        /// Gets the screenshot scale: <c>css</c> for CSS pixels, <c>device</c> for device pixels.
        /// </summary>
        public string Scale { get; init; }

        /// <summary>Gets the context device scale factor used when <see cref="Scale"/> is <c>css</c>.</summary>
        public double DeviceScaleFactor { get; init; } = 1;
    }
}
