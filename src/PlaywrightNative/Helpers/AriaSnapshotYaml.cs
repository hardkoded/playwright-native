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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Formats an <see cref="AccessibilitySnapshotResult"/> tree as Playwright-style
    /// YAML for <c>AriaSnapshotAsync</c>.
    /// </summary>
    internal static class AriaSnapshotYaml
    {
        /// <summary>
        /// Serializes <paramref name="node"/> to a YAML accessibility snapshot.
        /// </summary>
        /// <param name="node">The snapshot root, or <see langword="null"/>.</param>
        /// <param name="mode">Official Playwright <c>mode</c>. AI mode adds <c>[ref=eN]</c>.</param>
        /// <param name="depth">
        /// Official Playwright <c>depth</c>. Maximum descendant level to
        /// include. The root is level 0. When omitted, the full tree is written.
        /// </param>
        /// <param name="boxes">
        /// Official Playwright <c>boxes</c>. Pre-order
        /// <c>x,y,width,height</c> rows appended as <c>[box=…]</c>.
        /// </param>
        /// <param name="omitDescendantNames">
        /// When <see langword="true"/>, skip computed names on descendants
        /// (property expect matchers clip depth-1 names from nested content).
        /// </param>
        /// <returns>The YAML text, or an empty string when <paramref name="node"/> is null.</returns>
        internal static string Format(
            AccessibilitySnapshotResult node,
            AriaSnapshotMode mode = default,
            int? depth = default,
            IReadOnlyList<int[]> boxes = null,
            bool omitDescendantNames = false)
        {
            if (node == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            int nextRef = 1;
            int nextBox = 0;
            Write(builder, node, indent: 0, mode == AriaSnapshotMode.Ai, ref nextRef, 0, depth, boxes, ref nextBox, omitDescendantNames);
            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Official <c>toMatchAriaSnapshot</c> received YAML (no refs, unwrap
        /// document wrappers, emit states / <c>/url</c> / text children).
        /// </summary>
        /// <param name="node">The snapshot root, or <see langword="null"/>.</param>
        /// <returns>The YAML text, or an empty string when <paramref name="node"/> is null.</returns>
        internal static string FormatForExpect(AccessibilitySnapshotResult node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            List<AccessibilitySnapshotResult> roots = new List<AccessibilitySnapshotResult>();
            CollectExpectRoots(node, roots);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < roots.Count; i++)
            {
                WriteExpect(builder, roots[i], indent: 0);
            }

            return builder.ToString().TrimEnd();
        }

        private static void CollectExpectRoots(AccessibilitySnapshotResult node, List<AccessibilitySnapshotResult> roots)
        {
            if (node == null)
            {
                return;
            }

            if (IsExpectWrapper(node) && node.Children != null)
            {
                foreach (AccessibilitySnapshotResult child in node.Children)
                {
                    CollectExpectRoots(child, roots);
                }

                return;
            }

            roots.Add(node);
        }

        private static bool IsExpectWrapper(AccessibilitySnapshotResult node)
        {
            string role = node.Role ?? string.Empty;
            if (string.Equals(role, "WebArea", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "document", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Iframe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "presentation", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(role))
            {
                return true;
            }

            return string.Equals(role, "generic", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(node.Name);
        }

        private static void WriteExpect(StringBuilder builder, AccessibilitySnapshotResult node, int indent)
        {
            if (IsTextRole(node.Role))
            {
                string text = node.Name ?? node.Value ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                builder.Append(' ', indent * 2);
                builder.Append("- text: ");
                builder.Append(YamlEscapeValue(text));
                builder.AppendLine();
                return;
            }

            if (IsExpectWrapper(node) && node.Children != null)
            {
                foreach (AccessibilitySnapshotResult child in node.Children)
                {
                    WriteExpect(builder, child, indent);
                }

                return;
            }

            string role = NormalizeExpectRole(node.Role);
            if (IsSkippedRole(role))
            {
                return;
            }

            bool nameAsValue = UseNameAsValue(role);
            string name = node.Name ?? string.Empty;
            if (string.IsNullOrEmpty(name) && IsRowRole(role))
            {
                name = ConcatExpectName(node);
            }

            List<AccessibilitySnapshotResult> kids = VisibleExpectChildren(node, name);
            if (IsTableRole(role))
            {
                kids = WrapExpectRows(kids);
            }

            bool hasUrl = !string.IsNullOrEmpty(node.Url);
            bool hasPlaceholder = !string.IsNullOrEmpty(node.Placeholder)
                && !string.Equals(
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Placeholder),
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(name),
                    StringComparison.Ordinal);
            string collapsed = CollapsedText(node, name, nameAsValue);
            if (kids.Count == 1 && IsTextRole(kids[0].Role) && !hasUrl)
            {
                string childText = AriaSnapshotTemplateParser.NormalizeWhiteSpace(
                    kids[0].Name ?? kids[0].Value ?? string.Empty);
                string normalizedName = AriaSnapshotTemplateParser.NormalizeWhiteSpace(name);
                if (string.IsNullOrEmpty(normalizedName) || string.Equals(normalizedName, childText, StringComparison.Ordinal))
                {
                    nameAsValue = true;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = kids[0].Name ?? kids[0].Value ?? string.Empty;
                    }

                    kids = new List<AccessibilitySnapshotResult>();
                    collapsed = name;
                }
                else
                {
                    collapsed = childText;
                    kids = new List<AccessibilitySnapshotResult>();
                }
            }

            if (IsEditableRole(role) && kids.Count > 0 && kids.TrueForAll(kid => IsTextRole(kid.Role)))
            {
                List<string> parts = new List<string>();
                for (int i = 0; i < kids.Count; i++)
                {
                    string part = AriaSnapshotTemplateParser.NormalizeWhiteSpace(
                        kids[i].Name ?? kids[i].Value ?? string.Empty);
                    if (!string.IsNullOrEmpty(part))
                    {
                        parts.Add(part);
                    }
                }

                string joined = AriaSnapshotTemplateParser.NormalizeWhiteSpace(string.Join(" ", parts));
                if (!string.IsNullOrEmpty(node.Value)
                    && node.Value.Length >= joined.Length)
                {
                    joined = AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Value);
                }

                if (!string.IsNullOrEmpty(joined)
                    && !string.Equals(
                        AriaSnapshotTemplateParser.NormalizeWhiteSpace(name),
                        joined,
                        StringComparison.Ordinal))
                {
                    collapsed = joined;
                    kids = new List<AccessibilitySnapshotResult>();
                }
            }

            bool hasTextValue = HasCollapsedText(node, name, nameAsValue)
                || (!string.IsNullOrEmpty(collapsed)
                    && !nameAsValue
                    && kids.Count == 0
                    && !string.Equals(
                        AriaSnapshotTemplateParser.NormalizeWhiteSpace(name),
                        AriaSnapshotTemplateParser.NormalizeWhiteSpace(collapsed),
                        StringComparison.Ordinal));

            builder.Append(' ', indent * 2);
            builder.Append("- ");
            StringBuilder key = new StringBuilder(role);
            if (!nameAsValue && !string.IsNullOrEmpty(name))
            {
                key.Append(' ');
                key.Append(JsonString(name));
            }

            AppendExpectAttributes(key, node);
            string keyText = key.ToString();

            if (!hasUrl && !hasPlaceholder && kids.Count == 0 && !hasTextValue)
            {
                builder.Append(keyText);
                builder.AppendLine();
                return;
            }

            if (!hasUrl && !hasPlaceholder && kids.Count == 0 && hasTextValue)
            {
                builder.Append(keyText);
                builder.Append(": ");
                builder.Append(YamlEscapeValue(collapsed));
                builder.AppendLine();
                return;
            }

            builder.Append(keyText);
            builder.Append(':');
            builder.AppendLine();
            if (hasUrl)
            {
                builder.Append(' ', (indent + 1) * 2);
                builder.Append("- /url: ");
                builder.Append(YamlEscapeValue(node.Url));
                builder.AppendLine();
            }

            if (hasPlaceholder)
            {
                builder.Append(' ', (indent + 1) * 2);
                builder.Append("- /placeholder: ");
                builder.Append(YamlEscapeValue(node.Placeholder));
                builder.AppendLine();
            }

            if (hasTextValue && kids.Count == 0)
            {
                builder.Append(' ', (indent + 1) * 2);
                builder.Append("- text: ");
                builder.Append(YamlEscapeValue(collapsed));
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < kids.Count; i++)
            {
                WriteExpect(builder, kids[i], indent + 1);
            }
        }

        private static bool IsSkippedRole(string role)
        {
            return string.Equals(role, "ListMarker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "LineBreak", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Ignored", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "InlineTextBox", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UseNameAsValue(string role)
        {
            return string.Equals(role, "listitem", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "paragraph", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "group", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasCollapsedText(AccessibilitySnapshotResult node, string name, bool nameAsValue)
        {
            if (nameAsValue && !string.IsNullOrEmpty(name))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(node.Value) && IsEditableRole(node.Role)
                && !string.Equals(
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Value),
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(name ?? string.Empty),
                    StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static string CollapsedText(AccessibilitySnapshotResult node, string name, bool nameAsValue)
        {
            if (nameAsValue && !string.IsNullOrEmpty(name))
            {
                return name;
            }

            string value = node.Value ?? string.Empty;
            if (string.Equals(
                AriaSnapshotTemplateParser.NormalizeWhiteSpace(value),
                AriaSnapshotTemplateParser.NormalizeWhiteSpace(name ?? string.Empty),
                StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return value;
        }

        private static bool IsEditableRole(string role)
        {
            return string.Equals(role, "textbox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "searchbox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "combobox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "progressbar", StringComparison.OrdinalIgnoreCase);
        }

        private static List<AccessibilitySnapshotResult> VisibleExpectChildren(AccessibilitySnapshotResult node, string name)
        {
            List<AccessibilitySnapshotResult> kids = new List<AccessibilitySnapshotResult>();
            if (node.Children == null)
            {
                return kids;
            }

            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                if (child == null)
                {
                    continue;
                }

                if (IsSkippedRole(child.Role ?? string.Empty)
                    || IsExpectWrapper(child))
                {
                    if (IsExpectWrapper(child) && child.Children != null)
                    {
                        foreach (AccessibilitySnapshotResult nested in child.Children)
                        {
                            if (nested != null
                                && !IsSkippedRole(nested.Role ?? string.Empty)
                                && !IsExpectWrapper(nested))
                            {
                                kids.Add(nested);
                            }
                        }
                    }

                    continue;
                }

                if (IsTextRole(child.Role)
                    && string.Equals(
                        AriaSnapshotTemplateParser.NormalizeWhiteSpace(child.Name ?? string.Empty),
                        AriaSnapshotTemplateParser.NormalizeWhiteSpace(name),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                kids.Add(child);
            }

            return kids;
        }

        private static void AppendExpectAttributes(StringBuilder key, AccessibilitySnapshotResult node)
        {
            if (node.Checked == MixedState.Mixed)
            {
                key.Append(" [checked=mixed]");
            }
            else if (node.Checked == MixedState.On)
            {
                key.Append(" [checked]");
            }

            if (node.Disabled)
            {
                key.Append(" [disabled]");
            }

            if (node.Expanded)
            {
                key.Append(" [expanded]");
            }

            string invalid = AriaSnapshotMatcher.NormalizeInvalid(node.Invalid);
            if (string.Equals(invalid, "grammar", StringComparison.Ordinal)
                || string.Equals(invalid, "spelling", StringComparison.Ordinal))
            {
                key.Append(" [invalid=").Append(invalid).Append(']');
            }
            else if (string.Equals(invalid, "true", StringComparison.Ordinal))
            {
                key.Append(" [invalid]");
            }

            if (node.Level > 0
                && string.Equals(node.Role, "heading", StringComparison.OrdinalIgnoreCase))
            {
                key.Append(" [level=").Append(node.Level.ToString(CultureInfo.InvariantCulture)).Append(']');
            }

            if (node.Pressed == MixedState.Mixed)
            {
                key.Append(" [pressed=mixed]");
            }
            else if (node.Pressed == MixedState.On)
            {
                key.Append(" [pressed]");
            }

            if (node.Selected && IsSelectedExpectRole(role: NormalizeExpectRole(node.Role)))
            {
                key.Append(" [selected]");
            }
        }

        private static bool IsSelectedExpectRole(string role)
        {
            return string.Equals(role, "gridcell", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "option", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "row", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "tab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "rowheader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "columnheader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "treeitem", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTableRole(string role)
        {
            return string.Equals(role, "table", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "grid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "treegrid", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRowRole(string role)
        {
            return string.Equals(role, "row", StringComparison.OrdinalIgnoreCase);
        }

        private static List<AccessibilitySnapshotResult> WrapExpectRows(List<AccessibilitySnapshotResult> kids)
        {
            if (kids.Count == 0)
            {
                return kids;
            }

            bool hasRowgroup = false;
            List<AccessibilitySnapshotResult> rows = new List<AccessibilitySnapshotResult>();
            for (int i = 0; i < kids.Count; i++)
            {
                string role = NormalizeExpectRole(kids[i].Role);
                if (string.Equals(role, "rowgroup", StringComparison.OrdinalIgnoreCase))
                {
                    hasRowgroup = true;
                }

                if (IsRowRole(role))
                {
                    rows.Add(kids[i]);
                }
            }

            if (hasRowgroup || rows.Count != kids.Count)
            {
                return kids;
            }

            return new List<AccessibilitySnapshotResult>
            {
                new AccessibilitySnapshotResult
                {
                    Role = "rowgroup",
                    Children = rows,
                },
            };
        }

        private static string ConcatExpectName(AccessibilitySnapshotResult node)
        {
            if (node?.Children == null)
            {
                return AriaSnapshotTemplateParser.NormalizeWhiteSpace(node?.Name ?? string.Empty);
            }

            List<string> parts = new List<string>();
            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                if (child == null)
                {
                    continue;
                }

                string text = !string.IsNullOrEmpty(child.Name)
                    ? child.Name
                    : ConcatExpectName(child);
                text = AriaSnapshotTemplateParser.NormalizeWhiteSpace(text);
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
            }

            return AriaSnapshotTemplateParser.NormalizeWhiteSpace(string.Join(" ", parts));
        }

        private static string NormalizeExpectRole(string role)
        {
            if (string.IsNullOrEmpty(role))
            {
                return "generic";
            }

            if (string.Equals(role, "StaticText", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "InlineTextBox", StringComparison.OrdinalIgnoreCase))
            {
                return "text";
            }

            if (string.Equals(role, "LayoutTable", StringComparison.OrdinalIgnoreCase))
            {
                return "table";
            }

            if (string.Equals(role, "LayoutTableRow", StringComparison.OrdinalIgnoreCase))
            {
                return "row";
            }

            if (string.Equals(role, "LayoutTableCell", StringComparison.OrdinalIgnoreCase))
            {
                return "cell";
            }

            return role;
        }

        private static bool IsTextRole(string role)
        {
            return string.Equals(role, "StaticText", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "InlineTextBox", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "text", StringComparison.OrdinalIgnoreCase);
        }

        private static string JsonString(string value)
        {
            return "\"" + (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        private static string YamlEscapeValue(string value)
        {
            string text = value ?? string.Empty;
            if (text.Length == 0)
            {
                return "\"\"";
            }

            if (LooksLikeYamlNumber(text)
                || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase)
                || NeedsYamlQuotes(text)
                || char.IsWhiteSpace(text[0])
                || char.IsWhiteSpace(text[text.Length - 1]))
            {
                return JsonString(text);
            }

            return text;
        }

        private static bool NeedsYamlQuotes(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == ':' || ch == '#' || ch == '{' || ch == '}' || ch == '[' || ch == ']'
                    || ch == ',' || ch == '&' || ch == '*' || ch == '!' || ch == '|' || ch == '>'
                    || ch == '\'' || ch == '"' || ch == '%' || ch == '@' || ch == '`')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeYamlNumber(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static void Write(
            StringBuilder builder,
            AccessibilitySnapshotResult node,
            int indent,
            bool includeRefs,
            ref int nextRef,
            int level,
            int? maxDepth,
            IReadOnlyList<int[]> boxes,
            ref int nextBox,
            bool omitDescendantNames)
        {
            if (maxDepth != null && level > maxDepth.Value)
            {
                return;
            }

            builder.Append(' ', indent * 2);
            builder.Append("- ");
            string role = string.IsNullOrEmpty(node.Role) ? "generic" : node.Role;
            if (includeRefs && string.Equals(role, "iframe", System.StringComparison.OrdinalIgnoreCase))
            {
                role = "iframe";
            }

            builder.Append(role);
            if (!string.IsNullOrEmpty(node.Name) && !omitDescendantNames)
            {
                builder.Append(" \"");
                builder.Append(node.Name.Replace("\"", "\\\"", System.StringComparison.Ordinal));
                builder.Append('"');
            }

            if (node.Level > 0)
            {
                builder.Append(" [level=").Append(node.Level.ToString(CultureInfo.InvariantCulture)).Append(']');
            }

            if (includeRefs && node.Focused)
            {
                builder.Append(" [active]");
            }

            if (includeRefs)
            {
                builder.Append(" [ref=e").Append(nextRef.ToString(CultureInfo.InvariantCulture)).Append(']');
                nextRef++;
            }

            if (boxes != null && nextBox < boxes.Count)
            {
                int[] box = boxes[nextBox];
                nextBox++;
                builder.Append(" [box=")
                    .Append(box[0].ToString(CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(box[1].ToString(CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(box[2].ToString(CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(box[3].ToString(CultureInfo.InvariantCulture))
                    .Append(']');
            }

            builder.AppendLine();

            if (node.Children == null || (maxDepth != null && level == maxDepth.Value))
            {
                return;
            }

            foreach (AccessibilitySnapshotResult child in node.Children)
            {
                Write(builder, child, indent + 1, includeRefs, ref nextRef, level + 1, maxDepth, boxes, ref nextBox, omitDescendantNames);
            }
        }
    }
}
