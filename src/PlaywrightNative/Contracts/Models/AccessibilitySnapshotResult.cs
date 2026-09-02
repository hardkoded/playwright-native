/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace PlaywrightNative
{
    /// <summary>
    /// Internal accessibility tree node used by aria snapshots and expect matchers.
    /// </summary>
    internal class AccessibilitySnapshotResult : IEquatable<AccessibilitySnapshotResult>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccessibilitySnapshotResult"/> class.
        /// </summary>
        public AccessibilitySnapshotResult()
        {
            Children = Array.Empty<AccessibilitySnapshotResult>();
        }

        /// <summary><para>The <a href="https://www.w3.org/TR/wai-aria/#usage_intro)">role</a>.</para></summary>
        [JsonPropertyName("role")]
        public string Role { get; set; }

        /// <summary><para>A human readable name for the node.</para></summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary><para>The current value of the node, if applicable.</para></summary>
        [JsonPropertyName("valueString")]
        public string Value { get; set; }

        /// <summary><para>An additional human readable description of the node, if applicable.</para></summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary><para>Keyboard shortcuts associated with this node, if applicable.</para></summary>
        [JsonPropertyName("keyshortcuts")]
        public string Keyshortcuts { get; set; }

        /// <summary><para>A human readable alternative to the role, if applicable.</para></summary>
        [JsonPropertyName("roledescription")]
        public string Roledescription { get; set; }

        /// <summary><para>A description of the current value, if applicable.</para></summary>
        [JsonPropertyName("valuetext")]
        public string Valuetext { get; set; }

        /// <summary><para>Whether the node is disabled, if applicable.</para></summary>
        [JsonPropertyName("disabled")]
        public bool Disabled { get; set; }

        /// <summary><para>Whether the node is expanded or collapsed, if applicable.</para></summary>
        [JsonPropertyName("expanded")]
        public bool Expanded { get; set; }

        /// <summary><para>Whether the node is focused, if applicable.</para></summary>
        [JsonPropertyName("focused")]
        public bool Focused { get; set; }

        /// <summary>
        /// <para>
        /// Whether the node is <a href="https://en.wikipedia.org/wiki/Modal_window)">modal</a>,
        /// if applicable.
        /// </para>
        /// </summary>
        [JsonPropertyName("modal")]
        public bool Modal { get; set; }

        /// <summary><para>Whether the node text input supports multiline, if applicable.</para></summary>
        [JsonPropertyName("multiline")]
        public bool Multiline { get; set; }

        /// <summary><para>Whether more than one child can be selected, if applicable.</para></summary>
        [JsonPropertyName("multiselectable")]
        public bool Multiselectable { get; set; }

        /// <summary><para>Whether the node is read only, if applicable.</para></summary>
        [JsonPropertyName("readonly")]
        public bool Readonly { get; set; }

        /// <summary><para>Whether the node is required, if applicable.</para></summary>
        [JsonPropertyName("required")]
        public bool Required { get; set; }

        /// <summary><para>Whether the node is selected in its parent node, if applicable.</para></summary>
        [JsonPropertyName("selected")]
        public bool Selected { get; set; }

        /// <summary><para>Whether the checkbox is checked, or "mixed", if applicable.</para></summary>
        [JsonPropertyName("checked")]
        public MixedState Checked { get; set; }

        /// <summary><para>Whether the toggle button is checked, or "mixed", if applicable.</para></summary>
        [JsonPropertyName("pressed")]
        public MixedState Pressed { get; set; }

        /// <summary><para>The level of a heading, if applicable.</para></summary>
        [JsonPropertyName("level")]
        public int Level { get; set; }

        /// <summary><para>The minimum value in a node, if applicable.</para></summary>
        [JsonPropertyName("valuemin")]
        public float Valuemin { get; set; }

        /// <summary><para>The maximum value in a node, if applicable.</para></summary>
        [JsonPropertyName("valuemax")]
        public float Valuemax { get; set; }

        /// <summary><para>What kind of autocomplete is supported by a control, if applicable.</para></summary>
        [JsonPropertyName("autocomplete")]
        public string Autocomplete { get; set; }

        /// <summary><para>What kind of popup is currently being shown for a node, if applicable.</para></summary>
        [JsonPropertyName("haspopup")]
        public string Haspopup { get; set; }

        /// <summary><para>Whether and in what way this node's value is invalid, if applicable.</para></summary>
        [JsonPropertyName("invalid")]
        public string Invalid { get; set; }

        /// <summary><para>Whether the node is oriented horizontally or vertically, if applicable.</para></summary>
        [JsonPropertyName("orientation")]
        public string Orientation { get; set; }

        /// <summary><para>Child nodes, if any, if applicable.</para></summary>
        [JsonPropertyName("children")]
        public IEnumerable<AccessibilitySnapshotResult> Children { get; set; }

        /// <summary>
        /// Optional link target from the DOM <c>href</c> attribute. CDP AX
        /// nodes do not expose this; expect matching fills it in.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Official <c>/placeholder</c> when it differs from the accessible name.
        /// </summary>
        public string Placeholder { get; set; }

        /// <inheritdoc/>
        public bool Equals(AccessibilitySnapshotResult other)
            => other != null &&
            (ReferenceEquals(this, other) || (
            Role == other.Role &&
            Name == other.Name &&
            Value == other.Value &&
            Description == other.Description &&
            Keyshortcuts == other.Keyshortcuts &&
            Roledescription == other.Roledescription &&
            Valuetext == other.Valuetext &&
            Disabled == other.Disabled &&
            Expanded == other.Expanded &&
            Focused == other.Focused &&
            Modal == other.Modal &&
            Multiline == other.Multiline &&
            Multiselectable == other.Multiselectable &&
            Readonly == other.Readonly &&
            Required == other.Required &&
            Selected == other.Selected &&
            Checked == other.Checked &&
            Pressed == other.Pressed &&
            Level == other.Level &&
            Valuemin == other.Valuemin &&
            Valuemax == other.Valuemax &&
            Autocomplete == other.Autocomplete &&
            Haspopup == other.Haspopup &&
            Invalid == other.Invalid &&
            Orientation == other.Orientation &&
            (Children == other.Children || Children.SequenceEqual(other.Children))));

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is AccessibilitySnapshotResult && base.Equals(obj);

        /// <inheritdoc/>
        public override int GetHashCode()
            => (Role ?? string.Empty).GetHashCode() ^
                (Name ?? string.Empty).GetHashCode() ^
                (Value ?? string.Empty).GetHashCode() ^
                (Description ?? string.Empty).GetHashCode() ^
                (Keyshortcuts ?? string.Empty).GetHashCode() ^
                (Roledescription ?? string.Empty).GetHashCode() ^
                (Valuetext ?? string.Empty).GetHashCode() ^
                Disabled.GetHashCode() ^
                Expanded.GetHashCode() ^
                Focused.GetHashCode() ^
                Modal.GetHashCode() ^
                Multiline.GetHashCode() ^
                Multiselectable.GetHashCode() ^
                Readonly.GetHashCode() ^
                Required.GetHashCode() ^
                Selected.GetHashCode() ^
                Checked.GetHashCode() ^
                Pressed.GetHashCode() ^
                Level.GetHashCode() ^
                Valuemin.GetHashCode() ^
                Valuemax.GetHashCode() ^
                (Autocomplete ?? string.Empty).GetHashCode() ^
                (Haspopup ?? string.Empty).GetHashCode() ^
                (Invalid ?? string.Empty).GetHashCode() ^
                (Orientation ?? string.Empty).GetHashCode() ^
                Children.GetHashCode();
    }
}
