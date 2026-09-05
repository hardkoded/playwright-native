// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared <c>page.ariaSnapshot()</c> (no selector) implementation.
    /// </summary>
    internal static class PageAriaSnapshot
    {
        /// <summary>
        /// Captures the page aria snapshot. AI mode stitches frames; default mode
        /// snapshots <c>body</c>/<c>frameset</c> like official Playwright.
        /// </summary>
        /// <param name="page">Page to snapshot.</param>
        /// <param name="options">Official options, or <see langword="null"/>.</param>
        /// <returns>YAML aria snapshot.</returns>
        internal static async Task<string> CaptureAsync(IPage page, PageAriaSnapshotOptions options)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            options ??= new PageAriaSnapshotOptions();

            // Microsoft.Playwright defines AriaSnapshotMode.Ai = 0, so
            // default(AriaSnapshotMode) is Ai. A null options.Mode means the
            // official default (non-AI) snapshot, not AI mode.
            AriaSnapshotMode mode = options.Mode ?? AriaSnapshotMode.Default;
            bool boxes = options.Boxes == true;
            if (mode == AriaSnapshotMode.Ai)
            {
                return await AriaSnapshotAi.CapturePageAsync(page, options.Timeout, options.Depth, boxes)
                    .ConfigureAwait(false);
            }

            // Page-level DOM walks start from a synthetic fragment that unwraps to
            // the document children. Depth 0 is only that invisible page root, so
            // there is nothing printable (official page.ariaSnapshot({ depth: 0 })).
            if (options.Depth == 0)
            {
                return string.Empty;
            }

            // Prefer a one-shot query over locator.waitFor: after SetContent the
            // main-world context can be briefly unavailable and a wait would burn
            // the full default timeout.
            IElementHandle root = await page.QuerySelectorAsync("body").ConfigureAwait(false)
                ?? await page.QuerySelectorAsync("frameset").ConfigureAwait(false);
            if (root == null)
            {
                float waitMs = options.Timeout ?? 5_000f;
                root = await page.Locator("body, frameset").First
                    .ElementHandleAsync(waitMs)
                    .WaitAsync(TimeSpan.FromMilliseconds(waitMs + 1_000))
                    .ConfigureAwait(false);
            }

            if (root == null)
            {
                throw new PlaywrightNativeException("page.ariaSnapshot: no document body.");
            }

            return await root.AriaSnapshotAsync(mode, options.Depth, options.Boxes).ConfigureAwait(false);
        }
    }
}
