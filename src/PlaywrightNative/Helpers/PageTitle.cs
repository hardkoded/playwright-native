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
    /// Reads <c>document.title</c> without throwing when the execution context
    /// is destroyed mid-navigation. Official Playwright <c>page.title()</c>
    /// returns a string (including empty or Chrome's <c>Loading …</c> title)
    /// rather than rejecting during that race.
    /// </summary>
    internal static class PageTitle
    {
        /// <summary>
        /// Evaluates the title, retrying when the context is replaced.
        /// </summary>
        /// <param name="evaluateAsync">Reads <c>document.title</c>.</param>
        /// <returns>The document title, or an empty string if evaluation never succeeds.</returns>
        internal static async Task<string> ReadAsync(Func<Task<string>> evaluateAsync)
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            const int attempts = 8;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    return await evaluateAsync().ConfigureAwait(false) ?? string.Empty;
                }
                catch (PlaywrightNativeException) when (i < attempts - 1)
                {
                    await Task.Delay(25).ConfigureAwait(false);
                }
            }

            return string.Empty;
        }
    }
}
