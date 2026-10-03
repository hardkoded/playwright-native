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
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared <c>selectOption</c> wait/retry used by Chromium, WebKit, and Firefox
    /// element handles. Polls <see cref="ElementStateScript.SelectOptionFromJsonFunction"/>
    /// until options are present and enabled, or the timeout elapses.
    /// </summary>
    internal static class SelectOptionAction
    {
        /// <summary>
        /// Waits for visibility (unless <paramref name="force"/>), optionally scrolls,
        /// then selects matching options.
        /// </summary>
        /// <param name="handle">The <c>&lt;select&gt;</c> element.</param>
        /// <param name="json">JSON descriptor array from <see cref="SelectOptionPayload"/>.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="force">When <see langword="true"/>, skip the visibility wait.</param>
        /// <param name="scroll">When <see cref="ActionScroll.None"/>, skip scrolling into view.</param>
        /// <returns>The selected option values.</returns>
        internal static async Task<IReadOnlyCollection<string>> RunAsync(
            IElementHandle handle,
            string json,
            float? timeout,
            bool? force,
            ActionScroll scroll = default)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            await WaitForElementStateHelper.WaitVisibleUnlessForcedAsync(handle, force, timeout).ConfigureAwait(false);
            if (scroll != ActionScroll.None)
            {
                await handle.EvaluateAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);
            }

            string payload = json ?? "[]";
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            string lastReason = null;

            while (true)
            {
                string raw = null;
                try
                {
                    raw = await handle.EvaluateAsync<string>(
                        ElementStateScript.SelectOptionFromJsonFunction,
                        payload).ConfigureAwait(false);
                }
                catch (PlaywrightException ex)
                {
                    if (!IsTransientEvaluateError(ex))
                    {
                        throw;
                    }

                    // Detached / destroyed context: re-query via selector retry
                    // (ShouldWaitForSelectToBeSwapped replaces the <select>).
                    throw new PlaywrightException(ClickAction.NotAttachedMessage);
                }

                if (raw != null)
                {
                    using JsonDocument document = JsonDocument.Parse(raw);
                    JsonElement root = document.RootElement;
                    string status = root.TryGetProperty("status", out JsonElement statusElement)
                        ? statusElement.GetString()
                        : null;

                    if (string.Equals(status, "ok", StringComparison.Ordinal))
                    {
                        return ReadValues(root);
                    }

                    if (string.Equals(status, "error", StringComparison.Ordinal))
                    {
                        string message = root.TryGetProperty("message", out JsonElement messageElement)
                            ? messageElement.GetString()
                            : "Element is not a <select> element";
                        throw new PlaywrightException(message ?? "Element is not a <select> element");
                    }

                    if (string.Equals(status, "wait", StringComparison.Ordinal))
                    {
                        lastReason = root.TryGetProperty("reason", out JsonElement reasonElement)
                            ? reasonElement.GetString()
                            : "missing";
                    }
                    else
                    {
                        lastReason = "missing";
                    }
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw TimeoutError(timeoutMs, lastReason);
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Re-queries <paramref name="selector"/> and retries selectOption when the
        /// matched node detaches (official locator/page selectOption swap waits).
        /// </summary>
        /// <typeparam name="T">The selectOption result type.</typeparam>
        /// <param name="querySelectorAsync">One-shot selector query.</param>
        /// <param name="selector">The selector.</param>
        /// <param name="onHandle">Select on the matched handle.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in timeout messages.</param>
        /// <param name="scroll">Scroll option forwarded to the wait helper.</param>
        /// <returns>The selectOption result.</returns>
        internal static async Task<T> RunOnSelectorAsync<T>(
            Func<string, Task<IElementHandle>> querySelectorAsync,
            string selector,
            Func<IElementHandle, Task<T>> onHandle,
            float? timeout,
            string apiName,
            ActionScroll scroll = ActionScroll.None)
        {
            if (onHandle == null)
            {
                throw new ArgumentNullException(nameof(onHandle));
            }

            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            int[] waits = { 0, 20, 100, 100, 500 };
            int retry = 0;

            while (true)
            {
                try
                {
                    return await ElementQuery.WaitQueryAsync(
                        querySelectorAsync,
                        selector,
                        onHandle,
                        RemainingTimeout(timeoutMs, sw),
                        apiName,
                        scroll).ConfigureAwait(false);
                }
                catch (Exception ex) when (ClickAction.IsRetryable(ex))
                {
                    if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                    {
                        throw;
                    }

                    int wait = waits[Math.Min(retry, waits.Length - 1)];
                    retry++;
                    if (wait > 0)
                    {
                        await Task.Delay(wait).ConfigureAwait(false);
                    }
                }
            }
        }

        private static float? RemainingTimeout(int timeoutMs, Stopwatch sw)
        {
            if (timeoutMs == Timeout.Infinite)
            {
                return 0;
            }

            long left = timeoutMs - sw.ElapsedMilliseconds;
            return left < 1 ? 1 : left;
        }

        private static IReadOnlyCollection<string> ReadValues(JsonElement root)
        {
            List<string> values = new List<string>();
            if (!root.TryGetProperty("values", out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            {
                return values;
            }

            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    values.Add(item.GetString());
                }
            }

            return values;
        }

        private static bool IsTransientEvaluateError(PlaywrightException ex)
        {
            string message = ex?.Message ?? string.Empty;
            return message.Contains("detached", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Target closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Execution context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cannot find context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("session closed", StringComparison.OrdinalIgnoreCase);
        }

        private static PlaywrightException TimeoutError(int timeoutMs, string reason)
        {
            string message = "Timeout " + timeoutMs + "ms exceeded.";
            if (string.Equals(reason, "notenabled", StringComparison.Ordinal))
            {
                message += " option being selected is not enabled";
            }

            return new PlaywrightException(message);
        }
    }
}
