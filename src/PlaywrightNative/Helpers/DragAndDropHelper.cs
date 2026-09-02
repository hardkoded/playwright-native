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
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Mouse-based <see cref="IPage.DragAndDropAsync"/> shared by Chromium and WebKit.
    /// </summary>
    internal static class DragAndDropHelper
    {
        /// <summary>
        /// Drags <paramref name="source"/> onto <paramref name="target"/> using the page mouse.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="source">Source selector.</param>
        /// <param name="target">Target selector.</param>
        /// <param name="sourcePosition">Optional offset inside the source box.</param>
        /// <param name="targetPosition">Optional offset inside the target box.</param>
        /// <param name="force">When <see langword="true"/>, wait for attach only (skip visibility).</param>
        /// <param name="timeout">Selector wait timeout.</param>
        /// <param name="trial">When <see langword="true"/>, skip the mouse drag.</param>
        /// <param name="steps">Intermediate mouse-move segments. Defaults to 1.</param>
        /// <param name="scroll">When <see cref="ActionScroll.None"/>, skip scrolling into view.</param>
        /// <param name="strict">When set, both selectors honor official page.dragAndDrop({ strict }).</param>
        /// <returns>A task that completes when the mouse up has been sent.</returns>
        internal static async Task RunAsync(
            IPage page,
            string source,
            string target,
            Position sourcePosition,
            Position targetPosition,
            bool? force,
            float? timeout,
            bool? trial = default,
            int? steps = default,
            ActionScroll scroll = default,
            bool? strict = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            WaitForSelectorState state = force == true
                ? WaitForSelectorState.Attached
                : WaitForSelectorState.Visible;
            IElementHandle sourceHandle = await page.WaitForSelectorAsync(source, state, timeout, strict).ConfigureAwait(false);
            IElementHandle targetHandle = await page.WaitForSelectorAsync(target, state, timeout, strict).ConfigureAwait(false);
            if (sourceHandle == null || targetHandle == null)
            {
                throw new PlaywrightNativeException($"Could not resolve drag selectors '{source}' -> '{target}'");
            }

            await RunHandlesAsync(
                page,
                sourceHandle,
                targetHandle,
                sourcePosition,
                targetPosition,
                trial,
                steps,
                scroll).ConfigureAwait(false);
        }

        /// <summary>
        /// Drags <paramref name="sourceHandle"/> onto <paramref name="targetHandle"/>
        /// using the page mouse.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="sourceHandle">Resolved source element.</param>
        /// <param name="targetHandle">Resolved target element.</param>
        /// <param name="sourcePosition">Optional offset inside the source box.</param>
        /// <param name="targetPosition">Optional offset inside the target box.</param>
        /// <param name="trial">When <see langword="true"/>, skip the mouse drag.</param>
        /// <param name="steps">Intermediate mouse-move segments. Defaults to 1.</param>
        /// <param name="scroll">When <see cref="ActionScroll.None"/>, skip scrolling into view.</param>
        /// <returns>A task that completes when the mouse up has been sent.</returns>
        internal static async Task RunHandlesAsync(
            IPage page,
            IElementHandle sourceHandle,
            IElementHandle targetHandle,
            Position sourcePosition,
            Position targetPosition,
            bool? trial = default,
            int? steps = default,
            ActionScroll scroll = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (sourceHandle == null)
            {
                throw new ArgumentNullException(nameof(sourceHandle));
            }

            if (targetHandle == null)
            {
                throw new ArgumentNullException(nameof(targetHandle));
            }

            if (ActionTrial.IsTrial(trial))
            {
                return;
            }

            if (scroll != ActionScroll.None)
            {
                await ScrollIfNeededAsync(sourceHandle).ConfigureAwait(false);
                await ScrollIfNeededAsync(targetHandle).ConfigureAwait(false);
            }

            (float sx, float sy) = await PointAsync(sourceHandle, sourcePosition).ConfigureAwait(false);
            (float tx, float ty) = await PointAsync(targetHandle, targetPosition).ConfigureAwait(false);
            int moveSteps = steps ?? 1;
            await page.Mouse.MoveAsync(sx, sy).ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.Mouse.MoveAsync(tx, ty, steps: moveSteps).ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);
        }

        private static Task ScrollIfNeededAsync(IElementHandle handle)
            => handle.EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction);

        private static async Task<(float X, float Y)> PointAsync(IElementHandle handle, Position position)
        {
            ElementHandleBoundingBoxResult box = await handle.BoundingBoxAsync().ConfigureAwait(false);
            if (box == null)
            {
                throw new PlaywrightNativeException("Element is not visible");
            }

            float x = box.X + (position != null ? position.X : box.Width / 2f);
            float y = box.Y + (position != null ? position.Y : box.Height / 2f);
            return (x, y);
        }
    }
}
