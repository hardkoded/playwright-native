/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Paints locator matches during a screenshot (Playwright <c>mask</c>).
    /// </summary>
    internal static class ScreenshotMask
    {
        internal const string DefaultColor = "#FF00FF";

        private const string InjectFunction = @"(payload) => {
  const boxes = (payload && payload.boxes) || [];
  const color = (payload && payload.color) || '#FF00FF';
  const existing = document.querySelector('[data-pw-mask]');
  if (existing)
    existing.remove();
  const root = document.createElement('div');
  root.setAttribute('data-pw-mask', '1');
  root.style.position = 'fixed';
  root.style.left = '0';
  root.style.top = '0';
  root.style.width = '0';
  root.style.height = '0';
  root.style.zIndex = '2147483647';
  root.style.pointerEvents = 'none';
  for (let i = 0; i < boxes.length; i++) {
    const box = boxes[i];
    const overlay = document.createElement('div');
    overlay.style.position = 'fixed';
    overlay.style.left = box[0] + 'px';
    overlay.style.top = box[1] + 'px';
    overlay.style.width = box[2] + 'px';
    overlay.style.height = box[3] + 'px';
    overlay.style.background = color;
    root.appendChild(overlay);
  }
  document.documentElement.appendChild(root);
  return true;
}";

        /// <summary>
        /// Resolves <paramref name="mask"/> boxes and paints overlays on <paramref name="page"/>.
        /// </summary>
        /// <param name="page">The page being captured.</param>
        /// <param name="mask">Locators to cover.</param>
        /// <param name="maskColor">Overlay color. Defaults to <see cref="DefaultColor"/>.</param>
        /// <param name="tags">Handles to dispose after the capture.</param>
        /// <returns>A task that completes when overlays are in the DOM.</returns>
        internal static async Task ApplyAsync(
            IPage page,
            IEnumerable<ILocator> mask,
            string maskColor,
            List<IElementHandle> tags)
        {
            if (page == null || mask == null || tags == null)
            {
                return;
            }

            List<double[]> boxes = new List<double[]>();
            foreach (ILocator locator in mask)
            {
                if (locator == null)
                {
                    continue;
                }

                IReadOnlyList<IElementHandle> handles = await locator.ElementHandlesAsync().ConfigureAwait(false);
                foreach (IElementHandle handle in handles)
                {
                    ElementHandleBoundingBoxResult box = await handle.BoundingBoxAsync().ConfigureAwait(false);
                    if (box == null || box.Width <= 0 || box.Height <= 0)
                    {
                        continue;
                    }

                    boxes.Add(new[] { (double)box.X, (double)box.Y, (double)box.Width, (double)box.Height });
                }
            }

            if (boxes.Count == 0)
            {
                return;
            }

            string color = string.IsNullOrEmpty(maskColor) ? DefaultColor : maskColor;
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                ["boxes"] = boxes,
                ["color"] = color,
            };

            await page.EvaluateAsync<bool>(InjectFunction, payload).ConfigureAwait(false);
            IElementHandle root = await page.QuerySelectorAsync("[data-pw-mask]").ConfigureAwait(false);
            if (root != null)
            {
                tags.Add(root);
            }
        }
    }
}
