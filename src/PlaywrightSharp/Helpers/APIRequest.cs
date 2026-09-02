/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Factory for standalone <see cref="IAPIRequestContext"/> clients.
    /// </summary>
    internal sealed partial class APIRequest : IAPIRequest
    {
        /// <summary>
        /// Shared factory used by <see cref="Playwright.APIRequest"/>.
        /// </summary>
        internal static readonly APIRequest Instance = new APIRequest();

        /// <inheritdoc/>
        public Task<IAPIRequestContext> NewContextAsync(
            bool ignoreHTTPSErrors = false,
            IEnumerable<KeyValuePair<string, string>> extraHTTPHeaders = null,
            string baseURL = null,
            string userAgent = null,
            float? timeout = null,
            bool failOnStatusCode = false,
            int? maxRedirects = null,
            string storageState = null,
            string storageStatePath = null,
            IEnumerable<HttpCredentials> httpCredentials = null,
            Proxy proxy = null,
            IEnumerable<ClientCertificate> clientCertificates = null)
        {
            ClientCertificateHelper.Verify(clientCertificates);
            return Task.FromResult(APIRequestContext.CreateStandalone(
                ignoreHTTPSErrors,
                extraHTTPHeaders,
                baseURL,
                userAgent,
                timeout,
                failOnStatusCode,
                maxRedirects,
                storageState,
                storageStatePath,
                httpCredentials,
                proxy,
                clientCertificates));
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<IAPIRequestContext> IAPIRequest.NewContextAsync(APIRequestNewContextOptions options) => Task.FromResult<IAPIRequestContext>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
