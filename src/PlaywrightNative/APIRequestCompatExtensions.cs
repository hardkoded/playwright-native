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
#pragma warning disable CA1062
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Compat;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy helpers over official <see cref="IAPIRequest"/> APIs.
    /// </summary>
    public static class APIRequestCompatExtensions
    {
        /// <summary>Legacy new context with user agent string.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIRequestContext> NewContextAsync(this IAPIRequest request, string userAgent)
            => request.NewContextAsync(new APIRequestNewContextOptions { UserAgent = userAgent });

        /// <summary>Legacy new context with user agent and extra headers.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIRequestContext> NewContextAsync(
            this IAPIRequest request,
            string userAgent,
            Dictionary<string, string> extraHTTPHeaders)
            => request.NewContextAsync(new APIRequestNewContextOptions
            {
                UserAgent = userAgent,
                ExtraHTTPHeaders = extraHTTPHeaders,
            });

        /// <summary>Legacy new context with extra HTTP headers dictionary.</summary>
        public static Task<IAPIRequestContext> NewContextAsync(
            this IAPIRequest request,
            Dictionary<string, string> extraHTTPHeaders)
            => request.NewContextAsync(new APIRequestNewContextOptions { ExtraHTTPHeaders = extraHTTPHeaders });

        /// <summary>Legacy new context with PlaywrightNative credential arrays.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIRequestContext> NewContextAsync(
            this IAPIRequest request,
            LegacyAPIRequestNewContextOptions options)
        {
            if (request is Helpers.APIRequest sharp)
            {
                return sharp.NewContextAsync(
                    options?.IgnoreHTTPSErrors ?? false,
                    options?.ExtraHTTPHeaders,
                    options?.BaseURL,
                    options?.UserAgent,
                    options?.Timeout,
                    options?.FailOnStatusCode ?? false,
                    options?.MaxRedirects,
                    options?.StorageState,
                    options?.StorageStatePath,
                    options?.ResolveHttpCredentials(),
                    options?.Proxy,
                    options?.ClientCertificates);
            }

            return request.NewContextAsync((Microsoft.Playwright.APIRequestNewContextOptions)options);
        }

        /// <summary>Legacy POST with json/data/headers named parameters.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> PostAsync(
            this IAPIRequestContext context,
            string url,
            Dictionary<string, string> headers = default,
            object json = default,
            string data = default,
            byte[] dataBytes = default)
            => context.PostAsync(url, new APIRequestContextOptions
            {
                Headers = headers,
                DataObject = json,
                Data = data,
                DataByte = dataBytes,
            });

        /// <summary>Legacy POST with dataBytes/headers/maxRetries named parameters.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> PostAsync(
            this IAPIRequestContext context,
            string url,
            byte[] dataBytes = default,
            Dictionary<string, string> headers = default,
            int? maxRetries = default)
            => context.PostAsync(url, new APIRequestContextOptions
            {
                DataByte = dataBytes,
                Headers = headers,
                MaxRetries = maxRetries,
            });

        /// <summary>Legacy POST with headers and raw bytes.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> PostAsync(
            this IAPIRequestContext context,
            string url,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            byte[] dataBytes = default)
            => context.PostAsync(url, new APIRequestContextOptions
            {
                Headers = headers,
                DataByte = dataBytes,
            });

        /// <summary>Legacy POST with raw bytes named parameter.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> PostAsync(
            this IAPIRequestContext context,
            string url,
            byte[] dataBytes,
            IEnumerable<KeyValuePair<string, string>> headers = default)
            => context.PostAsync(url, headers, dataBytes);

        /// <summary>Legacy POST with string body and named headers.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> PostAsync(
            this IAPIRequestContext context,
            string url,
            string data,
            IEnumerable<KeyValuePair<string, string>> headers = default)
            => context.PostAsync(url, new APIRequestContextOptions
            {
                Data = data,
                Headers = headers,
            });

        /// <summary>Legacy storage state path string.</summary>
        public static Task<string> StorageStateAsync(this IAPIRequestContext context, string path)
            => context.StorageStateAsync(new APIRequestContextStorageStateOptions { Path = path });

        /// <summary>Legacy GET with headers.</summary>
        public static Task<IAPIResponse> GetAsync(
            this IAPIRequestContext context,
            string url,
            IEnumerable<KeyValuePair<string, string>> headers = default)
            => context.GetAsync(url, new APIRequestContextOptions { Headers = headers });

        /// <summary>Legacy PUT with string body.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> PutAsync(this IAPIRequestContext context, string url, string data)
            => context.PutAsync(url, new APIRequestContextOptions { Data = data });
    }
}
