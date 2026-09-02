/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Exposes TLS client certificates stored on a browser context.
    /// </summary>
    internal interface IHasClientCertificates
    {
        /// <summary>
        /// Certificates from <c>NewContextAsync(clientCertificates)</c>, or
        /// <see langword="null"/>.
        /// </summary>
        IReadOnlyList<ClientCertificate> ClientCertificates { get; }
    }
}
