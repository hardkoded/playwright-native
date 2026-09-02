/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Exposes extra HTTP headers stored on a browser context.
    /// </summary>
    internal interface IHasExtraHttpHeaders
    {
        /// <summary>
        /// Headers from <see cref="IBrowserContext.SetExtraHttpHeadersAsync"/> or
        /// <c>NewContextAsync(extraHTTPHeaders)</c>, or <see langword="null"/>.
        /// </summary>
        IReadOnlyDictionary<string, string> ExtraHttpHeaders { get; }
    }

    /// <summary>
    /// Applies official context+page extra HTTP header merge.
    /// </summary>
    internal interface IAppliesMergedExtraHttpHeaders
    {
        /// <summary>
        /// Sends <c>context.extraHTTPHeaders</c> merged with this page's
        /// <c>setExtraHTTPHeaders</c> values.
        /// </summary>
        /// <returns>A task that completes when the protocol update is sent.</returns>
        Task ApplyMergedExtraHttpHeadersAsync();
    }
}
