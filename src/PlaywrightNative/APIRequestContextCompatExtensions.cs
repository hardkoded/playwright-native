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
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy fetch helpers and cookie APIs over official <see cref="IAPIRequestContext"/>.
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

        /// <summary>
        /// Adds cookies into this request context. They will be sent with matching subsequent requests.
        /// For <see cref="IBrowserContext.APIRequest"/> and <see cref="IPage.APIRequest"/>, this is
        /// equivalent to calling <see cref="IBrowserContext.AddCookiesAsync"/> on the corresponding browser context.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="cookies">Cookies to add. Each cookie needs a url or a domain/path pair.</param>
        /// <returns>A task that completes when the cookies are stored.</returns>
        public static Task AddCookiesAsync(this IAPIRequestContext context, IEnumerable<Cookie> cookies)
            => AsImpl(context).AddCookiesAsync(cookies);

        /// <summary>
        /// Returns the cookies of this request context. If no URLs are specified, returns all cookies.
        /// If URLs are specified, only cookies that affect those URLs are returned.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="urls">Optional list of URLs.</param>
        /// <returns>The matching cookies.</returns>
        public static Task<IReadOnlyList<BrowserContextCookiesResult>> CookiesAsync(
            this IAPIRequestContext context,
            IEnumerable<string> urls = default)
            => AsImpl(context).CookiesAsync(urls);

        /// <summary>
        /// Returns the cookies of this request context that affect <paramref name="url"/>.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="url">A URL.</param>
        /// <returns>The matching cookies.</returns>
        public static Task<IReadOnlyList<BrowserContextCookiesResult>> CookiesAsync(
            this IAPIRequestContext context,
            string url)
            => AsImpl(context).CookiesAsync(new[] { url });

        /// <summary>
        /// Removes cookies from this request context. Without options, removes all cookies.
        /// For <see cref="IBrowserContext.APIRequest"/> and <see cref="IPage.APIRequest"/>, this is
        /// equivalent to calling <see cref="IBrowserContext.ClearCookiesAsync"/> on the corresponding browser context.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="options">Optional name / domain / path filters.</param>
        /// <returns>A task that completes when the matching cookies are removed.</returns>
        public static Task ClearCookiesAsync(
            this IAPIRequestContext context,
            BrowserContextClearCookiesOptions options = default)
            => AsImpl(context).ClearCookiesAsync(options);

        private static Helpers.APIRequestContext AsImpl(IAPIRequestContext context)
            => context as Helpers.APIRequestContext
                ?? throw new NotSupportedException("This request context does not support cookies.");
    }
}
