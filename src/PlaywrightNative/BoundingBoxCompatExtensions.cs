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
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Bounding-box conversion helpers.
    /// </summary>
    public static class BoundingBoxCompatExtensions
    {
        /// <summary>Converts a locator bounding box to an element-handle bounding box.</summary>
        public static ElementHandleBoundingBoxResult AsElementHandleBoundingBox(this LocatorBoundingBoxResult box)
        {
            if (box == null)
            {
                return null;
            }

            return new ElementHandleBoundingBoxResult
            {
                X = box.X,
                Y = box.Y,
                Width = box.Width,
                Height = box.Height,
            };
        }

        /// <summary>Converts an element-handle bounding box to a locator bounding box.</summary>
        public static LocatorBoundingBoxResult AsLocatorBoundingBox(this ElementHandleBoundingBoxResult box)
        {
            if (box == null)
            {
                return null;
            }

            return new LocatorBoundingBoxResult
            {
                X = box.X,
                Y = box.Y,
                Width = box.Width,
                Height = box.Height,
            };
        }
    }
}
