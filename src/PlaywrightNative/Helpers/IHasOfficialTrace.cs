/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Exposes the official Playwright action-trace session on a context.
    /// </summary>
    internal interface IHasOfficialTrace
    {
        /// <summary>The active official action-trace session, or <see langword="null"/>.</summary>
        OfficialTraceSession OfficialTrace { get; set; }
    }
}
