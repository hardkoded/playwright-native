/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Per-call action log lines, such as official
    /// <c>locator resolved to N elements. Proceeding with the first one</c>.
    /// </summary>
    internal static class ActionProgress
    {
        private static readonly AsyncLocal<List<string>> Lines = new AsyncLocal<List<string>>();

        /// <summary>
        /// Appends <paramref name="line"/> when it is not a consecutive duplicate.
        /// </summary>
        /// <param name="line">A single call-log line.</param>
        internal static void Log(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            List<string> list = Lines.Value;
            if (list == null)
            {
                list = new List<string>();
                Lines.Value = list;
            }

            if (list.Count > 0 && string.Equals(list[list.Count - 1], line, StringComparison.Ordinal))
            {
                return;
            }

            list.Add(line);
        }

        /// <summary>
        /// Snapshot of lines recorded on this async flow.
        /// </summary>
        /// <returns>Recorded lines, or an empty list.</returns>
        internal static IReadOnlyList<string> Snapshot()
        {
            List<string> list = Lines.Value;
            if (list == null || list.Count == 0)
            {
                return Array.Empty<string>();
            }

            return list.ToArray();
        }
    }
}
