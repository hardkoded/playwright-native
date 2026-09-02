// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Matches an accessibility tree against an official aria snapshot template.
    /// </summary>
    internal static class AriaSnapshotMatcher
    {
        internal static bool Matches(AccessibilitySnapshotResult root, AriaSnapshotTemplate template)
        {
            if (root == null || template == null)
            {
                return false;
            }

            List<AriaNode> nodes = Flatten(root);
            return MatchesDeep(nodes, template);
        }

        internal static string FormatReceived(AccessibilitySnapshotResult root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            AccessibilitySnapshotResult printable = Unwrap(root);
            return AriaSnapshotYaml.FormatForExpect(printable);
        }

        internal static string NormalizeInvalid(string actual)
        {
            if (string.IsNullOrEmpty(actual)
                || string.Equals(actual, "false", StringComparison.OrdinalIgnoreCase))
            {
                return "false";
            }

            if (string.Equals(actual, "grammar", StringComparison.OrdinalIgnoreCase))
            {
                return "grammar";
            }

            if (string.Equals(actual, "spelling", StringComparison.OrdinalIgnoreCase))
            {
                return "spelling";
            }

            return "true";
        }

        private static bool MatchesDeep(IReadOnlyList<AriaNode> nodes, AriaSnapshotTemplate template)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (MatchesNode(nodes[i], template, isDeepEqual: false))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesNode(AriaNode node, AriaSnapshotTemplate template, bool isDeepEqual)
        {
            if (template.Kind == "text")
            {
                return MatchesText(node.Name, template.Text, template.TextRegex)
                    || MatchesText(node.Value, template.Text, template.TextRegex)
                    || MatchesText(ConcatText(node), template.Text, template.TextRegex);
            }

            if (!string.Equals(template.Role, "fragment", StringComparison.Ordinal)
                && !string.Equals(template.Role, node.Role, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (template.NameRegex != null && !template.NameRegex.IsMatch(node.Name ?? string.Empty))
            {
                return false;
            }

            if (template.NameRegex == null
                && !string.IsNullOrEmpty(template.Name)
                && !string.Equals(
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty),
                    template.Name,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (template.Checked != null && !MatchesTriState(node.Checked, template.Checked))
            {
                return false;
            }

            if (template.Disabled != null && node.Disabled != template.Disabled.Value)
            {
                return false;
            }

            if (template.Expanded != null && node.Expanded != template.Expanded.Value)
            {
                return false;
            }

            if (template.Level != null && node.Level != template.Level.Value)
            {
                return false;
            }

            if (template.Pressed != null && !MatchesTriState(node.Pressed, template.Pressed))
            {
                return false;
            }

            if (template.Selected != null && node.Selected != template.Selected.Value)
            {
                return false;
            }

            if (template.Invalid != null && !MatchesInvalid(node.Invalid, template.Invalid))
            {
                return false;
            }

            if (template.UrlRegex != null && !template.UrlRegex.IsMatch(node.Url ?? string.Empty))
            {
                return false;
            }

            if (template.UrlRegex == null
                && template.Url != null
                && !MatchesText(node.Url, template.Url, null))
            {
                return false;
            }

            if (template.PlaceholderRegex != null && !template.PlaceholderRegex.IsMatch(node.Placeholder ?? string.Empty))
            {
                return false;
            }

            if (template.PlaceholderRegex == null
                && template.Placeholder != null
                && !MatchesText(node.Placeholder, template.Placeholder, null))
            {
                return false;
            }

            IReadOnlyList<AriaNode> children = EffectiveChildren(node);
            string mode = template.ContainerMode;
            bool deep = isDeepEqual || string.Equals(mode, "deep-equal", StringComparison.Ordinal);
            if (template.Children.Count == 1
                && string.Equals(template.Children[0].Kind, "text", StringComparison.Ordinal)
                && MatchesText(ConcatText(node), template.Children[0].Text, template.Children[0].TextRegex)
                && (string.IsNullOrEmpty(mode) || string.Equals(mode, "contain", StringComparison.Ordinal))
                && !string.Equals(mode, "deep-equal", StringComparison.Ordinal)
                && !string.Equals(mode, "equal", StringComparison.Ordinal)
                && (!deep || string.Equals(mode, "contain", StringComparison.Ordinal)))
            {
                return true;
            }

            if (string.Equals(mode, "contain", StringComparison.Ordinal))
            {
                return ContainsList(children, template.Children);
            }

            if (string.Equals(mode, "equal", StringComparison.Ordinal))
            {
                return ListEqual(children, template.Children, deep: false);
            }

            if (deep)
            {
                return ListEqual(children, template.Children, deep: true);
            }

            return ContainsList(children, template.Children);
        }

        private static IReadOnlyList<AriaNode> EffectiveChildren(AriaNode node)
        {
            if (string.Equals(node.Role, "table", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Role, "grid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Role, "treegrid", StringComparison.OrdinalIgnoreCase))
            {
                if (node.Children.Count > 0
                    && node.Children.TrueForAll(child => string.Equals(child.Role, "row", StringComparison.OrdinalIgnoreCase)))
                {
                    AriaNode group = new AriaNode { Role = "rowgroup" };
                    group.Children.AddRange(node.Children);
                    return new[] { group };
                }
            }

            if (node.Children.Count > 0)
            {
                return node.Children;
            }

            if (!string.IsNullOrEmpty(node.Value)
                && !string.Equals(
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Value),
                    AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty),
                    StringComparison.Ordinal))
            {
                return new[]
                {
                    new AriaNode
                    {
                        Role = "text",
                        Name = node.Value,
                        Value = node.Value,
                    },
                };
            }

            if (!string.IsNullOrEmpty(node.Name) || !string.IsNullOrEmpty(node.Value))
            {
                return new[]
                {
                    new AriaNode
                    {
                        Role = "text",
                        Name = string.IsNullOrEmpty(node.Value) ? node.Name : node.Value,
                        Value = node.Value ?? string.Empty,
                    },
                };
            }

            return node.Children;
        }

        private static string ConcatText(AriaNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (node.Children.Count == 0)
            {
                if (!string.IsNullOrEmpty(node.Value))
                {
                    return AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Value);
                }

                return AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty);
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(node.Value))
            {
                parts.Add(node.Value);
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                string child = ConcatText(node.Children[i]);
                if (!string.IsNullOrEmpty(child))
                {
                    parts.Add(child);
                }
            }

            if (parts.Count == 0)
            {
                return AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty);
            }

            return AriaSnapshotTemplateParser.NormalizeWhiteSpace(string.Join(" ", parts));
        }

        private static bool ContainsList(IReadOnlyList<AriaNode> children, IReadOnlyList<AriaSnapshotTemplate> template)
        {
            if (template.Count > children.Count)
            {
                return false;
            }

            int index = 0;
            for (int t = 0; t < template.Count; t++)
            {
                bool found = false;
                while (index < children.Count)
                {
                    if (MatchesNode(children[index], template[t], isDeepEqual: false))
                    {
                        found = true;
                        index++;
                        break;
                    }

                    index++;
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ListEqual(IReadOnlyList<AriaNode> children, IReadOnlyList<AriaSnapshotTemplate> template, bool deep)
        {
            if (template.Count != children.Count)
            {
                return false;
            }

            for (int i = 0; i < template.Count; i++)
            {
                if (!MatchesNode(children[i], template[i], deep))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesTriState(MixedState actual, string expected)
        {
            if (string.Equals(expected, "mixed", StringComparison.Ordinal))
            {
                return actual == MixedState.Mixed;
            }

            if (string.Equals(expected, "true", StringComparison.Ordinal))
            {
                return actual == MixedState.On;
            }

            return actual == MixedState.Off || actual == MixedState.Undefined;
        }

        private static bool MatchesInvalid(string actual, string expected)
        {
            string normalized = NormalizeInvalid(actual);
            if (string.Equals(expected, "true", StringComparison.Ordinal))
            {
                return string.Equals(normalized, "true", StringComparison.Ordinal);
            }

            if (string.Equals(expected, "false", StringComparison.Ordinal))
            {
                return string.IsNullOrEmpty(normalized) || string.Equals(normalized, "false", StringComparison.Ordinal);
            }

            return string.Equals(normalized, expected, StringComparison.Ordinal);
        }

        private static bool MatchesText(string actual, string expected, Regex regex)
        {
            string value = AriaSnapshotTemplateParser.NormalizeWhiteSpace(actual ?? string.Empty);
            if (regex != null)
            {
                return regex.IsMatch(value);
            }

            if (expected == null)
            {
                return true;
            }

            return string.Equals(value, AriaSnapshotTemplateParser.NormalizeWhiteSpace(expected), StringComparison.Ordinal);
        }

        private static AccessibilitySnapshotResult Unwrap(AccessibilitySnapshotResult root)
        {
            AccessibilitySnapshotResult current = root;
            while (current != null
                && IsWrapper(current.Role)
                && current.Children != null
                && current.Children.Count() == 1)
            {
                current = current.Children.First();
            }

            return current;
        }

        private static bool IsWrapper(string role)
        {
            return string.Equals(role, "WebArea", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "document", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "generic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "presentation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Iframe", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(role);
        }

        private static List<AriaNode> Flatten(AccessibilitySnapshotResult root)
        {
            AriaNode converted = Convert(root, url: null);
            List<AriaNode> all = new List<AriaNode>();
            Collect(converted, all);
            return all;
        }

        private static void Collect(AriaNode node, List<AriaNode> all)
        {
            all.Add(node);
            for (int i = 0; i < node.Children.Count; i++)
            {
                Collect(node.Children[i], all);
            }
        }

        private static AriaNode Convert(AccessibilitySnapshotResult node, string url)
        {
            string role = string.IsNullOrEmpty(node.Role) ? "generic" : node.Role;
            if (string.Equals(role, "StaticText", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "InlineTextBox", StringComparison.OrdinalIgnoreCase))
            {
                role = "text";
            }

            if (string.Equals(role, "LayoutTable", StringComparison.OrdinalIgnoreCase))
            {
                role = "table";
            }
            else if (string.Equals(role, "LayoutTableRow", StringComparison.OrdinalIgnoreCase))
            {
                role = "row";
            }
            else if (string.Equals(role, "LayoutTableCell", StringComparison.OrdinalIgnoreCase))
            {
                role = "cell";
            }

            AriaNode result = new AriaNode
            {
                Role = role,
                Name = node.Name ?? string.Empty,
                Value = node.Value ?? node.Valuetext ?? string.Empty,
                Checked = node.Checked,
                Disabled = node.Disabled,
                Expanded = node.Expanded,
                Level = node.Level,
                Pressed = node.Pressed,
                Selected = node.Selected,
                Invalid = node.Invalid,
                Url = !string.IsNullOrEmpty(node.Url) ? node.Url : url ?? string.Empty,
                Placeholder = node.Placeholder ?? string.Empty,
            };

            if (node.Children != null)
            {
                foreach (AccessibilitySnapshotResult child in node.Children)
                {
                    if (child == null
                        || string.Equals(child.Role, "ListMarker", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(child.Role, "LineBreak", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(child.Role, "Ignored", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(child.Role, "InlineTextBox", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AriaNode converted = Convert(child, url: null);
                    if (string.Equals(converted.Role, "text", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(converted.Name)
                        && string.IsNullOrWhiteSpace(converted.Value)
                        && converted.Children.Count == 0)
                    {
                        continue;
                    }

                    if ((string.Equals(converted.Role, "generic", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(converted.Role, "none", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(converted.Role, "presentation", StringComparison.OrdinalIgnoreCase))
                        && string.IsNullOrEmpty(converted.Name))
                    {
                        result.Children.AddRange(converted.Children);
                        continue;
                    }

                    result.Children.Add(converted);
                }
            }

            if (string.IsNullOrEmpty(result.Name) && result.Children.Count > 0)
            {
                result.Name = ConcatText(result);
            }

            if (!IsSelectedRole(result.Role))
            {
                result.Selected = false;
            }

            CollapseWidgetText(result);

            if ((string.Equals(result.Role, "table", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(result.Role, "grid", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(result.Role, "treegrid", StringComparison.OrdinalIgnoreCase))
                && result.Children.Count > 0
                && result.Children.TrueForAll(child => string.Equals(child.Role, "row", StringComparison.OrdinalIgnoreCase)))
            {
                AriaNode group = new AriaNode { Role = "rowgroup" };
                group.Children.AddRange(result.Children);
                result.Children.Clear();
                result.Children.Add(group);
            }

            return result;
        }

        private static void CollapseWidgetText(AriaNode node)
        {
            if (!string.Equals(node.Role, "progressbar", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(node.Role, "textbox", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(node.Role, "searchbox", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string text = !string.IsNullOrEmpty(node.Value) ? node.Value : ConcatText(node);
            text = AriaSnapshotTemplateParser.NormalizeWhiteSpace(text);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string name = AriaSnapshotTemplateParser.NormalizeWhiteSpace(node.Name ?? string.Empty);
            if (string.Equals(name, text, StringComparison.Ordinal))
            {
                node.Children.Clear();
                return;
            }

            node.Value = text;
            node.Children.Clear();
            node.Children.Add(new AriaNode
            {
                Role = "text",
                Name = text,
                Value = text,
            });
        }

        private static bool IsSelectedRole(string role)
        {
            return string.Equals(role, "gridcell", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "option", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "row", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "tab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "rowheader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "columnheader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "treeitem", StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class AriaNode
        {
            internal string Role { get; set; }

            internal string Name { get; set; }

            internal string Value { get; set; }

            internal MixedState Checked { get; set; }

            internal bool Disabled { get; set; }

            internal bool Expanded { get; set; }

            internal int Level { get; set; }

            internal MixedState Pressed { get; set; }

            internal bool Selected { get; set; }

            internal string Invalid { get; set; }

            internal string Url { get; set; }

            internal string Placeholder { get; set; }

            internal List<AriaNode> Children { get; } = new List<AriaNode>();
        }
    }
}
