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
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// No-op <see cref="ICoverage"/> for browsers without a coverage protocol.
    /// </summary>
    internal sealed class EmptyCoverage : ICoverage
    {
        /// <inheritdoc/>
        public Task StartJSCoverageAsync(bool resetOnNavigation = true, bool reportAnonymousScripts = false) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task<IReadOnlyList<JSCoverageEntry>> StopJSCoverageAsync()
            => Task.FromResult<IReadOnlyList<JSCoverageEntry>>(Array.Empty<JSCoverageEntry>());

        /// <inheritdoc/>
        public Task StartCSSCoverageAsync(bool resetOnNavigation = true) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task<IReadOnlyList<CSSCoverageEntry>> StopCSSCoverageAsync()
            => Task.FromResult<IReadOnlyList<CSSCoverageEntry>>(Array.Empty<CSSCoverageEntry>());
    }
}
