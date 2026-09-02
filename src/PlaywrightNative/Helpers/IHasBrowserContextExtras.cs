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
