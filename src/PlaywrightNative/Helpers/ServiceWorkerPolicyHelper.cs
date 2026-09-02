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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Applies official <c>serviceWorkers: 'block'</c> by replacing
    /// <c>navigator.serviceWorker.register</c> on every document.
    /// </summary>
    internal static class ServiceWorkerPolicyHelper
    {
        private const string BlockScript =
            "if (navigator.serviceWorker) navigator.serviceWorker.register = async () => { console.warn('Service Worker registration blocked by Playwright'); };";

        /// <summary>
        /// Installs the block init script when <paramref name="serviceWorkers"/> is <see cref="ServiceWorkerPolicy.Block"/>.
        /// </summary>
        /// <param name="context">The context that will host pages.</param>
        /// <param name="serviceWorkers">The requested policy.</param>
        /// <returns>A task that completes when the init script is stored.</returns>
        internal static Task ApplyAsync(IBrowserContext context, ServiceWorkerPolicy serviceWorkers)
        {
            if (context == null || serviceWorkers != ServiceWorkerPolicy.Block)
            {
                return Task.CompletedTask;
            }

            return context.AddInitScriptAsync(BlockScript);
        }
    }
}
