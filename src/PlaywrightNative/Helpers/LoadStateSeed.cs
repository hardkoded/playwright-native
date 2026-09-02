/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Seeds recorded lifecycle events from <c>document.readyState</c> when
    /// protocol load events were missed (about:blank <c>newPage</c> attach).
    /// </summary>
    internal static class LoadStateSeed
    {
        /// <summary>
        /// Records <c>DOMContentLoaded</c> / <c>load</c> from
        /// <paramref name="readyState"/> when not already present.
        /// </summary>
        /// <param name="record">Records a lifecycle event name.</param>
        /// <param name="readyState">The document readyState value.</param>
        internal static void Apply(Action<string> record, string readyState)
        {
            if (record == null || string.IsNullOrEmpty(readyState))
            {
                return;
            }

            if (readyState == "interactive" || readyState == "complete")
            {
                record("DOMContentLoaded");
            }

            if (readyState == "complete")
            {
                record("load");
            }
        }

        /// <summary>
        /// Evaluates <c>document.readyState</c> on <paramref name="page"/> and
        /// seeds lifecycle. Ignores evaluate failures (no context yet).
        /// </summary>
        /// <param name="page">The page to probe.</param>
        /// <param name="record">Records a lifecycle event name.</param>
        /// <returns>A task that completes when the probe finishes.</returns>
        internal static async Task TryFromDocumentAsync(IPage page, Action<string> record)
        {
            if (page == null || record == null)
            {
                return;
            }

            try
            {
                string readyState = await page.EvaluateAsync<string>("(() => document.readyState)()").ConfigureAwait(false);
                Apply(record, readyState);
            }
            catch (PlaywrightNativeException)
            {
                // Execution context is not available yet.
            }
        }
    }
}
