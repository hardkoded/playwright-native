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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>elementHandle.dispatchEvent</c>: wait for visible, then dispatch.
    /// </summary>
    internal static class ElementDispatchEventAction
    {
        /// <summary>
        /// Waits until the element is visible, then dispatches the DOM event.
        /// </summary>
        /// <param name="element">Target element handle.</param>
        /// <param name="type">DOM event type.</param>
        /// <param name="eventInit">Optional event-init object.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        internal static async Task RunAsync(IElementHandle element, string type, object eventInit, float? timeout)
        {
            await WaitForElementStateHelper.WaitAsync(element, ElementState.Visible, timeout).ConfigureAwait(false);
            await DispatchImmediateAsync(element, type, eventInit).ConfigureAwait(false);
        }

        private static Task DispatchImmediateAsync(IElementHandle element, string type, object eventInit)
        {
            if (DispatchEventScript.TryExtractHandles(eventInit, out IReadOnlyList<KeyValuePair<string, IJSHandle>> handles, out object jsonInit)
                && handles.Count > 0)
            {
                DispatchEventScript.EnsureSameContext(element, handles[0].Value);
                string function = DispatchEventScript.WithSingleHandle(type, jsonInit, handles[0].Key);
                return element.EvaluateAsync<bool>(function, handles[0].Value);
            }

            return element.EvaluateAsync<bool>(
                DispatchEventScript.FromPayloadFunction,
                DispatchEventScript.ToPayload(type, eventInit));
        }
    }
}
