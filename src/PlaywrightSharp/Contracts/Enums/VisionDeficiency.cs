/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp
{
    /// <summary>
    /// Official <c>page.emulateVisionDeficiency</c> type.
    /// </summary>
    public enum VisionDeficiency
    {
        /// <summary>
        /// Default. Same as <see cref="None"/>.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// Disable vision-deficiency emulation.
        /// </summary>
        None,

        /// <summary>
        /// Achromatopsia (no color).
        /// </summary>
        Achromatopsia,

        /// <summary>
        /// Blurred vision.
        /// </summary>
        BlurredVision,

        /// <summary>
        /// Deuteranopia (green-weak).
        /// </summary>
        Deuteranopia,

        /// <summary>
        /// Protanopia (red-weak).
        /// </summary>
        Protanopia,

        /// <summary>
        /// Tritanopia (blue-weak).
        /// </summary>
        Tritanopia,
    }
}
