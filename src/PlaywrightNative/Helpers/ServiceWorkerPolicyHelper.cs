/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
