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
using System.Threading.Tasks;

namespace PlaywrightNative
{
    /// <summary>
    /// Collects JavaScript and CSS coverage for a page.
    /// </summary>
    public interface ICoverage
    {
        /// <summary>
        /// Starts JS coverage via Chromium <c>Profiler.startPreciseCoverage</c>.
        /// </summary>
        /// <param name="resetOnNavigation">
        /// When <see langword="true"/> (the default), collected script sources
        /// are discarded on navigation. Pass <see langword="false"/> to keep
        /// sources from previous documents.
        /// </param>
        /// <param name="reportAnonymousScripts">
        /// When <see langword="true"/>, include eval / anonymous scripts.
        /// Defaults to <see langword="false"/>.
        /// </param>
        /// <returns>A task that completes when coverage has been enabled.</returns>
        Task StartJSCoverageAsync(bool resetOnNavigation = true, bool reportAnonymousScripts = false);

        /// <summary>
        /// Stops JS coverage and returns the collected entries.
        /// </summary>
        /// <returns>One entry per script that reported coverage.</returns>
        Task<IReadOnlyList<JSCoverageEntry>> StopJSCoverageAsync();

        /// <summary>
        /// Starts CSS coverage via Chromium <c>CSS.startRuleUsageTracking</c>.
        /// </summary>
        /// <param name="resetOnNavigation">
        /// When <see langword="true"/> (the default), collected stylesheets
        /// are discarded on navigation. Pass <see langword="false"/> to keep
        /// styles from previous documents.
        /// </param>
        /// <returns>A task that completes when coverage has been enabled.</returns>
        Task StartCSSCoverageAsync(bool resetOnNavigation = true);

        /// <summary>
        /// Stops CSS coverage and returns the collected entries.
        /// </summary>
        /// <returns>One entry per stylesheet that reported coverage.</returns>
        Task<IReadOnlyList<CSSCoverageEntry>> StopCSSCoverageAsync();
    }
}
