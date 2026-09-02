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
using System.Text;
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Reads a CDP/WIP/Juggler remote object payload.
    /// </summary>
    internal static class RemoteObject
    {
        /// <summary>
        /// Official injected <c>previewNode</c>: attributes (style skipped, boolean
        /// attrs without a value, sorted by length, trimmed to 500), auto-closing
        /// tags, text children trimmed to 50 code points, newlines as ↵.
        /// </summary>
        internal const string PreviewNodeFunction = @"node => {
  const oneLine = s => String(s).replace(/\n/g, '\u21B5').replace(/\t/g, '\u21C6');
  const trimEllipsis = (input, cap) => {
    input = String(input);
    if (input.length <= cap) return input;
    const chars = [...input];
    if (chars.length > cap) return chars.slice(0, cap - 1).join('') + '\u2026';
    return chars.join('');
  };
  if (!node) return 'node';
  if (node.nodeType === 3) return oneLine('#text=' + (node.nodeValue || ''));
  if (node.nodeType !== 1) return oneLine('<' + String(node.nodeName).toLowerCase() + ' />');
  const booleanAttributes = { checked: 1, selected: 1, disabled: 1, readonly: 1, multiple: 1 };
  const autoClosingTags = {
    AREA: 1, BASE: 1, BR: 1, COL: 1, COMMAND: 1, EMBED: 1, HR: 1, IMG: 1, INPUT: 1,
    KEYGEN: 1, LINK: 1, MENUITEM: 1, META: 1, PARAM: 1, SOURCE: 1, TRACK: 1, WBR: 1
  };
  const attrs = [];
  const list = node.attributes || [];
  for (let i = 0; i < list.length; i++) {
    const name = list[i].name;
    const value = list[i].value;
    if (name === 'style') continue;
    if (!value && booleanAttributes[name]) attrs.push(' ' + name);
    else attrs.push(' ' + name + '=""' + value + '""');
  }
  attrs.sort((a, b) => a.length - b.length);
  const attrText = trimEllipsis(attrs.join(''), 500);
  const tag = String(node.nodeName).toLowerCase();
  if (autoClosingTags[node.nodeName]) return oneLine('<' + tag + attrText + '/>');
  const children = node.childNodes;
  let onlyText = false;
  if (children.length <= 5) {
    onlyText = true;
    for (let i = 0; i < children.length; i++)
      onlyText = onlyText && children[i].nodeType === 3;
  }
  const text = onlyText ? (node.textContent || '') : (children.length ? '\u2026' : '');
  return oneLine('<' + tag + attrText + '>' + trimEllipsis(text, 50) + '</' + tag + '>');
}";

        /// <summary>
        /// Returns the remote <c>objectId</c>, or <see langword="null"/> when the value
        /// is null, missing, or a primitive without an id.
        /// </summary>
        /// <param name="remoteObject">The protocol remote object.</param>
        /// <returns>The object id, or <see langword="null"/>.</returns>
        internal static string GetObjectId(JsonElement? remoteObject)
        {
            if (remoteObject == null)
            {
                return null;
            }

            JsonElement value = remoteObject.Value;
            if (value.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.ValueKind == JsonValueKind.String
                && subtype.GetString() == "null")
            {
                return null;
            }

            if (!value.TryGetProperty("objectId", out JsonElement objectIdElement)
                || objectIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string objectId = objectIdElement.GetString();
            return string.IsNullOrEmpty(objectId) ? null : objectId;
        }

        /// <summary>
        /// Wraps a protocol value that has no <c>objectId</c> as an in-process handle.
        /// <see cref="IJSHandle.AsElement"/> is always <see langword="null"/>.
        /// </summary>
        /// <param name="remoteObject">The protocol remote object.</param>
        /// <returns>A primitive JS handle.</returns>
        internal static IJSHandle WrapPrimitive(JsonElement remoteObject)
        {
            string preview = HandlePreview(remoteObject);
            JsonElement value;
            if (remoteObject.TryGetProperty("value", out JsonElement inner))
            {
                value = inner.Clone();
            }
            else if (remoteObject.TryGetProperty("unserializableValue", out JsonElement uns)
                && uns.ValueKind == JsonValueKind.String)
            {
                value = uns.Clone();
            }
            else
            {
                value = default;
            }

            return new ImmediateJSHandle(value, preview);
        }

        /// <summary>
        /// Returns whether the remote object is a DOM node.
        /// </summary>
        /// <param name="remoteObject">The protocol remote object.</param>
        /// <returns><see langword="true"/> when <c>subtype</c> is <c>node</c>.</returns>
        internal static bool IsNode(JsonElement? remoteObject)
        {
            if (remoteObject == null)
            {
                return false;
            }

            JsonElement value = remoteObject.Value;
            return value.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.ValueKind == JsonValueKind.String
                && subtype.GetString() == "node";
        }

        /// <summary>
        /// Formats a remote object as console-message text.
        /// Matches upstream <c>renderPreview</c> (description / preview / subtype).
        /// </summary>
        /// <param name="remoteObject">The protocol remote object.</param>
        /// <returns>A printable preview.</returns>
        internal static string Preview(JsonElement? remoteObject)
        {
            if (remoteObject == null)
            {
                return string.Empty;
            }

            return RenderPreview(remoteObject.Value);
        }

        /// <summary>
        /// Official JSHandle preview: protocol <c>renderPreview</c>, or
        /// <c>JSHandle@type</c> when the object has an id but no description.
        /// </summary>
        /// <param name="remoteObject">The protocol remote object.</param>
        /// <returns>The handle preview string.</returns>
        internal static string HandlePreview(JsonElement? remoteObject)
        {
            if (remoteObject == null)
            {
                return "undefined";
            }

            JsonElement value = remoteObject.Value;
            string rendered = RenderPreview(value);
            string objectId = GetObjectId(remoteObject);
            if (string.IsNullOrEmpty(objectId))
            {
                return string.IsNullOrEmpty(rendered) ? "undefined" : rendered;
            }

            if (!string.IsNullOrEmpty(rendered))
            {
                return rendered;
            }

            string type = null;
            if (value.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.ValueKind == JsonValueKind.String)
            {
                type = subtype.GetString();
            }

            if (string.IsNullOrEmpty(type)
                && value.TryGetProperty("className", out JsonElement className)
                && className.ValueKind == JsonValueKind.String
                && className.GetString() == "Promise")
            {
                type = "promise";
            }

            if (string.IsNullOrEmpty(type)
                && value.TryGetProperty("type", out JsonElement typeEl)
                && typeEl.ValueKind == JsonValueKind.String)
            {
                type = typeEl.GetString();
            }

            return "JSHandle@" + (type ?? "object");
        }

        /// <summary>
        /// Upstream <c>sparseArrayToString</c> for CDP/WIP array previews.
        /// </summary>
        /// <param name="properties">The <c>preview.properties</c> array.</param>
        /// <returns>A string such as <c>[empty, 1, empty x 8, 2]</c>.</returns>
        internal static string SparseArrayToString(JsonElement properties)
        {
            if (properties.ValueKind != JsonValueKind.Array)
            {
                return "[]";
            }

            List<(int Index, string Value)> entries = new List<(int Index, string Value)>();
            foreach (JsonElement property in properties.EnumerateArray())
            {
                if (!property.TryGetProperty("name", out JsonElement nameEl)
                    || nameEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string name = nameEl.GetString();
                if (!int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                    || index < 0)
                {
                    continue;
                }

                string propValue = string.Empty;
                if (property.TryGetProperty("value", out JsonElement valueEl))
                {
                    propValue = valueEl.ValueKind == JsonValueKind.String
                        ? valueEl.GetString() ?? string.Empty
                        : StringifyValue(valueEl);
                }

                entries.Add((index, propValue));
            }

            entries.Sort((left, right) => left.Index.CompareTo(right.Index));
            int lastIndex = -1;
            List<string> tokens = new List<string>();
            foreach ((int index, string value) in entries)
            {
                int emptyItems = index - lastIndex - 1;
                if (emptyItems == 1)
                {
                    tokens.Add("empty");
                }
                else if (emptyItems > 1)
                {
                    tokens.Add("empty x " + emptyItems.ToString(CultureInfo.InvariantCulture));
                }

                tokens.Add(value);
                lastIndex = index;
            }

            return "[" + string.Join(", ", tokens) + "]";
        }

        /// <summary>
        /// Upstream Chromium/WebKit <c>renderPreview</c>.
        /// </summary>
        /// <param name="value">A protocol remote object.</param>
        /// <returns>The preview text.</returns>
        internal static string RenderPreview(JsonElement value)
        {
            if (value.TryGetProperty("type", out JsonElement typeEl)
                && typeEl.ValueKind == JsonValueKind.String
                && typeEl.GetString() == "undefined")
            {
                return "undefined";
            }

            // WebKit console arguments often include both a joined `value`
            // (",1,,,,,,,,,2,...") and `preview.properties`. Prefer the official
            // sparse-array preview so Chromium and WebKit match upstream.
            bool isArray = (value.TryGetProperty("subtype", out JsonElement arraySubtype)
                    && arraySubtype.ValueKind == JsonValueKind.String
                    && arraySubtype.GetString() == "array")
                || (value.TryGetProperty("className", out JsonElement arrayClass)
                    && arrayClass.ValueKind == JsonValueKind.String
                    && arrayClass.GetString() == "Array");
            if (isArray)
            {
                if (value.TryGetProperty("preview", out JsonElement arrayPreview)
                    && arrayPreview.TryGetProperty("properties", out JsonElement arrayProps))
                {
                    return SparseArrayToString(arrayProps);
                }

                if (value.TryGetProperty("value", out JsonElement joined)
                    && joined.ValueKind == JsonValueKind.String)
                {
                    return SparseArrayFromJoined(joined.GetString() ?? string.Empty);
                }
            }

            if (value.TryGetProperty("value", out JsonElement inner))
            {
                return StringifyValue(inner);
            }

            if (value.TryGetProperty("unserializableValue", out JsonElement unserializable)
                && unserializable.ValueKind == JsonValueKind.String)
            {
                return unserializable.GetString() ?? string.Empty;
            }

            string description = value.TryGetProperty("description", out JsonElement descriptionEl)
                && descriptionEl.ValueKind == JsonValueKind.String
                ? descriptionEl.GetString()
                : null;

            if (description == "Object"
                && value.TryGetProperty("preview", out JsonElement objectPreview)
                && objectPreview.TryGetProperty("properties", out JsonElement objectProps)
                && objectProps.ValueKind == JsonValueKind.Array)
            {
                List<string> tokens = new List<string>();
                foreach (JsonElement property in objectProps.EnumerateArray())
                {
                    string name = property.TryGetProperty("name", out JsonElement nameEl)
                        ? nameEl.GetString() ?? string.Empty
                        : string.Empty;
                    string propValue = string.Empty;
                    if (property.TryGetProperty("value", out JsonElement propEl))
                    {
                        propValue = propEl.ValueKind == JsonValueKind.String
                            ? propEl.GetString() ?? string.Empty
                            : StringifyValue(propEl);
                    }

                    tokens.Add(name + ": " + propValue);
                }

                return "{" + string.Join(", ", tokens) + "}";
            }

            if (!string.IsNullOrEmpty(description))
            {
                return description;
            }

            if (value.TryGetProperty("className", out JsonElement className)
                && className.ValueKind == JsonValueKind.String)
            {
                return className.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Joins console-API arguments with spaces.
        /// </summary>
        /// <param name="args">The <c>args</c> array from a console event.</param>
        /// <returns>The joined text.</returns>
        internal static string JoinConsoleArgs(JsonElement args)
        {
            if (args.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (JsonElement arg in args.EnumerateArray())
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Preview(arg));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Formats the first console/exception stack frame as <c>URL:line:column</c>.
        /// </summary>
        /// <param name="payload">A protocol payload that may contain <c>stackTrace.callFrames</c>.</param>
        /// <returns>The location string, or empty when no frame is present.</returns>
        internal static string FormatStackLocation(JsonElement payload)
        {
            if (!payload.TryGetProperty("stackTrace", out JsonElement stack)
                || !stack.TryGetProperty("callFrames", out JsonElement frames)
                || frames.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement frame in frames.EnumerateArray())
            {
                string url = frame.TryGetProperty("url", out JsonElement urlEl) ? urlEl.GetString() : string.Empty;
                int line = frame.TryGetProperty("lineNumber", out JsonElement lineEl) && lineEl.TryGetInt32(out int ln) ? ln : 0;
                int column = frame.TryGetProperty("columnNumber", out JsonElement colEl) && colEl.TryGetInt32(out int cn) ? cn : 0;
                if (!string.IsNullOrEmpty(url) || line != 0 || column != 0)
                {
                    return $"{url}:{line}:{column}";
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads a CDP/WIP <c>exceptionDetails</c> payload as
        /// <see cref="WebErrorLocation"/>.
        /// </summary>
        /// <param name="details">Runtime exception details.</param>
        /// <returns>The location. Url is empty when the protocol omitted it.</returns>
        internal static WebErrorLocation ParseWebErrorLocation(JsonElement details)
        {
            string url = details.TryGetProperty("url", out JsonElement urlEl)
                ? urlEl.GetString()
                : null;
            int line = details.TryGetProperty("lineNumber", out JsonElement lineEl)
                && lineEl.TryGetInt32(out int ln)
                ? ln
                : 0;
            int column = details.TryGetProperty("columnNumber", out JsonElement colEl)
                && colEl.TryGetInt32(out int cn)
                ? cn
                : 0;

            if (string.IsNullOrEmpty(url)
                && details.TryGetProperty("stackTrace", out JsonElement stack)
                && stack.TryGetProperty("callFrames", out JsonElement frames)
                && frames.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement frame in frames.EnumerateArray())
                {
                    string frameUrl = frame.TryGetProperty("url", out JsonElement fu)
                        ? fu.GetString()
                        : null;
                    if (string.IsNullOrEmpty(frameUrl))
                    {
                        continue;
                    }

                    url = frameUrl;
                    if (frame.TryGetProperty("lineNumber", out JsonElement fl)
                        && fl.TryGetInt32(out int fln))
                    {
                        line = fln;
                    }

                    if (frame.TryGetProperty("columnNumber", out JsonElement fc)
                        && fc.TryGetInt32(out int fcn))
                    {
                        column = fcn;
                    }

                    break;
                }
            }

            return new WebErrorLocation
            {
                Url = url ?? string.Empty,
                Line = line,
                Column = column,
            };
        }

        /// <summary>
        /// Converts a WebKit <c>Array.prototype.toString</c> sparse join
        /// (<c>,1,,,,,,,,,2,...</c>) into the official preview.
        /// </summary>
        /// <param name="text">Console text.</param>
        /// <param name="beautified">The sparse-array preview when conversion applies.</param>
        /// <returns><see langword="true"/> when <paramref name="text"/> was a sparse join.</returns>
        internal static bool TryBeautifySparseArrayJoin(string text, out string beautified)
        {
            beautified = null;
            if (string.IsNullOrEmpty(text)
                || (!text.StartsWith(',') && !text.Contains(",,", StringComparison.Ordinal)))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch != ',' && (ch < '0' || ch > '9') && ch != '-' && ch != '.')
                {
                    return false;
                }
            }

            beautified = SparseArrayFromJoined(text);
            return true;
        }

        private static string SparseArrayFromJoined(string joined)
        {
            string[] parts = joined.Split(',');
            List<(int Index, string Value)> entries = new List<(int Index, string Value)>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                {
                    continue;
                }

                entries.Add((i, parts[i]));
            }

            int lastIndex = -1;
            List<string> tokens = new List<string>();
            foreach ((int index, string value) in entries)
            {
                int emptyItems = index - lastIndex - 1;
                if (emptyItems == 1)
                {
                    tokens.Add("empty");
                }
                else if (emptyItems > 1)
                {
                    tokens.Add("empty x " + emptyItems.ToString(CultureInfo.InvariantCulture));
                }

                tokens.Add(value);
                lastIndex = index;
            }

            return "[" + string.Join(", ", tokens) + "]";
        }

        private static string StringifyValue(JsonElement inner)
        {
            return inner.ValueKind switch
            {
                JsonValueKind.String => inner.GetString() ?? string.Empty,
                JsonValueKind.Null => "null",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => inner.GetRawText(),
                JsonValueKind.Undefined => "undefined",
                _ => inner.GetRawText(),
            };
        }
    }
}
