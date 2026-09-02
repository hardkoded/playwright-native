/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
