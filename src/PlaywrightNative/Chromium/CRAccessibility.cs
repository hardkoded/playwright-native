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

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Chromium accessibility snapshot via <c>Accessibility.getFullAXTree</c>.
    /// Mirrors upstream <c>crAccessibility.ts</c>.
    /// </summary>
    internal static class CRAccessibility
    {
        /// <summary>
        /// Captures the page accessibility tree.
        /// </summary>
        /// <param name="session">The page CDP session.</param>
        /// <param name="interestingOnly">When omitted, defaults to <see langword="true"/>.</param>
        /// <param name="root">Optional DOM root handle.</param>
        /// <returns>The serialized tree, or <see langword="null"/>.</returns>
        internal static async Task<AccessibilitySnapshotResult> SnapshotAsync(
            CRSession session,
            bool? interestingOnly,
            IElementHandle root)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            await session.SendAsync("Accessibility.enable").ConfigureAwait(false);
            JsonElement? response = await session.SendAsync("Accessibility.getFullAXTree").ConfigureAwait(false);
            if (response == null
                || !response.Value.TryGetProperty("nodes", out JsonElement nodesEl)
                || nodesEl.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            CRAXNode tree = CRAXNode.CreateTree(nodesEl);
            if (tree == null)
            {
                return null;
            }

            CRAXNode needle = null;
            string objectId = TryGetObjectId(root);
            if (!string.IsNullOrEmpty(objectId))
            {
                JsonElement? described = await session.SendAsync("DOM.describeNode", new { objectId }).ConfigureAwait(false);
                if (described.HasValue
                    && described.Value.TryGetProperty("node", out JsonElement nodeEl)
                    && nodeEl.TryGetProperty("backendNodeId", out JsonElement backendEl)
                    && backendEl.ValueKind == JsonValueKind.Number)
                {
                    int backendId = backendEl.GetInt32();
                    needle = tree.Find(n => n.BackendDomNodeId == backendId);
                }
            }

            return AccessibilitySnapshotHelper.Snapshot(tree, needle, interestingOnly ?? true);
        }

        private static string TryGetObjectId(IElementHandle root)
        {
            if (root is ChromiumElementHandle instance)
            {
                return instance.ObjectId;
            }

            if (root is CRElementHandle handle)
            {
                return handle.ObjectId;
            }

            return null;
        }

        private sealed class CRAXNode : IAXNode
        {
            private readonly JsonElement _payload;
            private readonly List<CRAXNode> _children = new List<CRAXNode>();
            private readonly string _name;
            private readonly string _role;
            private readonly bool _richlyEditable;
            private readonly bool _editable;
            private readonly bool _focusable;
            private readonly bool _hidden;
            private bool? _cachedHasFocusableChild;

            private CRAXNode(JsonElement payload)
            {
                _payload = payload;
                _name = ReadAXValue(payload, "name") ?? string.Empty;
                _role = ReadAXValue(payload, "role") ?? "Unknown";

                if (!payload.TryGetProperty("properties", out JsonElement properties)
                    || properties.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                foreach (JsonElement property in properties.EnumerateArray())
                {
                    if (!property.TryGetProperty("name", out JsonElement nameEl)
                        || nameEl.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string propertyName = nameEl.GetString();
                    JsonElement value = ReadPropertyValue(property);
                    if (propertyName == "editable")
                    {
                        _richlyEditable = value.ValueKind == JsonValueKind.String
                            && string.Equals(value.GetString(), "richtext", StringComparison.Ordinal);
                        _editable = true;
                    }
                    else if (propertyName == "focusable")
                    {
                        _focusable = IsTruthy(value);
                    }
                    else if (propertyName == "hidden")
                    {
                        _hidden = IsTruthy(value);
                    }
                }
            }

            public IReadOnlyList<IAXNode> ChildNodes => _children.ConvertAll(c => (IAXNode)c);

            internal int? BackendDomNodeId
            {
                get
                {
                    if (_payload.TryGetProperty("backendDOMNodeId", out JsonElement idEl)
                        && idEl.ValueKind == JsonValueKind.Number)
                    {
                        return idEl.GetInt32();
                    }

                    return null;
                }
            }

            public bool IsInteresting(bool insideControl)
            {
                if (string.Equals(_role, "Ignored", StringComparison.Ordinal) || _hidden)
                {
                    return false;
                }

                if (_focusable || _richlyEditable)
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

                return IsLeafNode() && !string.IsNullOrEmpty(_name);
            }

            public bool IsLeafNode()
            {
                if (_children.Count == 0)
                {
                    return true;
                }

                if (IsPlainTextField() || IsTextOnlyObject())
                {
                    return true;
                }

                switch (_role)
                {
                    case "doc-cover":
                    case "graphics-symbol":
                    case "img":
                    case "Meter":
                    case "scrollbar":
                    case "slider":
                    case "separator":
                    case "progressbar":
                        return true;
                }

                if (HasFocusableChild())
                {
                    return false;
                }

                if (_focusable
                    && !string.Equals(_role, "WebArea", StringComparison.Ordinal)
                    && !string.Equals(_role, "RootWebArea", StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(_name))
                {
                    return true;
                }

                return string.Equals(_role, "heading", StringComparison.Ordinal) && !string.IsNullOrEmpty(_name);
            }

            public bool IsControl()
            {
                switch (_role)
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
                    case "tree":
                        return true;
                    default:
                        return false;
                }
            }

            public AccessibilitySnapshotResult Serialize()
            {
                Dictionary<string, JsonElement> properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                if (_payload.TryGetProperty("properties", out JsonElement propsEl)
                    && propsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement property in propsEl.EnumerateArray())
                    {
                        if (!property.TryGetProperty("name", out JsonElement nameEl)
                            || nameEl.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        properties[nameEl.GetString()] = ReadPropertyValue(property);
                    }
                }

                if (_payload.TryGetProperty("description", out JsonElement description)
                    && description.ValueKind == JsonValueKind.Object
                    && description.TryGetProperty("value", out JsonElement descriptionValue))
                {
                    properties["description"] = descriptionValue;
                }

                AccessibilitySnapshotResult node = new AccessibilitySnapshotResult
                {
                    Role = NormalizedRole(),
                    Name = ReadAXValue(_payload, "name") ?? string.Empty,
                };

                ApplyString(properties, "description", v => node.Description = v);
                ApplyString(properties, "keyshortcuts", v => node.Keyshortcuts = v);
                ApplyString(properties, "roledescription", v => node.Roledescription = v);
                ApplyString(properties, "valuetext", v => node.Valuetext = v);

                ApplyBool(properties, "disabled", v => node.Disabled = v);
                if (!string.Equals(_role, "WebArea", StringComparison.Ordinal)
                    && !string.Equals(_role, "RootWebArea", StringComparison.Ordinal))
                {
                    ApplyBool(properties, "focused", v => node.Focused = v);
                }

                ApplyBool(properties, "expanded", v => node.Expanded = v);
                ApplyBool(properties, "modal", v => node.Modal = v);
                ApplyBool(properties, "multiline", v => node.Multiline = v);
                ApplyBool(properties, "multiselectable", v => node.Multiselectable = v);
                ApplyBool(properties, "readonly", v => node.Readonly = v);
                ApplyBool(properties, "required", v => node.Required = v);
                ApplyBool(properties, "selected", v => node.Selected = v);

                ApplyInt(properties, "level", v => node.Level = v);
                ApplyFloat(properties, "valuemin", v => node.Valuemin = v);
                ApplyFloat(properties, "valuemax", v => node.Valuemax = v);

                ApplyToken(properties, "autocomplete", v => node.Autocomplete = v);
                ApplyToken(properties, "haspopup", v => node.Haspopup = v);
                ApplyToken(properties, "invalid", v => node.Invalid = v);
                ApplyToken(properties, "orientation", v => node.Orientation = v);

                if (_payload.TryGetProperty("value", out JsonElement valueEl)
                    && valueEl.ValueKind == JsonValueKind.Object
                    && valueEl.TryGetProperty("value", out JsonElement innerValue))
                {
                    if (innerValue.ValueKind == JsonValueKind.String)
                    {
                        node.Value = innerValue.GetString();
                    }
                    else if (innerValue.ValueKind == JsonValueKind.Number)
                    {
                        node.Value = innerValue.GetRawText();
                    }
                }

                if (properties.TryGetValue("checked", out JsonElement checkedEl))
                {
                    node.Checked = ToMixedState(checkedEl, MixedState.On, MixedState.Off);
                }

                if (properties.TryGetValue("pressed", out JsonElement pressedEl))
                {
                    node.Pressed = ToMixedState(pressedEl, MixedState.On, MixedState.Off);
                }

                return node;
            }

            internal static CRAXNode CreateTree(JsonElement nodes)
            {
                Dictionary<string, CRAXNode> byId = new Dictionary<string, CRAXNode>(StringComparer.Ordinal);
                List<CRAXNode> order = new List<CRAXNode>();
                foreach (JsonElement payload in nodes.EnumerateArray())
                {
                    if (!payload.TryGetProperty("nodeId", out JsonElement idEl)
                        || idEl.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    CRAXNode node = new CRAXNode(payload);
                    byId[idEl.GetString()] = node;
                    order.Add(node);
                }

                foreach (CRAXNode node in order)
                {
                    if (!node._payload.TryGetProperty("childIds", out JsonElement childIds)
                        || childIds.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement childId in childIds.EnumerateArray())
                    {
                        if (childId.ValueKind == JsonValueKind.String
                            && byId.TryGetValue(childId.GetString(), out CRAXNode child))
                        {
                            node._children.Add(child);
                        }
                    }
                }

                return order.Count == 0 ? null : order[0];
            }

            internal CRAXNode Find(Func<CRAXNode, bool> predicate)
            {
                if (predicate(this))
                {
                    return this;
                }

                foreach (CRAXNode child in _children)
                {
                    CRAXNode found = child.Find(predicate);
                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;
            }

            private static string ReadAXValue(JsonElement payload, string name)
            {
                if (!payload.TryGetProperty(name, out JsonElement value)
                    || value.ValueKind != JsonValueKind.Object
                    || !value.TryGetProperty("value", out JsonElement inner))
                {
                    return null;
                }

                return inner.ValueKind == JsonValueKind.String ? inner.GetString() : inner.ToString();
            }

            private static JsonElement ReadPropertyValue(JsonElement property)
            {
                if (property.TryGetProperty("value", out JsonElement wrapper)
                    && wrapper.ValueKind == JsonValueKind.Object
                    && wrapper.TryGetProperty("value", out JsonElement inner))
                {
                    return inner;
                }

                return default;
            }

            private static bool IsTruthy(JsonElement value)
                => value.ValueKind == JsonValueKind.True
                    || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), "true", StringComparison.Ordinal));

            private static void ApplyString(Dictionary<string, JsonElement> properties, string name, Action<string> assign)
            {
                if (properties.TryGetValue(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
                {
                    assign(value.GetString());
                }
            }

            private static void ApplyBool(Dictionary<string, JsonElement> properties, string name, Action<bool> assign)
            {
                if (properties.TryGetValue(name, out JsonElement value) && IsTruthy(value))
                {
                    assign(true);
                }
            }

            private static void ApplyInt(Dictionary<string, JsonElement> properties, string name, Action<int> assign)
            {
                if (properties.TryGetValue(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
                {
                    assign(value.GetInt32());
                }
            }

            private static void ApplyFloat(Dictionary<string, JsonElement> properties, string name, Action<float> assign)
            {
                if (properties.TryGetValue(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
                {
                    assign(value.GetSingle());
                }
            }

            private static void ApplyToken(Dictionary<string, JsonElement> properties, string name, Action<string> assign)
            {
                if (!properties.TryGetValue(name, out JsonElement value))
                {
                    return;
                }

                if (value.ValueKind == JsonValueKind.False)
                {
                    return;
                }

                string text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                if (string.IsNullOrEmpty(text) || string.Equals(text, "false", StringComparison.Ordinal))
                {
                    return;
                }

                assign(text);
            }

            private static MixedState ToMixedState(JsonElement value, MixedState on, MixedState off)
            {
                if (value.ValueKind == JsonValueKind.True
                    || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), "true", StringComparison.Ordinal)))
                {
                    return on;
                }

                if (value.ValueKind == JsonValueKind.False
                    || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), "false", StringComparison.Ordinal)))
                {
                    return off;
                }

                return MixedState.Mixed;
            }

            private string NormalizedRole()
            {
                if (string.Equals(_role, "RootWebArea", StringComparison.Ordinal))
                {
                    return "WebArea";
                }

                if (string.Equals(_role, "StaticText", StringComparison.Ordinal))
                {
                    return "text";
                }

                return _role;
            }

            private bool IsPlainTextField()
            {
                if (_richlyEditable)
                {
                    return false;
                }

                if (_editable)
                {
                    return true;
                }

                return string.Equals(_role, "textbox", StringComparison.Ordinal)
                    || string.Equals(_role, "ComboBox", StringComparison.Ordinal)
                    || string.Equals(_role, "searchbox", StringComparison.Ordinal);
            }

            private bool IsTextOnlyObject()
                => string.Equals(_role, "LineBreak", StringComparison.Ordinal)
                    || string.Equals(_role, "text", StringComparison.Ordinal)
                    || string.Equals(_role, "InlineTextBox", StringComparison.Ordinal)
                    || string.Equals(_role, "StaticText", StringComparison.Ordinal);

            private bool HasFocusableChild()
            {
                if (_cachedHasFocusableChild.HasValue)
                {
                    return _cachedHasFocusableChild.Value;
                }

                bool found = false;
                foreach (CRAXNode child in _children)
                {
                    if (child._focusable || child.HasFocusableChild())
                    {
                        found = true;
                        break;
                    }
                }

                _cachedHasFocusableChild = found;
                return found;
            }
        }
    }
}
