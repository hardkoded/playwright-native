// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Parses official Playwright aria snapshot YAML into a template tree.
    /// </summary>
    internal static class AriaSnapshotTemplateParser
    {
        internal static AriaSnapshotTemplate Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            string[] rawLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            List<string> lines = new List<string>();
            for (int i = 0; i < rawLines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(rawLines[i]))
                {
                    lines.Add(rawLines[i].TrimEnd());
                }
            }

            if (lines.Count == 0)
            {
                throw new AriaSnapshotParseException(
                    "Aria snapshot must be a YAML sequence, elements starting with \" -\"");
            }

            UnshiftLines(lines);

            if (!lines[0].TrimStart().StartsWith('-'))
            {
                throw new AriaSnapshotParseException(
                    "Aria snapshot must be a YAML sequence, elements starting with \" -\"");
            }

            AriaSnapshotTemplate fragment = new AriaSnapshotTemplate { Role = "fragment" };
            int index = 0;
            ParseSeq(fragment, lines, 0, ref index);
            if (fragment.Children.Count == 1
                && (string.IsNullOrEmpty(fragment.ContainerMode)
                    || string.Equals(fragment.ContainerMode, "contain", StringComparison.Ordinal)))
            {
                return fragment.Children[0];
            }

            return fragment;
        }

        internal static void ApplyTextValue(AriaSnapshotTemplate node, string value, bool isName, bool url = false, bool placeholder = false)
        {
            string raw = value ?? string.Empty;
            if (raw.Length >= 2 && raw[0] == '/' && raw[raw.Length - 1] == '/')
            {
                string pattern = raw.Substring(1, raw.Length - 2);
                try
                {
                    Regex regex = new Regex(pattern);
                    if (url)
                    {
                        node.UrlRegex = regex;
                    }
                    else if (placeholder)
                    {
                        node.PlaceholderRegex = regex;
                    }
                    else if (isName)
                    {
                        node.NameRegex = regex;
                    }
                    else
                    {
                        node.TextRegex = regex;
                    }

                    return;
                }
                catch (ArgumentException)
                {
                    // Official: treat bad regex as a string.
                }
            }

            string normalized = NormalizeWhiteSpace(raw);
            if (url)
            {
                node.Url = normalized;
            }
            else if (placeholder)
            {
                node.Placeholder = normalized;
            }
            else if (isName)
            {
                node.Name = normalized;
            }
            else
            {
                node.Text = normalized;
            }
        }

        internal static bool IsYamlSequence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] rawLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                if (rawLines[i].TrimStart().StartsWith('-'))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string Unshift(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            string[] rawLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            List<string> lines = new List<string>();
            for (int i = 0; i < rawLines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(rawLines[i]))
                {
                    lines.Add(rawLines[i].TrimEnd());
                }
            }

            UnshiftLines(lines);
            return string.Join("\n", lines);
        }

        internal static string NormalizeWhiteSpace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return Regex.Replace(
                text.Replace("\u200b", string.Empty, StringComparison.Ordinal)
                    .Replace("\u00ad", string.Empty, StringComparison.Ordinal),
                @"[\r\n\s\t]+",
                " ").Trim();
        }

        internal static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return Unescape(value.Substring(1, value.Length - 2));
            }

            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
            {
                return value.Substring(1, value.Length - 2).Replace("''", "'", StringComparison.Ordinal);
            }

            return value;
        }

        internal static string Unescape(string value)
        {
            StringBuilder result = new StringBuilder();
            bool escaped = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (escaped)
                {
                    result.Append(ch);
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else
                {
                    result.Append(ch);
                }
            }

            if (escaped)
            {
                result.Append('\\');
            }

            return result.ToString();
        }

        private static void ParseSeq(AriaSnapshotTemplate container, List<string> lines, int indent, ref int index)
        {
            while (index < lines.Count)
            {
                string line = lines[index];
                int pad = LeadingSpaces(line);
                if (pad < indent)
                {
                    return;
                }

                if (pad > indent)
                {
                    throw new AriaSnapshotParseException("Unexpected input:\n\n" + line.Trim() + "\n^\n");
                }

                string body = line.Substring(pad);
                if (!body.StartsWith('-'))
                {
                    throw new AriaSnapshotParseException(
                        "Aria snapshot must be a YAML sequence, elements starting with \" -\"");
                }

                body = body.Substring(1).TrimStart();
                index++;

                if (string.Equals(body, "|", StringComparison.Ordinal))
                {
                    StringBuilder block = new StringBuilder();
                    while (index < lines.Count && LeadingSpaces(lines[index]) > pad)
                    {
                        if (block.Length > 0)
                        {
                            block.Append(' ');
                        }

                        block.Append(lines[index].Trim());
                        index++;
                    }

                    AriaSnapshotTemplate folded = AriaSnapshotKeyParser.Parse(block.ToString());
                    container.Children.Add(folded);
                    continue;
                }

                if (LooksLikeNestedMapping(body))
                {
                    throw new AriaSnapshotParseException(
                        "Nested mappings are not allowed in compact mappings at line 1, column 12:\n\n- "
                        + body
                        + "\n           ^");
                }

                int colon = IndexOfUnquotedColon(body);
                if (colon < 0)
                {
                    container.Children.Add(ParseKeyOrSpecial(body));
                    continue;
                }

                string key = Unquote(body.Substring(0, colon).Trim());
                string value = body.Substring(colon + 1).Trim();
                if (string.Equals(value, "|", StringComparison.Ordinal)
                    || string.Equals(value, ">", StringComparison.Ordinal))
                {
                    StringBuilder block = new StringBuilder();
                    while (index < lines.Count && LeadingSpaces(lines[index]) > pad)
                    {
                        if (block.Length > 0)
                        {
                            block.Append(' ');
                        }

                        block.Append(lines[index].Trim());
                        index++;
                    }

                    if (string.Equals(key, "text", StringComparison.Ordinal))
                    {
                        AriaSnapshotTemplate folded = new AriaSnapshotTemplate { Kind = "text" };
                        ApplyTextValue(folded, block.ToString(), isName: false);
                        container.Children.Add(folded);
                        continue;
                    }

                    AriaSnapshotTemplate blockChild = AriaSnapshotKeyParser.Parse(key);
                    AriaSnapshotTemplate blockText = new AriaSnapshotTemplate { Kind = "text" };
                    ApplyTextValue(blockText, block.ToString(), isName: false);
                    blockChild.Children.Add(blockText);
                    container.Children.Add(blockChild);
                    continue;
                }

                if (string.Equals(key, "text", StringComparison.Ordinal))
                {
                    AriaSnapshotTemplate text = new AriaSnapshotTemplate { Kind = "text" };
                    ApplyTextValue(text, Unquote(value), isName: false);
                    container.Children.Add(text);
                    continue;
                }

                if (string.Equals(key, "/children", StringComparison.Ordinal))
                {
                    if (value != "contain" && value != "equal" && value != "deep-equal")
                    {
                        throw new AriaSnapshotParseException(
                            "Strict value should be \"contain\", \"equal\" or \"deep-equal\"");
                    }

                    container.ContainerMode = value;
                    continue;
                }

                if (key.StartsWith('/'))
                {
                    ApplyProp(container, key.Substring(1), Unquote(value));
                    continue;
                }

                AriaSnapshotTemplate child = AriaSnapshotKeyParser.Parse(key);
                if (value.Length > 0)
                {
                    AriaSnapshotTemplate text = new AriaSnapshotTemplate { Kind = "text" };
                    ApplyTextValue(text, Unquote(value), isName: false);
                    child.Children.Add(text);
                }

                container.Children.Add(child);
                if (value.Length == 0 && index < lines.Count && LeadingSpaces(lines[index]) > pad)
                {
                    ParseSeq(child, lines, LeadingSpaces(lines[index]), ref index);
                }
            }
        }

        private static AriaSnapshotTemplate ParseKeyOrSpecial(string body)
        {
            string key = Unquote(body);
            if (key.StartsWith("/children", StringComparison.Ordinal))
            {
                throw new AriaSnapshotParseException(
                    "Strict value should be \"contain\", \"equal\" or \"deep-equal\"");
            }

            return AriaSnapshotKeyParser.Parse(key);
        }

        private static void ApplyProp(AriaSnapshotTemplate node, string name, string value)
        {
            if (string.Equals(name, "url", StringComparison.Ordinal))
            {
                ApplyTextValue(node, value, isName: false, url: true);
            }
            else if (string.Equals(name, "placeholder", StringComparison.Ordinal))
            {
                ApplyTextValue(node, value, isName: false, placeholder: true);
            }
        }

        private static void UnshiftLines(List<string> lines)
        {
            int common = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                int pad = 0;
                while (pad < lines[i].Length && lines[i][pad] == ' ')
                {
                    pad++;
                }

                if (common < 0 || pad < common)
                {
                    common = pad;
                }
            }

            if (common > 0)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i] = lines[i].Length >= common ? lines[i].Substring(common) : lines[i];
                }
            }
        }

        private static bool LooksLikeNestedMapping(string body)
        {
            int first = IndexOfUnquotedColon(body);
            if (first < 0)
            {
                return false;
            }

            string after = body.Substring(first + 1);
            return IndexOfUnquotedColon(after) >= 0 && after.TrimEnd().EndsWith(':');
        }

        private static int IndexOfUnquotedColon(string text)
        {
            bool inDouble = false;
            bool inSingle = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '"' && !inSingle)
                {
                    inDouble = !inDouble;
                }
                else if (ch == '\'' && !inDouble)
                {
                    inSingle = !inSingle;
                }
                else if (ch == ':' && !inDouble && !inSingle)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int LeadingSpaces(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ')
            {
                i++;
            }

            return i;
        }
    }
}
