/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Axis-aligned bounding box in CSS pixels, as reported by CDP <c>DOM.getBoxModel</c>.
    /// Origin is the top-left of the viewport.
    /// </summary>
    /// <param name="X">Left edge of the box.</param>
    /// <param name="Y">Top edge of the box.</param>
    /// <param name="Width">Box width.</param>
    /// <param name="Height">Box height.</param>
    internal readonly record struct BoundingBox(double X, double Y, double Width, double Height);
}
