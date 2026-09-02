/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Enforces official Playwright <c>strictSelectors</c> on single-target actions.
    /// </summary>
    internal static class StrictSelector
    {
        /// <summary>
        /// Queries <paramref name="selector"/>. When <paramref name="strict"/> is
        /// <see langword="true"/> and more than one node matches, throws.
        /// </summary>
        /// <param name="querySelectorAsync">One-shot first-match query.</param>
        /// <param name="querySelectorAllAsync">All-matches query used in strict mode.</param>
        /// <param name="selector">CSS selector.</param>
        /// <param name="strict">Whether multiple matches are an error.</param>
        /// <returns>The first match, or <see langword="null"/>.</returns>
        internal static async Task<IElementHandle> QueryAsync(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            Func<string, Task<IReadOnlyList<IElementHandle>>> querySelectorAllAsync,
            string selector,
            bool strict)
        {
            if (querySelectorAsync == null)
            {
                throw new ArgumentNullException(nameof(querySelectorAsync));
            }

            if (querySelectorAllAsync != null && !strict)
            {
                IReadOnlyList<IElementHandle> matches = await querySelectorAllAsync(selector).ConfigureAwait(false);
                if (matches != null && matches.Count > 1)
                {
                    string preview = await TryPreviewAsync(matches[0]).ConfigureAwait(false);
                    string resolved =
                        "locator resolved to " +
                        matches.Count.ToString(CultureInfo.InvariantCulture) +
                        " elements. Proceeding with the first one: " +
                        preview;
                    ActionProgress.Log(resolved);
                    ClickAction.ResolvedLog.Value = resolved;
                    for (int i = 1; i < matches.Count; i++)
                    {
                        try
                        {
                            await matches[i].DisposeAsync().ConfigureAwait(false);
                        }
                        catch (PlaywrightNativeException)
                        {
                        }
                    }

                    return matches[0];
                }

                if (matches != null && matches.Count == 1)
                {
                    return matches[0];
                }

                return null;
            }

            if (!strict || querySelectorAllAsync == null)
            {
                return await querySelectorAsync(selector).ConfigureAwait(false);
            }

            IReadOnlyList<IElementHandle> all = await querySelectorAllAsync(selector).ConfigureAwait(false);
            if (all != null && all.Count > 1)
            {
                throw new PlaywrightNativeException(
                    await StrictModeViolation.FormatAsync(
                        StrictModeViolation.QuoteLocator(selector),
                        all).ConfigureAwait(false));
            }

            if (all != null && all.Count == 1)
            {
                return all[0];
            }

            return null;
        }

        private static async Task<string> TryPreviewAsync(IElementHandle handle)
        {
            if (handle == null)
            {
                return "element";
            }

            try
            {
                string preview = await handle.EvaluateAsync<string>(RemoteObject.PreviewNodeFunction).ConfigureAwait(false);
                return string.IsNullOrEmpty(preview) ? "element" : preview;
            }
            catch (PlaywrightNativeException)
            {
                return "element";
            }
        }
    }
}
