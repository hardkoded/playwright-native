/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright <c>pageerror</c> Error shape: <c>name</c> / <c>message</c> /
    /// <c>stack</c>. Ports <c>exceptionToError</c>, <c>getExceptionMessage</c>,
    /// <c>splitErrorMessage</c>, and WebKit Console stack reconstruction.
    /// </summary>
    internal static class PageErrorText
    {
        /// <summary>
        /// Parses a fired pageerror string (full stack, or <c>name: message</c>)
        /// into the official Error fields.
        /// </summary>
        /// <param name="text">The raw pageerror payload.</param>
        /// <returns>The parsed name, message, and stack.</returns>
        internal static PageErrorEventArgs Parse(string text)
        {
            string normalized = Normalize(text);
            return FromMessageWithStack(normalized, nameOverride: null);
        }

        /// <summary>
        /// Official Chromium <c>exceptionToError</c> from
        /// <c>Runtime.ExceptionDetails</c>.
        /// </summary>
        /// <param name="details">CDP <c>exceptionDetails</c>.</param>
        /// <returns>The official Error fields.</returns>
        internal static PageErrorEventArgs FromExceptionDetails(JsonElement details)
        {
            string messageWithStack = GetExceptionMessage(details);
            string nameOverride = TryReadNameOverride(details);
            return FromMessageWithStack(Normalize(messageWithStack), nameOverride);
        }

        /// <summary>
        /// Official WebKit <c>wkPage._onConsoleMessage</c> pageerror
        /// reconstruction from Console <c>text</c> plus <c>stackTrace</c>.
        /// </summary>
        /// <param name="text">The Console message text.</param>
        /// <param name="message">The Console.message payload.</param>
        /// <returns>The official Error fields.</returns>
        internal static PageErrorEventArgs FromWebKitConsole(string text, JsonElement message)
        {
            string normalized = Normalize(text);
            (string name, string errorMessage) = SplitErrorMessage(normalized);
            string stack = BuildWebKitStack(normalized, message);
            return new PageErrorEventArgs
            {
                Name = name,
                Message = errorMessage,
                Stack = stack,
            };
        }

        /// <summary>
        /// Formats an Error the way official bindings expose the pageerror
        /// string: full <c>stack</c> when present, otherwise <c>name: message</c>.
        /// </summary>
        /// <param name="error">The parsed error.</param>
        /// <returns>The event / buffer string.</returns>
        internal static string Format(PageErrorEventArgs error)
        {
            if (error == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(error.Stack))
            {
                return error.Stack;
            }

            if (string.IsNullOrEmpty(error.Name))
            {
                return error.Message ?? string.Empty;
            }

            return error.Name + ": " + (error.Message ?? string.Empty);
        }

        /// <summary>
        /// Official <c>splitErrorMessage</c>: first <c>:</c> separates name from
        /// message (the two characters <c>": "</c> are skipped).
        /// </summary>
        /// <param name="message">The name-and-message line (or full text).</param>
        /// <returns>The split name and message.</returns>
        internal static (string Name, string Message) SplitErrorMessage(string message)
        {
            message ??= string.Empty;
            int separationIdx = message.IndexOf(':');
            if (separationIdx == -1)
            {
                return (string.Empty, message);
            }

            string name = message.Substring(0, separationIdx);
            string rest = separationIdx + 2 <= message.Length
                ? message.Substring(separationIdx + 2)
                : message;
            return (name, rest);
        }

        private static PageErrorEventArgs FromMessageWithStack(string messageWithStack, string nameOverride)
        {
            string[] lines = messageWithStack.Split('\n');
            int firstStackTraceLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("    at", StringComparison.Ordinal))
                {
                    firstStackTraceLine = i;
                    break;
                }
            }

            string messageWithName;
            string stack;
            if (firstStackTraceLine == -1)
            {
                messageWithName = messageWithStack;
                stack = string.Empty;
            }
            else
            {
                messageWithName = string.Join("\n", lines, 0, firstStackTraceLine);
                stack = messageWithStack;
            }

            (string name, string message) = SplitErrorMessage(messageWithName);
            return new PageErrorEventArgs
            {
                Name = nameOverride ?? name,
                Message = message,
                Stack = stack,
            };
        }

        private static string GetExceptionMessage(JsonElement details)
        {
            if (details.TryGetProperty("exception", out JsonElement exception)
                && exception.ValueKind == JsonValueKind.Object)
            {
                if (exception.TryGetProperty("description", out JsonElement description)
                    && description.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(description.GetString()))
                {
                    return description.GetString();
                }

                return StringifyExceptionValue(exception);
            }

            string message = details.TryGetProperty("text", out JsonElement text)
                && text.ValueKind == JsonValueKind.String
                ? text.GetString() ?? string.Empty
                : string.Empty;

            if (details.TryGetProperty("stackTrace", out JsonElement stackTrace)
                && stackTrace.TryGetProperty("callFrames", out JsonElement frames)
                && frames.ValueKind == JsonValueKind.Array)
            {
                StringBuilder builder = new StringBuilder(message);
                foreach (JsonElement callframe in frames.EnumerateArray())
                {
                    string url = callframe.TryGetProperty("url", out JsonElement urlEl)
                        ? urlEl.GetString() ?? string.Empty
                        : string.Empty;
                    int line = callframe.TryGetProperty("lineNumber", out JsonElement lineEl)
                        && lineEl.TryGetInt32(out int ln)
                        ? ln
                        : 0;
                    int column = callframe.TryGetProperty("columnNumber", out JsonElement colEl)
                        && colEl.TryGetInt32(out int cn)
                        ? cn
                        : 0;
                    string functionName = callframe.TryGetProperty("functionName", out JsonElement fnEl)
                        && !string.IsNullOrEmpty(fnEl.GetString())
                        ? fnEl.GetString()
                        : " ";
                    builder.Append("\n    at ").Append(functionName).Append(" (").Append(url)
                        .Append(':').Append(line).Append(':').Append(column).Append(')');
                }

                return builder.ToString();
            }

            return message;
        }

        private static string StringifyExceptionValue(JsonElement exception)
        {
            if (exception.TryGetProperty("type", out JsonElement typeEl)
                && typeEl.ValueKind == JsonValueKind.String
                && string.Equals(typeEl.GetString(), "undefined", StringComparison.Ordinal))
            {
                return "undefined";
            }

            if (!exception.TryGetProperty("value", out JsonElement value))
            {
                return string.Empty;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Null => "null",
                JsonValueKind.Undefined => "undefined",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => value.GetRawText(),
            };
        }

        private static string TryReadNameOverride(JsonElement details)
        {
            if (!details.TryGetProperty("exception", out JsonElement exception)
                || exception.ValueKind != JsonValueKind.Object
                || !exception.TryGetProperty("preview", out JsonElement preview)
                || !preview.TryGetProperty("properties", out JsonElement properties)
                || properties.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement property in properties.EnumerateArray())
            {
                if (!property.TryGetProperty("name", out JsonElement nameEl)
                    || !string.Equals(nameEl.GetString(), "name", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!property.TryGetProperty("value", out JsonElement valueEl)
                    || valueEl.ValueKind == JsonValueKind.Null
                    || valueEl.ValueKind == JsonValueKind.Undefined)
                {
                    return "Error";
                }

                return valueEl.ValueKind == JsonValueKind.String
                    ? valueEl.GetString() ?? "Error"
                    : valueEl.GetRawText();
            }

            return null;
        }

        private static string BuildWebKitStack(string text, JsonElement message)
        {
            if (!TryGetCallFrames(message, out List<string> frames) || frames.Count == 0)
            {
                return text.Contains("    at", StringComparison.Ordinal) ? text : string.Empty;
            }

            StringBuilder builder = new StringBuilder(text);
            foreach (string frame in frames)
            {
                builder.Append('\n').Append(frame);
            }

            return builder.ToString();
        }

        private static bool TryGetCallFrames(JsonElement message, out List<string> frames)
        {
            frames = new List<string>();
            if (!message.TryGetProperty("stackTrace", out JsonElement stackTrace))
            {
                return false;
            }

            JsonElement callFrames = stackTrace;
            if (stackTrace.ValueKind == JsonValueKind.Object
                && stackTrace.TryGetProperty("callFrames", out JsonElement nested)
                && nested.ValueKind == JsonValueKind.Array)
            {
                callFrames = nested;
            }

            if (callFrames.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement callFrame in callFrames.EnumerateArray())
            {
                string functionName = callFrame.TryGetProperty("functionName", out JsonElement fnEl)
                    && !string.IsNullOrEmpty(fnEl.GetString())
                    ? fnEl.GetString()
                    : "unknown";
                string url = callFrame.TryGetProperty("url", out JsonElement urlEl)
                    ? urlEl.GetString() ?? string.Empty
                    : string.Empty;
                int line = callFrame.TryGetProperty("lineNumber", out JsonElement lineEl)
                    && lineEl.TryGetInt32(out int ln)
                    ? ln
                    : 0;
                int column = callFrame.TryGetProperty("columnNumber", out JsonElement colEl)
                    && colEl.TryGetInt32(out int cn)
                    ? cn
                    : 0;
                frames.Add("    at " + functionName + " (" + url + ":" + line + ":" + column + ")");
            }

            return frames.Count > 0;
        }

        private static string Normalize(string text)
            => (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
