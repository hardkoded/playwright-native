/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>PlaywrightNative-only browser-context extras.</summary>
    internal interface IHasBrowserContextExtras
    {
        /// <summary>Emitted when a service worker is created.</summary>
        event EventHandler<IWorker> ServiceWorker;

        /// <summary>Emitted when a dialog is closed.</summary>
        event EventHandler<IDialog> DialogClosed;

        /// <summary>Active service workers in this context.</summary>
        IReadOnlyCollection<IWorker> ServiceWorkers { get; }

        /// <summary>Legacy spelling of <see cref="IBrowserContext.CookiesAsync"/>.</summary>
        /// <param name="urls">Optional URL filter.</param>
        /// <returns>Matching cookies.</returns>
        Task<IReadOnlyList<BrowserContextCookiesResult>> GetCookiesAsync(IEnumerable<string> urls = default);
    }
}
