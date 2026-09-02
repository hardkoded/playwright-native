/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>page.dispatchEvent</c> / <c>frame.dispatchEvent</c>: wait for a
    /// match, then query and dispatch in one JavaScript turn so custom-selector
    /// engines stay atomic. CSS queries pierce open shadow roots.
    /// </summary>
    internal static class DispatchEventAction
    {
        private const string PierceQueryHelpers = @"
    const pierceQueryAll = (root, selector) => {
        const out = [];
        const visit = (scope) => {
            if (!scope) return;
            try {
                const matched = scope.querySelectorAll(selector);
                for (let i = 0; i < matched.length; i++)
                    out.push(matched[i]);
            } catch (e) {}
            let nodes;
            try { nodes = scope.querySelectorAll('*'); } catch (e) { nodes = []; }
            for (let i = 0; i < nodes.length; i++) {
                if (nodes[i].shadowRoot)
                    visit(nodes[i].shadowRoot);
            }
        };
        visit(root);
        return out;
    };
";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// Polls until the selector matches, then dispatches in the same evaluate.
        /// </summary>
        /// <param name="evaluateAsync">Page or frame evaluate.</param>
        /// <param name="selector">Target selector.</param>
        /// <param name="type">DOM event type.</param>
        /// <param name="eventInit">Optional event-init object.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="strict">Whether multiple matches are an error.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <returns>A task that completes when the event has been dispatched.</returns>
        internal static async Task RunAsync(
            Func<string, object, Task<bool>> evaluateAsync,
            string selector,
            string type,
            object eventInit,
            float? timeout,
            bool strict,
            string apiName)
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            if (string.IsNullOrEmpty(selector))
            {
                throw new ArgumentException("Selector must not be empty.", nameof(selector));
            }

            object jsonInit = eventInit;
            IJSHandle handle = null;
            string handleKey = null;
            if (DispatchEventScript.TryExtractHandles(eventInit, out IReadOnlyList<KeyValuePair<string, IJSHandle>> handles, out object splitInit))
            {
                jsonInit = splitInit;
                if (handles.Count > 0)
                {
                    handle = handles[0].Value;
                    handleKey = handles[0].Key;
                }
            }

            string script = BuildScript(selector, type, jsonInit, handleKey, strict);
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                bool done = await evaluateAsync(script, handle).ConfigureAwait(false);
                if (done)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && stopwatch.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException(
                        apiName + ": Timeout " + timeoutMs.ToString(CultureInfo.InvariantCulture) + "ms exceeded.");
                }

                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        private static string BuildScript(string selector, string type, object jsonInit, string handleKey, bool strict)
        {
            string typeJson = JsonSerializer.Serialize(type ?? string.Empty);
            string initJson = jsonInit == null ? "{}" : JsonSerializer.Serialize(jsonInit, JsonOptions);
            string selectorJson = JsonSerializer.Serialize(selector);
            string strictJson = strict ? "true" : "false";
            string handleAssign = string.IsNullOrEmpty(handleKey)
                ? string.Empty
                : "    if (typeof handle !== 'undefined' && handle !== null) eventInit[" + JsonSerializer.Serialize(handleKey) + "] = handle;\n";

            string queryBlock;
            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                queryBlock = @"
    const all = " + call.DocumentQueryAllExpression + @";
    const el = all && all.length ? all[0] : null;
";
            }
            else
            {
                queryBlock = PierceQueryHelpers + @"
    const all = pierceQueryAll(document, " + selectorJson + @");
    const el = all.length ? all[0] : null;
";
            }

            string body = StrictModeViolation.GeneratorSource + @"
    const type = " + typeJson + @";
    const strict = " + strictJson + @";
" + queryBlock + @"
    if (strict && all && all.length > 1) {
        throw new Error(formatStrict('locator(' + q(" + selectorJson + @") + ')', all));
    }
    if (!el) return false;
    const eventInit = Object.assign({ bubbles: true, cancelable: true, composed: true }, " + initJson + @");
" + handleAssign + DispatchEventScript.DispatchBody + @"
    return true;
";

            if (string.IsNullOrEmpty(handleKey))
            {
                return "(() => {" + body + "})()";
            }

            return "(handle) => {" + body + "}";
        }
    }
}
