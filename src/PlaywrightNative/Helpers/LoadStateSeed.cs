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
