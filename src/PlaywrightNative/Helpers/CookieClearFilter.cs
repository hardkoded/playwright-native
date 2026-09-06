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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Clears cookies matching name/domain/path filters by expiring them in
    /// place (official <c>clearCookies</c> / PR 40955).
    /// </summary>
    internal static class CookieClearFilter
    {
        /// <summary>
        /// Clears cookies using official <see cref="BrowserContextClearCookiesOptions"/> filters.
        /// When every filter is omitted, invokes <paramref name="clearAll"/>.
        /// </summary>
        /// <param name="context">The browser context.</param>
        /// <param name="options">Optional name/domain/path filters.</param>
        /// <param name="clearAll">Callback that clears the entire cookie store.</param>
        /// <returns>A task that completes when matching cookies have been removed.</returns>
        internal static Task ClearAsync(
            IBrowserContext context,
            BrowserContextClearCookiesOptions options,
            Func<Task> clearAll)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (clearAll == null)
            {
                throw new ArgumentNullException(nameof(clearAll));
            }

            if (options == null)
            {
                return clearAll();
            }

            string name = FirstNonEmpty(options.Name, options.NameString);
            string domain = FirstNonEmpty(options.Domain, options.DomainString);
            string path = FirstNonEmpty(options.Path, options.PathString);
            Regex nameRegex = options.NameRegex;
            Regex domainRegex = options.DomainRegex;
            Regex pathRegex = options.PathRegex;

            if (string.IsNullOrEmpty(name)
                && string.IsNullOrEmpty(domain)
                && string.IsNullOrEmpty(path)
                && nameRegex == null
                && domainRegex == null
                && pathRegex == null)
            {
                return clearAll();
            }

            return ClearAsync(context, name, domain, path, nameRegex, domainRegex, pathRegex);
        }

        /// <summary>
        /// Deletes cookies that match any supplied filter. When every filter is
        /// omitted, clears the entire store.
        /// </summary>
        /// <param name="context">The browser context.</param>
        /// <param name="name">Cookie name to delete, or <see langword="null"/>.</param>
        /// <param name="domain">Cookie domain to delete, or <see langword="null"/>.</param>
        /// <param name="path">Cookie path to delete, or <see langword="null"/>.</param>
        /// <param name="nameRegex">Cookie-name regular expression, or <see langword="null"/>.</param>
        /// <param name="domainRegex">Cookie-domain regular expression, or <see langword="null"/>.</param>
        /// <param name="pathRegex">Cookie-path regular expression, or <see langword="null"/>.</param>
        /// <param name="url">
        /// Absolute URL whose cookies should be deleted, or <see langword="null"/>.
        /// </param>
        /// <returns>A task that completes when the store has been updated.</returns>
        internal static async Task ClearAsync(
            IBrowserContext context,
            string name,
            string domain,
            string path,
            Regex nameRegex = null,
            Regex domainRegex = null,
            Regex pathRegex = null,
            string url = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(name)
                && string.IsNullOrEmpty(domain)
                && string.IsNullOrEmpty(path)
                && nameRegex == null
                && domainRegex == null
                && pathRegex == null
                && string.IsNullOrEmpty(url))
            {
                await context.ClearCookiesAsync().ConfigureAwait(false);
                return;
            }

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            List<Cookie> toExpire = new List<Cookie>();
            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                if (cookie == null
                    || !Matches(cookie, name, domain, path, nameRegex, domainRegex, pathRegex, url))
                {
                    continue;
                }

                // Official clearCookies expires matching cookies in place
                // (expires: 0) so cookieStore.change does not see a wipe of
                // the cookies that should remain. Preserve partitionKey and
                // _crHasCrossSiteAncestor so CDP overwrites the same CHIPS row.
                Cookie expired = new Cookie
                {
                    Name = cookie.Name,
                    Value = string.Empty,
                    Domain = cookie.Domain,
                    Path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path,
                    Expires = 0,
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure,
                    SameSite = cookie.SameSite,
                    PartitionKey = string.IsNullOrEmpty(cookie.PartitionKey) ? null : cookie.PartitionKey,
                };
                CookieExtras.SetHasCrossSiteAncestor(
                    expired,
                    BrowserContextCookiesResultExtras.GetHasCrossSiteAncestor(cookie));
                toExpire.Add(expired);
            }

            if (toExpire.Count > 0)
            {
                await context.AddCookiesAsync(toExpire).ConfigureAwait(false);
            }
        }

        private static string FirstNonEmpty(string left, string right)
        {
            if (!string.IsNullOrEmpty(left))
            {
                return left;
            }

            return string.IsNullOrEmpty(right) ? null : right;
        }

        private static bool Matches(
            BrowserContextCookiesResult cookie,
            string name,
            string domain,
            string path,
            Regex nameRegex,
            Regex domainRegex,
            Regex pathRegex,
            string url)
        {
            if (nameRegex != null && (cookie.Name == null || !nameRegex.IsMatch(cookie.Name)))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(name)
                && !string.Equals(cookie.Name, name, StringComparison.Ordinal))
            {
                return false;
            }

            if (domainRegex != null && (cookie.Domain == null || !domainRegex.IsMatch(cookie.Domain)))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(domain)
                && !DomainEquals(cookie.Domain, domain))
            {
                return false;
            }

            if (pathRegex != null && (cookie.Path == null || !pathRegex.IsMatch(cookie.Path)))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(path)
                && !string.Equals(cookie.Path, path, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(url) && !ContextCookies.MatchesUrl(cookie, url))
            {
                return false;
            }

            return true;
        }

        private static bool DomainEquals(string left, string right)
        {
            string a = NormalizeDomain(left);
            string b = NormalizeDomain(right);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDomain(string domain)
        {
            if (string.IsNullOrEmpty(domain))
            {
                return string.Empty;
            }

            return domain[0] == '.' ? domain.Substring(1) : domain;
        }
    }
}
