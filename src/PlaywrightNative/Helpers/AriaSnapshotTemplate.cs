// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// One official <c>toMatchAriaSnapshot</c> template node.
    /// </summary>
    internal sealed class AriaSnapshotTemplate
    {
        internal string Kind { get; set; } = "role";

        internal string Role { get; set; } = "fragment";

        internal string Name { get; set; } = string.Empty;

        internal Regex NameRegex { get; set; }

        internal string Checked { get; set; }

        internal bool? Disabled { get; set; }

        internal bool? Expanded { get; set; }

        internal int? Level { get; set; }

        internal string Pressed { get; set; }

        internal bool? Selected { get; set; }

        internal string Invalid { get; set; }

        internal string ContainerMode { get; set; }

        internal string Url { get; set; }

        internal Regex UrlRegex { get; set; }

        internal string Placeholder { get; set; }

        internal Regex PlaceholderRegex { get; set; }

        internal string Text { get; set; }

        internal Regex TextRegex { get; set; }

        internal List<AriaSnapshotTemplate> Children { get; } = new List<AriaSnapshotTemplate>();
    }
}
