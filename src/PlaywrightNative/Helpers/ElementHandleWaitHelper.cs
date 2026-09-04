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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared <c>elementHandle.waitForElementState</c> /
    /// <c>elementHandle.waitForSelector</c> wiring.
    /// </summary>
    internal static class ElementHandleWaitHelper
    {
        /// <summary>
        /// Waits until <paramref name="handle"/> reaches <paramref name="state"/>.
        /// </summary>
        /// <param name="handle">Element to observe.</param>
        /// <param name="state">Desired state.</param>
        /// <param name="options">Official wait options.</param>
        /// <returns>A task that completes when the state is reached.</returns>
        internal static Task WaitForElementStateAsync(
            IElementHandle handle,
            ElementState state,
            ElementHandleWaitForElementStateOptions options)
            => WaitForElementStateHelper.WaitAsync(handle, state, options?.Timeout);

        /// <summary>
        /// Waits for a descendant matching <paramref name="selector"/>.
        /// </summary>
        /// <param name="handle">Host element.</param>
        /// <param name="selector">CSS selector relative to the host.</param>
        /// <param name="options">Official wait options.</param>
        /// <param name="querySelectorAsync">Scoped one-shot query.</param>
        /// <param name="querySelectorAllAsync">Scoped multi query for strict mode.</param>
        /// <param name="strictSelectors">Context default for strict mode.</param>
        /// <returns>The matched handle, or <see langword="null"/> for detached/hidden-gone.</returns>
        internal static Task<IElementHandle> WaitForSelectorAsync(
            IElementHandle handle,
            string selector,
            ElementHandleWaitForSelectorOptions options,
            Func<string, Task<IElementHandle>> querySelectorAsync,
            Func<string, Task<IReadOnlyList<IElementHandle>>> querySelectorAllAsync,
            bool strictSelectors)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (querySelectorAsync == null)
            {
                throw new ArgumentNullException(nameof(querySelectorAsync));
            }

            bool strict = options?.Strict ?? strictSelectors;
            WaitForSelectorState state = options?.State ?? WaitForSelectorState.Visible;
            return WaitForSelectorHelper.WaitAsync(
                sel => StrictSelector.QueryAsync(querySelectorAsync, querySelectorAllAsync, sel, strict),
                selector,
                state,
                options?.Timeout,
                "elementHandle.waitForSelector",
                isScopeConnectedAsync: () => IsConnectedAsync(handle));
        }

        private static async Task<bool> IsConnectedAsync(IElementHandle handle)
        {
            try
            {
                return await handle.EvaluateAsync<bool>("el => !!(el && el.isConnected)").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                return false;
            }
        }
    }
}
