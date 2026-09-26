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
            List<BrowserContextCookiesResult> matched = new List<BrowserContextCookiesResult>();
            foreach (BrowserContextCookiesResult cookie in cookies)
            {
                if (cookie == null
                    || !Matches(cookie, name, domain, path, nameRegex, domainRegex, pathRegex, url))
                {
                    continue;
                }

                matched.Add(cookie);
                AppendExpiredVariants(toExpire, cookie);
            }

            if (toExpire.Count == 0)
            {
                return;
            }

            await context.AddCookiesAsync(toExpire).ConfigureAwait(false);

            // Darwin/WebKit CFNetwork can leave getAllCookies / document.cookie rows
            // after a single expires:0 setCookies (ShouldRemoveCookiesByNameRegex on
            // macOS WebKit). Re-expire until the filter is empty, and clear matching
            // non-HttpOnly names via document.cookie Max-Age=0 on open pages so the
            // page jar matches the store without a navigation.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                IReadOnlyList<BrowserContextCookiesResult> leftover =
                    await context.GetCookiesAsync().ConfigureAwait(false);
                List<Cookie> again = new List<Cookie>();
                foreach (BrowserContextCookiesResult cookie in leftover)
                {
                    if (cookie == null
                        || !Matches(cookie, name, domain, path, nameRegex, domainRegex, pathRegex, url))
                    {
                        continue;
                    }

                    AppendExpiredVariants(again, cookie);
                }

                if (again.Count == 0)
                {
                    break;
                }

                await context.AddCookiesAsync(again).ConfigureAwait(false);
                await Task.Delay(25).ConfigureAwait(false);
            }

            await ClearMatchedDocumentCookiesAsync(context, matched).ConfigureAwait(false);
        }

        private static void AppendExpiredVariants(List<Cookie> toExpire, BrowserContextCookiesResult cookie)
        {
            // Official clearCookies expires matching cookies in place
            // (expires: 0) so cookieStore.change does not see a wipe of
            // the cookies that should remain. Preserve partitionKey and
            // _crHasCrossSiteAncestor so CDP overwrites the same CHIPS row.
            // Use the store's path string as-is so WebKit replaces the same
            // row (trailing-slash variants are matched above).
            string storePath = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
            bool? hasCrossSite = BrowserContextCookiesResultExtras.GetHasCrossSiteAncestor(cookie);
            string partitionKey = string.IsNullOrEmpty(cookie.PartitionKey) ? null : cookie.PartitionKey;

            void AddExpired(string domain, string cookiePath)
            {
                Cookie expired = new Cookie
                {
                    Name = cookie.Name,
                    Value = string.Empty,
                    Domain = domain,
                    Path = cookiePath,
                    Expires = 0,
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure,
                    SameSite = cookie.SameSite,
                    PartitionKey = partitionKey,
                };
                CookieExtras.SetHasCrossSiteAncestor(expired, hasCrossSite);
                toExpire.Add(expired);
            }

            AddExpired(cookie.Domain, storePath);

            // Leading-dot domain variants: Darwin may store host-only vs domain
            // cookies under different Domain strings for the same document.cookie.
            if (!string.IsNullOrEmpty(cookie.Domain))
            {
                string trimmed = cookie.Domain[0] == '.' ? cookie.Domain.Substring(1) : cookie.Domain;
                string dotted = cookie.Domain[0] == '.' ? cookie.Domain : "." + cookie.Domain;
                if (!string.Equals(trimmed, cookie.Domain, StringComparison.Ordinal))
                {
                    AddExpired(trimmed, storePath);
                }

                if (!string.Equals(dotted, cookie.Domain, StringComparison.Ordinal))
                {
                    AddExpired(dotted, storePath);
                }
            }

            // WebKit/soup may store directory paths with a trailing slash
            // while getAllCookies reports the trimmed form (or the reverse).
            // Expire both so ShouldRemoveCookiesByPath does not leave the
            // live document.cookie row behind under Linux suite load.
            if (storePath.Length > 1)
            {
                string altPath = storePath.EndsWith('/')
                    ? storePath.TrimEnd('/')
                    : storePath + "/";
                if (!string.Equals(altPath, storePath, StringComparison.Ordinal))
                {
                    AddExpired(cookie.Domain, altPath);
                }
            }
        }

        private static async Task ClearMatchedDocumentCookiesAsync(
            IBrowserContext context,
            List<BrowserContextCookiesResult> matched)
        {
            if (matched == null || matched.Count == 0)
            {
                return;
            }

            IReadOnlyList<IPage> pages;
            try
            {
                pages = context.Pages;
            }
            catch (PlaywrightException)
            {
                return;
            }

            if (pages == null || pages.Count == 0)
            {
                return;
            }

            foreach (IPage page in pages)
            {
                if (page == null || page.IsClosed)
                {
                    continue;
                }

                foreach (BrowserContextCookiesResult cookie in matched)
                {
                    if (cookie == null
                        || string.IsNullOrEmpty(cookie.Name)
                        || cookie.HttpOnly)
                    {
                        continue;
                    }

                    string storePath = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
                    string script =
                        "document.cookie = " +
                        System.Text.Json.JsonSerializer.Serialize(
                            cookie.Name + "=; Max-Age=0; path=" + storePath);
                    try
                    {
                        await page.EvaluateAsync(script).ConfigureAwait(false);
                    }
                    catch (PlaywrightException)
                    {
                    }
                    catch (TimeoutException)
                    {
                    }
                }
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
                && !PathEquals(cookie.Path, path))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(url) && !ContextCookies.MatchesUrl(cookie, url))
            {
                return false;
            }

            return true;
        }

        private static bool PathEquals(string left, string right)
            => string.Equals(NormalizeCookiePath(left), NormalizeCookiePath(right), StringComparison.Ordinal);

        /// <summary>
        /// WebKit/soup sometimes reports directory cookie paths with a trailing
        /// slash. Compare paths with a single trailing slash trimmed (except root).
        /// </summary>
        private static string NormalizeCookiePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }

            if (path.Length > 1 && path.EndsWith('/'))
            {
                return path.TrimEnd('/');
            }

            return path;
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
