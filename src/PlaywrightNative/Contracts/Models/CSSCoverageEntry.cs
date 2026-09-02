/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightNative
{
    /// <summary>
    /// Coverage for a single stylesheet.
    /// </summary>
    public sealed class CSSCoverageEntry
    {
        /// <summary>
        /// Stylesheet URL, or the empty string for inline sheets.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Stylesheet text when it could be retrieved.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Used ranges inside <see cref="Text"/>.
        /// </summary>
        public IReadOnlyList<CSSCoverageRange> Ranges { get; set; }
    }
}
