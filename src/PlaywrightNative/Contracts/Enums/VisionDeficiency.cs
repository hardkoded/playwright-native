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
