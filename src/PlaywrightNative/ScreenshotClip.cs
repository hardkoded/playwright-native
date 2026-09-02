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
