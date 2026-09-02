/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightSharp
{
    /// <summary>
    /// Coverage for a single JavaScript file.
    /// </summary>
    public sealed class JSCoverageEntry
    {
        /// <summary>
        /// Script URL, or the empty string for anonymous scripts.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Chromium <c>scriptId</c> from <c>Debugger.scriptParsed</c> /
        /// <c>Profiler.takePreciseCoverage</c>.
        /// </summary>
        public string ScriptId { get; set; }

        /// <summary>
        /// Script source when it could be retrieved.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Per-function coverage ranges.
        /// </summary>
        public IReadOnlyList<JSCoverageFunction> Functions { get; set; }
    }
}
