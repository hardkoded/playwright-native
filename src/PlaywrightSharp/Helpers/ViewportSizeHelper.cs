/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Legacy <see cref="ViewportSize"/> helpers from pre-migration PlaywrightSharp.
    /// </summary>
    internal static class ViewportSizeHelper
    {
        /// <summary>Default viewport when none is specified (1280×720).</summary>
        internal static ViewportSize Default => new ViewportSize { Height = 720, Width = 1280 };

        /// <summary>Sentinel that disables viewport emulation.</summary>
        internal static ViewportSize NoViewport => new ViewportSize { Height = -1, Width = -1 };

        /// <summary>Clones <paramref name="viewport"/>.</summary>
        internal static ViewportSize Clone(ViewportSize viewport)
            => viewport == null ? null : new ViewportSize { Width = viewport.Width, Height = viewport.Height };

        /// <summary>
        /// Official omitted viewport is 1280×720. <see cref="NoViewport"/> disables emulation.
        /// </summary>
        internal static ViewportSize Resolve(ViewportSize viewport)
        {
            if (viewport == null)
            {
                return Clone(Default);
            }

            if (viewport.Width < 0 && viewport.Height < 0)
            {
                return null;
            }

            return Clone(viewport);
        }
    }
}
