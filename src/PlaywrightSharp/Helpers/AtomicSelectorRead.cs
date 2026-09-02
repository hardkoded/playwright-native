/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official atomic selector reads: query and property access run in one
    /// in-page turn so a custom engine's microtask cannot mutate first.
    /// </summary>
    internal static class AtomicSelectorRead
    {
        /// <summary>
        /// Waits until <paramref name="selector"/> matches, then returns
        /// <paramref name="valueJs"/> from that element in the same turn.
        /// </summary>
        /// <param name="evaluateAsync">Page or frame <c>evaluate</c>.</param>
        /// <param name="selector">A Playwright selector.</param>
        /// <param name="valueJs">Expression using <c>el</c>, e.g. <c>el.textContent</c>.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <param name="strict">When <see langword="true"/>, multiple matches throw.</param>
        /// <returns>The property value, which may be <see langword="null"/>.</returns>
        internal static async Task<string> WaitStringAsync(
            Func<string, Task<JsonElement?>> evaluateAsync,
            string selector,
            string valueJs,
            float? timeout,
            string apiName,
            bool strict)
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            string expression = ReadFunction(selector, valueJs, strict);
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();

            while (true)
            {
                JsonElement? raw = await evaluateAsync(expression).ConfigureAwait(false);
                ThrowIfStrict(raw, selector);
                if (TryRead(raw, out bool found, out string value) && found)
                {
                    return value;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw new TimeoutException(
                        apiName +
                        ": Timeout " +
                        timeoutMs.ToString(CultureInfo.InvariantCulture) +
                        "ms exceeded.");
                }

                await Task.Delay(16).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// One-shot official <c>isVisible</c>: query and visibility run in one turn.
        /// A missing element is not visible.
        /// </summary>
        /// <param name="evaluateAsync">Page or frame <c>evaluate</c>.</param>
        /// <param name="selector">A Playwright selector.</param>
        /// <param name="strict">When <see langword="true"/>, multiple matches throw.</param>
        /// <returns><see langword="true"/> when the first match is visible.</returns>
        internal static async Task<bool> IsVisibleAsync(
            Func<string, Task<JsonElement?>> evaluateAsync,
            string selector,
            bool strict)
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            JsonElement? raw;
            try
            {
                raw = await evaluateAsync(VisibleFunction(selector, strict)).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException ex)
            {
                throw EscapeXpathSelector(ex, selector);
            }

            ThrowIfStrict(raw, selector);
            if (TryRead(raw, out bool found, out string value) && found)
            {
                return string.Equals(value, "true", StringComparison.Ordinal);
            }

            if (raw.HasValue && raw.Value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            return false;
        }

        private static string QueryExpression(string selector)
        {
            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                return call.DocumentQueryExpression;
            }

            return "((" + ShadowPiercingQuery.QueryFunction + ")(" + JsonSerializer.Serialize(selector) + "))";
        }

        private static string QueryAllExpression(string selector)
        {
            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                return call.DocumentQueryAllExpression;
            }

            return "((" + ShadowPiercingQuery.QueryAllFunction + ")(" + JsonSerializer.Serialize(selector) + "))";
        }

        private static string StrictGuard(string selector, bool strict)
        {
            if (!strict)
            {
                return "const el = " + QueryExpression(selector) + ";";
            }

            return "const all = " + QueryAllExpression(selector) + ";" +
                " if (all && all.length > 1) return { ok: true, n: all.length };" +
                " const el = all && all.length ? all[0] : null;";
        }

        private static PlaywrightSharpException EscapeXpathSelector(PlaywrightSharpException ex, string selector)
        {
            if (ex == null)
            {
                return new PlaywrightSharpException("xpath");
            }

            if (string.IsNullOrEmpty(selector) || !selector.Contains('\'', StringComparison.Ordinal))
            {
                return ex;
            }

            string escaped = selector.Replace("'", "\\'", StringComparison.Ordinal);
            if (ex.Message.Contains(escaped, StringComparison.Ordinal))
            {
                return ex;
            }

            return new PlaywrightSharpException(ex.Message + " " + escaped);
        }

        private static void ThrowIfStrict(JsonElement? raw, string selector)
        {
            if (raw == null || raw.Value.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!raw.Value.TryGetProperty("n", out JsonElement n)
                || n.ValueKind != JsonValueKind.Number
                || !n.TryGetInt32(out int count)
                || count <= 1)
            {
                return;
            }

            throw new PlaywrightSharpException(
                "strict mode violation: " +
                StrictModeViolation.QuoteLocator(selector) +
                " resolved to " +
                count.ToString(CultureInfo.InvariantCulture) +
                " elements:");
        }

        private static string ReadFunction(string selector, string valueJs, bool strict)
        {
            return "() => { " + StrictGuard(selector, strict) + " if (!el) return { ok: false }; return { ok: true, v: " + valueJs + " }; }";
        }

        private static string VisibleFunction(string selector, bool strict)
        {
            return "() => { " + StrictGuard(selector, strict) + " if (!el) return { ok: true, v: 'false' }; return { ok: true, v: (" + DomVisibility.IsVisibleFunction + ")(el) ? 'true' : 'false' }; }";
        }

        private static bool TryRead(JsonElement? raw, out bool found, out string value)
        {
            found = false;
            value = null;
            if (raw == null)
            {
                return false;
            }

            JsonElement element = raw.Value;
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!element.TryGetProperty("ok", out JsonElement ok) || ok.ValueKind != JsonValueKind.True)
            {
                return true;
            }

            found = true;
            if (!element.TryGetProperty("v", out JsonElement v)
                || v.ValueKind == JsonValueKind.Null
                || v.ValueKind == JsonValueKind.Undefined)
            {
                value = null;
                return true;
            }

            if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            {
                value = v.GetBoolean() ? "true" : "false";
                return true;
            }

            value = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
            return true;
        }
    }
}
