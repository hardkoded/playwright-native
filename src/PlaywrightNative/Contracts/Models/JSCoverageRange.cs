/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative
{
    /// <summary>
    /// A covered or uncovered span inside a script.
    /// </summary>
    public sealed class JSCoverageRange
    {
        /// <summary>
        /// Inclusive start offset in the script source.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// Exclusive end offset in the script source.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// How many times this span ran.
        /// </summary>
        public int Count { get; set; }
    }
}
