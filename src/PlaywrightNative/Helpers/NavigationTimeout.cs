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
using System.Globalization;
using System.Threading;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>page.goto</c> / <c>frame.goto</c> timeout text.
    /// </summary>
    internal static class NavigationTimeout
    {
        private const float UnsetDefaultMs = 30_000;

        /// <summary>
        /// Builds the timeout exception upstream tests assert on.
        /// </summary>
        /// <param name="apiName">Public API name, e.g. <c>page.goto</c>.</param>
        /// <param name="url">Navigation URL.</param>
        /// <param name="waitUntil">Public waitUntil name (<c>load</c>, <c>networkidle</c>, ...).</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <returns>A timeout exception with the official message.</returns>
        internal static TimeoutException Exceeded(string apiName, string url, string waitUntil, int timeoutMs)
        {
            string ms = timeoutMs.ToString(CultureInfo.InvariantCulture);
            return new TimeoutException(
                apiName + ": Timeout " + ms + "ms exceeded." + Environment.NewLine
                + "navigating to \"" + url + "\", waiting until \"" + waitUntil + "\"");
        }

        /// <summary>
        /// Maps <see cref="WaitUntilState"/> to the official waitUntil token.
        /// </summary>
        /// <param name="waitUntil">The requested state.</param>
        /// <returns>The token used in timeout messages.</returns>
        internal static string WaitUntilName(WaitUntilState waitUntil)
        {
            return waitUntil switch
            {
                WaitUntilState.Commit => "commit",
                WaitUntilState.DOMContentLoaded => "domcontentloaded",
                WaitUntilState.NetworkIdle => "networkidle",
                _ => "load",
            };
        }

        /// <summary>
        /// Official <c>helper.completeUserURL</c>: assume <c>http://</c> for
        /// protocol-less localhost / 127.0.0.1 navigations.
        /// </summary>
        /// <param name="url">The user-supplied navigation URL.</param>
        /// <returns>The URL, with <c>http://</c> prepended when needed.</returns>
        internal static string CompleteUserUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            if (url.StartsWith("localhost", StringComparison.Ordinal)
                || url.StartsWith("127.0.0.1", StringComparison.Ordinal))
            {
                return "http://" + url;
            }

            return url;
        }

        /// <summary>
        /// Official network URLs omit the fragment; document navigations still
        /// keep it on <c>page.url()</c>.
        /// </summary>
        /// <param name="url">A request or response URL.</param>
        /// <returns>The URL without <c>#...</c>.</returns>
        internal static string WithoutHash(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            int hash = url.IndexOf('#');
            return hash >= 0 ? url.Substring(0, hash) : url;
        }

        /// <summary>
        /// CDP / WebKit frame URLs omit userinfo; compare navigations without it.
        /// </summary>
        /// <param name="url">A frame or requested URL.</param>
        /// <returns>The URL without <c>user:pass@</c>.</returns>
        internal static string WithoutUserInfo(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            int scheme = url.IndexOf("://", StringComparison.Ordinal);
            int at = url.IndexOf('@');
            if (scheme < 0 || at <= scheme)
            {
                return url;
            }

            return string.Concat(url.AsSpan(0, scheme + 3), url.AsSpan(at + 1));
        }

        /// <summary>
        /// Official <c>page.url()</c> keeps basic-auth credentials from the
        /// user-requested navigation URL.
        /// </summary>
        /// <param name="requestedUrl">The URL passed to <c>goto</c>.</param>
        /// <param name="currentUrl">The protocol-reported frame URL.</param>
        /// <returns>The current URL with userinfo restored when needed.</returns>
        internal static string PreserveUserInfo(string requestedUrl, string currentUrl)
        {
            if (string.IsNullOrEmpty(requestedUrl) || string.IsNullOrEmpty(currentUrl))
            {
                return currentUrl;
            }

            int scheme = requestedUrl.IndexOf("://", StringComparison.Ordinal);
            int at = requestedUrl.IndexOf('@');
            if (scheme < 0 || at <= scheme)
            {
                return currentUrl;
            }

            int currentScheme = currentUrl.IndexOf("://", StringComparison.Ordinal);
            if (currentScheme < 0)
            {
                return currentUrl;
            }

            ReadOnlySpan<char> afterScheme = currentUrl.AsSpan(currentScheme + 3);
            if (afterScheme.Contains('@'))
            {
                return currentUrl;
            }

            return string.Concat(
                currentUrl.AsSpan(0, currentScheme + 3),
                requestedUrl.AsSpan(scheme + 3, at - (scheme + 3)),
                "@",
                afterScheme);
        }

        /// <summary>
        /// Resolves the official navigation-timeout chain: explicit option,
        /// page navigation timeout, page timeout, context navigation timeout,
        /// context timeout, then 30s. <c>0</c> disables the timeout.
        /// </summary>
        /// <param name="explicitTimeout">Per-call timeout, or <see langword="null"/>.</param>
        /// <param name="pageNavigationTimeout">Page default navigation timeout.</param>
        /// <param name="pageTimeout">Page default timeout.</param>
        /// <param name="contextNavigationTimeout">Context default navigation timeout.</param>
        /// <param name="contextTimeout">Context default timeout.</param>
        /// <returns>Milliseconds suitable for <see cref="CancellationTokenSource"/>.</returns>
        internal static int ResolveMs(
            float? explicitTimeout,
            float pageNavigationTimeout,
            float pageTimeout,
            float contextNavigationTimeout,
            float contextTimeout)
        {
            if (explicitTimeout.HasValue)
            {
                return ToCtsMs(explicitTimeout.Value);
            }

            if (pageNavigationTimeout != UnsetDefaultMs)
            {
                return ToCtsMs(pageNavigationTimeout);
            }

            if (pageTimeout != UnsetDefaultMs)
            {
                return ToCtsMs(pageTimeout);
            }

            if (contextNavigationTimeout != UnsetDefaultMs)
            {
                return ToCtsMs(contextNavigationTimeout);
            }

            if (contextTimeout != UnsetDefaultMs)
            {
                return ToCtsMs(contextTimeout);
            }

            return (int)UnsetDefaultMs;
        }

        /// <summary>
        /// Chromium concatenates <c>Network.setExtraHTTPHeaders</c> referer with
        /// the <c>Page.navigate</c> referrer. When goto does not pass a referrer,
        /// reuse the extra header so intercepted requests see both values.
        /// </summary>
        /// <param name="referer">The <c>referer</c> option, or <see langword="null"/>.</param>
        /// <param name="extraHeaders">Page extra HTTP headers, or <see langword="null"/>.</param>
        /// <returns>The referrer to pass to navigate.</returns>
        internal static string ReferrerFromExtraHeaders(string referer, IReadOnlyDictionary<string, string> extraHeaders)
        {
            if (!string.IsNullOrEmpty(referer) || extraHeaders == null)
            {
                return referer;
            }

            foreach (KeyValuePair<string, string> header in extraHeaders)
            {
                if (string.Equals(header.Key, "referer", StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return referer;
        }

        /// <summary>
        /// Chromium concatenates <c>Network.setExtraHTTPHeaders</c> referer with
        /// the <c>Page.navigate</c> referrer as <c>"url, url"</c>. Emulate that
        /// when the intercepted header still has a single matching value.
        /// </summary>
        /// <param name="requestReferer">The intercepted <c>referer</c> header.</param>
        /// <param name="extraHeaders">Page extra HTTP headers, or <see langword="null"/>.</param>
        /// <returns>The concatenated referer, or <paramref name="requestReferer"/>.</returns>
        internal static string ConcatenateChromiumReferer(
            string requestReferer,
            IReadOnlyDictionary<string, string> extraHeaders)
        {
            string extraReferer = null;
            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> header in extraHeaders)
                {
                    if (string.Equals(header.Key, "referer", StringComparison.OrdinalIgnoreCase))
                    {
                        extraReferer = header.Value;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(extraReferer))
            {
                return requestReferer;
            }

            string current = string.IsNullOrEmpty(requestReferer) ? extraReferer : requestReferer;
            if (current.Contains(',', StringComparison.Ordinal))
            {
                return current;
            }

            return extraReferer + ", " + extraReferer;
        }

        /// <summary>
        /// Official <c>page.goto</c> rejection when both extra headers and the
        /// <c>referer</c> option specify a referer.
        /// </summary>
        /// <param name="url">Navigation URL (included in the exception).</param>
        /// <param name="referer">The <c>referer</c> option, or <see langword="null"/>.</param>
        /// <param name="extraHeaders">Page extra HTTP headers, or <see langword="null"/>.</param>
        internal static void ThrowIfRefererConflict(string url, string referer, IReadOnlyDictionary<string, string> extraHeaders)
        {
            if (string.IsNullOrEmpty(referer) || extraHeaders == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in extraHeaders)
            {
                if (string.Equals(header.Key, "referer", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(header.Value, referer, StringComparison.Ordinal))
                {
                    throw new NavigationException("\"referer\" is already specified as extra HTTP header", url);
                }
            }
        }

        private static int ToCtsMs(float timeout)
            => timeout <= 0 ? Timeout.Infinite : (int)timeout;
    }
}
