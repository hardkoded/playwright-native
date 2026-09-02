/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightNative;
using PlaywrightNative.Chromium;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Captures an element screenshot after applying page decorations and
    /// measuring the bounding box while those decorations are active.
    /// </summary>
    internal static class ElementScreenshot
    {
        /// <summary>
        /// Scrolls the element into view, measures its box, and captures a clip.
        /// </summary>
        /// <param name="element">The element to capture.</param>
        /// <param name="page">The owning page.</param>
        /// <param name="path">Optional file path.</param>
        /// <param name="type">Image format.</param>
        /// <param name="quality">JPEG quality.</param>
        /// <param name="omitBackground">Hide the default background.</param>
        /// <param name="timeout">Timeout for the capture.</param>
        /// <param name="scale">CSS vs device scale.</param>
        /// <param name="animations">Screenshot animations option.</param>
        /// <param name="caret">Screenshot caret option.</param>
        /// <param name="style">Optional caller stylesheet.</param>
        /// <param name="mask">Locators whose matches are painted over.</param>
        /// <param name="maskColor">Overlay color. Defaults to magenta.</param>
        /// <returns>The image bytes.</returns>
        internal static async Task<byte[]> CaptureAsync(
            IElementHandle element,
            IPage page,
            string path,
            ScreenshotType type,
            int? quality,
            bool? omitBackground,
            float? timeout,
            string scale,
            string animations,
            string caret = null,
            string style = null,
            IEnumerable<ILocator> mask = null,
            string maskColor = null)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return await ScreenshotDecorations.CaptureAsync(
                page,
                animations,
                caret,
                style,
                () => CaptureClipAsync(element, page, path, type, quality, omitBackground, timeout, scale),
                mask,
                maskColor).ConfigureAwait(false);
        }

        /// <summary>
        /// Official <c>_waitAndScrollIntoViewIfNeeded(waitForVisible: true)</c>
        /// plus a stable box so moving elements settle before capture.
        /// </summary>
        /// <param name="element">The element to capture.</param>
        /// <param name="timeout">Screenshot timeout.</param>
        /// <returns>A task that completes when the element is visible and stable.</returns>
        internal static async Task WaitForScreenshotReadyAsync(IElementHandle element, float? timeout)
        {
            try
            {
                await WaitForElementStateHelper.WaitAsync(element, ElementState.Visible, timeout).ConfigureAwait(false);
                await WaitForElementStateHelper.WaitAsync(element, ElementState.Stable, timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
                throw new TimeoutException(
                    "elementHandle.screenshot: Timeout " + timeoutMs + "ms exceeded.\nelement is not visible");
            }
        }

        private static async Task<byte[]> CaptureClipAsync(
            IElementHandle element,
            IPage page,
            string path,
            ScreenshotType type,
            int? quality,
            bool? omitBackground,
            float? timeout,
            string scale)
        {
            bool attached = await element.EvaluateAsync<bool>("el => el.isConnected").ConfigureAwait(false);
            if (!attached)
            {
                throw new PlaywrightNativeException("Element is not attached to the DOM");
            }

            await WaitForScreenshotReadyAsync(element, timeout).ConfigureAwait(false);

            await element.EvaluateAsync("el => { el.scrollIntoView({ block: 'nearest', inline: 'nearest' }); }").ConfigureAwait(false);
            ElementHandleBoundingBoxResult box = await element.BoundingBoxAsync().ConfigureAwait(false);
            if (box == null || box.Width <= 0 || box.Height <= 0)
            {
                throw new PlaywrightNativeException("Node is either not visible or not an HTMLElement");
            }

            // Official screenshotter.screenshotElement: documentRect = bbox + scroll,
            // then takeScreenshot(documentRect, fitsViewport).
            double[] scroll = await page.EvaluateAsync<double[]>("() => [window.scrollX, window.scrollY]").ConfigureAwait(false);
            double scrollX = scroll != null && scroll.Length > 0 ? scroll[0] : 0;
            double scrollY = scroll != null && scroll.Length > 1 ? scroll[1] : 0;
            Clip documentClip = ScreenshotValidate.EnclosingIntRect(
                box.X + scrollX,
                box.Y + scrollY,
                box.Width,
                box.Height);
            PageViewportSizeResult viewport = page.ViewportSize;
            bool fitsViewport = viewport == null
                || (box.Width <= viewport.Width && box.Height <= viewport.Height);

            if (page is Page chromiumPage)
            {
                return await chromiumPage.ScreenshotDocumentClipAsync(
                    documentClip,
                    fitsViewport,
                    path,
                    type,
                    quality,
                    omitBackground,
                    scale).ConfigureAwait(false);
            }

            if (page is WKPage webkitPage)
            {
                return await webkitPage.ScreenshotDocumentClipAsync(
                    documentClip,
                    path,
                    type,
                    quality,
                    omitBackground,
                    scale).ConfigureAwait(false);
            }

            return await page.ScreenshotAsync(
                path,
                type,
                quality,
                omitBackground,
                timeout,
                scale,
                fullPage: false,
                clip: new Clip { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height }).ConfigureAwait(false);
        }
    }
}
