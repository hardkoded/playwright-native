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
    /// Coverage for one function inside a script.
    /// </summary>
    public sealed class JSCoverageFunction
    {
        /// <summary>
        /// Function name, or the empty string for top-level / anonymous functions.
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// Offset ranges and hit counts.
        /// </summary>
        public IReadOnlyList<JSCoverageRange> Ranges { get; set; }
    }
}
