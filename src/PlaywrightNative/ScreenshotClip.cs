/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightNative
{
    /// <summary>
    /// A clipping rectangle in page coordinates.
    /// </summary>
    internal sealed class ScreenshotClip
    {
        /// <summary>Gets the left offset in CSS pixels.</summary>
        public double X { get; init; }

        /// <summary>Gets the top offset in CSS pixels.</summary>
        public double Y { get; init; }

        /// <summary>Gets the width in CSS pixels.</summary>
        public double Width { get; init; }

        /// <summary>Gets the height in CSS pixels.</summary>
        public double Height { get; init; }

        /// <summary>
        /// Gets the capture scale. <c>1</c> is device pixels; <c>1 / dsf</c> is CSS pixels.
        /// </summary>
        public double Scale { get; init; } = 1;
    }
}
