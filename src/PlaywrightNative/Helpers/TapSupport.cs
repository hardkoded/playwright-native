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
    /// Official <c>page.tap</c> requires context <c>hasTouch</c>.
    /// </summary>
    internal static class TapSupport
    {
        /// <summary>
        /// Official trial interceptor events. Capture-phase window listeners
        /// block these during <c>trial: true</c> so the tap still moves the
        /// pointer (pointerover/enter/out/leave) without delivering
        /// pointerdown/up or touchstart/end to page listeners.
        /// </summary>
        private const string InstallTrialInterceptorFunction = @"() => {
    const events = ['pointerdown', 'pointerup', 'touchstart', 'touchend', 'touchcancel'];
    const listener = (event) => {
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();
    };
    window.__pwTapTrialListener = listener;
    window.__pwTapTrialEvents = events;
    for (const type of events) {
        window.addEventListener(type, listener, { capture: true, passive: false });
    }
}";

        private const string RemoveTrialInterceptorFunction = @"() => {
    const listener = window.__pwTapTrialListener;
    const events = window.__pwTapTrialEvents || [];
    if (listener) {
        for (const type of events) {
            window.removeEventListener(type, listener, { capture: true });
        }
    }
    delete window.__pwTapTrialListener;
    delete window.__pwTapTrialEvents;
}";

        /// <summary>
        /// Throws official <c>The page does not support tap</c> when touch is off.
        /// </summary>
        /// <param name="context">The owning context, or <see langword="null"/>.</param>
        internal static void ThrowIfDisabled(IBrowserContext context)
        {
            if (context is IHasTouch touch && touch.HasTouch)
            {
                return;
            }

            throw new PlaywrightNativeException("The page does not support tap");
        }

        /// <summary>
        /// Official <c>trial</c> tap still dispatches the protocol tap, but a
        /// window capture interceptor swallows pointerdown/up and
        /// touchstart/end/cancel so page listeners only see the hover pair.
        /// </summary>
        /// <param name="handle">The element being tapped.</param>
        /// <param name="trial">When <see langword="true"/>, install the interceptor.</param>
        /// <param name="tapAsync">Dispatches the protocol tap.</param>
        /// <returns>A task that completes when the tap (or trial tap) finishes.</returns>
        internal static async Task WithTrialInterceptorAsync(IJSHandle handle, bool? trial, Func<Task> tapAsync)
        {
            if (tapAsync == null)
            {
                throw new ArgumentNullException(nameof(tapAsync));
            }

            if (!ActionTrial.IsTrial(trial))
            {
                await tapAsync().ConfigureAwait(false);
                return;
            }

            await handle.EvaluateAsync<object>(InstallTrialInterceptorFunction).ConfigureAwait(false);
            try
            {
                await tapAsync().ConfigureAwait(false);
            }
            finally
            {
                await handle.EvaluateAsync<object>(RemoveTrialInterceptorFunction).ConfigureAwait(false);
            }
        }
    }
}
