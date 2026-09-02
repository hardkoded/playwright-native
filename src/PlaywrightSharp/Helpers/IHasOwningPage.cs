/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Server-side page for a request, including popup main-frame
    /// navigations whose public <see cref="IRequest.Frame"/> throws.
    /// </summary>
    internal interface IHasOwningPage
    {
        /// <summary>
        /// Page that issued the request, or <see langword="null"/>.
        /// </summary>
        IPage OwningPage { get; }
    }
}
