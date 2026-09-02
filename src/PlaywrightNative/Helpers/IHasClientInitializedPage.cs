/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>page.initializedOrUndefined()</c>. Dialogs that open
    /// before the first non-initial navigation report <c>dialog.page()</c>
    /// as <see langword="null"/>.
    /// </summary>
    internal interface IHasClientInitializedPage
    {
        /// <summary>
        /// True after the first non-initial main-frame navigation
        /// (official <c>reportAsNew</c>).
        /// </summary>
        bool IsClientInitialized { get; }
    }
}
