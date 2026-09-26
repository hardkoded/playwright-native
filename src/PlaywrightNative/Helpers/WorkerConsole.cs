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
using System.Globalization;
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Builds an <see cref="IConsoleMessage"/> from a worker
    /// <c>Runtime.consoleAPICalled</c> payload.
    /// </summary>
    internal static class WorkerConsole
    {
        /// <summary>
        /// Parses a console-API payload.
        /// </summary>
        /// <param name="payload">The protocol event parameters.</param>
        /// <param name="wrapRemote">Creates a handle for a remote object with an id.</param>
        /// <param name="page">The page that owns the worker, if known.</param>
        /// <returns>The console message.</returns>
        internal static ConsoleMessage Parse(JsonElement payload, Func<JsonElement, IJSHandle> wrapRemote, IPage page = null)
        {
            string type = payload.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : "log";
            JsonElement? argsElement = payload.TryGetProperty("args", out JsonElement argsEl) ? argsEl : (JsonElement?)null;
            string text = argsElement.HasValue
                ? RemoteObject.JoinConsoleArgs(argsElement.Value)
                : string.Empty;
            string location = RemoteObject.FormatStackLocation(payload);
            IReadOnlyCollection<IJSHandle> args = ConsoleArgs.Wrap(argsElement, wrapRemote);
            double timestamp = NormalizeTimestampMs(
                payload.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetDouble(out double ts)
                    ? ts
                    : 0);
            return new ConsoleMessage(type, text, location, CompatCollections.AsList(args), page, timestamp);
        }

        /// <summary>
        /// Parses a WebKit <c>Console.messageAdded</c> payload.
        /// Mirrors upstream <c>wkWorkers._onConsoleMessage</c>: handles from
        /// <c>parameters</c>, text from previews when handles exist, and
        /// timestamp in milliseconds.
        /// </summary>
        /// <param name="payload">The protocol event parameters.</param>
        /// <param name="wrapRemote">Creates a handle for a remote object with an id.</param>
        /// <param name="page">The page that owns the worker, if known.</param>
        /// <returns>The console message, or <see langword="null"/> when the payload is empty.</returns>
        internal static ConsoleMessage ParseMessageAdded(
            JsonElement payload,
            Func<JsonElement, IJSHandle> wrapRemote = null,
            IPage page = null)
        {
            if (!payload.TryGetProperty("message", out JsonElement message))
            {
                return null;
            }

            string protocolText = message.TryGetProperty("text", out JsonElement textEl)
                ? textEl.GetString() ?? string.Empty
                : string.Empty;
            string level = message.TryGetProperty("level", out JsonElement levelEl) ? levelEl.GetString() : "log";
            string protocolType = message.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : string.Empty;
            string type;
            if (string.Equals(protocolType, "timing", StringComparison.Ordinal))
            {
                type = "timeEnd";
            }
            else if (string.Equals(protocolType, "log", StringComparison.Ordinal) || string.IsNullOrEmpty(protocolType))
            {
                type = level switch
                {
                    "warning" => "warning",
                    "error" => "error",
                    "debug" => "debug",
                    "info" => "info",
                    _ => "log",
                };
            }
            else
            {
                type = protocolType;
            }

            string url = message.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : string.Empty;
            int line = message.TryGetProperty("line", out JsonElement lineEl) && lineEl.TryGetInt32(out int ln) ? ln : 0;
            int column = message.TryGetProperty("column", out JsonElement colEl) && colEl.TryGetInt32(out int cn) ? cn : 0;

            // WebKit Console.line/column are 1-based; official location is 0-based.
            int zeroLine = line > 0 ? line - 1 : 0;
            int zeroColumn = column > 0 ? column - 1 : 0;
            string location = string.IsNullOrEmpty(url) && zeroLine == 0 && zeroColumn == 0
                ? string.Empty
                : url + ":" + zeroLine.ToString(CultureInfo.InvariantCulture) + ":" + zeroColumn.ToString(CultureInfo.InvariantCulture);

            IReadOnlyCollection<IJSHandle> args;
            string text;
            if (message.TryGetProperty("parameters", out JsonElement paramsEl)
                && paramsEl.ValueKind == JsonValueKind.Array
                && paramsEl.GetArrayLength() > 0)
            {
                args = ConsoleArgs.Wrap(paramsEl, wrapRemote);
                text = RemoteObject.JoinConsoleArgs(paramsEl);
                if (string.IsNullOrEmpty(text))
                {
                    text = protocolText;
                }
            }
            else
            {
                args = ConsoleArgs.FromText(protocolText);
                text = protocolText;
            }

            // Official wkWorkers: Console.message.timestamp is seconds.
            double timestamp = NormalizeTimestampMs(
                message.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetDouble(out double ts)
                    ? ts
                    : 0);
            return new ConsoleMessage(type, text, location, CompatCollections.AsList(args), page, timestamp);
        }

        private static double NormalizeTimestampMs(double timestamp)
        {
            if (timestamp <= 0)
            {
                return 0;
            }

            // WebKit Console timestamps are seconds; Runtime.consoleAPICalled may
            // already be milliseconds. Values below 1e12 are treated as seconds.
            return timestamp < 1e12 ? timestamp * 1000 : timestamp;
        }
    }
}
