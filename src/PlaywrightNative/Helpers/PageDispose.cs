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
    /// Official <c>await using page</c> closes the page and ignores a
    /// second close after the browser or context is already gone.
    /// </summary>
    internal static class PageDispose
    {
        /// <summary>
        /// Closes <paramref name="page"/> for <see cref="IAsyncDisposable"/>.
        /// </summary>
        /// <param name="page">The page to close.</param>
        /// <returns>A task that completes when close has been attempted.</returns>
        internal static async ValueTask RunAsync(IPage page)
        {
            try
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
            }
        }
    }
}
