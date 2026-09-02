/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
