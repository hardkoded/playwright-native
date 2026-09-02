/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Exposes the reason passed to <c>CloseAsync</c>.
    /// </summary>
    internal interface IHasCloseReason
    {
        /// <summary>
        /// Close reason, or <see langword="null"/> when none was supplied.
        /// </summary>
        string CloseReason { get; }
    }
}
