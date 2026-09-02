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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Resolves relative navigation URLs against a context <c>baseURL</c>.
    /// </summary>
    internal static class NavigationUrl
    {
        /// <summary>
        /// Returns the context <c>baseURL</c>, or <see langword="null"/>.
        /// </summary>
        /// <param name="context">The page's browser context.</param>
        /// <returns>The stored base URL, if any.</returns>
        internal static string ContextBase(IBrowserContext context)
            => context is IHasBaseUrl has ? has.BaseURL : null;

        /// <summary>
        /// Returns an absolute URL when <paramref name="url"/> is relative and
        /// <paramref name="context"/> has a base URL.
        /// </summary>
        /// <param name="context">The page's browser context.</param>
        /// <param name="url">The navigation target.</param>
        /// <returns>The resolved URL, or <paramref name="url"/> unchanged.</returns>
        internal static string Resolve(IBrowserContext context, string url)
        {
            if (string.IsNullOrEmpty(url) || HasScheme(url))
            {
                return url;
            }

            string baseUrl = ContextBase(context);
            if (string.IsNullOrEmpty(baseUrl))
            {
                return url;
            }

            // Official uses the WHATWG URL constructor: a base without a trailing
            // slash treats the last path segment as a file and replaces it.
            return Uri.TryCreate(new Uri(baseUrl, UriKind.Absolute), url, out Uri resolved)
                ? resolved.AbsoluteUri
                : url;
        }

        private static bool HasScheme(string url)
        {
            int colon = url.IndexOf(':');
            if (colon <= 0 || !char.IsLetter(url[0]))
            {
                return false;
            }

            for (int i = 1; i < colon; i++)
            {
                char ch = url[i];
                if (!char.IsLetterOrDigit(ch) && ch != '+' && ch != '-' && ch != '.')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
