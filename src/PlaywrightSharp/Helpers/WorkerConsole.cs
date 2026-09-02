/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PlaywrightSharp.Helpers
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
            double timestamp = payload.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetDouble(out double ts)
                ? ts
                : 0;
            return new ConsoleMessage(type, text, location, CompatCollections.AsList(args), page, timestamp);
        }

        /// <summary>
        /// Parses a WebKit <c>Console.messageAdded</c> payload.
        /// </summary>
        /// <param name="payload">The protocol event parameters.</param>
        /// <param name="page">The page that owns the worker, if known.</param>
        /// <returns>The console message, or <see langword="null"/> when the payload is empty.</returns>
        internal static ConsoleMessage ParseMessageAdded(JsonElement payload, IPage page = null)
        {
            if (!payload.TryGetProperty("message", out JsonElement message))
            {
                return null;
            }

            string text = message.TryGetProperty("text", out JsonElement textEl) ? textEl.GetString() : string.Empty;
            string level = message.TryGetProperty("level", out JsonElement levelEl) ? levelEl.GetString() : "log";
            string type = level switch
            {
                "warning" => "warning",
                "error" => "error",
                "debug" => "debug",
                "info" => "info",
                _ => "log",
            };
            string url = message.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : string.Empty;
            int line = message.TryGetProperty("line", out JsonElement lineEl) && lineEl.TryGetInt32(out int ln) ? ln : 0;
            int column = message.TryGetProperty("column", out JsonElement colEl) && colEl.TryGetInt32(out int cn) ? cn : 0;
            string location = string.IsNullOrEmpty(url) && line == 0 && column == 0
                ? string.Empty
                : url + ":" + line.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + column.ToString(System.Globalization.CultureInfo.InvariantCulture);
            IReadOnlyCollection<IJSHandle> args = message.TryGetProperty("parameters", out JsonElement paramsEl)
                ? ConsoleArgs.Wrap(paramsEl, _ => null)
                : ConsoleArgs.FromText(text);
            double timestamp = message.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetDouble(out double ts)
                ? ts
                : 0;
            return new ConsoleMessage(type, text, location, CompatCollections.AsList(args), page, timestamp);
        }
    }
}
