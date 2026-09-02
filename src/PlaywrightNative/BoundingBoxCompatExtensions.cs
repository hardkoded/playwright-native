/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
    }
}
