/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// C# port of official <c>parseTraceRaw</c>.
    /// </summary>
    internal static class OfficialTraceParser
    {
        internal sealed class ParsedTrace
        {
            internal List<JsonElement> Events { get; } = new List<JsonElement>();

            internal Dictionary<string, byte[]> Resources { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            internal List<string> Actions { get; } = new List<string>();

            internal List<ActionObject> ActionObjects { get; } = new List<ActionObject>();

            internal Dictionary<string, List<StackFrame>> Stacks { get; } = new Dictionary<string, List<StackFrame>>(StringComparer.Ordinal);
        }

        internal sealed class ActionObject
        {
            internal string CallId { get; set; }

            internal string Class { get; set; }

            internal string Method { get; set; }

            internal string Title { get; set; }

            internal JsonElement? Result { get; set; }
        }

        internal sealed class StackFrame
        {
            internal string File { get; set; }
        }

        internal static ParsedTrace Parse(string path)
        {
            ParsedTrace parsed = new ParsedTrace();
            using FileStream file = File.OpenRead(path);
            using ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                using Stream stream = entry.Open();
                using MemoryStream buffer = new MemoryStream();
                stream.CopyTo(buffer);
                parsed.Resources[entry.FullName] = buffer.ToArray();
            }

            Dictionary<string, ActionObject> byCall = new Dictionary<string, ActionObject>(StringComparer.Ordinal);
            List<(string CallId, string Title, double Start)> order = new List<(string, string, double)>();
            foreach (KeyValuePair<string, byte[]> item in parsed.Resources)
            {
                if (item.Key.EndsWith(".stacks", StringComparison.Ordinal))
                {
                    TryReadStacks(parsed, item.Value);
                    continue;
                }

                if (!item.Key.EndsWith(".trace", StringComparison.Ordinal)
                    && !item.Key.EndsWith(".network", StringComparison.Ordinal))
                {
                    continue;
                }

                string text = Encoding.UTF8.GetString(item.Value);
                foreach (string line in text.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement clone = document.RootElement.Clone();
                    parsed.Events.Add(clone);
                    if (!clone.TryGetProperty("type", out JsonElement type))
                    {
                        continue;
                    }

                    string typeName = type.GetString();
                    if (typeName == "before")
                    {
                        string callId = clone.GetProperty("callId").GetString();
                        string title = TitleOf(clone);
                        double start = clone.TryGetProperty("startTime", out JsonElement startEl)
                            ? startEl.GetDouble()
                            : 0;
                        order.Add((callId, title, start));
                        ActionObject action = new ActionObject
                        {
                            CallId = callId,
                            Title = title,
                            Class = clone.TryGetProperty("class", out JsonElement classEl) ? classEl.GetString() : null,
                            Method = clone.TryGetProperty("method", out JsonElement methodEl) ? methodEl.GetString() : null,
                        };
                        byCall[callId] = action;
                        parsed.ActionObjects.Add(action);
                    }
                    else if (typeName == "after"
                        && clone.TryGetProperty("callId", out JsonElement afterId)
                        && byCall.TryGetValue(afterId.GetString(), out ActionObject existing)
                        && clone.TryGetProperty("result", out JsonElement result))
                    {
                        existing.Result = result.Clone();
                    }
                }
            }

            order.Sort((a, b) => a.Start.CompareTo(b.Start));
            foreach ((string CallId, string Title, double Start) item in order)
            {
                parsed.Actions.Add(item.Title);
            }

            return parsed;
        }

        internal static string[] RelativeStack(ActionObject action, Dictionary<string, List<StackFrame>> stacks)
        {
            if (action == null || stacks == null || !stacks.TryGetValue(action.CallId, out List<StackFrame> frames))
            {
                return Array.Empty<string>();
            }

            List<string> names = new List<string>();
            foreach (StackFrame frame in frames)
            {
                names.Add(Path.GetFileName(frame.File));
            }

            return names.ToArray();
        }

        private static string TitleOf(JsonElement before)
        {
            if (before.TryGetProperty("title", out JsonElement titleEl)
                && titleEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(titleEl.GetString()))
            {
                return titleEl.GetString();
            }

            string method = before.TryGetProperty("method", out JsonElement methodEl)
                ? methodEl.GetString()
                : "unknown";
            return Pretty(method);
        }

        private static string Pretty(string method)
        {
            return method switch
            {
                "evaluate" => "Evaluate",
                "reload" => "Reload",
                "screenshot" => "Screenshot",
                "accept" => "Accept dialog",
                "goto" => "Navigate",
                "setContent" => "Set content",
                "waitForTimeout" => "Wait for timeout",
                "close" => "Close page",
                "route" => "Route requests",
                "continue" => "Continue request",
                _ => method,
            };
        }

        private static void TryReadStacks(ParsedTrace parsed, byte[] bytes)
        {
            string text = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(text) || text.Trim() == "{}")
            {
                return;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    List<StackFrame> frames = new List<StackFrame>();
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in property.Value.EnumerateArray())
                        {
                            frames.Add(new StackFrame
                            {
                                File = item.TryGetProperty("file", out JsonElement file)
                                    ? file.GetString()
                                    : "tracing.spec.ts",
                            });
                        }
                    }

                    parsed.Stacks[property.Name] = frames;
                }
            }
            catch (JsonException)
            {
            }
        }
    }
}
