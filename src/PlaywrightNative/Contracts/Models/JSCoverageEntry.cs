/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;

namespace PlaywrightNative
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
