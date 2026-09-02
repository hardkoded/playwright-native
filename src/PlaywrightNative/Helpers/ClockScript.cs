/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright <c>clockSource</c> loader and time parsers.
    /// </summary>
    internal static class ClockScript
    {
        /// <summary>
        /// JavaScript <c>Date</c> range used by official <c>parseTime</c>.
        /// </summary>
        internal const long MinDateMilliseconds = -8640000000000000L;

        /// <summary>
        /// JavaScript <c>Date</c> range used by official <c>parseTime</c>.
        /// </summary>
        internal const long MaxDateMilliseconds = 8640000000000000L;

        private static readonly Regex TicksPattern = new Regex(@"^(\d\d:){0,2}\d\d?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly object SourceLock = new object();
        private static string _source;

        /// <summary>
        /// Official injected clock module source.
        /// </summary>
        internal static string Source
        {
            get
            {
                lock (SourceLock)
                {
                    if (_source == null)
                    {
                        _source = LoadSource();
                    }

                    return _source;
                }
            }
        }

        /// <summary>
        /// Builds the official installer that exposes
        /// <c>globalThis.__pwClock.controller</c>.
        /// </summary>
        /// <param name="browserName">Chromium, webkit, or firefox.</param>
        /// <returns>The init-script source.</returns>
        internal static string BuildInjector(string browserName)
        {
            string name = JsonSerializer.Serialize(string.IsNullOrEmpty(browserName) ? "chromium" : browserName);
            return
                "(() => {" +
                "const module = {};" +
                Source +
                "if (!globalThis.__pwClock) {" +
                "globalThis.__pwClock = (module.exports.inject())(globalThis, " + name + ");" +
                "}" +
                "})();";
        }

        /// <summary>
        /// Formats a millisecond value for injected JavaScript.
        /// </summary>
        /// <param name="value">The number to format.</param>
        /// <returns>An invariant-culture decimal string.</returns>
        internal static string FormatNumber(long value)
            => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Formats a fractional millisecond value for injected JavaScript.
        /// </summary>
        /// <param name="value">The number to format.</param>
        /// <returns>An invariant-culture decimal string.</returns>
        internal static string FormatNumber(double value)
            => value.ToString("G17", CultureInfo.InvariantCulture);

        /// <summary>
        /// Converts a date/time to Unix milliseconds.
        /// </summary>
        /// <param name="time">The date/time.</param>
        /// <returns>Unix time in milliseconds.</returns>
        internal static long ToUnixMilliseconds(DateTime time)
            => new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeMilliseconds();

        /// <summary>
        /// Official <c>parseTime</c> for a Unix millisecond value.
        /// </summary>
        /// <param name="time">Unix time in milliseconds.</param>
        /// <returns>The same value when it is a valid JS <c>Date</c>.</returns>
        internal static long ParseTime(long time)
        {
            if (time < MinDateMilliseconds || time > MaxDateMilliseconds)
            {
                throw new PlaywrightNativeException("Invalid date: " + FormatNumber(time));
            }

            return time;
        }

        /// <summary>
        /// Official <c>parseTime</c> for a floating-point Unix millisecond value.
        /// </summary>
        /// <param name="time">Unix time in milliseconds.</param>
        /// <returns>The truncated value when it is a valid JS <c>Date</c>.</returns>
        internal static long ParseTime(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)
                || time < MinDateMilliseconds || time > MaxDateMilliseconds)
            {
                throw new PlaywrightNativeException("Invalid date: " + FormatNumber(time));
            }

            return (long)time;
        }

        /// <summary>
        /// Parses a date string to Unix milliseconds.
        /// </summary>
        /// <param name="time">A parseable date string.</param>
        /// <returns>Unix time in milliseconds.</returns>
        internal static long ParseTime(string time)
        {
            if (string.IsNullOrWhiteSpace(time))
            {
                throw new PlaywrightNativeException("Invalid date: (empty)");
            }

            if (!DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                throw new PlaywrightNativeException("Invalid date: " + time);
            }

            return ToUnixMilliseconds(parsed);
        }

        /// <summary>
        /// Official <c>parseTicks</c> for <c>'08'</c>, <c>'mm:ss'</c>, and
        /// <c>'hh:mm:ss'</c> strings. A bare number of seconds becomes milliseconds.
        /// </summary>
        /// <param name="ticks">The duration text.</param>
        /// <returns>Duration in milliseconds.</returns>
        internal static long ParseTicks(string ticks)
        {
            if (string.IsNullOrEmpty(ticks))
            {
                return 0;
            }

            if (!TicksPattern.IsMatch(ticks))
            {
                throw new PlaywrightNativeException("Clock only understands numbers, 'mm:ss' and 'hh:mm:ss'");
            }

            string[] parts = ticks.Split(':');
            long seconds = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                int parsed = int.Parse(parts[i], CultureInfo.InvariantCulture);
                if (parsed >= 60)
                {
                    throw new PlaywrightNativeException("Invalid time " + ticks);
                }

                seconds += parsed * (long)Math.Pow(60, parts.Length - i - 1);
            }

            return seconds * 1000;
        }

        private static string LoadSource()
        {
            Assembly assembly = typeof(ClockScript).Assembly;
            using Stream stream = assembly.GetManifestResourceStream("PlaywrightNative.Helpers.clockSource.js")
                ?? throw new PlaywrightNativeException("Bundled Playwright clock source is missing.");
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
