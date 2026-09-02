/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
