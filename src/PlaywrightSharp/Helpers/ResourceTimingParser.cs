/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Maps CDP / WebKit Inspector <c>Network.ResourceTiming</c> onto
    /// <see cref="RequestTimingResult"/>. Relative fields are milliseconds from
    /// <see cref="RequestTimingResult.StartTime"/>, or <c>-1</c> when unknown.
    /// </summary>
    internal static class ResourceTimingParser
    {
        /// <summary>
        /// Creates a timing object with unavailable relative fields set to <c>-1</c>.
        /// </summary>
        /// <returns>A new <see cref="RequestTimingResult"/>.</returns>
        internal static RequestTimingResult Create()
            => new RequestTimingResult
            {
                StartTime = 0,
                DomainLookupStart = -1,
                DomainLookupEnd = -1,
                ConnectStart = -1,
                ConnectEnd = -1,
                SecureConnectionStart = -1,
                RequestStart = -1,
                ResponseStart = -1,
                ResponseEnd = -1,
            };

        /// <summary>
        /// Sets <see cref="RequestTimingResult.StartTime"/> from a wall-clock timestamp
        /// in seconds since the Unix epoch.
        /// </summary>
        /// <param name="timing">The result to update.</param>
        /// <param name="wallTimeSeconds">Seconds since 1970-01-01 UTC.</param>
        internal static void ApplyWallTime(RequestTimingResult timing, double wallTimeSeconds)
        {
            if (timing == null || wallTimeSeconds <= 0)
            {
                return;
            }

            timing.StartTime = (float)(wallTimeSeconds * 1000.0);
        }

        /// <summary>
        /// Copies CDP-style timing fields from <paramref name="resourceTiming"/>.
        /// </summary>
        /// <param name="timing">The result to update.</param>
        /// <param name="resourceTiming">The protocol <c>timing</c> object.</param>
        /// <returns>The protocol <c>requestTime</c> baseline in seconds, or 0.</returns>
        internal static double ApplyResourceTiming(RequestTimingResult timing, JsonElement resourceTiming)
        {
            if (timing == null || resourceTiming.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            timing.DomainLookupStart = ReadMs(resourceTiming, "dnsStart", "domainLookupStart");
            timing.DomainLookupEnd = ReadMs(resourceTiming, "dnsEnd", "domainLookupEnd");
            timing.ConnectStart = ReadMs(resourceTiming, "connectStart");
            timing.ConnectEnd = ReadMs(resourceTiming, "connectEnd");
            timing.SecureConnectionStart = ReadMs(resourceTiming, "sslStart", "secureConnectionStart");
            timing.RequestStart = ReadMs(resourceTiming, "sendStart", "requestStart");
            timing.ResponseStart = ReadMs(resourceTiming, "receiveHeadersEnd", "responseStart");

            return ReadDouble(resourceTiming, "requestTime");
        }

        /// <summary>
        /// Sets <see cref="RequestTimingResult.ResponseEnd"/> from the loading-finished
        /// timestamp, both in the protocol's monotonic-seconds domain.
        /// </summary>
        /// <param name="timing">The result to update.</param>
        /// <param name="requestTimeSeconds">Baseline from <c>timing.requestTime</c>.</param>
        /// <param name="finishedTimestampSeconds">
        /// <c>Network.loadingFinished</c> <c>timestamp</c>.
        /// </param>
        internal static void ApplyResponseEnd(
            RequestTimingResult timing,
            double requestTimeSeconds,
            double finishedTimestampSeconds)
        {
            if (timing == null || requestTimeSeconds <= 0 || finishedTimestampSeconds <= 0)
            {
                return;
            }

            timing.ResponseEnd = (float)((finishedTimestampSeconds - requestTimeSeconds) * 1000.0);
        }

        /// <summary>
        /// Official <c>Response._requestFinished</c> plus client
        /// <c>_setResponseEndTiming</c>: <c>responseEnd</c> is elapsed
        /// milliseconds from request start, and missing
        /// <c>requestStart</c> / <c>responseStart</c> copy that value
        /// (memory-cache / redirect without a timing payload).
        /// </summary>
        /// <param name="timing">The result to update.</param>
        /// <param name="requestTimestampSeconds">Request <c>timestamp</c>.</param>
        /// <param name="finishedTimestampSeconds">Finish <c>timestamp</c>.</param>
        internal static void ApplyRequestFinished(
            RequestTimingResult timing,
            double requestTimestampSeconds,
            double finishedTimestampSeconds)
        {
            if (timing == null)
            {
                return;
            }

            float elapsed = (float)((finishedTimestampSeconds - requestTimestampSeconds) * 1000.0);
            if (timing.ResponseStart > elapsed)
            {
                elapsed = timing.ResponseStart;
            }

            timing.ResponseEnd = elapsed;
            if (timing.RequestStart < 0)
            {
                timing.RequestStart = elapsed;
            }

            if (timing.ResponseStart < 0)
            {
                timing.ResponseStart = elapsed;
            }
        }

        /// <summary>
        /// Official client <c>_setResponseEndTiming</c> when
        /// <c>responseStart</c> was never reported.
        /// </summary>
        /// <param name="timing">The result to update.</param>
        internal static void FillMissingFromResponseEnd(RequestTimingResult timing)
        {
            if (timing == null || timing.ResponseEnd < 0)
            {
                return;
            }

            if (timing.RequestStart < 0)
            {
                timing.RequestStart = timing.ResponseEnd;
            }

            if (timing.ResponseStart < 0)
            {
                timing.ResponseStart = timing.ResponseEnd;
            }
        }

        /// <summary>
        /// Reads a numeric property from <paramref name="element"/>, or 0 when missing.
        /// </summary>
        /// <param name="element">The JSON object.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The number, or 0.</returns>
        internal static double ReadDouble(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(name, out JsonElement value)
                || value.ValueKind != JsonValueKind.Number)
            {
                return 0;
            }

            return value.GetDouble();
        }

        private static float ReadMs(JsonElement element, params string[] names)
        {
            foreach (string name in names)
            {
                if (element.TryGetProperty(name, out JsonElement value)
                    && value.ValueKind == JsonValueKind.Number)
                {
                    double ms = value.GetDouble();

                    // Official WebKit wkMillisToRoundishMillis: -1000 and
                    // non-positive values are unavailable.
                    if (ms <= 0)
                    {
                        return -1;
                    }

                    return (float)((int)(ms * 1000.0) / 1000.0);
                }
            }

            return -1;
        }
    }
}
