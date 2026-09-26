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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
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
        Task<IAPIRequestContext> IAPIRequest.NewContextAsync(APIRequestNewContextOptions options)
        {
            options ??= new APIRequestNewContextOptions();

            // Legacy options accept HttpCredentials[]; the official base property
            // is a single credential and stays null when an array of length != 1
            // is assigned. Prefer ResolveHttpCredentials for the full list.
            IEnumerable<HttpCredentials> credentials = options is PlaywrightNative.Compat.LegacyAPIRequestNewContextOptions legacy
                ? legacy.ResolveHttpCredentials()
                : options.HttpCredentials != null
                    ? new[] { options.HttpCredentials }
                    : null;

            return NewContextAsync(
                ignoreHTTPSErrors: options.IgnoreHTTPSErrors ?? false,
                extraHTTPHeaders: options.ExtraHTTPHeaders,
                baseURL: options.BaseURL,
                userAgent: options.UserAgent,
                timeout: options.Timeout,
                failOnStatusCode: options.FailOnStatusCode ?? false,
                maxRedirects: options.MaxRedirects,
                storageState: options.StorageState,
                storageStatePath: options.StorageStatePath,
                httpCredentials: credentials,
                proxy: options.Proxy,
                clientCertificates: options.ClientCertificates);
        }
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
