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
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared <c>$eval</c> / <c>$$eval</c> plumbing for pages, frames, and element handles.
    /// </summary>
    internal static class EvalOnSelector
    {
        /// <summary>
        /// Temporary <c>Array.from</c> that copies array-likes without using the
        /// page's (possibly hijacked) <c>Array.from</c>. Official
        /// <c>$$eval</c> must survive <c>Array.from = () =&gt; []</c>.
        /// </summary>
        private const string ToArrayScript =
            "const stolen = Array.from; const toArray = (list) => { const out = []; if (!list) return out; for (let i = 0; i < list.length; i++) out.push(list[i]); return out; }; Array.from = toArray;";

        /// <summary>
        /// Builds a document-scoped <c>querySelectorAll</c> function for
        /// <paramref name="selector"/>. Returned as a function (not an IIFE)
        /// so <see cref="IPage.EvaluateHandleAsync(string, object)"/> can invoke it once.
        /// Official engines keep their official meaning via
        /// <see cref="CustomSelectors.TryResolve"/>.
        /// </summary>
        /// <param name="selector">A CSS or official-engine selector.</param>
        /// <returns>A JavaScript function <c>() =&gt; Element[]</c>.</returns>
        internal static string DocumentQuerySelectorAllExpression(string selector)
        {
            string inner;
            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                inner = call.DocumentQueryAllExpression;
            }
            else
            {
                inner = "document.querySelectorAll(" + JsonSerializer.Serialize(selector) + ")";
            }

            return "() => { " + ToArrayScript + " try { return toArray(" + inner + "); } finally { Array.from = stolen; } }";
        }

        /// <summary>
        /// Builds an element-scoped <c>querySelectorAll</c> function for <paramref name="selector"/>.
        /// </summary>
        /// <param name="selector">A CSS or registered <c>name=body</c> selector.</param>
        /// <returns>A JavaScript function <c>(el) =&gt; Element[]</c>.</returns>
        internal static string ElementQuerySelectorAllExpression(string selector)
        {
            string inner;
            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                inner = "(" + call.ElementQueryAllFunction + ")(el)";
            }
            else
            {
                inner = "el.querySelectorAll(" + JsonSerializer.Serialize(selector) + ")";
            }

            return "(el) => { " + ToArrayScript + " try { return toArray(" + inner + "); } finally { Array.from = stolen; } }";
        }

        /// <summary>
        /// Evaluates <paramref name="expression"/> on the queried element, then disposes it.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="queryTask">The in-flight selector query.</param>
        /// <param name="selector">The selector, used in the missing-node error.</param>
        /// <param name="expression">A function receiving the element as its first argument.</param>
        /// <param name="arg">An optional second argument passed to <paramref name="expression"/>.</param>
        /// <param name="apiName">Optional official API name prefixed on the missing-node error.</param>
        /// <returns>The function result.</returns>
        internal static async Task<T> OnHandleAsync<T>(
            Task<IElementHandle> queryTask,
            string selector,
            string expression,
            object arg,
            string apiName = null)
        {
            IElementHandle handle = await queryTask.ConfigureAwait(false);
            if (handle == null)
            {
                string prefix = string.IsNullOrEmpty(apiName) ? string.Empty : apiName + ": ";
                throw new PlaywrightNativeException(prefix + "No node found for selector: " + selector);
            }

            try
            {
                return await handle.EvaluateAsync<T>(expression, arg).ConfigureAwait(false);
            }
            finally
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Evaluates <paramref name="expression"/> on a remote element array, then disposes it.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="arrayTask">The in-flight handle for the element array.</param>
        /// <param name="expression">A function receiving the element array as its first argument.</param>
        /// <param name="arg">An optional second argument passed to <paramref name="expression"/>.</param>
        /// <returns>The function result.</returns>
        internal static async Task<T> OnArrayAsync<T>(
            Task<IJSHandle> arrayTask,
            string expression,
            object arg)
        {
            IJSHandle array = await arrayTask.ConfigureAwait(false);
            if (array == null)
            {
                throw new PlaywrightNativeException("Failed to create an element array for evaluation.");
            }

            try
            {
                return await array.EvaluateAsync<T>(expression, arg).ConfigureAwait(false);
            }
            finally
            {
                await array.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
