/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Options for a standalone <see cref="IAPIRequestContext"/>.
    /// </summary>
    internal sealed class APIRequestStandaloneOptions
    {
        /// <summary>
        /// Ignore TLS certificate errors.
        /// </summary>
        internal bool IgnoreHttpsErrors { get; set; }

        /// <summary>
        /// Headers sent with every request.
        /// </summary>
        internal IReadOnlyDictionary<string, string> ExtraHttpHeaders { get; set; }

        /// <summary>
        /// Base URL used to resolve relative request URLs.
        /// </summary>
        internal string BaseUrl { get; set; }

        /// <summary>
        /// Value for the User-Agent header.
        /// </summary>
        internal string UserAgent { get; set; }

        /// <summary>
        /// Default request timeout in milliseconds, or null for 30 seconds.
        /// </summary>
        internal int? TimeoutMs { get; set; }

        /// <summary>
        /// When true, a non-2xx status throws.
        /// </summary>
        internal bool FailOnStatusCode { get; set; }

        /// <summary>
        /// Default redirect limit, or null to follow automatically.
        /// </summary>
        internal int? MaxRedirects { get; set; }

        /// <summary>
        /// Cookies loaded from storage-state JSON.
        /// </summary>
        internal List<Cookie> Cookies { get; set; }

        /// <summary>
        /// Per-origin localStorage / IndexedDB loaded from storage-state JSON.
        /// Official standalone <c>request.storageState()</c> round-trips these.
        /// </summary>
        internal List<StorageStateOrigin> Origins { get; set; }

        /// <summary>
        /// HTTP Basic credentials, or null.
        /// </summary>
        internal IReadOnlyList<HttpCredentials> HttpCredentials { get; set; }

        /// <summary>
        /// Network proxy, or null.
        /// </summary>
        internal Proxy Proxy { get; set; }

        /// <summary>
        /// TLS client certificates, or null.
        /// </summary>
        internal IReadOnlyList<ClientCertificate> ClientCertificates { get; set; }
    }
}
