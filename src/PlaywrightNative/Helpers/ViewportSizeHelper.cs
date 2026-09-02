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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Legacy <see cref="ViewportSize"/> helpers from pre-migration PlaywrightNative.
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
