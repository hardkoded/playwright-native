/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Clears cookies matching name/domain/path filters by expiring them in
    /// place (official <c>clearCookies</c> / PR 40955).
    /// </summary>
    internal static class CookieClearFilter
    {
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
                // the cookies that should remain.
                toExpire.Add(new Cookie
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
                });
            }

            if (toExpire.Count > 0)
            {
                await context.AddCookiesAsync(toExpire).ConfigureAwait(false);
            }
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
