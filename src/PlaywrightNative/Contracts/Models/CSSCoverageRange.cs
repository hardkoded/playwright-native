/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative
{
    /// <summary>
    /// A used span inside a stylesheet.
    /// </summary>
    public sealed class CSSCoverageRange
    {
        /// <summary>
        /// Inclusive start offset.
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Exclusive end offset.
        /// </summary>
        public int End { get; set; }
    }
}
