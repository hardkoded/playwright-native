/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy fetch helpers over official <see cref="IAPIRequestContext"/>.
    /// </summary>
    public static class APIRequestContextCompatExtensions
    {
        /// <summary>Legacy fetch with method/headers/body named parameters.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> FetchAsync(
            this IAPIRequestContext context,
            IRequest request,
            string method = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string data = default,
            byte[] dataBytes = default,
            float? timeout = default)
            => context.FetchAsync(request, new APIRequestContextOptions
            {
                Method = method,
                Headers = headers,
                Data = data,
                DataByte = dataBytes,
                Timeout = timeout,
            });

        /// <summary>Legacy fetch with method/headers/body named parameters.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IAPIResponse> FetchAsync(
            this IAPIRequestContext context,
            string url,
            string method = default,
            IEnumerable<KeyValuePair<string, string>> headers = default,
            string data = default,
            byte[] dataBytes = default,
            float? timeout = default)
            => context.FetchAsync(url, new APIRequestContextOptions
            {
                Method = method,
                Headers = headers,
                Data = data,
                DataByte = dataBytes,
                Timeout = timeout,
            });
    }
}
