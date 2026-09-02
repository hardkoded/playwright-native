/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>helper.getViewportSizeFromWindowFeatures</c>.
    /// </summary>
    internal static class WindowOpenFeatures
    {
        /// <summary>
        /// Reads <c>width</c> and <c>height</c> from a <c>window.open</c> feature list.
        /// </summary>
        /// <param name="features">CDP / Playwright window-feature strings.</param>
        /// <returns>The size when both values parse, otherwise <see langword="null"/>.</returns>
        internal static ViewportSize ParseSize(IEnumerable<string> features)
        {
            if (features == null)
            {
                return null;
            }

            int? width = null;
            int? height = null;
            foreach (string feature in features)
            {
                if (string.IsNullOrEmpty(feature))
                {
                    continue;
                }

                if (feature.StartsWith("width=", StringComparison.Ordinal)
                    && int.TryParse(feature.AsSpan(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWidth))
                {
                    width = parsedWidth;
                    continue;
                }

                if (feature.StartsWith("height=", StringComparison.Ordinal)
                    && int.TryParse(feature.AsSpan(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedHeight))
                {
                    height = parsedHeight;
                }
            }

            if (!width.HasValue || !height.HasValue)
            {
                return null;
            }

            return new ViewportSize { Width = width.Value, Height = height.Value };
        }
    }
}
