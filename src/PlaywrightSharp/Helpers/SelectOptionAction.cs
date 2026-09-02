/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
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
                catch (PlaywrightSharpException ex)
                {
                    if (!IsTransientEvaluateError(ex))
                    {
                        throw;
                    }

                    lastReason = "detached";
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
                        throw new PlaywrightSharpException(message ?? "Element is not a <select> element");
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

        private static bool IsTransientEvaluateError(PlaywrightSharpException ex)
        {
            string message = ex?.Message ?? string.Empty;
            return message.Contains("detached", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Target closed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Execution context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cannot find context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("session closed", StringComparison.OrdinalIgnoreCase);
        }

        private static PlaywrightSharpException TimeoutError(int timeoutMs, string reason)
        {
            string message = "Timeout " + timeoutMs + "ms exceeded.";
            if (string.Equals(reason, "notenabled", StringComparison.Ordinal))
            {
                message += " option being selected is not enabled";
            }

            return new PlaywrightSharpException(message);
        }
    }
}
