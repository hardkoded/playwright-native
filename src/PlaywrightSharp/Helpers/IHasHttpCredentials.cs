/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Exposes HTTP credentials stored on a browser context.
    /// </summary>
    internal interface IHasHttpCredentials
    {
        /// <summary>
        /// Credentials from <see cref="IBrowserContext.SetHttpCredentialsAsync"/> or
        /// <c>NewContextAsync(httpCredentials)</c>.
        /// </summary>
        IReadOnlyList<HttpCredentials> HttpCredentialsList { get; }
    }
}
