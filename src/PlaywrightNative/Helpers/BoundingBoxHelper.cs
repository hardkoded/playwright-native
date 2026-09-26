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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared element-handle bounding-box geometry: client rect (layout flush,
    /// SVG, inline overflow) and parent-iframe offset so child-frame boxes are
    /// page-relative, matching official <c>ElementHandle.boundingBox</c>.
    /// </summary>
    internal static class BoundingBoxHelper
    {
        /// <summary>
        /// Returns <c>[x, y, width, height]</c> from
        /// <c>getBoundingClientRect</c>, which forces layout. <c>null</c> when
        /// the node is disconnected or <c>display:none</c>. Zero-size visible
        /// boxes are kept (height 0 is a valid box).
        /// </summary>
        internal const string ClientRectFunction = @"el => {
            if (!el || el.nodeType !== 1 || !el.isConnected)
                return null;
            const style = window.getComputedStyle(el);
            if (!style || style.display === 'none')
                return null;
            const r = el.getBoundingClientRect();
            return [r.x, r.y, r.width, r.height];
        }";

        /// <summary>
        /// Iframe content-box inset (border + padding) so a child-frame client
        /// rect can be translated to the iframe's border box.
        /// </summary>
        internal const string IFrameInsetFunction = @"el => {
            const style = window.getComputedStyle(el);
            return [
                (parseFloat(style.borderLeftWidth) || 0) + (parseFloat(style.paddingLeft) || 0),
                (parseFloat(style.borderTopWidth) || 0) + (parseFloat(style.paddingTop) || 0)
            ];
        }";

        /// <summary>
        /// Page-relative offset of <paramref name="frame"/>'s content origin.
        /// Main frame is <c>(0, 0)</c>. Nested frames add the hosting iframe's
        /// bounding box plus border/padding (recursive via
        /// <see cref="IElementHandle.BoundingBoxAsync"/>).
        /// </summary>
        /// <param name="frame">The element's owner frame.</param>
        /// <returns>Offset to add to a frame-local client rect.</returns>
        internal static async Task<(double X, double Y)> OwnerFrameOffsetAsync(IFrame frame)
        {
            if (frame == null || frame.ParentFrame == null)
            {
                return (0, 0);
            }

            IElementHandle frameElement = await FrameElementHelper.ResolveAsync(frame).ConfigureAwait(false);
            try
            {
                ElementHandleBoundingBoxResult box = await frameElement.BoundingBoxAsync().ConfigureAwait(false);
                if (box == null)
                {
                    return (0, 0);
                }

                float[] inset = await frameElement.EvaluateAsync<float[]>(IFrameInsetFunction).ConfigureAwait(false);
                double insetX = inset != null && inset.Length > 0 ? inset[0] : 0;
                double insetY = inset != null && inset.Length > 1 ? inset[1] : 0;
                return (box.X + insetX, box.Y + insetY);
            }
            finally
            {
                await frameElement.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
