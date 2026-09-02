/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Exposes the page that owns a WebSocket for wait-for-event abort.
    /// </summary>
    internal interface IHasOwnerPage
    {
        /// <summary>
        /// The owning page, or <see langword="null"/>.
        /// </summary>
        IPage OwnerPage { get; }
    }
}
