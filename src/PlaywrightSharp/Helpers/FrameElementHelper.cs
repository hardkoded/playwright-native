/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.WebKit;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Resolves the hosting <c>iframe</c>/<c>frame</c> element for a child frame.
    /// </summary>
    internal static class FrameElementHelper
    {
        /// <summary>
        /// Finds the parent-document element whose content frame is <paramref name="frame"/>.
        /// Uses <c>DOM.getFrameOwner</c> / <c>DOM.resolveNode</c> so closed and
        /// declarative shadow roots are visible.
        /// </summary>
        /// <param name="frame">The child frame.</param>
        /// <returns>The hosting element handle.</returns>
        internal static async Task<IElementHandle> ResolveAsync(IFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            IFrame parent = frame.ParentFrame;
            if (parent == null || frame.IsDetached)
            {
                throw new PlaywrightSharpException("Frame has been detached.");
            }

            if (frame is ChromiumFrame crFrame && frame.Page is Page crPage)
            {
                return await crPage.CrPage.GetFrameElementAsync(crFrame.Frame).ConfigureAwait(false);
            }

            if (frame is WebKitFrame wkFrame && frame.Page is WKPage wkPage)
            {
                return await wkPage.GetFrameElementAsync(wkFrame.GetWKFrame()).ConfigureAwait(false);
            }

            IReadOnlyList<IElementHandle> candidates = await parent
                .QuerySelectorAllAsync("iframe, frame")
                .ConfigureAwait(false);

            foreach (IElementHandle candidate in candidates)
            {
                IFrame content = await candidate.ContentFrameAsync().ConfigureAwait(false);
                if (ReferenceEquals(content, frame))
                {
                    return candidate;
                }

                await candidate.DisposeAsync().ConfigureAwait(false);
            }

            throw new PlaywrightSharpException("Frame has been detached.");
        }
    }
}
