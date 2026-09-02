/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// WebKit accessibility snapshot via <c>DOM.getAccessibilityPropertiesForNode</c>.
    /// Playwright's patched <c>Page.accessibilitySnapshot</c> is gone in WebKit 2276.
    /// </summary>
    internal static class WKAccessibility
    {
        private static readonly Dictionary<string, string> RoleToAria = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TextField"] = "textbox",
            ["AXWebArea"] = "WebArea",
            ["document"] = "WebArea",
            ["HTML"] = "WebArea",
            ["html"] = "WebArea",
        };

        /// <summary>
        /// Captures the page accessibility tree.
        /// </summary>
        /// <param name="session">The page inner-target session.</param>
        /// <param name="interestingOnly">When omitted, defaults to <see langword="true"/>.</param>
        /// <param name="root">Optional DOM root handle.</param>
        /// <returns>The serialized tree, or <see langword="null"/>.</returns>
        internal static async Task<AccessibilitySnapshotResult> SnapshotAsync(
            WKTargetSession session,
            bool? interestingOnly,
            IElementHandle root)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            (int? rootNodeId, bool isDocumentRoot) = await ResolveRootNodeIdAsync(session, root).ConfigureAwait(false);
            if (rootNodeId == null)
            {
                return null;
            }

            Dictionary<int, WKAXNode> built = new Dictionary<int, WKAXNode>();
            WKAXNode tree = await BuildNodeAsync(session, rootNodeId.Value, built, 0).ConfigureAwait(false);
            if (tree == null)
            {
                return null;
            }

            if (isDocumentRoot)
            {
                tree.EnsureWebAreaRoot();
            }

            WKAXNode needle = null;
            if (root != null && isDocumentRoot)
            {
                needle = await FindNeedleAsync(tree, root).ConfigureAwait(false);
            }
            else if (root != null)
            {
                needle = tree;
            }

            return AccessibilitySnapshotHelper.Snapshot(tree, needle, interestingOnly ?? true);
        }

        private static async Task<(int? NodeId, bool IsDocumentRoot)> ResolveRootNodeIdAsync(WKTargetSession session, IElementHandle root)
        {
            JsonElement? document = await session.SendAsync("DOM.getDocument").ConfigureAwait(false);
            int? documentNodeId = null;
            if (document.HasValue
                && document.Value.TryGetProperty("root", out JsonElement rootEl)
                && rootEl.TryGetProperty("nodeId", out JsonElement rootId)
                && rootId.ValueKind == JsonValueKind.Number)
            {
                documentNodeId = rootId.GetInt32();
            }

            string objectId = (root as WKElementHandle)?.ObjectId;
            if (string.IsNullOrEmpty(objectId))
            {
                return (documentNodeId, true);
            }

            try
            {
                JsonElement? requested = await session.SendAsync("DOM.requestNode", new { objectId }).ConfigureAwait(false);
                if (requested.HasValue
                    && requested.Value.TryGetProperty("nodeId", out JsonElement idEl)
                    && idEl.ValueKind == JsonValueKind.Number)
                {
                    return (idEl.GetInt32(), false);
                }
            }
            catch (PlaywrightNativeException)
            {
                // WebKit 2276 often rejects requestNode; match the element in the document tree instead.
            }

            return (documentNodeId, true);
        }

        private static async Task<WKAXNode> FindNeedleAsync(WKAXNode tree, IElementHandle root)
        {
            string name = await root.EvaluateAsync<string>(
                "el => (el.getAttribute('aria-label') || el.innerText || el.textContent || '').trim()").ConfigureAwait(false);
            string tag = await root.EvaluateAsync<string>("el => (el.tagName || '').toLowerCase()").ConfigureAwait(false);
            WKAXNode byName = tree.Find(node =>
                !string.IsNullOrEmpty(name) && string.Equals(node.GetAccessibleName(), name, StringComparison.Ordinal));
            if (byName != null)
            {
                return byName;
            }

            return tree.Find(node => string.Equals(node.GetNormalizedRole(), tag, StringComparison.Ordinal));
        }

        private static async Task<WKAXNode> BuildNodeAsync(
            WKTargetSession session,
            int nodeId,
            Dictionary<int, WKAXNode> built,
            int depth)
        {
            if (built.TryGetValue(nodeId, out WKAXNode existing))
            {
                return existing;
            }

            if (built.Count > 250 || depth > 24)
            {
                return null;
            }

            JsonElement? response = await session.SendAsync(
                "DOM.getAccessibilityPropertiesForNode",
                new { nodeId }).ConfigureAwait(false);
            if (response == null
                || !response.Value.TryGetProperty("properties", out JsonElement properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            WKAXNode node = new WKAXNode(properties);
            built[nodeId] = node;

            if (node.IsNonContentSubtree())
            {
                return node;
            }

            if (!properties.TryGetProperty("childNodeIds", out JsonElement children)
                || children.ValueKind != JsonValueKind.Array)
            {
                return node;
            }

            foreach (JsonElement childId in children.EnumerateArray())
            {
                if (childId.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                if (built.Count > 250)
                {
                    break;
                }

                WKAXNode child = await BuildNodeAsync(session, childId.GetInt32(), built, depth + 1).ConfigureAwait(false);
                if (child != null && !child.IsNonContentSubtree())
                {
                    node.AddChild(child);
                }
            }

            return node;
        }

        private sealed class WKAXNode : IAXNode
        {
            private readonly JsonElement _payload;
            private readonly List<WKAXNode> _children = new List<WKAXNode>();
            private string _forcedRole;

            internal WKAXNode(JsonElement payload)
            {
                _payload = payload;
            }

            public IReadOnlyList<IAXNode> ChildNodes => _children.ConvertAll(c => (IAXNode)c);

            public bool IsInteresting(bool insideControl)
            {
                if (IsNonContentSubtree() || IsIgnored())
                {
                    return false;
                }

                if (_payload.TryGetProperty("exists", out JsonElement exists)
                    && exists.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                string role = NormalizedRole();
                if (string.Equals(role, "ScrollArea", StringComparison.Ordinal))
                {
                    return false;
                }

                if (string.Equals(role, "WebArea", StringComparison.Ordinal))
                {
                    return true;
                }

                if (ReadBool("focused") || string.Equals(role, "MenuListOption", StringComparison.Ordinal))
                {
                    return true;
                }

                if (IsControl())
                {
                    return true;
                }

                if (insideControl)
                {
                    return false;
                }

                return IsLeafNode() && !string.IsNullOrEmpty(Name());
            }

            public bool IsLeafNode()
            {
                if (_children.Count == 0)
                {
                    return true;
                }

                return IsTextControl() || HasRedundantTextChild();
            }

            public bool IsControl()
            {
                switch (NormalizedRole())
                {
                    case "button":
                    case "checkbox":
                    case "ColorWell":
                    case "combobox":
                    case "DisclosureTriangle":
                    case "listbox":
                    case "menu":
                    case "menubar":
                    case "menuitem":
                    case "menuitemcheckbox":
                    case "menuitemradio":
                    case "radio":
                    case "scrollbar":
                    case "searchbox":
                    case "slider":
                    case "spinbutton":
                    case "switch":
                    case "tab":
                    case "textbox":
                    case "TextField":
                    case "tree":
                        return true;
                    default:
                        return false;
                }
            }

            public AccessibilitySnapshotResult Serialize()
            {
                AccessibilitySnapshotResult node = new AccessibilitySnapshotResult
                {
                    Role = NormalizedRole(),
                    Name = Name(),
                };

                ApplyBool("disabled", v => node.Disabled = v);
                string role = NormalizedRole();
                if (!string.Equals(role, "WebArea", StringComparison.Ordinal)
                    && !string.Equals(role, "ScrollArea", StringComparison.Ordinal))
                {
                    ApplyBool("focused", v => node.Focused = v);
                }

                ApplyBool("expanded", v => node.Expanded = v);
                ApplyBool("readonly", v => node.Readonly = v);
                ApplyBool("required", v => node.Required = v);
                ApplyBool("selected", v => node.Selected = v);

                if (HasNumber("headingLevel", out int level))
                {
                    node.Level = level;
                }

                if (HasString("checked", out string checkedValue))
                {
                    node.Checked = ToMixed(checkedValue);
                }

                if (_payload.TryGetProperty("pressed", out JsonElement pressed))
                {
                    if (pressed.ValueKind == JsonValueKind.True)
                    {
                        node.Pressed = MixedState.On;
                    }
                    else if (pressed.ValueKind == JsonValueKind.False)
                    {
                        node.Pressed = MixedState.Off;
                    }
                    else if (pressed.ValueKind == JsonValueKind.String)
                    {
                        string pressedValue = pressed.GetString();
                        if (string.Equals(pressedValue, "mixed", StringComparison.OrdinalIgnoreCase))
                        {
                            node.Pressed = MixedState.Mixed;
                        }
                        else if (string.Equals(pressedValue, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            node.Pressed = MixedState.On;
                        }
                    }
                }

                if (HasString("pressed", out string pressedToken)
                    && node.Pressed == MixedState.Undefined)
                {
                    if (string.Equals(pressedToken, "mixed", StringComparison.OrdinalIgnoreCase))
                    {
                        node.Pressed = MixedState.Mixed;
                    }
                    else if (string.Equals(pressedToken, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        node.Pressed = MixedState.On;
                    }
                }

                if (HasString("invalid", out string invalid)
                    && !string.Equals(invalid, "false", StringComparison.Ordinal))
                {
                    node.Invalid = invalid;
                }

                return node;
            }

            internal bool IsIgnored()
            {
                if (ReadBool("ignored") || ReadBool("hidden") || ReadBool("ignoredByDefault"))
                {
                    return true;
                }

                if (_payload.TryGetProperty("exists", out JsonElement existsFlag)
                    && existsFlag.ValueKind == JsonValueKind.False)
                {
                    return true;
                }

                return IsNonContentSubtree();
            }

            internal bool IsNonContentSubtree()
            {
                string role = NormalizedRole();
                return string.Equals(role, "ScrollArea", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Head", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Script", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Style", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Meta", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Noscript", StringComparison.OrdinalIgnoreCase);
            }

            internal string GetAccessibleName() => Name();

            internal string GetNormalizedRole() => NormalizedRole();

            internal void AddChild(WKAXNode child) => _children.Add(child);

            internal void EnsureWebAreaRoot() => _forcedRole = "WebArea";

            internal WKAXNode Find(Func<WKAXNode, bool> predicate)
            {
                if (predicate(this))
                {
                    return this;
                }

                foreach (WKAXNode child in _children)
                {
                    WKAXNode found = child.Find(predicate);
                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;
            }

            private static MixedState ToMixed(string value)
            {
                if (string.Equals(value, "true", StringComparison.Ordinal))
                {
                    return MixedState.On;
                }

                if (string.Equals(value, "false", StringComparison.Ordinal))
                {
                    return MixedState.Off;
                }

                return MixedState.Mixed;
            }

            private string NormalizedRole()
            {
                if (!string.IsNullOrEmpty(_forcedRole))
                {
                    return _forcedRole;
                }

                string role = ReadString("role") ?? string.Empty;
                if (role.StartsWith("AX", StringComparison.Ordinal) && role.Length > 2)
                {
                    role = role.Substring(2);
                }

                if (RoleToAria.TryGetValue(role, out string mapped))
                {
                    return mapped;
                }

                if (string.Equals(role, "WebArea", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "document", StringComparison.OrdinalIgnoreCase))
                {
                    return "WebArea";
                }

                if (role.Length > 0)
                {
                    return char.ToLowerInvariant(role[0]) + role.Substring(1);
                }

                return role;
            }

            private string Name()
            {
                if (HasString("label", out string label))
                {
                    return label;
                }

                return ReadString("name") ?? string.Empty;
            }

            private bool IsTextControl()
            {
                switch (NormalizedRole())
                {
                    case "combobox":
                    case "searchfield":
                    case "textbox":
                    case "TextField":
                        return true;
                    default:
                        return false;
                }
            }

            private bool HasRedundantTextChild()
            {
                if (_children.Count != 1)
                {
                    return false;
                }

                WKAXNode child = _children[0];
                return string.Equals(child.NormalizedRole(), "text", StringComparison.Ordinal)
                    && string.Equals(Name(), child.Name(), StringComparison.Ordinal);
            }

            private string ReadString(string name)
                => HasString(name, out string value) ? value : null;

            private bool HasString(string name, out string value)
            {
                value = null;
                if (!_payload.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                value = el.GetString();
                return true;
            }

            private bool HasNumber(string name, out int value)
            {
                value = 0;
                if (!_payload.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.Number)
                {
                    return false;
                }

                value = el.GetInt32();
                return true;
            }

            private bool ReadBool(string name)
            {
                return _payload.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.True;
            }

            private void ApplyBool(string name, Action<bool> assign)
            {
                if (ReadBool(name))
                {
                    assign(true);
                }
            }
        }
    }
}
