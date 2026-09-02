/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

namespace PlaywrightSharp.Input
{
    /// <summary>
    /// Simple width/height pair in CSS pixels.
    /// </summary>
    /// <param name="Width">The viewport width in CSS pixels.</param>
    /// <param name="Height">The viewport height in CSS pixels.</param>
    internal readonly record struct ViewportSize(int Width, int Height);
}
